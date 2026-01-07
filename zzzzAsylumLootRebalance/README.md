# Asylum Loot Rebalance

A balance patch mod for the Asylum Overhaul that fixes early game progression issues with the loot system.

## Overview

This mod addresses several balance issues that made the early game too easy:

1. **Assault rifles appearing in T0 containers** - M4A1, AK47, HK417 were available from Day 1
2. **Legendary weapons dropping at Level 1** - No level requirements on legendary drops
3. **Quality 6 items at any level** - 28.8% chance for max quality from the start
4. **Teleport potions in common containers** - Powerful utility items too accessible
5. **High-tier resources without restrictions** - Forged titanium available immediately

## Installation

1. Copy the `AsylumLootRebalance` folder to your `7 Days to Die/Mods/` directory
2. The mod will automatically load after the core Asylum mods
3. No configuration required

## Changes Made

### 1. Level-Gated Probability Templates

New templates that prevent legendary items from dropping until appropriate player levels:

| Template | Old Behavior | New Behavior |
|----------|-------------|--------------|
| `SMALevelGated_T1` | 0.1% from level 1 | 0% until level 40, scales to 0.15% |
| `SMALevelGated_T2` | 0.5% from level 1 | 0% until level 60, scales to 0.8% |
| `SMALevelGated_T3` | 4% from level 1 | 0% until level 80, scales to 5% |
| `SMALevelGated_T4` | 8% from level 1 | 0% until level 100, scales to 8% |
| `SMALevelGated_T5` | 15% from level 1 | 0% until level 125, scales to 15% |

### 2. Weapon Tier Reorganization

Weapons moved to appropriate tiers based on their actual power level:

**Removed from T0 (moved to T1):**
- M4A1CL1, M4A1TJS1, AK47NMW1, M4A1NMW1, HK417CL1
- MK23, TT-33

**Removed from T1 (moved to T2):**
- M4A1KK1, HK417CXLH1

**Removed from T2 (moved to T3):**
- 50ZDH (heavy sniper)
- BQ

### 3. Level-Gated Quality Templates

The `SMAQualityLevelGated` template now scales quality chances with player level:

| Level Range | Max Quality | Q6 Chance |
|------------|-------------|-----------|
| 1-59 | Q2 | 0% |
| 60-99 | Q3 | 0% |
| 100-139 | Q4 | 0% |
| 140-179 | Q5 | 0% |
| 180+ | Q6 | 3% |

### 4. Teleport Potion Restrictions

Teleport potions now use `teleportPotionLevelGated`:
- 0% chance until level 80
- 2% at levels 80-124
- 5% at levels 125-164
- 8% at levels 165+

### 5. Resource Level Gating

**Forged Titanium** (`titaniumLevelGated`):
- 0% until level 60
- Scales from 15% to 50% at high levels
- Reduced quantities in forges (4-18 → 2-8)

**Special Metals** (Aluminum, Cobalt, Copper, Zinc):
- 0% until level 40
- Scales from 20% to 50% at high levels

### 6. Skill Coin Restrictions

- SkillCoinPart requires level 60+ to drop
- SkillCoinComplete requires level 125+ to drop

## Compatibility

- **Load Order**: Must load AFTER all Asylum mods it modifies
- **Compatible With**: All Asylum Overhaul mods
- **Conflicts**: May conflict with other mods that modify the same loot tables

## Uninstallation

Simply remove the `AsylumLootRebalance` folder from your Mods directory.

## Technical Details

This mod uses XPath commands to modify existing loot tables without replacing them:
- `<set>` - Updates attribute values
- `<remove>` - Removes items from groups
- `<append>` - Adds new items/templates

All changes target specific XML paths in the Asylum mods' loot configurations.

## Version History

### v1.0.0
- Initial release
- Added 10 level-gated probability templates
- Added 2 level-gated quality templates
- Reorganized T0-T3 weapon groups
- Level-gated teleport potions, titanium, and skill coins

## Credits

- Asylum Overhaul Team - Original mod
- Balance analysis based on vanilla 7 Days to Die loot progression

## License

This mod is provided as-is for the Asylum Overhaul community.

