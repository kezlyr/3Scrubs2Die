using HarmonyLib;
using System.Reflection;
using System.Collections.Generic;
using System;
using System.Linq;

namespace ZZ_DiskStorage;

[HarmonyPatch]
internal static class Patches
{
	private const string DiskDriveContainerName = "diskDriveContainer";
	private const string DiskReaderContainerName = "diskReaderContainer";
	private const string DiskDropBoxContainerName = "diskDropBoxContainer";
	private const string DiskReaderSearchFieldId = "zzDiskSearchInput";
	private const string BindDiskReaderVisible = "zz_diskreader_visible";
	private const string BindDiskReaderWindowHeight = "zz_diskreader_window_height";
	private const string BindDiskStorageVisible = "zz_diskstorage_visible";
	private const string BindDiskStorageVanillaHeaderVisible = "zz_diskstorage_vanilla_header_visible";
	private const string BindDiskStorageTitle = "zz_diskstorage_title";
	private const bool DebugReaderUi = true;
	private const string PrevPageButtonId = "zzDiskPrevPage";
	private const string NextPageButtonId = "zzDiskNextPage";
	private const string CatAllButtonId = "zzDiskCatAll";
	private const string CatWeaponsButtonId = "zzDiskCatWeapons";
	private const string CatFoodButtonId = "zzDiskCatFood";
	private const string CatResourcesButtonId = "zzDiskCatResources";
	private const string CatArmorButtonId = "zzDiskCatArmor";
	private const string CatToolsButtonId = "zzDiskCatTools";
	private static readonly HashSet<string> PendingReaderUiRefresh = new();
	private static bool InQueuedReaderRefresh;

	private static bool TryGetBlockName(ITileEntityLootable te, out string blockName)
	{
		blockName = null;
		try
		{
			if (te is not ITileEntity tileEntity)
				return false;
			var world = GameManager.Instance?.World;
			if (world == null)
				return false;
			var pos = tileEntity.ToWorldPos();
			var block = world.GetBlock(pos).Block;
			blockName = block?.blockName;
			return !string.IsNullOrEmpty(blockName);
		}
		catch
		{
			blockName = null;
			return false;
		}
	}

	private static string TryLocalize(string keyOrName)
	{
		if (string.IsNullOrEmpty(keyOrName))
			return string.Empty;
		try
		{
			var localized = Localization.Get(keyOrName);
			if (!string.IsNullOrEmpty(localized))
				return localized;
		}
		catch
		{
			// best effort
		}
		return keyOrName;
	}

	private static string GetTeId(TileEntityLootContainer te)
	{
		var pos = te.ToWorldPos();
		return $"{te.GetClrIdx()}:{pos.x},{pos.y},{pos.z}";
	}

	private static ITileEntityLootable TryGetLootWindowTileEntity(XUiC_LootWindow lootWindow)
	{
		if (lootWindow == null)
			return null;

		var t = lootWindow.GetType();
		foreach (var name in new[] { "te", "_te", "tileEntity", "_tileEntity", "TileEntity" })
		{
			try
			{
				var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (f != null && typeof(ITileEntityLootable).IsAssignableFrom(f.FieldType))
				{
					var v = f.GetValue(lootWindow) as ITileEntityLootable;
					if (v != null)
						return v;
				}
			}
			catch { }

			try
			{
				var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (p != null && p.CanRead && typeof(ITileEntityLootable).IsAssignableFrom(p.PropertyType))
				{
					var v = p.GetValue(lootWindow, null) as ITileEntityLootable;
					if (v != null)
						return v;
				}
			}
			catch { }
		}

		// Last resort: scan any fields/properties assignable to ITileEntityLootable.
		try
		{
			var fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			for (var i = 0; i < fields.Length; i++)
			{
				var f = fields[i];
				if (f == null || !typeof(ITileEntityLootable).IsAssignableFrom(f.FieldType))
					continue;
				var v = f.GetValue(lootWindow) as ITileEntityLootable;
				if (v != null)
					return v;
			}

			var props = t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			for (var i = 0; i < props.Length; i++)
			{
				var p = props[i];
				if (p == null || !p.CanRead || !typeof(ITileEntityLootable).IsAssignableFrom(p.PropertyType))
					continue;
				var v = p.GetValue(lootWindow, null) as ITileEntityLootable;
				if (v != null)
					return v;
			}
		}
		catch { }

		return null;
	}

