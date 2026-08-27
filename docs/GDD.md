# Infect — Game Design Document (résumé)

> Concept par **Ottoreg** (2022). Résumé structuré du GDD servant de référence
> design pour le développement. Rogue-like d'action : le joueur incarne un
> **virus** qui infecte des hôtes en affrontant leur **système immunitaire**.

---

## 1. Pitch

Le joueur incarne un **virus** dont le but est d'**infecter le plus d'espèces et
d'individus possible**. Pour chaque hôte, le virus doit franchir les **défenses
immunitaires**, atteindre et faire **dysfonctionner un organe vital**. En
combattant, il récolte de l'**ADN viral** qui le fait **évoluer**, augmentant sa
létalité jusqu'à devenir une **maladie mortelle** pour l'espèce. Les défenses de
l'hôte **réagissent et s'adaptent** au fil de l'infection.

Une espèce est « vaincue » quand le virus a infecté et tué **tous les genres
d'individus** de cette espèce (jeune, adulte, mâle, femelle…).

---

## 2. Le niveau : l'hôte

- Le **niveau = l'hôte** infecté. Il est **généré procéduralement**.
- Le joueur se **déplace par les vaisseaux sanguins**, d'**organe en organe**.
- Le niveau est découpé en **groupes d'organes (systèmes)**, chacun défendu par
  des **cellules de défense spécifiques**.
- Avant de commencer, le joueur **choisit la zone d'entrée** de l'infection.
  Plus cette zone est proche d'un **organe vital**, plus le **niveau d'alerte
  initial** est élevé.

### Espèce & âge de l'hôte
- Chaque **espèce** a un système immunitaire propre.
- Au sein d'une espèce, la force immunitaire varie : un **adulte** résiste mieux,
  un **jeune** est contaminé plus facilement.
- Le virus doit **adapter ses compétences** selon l'espèce ET l'âge.
- En affrontant plusieurs hôtes d'une même espèce, le virus **s'adapte et évolue**
  pour être plus efficace ensuite.

---

## 3. Le niveau d'alerte

Représente l'**intensité de la réponse immunitaire**.

- **Augmente à chaque action d'infection** : infecter un **organe important** le
  fait beaucoup monter ; infecter une **simple cellule**, très peu.
- À **alerte maximale** : chaque partie du corps est protégée par un grand nombre
  de **cellules de défense (leucocytes)**, et les **centres de production** en
  génèrent à **intervalle régulier** jusqu'à la fin de l'alerte.

