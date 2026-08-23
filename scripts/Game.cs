using Godot;
using System.Diagnostics;

/// <summary>
/// Orchestrateur du prototype. Racine de la scène.
///
/// Concept : twin-stick roguelike. Le joueur (virus) se déplace dans une grande
/// map ouverte peuplée de milliers d'anticorps DORMANTS qui errent ; ceux qui
/// entrent dans le rayon d'activation se réveillent et poursuivent le joueur en
/// nuée. Le joueur tire des projectiles d'infection qui les détruisent.
///
/// Boucle en timestep fixe (_PhysicsProcess) = déterministe, base saine pour un
/// futur multijoueur. Caméra qui suit le joueur. Mesure du temps par système.
/// </summary>
public partial class Game : Node2D
{
    private Simulation _sim;
    private SpatialHashGrid _grid;
    private FlowField _flow;
    private Projectiles _proj;
    private Player _player;

    private EntityRenderer _entityRenderer;
    private ProjectileRenderer _projRenderer;
    private Camera2D _camera;
    private DebugHud _hud;

    private int _tick;
    private float _time;
    private int _flowRetarget;

    // Options runtime
    public bool LodEnabled = true;
    public bool FpsCapped;

    // Stats gameplay
    public int TotalKills;

    // Profiling (ms)
    public double MsGrid, MsBehavior, MsIntegrate, MsCollision, MsProjectiles, MsRender;

    public int Capacity => _sim.Capacity;
    public int EntityCount => _sim.Count;
    public int ActivatedCount => _sim.ActivatedCount;
    public int VisibleCount => _entityRenderer.VisibleCount;
    public int ProjectileCount => _proj.Count;
    public float PlayerHealth => _player.Health;

    public override void _Ready()
    {
        _sim = new Simulation(Config.Capacity);
        _sim.SetCount(Config.InitialEntityCount);

        _grid = new SpatialHashGrid(Config.WorldWidth, Config.WorldHeight,
                                    Config.CellSize, Config.Capacity);

        _flow = new FlowField(Config.WorldWidth, Config.WorldHeight, Config.CellSize * 2f);

        _proj = new Projectiles(Config.ProjectileCapacity);

        _player = new Player { Position = new Vector2(Config.WorldWidth * 0.5f, Config.WorldHeight * 0.5f) };
        AddChild(_player);
        _flow.SetTargetWorld(_player.Position);

        _camera = new Camera2D { Position = _player.Position, Zoom = new Vector2(1f, 1f) };
        AddChild(_camera);
        _camera.MakeCurrent();

        _entityRenderer = new EntityRenderer();
        AddChild(_entityRenderer);
        _entityRenderer.Init(Config.Capacity);

        _projRenderer = new ProjectileRenderer();
        AddChild(_projRenderer);
        _projRenderer.Init(Config.ProjectileCapacity);

        var layer = new CanvasLayer();
        AddChild(layer);
        _hud = new DebugHud { Game = this };
        layer.AddChild(_hud);

        Engine.MaxFps = 0;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _tick++;
        _time += dt;

        _player.Tick(dt, _proj);

        // Pathfinding de masse : on re-cible le flow field sur le joueur
        // périodiquement (le joueur bouge) — coûteux mais amorti sur plusieurs ticks.
        if (--_flowRetarget <= 0)
        {
            _flow.SetTargetWorld(_player.Position);
            _flowRetarget = 8;
        }

        Rect2 active = GetVisibleWorldRect();
        var sw = Stopwatch.StartNew();

        _grid.Rebuild(_sim.Position, _sim.Count);
        MsGrid = Lap(sw);

        AntibodySystem.Run(_sim, _grid, _flow, _player.Position, LodEnabled, active, _tick, _time);
        MsBehavior = Lap(sw);

        _sim.Integrate(dt);
        MsIntegrate = Lap(sw);

        CollisionSystem.Run(_sim, _grid);
        MsCollision = Lap(sw);

        TotalKills += ProjectileSystem.Run(_proj, _sim, _grid, dt);
        _sim.CompactDead();
        MsProjectiles = Lap(sw);

        ApplyContactDamage(dt);

        _camera.Position = _player.Position;
    }

    public override void _Process(double delta)
    {
        var sw = Stopwatch.StartNew();
        _entityRenderer.UpdateInstances(_sim, GetVisibleWorldRect());
        _projRenderer.UpdateInstances(_proj);
        MsRender = sw.Elapsed.TotalMilliseconds;

        _hud.Refresh();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp) SetZoom(_camera.Zoom.X * 1.1f);
            else if (mb.ButtonIndex == MouseButton.WheelDown) SetZoom(_camera.Zoom.X / 1.1f);
        }
        else if (e is InputEventKey k && k.Pressed && k.Keycode == Key.R)
        {
            ResetPlayer();
        }
    }

    // --- Dégâts de contact : anticorps activés qui touchent le joueur ---
    private void ApplyContactDamage(float dt)
    {
        Vector2 pp = _player.Position;
        float reach = Config.PlayerRadius + Config.EntityRadius + 2f;
        float reach2 = reach * reach;

        int cell = _grid.CellIndex(pp);
        int cols = _grid.Cols, rows = _grid.Rows;
        int cx = cell % cols, cy = cell / cols;
        var cellStart = _grid.CellStart;
        var entities = _grid.Entities;
        var pos = _sim.Position;
        var state = _sim.State;

        int contacts = 0;
        for (int oy = -1; oy <= 1; oy++)
        {
            int ny = cy + oy;
            if (ny < 0 || ny >= rows) continue;
            for (int ox = -1; ox <= 1; ox++)
            {
                int nx = cx + ox;
                if (nx < 0 || nx >= cols) continue;
                int nc = ny * cols + nx;
                for (int kk = cellStart[nc]; kk < cellStart[nc + 1]; kk++)
                {
                    int j = entities[kk];
                    if (state[j] != Simulation.Activated) continue;
                    Vector2 d = pos[j] - pp;
                    if (d.X * d.X + d.Y * d.Y <= reach2) contacts++;
                }
            }
        }

        if (contacts > 0)
            _player.TakeDamage(Config.PlayerContactDps * dt * Mathf.Min(contacts, 6));
    }

    private void ResetPlayer()
    {
        _player.Position = new Vector2(Config.WorldWidth * 0.5f, Config.WorldHeight * 0.5f);
        _player.Heal();
        var state = _sim.State;
        for (int i = 0; i < _sim.Count; i++) state[i] = Simulation.Dormant;
    }

    // --- API HUD ---
    public void SetEntityCount(int n) => _sim.SetCount(n);
    public void ToggleLod() => LodEnabled = !LodEnabled;

    public void ToggleFpsCap()
    {
        FpsCapped = !FpsCapped;
        Engine.MaxFps = FpsCapped ? 60 : 0;
    }

    // --- Utilitaires ---
    private static double Lap(Stopwatch sw)
    {
        double ms = sw.Elapsed.TotalMilliseconds;
        sw.Restart();
        return ms;
    }

    private void SetZoom(float z)
    {
        z = Mathf.Clamp(z, 0.1f, 3f);
        _camera.Zoom = new Vector2(z, z);
    }

    private Rect2 GetVisibleWorldRect()
    {
        Vector2 size = GetViewport().GetVisibleRect().Size / _camera.Zoom;
        Vector2 topLeft = _camera.GetScreenCenterPosition() - size * 0.5f;
        return new Rect2(topLeft, size).Grow(Config.EntityRadius * 2f);
    }
}