	private static XUiC_LootContainer TryGetLootWindowLootContainer(XUiC_LootWindow lootWindow)
	{
		if (lootWindow == null)
			return null;

		var t = lootWindow.GetType();
		foreach (var name in new[] { "lootContainer", "_lootContainer", "LootContainer", "lootContainerController" })
		{
			try
			{
				var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (f != null && typeof(XUiC_LootContainer).IsAssignableFrom(f.FieldType))
				{
					var v = f.GetValue(lootWindow) as XUiC_LootContainer;
					if (v != null)
						return v;
				}
			}
			catch { }

			try
			{
				var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (p != null && p.CanRead && typeof(XUiC_LootContainer).IsAssignableFrom(p.PropertyType))
				{
					var v = p.GetValue(lootWindow, null) as XUiC_LootContainer;
					if (v != null)
						return v;
				}
			}
			catch { }
		}

		// Last resort: scan any fields/properties assignable to XUiC_LootContainer.
		try
		{
			var fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			for (var i = 0; i < fields.Length; i++)
			{
				var f = fields[i];
				if (f == null || !typeof(XUiC_LootContainer).IsAssignableFrom(f.FieldType))
					continue;
				var v = f.GetValue(lootWindow) as XUiC_LootContainer;
				if (v != null)
					return v;
			}

			var props = t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			for (var i = 0; i < props.Length; i++)
			{
				var p = props[i];
				if (p == null || !p.CanRead || !typeof(XUiC_LootContainer).IsAssignableFrom(p.PropertyType))
					continue;
				var v = p.GetValue(lootWindow, null) as XUiC_LootContainer;
				if (v != null)
					return v;
			}
		}
		catch { }

		return null;
	}

	private static string TryGetViewComponentIdOrName(XUiController controller)
	{
		try
		{
			var vc = controller?.ViewComponent;
			var id = vc?.ID;
			if (!string.IsNullOrEmpty(id))
				return id;
			if (vc == null)
				return null;

			// Some builds expose a "name" field/property rather than ID.
			try
			{
				var p = AccessTools.Property(vc.GetType(), "Name") ?? AccessTools.Property(vc.GetType(), "name");
				if (p != null && p.PropertyType == typeof(string))
					id = p.GetValue(vc, null) as string;
				if (!string.IsNullOrEmpty(id))
					return id;
			}
			catch { }

			try
			{
				var f = AccessTools.Field(vc.GetType(), "Name") ?? AccessTools.Field(vc.GetType(), "name");
				if (f != null && f.FieldType == typeof(string))
					id = f.GetValue(vc) as string;
				if (!string.IsNullOrEmpty(id))
					return id;
			}
			catch { }
		}
		catch { }

		return null;
	}

	private static void RefreshLootUiGrid(XUiC_LootWindow lootWindow, TileEntityLootContainer teLoot)
	{
		try
		{
			if (lootWindow == null || teLoot == null)
				return;
			var pageItems = teLoot.GetItems();
			if (pageItems == null)
				return;
			var lootContainer = TryGetLootWindowLootContainer(lootWindow);
			if (lootContainer == null)
				return;

			lootContainer.SetStacks(pageItems);

			// Some builds require an explicit bindings refresh.
			try
			{
				var m = AccessTools.Method(lootContainer.GetType(), "RefreshBindings") ?? AccessTools.Method(lootContainer.GetType(), "Refresh");
				m?.Invoke(lootContainer, null);
			}
			catch { }

			try
			{
				var m2 = AccessTools.Method(lootWindow.GetType(), "RefreshBindings") ?? AccessTools.Method(lootWindow.GetType(), "Refresh");
				m2?.Invoke(lootWindow, null);
			}
			catch { }
		}
		catch
		{
			// best effort
		}
	}

