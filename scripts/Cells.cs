/// <summary>
/// Types de cellules (GDD §5). Stocké dans Simulation.Kind (byte).
///  - Defensive : cellule immunitaire. Doit être VAINCUE (PV) avant infection.
///                Attaque le joueur. Lâche de l'ADN offensif.
///  - Prey      : cellule d'organe vital. Infectée SANS combat (contact/tir).
///                Immobile. Lâche de l'ADN de survie.
///  - Neutral   : autres cellules. Réduire les PV jusqu'au seuil d'infection.
///                Passive. Lâche de l'ADN de renforcement.
/// </summary>
public static class CellKind
{
    public const byte Defensive = 0;
    public const byte Prey = 1;
    public const byte Neutral = 2;
    public const int Count = 3;
}

/// <summary>
/// Gain d'ADN viral (3 types) + montée d'alerte, agrégé par les systèmes et
/// reversé au joueur. Struct value-type : zéro allocation.
/// </summary>
public struct AdnGain
{
    public int Offensive;
    public int Survival;
    public int Reinforce;
    public int Infections;
    public float Alert;

    public void Add(in AdnGain o)
    {
        Offensive += o.Offensive;
        Survival += o.Survival;
        Reinforce += o.Reinforce;
        Infections += o.Infections;
        Alert += o.Alert;
    }
}
