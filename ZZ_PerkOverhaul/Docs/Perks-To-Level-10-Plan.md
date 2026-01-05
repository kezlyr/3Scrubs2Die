# Perks to Level 10 - Upgrade Plan (ZZ_PerkOverhaul)

## Goal
Turn every perk line that currently has **5 ranks** into a **10-rank** perk line, while keeping the "shape" and pacing of ranks 1-5 consistent and predictable.

In this mod setup, most perks are effectively "ranked" by how many `<level_requirements>` nodes they define (vanilla does **not** usually use `max_level="5"`). So "perks that only go to level 5" means:

- Any `<perk>` with **exactly 5** `<level_requirements>` entries.

## Non-Goals (for this pass)
- No UI changes.
- No new perk trees or new attributes.
- No major rebalance pass beyond extending existing patterns.
- No new buffs or triggered_effect mechanics unless needed to keep scaling coherent.

## Constraints & Reality Checks
1. **Attributes will be extended to 20.** Vanilla 5-rank perks often require `attX` up to 10 by rank 5.
  - For ranks 6-10, we can continue raising attribute requirements above 10 (since attributes will go to 20).
  - Optionally, we can still add a `PlayerLevel` or prerequisite gate if we want tighter pacing.

2. **A perk's "power" is not always linear.** Some perks scale in steps (big jump at rank 3 or 5), others are flat unlocks.
  - The plan must classify each perk's rank 1-5 pattern and extend it using a matching rule.

3. **XML patching is fragile when effect names are invalid.** Any new passive effect names must be verified against game-supported names.

## How to Identify Target Perks
### Vanilla definition
In [Data/Config/progression.xml](../../Data/Config/progression.xml), a "5-rank perk" looks like:

- `<perk name="...">` with `level_requirements` for `level="1".."5"`.

### Mod definition
In this mod, many perks are modified using `<set>` and `<append>` patches, but the perk *definition* still lives in vanilla.

Therefore, to make a perk 10 ranks we need to:

- Patch the original perk's **level requirements** to include ranks **6-10**.
- Extend any **rank-indexed effects** that currently use `level="1,5"` ranges or `level="1,2,3,4,5"` lists.
- Extend the **effect_description** list to include `level="6".."10"` keys.

## Standardized Rules (so the whole tree feels consistent)

### Rule A - Rank gates (requirements)
Pick one consistent gating strategy and apply it across all perks unless a perk has special needs.

Recommended default:
- Keep attribute requirement progression identical for ranks 1-5.
- For ranks 6-10:
  - Continue the attribute requirement progression above 10 (since attributes will be extended to 20).
  - Use `PlayerLevel` gates only if you want additional pacing beyond attribute investment.

Example (illustrative):
- Rank 6: `attX >= 12`
- Rank 7: `attX >= 14`
- Rank 8: `attX >= 16`
- Rank 9: `attX >= 18`
- Rank 10: `attX >= 20`

Optional (secondary pacing): add `PlayerLevel` thresholds alongside the above if desired.

Why:
- Keeps rank 6-10 driven by attribute investment.
- If PlayerLevel gates are added, they help smooth pacing for fast-leveling servers.

### Rule B - Numeric passive effects (the scaling engine)
For any passive effect that clearly scales across ranks 1-5, extend it to 10 ranks using the pattern it already uses.

#### Pattern 1: Linear progression
If ranks 1-5 are evenly spaced (or intended to be):

- Let $\Delta = (v_5 - v_1) / 4$.
- Extend:
  - $v_6 = v_5 + \Delta$
  - …
  - $v_{10} = v_5 + 5\Delta$

Use this when:
- The perk's values look like a straight line.
- The effect is "percentage add" or "base add" with simple pacing.

#### Pattern 2: Step progression (milestone ranks)
If the perk has obvious milestone ranks (e.g. big jump at 3 and 5):

- Preserve the milestone style:
  - Either add new milestones at 7 and 10, or
  - Repeat the last step size for 6-10.

Use this when:
- Ranks 1-5 contain "unlock at rank 3" style logic.
- Triggered effects are gated by `ProgressionLevel ... GTE 3/5`.

