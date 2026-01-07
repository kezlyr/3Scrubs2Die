using System;
using System.Collections.Generic;
using System.IO;

namespace ZZ_DiskStorage;

internal static class DiskStorageLogic
{
	private const bool DebugDeposit = true;
	private const bool DebugReader = true;
	private const string DiskPayloadKey = "zz_disk_storage_payload";
	private const string DiskLastWriteKey = "zz_disk_storage_lastwrite";
	private static readonly FastTags<TagGroup.Global> DiskTag = FastTags<TagGroup.Global>.GetTag("disk_storage");

	private sealed class ReaderSession
	{
		public List<ItemStack> AllStacks;
		public string Filter;
		public string Category;
		public int Page;
		public List<int> DisplayIndices;
		public int[] SlotToAllIndex;
		public Dictionary<ItemKey, int> SnapshotCounts;
	}

	private static readonly Dictionary<string, ReaderSession> ReaderSessionsByReaderId = new();

	private const int DiskBayCount = 4;
	
	private static int ReaderPageSize => ModConfig.Instance.ReaderVisibleRows * ModConfig.Instance.ReaderVisibleCols;

	private static bool IsBlockName(string actual, string baseName)
	{
		if (string.IsNullOrEmpty(actual) || string.IsNullOrEmpty(baseName))
			return false;
		if (string.Equals(actual, baseName, StringComparison.OrdinalIgnoreCase))
			return true;
		if (string.Equals(actual, baseName + "T1", StringComparison.OrdinalIgnoreCase))
			return true;
		if (string.Equals(actual, baseName + "T2", StringComparison.OrdinalIgnoreCase))
			return true;
		return false;
	}

	private static int GetDiskCapacity(ItemStack diskStack)
	{
		try
		{
			if (diskStack.IsEmpty() || diskStack.itemValue == null || diskStack.itemValue.IsEmpty())
				return ModConfig.Instance.DiskCapacityT0;

			var name = diskStack.itemValue.ItemClass?.Name;
			if (string.IsNullOrEmpty(name))
				return ModConfig.Instance.DiskCapacityT0;

			if (name.EndsWith("T2", StringComparison.OrdinalIgnoreCase))
				return ModConfig.Instance.DiskCapacityT2;
			if (name.EndsWith("T1", StringComparison.OrdinalIgnoreCase))
				return ModConfig.Instance.DiskCapacityT1;
			return ModConfig.Instance.DiskCapacityT0;
		}
		catch
		{
			return ModConfig.Instance.DiskCapacityT0;
		}
	}

	internal readonly struct ItemKey : IEquatable<ItemKey>
	{
		public readonly int Type;
		public readonly int Quality;
		public readonly int UseTimesBits;

		public ItemKey(int type, int quality, float useTimes)
		{
			Type = type;
			Quality = quality;
			UseTimesBits = FloatToIntBits(useTimes);
		}

		public ItemKey(ItemValue iv) : this(iv.type, iv.Quality, iv.UseTimes) { }

		public bool Equals(ItemKey other) => Type == other.Type && Quality == other.Quality && UseTimesBits == other.UseTimesBits;
		public override bool Equals(object obj) => obj is ItemKey other && Equals(other);
		public override int GetHashCode()
		{
			unchecked
			{
				var hash = 17;
				hash = (hash * 31) + Type;
				hash = (hash * 31) + Quality;
				hash = (hash * 31) + UseTimesBits;
				return hash;
			}
		}
	}

	private static int FloatToIntBits(float value)
	{
		// .NET Framework 4.7.2 doesn't have BitConverter.SingleToInt32Bits.
		return BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
	}

	private static void SetDiskMetadata(ref ItemStack diskStack, string key, string value)
	{
		// 7DTD uses ItemValue as a struct; calling SetMetadata on a copy won't persist.
		// Always copy-out, mutate, then assign back.
		var iv = diskStack.itemValue;
		iv.SetMetadata(key, value);
		diskStack.itemValue = iv;
	}

	private static ItemStack CopyStackWithCount(ItemStack source, int count)
	{
		// Important: treat ItemStack as a reference type (it is nullable in game code).
		// We must avoid reusing the same instance for both "remaining" and "payload".
		if (source == null || source.IsEmpty() || count <= 0)
			return ItemStack.Empty;

		var s = new ItemStack();
		s.itemValue = source.itemValue;
		s.count = count;
		return s;
	}

	private static string GetReaderId(TileEntityLootContainer te)
	{
		var pos = te.ToWorldPos();
		return $"{te.GetClrIdx()}:{pos.x},{pos.y},{pos.z}";
	}

	private static void EnsureReaderContainerFixedSize(TileEntityLootContainer te)
	{
		// Always keep the reader container fixed to the visible page size (10x7).
		// Pagination is handled by swapping which stacks we display into these 70 slots.
		TrySetContainerSizeSafe(te, new Vector2i(ModConfig.Instance.ReaderVisibleCols, ModConfig.Instance.ReaderVisibleRows));
	}

	private static bool TryGetItemKey(ItemStack stack, out ItemKey key)
	{
		key = default;
		if (stack == null || stack.IsEmpty())
			return false;
		var iv = stack.itemValue;
		if (iv == null || iv.IsEmpty())
			return false;
		key = new ItemKey(iv.type, iv.Quality, iv.UseTimes);
		return true;
	}

	private static Dictionary<ItemKey, int> CountByKey(ItemStack[] stacks)
	{
		var dict = new Dictionary<ItemKey, int>();
		if (stacks == null)
			return dict;

		for (var i = 0; i < stacks.Length; i++)
		{
			var s = stacks[i];
			if (!TryGetItemKey(s, out var key))
				continue;
			if (dict.TryGetValue(key, out var cur))
				dict[key] = cur + s.count;
			else
				dict[key] = s.count;
		}

		return dict;
	}

	private static bool TrySetContainerSizeSafe(TileEntityLootContainer te, Vector2i size)
	{
		try
		{
			te.SetContainerSize(size, false);
			return true;
		}
		catch (Exception ex)
		{
			Log.Error($"[ZZ_DiskStorage] SetContainerSize({size.x},{size.y}) failed: {ex}");
			return false;
		}
	}

	private static void SetReaderEmpty(TileEntityLootContainer te)
	{
		// Best effort: try true zero-slot UI; fall back to 1 locked empty slot.
		if (TrySetContainerSizeSafe(te, new Vector2i(0, 0)))
		{
			te.items = Array.Empty<ItemStack>();
		}
		else
		{
			TrySetContainerSizeSafe(te, new Vector2i(1, 1));
			te.items = ItemStack.CreateArray(1);
			te.items[0] = ItemStack.Empty;
		}
		te.SetModified();
	}

