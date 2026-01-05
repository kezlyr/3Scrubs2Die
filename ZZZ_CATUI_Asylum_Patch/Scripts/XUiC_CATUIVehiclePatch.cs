using UnityEngine;
using UnityEngine.Scripting;
using System.Collections.Generic;

[Preserve]
public class XUiC_CATUIVehiclePatch : XUiController
{
    private EntityVehicle currentVehicle;
    private readonly CachedStringFormatterFloat speedFormatter = new CachedStringFormatterFloat();
    private readonly CachedStringFormatterInt speedKPHFormatter = new CachedStringFormatterInt();
    private static Dictionary<string, XUiC_CATUIVehiclePatch> instances = new Dictionary<string, XUiC_CATUIVehiclePatch>();

    public override void Init()
    {
        base.Init();
        // Register this instance globally so other controllers can access it
        string playerId = base.xui?.playerUI?.entityPlayer?.entityId.ToString() ?? "global";
        instances[playerId] = this;
        Log.Out($"[CATUI Patch] Registered vehicle patch controller for player {playerId}");
    }

    public override void Update(float _dt)
    {
        base.Update(_dt);

        // Get current vehicle from XUi
        EntityVehicle vehicle = base.xui?.vehicle;
        if (vehicle != currentVehicle)
        {
            currentVehicle = vehicle;
            RefreshBindings();
        }
    }

    public static string GetSafeVehicleBinding(string bindingName, XUi xui = null)
    {
        try
        {
            // Try to find an instance for this player
            string playerId = xui?.playerUI?.entityPlayer?.entityId.ToString() ?? "global";
            if (instances.TryGetValue(playerId, out XUiC_CATUIVehiclePatch instance))
            {
                string value = "";
                if (instance.GetBindingValue(ref value, bindingName))
                {
                    return value;
                }
            }

            // Fallback to safe defaults
            switch (bindingName)
            {
                case "CATUI_VehicleCurrentSpeedFill":
                case "CATUI_VehicleCurrentSpeedKPH":
                case "CATUI_VehicleInventoryItemCount":
                case "CATUI_VehicleInventorySlotCount":
                    return "0";
                case "CATUI_VehicleIsBrake":
                case "CATUI_VehicleIsTurbo":
                case "CATUI_VehicleHasLight":
                case "CATUI_VehicleIsLight":
                    return "false";
                default:
                    return "0";
            }
        }
        catch (System.Exception ex)
        {
            Log.Warning($"[CATUI Patch] Error in GetSafeVehicleBinding: {ex.Message}");
            return "0";
        }
    }