#### Pattern 3: Multiplicative progression
If the perk scales by ratios (rare, but possible in mods):

- Let $r = v_5 / v_4$ (or use a best-fit ratio).
- Extend using $v_{n} = v_{n-1} * r$.

Use this when:
- Values form a geometric-like curve.

#### Pattern 4: Flat unlock / boolean effects
If the perk is mostly "unlocks recipe" or "enables ability" and doesn't scale:

- Keep the unlock rank as-is.
- Use ranks 6-10 for *secondary* improvements (small numeric bonuses) **only if** the perk already had numeric effects.
- Otherwise, ranks 6-10 can be QoL increments (durability, stamina cost, handling), but keep it modest.

### Rule C - Effect descriptions & localization
Every new rank needs tooltip text.

For each perk `perkX`:
- Add 5 new keys:
  - `perkXRank6Desc` … `perkXRank10Desc`
  - `perkXRank6LongDesc` … `perkXRank10LongDesc`

Plan for writing:
- Build the rank 6-10 text from the same deltas used for effects.
- Keep language consistent with existing Better Perks longdesc style.

## Implementation Phases

### Phase 1 - Inventory & classification
1. Generate a list of all perks with 5 level_requirements.
2. For each perk, classify its rank 1-5 behavior:
   - Linear numeric
   - Step/milestone
   - Unlock-only
   - Mixed

Deliverable:
- A spreadsheet-like table (can be a Markdown table) mapping:
  - perk name
  - parent skill
  - pattern type
  - gating strategy
  - notes (milestones, special triggers)

#### Auto-generated inventory (vanilla + this mod)

Counts:
- Vanilla perks with 5 ranks: **50**
- Mod-defined perks with 5 ranks: **4**

Default gating strategy for ranks 6-10 (unless we mark a perk as special-case later): Rule A (attribute-based gating)
- Rank 6: attX >= 12
- Rank 7: attX >= 14
- Rank 8: attX >= 16
- Rank 9: attX >= 18
- Rank 10: attX >= 20

