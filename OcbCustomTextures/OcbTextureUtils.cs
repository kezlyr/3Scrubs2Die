using System;
using System.Text.RegularExpressions;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

public static class OcbTextureUtils
{
	public class ResetQualitySettings : IDisposable
	{
		private readonly int masterTextureLimit;

		private readonly int mipmapsReduction;

		private readonly bool streamingMips;

		private readonly float mipSlope = 0.6776996f;

		public ResetQualitySettings()
		{
			masterTextureLimit = QualitySettings.globalTextureMipmapLimit;
			streamingMips = QualitySettings.streamingMipmapsActive;
			mipmapsReduction = QualitySettings.streamingMipmapsMaxLevelReduction;
			mipSlope = Shader.GetGlobalFloat("_MipSlope");
			QualitySettings.globalTextureMipmapLimit = 0;
			QualitySettings.streamingMipmapsActive = false;
			QualitySettings.streamingMipmapsMaxLevelReduction = 0;
			Shader.SetGlobalFloat("_MipSlope", 0.6776996f);
		}

		public void Dispose()
		{
			QualitySettings.globalTextureMipmapLimit = masterTextureLimit;
			QualitySettings.streamingMipmapsActive = streamingMips;
			QualitySettings.streamingMipmapsMaxLevelReduction = mipmapsReduction;
			Shader.SetGlobalFloat("_MipSlope", mipSlope);
		}
	}

