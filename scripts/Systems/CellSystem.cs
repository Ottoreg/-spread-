using Godot;
using System.Threading.Tasks;

/// <summary>
/// Comportement des cellules selon leur type (GDD §5) et leur état.
///
///  - Infectée (viro-cellule) : ne bouge plus, ignorée ici (gérée par ViroCellSystem).
///  - Prey : immobile (cellule d'organe). Aucune force.
///  - Neutral : passive — erre + séparation, ne s'active jamais.
///  - Defensive : dormante (erre) -> activée près du joueur -> nuée + poursuite
///    (flow field aware des murs). Seul type qui attaque le joueur.
///
/// Parallélisable sans risque (écriture par index propre). Zéro allocation.
/// </summary>
public static class CellSystem
{
    public static void Run(Simulation sim, SpatialHashGrid grid, FlowField flow,
                           Vector2 playerPos, bool lod, Rect2 activeRect,
                           int tick, float time, bool trailFresh, bool parallel)
    {
        int count = sim.Count;
        var pos = sim.Position;
        var vel = sim.Velocity;
        var acc = sim.Acceleration;
        var state = sim.State;
        var kind = sim.Kind;

        float perc2 = Config.PerceptionRadius * Config.PerceptionRadius;
        float sep2 = Config.SeparationRadius * Config.SeparationRadius;
        float actR2 = Config.ActivationRadius * Config.ActivationRadius;

        int cols = grid.Cols, rows = grid.Rows;
        var cellStart = grid.CellStart;
        var entities = grid.Entities;
        var cellOf = grid.CellOf;

        void Body(int i)
        {
            // Viro-cellules et proies : pas de pilotage.
            if (state[i] == Simulation.Infected || kind[i] == CellKind.Prey)
                return;

            bool defensive = kind[i] == CellKind.Defensive;
            Vector2 pi = pos[i];

            // Transitions d'état (défensives) :
            //  - s'active si le joueur est proche ET laisse une piste fraîche
            //    (immobile = pas de piste -> ne réveille pas les cellules) ;
            //  - se désactive (perte de trace) dès que la piste devient froide :
            //    elle se calme et se disperse (errance) au lieu d'orbiter en meute.
            if (defensive)
            {
                if (state[i] == Simulation.Dormant)
                {
                    Vector2 dp = pi - playerPos;
                    if (trailFresh && dp.X * dp.X + dp.Y * dp.Y < actR2)
                        state[i] = Simulation.Activated;
                }
                else if (state[i] == Simulation.Activated && !trailFresh)
                {
                    state[i] = Simulation.Dormant;
                }
            }
            bool activated = defensive && state[i] == Simulation.Activated;

            // LOD : cellule non activée, lointaine, hors phase -> wander seul.
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

            // Une cellule activée poursuit (elle n'est activée que tant que la
            // piste est fraîche) ; sinon elle erre (dormante = calme, dispersée).
            if (activated)
            {
                Vector2 fdir = flow.SampleDirection(pi);
                if (fdir.LengthSquared() > 0.0001f)
                    force += Steer(fdir * Config.MaxSpeed, vel[i], Config.MaxSpeed) * Config.SeekWeight;
            }
            else
            {
                float wa = time * 0.4f + i * 0.137f;
                Vector2 wdir = new(Mathf.Cos(wa), Mathf.Sin(wa));
                force += Steer(wdir * Config.DormantSpeed, vel[i], Config.DormantSpeed) * Config.WanderWeight;
            }

            float fl = force.Length();
            if (fl > Config.MaxForce)
                force = force / fl * Config.MaxForce;

            acc[i] += force;
        }

        if (parallel)
            Parallel.For(0, count, Body);
        else
            for (int i = 0; i < count; i++) Body(i);

        // Comptage des défensives activées (hors boucle chaude).
        int activatedCount = 0;
        for (int i = 0; i < count; i++)
            if (state[i] == Simulation.Activated) activatedCount++;
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