| Source | Perk | Parent | Ranks | Rank-5 att req | Pattern guess | Notes |
|---|---|---|---:|---|---|---|
| vanilla | perkAgilityMastery | skillAgilityAthletics | 5 | attAgility>=10 | mixed | has triggered_effect |
| vanilla | perkHardTarget | skillAgilityAthletics | 5 | attAgility>=10 | linear-candidate (range endpoints) |  |
| vanilla | perkParkour | skillAgilityAthletics | 5 | attAgility>=10 | linear-candidate (explicit list) | has triggered_effect |
| vanilla | perkRunAndGun | skillAgilityAthletics | 5 | attAgility>=10 | linear-candidate (explicit list) |  |
| vanilla | perkArchery | skillAgilityCombat | 5 | attAgility>=10 | mixed (range+list) |  |
| vanilla | perkDeepCuts | skillAgilityCombat | 5 | attAgility>=10 | step/milestone (mixed) | has triggered_effect |
| vanilla | perkFlurryOfAgility | skillAgilityCombat | 5 | attAgility>=10 | linear-candidate (explicit list) |  |
| vanilla | perkGunslinger | skillAgilityCombat | 5 | attAgility>=10 | step/milestone (mixed) | has triggered_effect |
| vanilla | perkFromTheShadows | skillAgilityStealth | 5 | attAgility>=10 | mixed (range+list) |  |
| vanilla | perkHiddenStrike | skillAgilityStealth | 5 | attAgility>=10 | mixed (range+list) |  |
| vanilla | perkBrawler | skillFortitudeCombat | 5 | attFortitude>=10 | step/milestone (mixed) | has triggered_effect |
| vanilla | perkFlurryOfFortitude | skillFortitudeCombat | 5 | attFortitude>=10 | linear-candidate (explicit list) |  |
| vanilla | perkMachineGunner | skillFortitudeCombat | 5 | attFortitude>=10 | step/milestone (mixed) | has triggered_effect |
| vanilla | perkHealingFactor | skillFortitudeRecovery | 5 | attFortitude>=10 | linear-candidate (explicit list) |  |
| vanilla | perkRuleOneCardio | skillFortitudeRecovery | 5 | attFortitude>=10 | mixed (range+list) |  |
| vanilla | perkSiphoningStrikes | skillFortitudeRecovery | 5 | attFortitude>=10 | mixed | has triggered_effect |
| vanilla | perkSlowMetabolism | skillFortitudeRecovery | 5 | attFortitude>=10 | linear-candidate (range endpoints) | has triggered_effect |
| vanilla | perkFortitudeMastery | skillFortitudeSurvival | 5 | attFortitude>=10 | linear-candidate (range endpoints) | has triggered_effect |
| vanilla | perkPainTolerance | skillFortitudeSurvival | 5 | attFortitude>=10 | linear-candidate (range endpoints) |  |
| vanilla | perkTheHuntsman | skillFortitudeSurvival | 5 | attFortitude>=10 | linear-candidate (range endpoints) |  |
| vanilla | perkElectrocutioner | skillIntellectCombat | 5 | attIntellect>=10 | step/milestone (mixed) | has triggered_effect |
| vanilla | perkFlurryOfIntellect | skillIntellectCombat | 5 | attIntellect>=10 | linear-candidate (explicit list) |  |
| vanilla | perkTurrets | skillIntellectCombat | 5 | attIntellect>=10 | mixed (range+list) |  |
| vanilla | perkAdvancedEngineering | skillIntellectCraftsmanship | 5 | attIntellect>=10 | mixed (range+list) |  |
| vanilla | perkGreaseMonkey | skillIntellectCraftsmanship | 5 | attIntellect>=10 | linear-candidate (range endpoints) |  |
| vanilla | perkIntellectMastery | skillIntellectCraftsmanship | 5 | attIntellect>=10 | step/milestone (mixed) | has triggered_effect |
| vanilla | perkPhysician | skillIntellectCraftsmanship | 5 | attIntellect>=10 | mixed (range+list) | has triggered_effect |
| vanilla | perkBetterBarter | skillIntellectInfluence | 5 | attIntellect>=10 | linear-candidate (explicit list) |  |
| vanilla | perkCharismaticNature | skillIntellectInfluence | 5 | attIntellect>=10 | step/milestone | has triggered_effect |
| vanilla | perkDaringAdventurer | skillIntellectInfluence | 5 | attIntellect>=10 | linear-candidate (range endpoints) |  |
| vanilla | perkDeadEye | skillPerceptionCombat | 5 | attPerception>=10 | mixed (range+list) | has triggered_effect |
| vanilla | perkDemolitionsExpert | skillPerceptionCombat | 5 | attPerception>=10 | mixed (range+list) |  |
| vanilla | perkFlurryOfPerception | skillPerceptionCombat | 5 | attPerception>=10 | linear-candidate (explicit list) |  |
| vanilla | perkJavelinMaster | skillPerceptionCombat | 5 | attPerception>=10 | step/milestone (mixed) | has triggered_effect |
| vanilla | perkAnimalTracker | skillPerceptionGeneral | 5 | attPerception>=7 | mixed (range+list) | has triggered_effect |
| vanilla | perkInfiltrator | skillPerceptionGeneral | 5 | attPerception>=7 | mixed (range+list) |  |
| vanilla | perkPenetrator | skillPerceptionGeneral | 5 | attPerception>=10 | mixed (range+list) |  |
| vanilla | perkPerceptionMastery | skillPerceptionGeneral | 5 | attPerception>=10 | linear-candidate (range endpoints) | has triggered_effect |
| vanilla | perkSalvageOperations | skillPerceptionScavenging | 5 | attPerception>=7 | mixed (range+list) |  |
| vanilla | perkTreasureHunter | skillPerceptionScavenging | 5 | attPerception>=7 | step/milestone (mixed) | has triggered_effect |
| vanilla | perkBoomstick | skillStrengthCombat | 5 | attStrength>=10 | step/milestone (mixed) | has triggered_effect |
| vanilla | perkFlurryOfStrength | skillStrengthCombat | 5 | attStrength>=10 | linear-candidate (explicit list) |  |
| vanilla | perkGrandSlam | skillStrengthCombat | 5 | attStrength>=10 | mixed | has triggered_effect |
| vanilla | perkPummelPete | skillStrengthCombat | 5 | attStrength>=10 | step/milestone (mixed) | has triggered_effect |
| vanilla | perkSkullCrusher | skillStrengthCombat | 5 | attStrength>=10 | step/milestone (mixed) | has triggered_effect |
| vanilla | perkJunkMiner | skillStrengthConstruction | 5 | attStrength>=10 | linear-candidate (explicit list) |  |
| vanilla | perkMiner69r | skillStrengthConstruction | 5 | attStrength>=7 | mixed (range+list) |  |
| vanilla | perkMotherLode | skillStrengthConstruction | 5 | attStrength>=7 | linear-candidate (range endpoints) |  |
| vanilla | perkPackMule | skillStrengthGeneral | 5 | attStrength>=5 | linear-candidate (explicit list) | has triggered_effect |
| vanilla | perkStrengthMastery | skillStrengthGeneral | 5 | attStrength>=10 | step/milestone (mixed) | has triggered_effect |


