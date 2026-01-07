using HarmonyLib;
using System.Reflection;
using System.Collections.Generic;

namespace ZZ_DiskStorage;

[HarmonyPatch]
internal static class Patches
{
	private sealed class PendingRemainder
	{
		public ItemValue ItemValue;
		public int Count;
	}

	private static readonly Dictionary<string, PendingRemainder> PendingRemainders = new();

	private static string PendingKey(TileEntityLootContainer te, int idx)
	{
		var pos = te.ToWorldPos();
		return $"{te.GetClrIdx()}:{pos.x},{pos.y},{pos.z}:{idx}";
	}
	private static bool TryGetLootSlotIndex(XUiC_ItemStack slot, out int idx)
	{
		idx = -1;
		if (slot == null)
			return false;

		var t = slot.GetType();
		foreach (var name in new[] { "SlotNumber", "slotNumber", "SlotIndex", "slotIndex", "slotIdx", "SlotIdx" })
		{
			var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (p != null && p.PropertyType == typeof(int))
			{
				try
				{
					idx = (int)p.GetValue(slot, null);
					if (idx >= 0)
						return true;
				}
				catch { }
			}

			var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (f != null && f.FieldType == typeof(int))
			{
				try
				{
					idx = (int)f.GetValue(slot);
					if (idx >= 0)
						return true;
				}
				catch { }
			}
		}

		return false;
	}

