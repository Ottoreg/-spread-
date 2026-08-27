using Godot;

/// <summary>
/// Paramètres globaux réglables du prototype-benchmark / slice de gameplay.
/// Concept : twin-stick roguelike. Le joueur (virus) infecte l'hôte ; des
/// milliers d'anticorps errent (dormants) puis s'activent pour l'attaquer.
/// Tout est ici pour faciliter le tuning pendant les mesures.
/// </summary>
public static class Config
{
    // --- Capacité & population ---
    public const int Capacity = 60000;          // mémoire pré-allouée (jamais réallouée)
    public const int InitialEntityCount = 3000;

    // --- Monde (grande map ouverte = organes interconnectés, à venir) ---
    public const float WorldWidth = 6000f;
    public const float WorldHeight = 6000f;

    // --- Anticorps : cinématique ---
    public const float MaxSpeed = 130f;         // vitesse max quand activé
    public const float DormantSpeed = 45f;      // vitesse de dérive quand dormant
    public const float MaxForce = 420f;

    // --- Flocking / nuée (anticorps activés) ---
    public const float PerceptionRadius = 40f;
    public const float SeparationRadius = 16f;
    public const float SeparationWeight = 1.9f;
    public const float AlignmentWeight = 0.8f;
    public const float CohesionWeight = 0.6f;
    public const float SeekWeight = 1.6f;       // poursuite du joueur (via flow field)
    public const float WanderWeight = 1.0f;     // errance quand dormant

    // --- Activation ---
    // Un anticorps dormant à moins de ce rayon du joueur se réveille et attaque.
    public const float ActivationRadius = 340f;

    // --- Collisions / tailles ---
    public const float EntityRadius = 5f;

    // --- Joueur (virus) ---
    public const float PlayerSpeed = 250f;
    public const float PlayerRadius = 11f;
    public const float PlayerMaxHealth = 100f;
    public const float PlayerContactDps = 14f;  // dégâts/s au contact d'un anticorps activé

    // --- Projectiles (infection) ---
    public const int ProjectileCapacity = 4000;
    public const float ProjectileSpeed = 720f;
    public const float ProjectileLifetime = 1.1f;
    public const float ProjectileRadius = 6f;
    public const float FireInterval = 0.085f;   // cadence de tir (s)

    // --- Grille spatiale : cellule = rayon de perception ---
    public const float CellSize = 40f;

    // --- Carte : organes reliés par des corridors ---
    public const float MapCellSize = 32f;       // résolution de la grille de navigation/murs
    public const int OrganCount = 7;
    public const float OrganMinRadius = 340f;
    public const float OrganMaxRadius = 620f;
    public const float CorridorHalfWidth = 70f;
    public const float MapMargin = 500f;         // marge sans organe au bord du monde
    public const ulong MapSeed = 1337;           // graine fixe => carte reproductible

    // --- LOD de simulation (anticorps dormants lointains) ---
    public const int LodStride = 6;
}
