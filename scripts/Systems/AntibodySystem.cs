using Godot;

/// <summary>
/// Comportement des anticorps. Deux régimes selon l'état FSM :
///
///  - DORMANT : erre dans la map (wander lisse et déterministe) + légère
///    séparation pour ne pas s'empiler. S'active si le joueur entre dans son
///    rayon d'activation.
///  - ACTIVÉ : nuée qui poursuit le joueur — séparation/alignement/cohésion
///    (via la grille spatiale) + pilotage vers le joueur fourni par le flow
///    field (pathfinding de masse, extensible aux obstacles/organes).
///
/// LOD : un anticorps dormant hors écran ne recalcule ses voisins que 1 tick
/// sur LodStride ; le reste du temps il se contente d'errer (O(1)). Les
/// anticorps activés sont toujours simulés à fond (c'est le gameplay).
///
/// Zéro allocation dans la boucle (indispensable contre les saccades GC).
/// </summary>
public static class AntibodySystem
{
    public static void Run(Simulation sim, SpatialHashGrid grid, FlowField flow,
                           Vector2 playerPos, bool lod, Rect2 activeRect,
                           int tick, float time)
    {
        int count = sim.Count;
        var pos = sim.Position;
        var vel = sim.Velocity;
        var acc = sim.Acceleration;
        var state = sim.State;

        float perc2 = Config.PerceptionRadius * Config.PerceptionRadius;
        float sep2 = Config.SeparationRadius * Config.SeparationRadius;
        float actR2 = Config.ActivationRadius * Config.ActivationRadius;

        int cols = grid.Cols, rows = grid.Rows;
        var cellStart = grid.CellStart;
        var entities = grid.Entities;
        var cellOf = grid.CellOf;

        int activatedCount = 0;

        for (int i = 0; i < count; i++)
        {
            Vector2 pi = pos[i];

            // --- Activation ---
            if (state[i] == Simulation.Dormant)
            {
                Vector2 dp = pi - playerPos;
                if (dp.X * dp.X + dp.Y * dp.Y < actR2)
                    state[i] = Simulation.Activated;
            }
            bool activated = state[i] == Simulation.Activated;
            if (activated) activatedCount++;

            // --- LOD : dormant lointain hors phase -> wander seul ---
            bool doNeighbors = true;
            if (!activated && lod && !activeRect.HasPoint(pi)
                && ((tick + i) % Config.LodStride != 0))
                doNeighbors = false;

            Vector2 force = Vector2.Zero;

            if (doNeighbors)
            {
                Vector2 sep = Vector2.Zero, ali = Vector2.Zero, coh = Vector2.Zero;
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
                            if (dist2 < sep2) sep += d / dist2;
                            if (activated) { ali += vel[j]; coh += pos[j]; }
                        }
                    }
                }

                if (sep.LengthSquared() > 0.0001f)
                    force += Steer(sep, vel[i], Config.MaxSpeed) * Config.SeparationWeight;

                if (activated && neighbors > 0)
                {
                    ali /= neighbors;
                    if (ali.LengthSquared() > 0.0001f)
                        force += Steer(ali, vel[i], Config.MaxSpeed) * Config.AlignmentWeight;

                    coh = coh / neighbors - pi;
                    if (coh.LengthSquared() > 0.0001f)
                        force += Steer(coh, vel[i], Config.MaxSpeed) * Config.CohesionWeight;
                }
            }

            if (activated)
            {
                Vector2 fdir = flow.SampleDirection(pi);
                if (fdir.LengthSquared() > 0.0001f)
                    force += Steer(fdir * Config.MaxSpeed, vel[i], Config.MaxSpeed) * Config.SeekWeight;
            }
            else
            {
                // Wander lisse et déterministe (pas de RNG, pas d'allocation).
                float wa = time * 0.4f + i * 0.137f;
                Vector2 wdir = new(Mathf.Cos(wa), Mathf.Sin(wa));
                force += Steer(wdir * Config.DormantSpeed, vel[i], Config.DormantSpeed) * Config.WanderWeight;
            }

            float fl = force.Length();
            if (fl > Config.MaxForce)
                force = force / fl * Config.MaxForce;

            acc[i] += force;
        }

        sim.ActivatedCount = activatedCount;
    }

    private static Vector2 Steer(Vector2 desired, Vector2 currentVel, float maxSpeed)
    {
        float len = desired.Length();
        if (len > 0.0001f)
            desired = desired / len * maxSpeed;
        return desired - currentVel;
    }
}