### Alerte initiale
Valeur de départ de l'alerte, variable selon : **type de virus**, **espèce de
l'hôte**, **charge virale initiale**, et **zone d'entrée choisie** (proximité d'un
organe vital = alerte plus haute d'entrée).

---

## 4. Le virus (joueur)

- But : infecter un maximum d'espèces ; rendre une espèce mortelle en tuant tous
  ses genres d'individus.
- À **chaque infection (réussie ou non)**, le virus **récolte de l'ADN viral**
  pour évoluer (compétences, létalité).
- Combat les défenses immunitaires de l'hôte.
- **Infection réussie** = avoir fait **défaillir un organe vital**. Le joueur peut
  ensuite **continuer** à détruire des cellules et infecter d'autres organes pour
  **plus d'ADN**.

---

## 5. Cellules : 3 grands types

| Type | Comportement | ADN viral lâché |
|---|---|---|
| **Défensive** | Cellules immunitaires. Doivent être **vaincues** avant d'être infectées. | **Offensif** (améliore attaque & infection) |
| **Proie** | Cellules des **organes vitaux**. Très **sensibles**, infectables **sans combat**. | **Survie** (améliore fuite, protection, régénération) |
| **Neutre** | Toutes les autres. Réduire leur **énergie vitale** jusqu'au **seuil d'infection**. | **Renforcement** (améliore n'importe quel type) |

### Cellules immunitaires (défensives) listées
Lymphocytes B et T · Macrophages · Neutrophiles · Compléments · Cellules
dendritiques · Cellules tueuses naturelles · Anticorps · Basophiles · Mastocytes
· Éosinophiles.

---

## 6. Viro-cellules (cellules infectées)

Une cellule infectée devient une **viro-cellule** :
- produit de l'**ADN viral** à intervalle régulier ;
- est **attaquée** par le système immunitaire (et par les virophages) ;
- si elle **survit assez longtemps**, l'incubation prend fin : elle **s'auto-détruit**
  et **relâche une plus grande quantité d'ADN viral**.

---

## 7. Virophages

- Des **virus rivaux** présents dans l'hôte qui **s'attaquent au virus du joueur**
  (et aux viro-cellules). Ils ont des **armes plus adaptées** pour le vaincre.
- **Cellules immunitaires et virophages s'ignorent mutuellement.**
- Vaincre un virophage libère de l'**ADN viral de son type**.

---

## 8. Bactéries & bactériophages

- Les **bactéries** de l'hôte peuvent **affaiblir les défenses immunitaires** en
  infectant des cellules (elles **monopolisent** une partie des défenses).
- Les actions du joueur peuvent **favoriser les bactéries** s'il monopolise
  lui-même les défenses.
- Les **bactériophages** attaquent les bactéries et **évitent un effet boule de
  neige** (bactéries + joueur épuisant toutes les défenses).

---

## 9. ADN viral (monnaie d'évolution)

Trois types, dépensés pour améliorer les compétences :

| Type d'ADN | Source principale | Améliore |
|---|---|---|
| **Offensif** | Cellules **défensives** vaincues, virophages | Attaque, Infection |
| **Survie** | Cellules **proies** (organes vitaux) | Fuite, Protection, Régénération |
| **Renforcement** | Cellules **neutres** | N'importe quel type |

Sources additionnelles : viro-cellules (production continue + burst à la fin
d'incubation), virophages vaincus.

---

## 10. Compétences & arbre de compétences

Compétences **actives ou passives**, regroupées par **type** dans des **arbres**.
Cinq branches :

| Branche | Effet | Coût (ADN) |
|---|---|---|
| **Attaque** | Éliminer plus vite les cellules défensives | Offensif |
| **Infection** | Renforce la propagation / contamination des cellules | Offensif |
| **Protection** | Réduit les dégâts et l'impact des contre-attaques | Survie |
| **Régénération** | Récupération plus rapide, annule certains effets (toxines, anticorps) | Survie |
| **Fuite** | Mobilité, esquive, éviter l'engagement contre ennemis puissants | Survie |

Règles clés :
- Le joueur **choisit un set limité** de compétences **avant chaque infection**,
  et **ne peut pas en changer** en cours d'infection → **choix stratégique**.
- Le **système immunitaire (et les virophages) s'adaptent** aux compétences
  choisies → oblige à **varier les approches** au fil du jeu.
- Chaque compétence peut être **améliorée en profondeur** dans sa branche.

---

## 11. Déroulé d'une infection

1. Choix d'un **point d'entrée**.
2. **Parcours** du corps depuis la zone d'entrée, à travers les organes.
3. **Vaincre / contourner** les défenses immunitaires rencontrées.
4. Atteindre un **organe vital**, le faire **dysfonctionner** puis **s'arrêter**.
5. Infection **achevée** — le joueur peut continuer pour récolter plus d'ADN.

---

## 12. Briques de gameplay

- **Exploration** : déplacement procédural dans les systèmes, choix des zones à infecter.
- **Combat** : temps réel (ou semi-temps réel) contre cellules et virophages.
- **Infection** : contamination des cellules, effets variables selon le type.
- **Progression** : gagner de l'ADN pour débloquer/améliorer les compétences.
- **Adaptation** : choix du set de compétences + réponse adaptative de l'immunité.
- **Évolution** : mutation du virus, effets long terme sur efficacité et létalité.

---

## 13. Boucle de gameplay

```
Choix de l'hôte + zone d'infection   → impacte alerte initiale, difficulté, défenses
        ↓
Infiltration & exploration           → déplacement par vaisseaux, reconnaissance
        ↓
Combat / Infection                   → affronter défenses, infecter cellules, récolter ADN
        ↓
Renforcement                         → compétences, choix stratégiques
        ↓
Atteinte d'un organe vital           → infection réussie = progression + récompenses + ↑ menace
        ↓
Retour menu / évolution              → adaptation aux prochaines cibles  → (boucle)
```

Le gameplay est une **boucle de tension croissante** : plus le joueur agit, plus
l'**alerte** monte et plus l'hôte devient dangereux (infiltration → survie →
destruction ciblée).

---

## Annexe — correspondance avec le prototype actuel

État du prototype `-spread-` vis-à-vis du GDD (pour situer ce qui existe déjà) :

| Élément GDD | État prototype |
|---|---|
| Virus joueur, twin-stick, tir d'infection | ✅ implémenté |
| Corps = organes reliés par vaisseaux, map ouverte | ✅ implémenté (génération procédurale simple) |
| Cellules de défense qui errent puis s'activent | ✅ base (anticorps dormants → activés, poursuite via flow field) |
| Navigation par les vaisseaux, collision membranes | ✅ implémenté |
| **Types de cellules** (défensive / proie / neutre) | ⬜ à faire |
| **Cellules immunitaires variées** (macrophages, lympho…) | ⬜ à faire |
| **Niveau d'alerte** + centres de production | ⬜ à faire |
| **Infection de cellules / viro-cellules** | ⬜ à faire |
| **ADN viral** (3 types) + **arbre de compétences** | ⬜ à faire |
| **Virophages**, **bactéries/bactériophages** | ⬜ à faire |
| **Organes vitaux** + condition de victoire | ⬜ à faire |
| **Choix hôte / espèce / âge / zone d'entrée** | ⬜ à faire (méta) |
| **Évolution inter-parties** | ⬜ à faire (méta) |

> Ce tableau n'est pas dans le GDD d'origine ; il sert de pont entre le design et
> l'implémentation en cours.
