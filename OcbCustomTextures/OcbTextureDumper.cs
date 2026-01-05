using System;
using System.IO;
using UnityEngine;

public static class OcbTextureDumper
{
	public static void DumpTexure(string path, Texture src, int idx = 0, bool linear = true, Func<Color[], Color[]> converter = null)
	{
		if (!((Object)(object)src == (Object)null))
		{
			Texture2D val = OcbTextureUtils.TextureFromGPU(src, idx, linear);
			if (converter != null)
			{
				Color[] pixels = val.GetPixels(0);
				pixels = converter(pixels);
				val.SetPixels(pixels);
				val.Apply(true, false);
			}
			byte[] bytes = ImageConversion.EncodeToPNG(val);
			File.WriteAllBytes(path, bytes);
		}
	}

	public static void DumpTexure(string path, Texture2D src, bool linear = true, Func<Color[], Color[]> converter = null)
	{
		DumpTexure(path, (Texture)(object)src, 0, linear, converter);
	}

	public static Color[] ExtractRoughnessFromTexture(Color[] pixels)
	{
		for (int num = pixels.Length - 1; num >= 0; num--)
		{
			pixels[num].r = pixels[num].g;
			pixels[num].g = pixels[num].g;
			pixels[num].b = pixels[num].g;
			pixels[num].a = 1f;
		}
		return pixels;
	}

	public static Color[] ExtractAmbientOcclusionFromTexture(Color[] pixels)
	{
		for (int num = pixels.Length - 1; num >= 0; num--)
		{
			pixels[num].r = pixels[num].a;
			pixels[num].g = pixels[num].a;
			pixels[num].b = pixels[num].a;
			pixels[num].a = 1f;
		}
		return pixels;
	}

	public static Color[] ExtractHeightFromAlbedoTexture(Color[] pixels)
	{
		for (int num = pixels.Length - 1; num >= 0; num--)
		{
			pixels[num].r = pixels[num].a;
			pixels[num].g = pixels[num].a;
			pixels[num].b = pixels[num].a;
			pixels[num].a = 1f;
		}
		return pixels;
	}

	public static Color[] RemoveHeightFromAlbedoTexture(Color[] pixels)
	{
		for (int num = pixels.Length - 1; num >= 0; num--)
		{
			pixels[num].a = 1f;
		}
		return pixels;
	}

	public static Color[] UnpackNormalPixels(Color[] pixels)
	{
		for (int num = pixels.Length - 1; num >= 0; num--)
		{
			pixels[num].r = pixels[num].a;
			float num2 = pixels[num].r * 2f - 1f;
			float num3 = pixels[num].g * 2f - 1f;
			float num4 = Mathf.Sqrt(1f - num2 * num2 - num3 * num3);
			pixels[num].b = num4 * 0.5f + 0.5f;
			pixels[num].a = 1f;
		}
		return pixels;
	}

	public static Color[] UnpackNormalPixelsSwitched(Color[] pixels)
	{
		for (int num = pixels.Length - 1; num >= 0; num--)
		{
			ref float g = ref pixels[num].g;
			ref float a = ref pixels[num].a;
			float a2 = pixels[num].a;
			float g2 = pixels[num].g;
			g = a2;
			a = g2;
		}
		return UnpackNormalPixels(pixels);
	}

	public static Color[] ExtractRedChannel(Color[] pixels)
	{
		for (int num = pixels.Length - 1; num >= 0; num--)
		{
			pixels[num].r = 1f - pixels[num].r;
			pixels[num].g = pixels[num].r;
			pixels[num].b = pixels[num].r;
			pixels[num].a = 1f;
		}
		return pixels;
	}

	public static Color[] ExtractGreenChannel(Color[] pixels)
	{
		for (int num = pixels.Length - 1; num >= 0; num--)
		{
			pixels[num].g = 1f - pixels[num].g;
			pixels[num].r = pixels[num].g;
			pixels[num].b = pixels[num].g;
			pixels[num].a = 1f;
		}
		return pixels;
	}

	public static Color[] ExtractBlueChannel(Color[] pixels)
	{
		for (int num = pixels.Length - 1; num >= 0; num--)
		{
			pixels[num].b = 1f - pixels[num].b;
			pixels[num].r = pixels[num].b;
			pixels[num].g = pixels[num].b;
			pixels[num].a = 1f;
		}
		return pixels;
	}

	public static Color[] ExtractAlphaChannel(Color[] pixels)
	{
		for (int num = pixels.Length - 1; num >= 0; num--)
		{
			pixels[num].a = 1f - pixels[num].a;
			pixels[num].r = pixels[num].a;
			pixels[num].g = pixels[num].a;
			pixels[num].b = pixels[num].a;
			pixels[num].a = 1f;
		}
		return pixels;
	}
}