	private static bool TrySetDragStack(object dragAndDrop, ItemStack stack)
	{
		if (dragAndDrop == null)
			return false;
		var t = dragAndDrop.GetType();

		try
		{
			var p = t.GetProperty("CurrentStack", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (p != null && p.CanWrite)
			{
				p.SetValue(dragAndDrop, stack, null);
				return true;
			}
		}
		catch { }

		try
		{
			var m = t.GetMethod("SetCurrentStack", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (m != null)
			{
				m.Invoke(dragAndDrop, new object[] { stack });
				return true;
			}
		}
		catch { }

		return false;
	}

	private static bool TrySetUiSlotStack(XUiC_ItemStack slot, ItemStack stack)
	{
		if (slot == null)
			return false;
		var t = slot.GetType();

		try
		{
			var p = t.GetProperty("ItemStack", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (p != null && p.CanWrite)
			{
				p.SetValue(slot, stack, null);
				return true;
			}
		}
		catch { }

		try
		{
			var f = t.GetField("ItemStack", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (f != null)
			{
				f.SetValue(slot, stack);
				return true;
			}
		}
		catch { }

		foreach (var name in new[] { "itemStack", "_itemStack", "stack", "_stack" })
		{
			try
			{
				var f2 = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (f2 != null)
				{
					f2.SetValue(slot, stack);
					return true;
				}
			}
			catch { }
		}

		return false;
	}

	private static ItemStack CopyStackWithCount(ItemStack source, int count)
	{
		if (source == null || source.IsEmpty() || count <= 0)
			return ItemStack.Empty;
		var s = new ItemStack();
		s.itemValue = source.itemValue;
		s.count = count;
		return s;
	}

	private static bool ShouldRejectNonDiskDropIntoSlot(XUiC_ItemStack targetSlot, ItemStack stackToPlace)
	{
		if (targetSlot == null)
			return false;

		if (stackToPlace.IsEmpty() || DiskStorageLogic.IsDiskItem(stackToPlace))
			return false;

		if (targetSlot.StackLocation != XUiC_ItemStack.StackLocationTypes.LootContainer)
			return false;

		var xui = targetSlot.xui;
		var te = xui?.lootContainer;
		if (te is not TileEntityLootContainer teLoot)
			return false;

		// Disk drive: ALL slots are disk-only.
		if (DiskStorageLogic.IsDiskDriveTileEntity(teLoot))
			return true;

		return false;
	}

	[HarmonyPatch(typeof(XUiC_LootWindow), nameof(XUiC_LootWindow.SetTileEntityChest))]
	[HarmonyPrefix]
	private static void XUiC_LootWindow_SetTileEntityChest_Prefix(ITileEntityLootable _te)
	{
		if (!DiskStorageLogic.IsDiskReaderTileEntity(_te))
			return;

		if (_te is TileEntityLootContainer teLoot)
			DiskStorageLogic.ReaderOnOpen(teLoot);
	}

	[HarmonyPatch(typeof(XUiC_ItemStack), nameof(XUiC_ItemStack.SwapItem))]
	[HarmonyPrefix]
	private static bool XUiC_ItemStack_SwapItem_Prefix(XUiC_ItemStack __instance)
	{
		var xui = __instance.xui;
		var dnd = xui?.dragAndDrop;
		var dragged = dnd?.CurrentStack ?? ItemStack.Empty;

		// Reader behavior:
		// - view-only for deposits (no player->reader placing)
		// - when withdrawing from a consolidated mega-stack, split so player only gets one normal stack
		if (__instance.StackLocation == XUiC_ItemStack.StackLocationTypes.LootContainer && xui?.lootContainer is TileEntityLootContainer teLoot && DiskStorageLogic.IsDiskReaderTileEntity(teLoot))
		{
			var slotStack = __instance.ItemStack;

			// Block deposits from player into reader.
			if (!dragged.IsEmpty() && dnd != null && XUiC_ItemStack.IsStackLocationFromPlayer(dnd.PickUpType))
			{
				dnd.PlaceItemBackInInventory();
				return false;
			}

			// Split mega-stack on pickup.
			if ((dragged == null || dragged.IsEmpty()) && slotStack != null && !slotStack.IsEmpty())
			{
				var maxStack = slotStack.itemValue?.ItemClass?.Stacknumber != null ? slotStack.itemValue.ItemClass.Stacknumber.Value : 1;
				if (maxStack < 1) maxStack = 1;
				if (slotStack.count > maxStack)
				{
					if (!TryGetLootSlotIndex(__instance, out var idx))
					{
						Log.Out("[ZZ_DiskStorage][DBG] Reader pickup split: could not determine slot index; allowing default behavior");
						return true;
					}

					var take = CopyStackWithCount(slotStack, maxStack);
					var remaining = CopyStackWithCount(slotStack, slotStack.count - maxStack);

					if (!TrySetDragStack(dnd, take))
					{
						Log.Out("[ZZ_DiskStorage][DBG] Reader pickup split: could not set drag stack; allowing default behavior");
						return true;
					}

					var cur = teLoot.GetItems();
					if (cur != null && idx >= 0 && idx < cur.Length)
					{
						cur[idx] = remaining;
						teLoot.items = cur;
						teLoot.SetModified();
					}

					return false;
				}
			}
		}

		if (!ShouldRejectNonDiskDropIntoSlot(__instance, dragged))
			return true;

		// Reject the drop BEFORE inventory/backend changes occur.
		// If the item came from the player, place it back so it never "vanishes".
		if (dnd != null && XUiC_ItemStack.IsStackLocationFromPlayer(dnd.PickUpType))
			dnd.PlaceItemBackInInventory();

		return false;
	}

	[HarmonyPatch(typeof(XUiC_ItemStack), nameof(XUiC_ItemStack.HandleMoveToPreferredLocation))]
	[HarmonyPrefix]
	private static bool XUiC_ItemStack_HandleMoveToPreferredLocation_Prefix(XUiC_ItemStack __instance)
	{
		// This is the right-click/quick-transfer path.
		// Block player->reader deposits; block player->drive non-disk.
		var xui = __instance.xui;
		if (xui?.lootContainer is not TileEntityLootContainer teLoot)
			return true;

		// Reader is view-only: prevent moving player stacks into it.
		if (DiskStorageLogic.IsDiskReaderTileEntity(teLoot))
		{
			// If this stack isn't currently in the loot container, it's coming from the player.
			if (__instance.StackLocation != XUiC_ItemStack.StackLocationTypes.LootContainer)
				return false;

			// Shift-click / quick-move FROM reader TO player: cap to one max stack.
			var slotStack = __instance.ItemStack;
			if (slotStack != null && !slotStack.IsEmpty())
			{
				var maxStack = slotStack.itemValue?.ItemClass?.Stacknumber != null ? slotStack.itemValue.ItemClass.Stacknumber.Value : 1;
				if (maxStack < 1) maxStack = 1;
				if (slotStack.count > maxStack)
				{
					if (TryGetLootSlotIndex(__instance, out var idx))
					{
						// Store remainder to restore when the game clears the slot.
						PendingRemainders[PendingKey(teLoot, idx)] = new PendingRemainder
						{
							ItemValue = slotStack.itemValue,
							Count = slotStack.count - maxStack
						};

						// Clamp source slot to maxStack so the quick-move only transfers one stack.
						var cur = teLoot.GetItems();
						if (cur != null && idx >= 0 && idx < cur.Length)
						{
							cur[idx] = CopyStackWithCount(slotStack, maxStack);
							teLoot.items = cur;
							teLoot.SetModified();
							TrySetUiSlotStack(__instance, cur[idx]);
						}
					}
				}
			}

			return true;
		}

		if (!DiskStorageLogic.IsDiskDriveTileEntity(teLoot))
			return true;

		// If the item being moved is not a disk, refuse the move.
		var stack = __instance.ItemStack;
		if (stack.IsEmpty() || DiskStorageLogic.IsDiskItem(stack))
			return true;

		return false;
	}



	[HarmonyPatch(typeof(XUiC_LootWindow), nameof(XUiC_LootWindow.CloseContainer))]
	[HarmonyPrefix]
	private static void XUiC_LootWindow_CloseContainer_Prefix(ITileEntityLootable ___te)
	{
		if (DiskStorageLogic.IsDiskDropBoxTileEntity(___te))
		{
			if (___te is TileEntityLootContainer dropBoxTe)
				DiskStorageLogic.DepositDropBoxIntoAdjacentDrive(dropBoxTe);
			return;
		}

		if (!DiskStorageLogic.IsDiskReaderTileEntity(___te))
			return;

		if (___te is TileEntityLootContainer teLoot)
			DiskStorageLogic.ReaderOnClose(teLoot);
	}

	[HarmonyPatch(typeof(TileEntityLootContainer), nameof(TileEntityLootContainer.UpdateSlot))]
	[HarmonyPrefix]
	private static void TileEntityLootContainer_UpdateSlot_Prefix(TileEntityLootContainer __instance, int _idx, ref ItemStack _item)
	{
		// Disk drive: ONLY accept disk items.
		if (DiskStorageLogic.IsDiskDriveTileEntity(__instance))
		{
			if (!_item.IsEmpty() && !DiskStorageLogic.IsDiskItem(_item))
			{
				var cur = __instance.GetItems();
				_item = (cur != null && _idx >= 0 && _idx < cur.Length) ? cur[_idx] : ItemStack.Empty;
			}
			return;
		}

		// Reader is content-only; we don't enforce slot rules here.
		if (DiskStorageLogic.IsDiskReaderTileEntity(__instance))
		{
			// If a quick-move cleared a consolidated mega-stack slot, restore the stored remainder.
			if (_item.IsEmpty())
			{
				var key = PendingKey(__instance, _idx);
				if (PendingRemainders.TryGetValue(key, out var pending) && pending != null && pending.Count > 0)
				{
					_item = new ItemStack(pending.ItemValue, pending.Count);
					PendingRemainders.Remove(key);
				}
			}
			return;
		}

		return;
	}

	[HarmonyPatch(typeof(TileEntityLootContainer), nameof(TileEntityLootContainer.UpdateSlot))]
	[HarmonyPostfix]
	private static void TileEntityLootContainer_UpdateSlot_Postfix(TileEntityLootContainer __instance, int _idx)
	{
		// Reader view is applied on open and persisted on close.
		return;
	}
}