	public static Texture2D TextureFromGPU(Texture src, int idx, bool linear)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Expected O, but got Unknown
		if ((Object)(object)src == (Object)null)
		{
			return null;
		}
		GraphicsFormat val = (GraphicsFormat)4;
		using (new ResetQualitySettings())
		{
			RenderTexture temporary = RenderTexture.GetTemporary(src.width, src.height, 32, val);
			RenderTexture active = RenderTexture.active;
			Graphics.Blit(src, temporary, idx, 0);
			Texture2D val2 = new Texture2D(src.width, src.height, val, src.mipmapCount, (TextureCreationFlags)1);
			val2.ReadPixels(new Rect(0f, 0f, (float)src.width, (float)src.height), 0, 0, false);
			val2.Apply(true, false);
			RenderTexture.active = active;
			RenderTexture.ReleaseTemporary(temporary);
			return val2;
		}
	}

	public static Texture LoadTexture(string bundle, string asset, out int idx)
	{
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Expected O, but got Unknown
		//IL_0131: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		idx = 0;
		string pattern = "^[0-9.:]+$";
		if (asset.EndsWith("]"))
		{
			int num = asset.LastIndexOf("[");
			if (num != -1)
			{
				idx = int.Parse(asset.Substring(num + 1, asset.Length - num - 2));
				AssetBundleManager.Instance.LoadAssetBundle(bundle, false);
				return AssetBundleManager.Instance.Get<Texture>(bundle, asset.Substring(0, num), false);
			}
			throw new Exception("Missing `[` to match ending `]`");
		}
		if (!string.IsNullOrWhiteSpace(bundle))
		{
			AssetBundleManager.Instance.LoadAssetBundle(bundle, false);
			return AssetBundleManager.Instance.Get<Texture>(bundle, asset, false);
		}
		if (Regex.Match(asset, pattern).Success)
		{
			string[] array = asset.Split(':');
			if (array.Length >= 5 && array.Length <= 6)
			{
				int num2 = int.Parse(array[0]);
				int num3 = int.Parse(array[1]);
				bool flag = array.Length == 6;
				Color val = default(Color);
				((Color)(ref val))._002Ector(float.Parse(array[2]), float.Parse(array[3]), float.Parse(array[4]), flag ? float.Parse(array[5]) : 1f);
				Texture2D val2 = new Texture2D(num2, num3, (TextureFormat)(flag ? 4 : 3), true);
				Color[] pixels = val2.GetPixels();
				for (int i = 0; i < pixels.Length; i++)
				{
					pixels[i] = val;
				}
				val2.SetPixels(pixels);
				val2.Apply(true, false);
				val2.Compress(true);
				return (Texture)(object)val2;
			}
			Log.Error("Invalid on-demand texture {0}", new object[1] { asset });
		}
		else
		{
			Log.Error("Can't load texture from disk {0}", new object[1] { asset });
		}
		return null;
	}

	public static Texture LoadTexture(DataPathIdentifier path, out int idx)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		return LoadTexture(path.BundlePath, path.AssetName, out idx);
	}

	public static Texture2DArray ResizeTextureArray(CommandBuffer cmd, Texture2DArray array, int size, bool mipChain = false, bool linear = true, bool destroy = false)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Expected O, but got Unknown
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		if (array.depth >= size)
		{
			return array;
		}
		Texture2DArray val = new Texture2DArray(((Texture)array).width, ((Texture)array).height, size, ((Texture)array).graphicsFormat, (TextureCreationFlags)1, ((Texture)array).mipmapCount);
		if (!((Texture)array).isReadable)
		{
			val.Apply(false, true);
		}
		((Texture)val).filterMode = ((Texture)array).filterMode;
		if (!((Object)val).name.Contains("extended_"))
		{
			((Object)val).name = "extended_" + ((Object)array).name;
		}
		for (int i = 0; i < Mathf.Min(array.depth, size); i++)
		{
			cmd.CopyTexture(RenderTargetIdentifier.op_Implicit((Texture)(object)array), i, RenderTargetIdentifier.op_Implicit((Texture)(object)val), i);
		}
		if (destroy)
		{
			Object.Destroy((Object)(object)array);
		}
		return val;
	}

	private static int GetMipMapOffset()
	{
		int textureQuality = GameOptionsManager.GetTextureQuality(-1);
		if (textureQuality <= 2)
		{
			return textureQuality;
		}
		return 2;
	}

	public static NativeArray<byte> GetPixelData(Texture src, int idx, int mip = 0)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		Texture2DArray val = (Texture2DArray)(object)((src is Texture2DArray) ? src : null);
		if (val != null)
		{
			return val.GetPixelData<byte>(idx, mip);
		}
		Texture2D val2 = (Texture2D)(object)((src is Texture2D) ? src : null);
		if (val2 != null)
		{
			return val2.GetPixelData<byte>(mip);
		}
		throw new Exception("Ivalid texture type to get pixel data");
	}

	public static void SetPixelData(NativeArray<byte> pixels, Texture src, int idx, int mip = 0)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		Texture2DArray val = (Texture2DArray)(object)((src is Texture2DArray) ? src : null);
		if (val != null)
		{
			val.SetPixelData<byte>(pixels, mip, idx, 0);
			return;
		}
		Texture2D val2 = (Texture2D)(object)((src is Texture2D) ? src : null);
		if (val2 != null)
		{
			val2.SetPixelData<byte>(pixels, mip, 0);
		}
		else
		{
			Log.Error("Invalid texture type to set pixels");
		}
	}

	public static void ApplyPixelData(Texture src, bool updateMipmaps = true, bool makeNoLongerReadable = false)
	{
		Texture2DArray val = (Texture2DArray)(object)((src is Texture2DArray) ? src : null);
		if (val != null)
		{
			val.Apply(updateMipmaps, makeNoLongerReadable);
			return;
		}
		Texture2D val2 = (Texture2D)(object)((src is Texture2D) ? src : null);
		if (val2 != null)
		{
			val2.Apply(updateMipmaps, makeNoLongerReadable);
		}
		else
		{
			Log.Error("Invalid texture type to apply pixels");
		}
	}

	public static void PatchTexture(CommandBuffer cmds, Texture dst, int dstidx, Texture src, int srcidx = 0)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		int mipMapOffset = GetMipMapOffset();
		if (dst.isReadable && src.isReadable)
		{
			for (int i = 0; i < dst.mipmapCount; i++)
			{
				SetPixelData(GetPixelData(src, srcidx, mipMapOffset + i), dst, dstidx, i);
			}
			ApplyPixelData(dst, updateMipmaps: false);
		}
		else
		{
			for (int j = 0; j < dst.mipmapCount; j++)
			{
				cmds.CopyTexture(RenderTargetIdentifier.op_Implicit(src), srcidx, j + mipMapOffset, RenderTargetIdentifier.op_Implicit(dst), dstidx, j);
			}
		}
	}

	public static void PatchTexture(CommandBuffer cmds, Texture2DArray dst, int dstidx, string bundle, string asset)
	{
		int idx;
		Texture src = LoadTexture(bundle, asset, out idx);
		PatchTexture(cmds, (Texture)(object)dst, dstidx, src, idx);
	}

	public static void PatchTexture(CommandBuffer cmds, Texture2DArray dst, int dstidx, DataPathIdentifier path)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		int idx;
		Texture src = LoadTexture(path, out idx);
		PatchTexture(cmds, (Texture)(object)dst, dstidx, src, idx);
	}

	public static void PatchMicroSplatNormal(CommandBuffer cmds, Texture2DArray dst, int dstidx, DataPathIdentifier path, bool convert = false)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		int idx;
		Texture src = LoadTexture(path, out idx);
		if (convert)
		{
			Texture2D val = TextureFromGPU(src, idx, linear: true);
			for (int i = 0; i < ((Texture)val).mipmapCount; i++)
			{
				Color[] pixels = val.GetPixels(i);
				for (int j = 0; j < pixels.Length; j++)
				{
					ref float g = ref pixels[j].g;
					ref float a = ref pixels[j].a;
					float a2 = pixels[j].a;
					float g2 = pixels[j].g;
					g = a2;
					a = g2;
				}
				val.SetPixels(pixels, i);
			}
			val.Compress(true);
			val.Apply(false, false);
			PatchTexture(cmds, (Texture)(object)dst, dstidx, (Texture)(object)val);
		}
		else
		{
			PatchTexture(cmds, (Texture)(object)dst, dstidx, src, idx);
		}
	}
}
