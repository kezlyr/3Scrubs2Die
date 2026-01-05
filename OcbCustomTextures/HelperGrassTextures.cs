using System;
using System.Collections;
using System.IO;
using UnityEngine;

public class HelperGrassTextures
{
	private static DateTime mt1;

	private static DateTime mt2;

	private static DateTime mt3;

	public static IEnumerator StartGrassHelper(string path, int x, int y)
	{
		while (true)
		{
			DynamicGrassPatcher(path, x, y);
			yield return (object)new WaitForSeconds(1f);
		}
	}

	private static bool IsSimilar(Color t1, Color t2)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		float num = 1f / 15f;
		if (Mathf.Abs(t1.r - t2.r) > num)
		{
			return false;
		}
		if (Mathf.Abs(t1.g - t2.g) > num)
		{
			return false;
		}
		if (Mathf.Abs(t1.b - t2.b) > num)
		{
			return false;
		}
		if (Mathf.Abs(t1.a - t2.a) > num)
		{
			return false;
		}
		return true;
	}

	public static Texture2D LoadTexture(string path)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Expected O, but got Unknown
		//IL_0017: Expected O, but got Unknown
		byte[] array = File.ReadAllBytes(path);
		Texture2D val = new Texture2D(2, 2);
		ImageConversion.LoadImage(val, array);
		return val;
	}

	public static void DynamicGrassPatcher(string path, int x, int y)
	{
		//IL_0259: Unknown result type (might be due to invalid IL or missing references)
		//IL_0267: Unknown result type (might be due to invalid IL or missing references)
		//IL_026c: Unknown result type (might be due to invalid IL or missing references)
		//IL_026e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0367: Unknown result type (might be due to invalid IL or missing references)
		//IL_0375: Unknown result type (might be due to invalid IL or missing references)
		//IL_037a: Unknown result type (might be due to invalid IL or missing references)
		//IL_037c: Unknown result type (might be due to invalid IL or missing references)
		MeshDescription val = MeshDescription.meshes[3];
		Texture texDiffuse = val.TexDiffuse;
		Texture2D val2 = (Texture2D)(object)((texDiffuse is Texture2D) ? texDiffuse : null);
		Texture texNormal = val.TexNormal;
		Texture2D val3 = (Texture2D)(object)((texNormal is Texture2D) ? texNormal : null);
		Texture texSpecular = val.TexSpecular;
		Texture2D val4 = (Texture2D)(object)((texSpecular is Texture2D) ? texSpecular : null);
		DateTime lastWriteTime = File.GetLastWriteTime(path + ".albedo.png");
		DateTime lastWriteTime2 = File.GetLastWriteTime(path + ".normal.png");
		DateTime lastWriteTime3 = File.GetLastWriteTime(path + ".aost.png");
		x = 580 * x + 34;
		y = 580 * y + 34;
		if (mt1 == lastWriteTime && mt2 == lastWriteTime2 && mt3 == lastWriteTime3)
		{
			return;
		}
		Log.Out("Reloading {0}", new object[1] { path });
		mt1 = lastWriteTime;
		mt2 = lastWriteTime2;
		mt3 = lastWriteTime3;
		bool num = mt1 != lastWriteTime;
		bool flag = mt2 != lastWriteTime2;
		bool flag2 = mt3 != lastWriteTime3;
		bool flag3 = true;
		if (num || flag3)
		{
			Log.Out("Reloading Albedo");
			Texture2D val5 = LoadTexture(path + ".albedo.png");
			for (int i = 0; i < ((Texture)val5).mipmapCount; i++)
			{
				int num2 = (int)Mathf.Pow(2f, (float)i);
				Graphics.CopyTexture((Texture)(object)val5, 0, i, 0, 0, ((Texture)val5).width / num2, ((Texture)val5).height / num2, (Texture)(object)val2, 0, i, x / num2, y / num2);
			}
			val.TexDiffuse = (Texture)(object)val2;
			val.textureAtlas.diffuseTexture = (Texture)(object)val2;
		}
		if (flag || flag3)
		{
			Log.Out("Reloading Normal");
			Texture2D val6 = LoadTexture(path + ".normal.png");
			((Texture)val6).filterMode = (FilterMode)2;
			Color32[] pixels = val6.GetPixels32();
			for (int j = 0; j < pixels.Length; j++)
			{
				byte r = pixels[j].r;
				byte g = pixels[j].g;
				g = (byte)Linear2Gamma(g);
				pixels[j].a = r;
				pixels[j].g = g;
				pixels[j].b = g;
				pixels[j].r = byte.MaxValue;
			}
			val6.SetPixels32(pixels);
			val6.Apply();
			for (int k = 0; k < ((Texture)val6).width; k++)
			{
				for (int l = 0; l < ((Texture)val6).height; l++)
				{
					Color pixel = val6.GetPixel(k, l);
					Color pixel2 = val3.GetPixel(x + k, y + l);
					IsSimilar(pixel, pixel2);
				}
			}
			for (int m = 0; m < ((Texture)val6).mipmapCount; m++)
			{
				int num3 = (int)Mathf.Pow(2f, (float)m);
				Graphics.CopyTexture((Texture)(object)val6, 0, m, 0, 0, ((Texture)val6).width / num3, ((Texture)val6).height / num3, (Texture)(object)val3, 0, m, x / num3, y / num3);
			}
			val.TexNormal = (Texture)(object)val3;
			val.textureAtlas.normalTexture = (Texture)(object)val3;
		}
		if (flag2 || flag3)
		{
			Log.Out("Reloading Specular");
			Texture2D val7 = LoadTexture(path + ".aost.png");
			((Texture)val7).filterMode = (FilterMode)2;
			Color32[] pixels2 = val7.GetPixels32();
			for (int n = 0; n < pixels2.Length; n++)
			{
			}
			val7.SetPixels32(pixels2);
			val7.Apply();
			for (int num4 = 0; num4 < ((Texture)val7).width; num4++)
			{
				for (int num5 = 0; num5 < ((Texture)val7).height; num5++)
				{
					Color pixel3 = val7.GetPixel(num4, num5);
					Color pixel4 = val4.GetPixel(x + num4, y + num5);
					IsSimilar(pixel3, pixel4);
				}
			}
			for (int num6 = 0; num6 < ((Texture)val7).mipmapCount; num6++)
			{
				int num7 = (int)Mathf.Pow(2f, (float)num6);
				Graphics.CopyTexture((Texture)(object)val7, 0, num6, 0, 0, ((Texture)val7).width / num7, ((Texture)val7).height / num7, (Texture)(object)val4, 0, num6, x / num7, y / num7);
			}
			val.TexSpecular = (Texture)(object)val4;
			val.textureAtlas.specularTexture = (Texture)(object)val4;
		}
		val.material.SetTexture("_Albedo", val.textureAtlas.diffuseTexture);
		val.material.SetTexture("_Normal", val.textureAtlas.normalTexture);
		val.material.SetTexture("_Gloss_AO_SSS", val.textureAtlas.specularTexture);
	}

	private static float Linear2Gamma(byte g)
	{
		return 255f * Mathf.Pow(((float)(int)g + 0.5f) / 255f, 0.45454544f);
	}

	private static float Gamma2Linear(byte g)
	{
		return 255f * Mathf.Pow(((float)(int)g + 0.5f) / 255f, 2.2f);
	}
}
