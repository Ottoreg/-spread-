using Godot;

/// <summary>
/// Déplace les projectiles et résout les impacts contre les cellules (via la
/// grille spatiale). Effet selon le type de cellule touchée (GDD §5) :
///  - Prey    : infectée immédiatement -> ADN de survie.
///  - Defensive : PV -= dégâts ; à 0, vaincue puis infectée -> ADN offensif.
///  - Neutral : PV -= dégâts ; au seuil (0), infectée -> ADN de renforcement.
///
/// Les cellules déjà infectées (viro-cellules) sont ignorées. Le projectile est
/// retiré à l'impact (mur, cellule) ou à expiration. Renvoie l'ADN/alerte gagnés.
/// </summary>
public static class ProjectileSystem
{
    public static AdnGain Run(Projectiles proj, Simulation sim, SpatialHashGrid grid, OrganMap map, float dt)
    {
        var pos = sim.Position;
        var dead = sim.Dead;
        var state = sim.State;
        var kind = sim.Kind;
        var hp = sim.Hp;

        int cols = grid.Cols, rows = grid.Rows;
        var cellStart = grid.CellStart;
        var entities = grid.Entities;

        float hitR = Config.ProjectileRadius + Config.EntityRadius;
        float hitR2 = hitR * hitR;

        AdnGain gain = default;

        int i = 0;
        while (i < proj.Count)
        {
            proj.Life[i] -= dt;
            if (proj.Life[i] <= 0f) { proj.RemoveAt(i); continue; }

            Vector2 p = proj.Position[i] + proj.Velocity[i] * dt;
            proj.Position[i] = p;

            if (p.X < 0f || p.X > Config.WorldWidth || p.Y < 0f || p.Y > Config.WorldHeight)
            { proj.RemoveAt(i); continue; }

            if (map != null && !map.IsOpen(p))
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
                        if (dead[j] || state[j] == Simulation.Infected) continue;

                        Vector2 d = pos[j] - p;
                        if (d.X * d.X + d.Y * d.Y > hitR2) continue;

                        HitCell(sim, j, kind, hp, ref gain);
                        hit = true;
                        break;
                    }
                }
            }

            if (hit) { proj.RemoveAt(i); continue; }
            i++;
        }

        return gain;
    }

    private static void HitCell(Simulation sim, int j, byte[] kind, float[] hp, ref AdnGain gain)
    {
        switch (kind[j])
        {
            case CellKind.Prey:
                sim.Infect(j);
                gain.Survival += Config.AdnPreyInfect;
                gain.Alert += Config.AlertPerPrey;
                gain.Infections++;
                break;

            case CellKind.Defensive:
                hp[j] -= Config.ProjectileDamage;
                if (hp[j] <= 0f)
                {
                    sim.Infect(j);
                    gain.Offensive += Config.AdnDefensiveDefeat;
                    gain.Alert += Config.AlertPerDefensive;
                    gain.Infections++;
                }
                break;

            default: // Neutral
                hp[j] -= Config.ProjectileDamage;
                if (hp[j] <= 0f)
                {
                    sim.Infect(j);
                    gain.Reinforce += Config.AdnNeutralInfect;
                    gain.Alert += Config.AlertPerNeutral;
                    gain.Infections++;
                }
                break;
        }
    }
}
