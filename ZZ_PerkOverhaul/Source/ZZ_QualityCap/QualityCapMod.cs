using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local

namespace ZZ_QualityCap
{
	public sealed class QualityCapMod : IModApi
	{
		public void InitMod(Mod modInstance)
		{
			try
			{
				var harmony = new Harmony("zz.perkoverhaul.qualitycap");
				harmony.PatchAll(Assembly.GetExecutingAssembly());
				Log.Out("[ZZ_QualityCap] Harmony patches applied.");

				// Runtime sanity check: log whether XML override_cost actually loaded into ProgressionClass.OverrideCost.
				// This runs after the game finishes starting and the progression database is available.
				try
				{
					ModEvents.GameStartDone.RegisterHandler(OnGameStartDone);
				}
				catch (Exception ex)
				{
					Log.Warning("[ZZ_QualityCap] Failed to register GameStartDone handler: " + ex.Message);
				}
			}
			catch (Exception ex)
			{
				Log.Error("[ZZ_QualityCap] Failed to apply Harmony patches: " + ex);
			}
		}

		private static void OnGameStartDone(ref ModEvents.SGameStartDoneData _)
		{
			try
			{
				var world = GameManager.Instance?.World;
				var player = world?.GetPrimaryPlayer();
				var pv = player?.Progression?.GetProgressionValue("perkInfiltrator");
				var pc = pv?.ProgressionClass;
				var oc = pc?.OverrideCost;
				var ocStr = (oc == null) ? "<null>" : string.Join(",", oc);
				Log.Out($"[ZZ_QualityCap] perkInfiltrator OverrideCost loaded: {ocStr}");
			}
			catch (Exception ex)
			{
				Log.Warning("[ZZ_QualityCap] GameStartDone sanity check failed: " + ex.Message);
			}
		}
	}

	/// <summary>
	/// Vanilla appears to only honor override_cost for a subset of perks (e.g. General Perks).
	/// This patch makes ProgressionClass.OverrideCost apply consistently for any perk purchased with Skill Points.
	/// </summary>
	[HarmonyPatch(typeof(ProgressionClass), nameof(ProgressionClass.CalculatedCostForLevel))]
	internal static class Patch_ProgressionClass_CalculatedCostForLevel
	{
		private static bool Prefix(ProgressionClass __instance, int _level, ref int __result)
		{
			try
			{
				if (__instance == null) return true;
				if (__instance.CurrencyType != ProgressionCurrencyType.SP) return true;
				if (!__instance.IsPerk) return true;

				var oc = __instance.OverrideCost;
				if (oc == null || oc.Length == 0) return true;

				var idx = _level - 1;
				if (idx < 0) idx = 0;
				if (idx >= oc.Length) idx = oc.Length - 1;

				var v = oc[idx];
				if (v <= 0) return true;

				__result = v;
				return false;
			}
			catch
			{
				return true;
			}
		}
	}

	/// <summary>
	/// Some UI and/or purchase paths use ProgressionValue.CostForNextLevel (cached) rather than recalculating.
	/// Force it to be calculated from ProgressionClass rules so override_cost takes effect everywhere.
	/// </summary>
	[HarmonyPatch(typeof(ProgressionValue), "get_CostForNextLevel")]
	internal static class Patch_ProgressionValue_get_CostForNextLevel
	{
		private static bool Prefix(ProgressionValue __instance, ref int __result)
		{
			try
			{
				var pc = __instance?.ProgressionClass;
				if (pc == null) return true;
				if (pc.CurrencyType != ProgressionCurrencyType.SP) return true;
				if (!pc.IsPerk) return true;

				// Next level purchase cost
				var nextLevel = __instance.Level + 1;
				__result = pc.CalculatedCostForLevel(nextLevel);
				return false;
			}
			catch
			{
				return true;
			}
		}
	}

	[HarmonyPatch(typeof(XUiC_SkillPerkLevel), "GetBindingValueInternal")]
	internal static class Patch_XUiC_SkillPerkLevel_GetBindingValueInternal
	{
		// Some UI layouts (like showing 10 perk levels at once) can cause the vanilla controller
		// to hit null state while bindings are being refreshed. Swallow the NRE so the window group
		// keeps updating, and leave blank text for that entry.
		private static Exception Finalizer(Exception __exception, ref bool __result, ref string _value, string _bindingName)
		{
			if (__exception is not NullReferenceException)
				return __exception;

			try
			{
				// If the backing entry is null, vanilla can throw while refreshing bindings.
				// Return a safe empty value and keep the UI running.
				_value = string.Empty;
				__result = true;
				return null;
			}
			catch
			{
				// ignore
			}

			return __exception;
		}
	}

	internal static class QualityCap
	{
		internal const int MaxQuality = 10;
		internal const int ArrayLen = MaxQuality + 1;

