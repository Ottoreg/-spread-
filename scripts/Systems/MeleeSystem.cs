using Godot;

/// <summary>
/// Attaque de mêlée du virus (coup de lame). Frappe toutes les cellules dans un
/// arc devant le joueur (portée + ouverture angulaire), via la grille spatiale.
/// Applique le même effet d'impact que le tir précédent selon le type de cellule
/// (GDD §5) et renvoie l'ADN / l'alerte gagnés.
/// </summary>
public static class MeleeSystem
{
    public static AdnGain Attack(Simulation sim, SpatialHashGrid grid,
                                 Vector2 center, Vector2 dir, float damage)
    {
        var pos = sim.Position;
        var dead = sim.Dead;
        var state = sim.State;
        var kind = sim.Kind;
        var hp = sim.Hp;

        float range = Config.MeleeRange;
        float range2 = range * range;
        float halfArcCos = Mathf.Cos(Mathf.DegToRad(Config.MeleeArcDegrees * 0.5f));

        int cols = grid.Cols, rows = grid.Rows;
        var cellStart = grid.CellStart;
        var entities = grid.Entities;

        int cell = grid.CellIndex(center);
        int cx = cell % cols, cy = cell / cols;
        int rad = Mathf.CeilToInt(range / Config.CellSize);

        AdnGain gain = default;

        for (int oy = -rad; oy <= rad; oy++)
        {
            int ny = cy + oy;
            if (ny < 0 || ny >= rows) continue;
            for (int ox = -rad; ox <= rad; ox++)
            {
                int nx = cx + ox;
                if (nx < 0 || nx >= cols) continue;

                int nc = ny * cols + nx;
                int start = cellStart[nc], end = cellStart[nc + 1];
                for (int k = start; k < end; k++)
                {
                    int j = entities[k];
                    if (dead[j] || state[j] == Simulation.Infected) continue;

                    Vector2 d = pos[j] - center;
                    float dist2 = d.X * d.X + d.Y * d.Y;
                    if (dist2 > range2 || dist2 <= 0.0001f) continue;

                    // À l'intérieur de l'arc de coupe ?
                    float dot = (d.X * dir.X + d.Y * dir.Y) / Mathf.Sqrt(dist2);
                    if (dot < halfArcCos) continue;

                    ApplyHit(sim, j, kind, hp, damage, ref gain);
                }
            }
        }

        return gain;
    }

    private static void ApplyHit(Simulation sim, int j, byte[] kind, float[] hp, float damage, ref AdnGain gain)
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
                hp[j] -= damage;
                if (hp[j] <= 0f)
                {
                    sim.Infect(j);
                    gain.Offensive += Config.AdnDefensiveDefeat;
                    gain.Alert += Config.AlertPerDefensive;
                    gain.Infections++;
                }
                break;

            default: // Neutral
                hp[j] -= damage;
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