Pattern legend (heuristic):
- **linear-candidate**: mostly uses `level="1,5" value="a,b"` or explicit lists; extend by continuing the same slope.
- **step/milestone**: has rank 3/5 milestone triggers; extend by adding milestones at 7/10 or continuing the last step size.
- **mixed**: needs manual review per-effect.

### Phase 2 - Apply a "template patch" per perk
For each perk, perform these edits:

1. **Extend level requirements:** add `<level_requirements level="6".."10"> ... </level_requirements>`
2. **Extend effects:**
   - For effects using `level="1,5" value="a,b"`, change to `level="1,10" value="a, ... , j"` (10 values).
   - For effects using `level="1,2,3,4,5"`, extend to 10 entries.
   - For triggered effects gated at 3 or 5, consider adding thresholds at 7 and/or 10.
3. **Extend descriptions:** ensure effect_description lists include 6-10.

### Phase 3 - Balance sanity checks
For each perk:
- Compare "power per point" at ranks 1-5 vs 6-10.
- Avoid "double-dipping" if the perk already stacks with another perk line.

### Phase 4 - Validation
- XML parse checks (no invalid effect names, no malformed XML).
- In-game checks:
  - Perk shows 10 ranks.
  - Requirements display correctly.
  - Effects apply at ranks 6-10.

## Tooling / Scripts (recommended)

### 1) List perks with exactly 5 ranks (vanilla)
PowerShell snippet:
```powershell
$vanilla=[xml](Get-Content -LiteralPath 'Data\Config\progression.xml' -Raw)
$perks=$vanilla.SelectNodes('//progression/perks/perk')
$perks | ForEach-Object {
  [pscustomobject]@{
    name=$_.GetAttribute('name')
    parent=$_.GetAttribute('parent')
    lrCount=$_.SelectNodes('level_requirements').Count
  }
} | Where-Object { $_.lrCount -eq 5 } | Sort-Object parent,name
```

### 2) Find rank-scaled effects inside your mod patches
Search patterns:
- `level="1,5"`
- `level="1,2,3,4,5"`

This finds where we must add 6-10 values.

## Worked Example (pattern: linear)
If a perk has:
```xml
<passive_effect name="ReloadSpeedMultiplier" operation="perc_add" level="1,5" value="0.05,0.25" />
```
Then ranks 1-5 are linear.
- $\Delta = (0.25 - 0.05)/4 = 0.05$
- Extend to rank 10:
  - 0.05, 0.10, 0.15, 0.20, 0.25, 0.30, 0.35, 0.40, 0.45, 0.50

## Risks & Edge Cases
- Perks with multiple tags and stacking sources can get out of hand by rank 10.
- Triggered effects with "Equals 5" gates may never fire at ranks 6-10 unless expanded.
- Some perks are 3-rank or 4-rank by design (`max_level` is used there); do not change those unless explicitly requested.

## Acceptance Criteria
- Every perk that had exactly 5 ranks now displays 10 ranks.
- Rank 6-10 feel like a continuation of rank 1-5 (same pacing style).
- No XML load errors.

---

### Next Step I can do automatically
If you want, I can generate the Phase 1 inventory table directly into this doc (perk name → pattern guess → where to patch), using your current vanilla + mod files.
