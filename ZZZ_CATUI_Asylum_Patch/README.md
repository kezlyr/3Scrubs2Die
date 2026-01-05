# CATUI ZombieINC Vehicle Compatibility Patch

## Description
This mod fixes compatibility issues between CATUI and ZombieINC vehicles that cause binding evaluation errors and UI exceptions.

## Problem
When using ZombieINC vehicles with CATUI, you may see errors like:
```
[XUi] Binding expression can not be evaluated: {# CATUI_VehicleCurrentSpeedFill * 0.8 }
Input string was not in a correct format.
```

This happens because ZombieINC vehicles have custom buffs and configurations that interfere with CATUI's vehicle data reading.

## Solution
This patch provides:

1. **Safe Binding Evaluation**: Intercepts CATUI vehicle binding errors and provides safe fallback values
2. **Harmony Patches**: Uses Harmony to patch the binding evaluation system
3. **Custom Controller**: Provides a backup vehicle data controller

## Features
- Fixes speed display errors
- Handles brake/turbo indicator issues  
- Provides safe inventory count displays
- Maintains headlight functionality
- Logs warnings instead of throwing exceptions

## Installation
1. Extract this mod to your `Mods` folder
2. Make sure it loads AFTER both CATUI and ZombieINC mods (hence the ZZZ prefix)
3. Restart the game

## Load Order
This mod should load last:
1. CATUI
2. ZombieINC mods
3. **ZZZ_CATUI_ZombieINC_Patch** (this mod)

## Compatibility
- Requires CATUI mod
- Requires ZombieINC vehicle mods
- Compatible with 7 Days to Die A21+

## Technical Details
The patch works by:
1. Intercepting `BindingItemNcalc.EvaluateExpression()` calls
2. Detecting CATUI vehicle binding expressions
3. Providing safe fallback values when evaluation fails
4. Logging warnings instead of throwing exceptions

## Troubleshooting
If you still see errors:
1. Check mod load order
2. Verify all required mods are installed
3. Check the game log for "[CATUI Patch]" messages
4. Report issues with full log output

## Version
1.0.0 - Initial release
