public class TilingTexture : TilingSource
{
	public TextureConfig Cfg;

	public TilingTexture(TextureConfig cfg, UVRectTiling tiling)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		Cfg = cfg;
		Tiling = tiling;
	}

	public override string ToString()
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		return $"External texture {Cfg} to {Dst}";
	}
}
