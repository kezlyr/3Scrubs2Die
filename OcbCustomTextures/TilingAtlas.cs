using UnityEngine;

public class TilingAtlas : TilingSource
{
	public Texture2D Atlas;

	public Vector2i Pos;

	public Vector2i Size;

	public TilingAtlas(UVRectTiling tiling)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		Tiling = tiling;
	}

	public override string ToString()
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		return $"Atlas {Atlas} at {Pos} ({Size}) to {Dst}";
	}
}
