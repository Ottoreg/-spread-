using Godot;
using System.Diagnostics;

/// <summary>
/// Orchestrateur du prototype-benchmark. Racine de la scène.
///
/// - Détient l'état de simulation (SoA), la grille spatiale, le flow field.
/// - Fait tourner les systèmes dans un ordre fixe, chaque tick (_PhysicsProcess,
///   timestep fixe = déterministe, base saine pour un futur multijoueur).
/// - Construit caméra, rendu et HUD en code (scène minimale).
/// - Mesure le temps de chaque système pour le profiling.
/// </summary>
public partial class Game : Node2D
{
    private Simulation _sim;
    private SpatialHashGrid _grid;
    private FlowField _flow;
    private EntityRenderer _renderer;
    private Camera2D _camera;
    private DebugHud _hud;

    private int _tick;

    // Options runtime
    public bool LodEnabled;
    public bool FpsCapped;

    // Profiling (ms) — lus par le HUD
    public double MsGrid, MsFsm, MsFlock, MsIntegrate, MsCollision, MsRender;

    public int Capacity => _sim.Capacity;
    public int EntityCount => _sim.Count;
    public int VisibleCount => _renderer.VisibleCount;

    public override void _Ready()
    {
        _sim = new Simulation(Config.Capacity);
        _sim.SetCount(Config.InitialEntityCount);

        _grid = new SpatialHashGrid(Config.WorldWidth, Config.WorldHeight,
                                    Config.CellSize, Config.Capacity);

        // Flow field sur une grille plus grossière (2x la cellule spatiale).
        _flow = new FlowField(Config.WorldWidth, Config.WorldHeight, Config.CellSize * 2f);
        _flow.SetTargetWorld(new Vector2(Config.WorldWidth * 0.5f, Config.WorldHeight * 0.5f));

        _camera = new Camera2D
        {
            Position = new Vector2(Config.WorldWidth * 0.5f, Config.WorldHeight * 0.5f),
            Zoom = new Vector2(0.5f, 0.5f)
        };
        AddChild(_camera);
        _camera.MakeCurrent();

        _renderer = new EntityRenderer();
        AddChild(_renderer);
        _renderer.Init(Config.Capacity);

        var layer = new CanvasLayer();
        AddChild(layer);
        _hud = new DebugHud { Game = this };
        layer.AddChild(_hud);

        Engine.MaxFps = 0; // non plafonné pour mesurer le pic
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _tick++;

        Rect2 active = GetVisibleWorldRect();
        var sw = Stopwatch.StartNew();

        _grid.Rebuild(_sim.Position, _sim.Count);
        MsGrid = Lap(sw);

        StateMachineSystem.Run(_sim, dt);
        MsFsm = Lap(sw);

        FlockingSystem.Run(_sim, _grid, _flow, LodEnabled, active, _tick);
        MsFlock = Lap(sw);

        _sim.Integrate(dt);
        MsIntegrate = Lap(sw);

        CollisionSystem.Run(_sim, _grid);
        MsCollision = Lap(sw);
    }

    public override void _Process(double delta)
    {
        HandlePan((float)delta);

        var sw = Stopwatch.StartNew();
        _renderer.UpdateInstances(_sim, GetVisibleWorldRect());
        MsRender = sw.Elapsed.TotalMilliseconds;

        _hud.Refresh();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventMouseButton mb && mb.Pressed)
        {
            switch (mb.ButtonIndex)
            {
                case MouseButton.WheelUp:
                    SetZoom(_camera.Zoom.X * 1.1f);
                    break;
                case MouseButton.WheelDown:
                    SetZoom(_camera.Zoom.X / 1.1f);
                    break;
                case MouseButton.Left:
                    _flow.SetTargetWorld(GetGlobalMousePosition());
                    break;
            }
        }
    }

    // --- API pour le HUD ---
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

    private void HandlePan(float dt)
    {
        Vector2 move = Vector2.Zero;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) move.Y -= 1;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) move.Y += 1;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) move.X -= 1;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) move.X += 1;

        if (move != Vector2.Zero)
            _camera.Position += move.Normalized() * (700f / _camera.Zoom.X) * dt;
    }

    private void SetZoom(float z)
    {
        z = Mathf.Clamp(z, 0.05f, 4f);
        _camera.Zoom = new Vector2(z, z);
    }

    private Rect2 GetVisibleWorldRect()
    {
        Vector2 size = GetViewport().GetVisibleRect().Size / _camera.Zoom;
        Vector2 topLeft = _camera.GetScreenCenterPosition() - size * 0.5f;
        return new Rect2(topLeft, size).Grow(Config.EntityRadius * 2f);
    }
}
