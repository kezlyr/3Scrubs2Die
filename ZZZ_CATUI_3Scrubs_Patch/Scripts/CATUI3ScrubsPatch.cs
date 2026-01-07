using HarmonyLib;

public sealed class CATUI3ScrubsPatch : IModApi
{
    public void InitMod(Mod mod)
    {
        var harmony = new Harmony("3scrubs.catui.patch");
        harmony.PatchAll();
        Log.Out("[3Scrubs][CATUI Patch] Harmony patches applied");
    }
}

