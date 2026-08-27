using Godot;

/// <summary>
/// Déplace les projectiles et résout les impacts contre les anticorps via la
/// grille spatiale (voisinage 3x3). Un impact marque l'anticorps détruit
/// (compaction ultérieure) et retire le projectile. Un projectile expiré ou
/// sorti du monde est également retiré.
/// </summary>
public static class ProjectileSystem
{
    public static int Run(Projectiles proj, Simulation sim, SpatialHashGrid grid, OrganMap map, float dt)
    {
        var pos = sim.Position;
        var dead = sim.Dead;
        int cols = grid.Cols, rows = grid.Rows;
        var cellStart = grid.CellStart;
        var entities = grid.Entities;

        float hitR = Config.ProjectileRadius + Config.EntityRadius;
        float hitR2 = hitR * hitR;

        int kills = 0;
        int i = 0;
        while (i < proj.Count)
        {
            proj.Life[i] -= dt;
            if (proj.Life[i] <= 0f) { proj.RemoveAt(i); continue; }

            Vector2 p = proj.Position[i] + proj.Velocity[i] * dt;
            proj.Position[i] = p;

            if (p.X < 0f || p.X > Config.WorldWidth || p.Y < 0f || p.Y > Config.WorldHeight)
            { proj.RemoveAt(i); continue; }

            // Impact sur un mur (tissu solide) -> le projectile s'arrête.
            if (map != null && !map.IsOpenWorld(p))
            { proj.RemoveAt(i); continue; }

            int cell = grid.CellIndex(p);
            int cx = cell % cols, cy = cell / cols;
            bool hit = false;

            for (int oy = -1; oy <= 1 && !hit; oy++)
            {
                int ny = cy + oy;
                if (ny < 0 || ny >= rows) continue;
                for (int ox = -1; ox <= 1 && !hit; ox++)
                {
                    int nx = cx + ox;
                    if (nx < 0 || nx >= cols) continue;

                    int nc = ny * cols + nx;
                    int start = cellStart[nc], end = cellStart[nc + 1];
                    for (int k = start; k < end; k++)
                    {
                        int j = entities[k];
                        if (dead[j]) continue;
                        Vector2 d = pos[j] - p;
                        if (d.X * d.X + d.Y * d.Y <= hitR2)
                        {
                            sim.Kill(j);
                            kills++;
                            hit = true;
                            break;
                        }
                    }
                }
            }

            if (hit) { proj.RemoveAt(i); continue; }
            i++;
        }

        return kills;
    }
}
