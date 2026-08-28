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
    private OrganMap _map;
    private Simulation _sim;
    private SpatialHashGrid _grid;
    private FlowField _flow;
    private Player _player;
    private readonly Skills _skills = new();

    private MapRenderer _mapRenderer;
    private EntityRenderer _entityRenderer;
    private Camera2D _camera;
    private DebugHud _hud;
    private SkillTreeHud _skillHud;

    /// <summary>Graine de génération de la carte (fixée par le menu d'accueil).</summary>
    public ulong Seed = Config.MapSeed;

    private int _tick;
    private float _time;
    private int _flowRetarget;
    private float _spawnTimer;

    // Piste (traque)
    private Vector2 _lastMovePos;
    private float _trailAge;
    private bool _trailFresh = true;

    // Positions de burst de viro-cellules (réutilisé, zéro alloc par frame)
    private readonly System.Collections.Generic.List<Vector2> _bursts = new();

    // Options runtime
    public bool LodEnabled = true;
    public bool FpsCapped;
    public bool Multithread = true;

    // Stats gameplay
    public int TotalInfections;
    public int AdnOffensive, AdnSurvival, AdnReinforce;
    public float Alert;

    // Profiling (ms)
    public double MsGrid, MsBehavior, MsIntegrate, MsCollision, MsProjectiles, MsRender;

    public int Capacity => _sim.Capacity;
    public int EntityCount => _sim.Count;
    public int ActivatedCount => _sim.ActivatedCount;
    public int VisibleCount => _entityRenderer.VisibleCount;
    public float PlayerHealth => _player.Health;
    public float AlertPct => Alert / Config.AlertMax * 100f;

    public override void _Ready()
    {
        _map = new OrganMap(Config.WorldWidth, Config.WorldHeight, Config.MapCellSize, Seed);

        _sim = new Simulation(Config.Capacity);
        _sim.SetMap(_map);
        _sim.SetCount(Config.InitialEntityCount);

        _grid = new SpatialHashGrid(Config.WorldWidth, Config.WorldHeight,
                                    Config.CellSize, Config.Capacity);

        _flow = new FlowField(_map);

        _player = new Player { Position = _map.SpawnPoint, Map = _map, Skills = _skills };
        AddChild(_player);
        _lastMovePos = _player.Position;
        _flow.SetTargetWorld(_player.Position, _sim);

        _camera = new Camera2D { Position = _player.Position, Zoom = new Vector2(1f, 1f) };
        AddChild(_camera);
        _camera.MakeCurrent();

        _mapRenderer = new MapRenderer { Map = _map };
        AddChild(_mapRenderer);

        _entityRenderer = new EntityRenderer();
        AddChild(_entityRenderer);
        _entityRenderer.Init(Config.Capacity);

        var layer = new CanvasLayer();
        AddChild(layer);
        _hud = new DebugHud { Game = this };
        layer.AddChild(_hud);
        _skillHud = new SkillTreeHud { Game = this };
        layer.AddChild(_skillHud);

        // Fond = tissu de l'hôte (sombre). Les cavités des organes, plus claires,
        // se détachent comme des chambres creusées dans la chair.
        RenderingServer.SetDefaultClearColor(new Color(0.09f, 0.035f, 0.05f));

        Engine.MaxFps = 0;

        // Anti "spirale de la mort" : ne jamais empiler plusieurs pas physiques
        // par image. Au-delà du budget, le jeu ralentit doucement au lieu de
        // s'effondrer à 0 FPS (dégradation gracieuse, benchmark lisible).
        Engine.MaxPhysicsStepsPerFrame = 1;
    }

    public override void _PhysicsProcess(double delta)
    {
        // Simulation en pause quand l'arbre de compétences est ouvert.
        if (_skillHud != null && _skillHud.IsOpen) return;

        float dt = (float)delta;
        _tick++;
        _time += dt;

        _player.Tick(dt);

        // Piste : fraîche tant que le joueur bouge. S'il reste immobile trop
        // longtemps, les défenses activées perdent sa trace.
        if (_player.Position.DistanceTo(_lastMovePos) > Config.TrailMoveThreshold)
        {
            _lastMovePos = _player.Position;
            _trailAge = 0f;
        }
        else _trailAge += dt;
        _trailFresh = _trailAge < Config.TrailLifetime;

        // Pathfinding de masse : on re-cible le flow field sur le joueur
        // périodiquement, en contournant les viro-cellules (obstacles dynamiques).
        if (--_flowRetarget <= 0)
        {
            _flow.SetTargetWorld(_player.Position, _sim);
            _flowRetarget = 8;
        }

        Rect2 active = GetVisibleWorldRect();
        var sw = Stopwatch.StartNew();

        _grid.Rebuild(_sim.Position, _sim.Count);
        MsGrid = Lap(sw);

        // Combat de mêlée : résolu sur la grille fraîche quand un coup part.
        AdnGain gain = default;
        if (_player.AttackFired)
            gain = MeleeSystem.Attack(_sim, _grid, _player.Position, _player.AimDir, _skills.Damage);

        CellSystem.Run(_sim, _grid, _flow, _player.Position, LodEnabled, active, _tick, _time, _trailFresh, Multithread);
        MsBehavior = Lap(sw);

        _sim.Integrate(dt, Multithread);
        MsIntegrate = Lap(sw);

        CollisionSystem.Run(_sim, _grid, _map, Multithread);
        MsCollision = Lap(sw);

        _bursts.Clear();
        gain.Add(ViroCellSystem.Run(_sim, dt, _bursts));
        _sim.CompactDead();
        ApplyGain(gain);
        MsProjectiles = Lap(sw);

        ApplyContactInteractions(dt);
        UpdateAlertAndSpawns(dt);
        SpawnBursts();

        _camera.Position = _player.Position;
    }

    // Explosion de particules à la fin d'incubation d'une viro-cellule (culling caméra).
    private void SpawnBursts()
    {
        if (_bursts.Count == 0) return;
        Rect2 view = GetVisibleWorldRect();
        foreach (var p in _bursts)
            if (view.HasPoint(p))
                SpawnBurst(p);
    }

    private void SpawnBurst(Vector2 pos)
    {
        var fx = new CpuParticles2D
        {
            Position = pos,
            ZIndex = 6,
            Emitting = true,
            OneShot = true,
            Explosiveness = 1f,
            Amount = 14,
            Lifetime = 0.5f,
            Direction = Vector2.Up,
            Spread = 180f,
            Gravity = Vector2.Zero,
            InitialVelocityMin = 70f,
            InitialVelocityMax = 170f,
            ScaleAmountMin = 1.5f,
            ScaleAmountMax = 2.5f,
            Color = new Color(0.45f, 1f, 0.5f)
        };
        AddChild(fx);
        // Libération automatique après la durée de vie.
        var timer = GetTree().CreateTimer(1.0);
        timer.Timeout += fx.QueueFree;
    }

    private void ApplyGain(in AdnGain g)
    {
        AdnOffensive += g.Offensive;
        AdnSurvival += g.Survival;
        AdnReinforce += g.Reinforce;
        TotalInfections += g.Infections;
        Alert = Mathf.Min(Config.AlertMax, Alert + g.Alert);
    }

    // Alerte : décroît lentement ; pilote la cadence de production de défenses.
    private void UpdateAlertAndSpawns(float dt)
    {
        Alert = Mathf.Max(0f, Alert - Config.AlertDecayPerSec * dt);

        float t = Alert / Config.AlertMax;
        float interval = Mathf.Lerp(Config.AlertSpawnSlow, Config.AlertSpawnFast, t);

        _spawnTimer -= dt;
        if (_spawnTimer <= 0f)
        {
            _spawnTimer = interval;
            SpawnDefenseFromAlert();
        }
    }

    private void SpawnDefenseFromAlert()
    {
        if (_map.Organs.Count == 0) return;
        var o = _map.Organs[(int)(GD.Randf() * _map.Organs.Count) % _map.Organs.Count];
        float ang = GD.Randf() * Mathf.Tau;
        float r = GD.Randf() * o.Radius * 0.8f;
        Vector2 pos = o.Center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
        _sim.SpawnCell(pos, CellKind.Defensive);
    }

    public override void _Process(double delta)
    {
        var sw = Stopwatch.StartNew();
        _entityRenderer.UpdateInstances(_sim, GetVisibleWorldRect());
        MsRender = sw.Elapsed.TotalMilliseconds;

        _hud.Refresh();
        _skillHud.Refresh();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp) SetZoom(_camera.Zoom.X * 1.1f);
            else if (mb.ButtonIndex == MouseButton.WheelDown) SetZoom(_camera.Zoom.X / 1.1f);
        }
        else if (e is InputEventKey k && k.Pressed)
        {
            if (k.Keycode == Key.R) ResetPlayer();
            else if (k.Keycode == Key.Escape) _hud.ToggleAdmin();
            else if (k.Keycode == Key.C) _skillHud.Toggle();
        }
    }

    // --- Contact joueur : défensives activées blessent ; proies s'infectent ---
    private void ApplyContactInteractions(float dt)
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
        var kind = _sim.Kind;

        int contacts = 0;
        AdnGain gain = default;

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
                    if (state[j] == Simulation.Infected) continue;
                    Vector2 d = pos[j] - pp;
                    if (d.X * d.X + d.Y * d.Y > reach2) continue;

                    if (kind[j] == CellKind.Defensive && state[j] == Simulation.Activated)
                        contacts++;
                    else if (kind[j] == CellKind.Prey)
                    {
                        _sim.Infect(j); // cellule d'organe : infectée au contact
                        gain.Survival += Config.AdnPreyInfect;
                        gain.Alert += Config.AlertPerPrey;
                        gain.Infections++;
                    }
                }
            }
        }

        if (contacts > 0)
            _player.TakeDamage(Config.PlayerContactDps * dt * Mathf.Min(contacts, 6));
        if (gain.Infections > 0)
            ApplyGain(gain);
    }

    private void ResetPlayer()
    {
        _skills.Reset();
        _player.Position = _map.SpawnPoint;
        _player.Heal();
        Alert = 0f;
        AdnOffensive = AdnSurvival = AdnReinforce = 0;
        TotalInfections = 0;
        _sim.SetCount(0);
        _sim.SetCount(Config.InitialEntityCount);
    }

    // --- API arbre de compétences ---
    public Skills PlayerSkills => _skills;

    public bool CanAfford(int branch)
    {
        if (_skills.IsMaxed(branch)) return false;
        int cost = _skills.NextCost(branch);
        int primary = _skills.IsOffensive(branch) ? AdnOffensive : AdnSurvival;
        return primary + AdnReinforce >= cost; // le renforcement complète
    }

    public bool TryUpgradeSkill(int branch)
    {
        if (!CanAfford(branch)) return false;

        int cost = _skills.NextCost(branch);
        ref int primary = ref (_skills.IsOffensive(branch) ? ref AdnOffensive : ref AdnSurvival);

        int fromPrimary = Mathf.Min(cost, primary);
        int rem = cost - fromPrimary;      // complété par l'ADN de renforcement
        primary -= fromPrimary;
        AdnReinforce -= rem;

        _skills.Level[branch]++;
        return true;
    }

    // --- API HUD ---
    public void SetEntityCount(int n) => _sim.SetCount(n);
    public void ToggleLod() => LodEnabled = !LodEnabled;
    public void ToggleThreads() => Multithread = !Multithread;
    public int ThreadCount => System.Environment.ProcessorCount;

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
