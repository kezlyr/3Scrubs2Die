using System;
using System.Xml.Linq;
using UnityEngine;

public class TextureConfig
{
	public string ID;

	public string Name;

	public int PaintCost = 1;

	public int SortIndex = 255;

	public bool Hidden;

	public string Group;

	public DynamicProperties Props;

	public UVRectTiling tiling;

	public TextureAssetUrl Diffuse;

	public TextureAssetUrl Normal;

	public TextureAssetUrl Specular;

	public readonly int Length;

	public TextureConfig(XElement xml, DynamicProperties props)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0303: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0308: Unknown result type (might be due to invalid IL or missing references)
		Props = props;
		tiling = default(UVRectTiling);
		if (!XmlExtensions.HasAttribute(xml, (XName)"id"))
		{
			throw new Exception("Mandatory attribute `id` missing");
		}
		if (!props.Contains("Diffuse"))
		{
			throw new Exception("Mandatory property `Diffuse` missing");
		}
		ID = XmlExtensions.GetAttribute(xml, (XName)"id");
		Name = XmlExtensions.GetAttribute(xml, (XName)"name");
		props.ParseString("Group", ref Group);
		props.ParseInt("PaintCost", ref PaintCost);
		props.ParseBool("Hidden", ref Hidden);
		props.ParseInt("SortIndex", ref SortIndex);
		((Rect)(ref tiling.uv)).x = (XmlExtensions.HasAttribute(xml, (XName)"x") ? float.Parse(XmlExtensions.GetAttribute(xml, (XName)"x")) : 0f);
		((Rect)(ref tiling.uv)).y = (XmlExtensions.HasAttribute(xml, (XName)"y") ? float.Parse(XmlExtensions.GetAttribute(xml, (XName)"y")) : 0f);
		((Rect)(ref tiling.uv)).width = (XmlExtensions.HasAttribute(xml, (XName)"w") ? float.Parse(XmlExtensions.GetAttribute(xml, (XName)"w")) : 1f);
		((Rect)(ref tiling.uv)).height = (XmlExtensions.HasAttribute(xml, (XName)"h") ? float.Parse(XmlExtensions.GetAttribute(xml, (XName)"h")) : 1f);
		tiling.blockW = ((!XmlExtensions.HasAttribute(xml, (XName)"blockw")) ? 1 : int.Parse(XmlExtensions.GetAttribute(xml, (XName)"blockw")));
		tiling.blockH = ((!XmlExtensions.HasAttribute(xml, (XName)"blockh")) ? 1 : int.Parse(XmlExtensions.GetAttribute(xml, (XName)"blockh")));
		tiling.material = ((!props.Contains("Material")) ? null : MaterialBlock.fromString(props.GetString("Material")));
		tiling.bSwitchUV = props.Contains("SwitchUV") && props.GetBool("SwitchUV");
		tiling.bGlobalUV = props.Contains("GlobalUV") && props.GetBool("GlobalUV");
		tiling.textureName = (XmlExtensions.HasAttribute(xml, (XName)"name") ? XmlExtensions.GetAttribute(xml, (XName)"name") : ID);
		tiling.color = ((!props.Contains("Color")) ? Color.white : StringParsers.ParseColor(props.GetString("Color")));
		Diffuse = new TextureAssetUrl(props.Contains("Diffuse") ? props.GetString("Diffuse") : null);
		Length = Diffuse.Assets.Length;
		string text = (props.Contains("Normal") ? props.GetString("Normal") : null);
		if (text == null)
		{
			Normal = null;
		}
		else
		{
			Normal = new TextureAssetUrl(text);
		}
		string text2 = (props.Contains("Specular") ? props.GetString("Specular") : null);
		if (text2 == null)
		{
			Specular = null;
		}
		else
		{
			Specular = new TextureAssetUrl(text2);
		}
		if (Normal != null && Normal.Assets.Length != Length)
		{
			throw new Exception("Amount of normal maps different than diffuse maps!");
		}
		if (Specular != null && Specular.Assets.Length != Length)
		{
			throw new Exception("Amount of specular maps different than diffuse maps!");
		}
		tiling.index = -1;
	}
}
