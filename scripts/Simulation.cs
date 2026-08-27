using Godot;
using System.Threading.Tasks;

/// <summary>
/// État de simulation des anticorps en Structure-of-Arrays (SoA).
/// Les entités NE SONT PAS des Nodes : ce sont des indices dans des tableaux
/// plats et parallèles, contigus en mémoire (cache-friendly, zéro allocation
/// par frame). C'est le cœur de l'approche data-oriented.
///
/// État FSM : 0 = DORMANT (erre dans la map), 1 = ACTIVÉ (poursuit le joueur).
/// Suppression : marquage <see cref="Dead"/> puis compaction dense (swap) via
/// <see cref="CompactDead"/> — garde les tableaux sans trous, donc rapides.
/// </summary>
public class Simulation
{
    public const byte Dormant = 0;
    public const byte Activated = 1;

    public readonly int Capacity;
    public int Count { get; private set; }

    public readonly Vector2[] Position;
    public readonly Vector2[] Velocity;
    public readonly Vector2[] Acceleration;
    public readonly byte[] State;
    public readonly bool[] Dead;
    public readonly Vector2[] Displacement; // tampon de poussée des collisions (parallèle)

    public int ActivatedCount { get; set; }

    private readonly RandomNumberGenerator _rng = new();
    private OrganMap _map;

    public Simulation(int capacity)
    {
        Capacity = capacity;
        Position = new Vector2[capacity];
        Velocity = new Vector2[capacity];
        Acceleration = new Vector2[capacity];
        State = new byte[capacity];
        Dead = new bool[capacity];
        Displacement = new Vector2[capacity];
        _rng.Randomize();
    }

    /// <summary>Carte utilisée pour le spawn en zone ouverte et la collision murs.</summary>
    public void SetMap(OrganMap map) => _map = map;

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
        Position[i] = SampleOpenPosition();
        float a = _rng.RandfRange(0f, Mathf.Tau);
        Velocity[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Config.DormantSpeed;
        Acceleration[i] = Vector2.Zero;
        State[i] = Dormant;
        Dead[i] = false;
    }

    private Vector2 SampleOpenPosition()
    {
        if (_map == null)
            return new Vector2(_rng.RandfRange(0f, Config.WorldWidth),
                               _rng.RandfRange(0f, Config.WorldHeight));

        for (int t = 0; t < 32; t++)
        {
            Vector2 p = new(_rng.RandfRange(0f, Config.WorldWidth),
                            _rng.RandfRange(0f, Config.WorldHeight));
            if (_map.IsOpenWorld(p)) return p;
        }
        return _map.SpawnPoint; // repli : centre du premier organe
    }

    /// <summary>Marque une entité comme détruite (appliqué à la compaction).</summary>
    public void Kill(int i) => Dead[i] = true;

    /// <summary>Retire les entités mortes en compactant les tableaux (dense).</summary>
    public void CompactDead()
    {
        int w = 0;
        for (int r = 0; r < Count; r++)
        {
            if (Dead[r]) continue;
            if (w != r)
            {
                Position[w] = Position[r];
                Velocity[w] = Velocity[r];
                Acceleration[w] = Acceleration[r];
                State[w] = State[r];
            }
            Dead[w] = false;
            w++;
        }
        Count = w;
    }

    /// <summary>
    /// Intègre vitesse/position (Euler semi-implicite), plafonne selon l'état
    /// (dormant plus lent), borne au monde, remet l'accélération à zéro.
    /// </summary>
    public void Integrate(float dt, bool parallel)
    {
        int count = Count;

        void Step(int i)
        {
            float maxSpeed = State[i] == Activated ? Config.MaxSpeed : Config.DormantSpeed;

            Vector2 v = Velocity[i] + Acceleration[i] * dt;
            float speed = v.Length();
            if (speed > maxSpeed)
                v = v / speed * maxSpeed;
            Velocity[i] = v;

            Position[i] = _map != null
                ? _map.Slide(Position[i], v * dt)
                : ClampWorld(Position[i] + v * dt);
            Acceleration[i] = Vector2.Zero;
        }

        if (parallel)
            Parallel.For(0, count, Step);
        else
            for (int i = 0; i < count; i++) Step(i);
    }

    private static Vector2 ClampWorld(Vector2 p)
    {
        p.X = Mathf.Clamp(p.X, 0f, Config.WorldWidth);
        p.Y = Mathf.Clamp(p.Y, 0f, Config.WorldHeight);
        return p;
    }
}
