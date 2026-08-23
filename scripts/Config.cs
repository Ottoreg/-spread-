using Godot;

/// <summary>
/// Paramètres globaux réglables du prototype-benchmark.
/// Toutes les valeurs sont ici pour faciliter le tuning pendant les mesures.
/// </summary>
public static class Config
{
    // --- Capacité & population de départ ---
    // Taille max des tableaux (mémoire pré-allouée, jamais réallouée en cours de run).
    public const int Capacity = 60000;
    public const int InitialEntityCount = 2000;

    // --- Monde ---
    public const float WorldWidth = 6000f;
    public const float WorldHeight = 6000f;

    // --- Cinématique ---
    public const float MaxSpeed = 120f;   // px/s
    public const float MaxForce = 400f;   // px/s^2 (cap de la force de pilotage)

    // --- Flocking (boids) ---
    public const float PerceptionRadius = 40f;
    public const float SeparationRadius = 18f;
    public const float SeparationWeight = 1.8f;
    public const float AlignmentWeight = 1.0f;
    public const float CohesionWeight = 0.9f;
    public const float FlowWeight = 1.4f;   // poids du pathfinding (flow field)

    // --- Collisions ---
    public const float EntityRadius = 5f;

    // --- Grille spatiale : cellule = rayon de perception (voisinage 3x3 suffit) ---
    public const float CellSize = 40f;

    // --- LOD de simulation ---
    // Hors zone active, une entité ne recalcule son flocking complet que
    // 1 tick sur LodStride (elle suit le flow field le reste du temps).
    public const int LodStride = 6;
}
