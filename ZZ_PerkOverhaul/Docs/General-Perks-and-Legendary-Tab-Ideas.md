# General Perks + Legendary Tab Ideas (Draft)

Purpose
- Collect design ideas for **General Perks** (cross-attribute / universal) and a **Legendary** perk tab.
- Keep concepts **implementable** using typical 7DTD progression patterns (perk ranks, requirements, buffs, triggered effects).

Guiding principles
- General perks should provide **build glue** (quality-of-life, survivability, utility) without replacing attribute trees.
- Legendary perks should be **capstones**: powerful, limited in count, and gated behind late-game requirements.

---

## General Perks (Cross-Attribute)

These are meant to live outside the five attribute trees.

Selected set: **Field Medic**, **Scavenger’s Touch**, **Survivor’s Luck**, **Iron Will**, **Combat Fundamentals**, **Night Runner**, **Builder’s Discipline**, **Tough Skin**, **Resourceful**.

### 1) Field Medic
- Theme: emergency recovery and teammate support.
- Possible effects (per rank):
  - Increased healing item effectiveness.
  - Reduced healing item use time.
  - Revive/assist speed bonus (if applicable in your modpack rules).
  - Short “stabilized” buff after healing: reduced bleeding / DOT.

### 2) Scavenger’s Touch
- Theme: loot efficiency and exploration pacing.
- Possible effects:
  - Container search speed.
  - Small lootstage bonus (keep modest to avoid invalidating Perception).
  - Reduced tool degradation while harvesting trash/salvage.

### 3) Survivor’s Luck
- Theme: “bad luck protection” and clutch moments.
- Possible effects:
  - When dropping below a health threshold, gain brief damage resistance.
  - Reduced chance to sustain critical injuries.
  - Small stamina/health regen boost when near death.

### 4) Iron Will
- Theme: resist control and recover faster.
- Possible effects:
  - Stun/knockdown resistance.
  - Faster status recovery (concussion, cripple, slow).
  - Reduced stamina drain under debuffs.

### 5) Combat Fundamentals
- Theme: baseline combat proficiency across weapons.
- Possible effects:
  - Small universal weapon handling / reload / ADS bonus.
  - Small universal stamina cost reduction for attacks.
  - Avoid large raw damage boosts (leave that to attribute trees).

### 6) Night Runner
- Theme: nighttime mobility and survival.
- Possible effects:
  - Increased movement speed at night.
  - Reduced stamina cost while sprinting at night.
  - Slight stealth/noise reduction at night.

### 7) Builder’s Discipline
- Theme: base work cadence.
- Possible effects:
  - Faster block upgrade/repair.
  - Reduced material cost for repairs.
  - Reduced tool degradation when upgrading.

### 8) Tough Skin
- Theme: general mitigation without overshadowing armor perks.
- Possible effects:
  - Small damage resistance or crit resistance.
  - Reduced bleed chance.
  - Increased injury threshold.

### 9) Resourceful
- Theme: crafting yield efficiency.
- Possible effects:
  - Chance to not consume crafting ingredients (small chance; scales by rank).
  - Reduced workstation fuel use.

---

## Legendary Tab (Concept)

High-level goal
- A dedicated tab for **late-game, high-impact perks** that feel like “signature powers.”

### Structure (minimal)
- 6–12 Legendary perks total.
- Each Legendary perk has:
  - **Max rank**: 1 (simplest) or 3 (if you want progression).
  - **Hard gate** requirements:
    - Player Level threshold (e.g., 60/80/100), and/or
    - Attribute thresholds (e.g., two attributes at 15+), and/or
    - Completion gates (read X book sets, complete X quests), if you prefer.
  - **Soft gate**: high skill point cost.

### Acquisition rules (balance levers)
Pick one approach:
1) **Limited slots**: Only 1–2 Legendary perks can be active/purchased total.
2) **Legendary currency**: Costs a special token (quest reward / endgame drop).
3) **Mutual exclusivity**: Legendary perks belong to archetypes; choosing one locks out others.

### Legendary perk themes (examples)

#### A) Juggernaut (survival)
- Big damage resistance while below 50% HP.
- Strong injury/bleed resistance.
- Optional drawback: reduced sprint speed or increased stamina cost.

#### B) Executioner (combat)
- Mark a target on hit; marked target takes increased damage from you.
- On kill: short buff (attack speed / reload / move speed).

#### C) Ghost (stealth)
- Dramatically reduced stealth penalties while moving.
- Large sneak damage bonus on first hit after 3s undetected.

#### D) Warlord (leadership / team utility)
- Nearby allies gain small regen or stamina recovery.
- You gain bonuses when allies are nearby.

#### E) Engineer Supreme (base defense)
- Turrets/traps do significantly more damage / have improved handling.
- Workstations consume less fuel and craft faster.

#### F) Relentless (stamina engine)
- Stamina regen massively improved.
- Power attacks cost less.
- Optional drawback: reduced max stamina (or none, if you want pure reward).

#### G) Treasure King (economy)
- Strong barter bonus + extra quest choice.
- Small lootstage bonus.
- Keep it from eclipsing Perception by requiring Perception 15+ or completing treasure-related milestones.

---

## UI/UX notes for a new Legendary tab (ideas only)

If you add a new tab:
- Keep naming consistent with vanilla tabs.
- Legendary perks should be immediately recognizable as “special” via:
  - Unique icon set, or
  - A consistent prefix in perk names, or
  - A distinct category header.

Recommended minimal behavior
- Tab is hidden until the player meets a basic unlock condition (e.g., Player Level 50).
- Once unlocked, the tab shows Legendary perks with clear requirements and cost.

---

## Open questions (for locking scope)
- Do Legendary perks allow respec?
- Should Legendary perks be **rank 1 only** (cleanest) or 3 ranks (more progression)?
- Should General perks be unlimited, or capped similarly (to avoid bloating builds)?