	private static void SetReaderInsertableEmpty(TileEntityLootContainer te)
	{
		// When disks are present but empty, show a minimal (empty) view.
		// Deposits happen via the drop box, not by placing into the reader.
		TrySetContainerSizeSafe(te, new Vector2i(1, 1));
		te.items = ItemStack.CreateArray(1);
		te.items[0] = ItemStack.Empty;
		te.SetModified();
	}

	private static bool TryGetAdjacentDriveDisks(TileEntityLootContainer readerTe, out TileEntityLootContainer driveTe, out ItemStack[] driveItems, out int scanLen)
	{
		driveTe = FindAdjacentDiskDrive(readerTe);
		driveItems = null;
		scanLen = 0;
		if (driveTe == null)
			return false;
		driveItems = driveTe.GetItems();
		if (driveItems == null)
			return false;
		scanLen = Math.Min(DiskBayCount, driveItems.Length);
		for (var i = 0; i < scanLen; i++)		
		{
			if (IsDiskItem(driveItems[i]))
				return true;
		}
		return false;
	}

	internal static List<TileEntityLootContainer> FindAdjacentDiskDriveNetwork(TileEntityLootContainer sourceTe)
	{
		var world = GameManager.Instance?.World;
		if (world == null || sourceTe == null)
			return new List<TileEntityLootContainer>();

		var srcPos = sourceTe.ToWorldPos();
		var clrIdx = sourceTe.GetClrIdx();
		return FindDiskDriveNetworkFromPos(world, clrIdx, srcPos);
	}

	private static List<TileEntityLootContainer> FindDiskDriveNetworkFromPos(World world, int clrIdx, Vector3i fromPos)
	{
		var result = new List<TileEntityLootContainer>();
		if (world == null)
			return result;

		var offsets = new[]
		{
			new Vector3i(1, 0, 0),
			new Vector3i(-1, 0, 0),
			new Vector3i(0, 0, 1),
			new Vector3i(0, 0, -1),
			new Vector3i(0, 1, 0),
			new Vector3i(0, -1, 0),
		};

		var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var queue = new Queue<Vector3i>();

		// Seed with any directly-adjacent diskDrive blocks.
		foreach (var off in offsets)
		{
			var pos = fromPos + off;
			var bv = world.GetBlock(pos);
			var block = bv.Block;
			if (block == null || !IsBlockName(block.blockName, "diskDrive"))
				continue;
			var key = $"{clrIdx}:{pos.x},{pos.y},{pos.z}";
			if (visited.Add(key))
				queue.Enqueue(pos);
		}

		while (queue.Count > 0)
		{
			var pos = queue.Dequeue();
			if (TryGetDiskDriveAtPos(world, clrIdx, pos, out var driveTe) && driveTe != null)
			{
				// Ensure we don't add the same TE twice (e.g. multi-block structures)
				if (!result.Contains(driveTe))
					result.Add(driveTe);
			}

			// Expand to adjacent disk drives.
			foreach (var off in offsets)
			{
				var next = pos + off;
				var bv = world.GetBlock(next);
				var block = bv.Block;
				if (block == null || !IsBlockName(block.blockName, "diskDrive"))
					continue;

				var key = $"{clrIdx}:{next.x},{next.y},{next.z}";
				if (visited.Add(key))
					queue.Enqueue(next);
			}
		}

		return result;
	}

