# -spread-

Prototype d'un **twin-stick roguelike** dans le corps d'un hôte : le joueur incarne un
**virus** ; des **milliers d'anticorps** errent puis s'activent pour l'attaquer en nuée.
Rendu **fil-de-fer**. Moteur : **Godot 4 / C#**. Cible : **PC d'abord** (mobile plus tard).

> Sert aussi de benchmark de faisabilité : mesurer des chiffres réels
> (FPS, ms/système, N max) et valider l'architecture data-oriented.

## Ce qui est inclus

- Architecture **data-oriented** : les anticorps sont des tableaux plats (SoA), **pas des Nodes**.
- **Grille de hachage spatiale** pour le voisinage en O(n·k) (nuée, collisions, impacts).
- **Flow field** re-ciblé sur le joueur : pathfinding de masse pour des milliers de poursuivants.
- **Anticorps** dormants (errance) → **activés** (nuée + poursuite) selon un rayon d'activation.
- **Joueur** twin-stick (déplacement, visée, tir) + **projectiles** d'infection (pool SoA).
- **Rendu fil-de-fer** en `MultiMeshInstance2D` (quelques draw calls pour des dizaines de milliers de formes).
- **LOD de simulation** activable (anticorps dormants lointains moins coûteux).
- **HUD de profiling** : FPS, populations, temps de chaque système, contrôles.

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
| Déplacer le virus | `WASD` ou flèches |
| Viser | Souris |
| Tirer (infecter) | Clic gauche maintenu |
| Zoom | Molette souris |
| Réinitialiser (PV + désactiver) | `R` |
| Ajuster la population d'anticorps | Boutons / slider du HUD |
| Activer le LOD de simulation | Bouton `LOD` |
| Plafonner à 60 FPS | Bouton `Cap FPS` |

## Comment mener le benchmark

1. Lancer, laisser tourner quelques secondes, noter le **FPS** et les **ms par système** dans le HUD.
2. Monter la population par paliers (slider / `+5000`) jusqu'à ce que le FPS chute sous 60, puis sous 30.
3. Comparer **LOD off** vs **LOD on** à population élevée.
4. Repérer le système le plus coûteux (colonne `ms` du HUD).
5. Faire varier le **rayon d'activation** (`Config.ActivationRadius`) pour voir le coût des anticorps *activés* (les plus chers) vs *dormants*.

> Le portage/tests **mobile sont mis de côté** pour l'instant. Le code reste
> portable (`gl_compatibility`, data-oriented) : on pourra exporter Android/iOS
> plus tard sans refonte.

## Réglages

Tout se règle dans [`scripts/Config.cs`](scripts/Config.cs) : capacité, monde, vitesses,
poids de flocking, rayon de collision, taille de cellule, pas de LOD.
