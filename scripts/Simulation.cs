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
    // États de comportement/infection.
    public const byte Dormant = 0;
    public const byte Activated = 1;
    public const byte Infected = 2;  // devenue viro-cellule

    public readonly int Capacity;
    public int Count { get; private set; }

    public readonly Vector2[] Position;
    public readonly Vector2[] Velocity;
    public readonly Vector2[] Acceleration;
    public readonly byte[] State;
    public readonly byte[] Kind;      // CellKind (Defensive/Prey/Neutral)
    public readonly float[] Hp;       // PV restants (cellules à vaincre)
    public readonly float[] Timer;    // incubation quand Infected
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
        Kind = new byte[capacity];
        Hp = new float[capacity];
        Timer = new float[capacity];
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
        InitCell(i, SampleOpenPosition(), RandomKind());
    }

    /// <summary>Ajoute une cellule (spawn dynamique, ex. production d'alerte).</summary>
    public void SpawnCell(Vector2 pos, byte kind)
    {
        if (Count >= Capacity) return;
        InitCell(Count, pos, kind);
        Count++;
    }

    private void InitCell(int i, Vector2 pos, byte kind)
    {
        Position[i] = pos;
        Kind[i] = kind;
        State[i] = Dormant;
        Dead[i] = false;
        Timer[i] = 0f;

        if (kind == CellKind.Prey)
        {
            Velocity[i] = Vector2.Zero;      // immobile
            Hp[i] = 1f;
        }
        else
        {
            float a = _rng.RandfRange(0f, Mathf.Tau);
            Velocity[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Config.DormantSpeed;
            Hp[i] = kind == CellKind.Defensive ? Config.DefensiveHp : Config.NeutralHp;
        }
        Acceleration[i] = Vector2.Zero;
    }

    private byte RandomKind()
    {
        float r = _rng.Randf();
        if (r < Config.DefensiveShare) return CellKind.Defensive;
        if (r < Config.DefensiveShare + Config.PreyShare) return CellKind.Prey;
        return CellKind.Neutral;
    }

    /// <summary>
    /// Passe une cellule à l'état viro-cellule (infectée). Timer = incubation ;
    /// Hp (inutile après infection) est réutilisé comme minuteur de production.
    /// </summary>
    public void Infect(int i)
    {
        State[i] = Infected;
        Velocity[i] = Vector2.Zero;
        Timer[i] = Config.IncubationTime;
        Hp[i] = Config.ViroProductionInterval;
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
            if (_map.IsOpen(p)) return p;
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
                Kind[w] = Kind[r];
                Hp[w] = Hp[r];
                Timer[w] = Timer[r];
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
