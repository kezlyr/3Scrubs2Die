using HarmonyLib;
using System.Reflection;

public class HarmonyInit : IModApi
{
    public void InitMod(Mod mod)
    {
        var harmony = new Harmony("1.0.Mods.ShockShaderPatch.Emu46");
        harmony.PatchAll(Assembly.GetExecutingAssembly());
        Log.Out($"InitMod ShockShaderPatch_Emu46");
    }
}
