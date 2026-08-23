# -spread- — Architecture technique (étude de faisabilité)

> Prototype-benchmark : simulation topdown d'un grand nombre d'entités.
> Moteur : **Godot 4.x en C# (.NET)**. Cible : **PC + mobile**, solo (multi plus tard).

---

## 1. Objectifs & cadrage

| Paramètre | Choix pour le prototype |
|-----------|-------------------------|
| Nombre d'entités | 1 000 → 10 000 (réglable en direct, on pousse jusqu'au décrochage) |
| Comportements | Flocking (boids) + pathfinding (flow field) + machine à états + collisions |
| Vue | Grand monde, caméra locale (pan + zoom), culling agressif |
| Réseau | Solo. Architecture pensée pour rester **compatible** multi plus tard |
| Plateformes | PC (Win/Mac/Linux) + mobile (Android/iOS), un seul codebase |

But du prototype : **mesurer des chiffres réels** (FPS, ms/frame CPU, N max) sur PC et sur un mobile de référence, et valider que l'architecture tient.

---

## 2. Principe directeur : Data-Oriented Design, pas de Node par entité

La faute classique en Godot est de faire **un `Node2D` (ou pire, une scène) par entité**. À 10 000 entités, le coût des nodes (allocation, `_Process` par node, arbre de scène) tue le framerate.

**Choix retenu : les entités ne sont PAS des nodes.**

- Toutes les données des entités vivent dans des **tableaux plats parallèles** (Structure-of-Arrays / SoA) en C# :
  ```
  Vector2[] Position
  Vector2[] Velocity
  byte[]    State        // état FSM
  int[]     TargetCell   // cible pathfinding
  float[]   Health
  ...
  ```
- Un seul `MultiMeshInstance2D` dessine **toutes** les entités (voir §6).
- Un unique orchestrateur (`Game.cs`) fait tourner les systèmes dans `_PhysicsProcess`.

Avantages : mémoire contiguë (cache-friendly), aucune surcharge de l'arbre de scène, boucles vectorisables, parallélisation facile.

---

## 3. Vue d'ensemble des systèmes (pipeline par tick)

Ordre d'exécution à chaque pas de simulation (timestep fixe) :

```
_PhysicsProcess(delta)
  1. InputSystem        → caméra (pan/zoom), réglage de N
  2. SpatialGrid.Rebuild→ réindexe les entités dans la grille
  3. StateMachineSystem → transitions d'états (idle/seek/flee/...)
  4. PathfindingSystem  → lit le flow field, calcule la direction désirée
  5. FlockingSystem     → séparation + alignement + cohésion (via grille)
  6. Integrate          → applique vitesse, limites, met à jour Position
  7. CollisionSystem    → résout les chevauchements (via grille)
  8. EntityRenderer     → pousse les transforms visibles dans le MultiMesh
  9. DebugHud           → FPS, ms, compteur
```

Chaque système est une classe séparée opérant sur les tableaux SoA. Aucun système n'alloue par entité et par frame.

---

## 4. Partitionnement spatial (le cœur de la perf)

Sans structure spatiale, flocking et collisions sont en **O(n²)** → mort dès quelques milliers.

**Choix : grille de hachage spatiale uniforme (Spatial Hash Grid).**

- Le monde est découpé en cellules carrées de taille ≈ rayon de perception des entités.
- Chaque frame, on range chaque entité dans sa cellule (`cellIndex = f(position)`).
- Les requêtes de voisinage ne regardent que les **9 cellules** autour (3×3) → O(n·k) avec k petit.
- Implémentation sans allocation : tableaux `cellStart[]` / `entityIndices[]` reconstruits par counting sort.

Pourquoi une grille plutôt qu'un quadtree : monde majoritairement uniforme, densité bornée, reconstruction O(n) triviale à paralléliser, pas de rééquilibrage. Un quadtree serait préférable si la densité était très hétérogène.

---

## 5. Comportements

### 5.1 Flocking (boids)
Trois règles classiques, calculées sur les voisins de la grille :
- **Séparation** : s'éloigner des voisins trop proches.
- **Alignement** : s'orienter vers la vitesse moyenne des voisins.
- **Cohésion** : se rapprocher du centre de masse des voisins.
Pondérées, plafonnées par une force max et une vitesse max.

### 5.2 Pathfinding : flow field (champ de direction)
Pour faire naviguer **des milliers** d'entités vers une cible commune, on **ne fait PAS un A\* par entité**. On calcule **un seul flow field** partagé :
- Une grille de navigation (indépendante de la grille spatiale, plus grossière).
- Un BFS/Dijkstra depuis la cible produit un champ « coût », puis un champ « direction » (vecteur par cellule pointant vers la cible en contournant les obstacles).
- Chaque entité lit simplement le vecteur de sa cellule → **O(1) par entité**.
- Recalculé seulement quand la cible ou les obstacles changent.

