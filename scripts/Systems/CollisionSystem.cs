using Godot;

/// <summary>
/// Résolution de collisions entité-entité par correction positionnelle douce
/// (on écarte les paires qui se chevauchent). Les paires sont trouvées via la
/// grille spatiale (voisinage 3x3), donc en O(n·k).
///
/// Chaque paire est traitée une seule fois (condition j &gt; i). Solveur en une
/// passe : suffisant pour un benchmark ; on peut itérer plusieurs passes pour
/// un empilement plus rigide.
/// </summary>
public static class CollisionSystem
{
    public static void Run(Simulation sim, SpatialHashGrid grid)
    {
        int count = sim.Count;
        var pos = sim.Position;

        float minDist = Config.EntityRadius * 2f;
        float minDist2 = minDist * minDist;

        int cols = grid.Cols, rows = grid.Rows;
        var cellStart = grid.CellStart;
        var entities = grid.Entities;
        var cellOf = grid.CellOf;

        for (int i = 0; i < count; i++)
        {
            Vector2 pi = pos[i];
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
                        if (j <= i) continue; // chaque paire une seule fois

                        Vector2 d = pos[j] - pi;
                        float dist2 = d.X * d.X + d.Y * d.Y;
                        if (dist2 >= minDist2 || dist2 <= 0.00001f) continue;

                        float dist = Mathf.Sqrt(dist2);
                        float overlap = (minDist - dist) * 0.5f;
                        Vector2 push = d / dist * overlap;

                        pi -= push;
                        pos[i] = pi;
                        pos[j] += push;
                    }
                }
            }
        }
    }
}
