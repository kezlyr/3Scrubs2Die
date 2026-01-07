using System;
using HarmonyLib;

[HarmonyPatch(typeof(EntityAlive), "FireAttackedEvents")]
public static class CopperAmmoRefundPatch
{
    private const string RefundProcBuff = "buffAmmoRefundProc";

    public static void Postfix(EntityAlive __instance, DamageResponse _dmResponse)
    {
        if (__instance is EntityPlayer)
        {
            return;
        }

        if (_dmResponse.Source == null)
        {
            return;
        }

        var damageSource = _dmResponse.Source;
        if (damageSource.ItemClass == null)
        {
            return;
        }

        var attackerId = damageSource.CreatorEntityId > 0 ? damageSource.CreatorEntityId : damageSource.ownerEntityId;
        if (attackerId <= 0)
        {
            return;
        }

        var attacker = GameManager.Instance.World.GetEntity(attackerId) as EntityPlayer;
        if (attacker == null || attacker.inventory == null || attacker.Buffs == null)
        {
            return;
        }

        if (!attacker.Buffs.HasBuff(RefundProcBuff))
        {
            return;
        }

        var ammoName = GetAmmoNameFromDamage(attacker, damageSource);
        if (string.IsNullOrEmpty(ammoName))
        {
            Log.Out(string.Format(
                "[3Scrubs][AmmoRefund] Proc present but no ammo source found. SourceItem='{0}' AttackingItem='{1}' CreatorId={2} OwnerId={3}",
                damageSource.ItemClass != null ? damageSource.ItemClass.Name : "",
                damageSource.AttackingItem.ItemClass != null ? damageSource.AttackingItem.ItemClass.Name : "",
                damageSource.CreatorEntityId,
                damageSource.ownerEntityId));
            attacker.Buffs.RemoveBuff(RefundProcBuff, true);
            return;
        }

        var itemValue = ItemClass.GetItem(ammoName, true);
        var itemStack = new ItemStack(itemValue, 1);
        attacker.inventory.AddItem(itemStack);
        attacker.Buffs.RemoveBuff(RefundProcBuff, true);
    }

    private static string GetAmmoNameFromDamage(EntityPlayer attacker, DamageSource damageSource)
    {
        // Best case: damage source explicitly reports an ammo item
        var sourceItemName = damageSource.ItemClass != null ? damageSource.ItemClass.Name : null;
        if (!string.IsNullOrEmpty(sourceItemName) && sourceItemName.StartsWith("ammo", StringComparison.OrdinalIgnoreCase))
        {
            return sourceItemName;
        }

        // Sometimes AttackingItem can be the ammo item (often it is the weapon)
        var attackingItemName = damageSource.AttackingItem.ItemClass != null ? damageSource.AttackingItem.ItemClass.Name : null;
        if (!string.IsNullOrEmpty(attackingItemName) && attackingItemName.StartsWith("ammo", StringComparison.OrdinalIgnoreCase))
        {
            return attackingItemName;
        }

        if (attacker == null || attacker.inventory == null)
        {
            return null;
        }

        // Fallback: use the attacker's currently-held gun and selected ammo index.
        var holdingItemValue = attacker.inventory.holdingItemItemValue;
        var holdingGun = attacker.inventory.GetHoldingGun();
        if (holdingGun == null)
        {
            return null;
        }

        var magazineItemNames = holdingGun.MagazineItemNames;
        if (magazineItemNames == null || magazineItemNames.Length == 0)
        {
            return null;
        }

        var idx = (int)holdingItemValue.SelectedAmmoTypeIndex;
        if (idx < 0 || idx >= magazineItemNames.Length)
        {
            return null;
        }

        return magazineItemNames[idx];
    }
}
