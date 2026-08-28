using Godot;

/// <summary>
/// Le joueur : le virus. Contrôle twin-stick — déplacement ZQSD/flèches, visée à
/// la souris. Attaque par défaut = coup de lame de MÊLÉE (clic gauche maintenu) :
/// un arc horizontal devant le virus. Rendu fil-de-fer via _Draw (une entité).
/// </summary>
public partial class Player : Node2D
{
    public float Health { get; private set; } = Config.PlayerMaxHealth;
    public Vector2 Velocity { get; private set; }
    public Vector2 AimDir { get; private set; } = Vector2.Right;
    public OrganMap Map;
    public Skills Skills;

    public bool AttackFired { get; private set; } // vrai le tick où un coup part

    private float _attackCooldown;
    private float _slashTime;   // temps restant d'animation du coup
    private float _swingSign = 1f;

    private float MoveSpeed => Skills?.MoveSpeed ?? Config.PlayerSpeed;
    private float AttackInterval => Skills?.AttackInterval ?? Config.AttackInterval;
    private float MaxHp => Skills?.MaxHealth ?? Config.PlayerMaxHealth;

    public override void _Ready()
    {
        ZIndex = 10; // au-dessus des cellules
    }

    public void Tick(float dt)
    {
        // Déplacement ZQSD (AZERTY) + flèches
        Vector2 move = Vector2.Zero;
        if (Input.IsKeyPressed(Key.Z) || Input.IsKeyPressed(Key.Up)) move.Y -= 1;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) move.Y += 1;
        if (Input.IsKeyPressed(Key.Q) || Input.IsKeyPressed(Key.Left)) move.X -= 1;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) move.X += 1;

        Velocity = (move == Vector2.Zero ? Vector2.Zero : move.Normalized() * MoveSpeed);
        Position = Map != null
            ? Map.Slide(Position, Velocity * dt)
            : new Vector2(
                Mathf.Clamp(Position.X + Velocity.X * dt, 0f, Config.WorldWidth),
                Mathf.Clamp(Position.Y + Velocity.Y * dt, 0f, Config.WorldHeight));

        // Visée (stick droit = souris)
        Vector2 toMouse = GetGlobalMousePosition() - Position;
        if (toMouse.LengthSquared() > 1f)
            AimDir = toMouse.Normalized();

        // Régénération (compétence Régénération)
        float regen = Skills?.RegenPerSec ?? 0f;
        if (regen > 0f && Health > 0f)
            Health = Mathf.Min(MaxHp, Health + regen * dt);

        // Attaque de mêlée (clic gauche maintenu)
        AttackFired = false;
        _attackCooldown -= dt;
        if (Input.IsMouseButtonPressed(MouseButton.Left) && _attackCooldown <= 0f)
        {
            _attackCooldown = AttackInterval;
            _slashTime = Config.SlashDuration;
            _swingSign = -_swingSign;   // alterne le sens du coup
            AttackFired = true;
        }
        if (_slashTime > 0f) _slashTime -= dt;

        QueueRedraw();
    }

    public void TakeDamage(float amount) => Health = Mathf.Max(0f, Health - amount);
    public void Heal() => Health = MaxHp;
    public bool IsDead => Health <= 0f;

    public override void _Draw()
    {
        // Corps du virus : triangle fil-de-fer orienté vers la visée.
        float r = Config.PlayerRadius;
        float a = AimDir.Angle();
        Vector2 tip = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * 1.4f;
        Vector2 left = new Vector2(Mathf.Cos(a + 2.5f), Mathf.Sin(a + 2.5f)) * r;
        Vector2 right = new Vector2(Mathf.Cos(a - 2.5f), Mathf.Sin(a - 2.5f)) * r;
        DrawPolyline(new[] { tip, left, right, tip }, new Color(0.4f, 1f, 0.5f), 2f, true);

        // Coup de lame : arc de coupe qui balaie devant le virus, en fondu.
        if (_slashTime > 0f)
        {
            float p = _slashTime / Config.SlashDuration;   // 1 -> 0
            float half = Mathf.DegToRad(Config.MeleeArcDegrees * 0.5f);
            // Balayage : le tranchant part d'un bord vers l'autre selon la progression.
            float sweep = Mathf.Lerp(half, -half, 1f - p) * _swingSign;
            float mid = a + sweep;
            var col = new Color(0.85f, 0.95f, 1f, p);
            DrawArc(Vector2.Zero, Config.MeleeRange, mid - 0.35f, mid + 0.35f, 10, col, 3f, true);
        }
    }
}
