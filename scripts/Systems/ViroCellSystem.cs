using Godot;

/// <summary>
/// Gère les viro-cellules (cellules infectées, GDD §6) :
///  - produisent de l'ADN viral à intervalle régulier (Hp réutilisé comme
///    minuteur de production) ;
///  - à la fin de l'incubation (Timer), s'auto-détruisent en relâchant un burst
///    d'ADN, puis sont retirées (marquées Dead).
///
/// Le type d'ADN produit dépend du type d'origine de la cellule. Système serial
/// (les viro-cellules sont peu nombreuses par rapport au total).
/// </summary>
public static class ViroCellSystem
{
    public static AdnGain Run(Simulation sim, float dt, System.Collections.Generic.List<Vector2> bursts)
    {
        int count = sim.Count;
        var pos = sim.Position;
        var state = sim.State;
        var kind = sim.Kind;
        var timer = sim.Timer;
        var prod = sim.Hp;

        AdnGain gain = default;

        for (int i = 0; i < count; i++)
        {
            if (state[i] != Simulation.Infected) continue;

            // Production périodique.
            prod[i] -= dt;
            if (prod[i] <= 0f)
            {
                AddByKind(kind[i], Config.ViroProductionAdn, ref gain);
                prod[i] += Config.ViroProductionInterval;
            }

            // Fin d'incubation -> burst + mort.
            timer[i] -= dt;
            if (timer[i] <= 0f)
            {
                AddByKind(kind[i], Config.ViroBurstAdn, ref gain);
                bursts?.Add(pos[i]);
                sim.Kill(i);
            }
        }

        return gain;
    }

    private static void AddByKind(byte kind, int amount, ref AdnGain gain)
    {
        switch (kind)
        {
            case CellKind.Defensive: gain.Offensive += amount; break;
            case CellKind.Prey: gain.Survival += amount; break;
            default: gain.Reinforce += amount; break;
        }
    }
}