    public new bool GetBindingValue(ref string value, string bindingName)
    {
        if (currentVehicle == null)
        {
            // Provide safe default values when no vehicle
            switch (bindingName)
            {
                case "CATUI_VehicleCurrentSpeedFill":
                    value = "0";
                    return true;
                case "CATUI_VehicleCurrentSpeedKPH":
                    value = "0";
                    return true;
                case "CATUI_VehicleIsBrake":
                    value = "false";
                    return true;
                case "CATUI_VehicleIsTurbo":
                    value = "false";
                    return true;
                case "CATUI_VehicleHasLight":
                    value = "false";
                    return true;
                case "CATUI_VehicleIsLight":
                    value = "false";
                    return true;
                case "CATUI_VehicleInventoryItemCount":
                    value = "0";
                    return true;
                case "CATUI_VehicleInventorySlotCount":
                    value = "0";
                    return true;
            }
            return false;
        }

        try
        {
            Vehicle vehicleScript = currentVehicle.GetVehicle();
            if (vehicleScript == null)
            {
                // Vehicle script not available, provide safe defaults
                switch (bindingName)
                {
                    case "CATUI_VehicleCurrentSpeedFill":
                        value = "0";
                        return true;
                    case "CATUI_VehicleCurrentSpeedKPH":
                        value = "0";
                        return true;
                    case "CATUI_VehicleIsBrake":
                        value = "false";
                        return true;
                    case "CATUI_VehicleIsTurbo":
                        value = "false";
                        return true;
                    case "CATUI_VehicleHasLight":
                        value = "false";
                        return true;
                    case "CATUI_VehicleIsLight":
                        value = "false";
                        return true;
                    case "CATUI_VehicleInventoryItemCount":
                        value = "0";
                        return true;
                    case "CATUI_VehicleInventorySlotCount":
                        value = "0";
                        return true;
                }
                return false;
            }

            switch (bindingName)
            {
                case "CATUI_VehicleCurrentSpeedFill":
                    // Calculate speed fill as percentage (0-1)
                    float currentSpeed = vehicleScript.CurrentVelocity.magnitude;
                    float maxSpeed = vehicleScript.MaxPossibleSpeed;
                    float speedFill = (maxSpeed > 0) ? Mathf.Clamp01(currentSpeed / maxSpeed) : 0f;
                    value = speedFormatter.Format(speedFill);
                    return true;

                case "CATUI_VehicleCurrentSpeedKPH":
                    // Convert speed to KPH (assuming game speed is in m/s)
                    float speedMPS = vehicleScript.CurrentVelocity.magnitude;
                    int speedKPH = Mathf.RoundToInt(speedMPS * 3.6f); // Convert m/s to km/h
                    value = speedKPHFormatter.Format(speedKPH);
                    return true;

                case "CATUI_VehicleIsBrake":
                    // Check if braking
                    bool isBraking = vehicleScript.CurrentIsBreak;
                    value = isBraking.ToString().ToLower();
                    return true;

                case "CATUI_VehicleIsTurbo":
                    // Check if using turbo
                    bool isTurbo = vehicleScript.IsTurbo;
                    value = isTurbo.ToString().ToLower();
                    return true;

                case "CATUI_VehicleHasLight":
                    // Check if vehicle has headlight capability
                    bool hasLight = currentVehicle.HasHeadlight();
                    value = hasLight.ToString().ToLower();
                    return true;

                case "CATUI_VehicleIsLight":
                    // Check if headlight is currently on
                    bool isLightOn = currentVehicle.IsHeadlightOn;
                    value = isLightOn.ToString().ToLower();
                    return true;

                case "CATUI_VehicleInventoryItemCount":
                    // Get current item count in vehicle storage
                    int itemCount = GetVehicleItemCount();
                    value = itemCount.ToString();
                    return true;

                case "CATUI_VehicleInventorySlotCount":
                    // Get total storage slots
                    int slotCount = GetVehicleSlotCount();
                    value = slotCount.ToString();
                    return true;
            }
        }
        catch (System.Exception ex)
        {
            // Log error and provide safe fallback
            Log.Warning($"[CATUI Patch] Error getting vehicle binding '{bindingName}': {ex.Message}");
            
            switch (bindingName)
            {
                case "CATUI_VehicleCurrentSpeedFill":
                case "CATUI_VehicleCurrentSpeedKPH":
                case "CATUI_VehicleInventoryItemCount":
                case "CATUI_VehicleInventorySlotCount":
                    value = "0";
                    return true;
                case "CATUI_VehicleIsBrake":
                case "CATUI_VehicleIsTurbo":
                case "CATUI_VehicleHasLight":
                case "CATUI_VehicleIsLight":
                    value = "false";
                    return true;
            }
        }

        return false;
    }

    private int GetVehicleItemCount()
    {
        try
        {
            if (currentVehicle?.bag?.GetSlots() != null)
            {
                int count = 0;
                ItemStack[] slots = currentVehicle.bag.GetSlots();
                for (int i = 0; i < slots.Length; i++)
                {
                    if (!slots[i].IsEmpty())
                        count += slots[i].count;
                }
                return count;
            }
        }
        catch (System.Exception ex)
        {
            Log.Warning($"[CATUI Patch] Error getting vehicle item count: {ex.Message}");
        }
        return 0;
    }

    private int GetVehicleSlotCount()
    {
        try
        {
            if (currentVehicle?.bag?.GetSlots() != null)
            {
                return currentVehicle.bag.GetSlots().Length;
            }
        }
        catch (System.Exception ex)
        {
            Log.Warning($"[CATUI Patch] Error getting vehicle slot count: {ex.Message}");
        }
        return 0;
    }
}
