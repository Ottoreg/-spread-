# -spread- — Architecture technique (étude de faisabilité)

> Prototype-benchmark : **twin-stick roguelike** dans le corps d'un hôte.
> Le joueur incarne un **virus** ; des milliers d'**anticorps** errent puis
> s'activent pour l'attaquer. Rendu **fil-de-fer** (formes géométriques).
> Moteur : **Godot 4.x en C# (.NET)**. Cible : **PC d'abord** (mobile plus tard),
> solo (multi plus tard).

## Concept

- **Joueur = virus** : contrôle twin-stick (déplacement clavier, visée souris,
  tir maintenu). Doit infecter l'hôte ; les projectiles détruisent les anticorps.
- **Anticorps = les milliers d'entités** : à l'état **dormant** ils errent dans
  la map ; quand le joueur s'approche (rayon d'activation) ils passent **activés**
  et le poursuivent en nuée.
- **Map** : grande zone ouverte continue = organes interconnectés de l'hôte
  (obstacles/murs d'organes à venir ; le flow field les gère déjà par conception).

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
_PhysicsProcess(delta)  — timestep fixe
  1. Player.Tick        → déplacement, visée, tir (spawn projectiles)
  2. FlowField retarget → re-cible le champ sur le joueur (amorti /8 ticks)
  3. SpatialGrid.Rebuild→ réindexe les entités dans la grille
  4. AntibodySystem     → activation + errance (dormant) / nuée+poursuite (activé)
  5. Integrate          → applique vitesse (cap selon état), bornes, Position
  6. CollisionSystem    → résout les chevauchements (via grille)
  7. ProjectileSystem   → déplace les tirs, résout impacts (via grille), kills
  8. Simulation.Compact → retire les anticorps détruits (compaction dense)
  9. ContactDamage      → anticorps activés touchant le joueur → dégâts

_Process(delta)
  • EntityRenderer / ProjectileRenderer → MultiMesh (fil-de-fer) + culling
  • DebugHud → FPS, populations, ms par système
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

Dans le jeu : **dormant** = errance seule (+ séparation) ; **activé** = les trois
règles de nuée + poursuite du joueur via le flow field.

### 5.2 Pathfinding : flow field (champ de direction) vers le joueur
Pour faire poursuivre **des milliers** d'anticorps vers le joueur, on **ne fait
PAS un A\* par entité**. On calcule **un seul flow field** partagé, re-ciblé sur
le joueur :
- Une grille de navigation (indépendante de la grille spatiale, plus grossière).
- Un BFS depuis la cellule du joueur produit un champ « coût », puis un champ
  « direction » (vecteur par cellule pointant vers le joueur, contournant les
  futurs obstacles/murs d'organes).
- Chaque anticorps activé lit le vecteur de sa cellule → **O(1) par entité**.
- Recalculé périodiquement (le joueur bouge), coût amorti sur plusieurs ticks.

C'est LA technique clé qui rend le pathfinding de masse viable.

### 5.3 Machine à états (FSM) : activation
FSM légère encodée en `byte[] State` (0 = Dormant, 1 = Activé). Activation par
rayon autour du joueur (extensible : bruit, alerte propagée, déclencheurs de
zone). Data-driven, pas de branchements lourds par entité.

### 5.4 Joueur & projectiles
Le joueur est une entité à part (un seul, donc pas d'instancing) : twin-stick,
rendu fil-de-fer via `_Draw`. Les projectiles d'infection sont un **pool SoA**
séparé (spawn/mort par swap, zéro allocation), résolus contre les anticorps via
la même grille spatiale.

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
Main.tscn                # scène minimale (un Node2D ; tout est construit en code)
scripts/
  Config.cs              # constantes/paramètres réglables
  Game.cs                # orchestrateur : boucle, systèmes, joueur, caméra
  Simulation.cs          # données SoA des anticorps + intégration + compaction
  Projectiles.cs         # pool SoA des projectiles d'infection
  Player.cs              # le virus : twin-stick, visée, tir, rendu fil-de-fer
  SpatialHashGrid.cs     # grille de hachage spatiale
  FlowField.cs           # champ de direction vers le joueur (pathfinding de masse)
  Systems/
    AntibodySystem.cs    # activation + errance/nuée + poursuite
    CollisionSystem.cs   # séparation entité-entité
    ProjectileSystem.cs  # déplacement + impacts des projectiles
  Rendering/
    WireMesh.cs          # maillages fil-de-fer (polygones, segments)
    EntityRenderer.cs    # anticorps : MultiMeshInstance2D wireframe
    ProjectileRenderer.cs# projectiles : MultiMeshInstance2D wireframe
  UI/
    DebugHud.cs          # FPS, populations, ms, contrôles
```

---

## 12. Ce que le benchmark doit répondre

1. Combien d'entités à 60 FPS sur PC ? à 30 FPS ?
2. Idem sur un mobile de référence (Android milieu de gamme).
3. Quel système coûte le plus (profiling par poste) ?
4. Le LOD de simulation double-t-il/triple-t-il la capacité ?
5. Y a-t-il des saccades GC ? (à traquer et éliminer)

Ces chiffres décident si on part sur Godot pour le vrai projet, ou s'il faut viser Unity DOTS / simulation GPU.
