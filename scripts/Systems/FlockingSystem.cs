using Godot;

/// <summary>
/// Comportement de nuée (boids) : séparation + alignement + cohésion, calculés
/// sur les voisins via la grille spatiale (voisinage 3x3). On y ajoute la force
/// de pilotage vers la cible fournie par le flow field.
///
/// LOD de simulation : hors de la zone active (autour de la caméra), une entité
/// ne fait le calcul complet des voisins que 1 tick sur LodStride ; le reste du
/// temps elle se contente de suivre le flow field (O(1)). Cela réduit fortement
/// le coût sans figer les entités lointaines.
///
/// Aucune allocation dans la boucle : indispensable pour éviter les saccades GC.
/// </summary>
public static class FlockingSystem
{
    public static void Run(Simulation sim, SpatialHashGrid grid, FlowField flow,
                           bool lod, Rect2 activeRect, int tick)
    {
        int count = sim.Count;
        var pos = sim.Position;
        var vel = sim.Velocity;
        var acc = sim.Acceleration;

        float perc2 = Config.PerceptionRadius * Config.PerceptionRadius;
        float sep2 = Config.SeparationRadius * Config.SeparationRadius;

        int cols = grid.Cols, rows = grid.Rows;
        var cellStart = grid.CellStart;
        var entities = grid.Entities;
        var cellOf = grid.CellOf;

        for (int i = 0; i < count; i++)
        {
            Vector2 pi = pos[i];

            // --- LOD : entité lointaine hors phase -> flow field seul ---
            if (lod && !activeRect.HasPoint(pi) && ((tick + i) % Config.LodStride != 0))
            {
                Vector2 fd = flow.SampleDirection(pi);
                if (fd.LengthSquared() > 0.0001f)
                    acc[i] += Steer(fd * Config.MaxSpeed, vel[i]) * Config.FlowWeight;
                continue;
            }

            Vector2 sep = Vector2.Zero;
            Vector2 ali = Vector2.Zero;
            Vector2 coh = Vector2.Zero;
            int neighbors = 0;

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
                        if (dist2 > perc2 || dist2 <= 0.0001f) continue;

                        neighbors++;
                        ali += vel[j];
                        coh += pos[j];
                        if (dist2 < sep2)
                            sep += d / dist2; // plus fort quand plus proche
                    }
                }
            }

            Vector2 force = Vector2.Zero;
            if (neighbors > 0)
            {
                ali /= neighbors;
                if (ali.LengthSquared() > 0.0001f)
                    force += Steer(ali, vel[i]) * Config.AlignmentWeight;

                coh = coh / neighbors - pi;
                if (coh.LengthSquared() > 0.0001f)
                    force += Steer(coh, vel[i]) * Config.CohesionWeight;

                if (sep.LengthSquared() > 0.0001f)
                    force += Steer(sep, vel[i]) * Config.SeparationWeight;
            }

            Vector2 fdir = flow.SampleDirection(pi);
            if (fdir.LengthSquared() > 0.0001f)
                force += Steer(fdir * Config.MaxSpeed, vel[i]) * Config.FlowWeight;

            float fl = force.Length();
            if (fl > Config.MaxForce)
                force = force / fl * Config.MaxForce;

            acc[i] += force;
        }
    }

    // Pilotage à la Reynolds : (direction désirée ramenée à MaxSpeed) - vitesse actuelle.
    private static Vector2 Steer(Vector2 desired, Vector2 currentVel)
    {
        float len = desired.Length();
        if (len > 0.0001f)
            desired = desired / len * Config.MaxSpeed;
        return desired - currentVel;
    }
}
