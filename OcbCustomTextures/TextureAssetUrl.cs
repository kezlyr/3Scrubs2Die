using System;
using System.IO;
using UnityEngine;

public class TextureAssetUrl
{
	public DataPathIdentifier Path;

	public string[] Assets;

	public TextureAssetUrl(string url)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		Path = DataLoader.ParseDataPathIdentifier(url);
		if (((DataPathIdentifier)(ref Path)).IsBundle)
		{
			AssetBundleManager.Instance.LoadAssetBundle(Path.BundlePath, false);
		}
		Assets = Path.AssetName.Split(',', StringSplitOptions.None);
	}

	public Texture2D LoadTexture2D()
	{
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Expected O, but got Unknown
		//IL_003e: Expected O, but got Unknown
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		if (!((DataPathIdentifier)(ref Path)).IsBundle)
		{
			byte[] array = File.ReadAllBytes(Assets[0]);
			Texture2D val = new Texture2D(2, 2);
			ImageConversion.LoadImage(val, array);
			return val;
		}
		int idx;
		Texture obj = OcbTextureUtils.LoadTexture(Path, out idx);
		return (Texture2D)(object)((obj is Texture2D) ? obj : null);
	}

	public void CopyTo(Texture2D dst, int x, int y)
	{
		Texture2D val = LoadTexture2D();
		if (!((Object)(object)val == (Object)null))
		{
			dst.SetPixels(x, y, ((Texture)val).width, ((Texture)val).height, val.GetPixels(0));
		}
	}
}
