using Godot;

/// <summary>
/// Pool de projectiles (infection) en SoA. Taille fixe, recyclage par swap :
/// les projectiles vivants occupent [0, Count[. Spawn = ajout en fin ;
/// mort = swap avec le dernier vivant. Zéro allocation en jeu.
/// </summary>
public class Projectiles
{
    public readonly int Capacity;
    public int Count { get; private set; }

    public readonly Vector2[] Position;
    public readonly Vector2[] Velocity;
    public readonly float[] Life;

    public Projectiles(int capacity)
    {
        Capacity = capacity;
        Position = new Vector2[capacity];
        Velocity = new Vector2[capacity];
        Life = new float[capacity];
    }

    public void Spawn(Vector2 pos, Vector2 vel)
    {
        if (Count >= Capacity) return;
        int i = Count++;
        Position[i] = pos;
        Velocity[i] = vel;
        Life[i] = Config.ProjectileLifetime;
    }

    /// <summary>Retire le projectile i (swap avec le dernier vivant).</summary>
    public void RemoveAt(int i)
    {
        int last = --Count;
        if (i != last)
        {
            Position[i] = Position[last];
            Velocity[i] = Velocity[last];
            Life[i] = Life[last];
        }
    }
}
