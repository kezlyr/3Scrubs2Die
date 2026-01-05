# Workstation Tags (7 Days to Die)

This document is a quick reference for the **tag values** related to workstations in this workspace, so we can consistently tag new workstation items/blocks and have them interact with progression/perks/loot the way we expect.

## 1) There are 3 different “tag-like” systems

When modding workstations, it’s easy to conflate these:

1. **Item/Block `Tags`** (property on `<item>` / `<block>`)
   - Example: `<property name="Tags" value="workstationSkill,twitch_workstation" />`
   - Used for: perk effects (`passive_effect tags="..."`), loot weighting, requirements like `HoldingItemHasTags`, some UI filters.

2. **Recipe `craft_area`** (attribute on `<recipe>`)
   - Example: `<recipe ... craft_area="workbench" />`
   - Used for: *where* the recipe can be crafted.

3. **Recipe `tags`** (attribute on `<recipe>`)
   - Example: `<recipe ... tags="workbenchCrafting,learnable" />`
   - Used for: recipe grouping, learnable filters, perk/magazine gating systems depending on the mod/vanilla logic.

This doc focuses on (1): **Item/Block `Tags` values** related to workstations.

## 2) Core workstation tag vocabulary in this workspace

### `workstationSkill`

**Meaning (practical):** “This is a workstation-related item/block.”

**Where it appears:**
- Vanilla items like `toolBellows`, `toolForgeCrucible`, dew collector mods, etc.
- Vanilla blocks like `cntDewCollector` (and other Collector-type blocks) are tagged `workstationSkill`.

**Why it matters:**
- Your progression/perk changes reference this tag in passive effects (e.g., loot probability boosts).

**Use it when:**
- You create a new *workstation add-on item* (things that go into a workstation’s mod slots).
- You create a new *workstation-like block* that you want counted as “workstation-related” for perks/loot rules.


### `workstationCSM`

**Meaning (practical):** “Workstation crafting skill magazine / crafting-skill progression item.”

**Where it appears:**
- Vanilla `workstationSkillMagazine` item uses `Tags="workstationCSM,csm"` and unlocks `craftingWorkstations`.

**Why it matters:**
- Perk effects can target this tag to affect magazine/skill-book drop odds.

**Use it when:**
- You add a new *workstation crafting-skill magazine* item type and want it to follow the same loot/progression tagging pattern.


### `csm`

**Meaning (practical):** “This is a crafting skill magazine (generic).”

**Where it appears:**
- On skill-magazine items alongside a more specific `*CSM` tag (e.g., `workstationCSM`).

**Use it when:**
- You’re creating new skill magazines that should behave like vanilla skill magazines.


### `twitch_workstation`

**Meaning (practical):** “This block should be recognized as a workstation for Twitch integration.”

**Where it appears:**
- Vanilla workstation-like blocks (e.g., collector blocks) often include this.

**Use it when:**
- You want your new workstation block to participate in Twitch systems (if you use them).
- If you don’t care about Twitch integration, it’s usually safe to omit.


## 3) Common companion tags for workstation items

These aren’t “workstation tags” per se, but they frequently show up on workstation add-ons and are useful patterns to follow.

### Example: `toolAnvil`

- Vanilla `toolAnvil` uses `Tags="toolAnvil,workstationSkill"`.
- That unique tag is then referenced by requirements like `HoldingItemHasTags`.

**Pattern:**
- Add `workstationSkill` for the overall category.
- Add a unique tag to enable conditional effects/requirements.


## 4) Tagging checklist when creating new workstation content

### A) Creating a new workstation add-on item (like bellows/anvil/crucible)

In `items.xml`:
- Include `workstationSkill` in `Tags`.
- Add a unique tag if you need requirements or special behavior (e.g., `toolMyNewAddon`).
- If it should be gated by workstation crafting progression, consider: `UnlockedBy="craftingWorkstations"`.

Example pattern:
```xml
<item name="toolMyNewAddon">
  <property name="UnlockedBy" value="craftingWorkstations" />
  <property name="Tags" value="workstationSkill,toolMyNewAddon" />
</item>
```


### B) Creating a new workstation-like block (placeable workstation)

In `blocks.xml` (minimum tagging):
- Include `workstationSkill` in the block `Tags`.
- Optionally include `twitch_workstation`.

If the block takes workstation add-ons:
- Use `RequiredMods="item1,item2,..."` and `RequiredModsOnly="true"` if you want those slots to be mandatory.

Example pattern:
```xml
<block name="cntMyWorkstation">
  <property name="Tags" value="workstationSkill,twitch_workstation" />
  <property name="RequiredMods" value="toolMyNewAddon" />
  <property name="RequiredModsOnly" value="true" />
</block>
```


## 5) Related (not tags, but commonly needed for new workstations)

These aren’t tags, but you’ll usually touch them when you make a new workstation block:

- **Workstation UI binding:** some mods use a block property like `<property name="Workstation" value="myWorkstationId"/>` plus an XUi `window_group` named `workstation_myWorkstationId`.
- **`craft_area`:** recipes must reference the correct `craft_area` for the station.

If you want, I can add a follow-up doc that maps: `block Class/Workstation properties` → `XUi window_group` → `recipe craft_area` conventions used in this install.
