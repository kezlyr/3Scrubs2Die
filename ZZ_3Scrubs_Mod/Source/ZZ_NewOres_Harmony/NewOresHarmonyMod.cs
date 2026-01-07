using System;
using System.Reflection;
using HarmonyLib;

namespace ZZ_NewOres_Harmony;

public class NewOresHarmonyMod : IModApi
{
	public void InitMod(Mod mod)
	{
		try
		{
			Log.Out("[ZZ_NewOres_Harmony] InitMod starting...");

			// Custom controller registration disabled - using vanilla UI patterns
			/*
			try
			{
				var controllerType = typeof(XUiC_ZZ_ForgeRightStackWindow);
				Log.Out($"[ZZ_NewOres_Harmony] Registering custom controller: {controllerType.FullName}");
			}
			catch (Exception ex)
			{
				Log.Error($"[ZZ_NewOres_Harmony] Controller registration failed: {ex}");
			}
			*/

			var harmony = new Harmony("zz.newores.forgeworkstation");
			harmony.PatchAll(Assembly.GetExecutingAssembly());
			Log.Out("[ZZ_NewOres_Harmony] Harmony PatchAll completed.");

			// Verify the critical patch is installed (the crash is inside Init).
			try
			{
				var m = AccessTools.Method(typeof(XUiC_WorkstationMaterialInputWindow), "Init");
				if (m == null)
				{
					Log.Error("[ZZ_NewOres_Harmony] Could not reflect XUiC_WorkstationMaterialInputWindow.Init");
				}
				else
				{
					var info = Harmony.GetPatchInfo(m);
					var prefixCount = info?.Prefixes?.Count ?? 0;
					var postfixCount = info?.Postfixes?.Count ?? 0;
					Log.Out($"[ZZ_NewOres_Harmony] PatchInfo for XUiC_WorkstationMaterialInputWindow.Init: prefixes={prefixCount}, postfixes={postfixCount}");
				}
			}
			catch (Exception ex)
			{
				Log.Error($"[ZZ_NewOres_Harmony] Patch verification failed: {ex}");
			}
		}
		catch (Exception ex)
		{
			Log.Error($"[ZZ_NewOres_Harmony] InitMod failed: {ex}");
		}
	}
}
