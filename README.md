# -spread-

Prototype-benchmark de faisabilité : simulation **topdown** d'un **très grand nombre d'entités**
(flocking + pathfinding + machine à états + collisions), en **Godot 4 / C#**, multiplateforme **PC + mobile**.

> Étude de faisabilité pour un futur projet de jeu vidéo. Objectif : mesurer des chiffres réels
> (FPS, ms/système, N max) sur PC et mobile, et valider l'architecture data-oriented.

## Ce qui est inclus

- Architecture **data-oriented** : les entités sont des tableaux plats (SoA), **pas des Nodes**.
- **Grille de hachage spatiale** pour le voisinage en O(n·k) (flocking, collisions).
- **Flow field** : un seul champ de direction partagé pour le pathfinding de masse.
- **Flocking** (séparation / alignement / cohésion) + **FSM** + **collisions**.
- **Rendu** en un seul `MultiMeshInstance2D` (quelques draw calls pour des dizaines de milliers de sprites).
- **LOD de simulation** activable (les entités lointaines coûtent moins cher).
- **HUD de profiling** : FPS, population, temps de chaque système, contrôles.

Voir [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) pour les détails de conception, les budgets et les limites.

## Prérequis

- **Godot 4.3** (ou 4.2+) — **édition .NET / Mono** (celle qui supporte C#).
- **.NET SDK 8.0** installé (pour la compilation C#).

## Lancer

1. Ouvrir Godot 4 (.NET), *Import* → sélectionner le fichier `project.godot` de ce dossier.
2. À la première ouverture, Godot génère la solution C# et compile.
3. Appuyer sur **F5** (Play). La scène `Main.tscn` démarre.

## Contrôles

| Action | Entrée |
|--------|--------|
| Déplacer la caméra | `WASD` ou flèches |
| Zoom | Molette souris |
| Définir la cible (flow field) | Clic gauche dans le monde |
| Ajuster la population | Boutons / slider du HUD |
| Activer le LOD de simulation | Bouton `LOD` |
| Plafonner à 60 FPS | Bouton `Cap FPS` |

## Comment mener le benchmark

1. Lancer, laisser tourner quelques secondes, noter le **FPS** et les **ms par système** dans le HUD.
2. Monter la population par paliers (slider / `+5000`) jusqu'à ce que le FPS chute sous 60, puis sous 30.
3. Comparer **LOD off** vs **LOD on** à population élevée.
4. Repérer le système le plus coûteux (colonne `ms` du HUD).
5. **Exporter sur mobile** (Android/iOS) et refaire les mesures — le mobile est le vrai plafond.

## Export mobile (résumé)

- **Android** : installer le *Android Build Template* + SDK, créer un preset Android, export APK.
- **iOS** : nécessite macOS + Xcode, preset iOS.
- Garder `gl_compatibility` (déjà configuré) pour la meilleure compatibilité GPU mobile.

## Réglages

Tout se règle dans [`scripts/Config.cs`](scripts/Config.cs) : capacité, monde, vitesses,
poids de flocking, rayon de collision, taille de cellule, pas de LOD.
