using HarmonyLib;
using UnityEngine;

public class AvatarZombieControllerPatches
{
    [HarmonyPatch(typeof(AvatarZombieController), "Electrocute")]
    public class AZC_Electrocute
    {
        public static bool Prefix(AvatarZombieController __instance, bool enabled)
        {
            if (enabled)
            {
                /* original vanilla code replaced
                Material mainZombieBodyMaterial = GetMainZombieBodyMaterial();
                if ((bool)mainZombieBodyMaterial)
                {
                    mainZombieBodyMaterial.EnableKeyword("_ELECTRIC_SHOCK_ON");
                }
                */

                var allRenderers = __instance.entity.GetComponentsInChildren<Renderer>();

                foreach (var rend in allRenderers)
                {
                    foreach (var mat in rend.materials)
                    {
                        mat.EnableKeyword("_ELECTRIC_SHOCK_ON");
                    }
                }

                if ((bool)__instance.dismemberMat)
                {
                    __instance.dismemberMat.EnableKeyword("_ELECTRIC_SHOCK_ON");
                }
                if ((bool)__instance.mainZombieMaterialCopy)
                {
                    __instance.mainZombieMaterialCopy.EnableKeyword("_ELECTRIC_SHOCK_ON");
                }
                if ((bool)__instance.gibCapMaterialCopy)
                {
                    __instance.gibCapMaterialCopy.EnableKeyword("_ELECTRIC_SHOCK_ON");
                }
                __instance.StartAnimationElectrocute(0.6f);
            }
            else
            {
                /* original vanilla code replaced
                Material mainZombieBodyMaterial2 = GetMainZombieBodyMaterial();
                if ((bool)mainZombieBodyMaterial2)
                {
                    mainZombieBodyMaterial2.DisableKeyword("_ELECTRIC_SHOCK_ON");
                }
                */

                var allRenderers = __instance.entity.GetComponentsInChildren<Renderer>();
                foreach (var rend in allRenderers)
                {
                    foreach (var mat in rend.materials)
                    {
                        mat.DisableKeyword("_ELECTRIC_SHOCK_ON");
                    }
                }

                if ((bool)__instance.dismemberMat)
                {
                    __instance.dismemberMat.DisableKeyword("_ELECTRIC_SHOCK_ON");
                }
                if ((bool)__instance.mainZombieMaterialCopy)
                {
                    __instance.mainZombieMaterialCopy.DisableKeyword("_ELECTRIC_SHOCK_ON");
                }
                if ((bool)__instance.gibCapMaterialCopy)
                {
                    __instance.gibCapMaterialCopy.DisableKeyword("_ELECTRIC_SHOCK_ON");
                }
                __instance.StartAnimationElectrocute(0f);
            }

            return false;
        }

    }
}
