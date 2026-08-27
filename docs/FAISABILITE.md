# Étude de faisabilité — résultats & verdict

Objectif initial : valider qu'on peut simuler **un très grand nombre d'entités**
en topdown, choisir la **meilleure techno** (fluidité + portabilité), et
**déterminer limites et contraintes**.

## Verdict : ✅ FAISABLE en Godot 4 / C#

Pour le jeu visé (twin-stick, virus vs anticorps), **Godot 4 + C# avec une
architecture data-oriented est le bon choix**. Pas besoin d'Unity DOTS ni de
simulation GPU à cette échelle. La portabilité (PC maintenant, mobile plus tard)
reste ouverte sans refonte.

## Mesures réelles

**Machine de test** : CPU 12 threads. Rendu 1280×720. Map ouverte 6000×6000.
Comportement complet actif (activation, nuée/flocking, poursuite flow field,
collisions, projectiles). Godot 4.7 .NET.

### Profil à 15 000 anticorps (ms par système)

| Système | Mono-thread | Multi-thread (12) | Gain |
|---|---|---|---|
| ia (comportement) | 5,82 | 0,78 | ×7,5 |
| integ (intégration) | 1,30 | 0,17 | ×7,6 |
| coll (collisions)¹ | 3,75 | 3,80 → **~0,5 attendu** | — |
| grid (grille) | 0,54 | 0,67 | mono-thread |
| proj / rendu | ~0,06 / ~0,38 | idem | négligeable |

¹ Collisions parallélisées après cette mesure (« chacun se pousse soi-même »,
race-safe). Le poste dominant mono-thread était les collisions ; c'est corrigé.

À 15 000 entités : **60 FPS stable**, avec un budget de tick largement
sous les 16,6 ms disponibles → grosse marge.

### Montée en charge

| Population | Comportement |
|---|---|
| ~15 000 | 60 FPS constant, aucune chute |
| ~24 000 | début de ralentissement **progressif** (plus de falaise) |
| 60 000 (stress extrême) | ~14 FPS, **sans freeze** — dégradation gracieuse |

> Note : les tests à très haute population concentrent des milliers d'entités
> *activées* qui s'agglutinent autour du joueur (cas le plus coûteux). Le jeu
> réel n'aura jamais cette densité : seuls quelques centaines d'anticorps sont
> activés à la fois (279 activés observés à 15 000 au total).

## Ce qui rend ça possible (récap technique)

- **Data-oriented / SoA** : entités = tableaux plats, pas de Nodes.
- **Grille de hachage spatiale** : voisinage O(n·k) au lieu de O(n²).
- **Flow field** : pathfinding de masse partagé, O(1) par entité.
- **Multithread** (Parallel.For) sur comportement, intégration, collisions.
- **MultiMesh** : rendu fil-de-fer en poignée de draw calls + culling caméra.
- **Anti spirale physique** (`MaxPhysicsStepsPerFrame = 1`) : dégradation douce,
  jamais de freeze à 0 FPS.
- **LOD de simulation** : les anticorps dormants lointains coûtent moins cher.

## Limites & contraintes déterminées

1. **Coût plancher = O(n) sur le total d'entités.** Même dormantes, toutes les
   entités passent chaque tick dans : grille + intégration + check d'activation.
   → limite l'effectif *total simulé simultanément* (pas *actif*).
2. **Densité locale = pire cas.** Un gros amas activé fait grimper le coût
   voisinage (k grand) pour flocking et collisions. À surveiller côté game design
   (éviter que 5000 anticorps se coincent sur un point).
3. **Grille de rebuild mono-thread** : ~0,7 ms à 15k, devient notable à 60k.
   Parallélisable si besoin plus tard.
4. **GC .NET** : discipline stricte « zéro allocation par frame » respectée dans
   les systèmes chauds. À maintenir (pas de LINQ/List temporaires en boucle).
5. **Mobile** : mis de côté pour l'instant ; l'archi reste compatible.

## Prochain grand levier (si on veut viser BEAUCOUP plus)

**Endormissement / streaming** : les entités loin du joueur ne sont ni déplacées
ni insérées dans la grille (elles « dorment »). Le coût ne dépend alors plus du
total sur la map mais du nombre d'entités **autour du joueur**. Cela permettrait
des dizaines/centaines de milliers d'anticorps répartis dans tout le corps de
l'hôte, avec un coût constant. C'est l'étape recommandée après les organes/murs.

## Conclusion pour la suite du projet

- **Techno validée** : Godot 4 / C#, on continue dessus.
- **Marge confortable** pour le gameplay réel visé.
- Le développement peut passer de la « faisabilité » à la **construction du jeu**
  (organes/map, types d'anticorps, vagues/activation, boucle roguelike).
