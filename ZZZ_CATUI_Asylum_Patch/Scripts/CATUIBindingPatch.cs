using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

[HarmonyPatch]
public static class CATUIBindingPatch
{

    // Patch BindingInfo.RefreshValue to handle ArrayTypeMismatchException
    [HarmonyPatch(typeof(BindingInfo), "RefreshValue")]
    [HarmonyFinalizer]
    public static Exception RefreshValue_Finalizer(Exception __exception, BindingInfo __instance)
    {
        if (__exception != null && __exception is System.ArrayTypeMismatchException)
        {
            try
            {
                // Check if this is related to CATUI bindings using the SourceText property
                if (__instance.SourceText != null && __instance.SourceText.Contains("CATUI"))
                {
                    // Silently suppress the exception - no logging
                    return null;
                }
            }
            catch
            {
                // Silently handle any errors in the patch itself
            }
        }

        return __exception; // Let other exceptions through
    }

    // Patch the NCalc expression evaluation to handle null/invalid CATUI vehicle bindings
    [HarmonyPatch(typeof(BindingItemNcalc), "EvaluateExpression")]
    [HarmonyPrefix]
    public static bool EvaluateExpression_Prefix(BindingItemNcalc __instance, ref object __result)
    {
        try
        {
            // Check if this is a CATUI vehicle binding that might fail
            string expression = __instance.FieldName;
            if (expression != null && expression.Contains("CATUI_Vehicle"))
            {
                // Try to evaluate safely
                return TryEvaluateCATUIVehicleExpression(__instance, expression, out __result);
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[CATUI Patch] Error in binding evaluation prefix: {ex.Message}");
        }

        // Continue with original method
        return true;
    }

    // Additional patch for XUiC_HUDStatBar to prevent binding refresh errors
    [HarmonyPatch(typeof(XUiC_HUDStatBar), "Update")]
    [HarmonyFinalizer]
    public static Exception HUDStatBar_Update_Finalizer(Exception __exception, XUiC_HUDStatBar __instance)
    {
        if (__exception != null && __exception is System.ArrayTypeMismatchException)
        {
            Log.Warning($"[CATUI Patch] Suppressed ArrayTypeMismatchException in HUDStatBar.Update");
            return null; // Suppress the exception
        }
        return __exception;
    }

    [HarmonyPatch(typeof(BindingItemNcalc), "EvaluateExpression")]
    [HarmonyFinalizer]
    public static Exception EvaluateExpression_Finalizer(Exception __exception, BindingItemNcalc __instance, ref object __result)
    {
        if (__exception != null)
        {
            try
            {
                string expression = __instance.FieldName;
                if (expression != null && expression.Contains("CATUI_Vehicle"))
                {
                    Log.Warning($"[CATUI Patch] Caught CATUI vehicle binding error: {__exception.Message}");

                    // Provide safe fallback values
                    if (expression.Contains("CATUI_VehicleCurrentSpeedFill"))
                    {
                        __result = 0.0;
                        return null; // Suppress the exception
                    }
                    else if (expression.Contains("CATUI_VehicleIsBrake") || expression.Contains("CATUI_VehicleIsTurbo"))
                    {
                        __result = false;
                        return null;
                    }
                    else if (expression.Contains("CATUI_VehicleCurrentSpeedKPH"))
                    {
                        __result = 0;
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[CATUI Patch] Error in finalizer: {ex.Message}");
            }
        }
        
        return __exception;
    }

    private static bool TryEvaluateCATUIVehicleExpression(BindingItemNcalc instance, string expression, out object result)
    {
        result = null;
        
        try
        {
            // Get the XUi context to check for vehicle
            XUi xui = instance.view?.Controller?.xui;
            EntityVehicle vehicle = xui?.vehicle;
            
            if (vehicle == null)
            {
                // No vehicle, provide safe defaults
                if (expression.Contains("CATUI_VehicleCurrentSpeedFill"))
                {
                    result = 0.0;
                    return false; // Skip original method
                }
                else if (expression.Contains("CATUI_VehicleIsBrake") || expression.Contains("CATUI_VehicleIsTurbo"))
                {
                    result = false;
                    return false;
                }
                else if (expression.Contains("CATUI_VehicleCurrentSpeedKPH"))
                {
                    result = 0;
                    return false;
                }
            }
            else
            {
                // Vehicle exists, try to get safe values
                Vehicle vehicleScript = vehicle.GetVehicle();
                if (vehicleScript != null)
                {
                    if (expression.Contains("CATUI_VehicleCurrentSpeedFill"))
                    {
                        float currentSpeed = vehicleScript.CurrentVelocity.magnitude;
                        float maxSpeed = vehicleScript.MaxPossibleSpeed;
                        float speedFill = (maxSpeed > 0) ? Mathf.Clamp01(currentSpeed / maxSpeed) : 0f;
                        result = speedFill;
                        return false;
                    }
                    else if (expression.Contains("CATUI_VehicleCurrentSpeedKPH"))
                    {
                        float speedMPS = vehicleScript.CurrentVelocity.magnitude;
                        int speedKPH = Mathf.RoundToInt(speedMPS * 3.6f);
                        result = speedKPH;
                        return false;
                    }
                    else if (expression.Contains("CATUI_VehicleIsBrake"))
                    {
                        bool isBraking = vehicleScript.CurrentIsBreak;
                        result = isBraking;
                        return false;
                    }
                    else if (expression.Contains("CATUI_VehicleIsTurbo"))
                    {
                        bool isTurbo = vehicleScript.IsTurbo;
                        result = isTurbo;
                        return false;
                    }
                }
                else
                {
                    // Vehicle script not available, provide safe defaults
                    if (expression.Contains("CATUI_VehicleCurrentSpeedFill"))
                    {
                        result = 0.0;
                        return false;
                    }
                    else if (expression.Contains("CATUI_VehicleIsBrake") || expression.Contains("CATUI_VehicleIsTurbo"))
                    {
                        result = false;
                        return false;
                    }
                    else if (expression.Contains("CATUI_VehicleCurrentSpeedKPH"))
                    {
                        result = 0;
                        return false;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[CATUI Patch] Error evaluating CATUI vehicle expression '{expression}': {ex.Message}");
            
            // Provide safe fallback
            if (expression.Contains("CATUI_VehicleCurrentSpeedFill"))
            {
                result = 0.0;
            }
            else if (expression.Contains("CATUI_VehicleIsBrake") || expression.Contains("CATUI_VehicleIsTurbo"))
            {
                result = false;
            }
            else if (expression.Contains("CATUI_VehicleCurrentSpeedKPH"))
            {
                result = 0;
            }
            
            return false; // Skip original method
        }
        
        // Continue with original method
        return true;
    }
}

// Initialize the Harmony patches
public class CATUIPatchInit : IModApi
{
    public void InitMod(Mod modInstance)
    {
        var harmony = new Harmony("catui.zombieinc.patch");
        harmony.PatchAll();
    }
}
