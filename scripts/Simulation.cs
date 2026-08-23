using Godot;

/// <summary>
/// État de simulation en Structure-of-Arrays (SoA).
/// Les entités NE SONT PAS des Nodes : ce sont des indices dans des tableaux
/// plats et parallèles, contigus en mémoire (cache-friendly, zéro allocation
/// par frame). C'est le cœur de l'approche data-oriented.
/// </summary>
public class Simulation
{
    public readonly int Capacity;
    public int Count { get; private set; }

    public readonly Vector2[] Position;
    public readonly Vector2[] Velocity;
    public readonly Vector2[] Acceleration;
    public readonly byte[] State;      // état FSM (0/1 dans ce prototype)
    public readonly float[] StateTimer;

    private readonly RandomNumberGenerator _rng = new();

    public Simulation(int capacity)
    {
        Capacity = capacity;
        Position = new Vector2[capacity];
        Velocity = new Vector2[capacity];
        Acceleration = new Vector2[capacity];
        State = new byte[capacity];
        StateTimer = new float[capacity];
        _rng.Randomize();
    }

    /// <summary>Ajuste la population active. Les nouvelles entités sont spawnées.</summary>
    public void SetCount(int n)
    {
        n = Mathf.Clamp(n, 0, Capacity);
        if (n > Count)
            for (int i = Count; i < n; i++)
                SpawnAt(i);
        Count = n;
    }

    private void SpawnAt(int i)
    {
        Position[i] = new Vector2(
            _rng.RandfRange(0f, Config.WorldWidth),
            _rng.RandfRange(0f, Config.WorldHeight));
        float a = _rng.RandfRange(0f, Mathf.Tau);
        Velocity[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Config.MaxSpeed * 0.5f;
        Acceleration[i] = Vector2.Zero;
        State[i] = 0;
        StateTimer[i] = _rng.RandfRange(0f, 3f);
    }

    /// <summary>
    /// Intègre vitesse/position (Euler semi-implicite), plafonne la vitesse,
    /// borne au monde, puis remet l'accélération à zéro pour le tick suivant.
    /// </summary>
    public void Integrate(float dt)
    {
        int count = Count;
        float maxSpeed = Config.MaxSpeed;
        for (int i = 0; i < count; i++)
        {
            Vector2 v = Velocity[i] + Acceleration[i] * dt;
            float speed = v.Length();
            if (speed > maxSpeed)
                v = v / speed * maxSpeed;
            Velocity[i] = v;

            Vector2 p = Position[i] + v * dt;
            p.X = Mathf.Clamp(p.X, 0f, Config.WorldWidth);
            p.Y = Mathf.Clamp(p.Y, 0f, Config.WorldHeight);
            Position[i] = p;

            Acceleration[i] = Vector2.Zero;
        }
    }
}
