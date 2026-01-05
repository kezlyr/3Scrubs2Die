using System;
using System.Collections.Generic;
using System.Xml.Linq;
using HarmonyLib;
using UnityEngine;

public static class GrassTextures
{
	[HarmonyPatch(typeof(BlockTexturesFromXML), "CreateBlockTextures")]
	public class BlockTexturesFromXMLCreateBlockTexturesPostfix
	{
		public static void Postfix(XmlFile _xmlFile)
		{
			ParseGrassConfig(_xmlFile.XmlDoc.Root);
		}
	}

	private static readonly List<TextureConfig> CustomGrass = new List<TextureConfig>();

	private static readonly TilingArea Tilings = new TilingArea();

	private static Dictionary<string, int> UvMap => OcbCustomTextures.UvMap;

	private static void ParseGrassConfig(XElement root)
	{
		Tilings.Clear();
		CustomGrass.Clear();
		foreach (XElement item2 in root.Elements())
		{
			if (!(item2.Name != "grass"))
			{
				DynamicProperties dynamicProperties = OcbCustomTextures.GetDynamicProperties(item2);
				TextureConfig item = new TextureConfig(item2, dynamicProperties);
				CustomGrass.Add(item);
			}
		}
		InitGrassConfig();
	}

	public static void InitGrassConfig()
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0196: Unknown result type (might be due to invalid IL or missing references)
		//IL_0156: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0277: Unknown result type (might be due to invalid IL or missing references)
		//IL_027e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0280: Unknown result type (might be due to invalid IL or missing references)
		if (CustomGrass.Count == 0)
		{
			return;
		}
		MeshDescription val = MeshDescription.meshes[3];
		foreach (XElement item in new XmlFile(val.MetaData).XmlDoc.Root.Elements())
		{
			if (!(item.Name != "uv"))
			{
				int num = int.Parse(XmlExtensions.GetAttribute(item, (XName)"id"));
				((UVRectTiling)(ref val.textureAtlas.uvMapping[num])).FromXML(item);
				UVRectTiling val2 = val.textureAtlas.uvMapping[num];
				val2.index = num;
				val.textureAtlas.uvMapping[num] = val2;
				Tilings.Add(new TilingAtlas(val2));
			}
		}
		foreach (TextureConfig item2 in CustomGrass)
		{
			if (item2.Diffuse.Assets.Length != 0)
			{
				UVRectTiling val3 = new UVRectTiling
				{
					uv = default(Rect),
					index = GetNextFreeUV(val.textureAtlas)
				};
				if (!UvMap.ContainsKey(item2.ID))
				{
					UvMap[item2.ID] = val3.index;
				}
				val3.index = UvMap[item2.ID];
				val3.textureName = item2.Diffuse.Assets[0];
				Tilings.Add(new TilingTexture(item2, val3));
			}
		}
		int width = Tilings.Width;
		int height = Tilings.Height;
		foreach (TilingSource item3 in Tilings.List)
		{
			UVRectTiling tiling = item3.Tiling;
			tiling.bGlobalUV = false;
			tiling.blockW = (tiling.blockH = 1);
			((Rect)(ref tiling.uv)).Set(((float)(item3.Dst.x * 580) + 34f) / (float)width, ((float)(item3.Dst.y * 580) + 34f) / (float)height, 512f / (float)width, 512f / (float)height);
			val.textureAtlas.uvMapping[tiling.index] = tiling;
		}
	}

	private static int GetNextFreeUV(TextureAtlas textureAtlas)
	{
		int num = textureAtlas.uvMapping.Length;
		Array.Resize(ref textureAtlas.uvMapping, num + 1);
		return num;
	}

	public static void ExecuteGrassPatching(MeshDescription grass)
	{
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Expected O, but got Unknown
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Expected O, but got Unknown
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Expected O, but got Unknown
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0223: Unknown result type (might be due to invalid IL or missing references)
		//IL_022a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0231: Unknown result type (might be due to invalid IL or missing references)
		//IL_0238: Unknown result type (might be due to invalid IL or missing references)
		//IL_0241: Unknown result type (might be due to invalid IL or missing references)
		//IL_0248: Unknown result type (might be due to invalid IL or missing references)
		//IL_024f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0256: Unknown result type (might be due to invalid IL or missing references)
		//IL_0268: Unknown result type (might be due to invalid IL or missing references)
		//IL_026f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0276: Unknown result type (might be due to invalid IL or missing references)
		//IL_027d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0286: Unknown result type (might be due to invalid IL or missing references)
		//IL_028d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0294: Unknown result type (might be due to invalid IL or missing references)
		//IL_029b: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_033e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0348: Unknown result type (might be due to invalid IL or missing references)
		//IL_0364: Unknown result type (might be due to invalid IL or missing references)
		//IL_036e: Unknown result type (might be due to invalid IL or missing references)
		//IL_038a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0394: Unknown result type (might be due to invalid IL or missing references)
		if (GameManager.IsDedicatedServer || Tilings.List.Count == 0)
		{
			return;
		}
		int width = Tilings.Width;
		int height = Tilings.Height;
		Texture2D val = new Texture2D(width, height, (TextureFormat)4, true, false);
		Texture2D val2 = new Texture2D(width, height, (TextureFormat)4, true, true);
		Texture2D val3 = new Texture2D(width, height, (TextureFormat)4, true, false);
		if (!((Object)grass.TexDiffuse).name.StartsWith("extended_"))
		{
			((Object)val).name = "extended_" + ((Object)grass.TexDiffuse).name;
		}
		if (!((Object)grass.TexNormal).name.StartsWith("extended_"))
		{
			((Object)val2).name = "extended_" + ((Object)grass.TexNormal).name;
		}
		if (!((Object)grass.TexSpecular).name.StartsWith("extended_"))
		{
			((Object)val3).name = "extended_" + ((Object)grass.TexSpecular).name;
		}
		Color[] pixels = val.GetPixels();
		for (int i = 0; i < pixels.Length; i++)
		{
			pixels[i] = Color.clear;
		}
		val.SetPixels(pixels);
		val2.SetPixels(pixels);
		val3.SetPixels(pixels);
		int idx;
		Texture obj = OcbTextureUtils.LoadTexture(OcbCustomTextures.GrassBundle, "ta_grass", out idx);
		Texture2D val4 = (Texture2D)(object)((obj is Texture2D) ? obj : null);
		Texture obj2 = OcbTextureUtils.LoadTexture(OcbCustomTextures.GrassBundle, "ta_grass_n", out idx);
		Texture2D val5 = (Texture2D)(object)((obj2 is Texture2D) ? obj2 : null);
		Texture obj3 = OcbTextureUtils.LoadTexture(OcbCustomTextures.GrassBundle, "ta_grass_s", out idx);
		Texture2D val6 = (Texture2D)(object)((obj3 is Texture2D) ? obj3 : null);
		Vector2i val7 = default(Vector2i);
		((Vector2i)(ref val7))._002Ector(576, 576);
		Vector2i val8 = default(Vector2i);
		Vector2i val9 = default(Vector2i);
		Vector2i val10 = default(Vector2i);
		for (int j = 0; j < Tilings.List.Count; j++)
		{
			TilingSource tilingSource = Tilings.List[j];
			if (tilingSource is TilingAtlas tilingAtlas)
			{
				((Vector2i)(ref val8))._002Ector(tilingSource.Dst.x * 580, tilingSource.Dst.y * 580);
				((Vector2i)(ref val9))._002Ector((int)((double)(((Rect)(ref tilingAtlas.Tiling.uv)).x * 4096f) - 34.5), (int)((double)(((Rect)(ref tilingAtlas.Tiling.uv)).y * 4096f) - 34.5));
				val.SetPixels(val8.x, val8.y, val7.x, val7.y, val4.GetPixels(val9.x, val9.y, val7.x, val7.y));
				val2.SetPixels(val8.x, val8.y, val7.x, val7.y, val5.GetPixels(val9.x, val9.y, val7.x, val7.y));
				val3.SetPixels(val8.x, val8.y, val7.x, val7.y, val6.GetPixels(val9.x, val9.y, val7.x, val7.y));
			}
			else if (tilingSource is TilingTexture tilingTexture)
			{
				((Vector2i)(ref val10))._002Ector(tilingSource.Dst.x * 580, tilingSource.Dst.y * 580);
				tilingTexture.Cfg.Diffuse.CopyTo(val, val10.x + 32, val10.y + 32);
				tilingTexture.Cfg.Normal.CopyTo(val2, val10.x + 32, val10.y + 32);
				tilingTexture.Cfg.Specular.CopyTo(val3, val10.x + 32, val10.y + 32);
			}
		}
		val.Apply(true);
		val2.Apply(true);
		val3.Apply(true);
		val.Compress(true);
		val2.Compress(true);
		val3.Compress(true);
		Log.Out("Unload custom atlas loaded for patching");
		OcbCustomTextures.ReleaseTexture((Texture)(object)val4, null, addressable: false);
		OcbCustomTextures.ReleaseTexture((Texture)(object)val5, null, addressable: false);
		OcbCustomTextures.ReleaseTexture((Texture)(object)val6, null, addressable: false);
		Log.Out("Unload original as replaced with patched atlas");
		OcbCustomTextures.ReleaseTexture(grass.TexDiffuse, (Texture)(object)val, addressable: false);
		OcbCustomTextures.ReleaseTexture(grass.TexNormal, (Texture)(object)val2, addressable: false);
		OcbCustomTextures.ReleaseTexture(grass.TexSpecular, (Texture)(object)val3, addressable: false);
		grass.TexDiffuse = (grass.textureAtlas.diffuseTexture = (Texture)(object)val);
		grass.TexNormal = (grass.textureAtlas.normalTexture = (Texture)(object)val2);
		grass.TexSpecular = (grass.textureAtlas.specularTexture = (Texture)(object)val3);
		grass.material.SetTexture("_Albedo", (Texture)(object)val);
		grass.material.SetTexture("_Normal", (Texture)(object)val2);
		grass.material.SetTexture("_Gloss_AO_SSS", (Texture)(object)val3);
	}
}