C'est LA technique clé qui rend le pathfinding de masse viable. (Pour des cibles multiples/individuelles, on ajoutera plus tard des flow fields par groupe ou un cache.)

### 5.3 Machine à états (FSM)
FSM légère encodée en `byte[] State` + `float[] StateTimer`. États d'exemple : `Wander`, `Seek`, `Flee`, `Idle`. Transitions data-driven, pas de branchements lourds par entité.

### 6. Rendu

- **1 seul `MultiMeshInstance2D`** avec un `MultiMesh` (TransformFormat 2D, couleurs activées).
- On ne pousse que les entités **visibles** (culling par rapport au rectangle caméra élargi d'une marge) → `InstanceCount` = nb visibles.
- Résultat : quelques draw calls pour des dizaines de milliers d'entités.
- Sprite = quad simple + texture d'atlas (ou couleur par état pour le debug).

---

## 7. Simulation LOD (montée en charge)

Pour tenir sur mobile, toutes les entités ne sont pas simulées à pleine fréquence :
- **Zone active** (autour caméra) : simulation complète chaque tick.
- **Zone éloignée** : mise à jour tous les N ticks (time-slicing par « bucket ») ou intégration simplifiée.
- Hors-champ lointain : quasi gelé / agrégé.

Réglable, désactivable pour mesurer le coût brut.

---

## 8. Boucle de temps

- Simulation en **timestep fixe** dans `_PhysicsProcess` (déterministe, indispensable pour un futur multi).
- Rendu interpolé dans `_Process` si besoin de fluidité visuelle > tick rate.
- Cap FPS configurable (30/60) pour la batterie mobile.

---

## 9. Contraintes & budgets

### Budget CPU (indicatif, à valider par le benchmark)
| Poste | Cible / frame @10k entités |
|-------|-----------------------------|
| Rebuild grille | < 0.5 ms |
| Flocking | < 3 ms |
| Pathfinding (lecture) | < 0.5 ms |
| Collisions | < 2 ms |
| Rendu (push MultiMesh) | < 1 ms |

### Mobile = facteur limitant
- **Thermal throttling** : viser ~60 % du pic soutenu.
- **Draw calls** : instancing obligatoire (déjà prévu).
- **Mémoire** : SoA compact, pas de garbage par frame (zéro `new` dans la boucle).
- **GC .NET** : bannir les allocations par frame (pas de LINQ, pas de `List` temporaires dans les systèmes) → sinon micro-freezes.

### Compatibilité multi (préparé, pas implémenté)
- Timestep fixe + simulation déterministe = base d'un futur lockstep.
- Séparer clairement **état de simulation** (rejouable) et **présentation** (rendu/UI).

---

## 10. Limites connues de l'approche

- **Godot C# + GC** : il faut être discipliné sur les allocations ; c'est la principale source de saccades.
- **Pas de multithreading natif ECS** comme Unity DOTS : Godot permet le `System.Threading.Tasks.Parallel`, mais avec précautions (pas d'API Godot hors thread principal). On paralléllisera flocking/collisions sur les tableaux SoA, pas les appels moteur.
- **Au-delà de ~50-100k** entités sur mobile : il faudra passer à la simulation sur GPU (compute shaders) — hors périmètre du prototype.
- **Pathfinding individuel massif** (chaque entité sa propre cible) : non couvert par un flow field unique ; nécessitera du caching/groupes.

---

## 11. Arborescence du projet

```
project.godot            # config projet Godot 4 .NET
Spread.csproj            # projet C#
Main.tscn                # scène minimale (un Node2D + Camera2D, tout est construit en code)
scripts/
  Config.cs              # constantes/paramètres réglables
  Game.cs                # orchestrateur : boucle, systèmes, spawn
  Simulation.cs          # données SoA des entités + intégration
  SpatialHashGrid.cs     # grille de hachage spatiale
  FlowField.cs           # champ de direction (pathfinding de masse)
  Systems/
    FlockingSystem.cs
    StateMachineSystem.cs
    CollisionSystem.cs
  Rendering/
    EntityRenderer.cs    # MultiMeshInstance2D
  UI/
    DebugHud.cs          # FPS, ms, N, contrôles
```

---

## 12. Ce que le benchmark doit répondre

1. Combien d'entités à 60 FPS sur PC ? à 30 FPS ?
2. Idem sur un mobile de référence (Android milieu de gamme).
3. Quel système coûte le plus (profiling par poste) ?
4. Le LOD de simulation double-t-il/triple-t-il la capacité ?
5. Y a-t-il des saccades GC ? (à traquer et éliminer)

Ces chiffres décident si on part sur Godot pour le vrai projet, ou s'il faut viser Unity DOTS / simulation GPU.