	private static List<int> BuildDisplayIndices(List<ItemStack> allStacks, string filter, string category)
	{
		var indices = new List<int>();
		if (allStacks == null || allStacks.Count == 0)
			return indices;

		var term = (filter ?? string.Empty).Trim();
		var hasFilter = term.Length > 0;
		var hasCategory = !string.IsNullOrEmpty(category) && !category.Equals("All", StringComparison.OrdinalIgnoreCase);

		for (var i = 0; i < allStacks.Count; i++)
		{
			var s = allStacks[i];
			if (s == null || s.IsEmpty() || s.itemValue == null || s.itemValue.IsEmpty() || s.count <= 0)
				continue;

			var ic = ItemClass.GetForId(s.itemValue.type);
			if (ic == null) continue;

			if (hasCategory)
			{
				var match = false;
				var groups = ic.Groups;
				if (groups != null)
				{
					foreach (var g in groups)
					{
						if (category.Equals("Weapons", StringComparison.OrdinalIgnoreCase))
						{
							if (g == "Ammo/Weapons" || g == "Weapons" || g == "Ammo" || g == "Ranged Weapons" || g == "Melee Weapons") { match = true; break; }
						}
						else if (category.Equals("Food", StringComparison.OrdinalIgnoreCase))
						{
							if (g == "Food/Cooking" || g == "Medical" || g == "Chemicals") { match = true; break; }
						}
						else if (category.Equals("Resources", StringComparison.OrdinalIgnoreCase))
						{
							if (g == "Resources") { match = true; break; }
						}
						else if (category.Equals("Armor", StringComparison.OrdinalIgnoreCase))
						{
							if (g == "Armor" || g == "Clothing") { match = true; break; }
						}
						else if (category.Equals("Tools", StringComparison.OrdinalIgnoreCase))
						{
							if (g == "Tools/Traps" || g == "Tools") { match = true; break; }
						}
					}
				}
				if (!match) continue;
			}

			if (!hasFilter)
			{
				indices.Add(i);
				continue;
			}

			var name = ic.GetLocalizedItemName() ?? string.Empty;
			if (name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
				indices.Add(i);
		}

		return indices;
	}

	private static void RenderReaderPage(TileEntityLootContainer readerTe, ReaderSession session)
	{
		if (readerTe == null || session == null)
			return;

		EnsureReaderContainerFixedSize(readerTe);
		if (session.DisplayIndices == null)
			session.DisplayIndices = BuildDisplayIndices(session.AllStacks, session.Filter, session.Category);

		var displayCount = session.DisplayIndices.Count;
		var maxPage = displayCount <= 0 ? 0 : (int)Math.Ceiling(displayCount / (double)ReaderPageSize) - 1;
		if (maxPage < 0) maxPage = 0;
		if (session.Page < 0) session.Page = 0;
		if (session.Page > maxPage) session.Page = maxPage;

		if (session.SlotToAllIndex == null || session.SlotToAllIndex.Length != ReaderPageSize)
			session.SlotToAllIndex = new int[ReaderPageSize];
		for (var i = 0; i < session.SlotToAllIndex.Length; i++)
			session.SlotToAllIndex[i] = -1;

		var pageStart = session.Page * ReaderPageSize;

		// IMPORTANT (2.5+): some looting UI code caches the original items[] reference.
		// If we replace readerTe.items with a new array, the UI can keep showing the old page.
		// So: allocate once (if needed) and then mutate in-place.
		var items = readerTe.items;
		if (items == null || items.Length != ReaderPageSize)
		{
			items = ItemStack.CreateArray(ReaderPageSize);
			readerTe.items = items;
		}
		for (var slot = 0; slot < ReaderPageSize; slot++)
		{
			var di = pageStart + slot;
			if (di >= 0 && di < displayCount)
			{
				var allIdx = session.DisplayIndices[di];
				session.SlotToAllIndex[slot] = allIdx;
				var s = session.AllStacks[allIdx];
				items[slot] = (s == null || s.IsEmpty()) ? ItemStack.Empty : CopyStackWithCount(s, s.count);
			}
			else
			{
				items[slot] = ItemStack.Empty;
			}
		}

		// IMPORTANT: do not call SetModified() here.
		// Notifying listeners while looting UI is active can cause XUiC_LootContainer.OnTileEntityChanged
		// to run during teardown/input and throw IndexOutOfRange. We refresh the UI explicitly.
		// Do not SetModified() here (see comment above).
	}

	private static long GetDiskLastWriteTicks(ItemStack diskStack)
	{
		try
		{
			if (diskStack.IsEmpty() || diskStack.itemValue == null)
				return 0;
			if (!diskStack.itemValue.TryGetMetadata(DiskLastWriteKey, out string ticksStr) || string.IsNullOrWhiteSpace(ticksStr))
				return 0;
			return long.TryParse(ticksStr, out var ticks) ? ticks : 0;
		}
		catch
		{
			return 0;
		}
	}

	private static void StampDiskLastWrite(ref ItemStack diskStack)
	{
		try
		{
			if (!IsDiskItem(diskStack))
				return;
			SetDiskMetadata(ref diskStack, DiskLastWriteKey, DateTime.UtcNow.Ticks.ToString());
		}
		catch
		{
			// best-effort
		}
	}

	private static bool TryGetAdjacentDriveActiveDisk(TileEntityLootContainer readerTe, out TileEntityLootContainer driveTe, out int diskBayIndex, out ItemStack diskStack)
	{
		driveTe = FindAdjacentDiskDrive(readerTe);
		diskBayIndex = -1;
		diskStack = ItemStack.Empty;
		if (driveTe == null)
			return false;

		var driveItems = driveTe.GetItems();
		if (driveItems == null)
			return false;

		var scanLen = Math.Min(DiskBayCount, driveItems.Length);
		var bestIdx = -1;
		var bestTicks = -1L;
		ItemStack bestDisk = ItemStack.Empty;
		for (var i = 0; i < scanLen; i++)
		{
			var candidate = driveItems[i];
			if (!IsDiskItem(candidate))
				continue;

			// Prefer the disk most recently written to.
			var ticks = GetDiskLastWriteTicks(candidate);
			if (bestIdx < 0 || ticks > bestTicks)
			{
				bestIdx = i;
				bestTicks = ticks;
				bestDisk = candidate;
			}
		}

		if (bestIdx >= 0)
		{
			diskBayIndex = bestIdx;
			diskStack = bestDisk;
			return true;
		}

		return false;
	}

	internal static bool TryGetDiskDriveAtPos(World world, int clrIdx, Vector3i pos, out TileEntityLootContainer driveTe)
	{
		driveTe = null;
		var te = TryGetTileEntity(world, clrIdx, pos);
		if (te is TileEntityLootContainer lootTe && IsDiskDriveTileEntity(lootTe))
		{
			driveTe = lootTe;
			return true;
		}
		return false;
	}

	internal static bool TryFindAdjacentDiskDrive(World world, int clrIdx, Vector3i fromPos, out TileEntityLootContainer driveTe)
	{
		driveTe = null;
		var offsets = new[]
		{
			new Vector3i(1, 0, 0),
			new Vector3i(-1, 0, 0),
			new Vector3i(0, 0, 1),
			new Vector3i(0, 0, -1),
			new Vector3i(0, 1, 0),
			new Vector3i(0, -1, 0),
		};

		foreach (var off in offsets)
		{
			var pos = fromPos + off;
			var bv = world.GetBlock(pos);
			var block = bv.Block;
			if (block == null || !IsBlockName(block.blockName, "diskDrive"))
				continue;

			if (TryGetDiskDriveAtPos(world, clrIdx, pos, out driveTe))
				return true;
		}

		return false;
	}

	internal static bool TryGetDiskTargetDriveFromHit(WorldRayHitInfo hitInfo, out TileEntityLootContainer driveTe)
	{
		driveTe = null;
		var world = GameManager.Instance?.World;
		if (world == null || hitInfo == null || !hitInfo.bHitValid)
			return false;

		var pos = hitInfo.hit.blockPos;
		var clrIdx = hitInfo.hit.clrIdx;
		var bv = world.GetBlock(pos);
		var block = bv.Block;
		if (block == null)
			return false;

		if (IsBlockName(block.blockName, "diskDrive"))
			return TryGetDiskDriveAtPos(world, clrIdx, pos, out driveTe);

		if (IsBlockName(block.blockName, "diskReader"))
			return TryFindAdjacentDiskDrive(world, clrIdx, pos, out driveTe);

		return false;
	}

	internal static bool TryLoadDiskPayload(ItemStack diskStack, out ItemStack[] payloadSlots)
	{
		var cap = GetDiskCapacity(diskStack);
		payloadSlots = ItemStack.CreateArray(cap);
		if (!IsDiskItem(diskStack))
			return false;

		if (!diskStack.itemValue.TryGetMetadata(DiskPayloadKey, out string payload) || string.IsNullOrWhiteSpace(payload))
		{
			if (DebugDeposit)
				Log.Out($"[ZZ_DiskStorage][DBG] TryLoadDiskPayload: no payload metadata on disk (type={diskStack.itemValue?.type}, q={diskStack.itemValue?.Quality}). Treating as empty.");
			return true;
		}

		payloadSlots = DeserializeItemStacks(payload, cap);
		return true;
	}

	internal static void SaveDiskPayload(ref ItemStack diskStack, ItemStack[] payloadSlots)
	{
		if (!IsDiskItem(diskStack))
			return;
		var cap = GetDiskCapacity(diskStack);
		var normalized = NormalizeStacks(payloadSlots, cap);
		var fixedLen = ItemStack.CreateArray(cap);
		var copyLen = Math.Min(fixedLen.Length, normalized.Length);
		for (var i = 0; i < copyLen; i++)
			fixedLen[i] = normalized[i];
		var payload = SerializeItemStacks(fixedLen);
		SetDiskMetadata(ref diskStack, DiskPayloadKey, payload);
		if (DebugDeposit)
		{
			var ok = diskStack.itemValue.TryGetMetadata(DiskPayloadKey, out string check) && !string.IsNullOrWhiteSpace(check);
			Log.Out($"[ZZ_DiskStorage][DBG] SaveDiskPayload: wrote payload len={payload?.Length ?? 0} readbackOk={ok} readbackLen={(ok ? check.Length : 0)} (disk type={diskStack.itemValue?.type}, q={diskStack.itemValue?.Quality}).");
		}
		StampDiskLastWrite(ref diskStack);
	}

	internal static bool TryDepositStackIntoDrive(TileEntityLootContainer driveTe, ref ItemStack incoming)
	{
		if (driveTe == null || incoming.IsEmpty())
			return false;

		if (DebugDeposit)
		{
			var iv = incoming.itemValue;
			Log.Out($"[ZZ_DiskStorage][DBG] TryDepositStackIntoDrive: incoming type={iv?.type} q={iv?.Quality} count={incoming.count}");
		}

		var driveItems = driveTe.GetItems();
		if (driveItems == null)
			return false;

		var changedAny = false;
		var remaining = incoming;
		var scanLen = Math.Min(DiskBayCount, driveItems.Length);
		for (var bay = 0; bay < scanLen && !remaining.IsEmpty(); bay++)
		{
			var disk = driveItems[bay];
			if (!IsDiskItem(disk))
				continue;

			if (DebugDeposit)
				Log.Out($"[ZZ_DiskStorage][DBG]  bay {bay}: disk ok type={disk.itemValue?.type} lastWrite={GetDiskLastWriteTicks(disk)}");

			if (!TryLoadDiskPayload(disk, out var payload))
				continue;

			var changed = false;
			var before = remaining.count;
			remaining = DepositIntoPayload(payload, remaining, out changed);
			if (DebugDeposit)
			{
				var after = remaining.IsEmpty() ? 0 : remaining.count;
				Log.Out($"[ZZ_DiskStorage][DBG]   deposit into bay {bay}: moved={before - after} remaining={after} changed={changed}");
			}
			if (changed)
			{
				SaveDiskPayload(ref disk, payload);
				driveItems[bay] = disk;
				changedAny = true;
			}
		}

		if (changedAny)
		{
			driveTe.items = driveItems;
			driveTe.SetModified();
		}

		incoming = remaining;
		return changedAny;
	}

	internal static int DepositInventoryIntoDrive(EntityPlayerLocal player, TileEntityLootContainer driveTe, int excludeSlotIdx)
	{
		if (player == null || driveTe == null)
			return 0;

		var inv = player.inventory;
		if (inv == null)
			return 0;

		var movedTotal = 0;
		var slotCount = inv.GetSlotCount();
		var startIdx = 0;
		var endExclusive = slotCount;
		try
		{
			// Prefer backpack only (avoid toolbelt/equipment surprises).
			startIdx = inv.SHIFT_KEY_SLOT_OFFSET;
			endExclusive = inv.PUBLIC_SLOTS;
			if (startIdx < 0) startIdx = 0;
			if (endExclusive <= 0 || endExclusive > slotCount) endExclusive = slotCount;
			if (startIdx > endExclusive) startIdx = 0;
		}
		catch
		{
			startIdx = 0;
			endExclusive = slotCount;
		}

		for (var i = startIdx; i < endExclusive; i++)
		{
			if (i == excludeSlotIdx)
				continue;

			var stack = inv.GetItemStack(i);
			if (stack.IsEmpty())
				continue;
			if (IsDiskItem(stack))
				continue;

			var before = stack.count;
			TryDepositStackIntoDrive(driveTe, ref stack);
			if (stack.IsEmpty())
			{
				inv.SetItem(i, ItemStack.Empty);
				movedTotal += before;
			}
			else if (stack.count != before)
			{
				inv.SetItem(i, stack);
				movedTotal += (before - stack.count);
			}
		}

		if (movedTotal > 0)
			inv.Changed();

		return movedTotal;
	}

	internal static bool IsDiskReaderTileEntity(ITileEntityLootable te)
	{
		if (te is not ITileEntity tileEntity)
			return false;

		var world = GameManager.Instance?.World;
		if (world == null)
			return false;

		var pos = tileEntity.ToWorldPos();
		var bv = world.GetBlock(pos);
		var block = bv.Block;
		if (block == null)
			return false;

		return IsBlockName(block.blockName, "diskReader");
	}

	internal static bool IsDiskDriveTileEntity(ITileEntityLootable te)
	{
		if (te is not ITileEntity tileEntity)
			return false;

		var world = GameManager.Instance?.World;
		if (world == null)
			return false;

		var pos = tileEntity.ToWorldPos();
		var bv = world.GetBlock(pos);
		var block = bv.Block;
		if (block == null)
			return false;

		return IsBlockName(block.blockName, "diskDrive");
	}

	internal static bool IsDiskDropBoxTileEntity(ITileEntityLootable te)
	{
		if (te is not ITileEntity tileEntity)
			return false;

		var world = GameManager.Instance?.World;
		if (world == null)
			return false;

		var pos = tileEntity.ToWorldPos();
		var bv = world.GetBlock(pos);
		var block = bv.Block;
		if (block == null)
			return false;

		return IsBlockName(block.blockName, "diskDropBox");
	}

	internal static int DepositDropBoxIntoAdjacentDrive(TileEntityLootContainer dropBoxTe)
	{
		try
		{
			if (dropBoxTe == null)
				return 0;

			var world = GameManager.Instance?.World;
			if (world == null)
				return 0;

			var pos = dropBoxTe.ToWorldPos();
			var clrIdx = dropBoxTe.GetClrIdx();
			var drives = FindDiskDriveNetworkFromPos(world, clrIdx, pos);
			if (drives.Count == 0)
			{
				if (DebugDeposit)
					Log.Out($"[ZZ_DiskStorage][DBG] DropBox close: NO adjacent diskDrive found at {pos.x},{pos.y},{pos.z} clr={clrIdx}");
				return 0;
			}
			if (DebugDeposit)
			{
				Log.Out($"[ZZ_DiskStorage][DBG] DropBox close: found diskDrive network size={drives.Count} near {pos.x},{pos.y},{pos.z} clr={clrIdx}");
			}

			var items = dropBoxTe.GetItems();
			if (items == null || items.Length == 0)
				return 0;

			var movedTotal = 0;
			var changedAny = false;
			for (var i = 0; i < items.Length; i++)
			{
				var stack = items[i];
				if (stack.IsEmpty())
					continue;
				if (IsDiskItem(stack))
					continue;

				if (DebugDeposit)
					Log.Out($"[ZZ_DiskStorage][DBG]  DropBox slot {i}: start count={stack.count} type={stack.itemValue?.type} q={stack.itemValue?.Quality}");

				var before = stack.count;
				for (var d = 0; d < drives.Count && !stack.IsEmpty(); d++)
					TryDepositStackIntoDrive(drives[d], ref stack);
				if (DebugDeposit)
					Log.Out($"[ZZ_DiskStorage][DBG]  DropBox slot {i}: after deposit remaining={(stack.IsEmpty() ? 0 : stack.count)} (moved={before - (stack.IsEmpty() ? 0 : stack.count)})");
				if (stack.IsEmpty())
				{
					items[i] = ItemStack.Empty;
					changedAny = true;
					movedTotal += before;
				}
				else if (stack.count != before)
				{
					items[i] = stack;
					changedAny = true;
					movedTotal += (before - stack.count);
				}
			}

			if (changedAny)
			{
				dropBoxTe.items = items;
				dropBoxTe.SetModified();
			}

			return movedTotal;
		}
		catch (Exception ex)
		{
			Log.Error($"[ZZ_DiskStorage] DepositDropBoxIntoAdjacentDrive failed: {ex}");
			return 0;
		}
	}

	internal static bool IsDiskItem(ItemStack stack)
	{
		if (stack.IsEmpty())
			return false;

		var itemValue = stack.itemValue;
		if (itemValue == null || itemValue.IsEmpty())
			return false;

		var itemClass = itemValue.ItemClass;
		if (itemClass == null)
			return false;

		return itemClass.ItemTags.Test_AnySet(DiskTag);
	}

	internal static void ReaderOnOpen(TileEntityLootContainer readerTe)
	{
		try
		{
			if (DebugReader)
			{
				var rpos = readerTe.ToWorldPos();
				Log.Out($"[ZZ_DiskStorage][DBG] ReaderOnOpen: reader at {rpos.x},{rpos.y},{rpos.z} clr={readerTe.GetClrIdx()}");
			}

			var allStacks = LoadAllDisksAggregated(readerTe);
			var id = GetReaderId(readerTe);
			var session = new ReaderSession
			{
				AllStacks = allStacks,
				Filter = string.Empty,
				Category = null,
				Page = 0,
			};
			session.DisplayIndices = BuildDisplayIndices(session.AllStacks, session.Filter, session.Category);
			session.SnapshotCounts = CountByKey(session.AllStacks?.ToArray() ?? Array.Empty<ItemStack>());
			ReaderSessionsByReaderId[id] = session;
			RenderReaderPage(readerTe, session);
		}
		catch (Exception ex)
		{
			Log.Error($"[ZZ_DiskStorage] ReaderOnOpen failed: {ex}");
		}
	}

	internal static void ReaderOnClose(TileEntityLootContainer readerTe)
	{
		try
		{
			var id = GetReaderId(readerTe);
			if (!ReaderSessionsByReaderId.TryGetValue(id, out var session) || session == null)
				return;
			ReaderSessionsByReaderId.Remove(id);

			var snapshotCounts = session.SnapshotCounts ?? new Dictionary<ItemKey, int>();
			var currentCounts = CountByKey(session.AllStacks?.ToArray() ?? Array.Empty<ItemStack>());
			ApplyWithdrawalsToAdjacentDrive(readerTe, snapshotCounts, currentCounts);
		}
		catch (Exception ex)
		{
			Log.Error($"[ZZ_DiskStorage] ReaderOnClose failed: {ex}");
		}
	}

	internal static void ReaderSetFilter(TileEntityLootContainer readerTe, string filter)
	{
		try
		{
			var id = GetReaderId(readerTe);
			if (!ReaderSessionsByReaderId.TryGetValue(id, out var session) || session == null)
				return;
			session.Filter = filter ?? string.Empty;
			session.Page = 0;
			session.DisplayIndices = BuildDisplayIndices(session.AllStacks, session.Filter, session.Category);
			RenderReaderPage(readerTe, session);
		}
		catch (Exception ex)
		{
			Log.Error($"[ZZ_DiskStorage] ReaderSetFilter failed: {ex}");
		}
	}

	internal static void ReaderSetCategory(TileEntityLootContainer readerTe, string category)
	{
		try
		{
			var id = GetReaderId(readerTe);
			if (!ReaderSessionsByReaderId.TryGetValue(id, out var session) || session == null)
				return;
			session.Category = category;
			session.Page = 0;
			session.DisplayIndices = BuildDisplayIndices(session.AllStacks, session.Filter, session.Category);
			RenderReaderPage(readerTe, session);
		}
		catch (Exception ex)
		{
			Log.Error($"[ZZ_DiskStorage] ReaderSetCategory failed: {ex}");
		}
	}

	internal static void ReaderChangePage(TileEntityLootContainer readerTe, int delta)
	{
		try
		{
			var id = GetReaderId(readerTe);
			if (!ReaderSessionsByReaderId.TryGetValue(id, out var session) || session == null)
				return;
			session.Page += delta;
			RenderReaderPage(readerTe, session);
		}
		catch (Exception ex)
		{
			Log.Error($"[ZZ_DiskStorage] ReaderChangePage failed: {ex}");
		}
	}

	internal static void ReaderUpdateSlotFromUi(TileEntityLootContainer readerTe, int slotIdx, ItemStack newStack)
	{
		try
		{
			var id = GetReaderId(readerTe);
			if (!ReaderSessionsByReaderId.TryGetValue(id, out var session) || session == null)
				return;
			if (session.SlotToAllIndex == null || slotIdx < 0 || slotIdx >= session.SlotToAllIndex.Length)
				return;
			var allIdx = session.SlotToAllIndex[slotIdx];
			if (allIdx < 0 || session.AllStacks == null || allIdx >= session.AllStacks.Count)
				return;
			session.AllStacks[allIdx] = (newStack == null || newStack.IsEmpty() || newStack.count <= 0) ? ItemStack.Empty : CopyStackWithCount(newStack, newStack.count);
			// Rebuild display list if this became empty.
			if (session.AllStacks[allIdx].IsEmpty())
				session.DisplayIndices = BuildDisplayIndices(session.AllStacks, session.Filter, session.Category);
		}
		catch
		{
			// best effort
		}
	}

	internal static ItemStack[] ReaderBuildSearchReorderedStacks(ItemStack[] items, string searchText)
	{
		try
		{
			if (items == null || items.Length == 0)
				return items;

			var term = (searchText ?? string.Empty).Trim();
			if (term.Length == 0)
				return items;

			var matches = new List<ItemStack>(items.Length);
			var nonMatches = new List<ItemStack>(items.Length);

			for (var i = 0; i < items.Length; i++)
			{
				var s = items[i];
				if (s == null || s.IsEmpty() || s.itemValue == null || s.itemValue.IsEmpty())
				{
					nonMatches.Add(s);
					continue;
				}

				var ic = ItemClass.GetForId(s.itemValue.type);
				var name = ic != null ? (ic.GetLocalizedItemName() ?? string.Empty) : string.Empty;
				if (name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
					matches.Add(s);
				else
					nonMatches.Add(s);
			}

			// Stable partition: matches first, then everything else. This avoids breaking withdrawal accounting.
			var reordered = ItemStack.CreateArray(items.Length);
			var w = 0;
			for (var i = 0; i < matches.Count; i++)
				reordered[w++] = matches[i];
			for (var i = 0; i < nonMatches.Count; i++)
				reordered[w++] = nonMatches[i];

			return reordered;
		}
		catch (Exception ex)
		{
			Log.Error($"[ZZ_DiskStorage] ReaderBuildSearchReorderedStacks failed: {ex}");
			return items;
		}
	}

	internal static ItemStack[] ReaderApplySearchReorderNoNotify(TileEntityLootContainer readerTe, string searchText)
	{
		// Kept for compatibility with older code paths; now handled via ReaderSetFilter().
		ReaderSetFilter(readerTe, searchText);
		return readerTe?.GetItems();
	}

	private static List<ItemStack> LoadAllDisksAggregated(TileEntityLootContainer readerTe)
	{
		var output = new List<ItemStack>();
		// Reader shows ALL items on ALL disks in the adjacent DRIVE NETWORK (aggregated).
		var drives = FindAdjacentDiskDriveNetwork(readerTe);
		if (drives.Count == 0)
		{
			if (DebugReader)
				Log.Out("[ZZ_DiskStorage][DBG] LoadAllDisksIntoReader: no adjacent drive network found; setting reader empty");
			return output;
		}

		var foundAnyDisk = false;
		if (DebugReader)
			Log.Out($"[ZZ_DiskStorage][DBG] LoadAllDisksIntoReader: scanning drive network size={drives.Count}");

		var mergedQ0 = new Dictionary<(int type, int useBits), (ItemStack sample, int totalCount)>();
		var others = new List<ItemStack>();

		for (var d = 0; d < drives.Count; d++)
		{
			var driveTe = drives[d];
			var driveItems = driveTe?.GetItems();
			if (driveItems == null)
				continue;

			var scanLen = Math.Min(DiskBayCount, driveItems.Length);
			for (var bay = 0; bay < scanLen; bay++)
			{
				var disk = driveItems[bay];
				if (!IsDiskItem(disk))
					continue;
				foundAnyDisk = true;

				if (!TryLoadDiskPayload(disk, out var payload) || payload == null)
					continue;

				for (var slot = 0; slot < payload.Length; slot++)
				{
					var s = payload[slot];
					if (s == null || s.IsEmpty())
						continue;
					var iv = s.itemValue;
					if (iv == null || iv.IsEmpty())
						continue;

					if (iv.Quality == 0)
					{
						var key = (iv.type, FloatToIntBits(iv.UseTimes));
						if (mergedQ0.TryGetValue(key, out var cur))
							mergedQ0[key] = (cur.sample, cur.totalCount + s.count);
						else
							mergedQ0[key] = (s, s.count);
						continue;
					}

					others.Add(s);
				}
			}
		}

		if (!foundAnyDisk)
		{
			if (DebugReader)
				Log.Out("[ZZ_DiskStorage][DBG] LoadAllDisksIntoReader: drive network found but no disks present; setting reader empty");
			return output;
		}

		var result = new List<ItemStack>();
		foreach (var kvp in mergedQ0)
		{
			var sample = kvp.Value.sample;
			var total = kvp.Value.totalCount;
			if (sample == null || sample.IsEmpty() || total <= 0)
				continue;

			// Show ONE consolidated stack (can be > maxStack). We handle safe withdrawals via reader UpdateSlot patch.
			result.Add(CopyStackWithCount(sample, total));
		}

		// Quality > 0 stacks are never merged.
		for (var i = 0; i < others.Count; i++)
		{
			var s = others[i];
			if (s == null || s.IsEmpty())
				continue;
			result.Add(CopyStackWithCount(s, s.count));
		}

		if (result.Count <= 0)
		{
			// Disks exist, but there are no items on them.
			if (DebugReader)
				Log.Out("[ZZ_DiskStorage][DBG] LoadAllDisksIntoReader: disks found but no items; showing empty view");
			return output;
		}

		if (DebugReader)
			Log.Out($"[ZZ_DiskStorage][DBG] LoadAllDisksIntoReader: displaying {result.Count} stacks");
		output.AddRange(result);
		return output;
	}

	internal static void WithdrawFromDrives(TileEntityLootContainer readerTe, ItemKey key, int count)
	{
		if (count <= 0) return;

		var drives = FindAdjacentDiskDriveNetwork(readerTe);
		if (drives.Count == 0) return;

		var changedDriveTes = new HashSet<TileEntityLootContainer>();
		var toRemove = count;

		foreach (var driveTe in drives)
		{
			if (toRemove <= 0) break;
			var driveItems = driveTe.GetItems();
			if (driveItems == null) continue;

			var scanLen = Math.Min(DiskBayCount, driveItems.Length);
			for (var bay = 0; bay < scanLen && toRemove > 0; bay++)
			{
				var disk = driveItems[bay];
				if (!IsDiskItem(disk)) continue;
				if (!TryLoadDiskPayload(disk, out var payload) || payload == null) continue;

				var changed = false;
				for (var slot = 0; slot < payload.Length && toRemove > 0; slot++)
				{
					var s = payload[slot];
					if (!TryGetItemKey(s, out var sk)) continue;
					if (!sk.Equals(key)) continue;

					var take = Math.Min(toRemove, s.count);
					s.count -= take;
					toRemove -= take;
					if (s.count <= 0) payload[slot] = ItemStack.Empty;
					else payload[slot] = s;
					changed = true;
				}

				if (changed)
				{
					SaveDiskPayload(ref disk, payload);
					driveItems[bay] = disk;
					changedDriveTes.Add(driveTe);
				}
			}
		}

		foreach (var driveTe in changedDriveTes)
		{
			driveTe.SetModified();
		}
	}

	internal static void UpdateReaderSnapshot(TileEntityLootContainer readerTe, ItemKey key, int delta)
	{
		var id = GetReaderId(readerTe);
		if (ReaderSessionsByReaderId.TryGetValue(id, out var session) && session != null)
		{
			if (session.SnapshotCounts == null) session.SnapshotCounts = new Dictionary<ItemKey, int>();
			
			if (session.SnapshotCounts.TryGetValue(key, out var current))
			{
				session.SnapshotCounts[key] = current + delta;
			}
			else
			{
				session.SnapshotCounts[key] = delta;
			}
		}
	}

	private static void ApplyWithdrawalsToAdjacentDrive(TileEntityLootContainer readerTe, Dictionary<ItemKey, int> snapshotCounts, Dictionary<ItemKey, int> currentCounts)
	{
		if (snapshotCounts == null || snapshotCounts.Count == 0)
			return;

		var drives = FindAdjacentDiskDriveNetwork(readerTe);
		if (drives.Count == 0)
			return;

		var driveItemsByTe = new Dictionary<TileEntityLootContainer, ItemStack[]>();
		for (var d = 0; d < drives.Count; d++)
		{
			var te = drives[d];
			if (te == null)
				continue;
			var items = te.GetItems();
			if (items != null)
				driveItemsByTe[te] = items;
		}

		var changedDriveTes = new HashSet<TileEntityLootContainer>();
		foreach (var kvp in snapshotCounts)
		{
			var key = kvp.Key;
			var snapCount = kvp.Value;
			currentCounts.TryGetValue(key, out var curCount);
			var toRemove = snapCount - curCount;
			if (toRemove <= 0)
				continue;

			foreach (var entry in driveItemsByTe)
			{
				if (toRemove <= 0)
					break;
				var driveTe = entry.Key;
				var driveItems = entry.Value;
				if (driveItems == null)
					continue;

				var scanLen = Math.Min(DiskBayCount, driveItems.Length);
				for (var bay = 0; bay < scanLen && toRemove > 0; bay++)
				{
					var disk = driveItems[bay];
					if (!IsDiskItem(disk))
						continue;
					if (!TryLoadDiskPayload(disk, out var payload) || payload == null)
						continue;

					var changed = false;
					for (var slot = 0; slot < payload.Length && toRemove > 0; slot++)
					{
						var s = payload[slot];
						if (!TryGetItemKey(s, out var sk))
							continue;
						if (!sk.Equals(key))
							continue;

						var take = Math.Min(toRemove, s.count);
						s.count -= take;
						toRemove -= take;
						if (s.count <= 0)
							payload[slot] = ItemStack.Empty;
						else
							payload[slot] = s;
						changed = true;
					}

					if (changed)
					{
						SaveDiskPayload(ref disk, payload);
						driveItems[bay] = disk;
						changedDriveTes.Add(driveTe);
					}
				}
			}
		}

		foreach (var driveTe in changedDriveTes)
		{
			if (!driveItemsByTe.TryGetValue(driveTe, out var items) || items == null)
				continue;
			driveTe.items = items;
			driveTe.SetModified();
		}
	}

	internal static void SaveFromReaderIntoDisk(TileEntityLootContainer te)
	{
		try
		{
			if (!TryGetAdjacentDriveActiveDisk(te, out var driveTe, out var diskBayIndex, out var diskStack))
				return;

			var cap = GetDiskCapacity(diskStack);

			var readerItems = te.GetItems() ?? Array.Empty<ItemStack>();
			var normalized = NormalizeStacks(readerItems, cap);

			var payloadSlots = ItemStack.CreateArray(cap);
			var copyLen = Math.Min(payloadSlots.Length, normalized.Length);
			for (var i = 0; i < copyLen; i++)
				payloadSlots[i] = normalized[i];

			var payload = SerializeItemStacks(payloadSlots);
			SetDiskMetadata(ref diskStack, DiskPayloadKey, payload);
			StampDiskLastWrite(ref diskStack);

			// Write back the updated disk stack into the drive.
			var driveItems = driveTe.GetItems();
			if (driveItems != null && diskBayIndex >= 0 && diskBayIndex < driveItems.Length)
			{
				driveItems[diskBayIndex] = diskStack;
				driveTe.items = driveItems;
				driveTe.SetModified();
			}
		}
		catch (Exception ex)
		{
			Log.Error($"[ZZ_DiskStorage] SaveFromReaderIntoDisk failed: {ex}");
		}
	}

	internal static void SyncDriveBaysToReaderIfPresent(TileEntityLootContainer readerTe)
	{
		try
		{
			var drive = FindAdjacentDiskDrive(readerTe);
			if (drive == null)
				return;
			// Reader no longer mirrors bay slots; it reads disk contents only.
		}
		catch (Exception ex)
		{
			Log.Error($"[ZZ_DiskStorage] SyncDriveBaysToReaderIfPresent failed: {ex}");
		}
	}

	internal static void SyncReaderBaysToDriveIfPresent(TileEntityLootContainer readerTe)
	{
		try
		{
			var drive = FindAdjacentDiskDrive(readerTe);
			if (drive == null)
				return;
			// Reader no longer mirrors bay slots; it reads disk contents only.
		}
		catch (Exception ex)
		{
			Log.Error($"[ZZ_DiskStorage] SyncReaderBaysToDriveIfPresent failed: {ex}");
		}
	}

	private static TileEntityLootContainer FindAdjacentDiskDrive(TileEntityLootContainer readerTe)
	{
		var world = GameManager.Instance?.World;
		if (world == null)
			return null;

		var readerPos = readerTe.ToWorldPos();
		var clrIdx = readerTe.GetClrIdx();

		var offsets = new[]
		{
			new Vector3i(1, 0, 0),
			new Vector3i(-1, 0, 0),
			new Vector3i(0, 0, 1),
			new Vector3i(0, 0, -1),
			new Vector3i(0, 1, 0),
			new Vector3i(0, -1, 0),
		};

		foreach (var off in offsets)
		{
			var pos = readerPos + off;
			var bv = world.GetBlock(pos);
			var block = bv.Block;
			if (block == null || !IsBlockName(block.blockName, "diskDrive"))
				continue;

			var te = TryGetTileEntity(world, clrIdx, pos);
			if (te is TileEntityLootContainer lootTe && IsDiskDriveTileEntity(lootTe))
				return lootTe;
		}

		return null;
	}

	private static object TryGetTileEntity(World world, int clrIdx, Vector3i pos)
	{
		try
		{
			var t = world.GetType();
			var mi = t.GetMethod("GetTileEntity", new[] { typeof(Vector3i) });
			if (mi != null)
				return mi.Invoke(world, new object[] { pos });

			mi = t.GetMethod("GetTileEntity", new[] { typeof(int), typeof(Vector3i) });
			if (mi != null)
				return mi.Invoke(world, new object[] { clrIdx, pos });

			mi = t.GetMethod("GetTileEntity", new[] { typeof(Vector3i), typeof(int) });
			if (mi != null)
				return mi.Invoke(world, new object[] { pos, clrIdx });
		}
		catch (Exception ex)
		{
			Log.Error($"[ZZ_DiskStorage] TryGetTileEntity reflection failed: {ex}");
		}

		return null;
	}

	private static ItemStack[] NormalizeStacks(ItemStack[] items, int maxUnique)
	{
		if (items == null || items.Length == 0)
			return Array.Empty<ItemStack>();

		// Merge ONLY quality-0 items. Anything with quality > 0 is never merged.
		var merged = new System.Collections.Generic.Dictionary<(int type, float useTimes), ItemStack>();
		var nonMerge = new System.Collections.Generic.List<ItemStack>();
		for (var i = 0; i < items.Length; i++)
		{
			var s = items[i];
			if (s == null || s.IsEmpty())
				continue;

			var iv = s.itemValue;
			if (iv == null || iv.IsEmpty())
				continue;

			if (iv.Quality > 0)
			{
				nonMerge.Add(s);
				continue;
			}

			var key = (iv.type, iv.UseTimes);
			if (merged.TryGetValue(key, out var existing))
			{
				existing.count += s.count;
				merged[key] = existing;
				continue;
			}

			merged[key] = s;
		}

		var total = merged.Count + nonMerge.Count;
		if (total == 0)
			return Array.Empty<ItemStack>();

		var result = ItemStack.CreateArray(Math.Min(maxUnique, total));
		var idx = 0;
		foreach (var kvp in merged)
		{
			if (idx >= result.Length)
				break;
			result[idx++] = kvp.Value;
		}
		for (var i = 0; i < nonMerge.Count && idx < result.Length; i++)
			result[idx++] = nonMerge[i];

		// Trim to actual length.
		if (idx == result.Length)
			return result;
		var trimmed = ItemStack.CreateArray(idx);
		Array.Copy(result, trimmed, idx);
		return trimmed;
	}

	internal static ItemStack DepositToDrives(TileEntityLootContainer readerTe, ItemStack item)
	{
		if (item.IsEmpty()) return item;

		var drives = FindAdjacentDiskDriveNetwork(readerTe);
		if (drives.Count == 0) return item;

		var remaining = item.Clone();
		var changedDriveTes = new HashSet<TileEntityLootContainer>();

		foreach (var driveTe in drives)
		{
			if (remaining.count <= 0) break;
			var driveItems = driveTe.GetItems();
			if (driveItems == null) continue;

			var scanLen = Math.Min(DiskBayCount, driveItems.Length);
			for (var bay = 0; bay < scanLen && remaining.count > 0; bay++)
			{
				var disk = driveItems[bay];
				if (!IsDiskItem(disk)) continue;
				if (!TryLoadDiskPayload(disk, out var payload)) continue;

				var changed = false;
				remaining = DepositIntoPayload(payload, remaining, out changed);

				if (changed)
				{
					SaveDiskPayload(ref disk, payload);
					driveItems[bay] = disk;
					changedDriveTes.Add(driveTe);
				}
			}
		}

		foreach (var driveTe in changedDriveTes)
		{
			driveTe.SetModified();
		}
		
		return remaining;
	}

	internal static ItemStack DepositIntoPayload(ItemStack[] payloadSlots, ItemStack incoming, out bool changed)
	{
		changed = false;
		if (payloadSlots == null || payloadSlots.Length == 0 || incoming.IsEmpty())
			return incoming;

		var remaining = incoming;
		var iv = remaining.itemValue;
		if (iv == null || iv.IsEmpty())
			return incoming;

		var mergeAllowed = iv.Quality == 0;
		var maxStack = iv.ItemClass?.Stacknumber != null ? iv.ItemClass.Stacknumber.Value : 1;

		if (mergeAllowed)
		{
			for (var i = 0; i < payloadSlots.Length && !remaining.IsEmpty(); i++)
			{
				var cur = payloadSlots[i];
				if (cur == null || cur.IsEmpty())
					continue;

				var civ = cur.itemValue;
				if (civ == null || civ.IsEmpty())
					continue;
				if (civ.Quality != 0)
					continue;
				if (civ.type != iv.type)
					continue;
				if (Math.Abs(civ.UseTimes - iv.UseTimes) > 0.0001f)
					continue;

				var room = maxStack - cur.count;
				if (room <= 0)
					continue;
				var toMove = Math.Min(room, remaining.count);
				cur.count += toMove;
				remaining.count -= toMove;
				payloadSlots[i] = cur;
				changed = true;
			}
		}

		for (var i = 0; i < payloadSlots.Length && !remaining.IsEmpty(); i++)
		{
			var cur = payloadSlots[i];
			if (cur != null && !cur.IsEmpty())
				continue;

			var toMove = Math.Min(maxStack, remaining.count);
			payloadSlots[i] = CopyStackWithCount(remaining, toMove);
			remaining.count -= toMove;
			changed = true;
			if (remaining.count <= 0)
				remaining = ItemStack.Empty;
		}

		return remaining;
	}

	private static string SerializeItemStacks(ItemStack[] slots)
	{
		using var ms = new MemoryStream();
		using var bw = new BinaryWriter(ms);

		bw.Write(slots.Length);
		for (var i = 0; i < slots.Length; i++)
		{
			var stack = slots[i];
			if (stack == null)
				stack = ItemStack.Empty;
			stack.Write(bw);
		}

		bw.Flush();
		return Convert.ToBase64String(ms.ToArray());
	}

	private static ItemStack[] DeserializeItemStacks(string base64, int expectedLen)
	{
		var slots = ItemStack.CreateArray(expectedLen);

		try
		{
			var bytes = Convert.FromBase64String(base64);
			using var ms = new MemoryStream(bytes);
			using var br = new BinaryReader(ms);

			var len = br.ReadInt32();
			var readLen = Math.Min(len, expectedLen);

			for (var i = 0; i < readLen; i++)
				slots[i] = new ItemStack().Read(br);
		}
		catch (Exception ex)
		{
			Log.Error($"[ZZ_DiskStorage] DeserializeItemStacks failed; treating as empty. Error: {ex}");
		}

		return slots;
	}
}
