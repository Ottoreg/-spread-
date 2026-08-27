using Godot;
using System.Threading.Tasks;

/// <summary>
/// Résolution de collisions entité-entité par correction positionnelle douce.
/// Trouve les paires via la grille spatiale (voisinage 3x3), donc O(n·k).
///
/// Version parallélisable et race-safe : « chacun se pousse soi-même ».
///  - Passe 1 (parallèle) : chaque entité LIT ses voisins (lecture seule) et
///    accumule SA poussée dans Displacement[i] uniquement (aucune écriture
///    partagée -> pas de data race).
///  - Passe 2 (parallèle) : applique Displacement[i] à Position[i].
///
/// Chaque paire est vue des deux côtés (i pousse à cause de j, et j à cause de
/// i), chacun s'écartant de la moitié : la séparation nette est équivalente à
/// l'ancienne version symétrique, mais sans conflit entre threads.
/// </summary>
public static class CollisionSystem
{
    public static void Run(Simulation sim, SpatialHashGrid grid, bool parallel)
    {
        int count = sim.Count;
        var pos = sim.Position;
        var disp = sim.Displacement;

        float minDist = Config.EntityRadius * 2f;
        float minDist2 = minDist * minDist;

        int cols = grid.Cols, rows = grid.Rows;
        var cellStart = grid.CellStart;
        var entities = grid.Entities;
        var cellOf = grid.CellOf;

        // Passe 1 : calcule la poussée propre à chaque entité (lecture seule sur pos).
        void Resolve(int i)
        {
            Vector2 pi = pos[i];
            Vector2 push = Vector2.Zero;

            int cell = cellOf[i];
            int cx = cell % cols, cy = cell / cols;

            for (int oy = -1; oy <= 1; oy++)
            {
                int ny = cy + oy;
                if (ny < 0 || ny >= rows) continue;
                for (int ox = -1; ox <= 1; ox++)
                {
                    int nx = cx + ox;
                    if (nx < 0 || nx >= cols) continue;

                    int nc = ny * cols + nx;
                    int start = cellStart[nc], end = cellStart[nc + 1];
                    for (int k = start; k < end; k++)
                    {
                        int j = entities[k];
                        if (j == i) continue;
                        Vector2 d = pi - pos[j];
                        float dist2 = d.X * d.X + d.Y * d.Y;
                        if (dist2 >= minDist2 || dist2 <= 0.00001f) continue;
                        float dist = Mathf.Sqrt(dist2);
                        push += d / dist * ((minDist - dist) * 0.5f);
                    }
                }
            }

            disp[i] = push;
        }

        // Passe 2 : applique la poussée (indépendant par index).
        void Apply(int i)
        {
            Vector2 p = pos[i] + disp[i];
            p.X = Mathf.Clamp(p.X, 0f, Config.WorldWidth);
            p.Y = Mathf.Clamp(p.Y, 0f, Config.WorldHeight);
            pos[i] = p;
        }

        if (parallel)
        {
            Parallel.For(0, count, Resolve);
            Parallel.For(0, count, Apply);
        }
        else
        {
            for (int i = 0; i < count; i++) Resolve(i);
            for (int i = 0; i < count; i++) Apply(i);
        }
    }
}