	private static void TryInvokeByName(object target, params string[] methodNames)
	{
		if (target == null || methodNames == null || methodNames.Length == 0)
			return;
		var t = target.GetType();
		for (var i = 0; i < methodNames.Length; i++)
		{
			var name = methodNames[i];
			if (string.IsNullOrEmpty(name))
				continue;
			try
			{
				var m0 = t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
				m0?.Invoke(target, null);
			}
			catch { }
			try
			{
				var m1 = t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(bool) }, null);
				m1?.Invoke(target, new object[] { true });
			}
			catch { }
		}
	}

	private static void TryForceLootRedraw(XUiC_LootContainer lootContainer)
	{
		if (lootContainer == null)
			return;

		// Try common refresh/update methods; exact names vary across builds.
		TryInvokeByName(lootContainer,
			"RefreshBindings",
			"Refresh",
			"UpdateBindings",
			"UpdateSlots",
			"UpdateSlotControls",
			"UpdateItemStacks",
			"UpdateView",
			"Rebuild");

		try
		{
			var lootWindow = lootContainer.GetParentByType<XUiC_LootWindow>();
			TryInvokeByName(lootWindow, "RefreshBindings", "Refresh", "UpdateBindings");
		}
		catch { }

		try
		{
			var vc = lootContainer.ViewComponent;
			TryInvokeByName(vc, "SetDirty", "Invalidate", "Refresh", "Update");
		}
		catch { }
	}

	private static IEnumerable<XUiController> EnumerateControllerTree(XUiController root)
	{
		if (root == null)
			yield break;

		var stack = new Stack<XUiController>();
		stack.Push(root);
		var seen = new HashSet<XUiController>();
		while (stack.Count > 0)
		{
			var cur = stack.Pop();
			if (cur == null || seen.Contains(cur))
				continue;
			seen.Add(cur);
			yield return cur;

			try
			{
				// Try a few common ways XUi stores children.
				var t = cur.GetType();
				object childrenObj = null;
				foreach (var name in new[] { "Children", "children", "_children", "childControllers", "ChildControllers" })
				{
					if (childrenObj != null) break;
					try
					{
						var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						if (p != null && p.CanRead)
							childrenObj = p.GetValue(cur, null);
					}
					catch { }
					try
					{
						var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						if (f != null)
							childrenObj = f.GetValue(cur);
					}
					catch { }
				}

				if (childrenObj is IEnumerable<XUiController> ec)
				{
					foreach (var ch in ec)
						if (ch != null) stack.Push(ch);
				}
				else if (childrenObj is System.Collections.IEnumerable e)
				{
					foreach (var obj in e)
						if (obj is XUiController ch) stack.Push(ch);
				}
				else
				{
					// Try method-based access
					var m = t.GetMethod("GetChildren", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null)
						?? t.GetMethod("get_Children", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
					if (m != null)
					{
						var v = m.Invoke(cur, null);
						if (v is IEnumerable<XUiController> ec2)
						{
							foreach (var ch in ec2)
								if (ch != null) stack.Push(ch);
						}
						else if (v is System.Collections.IEnumerable e2)
						{
							foreach (var obj in e2)
								if (obj is XUiController ch) stack.Push(ch);
						}
					}
				}
			}
			catch { }
		}
	}

	private static void TryApplyStacksDirectToSlotControllers(XUiC_LootContainer lootContainer, ItemStack[] pageItems)
	{
		if (lootContainer == null || pageItems == null)
			return;

		try
		{
			var slotsByIdx = new Dictionary<int, XUiC_ItemStack>();
			foreach (var c in EnumerateControllerTree(lootContainer))
			{
				if (c is not XUiC_ItemStack slot)
					continue;
				try
				{
					if (slot.StackLocation != XUiC_ItemStack.StackLocationTypes.LootContainer)
						continue;
					if (!TryGetLootSlotIndex(slot, out var idx))
						continue;
					slotsByIdx[idx] = slot;
				}
				catch { }
			}

			var updated = 0;
			for (var i = 0; i < pageItems.Length; i++)
			{
				if (!slotsByIdx.TryGetValue(i, out var slot) || slot == null)
					continue;
				var s = pageItems[i] ?? ItemStack.Empty;
				if (TrySetUiSlotStack(slot, s))
				{
					updated++;
					TryInvokeByName(slot, "RefreshBindings", "Refresh", "UpdateBindings", "UpdateItemStack", "UpdateItem", "OnItemStackChanged");
					try
					{
						var vc = slot.ViewComponent;
						TryInvokeByName(vc, "SetDirty", "Invalidate", "Refresh", "Update");
					}
					catch { }
				}
			}

			if (DebugReaderUi)
				Log.Out($"[ZZ_DiskStorage][DBG] Reader UI slots updated: {updated}");
		}
		catch
		{
			// best effort
		}
	}

	[HarmonyPatch(typeof(XUiC_ContainerStandardControls), "OnOpen")]
	private class Patch_XUiC_ContainerStandardControls_OnOpen
	{
		private static void Postfix(XUiC_ContainerStandardControls __instance)
		{
			// Disable sorting for Disk Readers by hiding the button
			var xui = __instance.xui;
            bool isDiskReader = (xui != null && xui.lootContainer is TileEntityLootContainer te && DiskStorageLogic.IsDiskReaderTileEntity(te));
            
            var btnSort = __instance.GetChildById("btnSort");
            if (btnSort != null)
            {
                btnSort.ViewComponent.IsVisible = !isDiskReader;
            }
		}
	}

	[HarmonyPatch(typeof(XUiController), "Update")]
	private class Patch_XUiController_Update_ZZDiskReaderQueuedRefresh
	{
		private static void Postfix(XUiController __instance)
		{
			// In 2.5, many controllers rely on base XUiController.Update(float) and don't override.
			// We hook here and only act when the controller is the loot container grid.
			if (__instance is not XUiC_LootContainer lootContainer)
				return;
			var containerId = TryGetViewComponentIdOrName(lootContainer);
			if (string.IsNullOrEmpty(containerId) || !containerId.Equals("queue", StringComparison.OrdinalIgnoreCase))
				return;

			var didEnter = false;
			try
			{
				var xui = lootContainer?.xui;
				if (xui?.lootContainer is not TileEntityLootContainer teLoot)
					return;
				if (!DiskStorageLogic.IsDiskReaderTileEntity(teLoot))
					return;

				var key = GetTeId(teLoot);
				if (!PendingReaderUiRefresh.Contains(key))
					return;
				if (InQueuedReaderRefresh)
					return;

				PendingReaderUiRefresh.Remove(key);
				InQueuedReaderRefresh = true;
				didEnter = true;

				var pageItems = teLoot.GetItems();
				if (pageItems == null)
					return;

				lootContainer.SetStacks(pageItems);
				if (DebugReaderUi)
				{
					var first = ItemStack.Empty;
					try
					{
						for (var i = 0; i < pageItems.Length; i++)
						{
							var s = pageItems[i];
							if (s != null && !s.IsEmpty())
							{
								first = s;
								break;
							}
						}
					}
					catch { }
					Log.Out($"[ZZ_DiskStorage][DBG] Reader UI refresh applied (firstType={first.itemValue?.type} count={first.count})");
				}

				TryForceLootRedraw(lootContainer);
				TryApplyStacksDirectToSlotControllers(lootContainer, pageItems);
			}
			catch
			{
				// best effort
			}
			finally
			{
				if (didEnter)
					InQueuedReaderRefresh = false;
			}
		}
	}

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

	[HarmonyPatch(typeof(XUiC_LootWindow), nameof(XUiC_LootWindow.SetTileEntityChest))]
	[HarmonyPostfix]
	private static void XUiC_LootWindow_SetTileEntityChest_Postfix(XUiC_LootWindow __instance, ITileEntityLootable _te)
	{
		try
		{
			if (_te is not TileEntityLootContainer teLoot)
				return;
			if (!DiskStorageLogic.IsDiskReaderTileEntity(teLoot))
				return;
			var items = teLoot.GetItems();
			if (items == null)
				return;
			var lootContainerField = AccessTools.Field(__instance.GetType(), "lootContainer");
			var lootContainer = lootContainerField != null ? lootContainerField.GetValue(__instance) as XUiC_LootContainer : null;
			lootContainer?.SetStacks(items);
		}
		catch
		{
			// best effort
		}
	}

	[HarmonyPatch(typeof(XUiC_LootWindow), "GetBindingValueInternal")]
	[HarmonyPostfix]
	private static void XUiC_LootWindow_GetBindingValueInternal_Postfix(XUiC_LootWindow __instance, ref bool __result, ref string _value, string _bindingName, ITileEntityLootable ___te, string ___lootContainerName)
	{
		if (__result)
			return;
		if (string.IsNullOrEmpty(_bindingName))
			return;

		var isDiskReader = false;
		var isDiskDrive = false;
		var isDiskDropBox = false;
		try
		{
			// Prefer the container name (stable even if the block name changes).
			if (!string.IsNullOrEmpty(___lootContainerName) && string.Equals(___lootContainerName, DiskReaderContainerName, StringComparison.OrdinalIgnoreCase))
				isDiskReader = true;
			else if (!string.IsNullOrEmpty(___lootContainerName) && string.Equals(___lootContainerName, DiskDriveContainerName, StringComparison.OrdinalIgnoreCase))
				isDiskDrive = true;
			else if (!string.IsNullOrEmpty(___lootContainerName) && string.Equals(___lootContainerName, DiskDropBoxContainerName, StringComparison.OrdinalIgnoreCase))
				isDiskDropBox = true;
			else if (___te != null)
			{
				// Fall back to block detection (handles tier variants).
				if (DiskStorageLogic.IsDiskReaderTileEntity(___te))
					isDiskReader = true;
				else if (DiskStorageLogic.IsDiskDriveTileEntity(___te))
					isDiskDrive = true;
				else if (DiskStorageLogic.IsDiskDropBoxTileEntity(___te))
					isDiskDropBox = true;
			}
		}
		catch
		{
			isDiskReader = false;
			isDiskDrive = false;
			isDiskDropBox = false;
		}

		var isDiskStorage = isDiskReader || isDiskDrive || isDiskDropBox;

		if (string.Equals(_bindingName, BindDiskReaderVisible, StringComparison.OrdinalIgnoreCase))
		{
			_value = isDiskReader ? "true" : "false";
			__result = true;
			return;
		}

		if (string.Equals(_bindingName, BindDiskStorageVisible, StringComparison.OrdinalIgnoreCase))
		{
			_value = isDiskStorage ? "true" : "false";
			__result = true;
			return;
		}

		if (string.Equals(_bindingName, BindDiskStorageVanillaHeaderVisible, StringComparison.OrdinalIgnoreCase))
		{
			_value = isDiskStorage ? "false" : "true";
			__result = true;
			return;
		}

		if (string.Equals(_bindingName, BindDiskStorageTitle, StringComparison.OrdinalIgnoreCase))
		{
			if (!isDiskStorage)
			{
				_value = ___lootContainerName ?? string.Empty;
				__result = true;
				return;
			}

			// Prefer the actual block name so tiers show correctly.
			if (TryGetBlockName(___te, out var blockName))
				_value = TryLocalize(blockName);
			else
				_value = TryLocalize(___lootContainerName);

			__result = true;
			return;
		}

		if (string.Equals(_bindingName, BindDiskReaderWindowHeight, StringComparison.OrdinalIgnoreCase))
		{
			// Vanilla looting window is 378. Disk reader grid is taller (7 rows @ 75px-ish), so bump it.
			_value = isDiskReader ? "640" : "378";
			__result = true;
			return;
		}
	}

	[HarmonyPatch(typeof(XUiC_TextInput), nameof(XUiC_TextInput.OnChange))]
	[HarmonyPostfix]
	private static void XUiC_TextInput_OnChange_Postfix(XUiC_TextInput __instance)
	{
		try
		{
			var vc = __instance?.ViewComponent;
			var id = vc?.ID;
			if (string.IsNullOrEmpty(id) && vc != null)
			{
				try
				{
					var p = AccessTools.Property(vc.GetType(), "Name") ?? AccessTools.Property(vc.GetType(), "name");
					if (p != null && p.PropertyType == typeof(string))
						id = p.GetValue(vc, null) as string;
					if (string.IsNullOrEmpty(id))
					{
						var f = AccessTools.Field(vc.GetType(), "Name") ?? AccessTools.Field(vc.GetType(), "name");
						if (f != null && f.FieldType == typeof(string))
							id = f.GetValue(vc) as string;
					}
				}
				catch { }
			}
			if (!string.Equals(id, DiskReaderSearchFieldId, StringComparison.OrdinalIgnoreCase))
				return;

			var lootWindow = __instance.GetParentByType<XUiC_LootWindow>();
			if (lootWindow == null)
				return;

			// Private field access via reflection (keep it resilient across versions).
			var te = TryGetLootWindowTileEntity(lootWindow);
			if (te is not TileEntityLootContainer teLoot)
				return;
			if (!DiskStorageLogic.IsDiskReaderTileEntity(teLoot))
				return;

			if (DebugReaderUi)
				Log.Out($"[ZZ_DiskStorage][DBG] Reader search change: text='{__instance.Text ?? string.Empty}'");

			DiskStorageLogic.ReaderSetFilter(teLoot, __instance.Text);
			PendingReaderUiRefresh.Add(GetTeId(teLoot));
		}
		catch
		{
			// best effort
		}
	}


	[HarmonyPatch]
	private class Patch_XUiC_SimpleButton_OnPress_ZZDiskReaderPaging
	{
		private static MethodBase TargetMethod()
		{
			// Different 7DTD builds have used different method names for button presses.
			return AccessTools.Method(typeof(XUiC_SimpleButton), "OnPressed")
				?? AccessTools.Method(typeof(XUiC_SimpleButton), "OnPress");
		}

		private static void Postfix(XUiC_SimpleButton __instance)
		{
			try
			{
				var id = TryGetViewComponentIdOrName(__instance);
				if (string.IsNullOrEmpty(id))
					return;

				string cat = null;
				if (id.Equals(CatAllButtonId, StringComparison.OrdinalIgnoreCase)) cat = "All";
				else if (id.Equals(CatWeaponsButtonId, StringComparison.OrdinalIgnoreCase)) cat = "Weapons";
				else if (id.Equals(CatFoodButtonId, StringComparison.OrdinalIgnoreCase)) cat = "Food";
				else if (id.Equals(CatResourcesButtonId, StringComparison.OrdinalIgnoreCase)) cat = "Resources";
				else if (id.Equals(CatArmorButtonId, StringComparison.OrdinalIgnoreCase)) cat = "Armor";
				else if (id.Equals(CatToolsButtonId, StringComparison.OrdinalIgnoreCase)) cat = "Tools";

				var isPage = id.Equals(PrevPageButtonId, StringComparison.OrdinalIgnoreCase) || id.Equals(NextPageButtonId, StringComparison.OrdinalIgnoreCase);

				if (!isPage && cat == null)
					return;

				var lootWindow = __instance.GetParentByType<XUiC_LootWindow>();
				if (lootWindow == null)
					return;
				var te = TryGetLootWindowTileEntity(lootWindow);
				if (te is not TileEntityLootContainer teLoot)
					return;
				if (!DiskStorageLogic.IsDiskReaderTileEntity(teLoot))
					return;

				if (isPage)
				{
					if (DebugReaderUi)
						Log.Out($"[ZZ_DiskStorage][DBG] Reader page click: {(id.Equals(PrevPageButtonId, StringComparison.OrdinalIgnoreCase) ? "prev" : "next")}");

					DiskStorageLogic.ReaderChangePage(teLoot, id.Equals(PrevPageButtonId, StringComparison.OrdinalIgnoreCase) ? -1 : 1);
				}
				else
				{
					if (DebugReaderUi)
						Log.Out($"[ZZ_DiskStorage][DBG] Reader category click: {cat}");
					DiskStorageLogic.ReaderSetCategory(teLoot, cat);
				}
				PendingReaderUiRefresh.Add(GetTeId(teLoot));
			}
			catch
			{
				// best effort
			}
		}
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
						TrySetUiSlotStack(__instance, remaining);
						DiskStorageLogic.ReaderUpdateSlotFromUi(teLoot, idx, remaining);
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
							TrySetUiSlotStack(__instance, cur[idx]);
							DiskStorageLogic.ReaderUpdateSlotFromUi(teLoot, idx, cur[idx]);
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

			DiskStorageLogic.ReaderUpdateSlotFromUi(__instance, _idx, _item);
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

	[HarmonyPatch(typeof(XUiC_LootWindow), "OnOpen")]
	[HarmonyPostfix]
	private static void XUiC_LootWindow_OnOpen_Postfix(XUiC_LootWindow __instance)
	{
		if (__instance == null || __instance.ViewComponent == null) return;
		var content = __instance.GetChildById("content");
		if (content == null || content.ViewComponent == null) return;

		var te = TryGetLootWindowTileEntity(__instance);
		if (te != null && DiskStorageLogic.IsDiskReaderTileEntity(te as TileEntityLootContainer))
		{
			// Shift content down to make room for tabs (Standard Y is -49, Tabs take ~40px)
			content.ViewComponent.Position = new Vector2i(3, -89);
		}
		else
		{
			// Restore standard position
			content.ViewComponent.Position = new Vector2i(3, -49);
		}
	}
}
