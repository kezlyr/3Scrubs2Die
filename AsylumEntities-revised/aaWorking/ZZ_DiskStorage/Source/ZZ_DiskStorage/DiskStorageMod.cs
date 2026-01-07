using System;
using HarmonyLib;

namespace ZZ_DiskStorage;

public class DiskStorageMod : IModApi
{
	public void InitMod(Mod mod)
	{
		try
		{
			Log.Out("[ZZ_DiskStorage] InitMod starting...");
			var harmony = new Harmony("zz.diskstorage");
			harmony.PatchAll();
			Log.Out("[ZZ_DiskStorage] Harmony patches applied.");
		}
		catch (Exception ex)
		{
			Log.Error($"[ZZ_DiskStorage] InitMod failed: {ex}");
		}
	}
}
