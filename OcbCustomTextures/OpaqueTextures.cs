using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

public static class OpaqueTextures
{
	[HarmonyPatch(typeof(BlockTexturesFromXML), "CreateBlockTextures")]
	public class BlockTexturesFromXMLCreateBlockTexturesPrefix
	{
		public static void Prefix(XmlFile _xmlFile)
		{
			ParseOpaqueConfig(_xmlFile.XmlDoc.Root);
		}
	}

	[HarmonyPatch]
	private static class BlockTexturesFromXMLCreateBlockTexturesHook
	{
		private static IEnumerable<MethodBase> TargetMethods()
		{
			yield return AccessTools.EnumeratorMoveNext((MethodBase)AccessTools.Method(typeof(BlockTexturesFromXML), "CreateBlockTextures", (Type[])null, (Type[])null));
		}

		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			List<CodeInstruction> list = new List<CodeInstruction>(instructions);
			list.Insert(list.Count - 1, CodeInstruction.Call(typeof(OpaqueTextures), "InitOpaqueConfig", (Type[])null, (Type[])null));
			return list;
		}
	}

	[HarmonyPatch]
	private static class MeshDescriptionLoadSingleArray
	{
		private static IEnumerable<MethodBase> TargetMethods()
		{
			yield return AccessTools.EnumeratorMoveNext((MethodBase)AccessTools.Method(typeof(MeshDescription), "loadSingleArray", (Type[])null, (Type[])null));
		}

		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			List<CodeInstruction> list = new List<CodeInstruction>(instructions);
			for (int i = 0; i < list.Count; i++)
			{
				if (!(list[i].opcode != OpCodes.Stfld) && list[i].operand is FieldInfo fieldInfo)
				{
					if (fieldInfo.Name == "TexDiffuse")
					{
						list.Insert(i++, CodeInstruction.Call(typeof(OpaqueTextures), "PatchDiffuse", (Type[])null, (Type[])null));
					}
					else if (fieldInfo.Name == "TexNormal")
					{
						list.Insert(i++, CodeInstruction.Call(typeof(OpaqueTextures), "PatchNormal", (Type[])null, (Type[])null));
					}
					else if (fieldInfo.Name == "TexSpecular")
					{
						list.Insert(i++, CodeInstruction.Call(typeof(OpaqueTextures), "PatchSpecular", (Type[])null, (Type[])null));
					}
				}
			}
			return list;
		}
	}

	private static readonly Dictionary<string, TextureConfig> OpaqueConfigs = new Dictionary<string, TextureConfig>();

	private static int OpaquesAdded = 0;

	private static int builtinOpaques = -1;

	private static Dictionary<string, int> UvMap => OcbCustomTextures.UvMap;

	private static void ParseOpaqueConfig(XElement root)
	{
		UvMap.Clear();
		OpaquesAdded = 0;
		OpaqueConfigs.Clear();
		foreach (XElement item in root.Elements())
		{
			if (!(item.Name != "opaque"))
			{
				DynamicProperties dynamicProperties = OcbCustomTextures.GetDynamicProperties(item);
				TextureConfig textureConfig = new TextureConfig(item, dynamicProperties);
				OpaqueConfigs[textureConfig.ID] = textureConfig;
			}
		}
	}

	private static int GetFreePaintID()
	{
		for (int i = 0; i < BlockTextureData.list.Length; i++)
		{
			if (BlockTextureData.list[i] == null)
			{
				return i;
			}
		}
		throw new Exception("No more free Paint IDs");
	}

	private static ushort PatchAtlasBlocks(MeshDescription mesh, TextureConfig tex)
	{
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		if (mesh == null)
		{
			throw new Exception("MESH MISSING");
		}
		TextureAtlas textureAtlas = mesh.textureAtlas;
		TextureAtlasBlocks val = (TextureAtlasBlocks)(object)((textureAtlas is TextureAtlasBlocks) ? textureAtlas : null);
		if (val == null)
		{
			throw new Exception("INVALID ATLAS TYPE");
		}
		if (((TextureAtlas)val).uvMapping.Length > 65535)
		{
			throw new Exception("INVALID ATLAS SIZE");
		}
		ushort num = (ushort)((TextureAtlas)val).uvMapping.Length;
		if (!UvMap.ContainsKey(tex.ID))
		{
			UvMap[tex.ID] = num;
		}
		else if (UvMap[tex.ID] != num)
		{
			Log.Warning("Overwriting texture key {0}", new object[1] { tex.ID });
		}
		if (((TextureAtlas)val).uvMapping.Length < num + 1)
		{
			Array.Resize(ref ((TextureAtlas)val).uvMapping, num + 1);
		}
		((TextureAtlas)val).uvMapping[num] = tex.tiling;
		return num;
	}

	private static void InitOpaqueConfig()
	{
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		MeshDescription val = MeshDescription.meshes[0];
		TextureAtlas textureAtlas = val.textureAtlas;
		TextureAtlasBlocks val2 = (TextureAtlasBlocks)(object)((textureAtlas is TextureAtlasBlocks) ? textureAtlas : null);
		if (builtinOpaques == -1 && (Object)(object)((TextureAtlas)val2).diffuseTexture != (Object)null)
		{
			Texture diffuseTexture = ((TextureAtlas)val2).diffuseTexture;
			builtinOpaques = ((Texture2DArray)((diffuseTexture is Texture2DArray) ? diffuseTexture : null)).depth;
		}
		List<TextureConfig> list = OpaqueConfigs.Values.ToList();
		if (val == null)
		{
			throw new Exception("MESH MISSING");
		}
		TextureAtlas textureAtlas2 = val.textureAtlas;
		TextureAtlasBlocks val3 = (TextureAtlasBlocks)(object)((textureAtlas2 is TextureAtlasBlocks) ? textureAtlas2 : null);
		if (val3 == null)
		{
			throw new Exception("INVALID ATLAS TYPE");
		}
		for (int i = 0; i < list.Count; i++)
		{
			TextureConfig textureConfig = list[i];
			if (ushort.TryParse(textureConfig.ID, out var result))
			{
				textureConfig.tiling.index = ((TextureAtlas)val3).uvMapping[result].index;
				continue;
			}
			textureConfig.tiling.index = builtinOpaques + OpaquesAdded;
			BlockTextureData val4 = new BlockTextureData
			{
				Name = textureConfig.Name,
				Group = textureConfig.Group,
				Hidden = textureConfig.Hidden,
				SortIndex = (byte)textureConfig.SortIndex,
				PaintCost = (ushort)textureConfig.PaintCost,
				TextureID = PatchAtlasBlocks(val, textureConfig),
				LocalizedName = Localization.Get(textureConfig.Name, false),
				ID = GetFreePaintID()
			};
			OpaquesAdded += textureConfig.Length;
			val4.Init();
		}
		PatchCustomOpaques();
	}

	private static void PatchCustomOpaques()
	{
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Expected O, but got Unknown
		if (OpaquesAdded != 0 && !GameManager.IsDedicatedServer)
		{
			MeshDescription val = MeshDescription.meshes[0];
			if (val == null)
			{
				throw new Exception("MESH MISSING");
			}
			TextureAtlas textureAtlas = val.textureAtlas;
			TextureAtlasBlocks val2 = (TextureAtlasBlocks)(object)((textureAtlas is TextureAtlasBlocks) ? textureAtlas : null);
			if (val2 == null)
			{
				throw new Exception("INVALID ATLAS TYPE");
			}
			Texture diffuseTexture = ((TextureAtlas)val2).diffuseTexture;
			Texture2DArray val3 = (Texture2DArray)(object)((diffuseTexture is Texture2DArray) ? diffuseTexture : null);
			if (val3 == null)
			{
				throw new Exception("Diffuse not a texture2Darray!");
			}
			Texture normalTexture = ((TextureAtlas)val2).normalTexture;
			Texture2DArray val4 = (Texture2DArray)(object)((normalTexture is Texture2DArray) ? normalTexture : null);
			if (val4 == null)
			{
				throw new Exception("Normal not a texture2Darray!");
			}
			Texture specularTexture = ((TextureAtlas)val2).specularTexture;
			Texture2DArray val5 = (Texture2DArray)(object)((specularTexture is Texture2DArray) ? specularTexture : null);
			if (val5 == null)
			{
				throw new Exception("Specular not a texture2Darray!");
			}
			CommandBuffer val6 = new CommandBuffer();
			val6.SetExecutionFlags((CommandBufferExecutionFlags)2);
			Texture2DArray val7 = ApplyDiffuseTextures(val3, val6);
			Texture2DArray val8 = ApplyNormalTextures(val4, val6);
			Texture2DArray val9 = ApplySpecularTexture(val5, val6);
			int globalTextureMipmapLimit = QualitySettings.globalTextureMipmapLimit;
			QualitySettings.globalTextureMipmapLimit = 0;
			Graphics.ExecuteCommandBufferAsync(val6, (ComputeQueueType)0);
			QualitySettings.globalTextureMipmapLimit = globalTextureMipmapLimit;
			OcbCustomTextures.ReleaseTexture((Texture)(object)val3, (Texture)(object)val7);
			val.TexDiffuse = (((TextureAtlas)val2).diffuseTexture = (Texture)(object)val7);
			Log.Out("Set Opaque diffuse: {0}", new object[1] { val.TexDiffuse });
			OcbCustomTextures.ReleaseTexture((Texture)(object)val4, (Texture)(object)val8);
			val.TexNormal = (((TextureAtlas)val2).normalTexture = (Texture)(object)val8);
			Log.Out("Set Opaque normal: {0}", new object[1] { val.TexNormal });
			OcbCustomTextures.ReleaseTexture((Texture)(object)val5, (Texture)(object)val9);
			val.TexSpecular = (((TextureAtlas)val2).specularTexture = (Texture)(object)val9);
			Log.Out("Set Opaque MOER: {0}", new object[1] { val.TexSpecular });
			val.ReloadTextureArrays(false);
		}
	}

	private static Texture2DArray PatchDiffuse(Texture2DArray texture)
	{
		return PatchTextureAtlas(texture, ApplyDiffuseTextures);
	}

	private static Texture2DArray PatchNormal(Texture2DArray texture)
	{
		return PatchTextureAtlas(texture, ApplyNormalTextures);
	}

	private static Texture2DArray PatchSpecular(Texture2DArray texture)
	{
		return PatchTextureAtlas(texture, ApplySpecularTexture);
	}

	private static Texture2DArray PatchTextureAtlas(Texture2DArray texture, Func<Texture2DArray, CommandBuffer, Texture2DArray> apply)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Expected O, but got Unknown
		if (OpaquesAdded == 0)
		{
			return texture;
		}
		if (GameManager.IsDedicatedServer)
		{
			return texture;
		}
		if (!((Object)texture).name.StartsWith("ta_opaque"))
		{
			return texture;
		}
		using (new OcbTextureUtils.ResetQualitySettings())
		{
			CommandBuffer val = new CommandBuffer();
			val.SetExecutionFlags((CommandBufferExecutionFlags)2);
			Texture2DArray val2 = apply(texture, val);
			Graphics.ExecuteCommandBufferAsync(val, (ComputeQueueType)0);
			OcbCustomTextures.ReleaseTexture((Texture)(object)texture, (Texture)(object)val2);
			Log.Out("Patched: {0}", new object[1] { val2 });
			return val2;
		}
	}

	private static Texture2DArray ApplyDiffuseTextures(Texture2DArray texture, CommandBuffer cmds)
	{
		return ApplyTextures(texture, cmds, (TextureConfig x) => x.Diffuse, "assets/defaultdiffuse.png");
	}

	private static Texture2DArray ApplyNormalTextures(Texture2DArray texture, CommandBuffer cmds)
	{
		return ApplyTextures(texture, cmds, (TextureConfig x) => x.Normal, "assets/defaultnormal.png");
	}

	private static Texture2DArray ApplySpecularTexture(Texture2DArray texture, CommandBuffer cmds)
	{
		return ApplyTextures(texture, cmds, (TextureConfig x) => x.Specular, "assets/defaultspecular.png");
	}

	private static Texture2DArray ApplyTextures(Texture2DArray texture, CommandBuffer cmds, Func<TextureConfig, TextureAssetUrl> lookup, string fallback)
	{
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		if (OpaquesAdded == 0)
		{
			return texture;
		}
		if (GameManager.IsDedicatedServer)
		{
			return texture;
		}
		if (!((Object)texture).name.StartsWith("ta_opaque"))
		{
			return texture;
		}
		Texture2DArray val = OcbTextureUtils.ResizeTextureArray(cmds, texture, texture.depth + OpaquesAdded, mipChain: true);
		foreach (TextureConfig value in OpaqueConfigs.Values)
		{
			for (int i = 0; i < value.Length; i++)
			{
				PatchTextures(cmds, val, lookup(value), value.tiling, i, fallback);
			}
		}
		return val;
	}

	private static void PatchTextures(CommandBuffer cmds, Texture2DArray copy, TextureAssetUrl src, UVRectTiling tiling, int i, string fallback)
	{
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		if (src != null || !string.IsNullOrEmpty(fallback))
		{
			int idx;
			Texture src2 = ((src != null) ? OcbTextureUtils.LoadTexture(src.Path.BundlePath, src.Assets[i], out idx) : OcbTextureUtils.LoadTexture(OcbCustomTextures.CommonBundle, fallback, out idx));
			OcbTextureUtils.PatchTexture(cmds, (Texture)(object)copy, tiling.index + i, src2, idx);
		}
	}
}