		private static bool IsAllDefault(Array arr, int startIndex, int endIndexInclusive)
		{
			if (arr == null) return true;
			var elemType = arr.GetType().GetElementType();
			if (elemType == null) return true;
			var defaultValue = elemType.IsValueType ? Activator.CreateInstance(elemType) : null;

			for (var i = startIndex; i <= endIndexInclusive && i < arr.Length; i++)
			{
				var v = arr.GetValue(i);
				if (!Equals(v, defaultValue))
					return false;
			}

			return true;
		}

		private static float Clamp01(float v)
		{
			if (v < 0f) return 0f;
			if (v > 1f) return 1f;
			return v;
		}

		private static bool NameContainsAny(string name, params string[] needles)
		{
			if (string.IsNullOrEmpty(name)) return false;
			foreach (var n in needles)
			{
				if (name.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0)
					return true;
			}
			return false;
		}

		private static void PopulateExtendedQualityEntries(string fieldName, Array newArr)
		{
			if (newArr == null) return;
			if (newArr.Length < ArrayLen) return;

			var elemType = newArr.GetType().GetElementType();
			if (elemType == null) return;

			// Only touch arrays that were previously vanilla-length and now need QL7-10 values.
			// If indices 7-10 are already set (e.g., by XML), leave them alone.
			if (!IsAllDefault(newArr, 7, 10))
				return;

			// Heuristic safety: never try to "invent" extra colors/strings/etc.
			if (elemType == typeof(string))
				return;

			// Special-case: keep mod slot caps stable (vanilla caps are usually enforced elsewhere too).
			if (elemType == typeof(int) && NameContainsAny(fieldName, "slot", "slots"))
			{
				var baseV = (int)newArr.GetValue(6);
				for (var i = 7; i <= 10; i++)
					newArr.SetValue(baseV, i);
				return;
			}

			// Default strategy: extrapolate from the last two vanilla steps.
			// This keeps QL7-10 "on curve" without changing QL0-6.
			if (elemType == typeof(float))
			{
				var v4 = (float)newArr.GetValue(4);
				var v5 = (float)newArr.GetValue(5);
				var v6 = (float)newArr.GetValue(6);
				var delta = v6 - v5;
				if (Math.Abs(delta) < 0.00001f)
					delta = v5 - v4;

				for (var i = 7; i <= 10; i++)
				{
					var prev = (float)newArr.GetValue(i - 1);
					var next = prev + delta;

					// Clamp a few obvious percentage-like arrays.
					if (NameContainsAny(fieldName, "chance", "prob", "probability"))
						next = Clamp01(next);
					if (NameContainsAny(fieldName, "loss"))
						if (next < 0f) next = 0f;

					newArr.SetValue(next, i);
				}

				return;
			}

			if (elemType == typeof(int))
			{
				var v4 = (int)newArr.GetValue(4);
				var v5 = (int)newArr.GetValue(5);
				var v6 = (int)newArr.GetValue(6);
				var delta = v6 - v5;
				if (delta == 0)
					delta = v5 - v4;

				for (var i = 7; i <= 10; i++)
				{
					var prev = (int)newArr.GetValue(i - 1);
					var next = prev + delta;
					if (NameContainsAny(fieldName, "loss") && next < 0) next = 0;
					newArr.SetValue(next, i);
				}
			}
		}

