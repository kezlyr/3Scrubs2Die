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

	private static readonly Dictionary<string, Dictionary<ItemKey, int>> ReaderSnapshotCountsByReaderId = new();

	private const int DiskBayCount = 4;
	private const int MaxUniqueItemTypesPerDisk = 30;
	private const int ReaderGridWidth = 7;

	private readonly struct ItemKey : IEquatable<ItemKey>
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
		if (!TrySetContainerSizeSafe(te, new Vector2i(0, 0)))
			TrySetContainerSizeSafe(te, new Vector2i(1, 1));
		te.items = Array.Empty<ItemStack>();
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

	private static void ResizeReaderToCount(TileEntityLootContainer te, int count)
	{
		if (count <= 0)
		{
			SetReaderEmpty(te);
			return;
		}

		var width = ReaderGridWidth;
		var height = (int)Math.Ceiling(count / (double)width);
		if (height < 1)
			height = 1;
		TrySetContainerSizeSafe(te, new Vector2i(width, height));
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
			if (block == null || !string.Equals(block.blockName, "diskDrive", StringComparison.OrdinalIgnoreCase))
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

		if (string.Equals(block.blockName, "diskDrive", StringComparison.OrdinalIgnoreCase))
			return TryGetDiskDriveAtPos(world, clrIdx, pos, out driveTe);

		if (string.Equals(block.blockName, "diskReader", StringComparison.OrdinalIgnoreCase))
			return TryFindAdjacentDiskDrive(world, clrIdx, pos, out driveTe);

		return false;
	}

	internal static bool TryLoadDiskPayload(ItemStack diskStack, out ItemStack[] payloadSlots)
	{
		payloadSlots = ItemStack.CreateArray(MaxUniqueItemTypesPerDisk);
		if (!IsDiskItem(diskStack))
			return false;

		if (!diskStack.itemValue.TryGetMetadata(DiskPayloadKey, out string payload) || string.IsNullOrWhiteSpace(payload))
		{
			if (DebugDeposit)
				Log.Out($"[ZZ_DiskStorage][DBG] TryLoadDiskPayload: no payload metadata on disk (type={diskStack.itemValue?.type}, q={diskStack.itemValue?.Quality}). Treating as empty.");
			return true;
		}

		payloadSlots = DeserializeItemStacks(payload, MaxUniqueItemTypesPerDisk);
		return true;
	}

	internal static void SaveDiskPayload(ref ItemStack diskStack, ItemStack[] payloadSlots)
	{
		if (!IsDiskItem(diskStack))
			return;
		var normalized = NormalizeStacks(payloadSlots, MaxUniqueItemTypesPerDisk);
		var fixedLen = ItemStack.CreateArray(MaxUniqueItemTypesPerDisk);
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

		return string.Equals(block.blockName, "diskReader", StringComparison.OrdinalIgnoreCase);
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

		return string.Equals(block.blockName, "diskDrive", StringComparison.OrdinalIgnoreCase);
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

		return string.Equals(block.blockName, "diskDropBox", StringComparison.OrdinalIgnoreCase);
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
			if (!TryFindAdjacentDiskDrive(world, clrIdx, pos, out var driveTe) || driveTe == null)
			{
				if (DebugDeposit)
					Log.Out($"[ZZ_DiskStorage][DBG] DropBox close: NO adjacent diskDrive found at {pos.x},{pos.y},{pos.z} clr={clrIdx}");
				return 0;
			}
			if (DebugDeposit)
			{
				var dpos = driveTe.ToWorldPos();
				Log.Out($"[ZZ_DiskStorage][DBG] DropBox close: found diskDrive at {dpos.x},{dpos.y},{dpos.z} clr={driveTe.GetClrIdx()}");
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
				TryDepositStackIntoDrive(driveTe, ref stack);
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

			LoadAllDisksIntoReader(readerTe);
			var id = GetReaderId(readerTe);
			ReaderSnapshotCountsByReaderId[id] = CountByKey(readerTe.GetItems() ?? Array.Empty<ItemStack>());
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
			if (!ReaderSnapshotCountsByReaderId.TryGetValue(id, out var snapshotCounts))
				return;
			ReaderSnapshotCountsByReaderId.Remove(id);

			var currentCounts = CountByKey(readerTe.GetItems() ?? Array.Empty<ItemStack>());
			ApplyWithdrawalsToAdjacentDrive(readerTe, snapshotCounts, currentCounts);
		}
		catch (Exception ex)
		{
			Log.Error($"[ZZ_DiskStorage] ReaderOnClose failed: {ex}");
		}
	}

	private static void LoadAllDisksIntoReader(TileEntityLootContainer readerTe)
	{
		// Reader shows ALL items on ALL disks in the adjacent drive (aggregated).
		if (!TryGetAdjacentDriveDisks(readerTe, out var driveTe, out var driveItems, out var scanLen))
		{
			if (DebugReader)
				Log.Out("[ZZ_DiskStorage][DBG] LoadAllDisksIntoReader: no adjacent drive or no disks found; setting reader empty");
			SetReaderEmpty(readerTe);
			return;
		}

		if (DebugReader)
		{
			var dpos = driveTe.ToWorldPos();
			Log.Out($"[ZZ_DiskStorage][DBG] LoadAllDisksIntoReader: drive at {dpos.x},{dpos.y},{dpos.z} clr={driveTe.GetClrIdx()} scanLen={scanLen}");
			for (var i = 0; i < scanLen; i++)
			{
				var disk = driveItems[i];
				if (!IsDiskItem(disk))
				{
					Log.Out($"[ZZ_DiskStorage][DBG]  bay {i}: not a disk");
					continue;
				}
				var hasPayload = disk.itemValue.TryGetMetadata(DiskPayloadKey, out string payload) && !string.IsNullOrWhiteSpace(payload);
				Log.Out($"[ZZ_DiskStorage][DBG]  bay {i}: disk type={disk.itemValue?.type} lastWrite={GetDiskLastWriteTicks(disk)} hasPayload={hasPayload} payloadLen={(hasPayload ? payload.Length : 0)}");
			}
		}

		var mergedQ0 = new Dictionary<(int type, int useBits), (ItemStack sample, int totalCount)>();
		var others = new List<ItemStack>();

		for (var bay = 0; bay < scanLen; bay++)
		{
			var disk = driveItems[bay];
			if (!IsDiskItem(disk))
				continue;

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
			SetReaderInsertableEmpty(readerTe);
			return;
		}

		ResizeReaderToCount(readerTe, result.Count);
		if (DebugReader)
			Log.Out($"[ZZ_DiskStorage][DBG] LoadAllDisksIntoReader: displaying {result.Count} stacks");
		readerTe.items = ItemStack.CreateArray(result.Count);
		for (var i = 0; i < result.Count; i++)
			readerTe.items[i] = result[i];
		readerTe.SetModified();
	}

	private static void ApplyWithdrawalsToAdjacentDrive(TileEntityLootContainer readerTe, Dictionary<ItemKey, int> snapshotCounts, Dictionary<ItemKey, int> currentCounts)
	{
		if (snapshotCounts == null || snapshotCounts.Count == 0)
			return;

		if (!TryGetAdjacentDriveDisks(readerTe, out var driveTe, out var driveItems, out var scanLen))
			return;

		var changedAnyDisk = false;
		foreach (var kvp in snapshotCounts)
		{
			var key = kvp.Key;
			var snapCount = kvp.Value;
			currentCounts.TryGetValue(key, out var curCount);
			var toRemove = snapCount - curCount;
			if (toRemove <= 0)
				continue;

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
					changedAnyDisk = true;
				}
			}
		}

		if (changedAnyDisk)
		{
			driveTe.items = driveItems;
			driveTe.SetModified();
		}
	}

	internal static void SaveFromReaderIntoDisk(TileEntityLootContainer te)
	{
		try
		{
			if (!TryGetAdjacentDriveActiveDisk(te, out var driveTe, out var diskBayIndex, out var diskStack))
				return;

			var readerItems = te.GetItems() ?? Array.Empty<ItemStack>();
			var normalized = NormalizeStacks(readerItems, MaxUniqueItemTypesPerDisk);

			var payloadSlots = ItemStack.CreateArray(MaxUniqueItemTypesPerDisk);
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
			if (block == null || !string.Equals(block.blockName, "diskDrive", StringComparison.OrdinalIgnoreCase))
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

	private static ItemStack DepositIntoPayload(ItemStack[] payloadSlots, ItemStack incoming, out bool changed)
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
