namespace ZZ_DiskStorage;

internal sealed class ItemActionDepositToDiskStorage : ItemAction
{
	public override void ExecuteAction(ItemActionData _actionData, bool _bReleased)
	{
		// Only act on click/press, not release.
		if (_bReleased)
			return;

		try
		{
			if (_actionData?.invData?.holdingEntity is not EntityPlayerLocal player)
				return;

			var hit = _actionData.GetUpdatedHitInfo();
			if (!DiskStorageLogic.TryGetDiskTargetDriveFromHit(hit, out var driveTe))
				return;

			// Dump everything except the depositor item itself.
			DiskStorageLogic.DepositInventoryIntoDrive(player, driveTe, _actionData.invData.slotIdx);
		}
		catch (System.Exception ex)
		{
			Log.Error($"[ZZ_DiskStorage] Depositor ExecuteAction failed: {ex}");
		}
	}
}