		internal static void ExpandType(string typeName)
		{
			var t = AccessTools.TypeByName(typeName);
			if (t == null)
			{
				Log.Warning($"[ZZ_QualityCap] Type not found: {typeName}");
				return;
			}

			// Best-effort: bump any mutable max-quality ints.
			foreach (var f in t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
			{
				if (f.FieldType != typeof(int))
					continue;

				var name = f.Name;
				if (name.IndexOf("maxquality", StringComparison.OrdinalIgnoreCase) < 0)
					continue;

				if (f.IsLiteral)
					continue; // const cannot be set

				try
				{
					f.SetValue(null, MaxQuality);
				}
				catch
				{
					// ignore
				}
			}

			// Best-effort: resize any static arrays that look like they are indexed by quality.
			foreach (var f in t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
			{
				if (!f.FieldType.IsArray)
					continue;

				var arr = f.GetValue(null) as Array;
				if (arr == null)
					continue;

				// Only grow arrays that are too small.
				if (arr.Length >= ArrayLen)
					continue;

				// Heuristic: only touch arrays that are plausibly quality-indexed (7 is the vanilla size).
				if (arr.Length != 7)
					continue;

				try
				{
					var elemType = f.FieldType.GetElementType();
					if (elemType == null)
						continue;

					var newArr = Array.CreateInstance(elemType, ArrayLen);
					Array.Copy(arr, newArr, arr.Length);
					PopulateExtendedQualityEntries(f.Name, newArr);
					f.SetValue(null, newArr);
					Log.Out($"[ZZ_QualityCap] Resized {typeName}.{f.Name} from {arr.Length} -> {ArrayLen}");
				}
				catch (Exception ex)
				{
					Log.Warning($"[ZZ_QualityCap] Failed resizing {typeName}.{f.Name}: {ex.Message}");
				}
			}
		}
	}

	internal static class TierScaling
	{
		internal const int MaxTier = 10;

		private static float SafeGet(float[] arr, int i) => (arr != null && i >= 0 && i < arr.Length) ? arr[i] : 0f;

		private static bool TryGetLastTwoPoints(float[] levels, float[] values, out float x1, out float y1, out float x2, out float y2)
		{
			x1 = y1 = x2 = y2 = 0f;
			if (levels == null || values == null) return false;
			if (levels.Length < 2 || values.Length < 2) return false;
			if (levels.Length != values.Length) return false;

			var last = levels.Length - 1;
			x2 = levels[last];
			y2 = values[last];
			x1 = levels[last - 1];
			y1 = values[last - 1];
			return true;
		}

		internal static void EnsureTier10ForOutOfRange(PassiveEffect pe, float requestedTier)
		{
			if (pe == null) return;
			if (requestedTier <= 6f) return; // leave vanilla tiers untouched

			var levels = pe.Levels;
			var values = pe.Values;
			if (levels == null || values == null) return;
			if (levels.Length == 0 || values.Length == 0) return;
			if (levels.Length != values.Length) return;

			var maxConfigured = levels.Max();
			if (maxConfigured >= MaxTier) return; // already extended
			if (Math.Abs(maxConfigured - 6f) > 0.0001f) return; // only extend the vanilla ceiling
			if (requestedTier <= maxConfigured) return;

			// Special case: ModSlots is effectively integer-valued and should not go crazy.
			// Many items top out at 4 slots; if tier 6 is already >=4, keep it flat.
			if (pe.Type == PassiveEffects.ModSlots)
			{
				var v6 = values[values.Length - 1];
				var v10 = v6;
				if (v10 < 4f)
					v10 = 4f;

				pe.Levels = AppendPoint(levels, MaxTier);
				pe.Values = AppendPoint(values, v10);
				return;
			}

			// Default: add a tier-10 control point by extending the last segment linearly.
			if (!TryGetLastTwoPoints(levels, values, out var x1, out var y1, out var x2, out var y2))
				return;
			var dx = x2 - x1;
			if (Math.Abs(dx) < 0.0001f)
				return;
			var slope = (y2 - y1) / dx;
			var y10 = y2 + slope * (MaxTier - x2);

			pe.Levels = AppendPoint(levels, MaxTier);
			pe.Values = AppendPoint(values, y10);
		}

		private static float[] AppendPoint(float[] src, float value)
		{
			var dst = new float[src.Length + 1];
			Array.Copy(src, dst, src.Length);
			dst[dst.Length - 1] = value;
			return dst;
		}
	}

	[HarmonyPatch]
	internal static class Patch_QualityInfo_Cctor
	{
		private static MethodBase TargetMethod()
		{
			var t = AccessTools.TypeByName("QualityInfo");
			return t?.TypeInitializer;
		}

		private static void Postfix()
		{
			QualityCap.ExpandType("QualityInfo");
		}
	}

	[HarmonyPatch]
	internal static class Patch_ItemValue_Cctor
	{
		private static MethodBase TargetMethod()
		{
			var t = AccessTools.TypeByName("ItemValue");
			return t?.TypeInitializer;
		}

		private static void Postfix()
		{
			QualityCap.ExpandType("ItemValue");
		}
	}

	// Extra safety: ensure the quality arrays are expanded right before parsing qualityinfo.xml.
	[HarmonyPatch]
	internal static class Patch_QualityInfoFromXml_CreateQualityInfo
	{
		private static MethodBase TargetMethod()
		{
			var t = AccessTools.TypeByName("QualityInfoFromXml");
			if (t == null) return null;

			// Prefer the known method name from metadata if present.
			var m = AccessTools.Method(t, "CreateQualityInfo");
			if (m != null) return m;

			// Fallback: any static method returning QualityInfo.
			return t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
				.FirstOrDefault(x => x.ReturnType != typeof(void) && x.ReturnType.Name == "QualityInfo");
		}

		private static void Prefix()
		{
			QualityCap.ExpandType("QualityInfo");
			QualityCap.ExpandType("ItemValue");
		}
	}

	// Fix tiered passive effects that were authored with a vanilla tier ceiling of 6.
	// Without this, QL7+ often falls back to the tier-1 value for arrays like ModSlots.
	[HarmonyPatch(typeof(PassiveEffect))]
	[HarmonyPatch(nameof(PassiveEffect.ModifyValue))]
	internal static class Patch_PassiveEffect_ModifyValue_Tier10
	{
		private static void Prefix(PassiveEffect __instance, float _level)
		{
			try
			{
				TierScaling.EnsureTier10ForOutOfRange(__instance, _level);
			}
			catch (Exception ex)
			{
				Log.Warning("[ZZ_QualityCap] Tier scaling patch failed: " + ex.Message);
			}
		}
	}

	// NOTE: Crafting UI patches removed by request.
}
