using Godot;

/// <summary>
/// Arbre de compétences du virus (GDD §10). Cinq branches, une compétence
/// améliorable par branche pour cette tranche. Chaque niveau se paie en ADN :
///  - Attaque, Infection  -> ADN offensif
///  - Protection, Régénération, Fuite -> ADN survie
///  - l'ADN de renforcement est un joker qui complète n'importe quel achat.
///
/// La classe stocke les niveaux et expose les stats calculées lues par le
/// joueur et le tir. L'achat (dépense d'ADN) est fait par Game.
/// </summary>
public class Skills
{
    public const int Attaque = 0;
    public const int Infection = 1;
    public const int Protection = 2;
    public const int Regeneration = 3;
    public const int Fuite = 4;
    public const int Count = 5;
    public const int MaxLevel = 5;

    public readonly int[] Level = new int[Count];

    private static readonly string[] NameOf =
        { "Attaque", "Infection", "Protection", "Régénération", "Fuite" };
    private static readonly int[] BaseCost = { 3, 3, 4, 4, 4 };
    // true = coûte de l'ADN offensif ; false = ADN survie.
    private static readonly bool[] OffensiveCost = { true, true, false, false, false };

    public string Name(int b) => NameOf[b];
    public bool IsOffensive(int b) => OffensiveCost[b];
    public bool IsMaxed(int b) => Level[b] >= MaxLevel;
    public int NextCost(int b) => IsMaxed(b) ? 0 : BaseCost[b] * (Level[b] + 1);

    // --- Stats calculées ---
    public float Damage => Config.MeleeDamage + Level[Attaque] * 0.5f;
    public float AttackInterval => Config.AttackInterval * Mathf.Pow(0.85f, Level[Infection]);
    public float MaxHealth => Config.PlayerMaxHealth + Level[Protection] * 25f;
    public float RegenPerSec => Level[Regeneration] * 2.5f;
    public float MoveSpeed => Config.PlayerSpeed * (1f + Level[Fuite] * 0.12f);

    /// <summary>Description courte de l'effet actuel de la branche.</summary>
    public string Effect(int b) => b switch
    {
        Attaque => $"Dégâts : {Damage:0.#}",
        Infection => $"Cadence : {1f / AttackInterval:0.#}/s",
        Protection => $"PV max : {MaxHealth:0}",
        Regeneration => $"Régén : {RegenPerSec:0.#}/s",
        _ => $"Vitesse : {MoveSpeed:0}",
    };

    public void Reset() => System.Array.Clear(Level, 0, Count);
}
