using System;
using HarmonyLib;

[HarmonyPatch(typeof(XUiC_TargetBar), "GetBindingValueInternal")]
public static class TargetBarRendBindingsPatch
{
    private const string RendBuffName = "buffInjuryRend";
    private const string RendStacksCVar = ".rendStacks";

    private const string BindingIsRend = "CATUI_EntityIsRend";
    private const string BindingRendStacks = "CATUI_EntityRendStacks";

    private const string NickelSlowBuffName = "buffInjuryNickelSlow";
    private const string NickelSlowStacksCVar = ".nickelSlowStacks";

    private const string NickelFreezeBuffName = "buffInjuryNickelFreeze";

    private const string BindingIsNickelSlow = "CATUI_EntityIsNickelSlow";
    private const string BindingNickelSlowStacks = "CATUI_EntityNickelSlowStacks";
    private const string BindingIsNickelFrozen = "CATUI_EntityIsNickelFrozen";

    public static void Postfix(XUiC_TargetBar __instance, ref bool __result, ref string value, string bindingName)
    {
        if (__result)
        {
            return;
        }

        var isSupportedBinding =
            string.Equals(bindingName, BindingIsRend, StringComparison.Ordinal) ||
            string.Equals(bindingName, BindingRendStacks, StringComparison.Ordinal) ||
            string.Equals(bindingName, BindingIsNickelSlow, StringComparison.Ordinal) ||
            string.Equals(bindingName, BindingNickelSlowStacks, StringComparison.Ordinal) ||
            string.Equals(bindingName, BindingIsNickelFrozen, StringComparison.Ordinal);

        if (!isSupportedBinding)
        {
            return;
        }

        try
        {
            EntityAlive target = null;
            if (__instance != null)
            {
                target = __instance.Target;
            }
            var buffs = target != null ? target.Buffs : null;
            var hasRend = buffs != null && buffs.HasBuff(RendBuffName);
            var hasNickelSlow = buffs != null && buffs.HasBuff(NickelSlowBuffName);
            var hasNickelFreeze = buffs != null && buffs.HasBuff(NickelFreezeBuffName);

            if (string.Equals(bindingName, BindingIsRend, StringComparison.Ordinal))
            {
                value = hasRend ? "True" : "False";
                __result = true;
                return;
            }

            if (string.Equals(bindingName, BindingRendStacks, StringComparison.Ordinal))
            {
                if (!hasRend)
                {
                    value = "0";
                    __result = true;
                    return;
                }

                var stacks = (int)Math.Round(buffs.GetCustomVar(RendStacksCVar), MidpointRounding.AwayFromZero);
                if (stacks < 0)
                {
                    stacks = 0;
                }

                value = stacks.ToString();
                __result = true;
                return;
            }

            if (string.Equals(bindingName, BindingIsNickelSlow, StringComparison.Ordinal))
            {
                value = hasNickelSlow ? "True" : "False";
                __result = true;
                return;
            }

            if (string.Equals(bindingName, BindingNickelSlowStacks, StringComparison.Ordinal))
            {
                if (!hasNickelSlow)
                {
                    value = "0";
                    __result = true;
                    return;
                }

                var stacks = (int)Math.Round(buffs.GetCustomVar(NickelSlowStacksCVar), MidpointRounding.AwayFromZero);
                if (stacks < 0)
                {
                    stacks = 0;
                }

                value = stacks.ToString();
                __result = true;
                return;
            }

            if (string.Equals(bindingName, BindingIsNickelFrozen, StringComparison.Ordinal))
            {
                value = hasNickelFreeze ? "True" : "False";
                __result = true;
            }
        }
        catch (Exception exception)
        {
            Log.Warning(string.Format("[3Scrubs][CATUI Patch] Failed to evaluate '{0}': {1}", bindingName, exception.Message));
        }
    }
}
