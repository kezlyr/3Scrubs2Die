using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using HarmonyLib;
using UnityEngine;

public class OcbCustomTextures : IModApi
{
	[HarmonyPatch(typeof(BlocksFromXml), "CreateBlocks")]
	private static class BlocksFromXmlCreateBlocksPrefix
	{
		private static void Prefix(XmlFile _xmlFile)
		{
			if (_xmlFile == null)
			{
				return;
			}
			XElement root = _xmlFile.XmlDoc.Root;
			if (!root.HasElements)
			{
				return;
			}
			foreach (XElement item in root.Elements("block"))
			{
				foreach (XElement item2 in item.Elements("property"))
				{
					ResolveTextureProperties(item2);
				}
			}
			GrassTextures.ExecuteGrassPatching(MeshDescription.meshes[3]);
		}
	}

	[HarmonyPatch(typeof(MeshDescription), "Unload")]
	private static class MeshDescriptionUnload
	{
		private static bool Prefix(Texture tex)
		{
			if ((Object)(object)tex == (Object)null)
			{
				return true;
			}
			return !((Object)tex).name.StartsWith("extended_");
		}
	}

	public static string GrassBundle;

	public static string CommonBundle;

	public static readonly Dictionary<string, int> UvMap = new Dictionary<string, int>();

	public void InitMod(Mod mod)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		Log.Out("OCB Harmony Patch: " + GetType().ToString());
		new Harmony(GetType().ToString()).PatchAll(Assembly.GetExecutingAssembly());
		GrassBundle = Path.Combine(mod.Path, "Resources/Grass.unity3d");
		CommonBundle = Path.Combine(mod.Path, "Resources/Common.unity3d");
	}

	public static DynamicProperties GetDynamicProperties(XElement xml)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Expected O, but got Unknown
		DynamicProperties val = new DynamicProperties();
		foreach (XElement item in xml.Elements())
		{
			if (item.Name == "property")
			{
				val.Add(item, true, false);
			}
		}
		return val;
	}

	private static void ResolveTextureProperties(XElement prop)
	{
		ResolveTexture("Texture", prop);
		ResolveTexture("UiBackgroundTexture", prop);
	}

	private static void ResolveTexture(string name, XElement prop)
	{
		if (!XmlExtensions.HasAttribute(prop, (XName)"name") || prop.Attribute("name").Value != name || !XmlExtensions.HasAttribute(prop, (XName)"value"))
		{
			return;
		}
		string value = prop.Attribute("value").Value;
		if (!value.All((char x) => (x >= '0' && x <= '9') || x == ','))
		{
			string text = ResolveTextureIDs(value);
			prop.SetAttributeValue("value", text);
			if (!text.All((char x) => (x >= '0' && x <= '9') || x == ','))
			{
				Log.Error("Texture name(s) not resolved: {0}", new object[1] { text });
			}
		}
	}

	private static string ResolveTextureIDs(string value)
	{
		if (!value.All((char x) => (x >= '0' && x <= '9') || x == ','))
		{
			if (value.Contains(","))
			{
				string[] array = value.Split(',');
				for (int num = 0; num < array.Length; num++)
				{
					if (UvMap.TryGetValue(array[num], out var value2))
					{
						array[num] = value2.ToString();
					}
				}
				return string.Join(",", array);
			}
			if (UvMap.TryGetValue(value, out var value3))
			{
				return value3.ToString();
			}
		}
		return value;
	}

	internal static void ReleaseTexture(Texture org, Texture cpy, bool addressable = true)
	{
		if ((Object)(object)org == (Object)(object)cpy || (Object)(object)org == (Object)null)
		{
			return;
		}
		if (addressable)
		{
			Log.Out("Release: {0}", new object[1] { org });
			if (!((Object)org).name.StartsWith("extended_"))
			{
				LoadManager.ReleaseAddressable<Texture>(org);
			}
		}
		else
		{
			Log.Out("Unload: {0}", new object[1] { org });
			if (!((Object)org).name.StartsWith("extended_"))
			{
				Resources.UnloadAsset((Object)(object)org);
			}
		}
	}
}
