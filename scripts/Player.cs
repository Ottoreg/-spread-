using Godot;

/// <summary>
/// Le joueur : le virus. Contrôle twin-stick — déplacement clavier (WASD/flèches),
/// visée à la souris, tir maintenu. Rendu fil-de-fer via _Draw (une seule entité,
/// donc pas besoin d'instancing ici). Le virus infecte en tirant des projectiles.
/// </summary>
public partial class Player : Node2D
{
    public float Health { get; private set; } = Config.PlayerMaxHealth;
    public Vector2 Velocity { get; private set; }
    public Vector2 AimDir { get; private set; } = Vector2.Right;
    public OrganMap Map;

    private float _fireCooldown;

    public override void _Ready()
    {
        ZIndex = 10; // au-dessus des anticorps
    }

    /// <summary>Met à jour déplacement, visée et tir. Retourne au pool les projectiles tirés.</summary>
    public void Tick(float dt, Projectiles projectiles)
    {
        // Déplacement (stick gauche)
        Vector2 move = Vector2.Zero;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) move.Y -= 1;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) move.Y += 1;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) move.X -= 1;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) move.X += 1;

        Velocity = (move == Vector2.Zero ? Vector2.Zero : move.Normalized() * Config.PlayerSpeed);
        Position = Map != null
            ? Map.Slide(Position, Velocity * dt)
            : new Vector2(
                Mathf.Clamp(Position.X + Velocity.X * dt, 0f, Config.WorldWidth),
                Mathf.Clamp(Position.Y + Velocity.Y * dt, 0f, Config.WorldHeight));

        // Visée (stick droit = souris)
        Vector2 toMouse = GetGlobalMousePosition() - Position;
        if (toMouse.LengthSquared() > 1f)
            AimDir = toMouse.Normalized();

        // Tir maintenu
        _fireCooldown -= dt;
        if (Input.IsMouseButtonPressed(MouseButton.Left) && _fireCooldown <= 0f)
        {
            _fireCooldown = Config.FireInterval;
            projectiles.Spawn(Position + AimDir * Config.PlayerRadius,
                              AimDir * Config.ProjectileSpeed);
        }

        QueueRedraw(); // met à jour l'orientation dessinée
    }

    public void TakeDamage(float amount)
    {
        Health = Mathf.Max(0f, Health - amount);
    }

    public void Heal()
    {
        Health = Config.PlayerMaxHealth;
    }

    public bool IsDead => Health <= 0f;

    public override void _Draw()
    {
        // Triangle fil-de-fer orienté vers la visée.
        float r = Config.PlayerRadius;
        float a = AimDir.Angle();
        Vector2 tip = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * 1.4f;
        Vector2 left = new Vector2(Mathf.Cos(a + 2.5f), Mathf.Sin(a + 2.5f)) * r;
        Vector2 right = new Vector2(Mathf.Cos(a - 2.5f), Mathf.Sin(a - 2.5f)) * r;

        var pts = new Vector2[] { tip, left, right, tip };
        DrawPolyline(pts, new Color(0.4f, 1f, 0.5f), 2f, true);
    }
}
