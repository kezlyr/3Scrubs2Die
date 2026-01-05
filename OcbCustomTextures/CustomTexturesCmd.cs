using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;

public class CustomTexturesCmd : ConsoleCmdAbstract
{
	private static string info = "CustomTextures";

	public static Coroutine GrassHelperRunner = null;

	public override bool IsExecuteOnClient => true;

	public override bool AllowedInMainMenu => true;

	public override string[] getCommands()
	{
		return new string[2] { info, "ct" };
	}

	public override string getDescription()
	{
		return "Custom Textures";
	}

	public override string getHelp()
	{
		return "Custom Textures\n";
	}

	public static MeshDescription GetMesh(string name)
	{
		switch (name)
		{
		case "opaque":
			return MeshDescription.meshes[0];
		case "terrain":
			return MeshDescription.meshes[5];
		case "grass":
			return MeshDescription.meshes[3];
		case "models":
			return MeshDescription.meshes[0];
		case "transparent":
			return MeshDescription.meshes[2];
		case "water":
			return MeshDescription.meshes[1];
		case "decals":
			return MeshDescription.meshes[4];
		default:
			Log.Warning("Invalid mesh {0}", new object[1] { name });
			return null;
		}
	}

	private static void DumpMeshAtlas(MeshDescription mesh, string name)
	{
		string text = "export/" + name;
		TextureAtlas textureAtlas = mesh.textureAtlas;
		Directory.CreateDirectory(text);
		TextureAtlasTerrain val = (TextureAtlasTerrain)(object)((textureAtlas is TextureAtlasTerrain) ? textureAtlas : null);
		if (val != null)
		{
			for (int i = 0; i < val.diffuse.Length; i++)
			{
				OcbTextureDumper.DumpTexure($"{text}/terrain.{i}.diffuse.png", val.diffuse[i], linear: false);
			}
			for (int j = 0; j < val.normal.Length; j++)
			{
				OcbTextureDumper.DumpTexure($"{text}/terrain.{j}.normal.png", val.normal[j], linear: true, OcbTextureDumper.UnpackNormalPixels);
			}
			for (int k = 0; k < val.specular.Length; k++)
			{
				OcbTextureDumper.DumpTexure($"{text}/terrain.{k}.specular.png", val.specular[k]);
			}
		}
		Texture diffuseTexture = textureAtlas.diffuseTexture;
		Texture2DArray val2 = (Texture2DArray)(object)((diffuseTexture is Texture2DArray) ? diffuseTexture : null);
		if (val2 != null)
		{
			for (int l = 0; l < val2.depth; l++)
			{
				OcbTextureDumper.DumpTexure($"{text}/array.{l}.diffuse.png", (Texture)(object)val2, l);
			}
		}
		else
		{
			Texture diffuseTexture2 = textureAtlas.diffuseTexture;
			Texture2D val3 = (Texture2D)(object)((diffuseTexture2 is Texture2D) ? diffuseTexture2 : null);
			if (val3 != null)
			{
				OcbTextureDumper.DumpTexure($"{text}/atlas.diffuse.png", val3, linear: false);
			}
			else if ((Object)(object)textureAtlas.diffuseTexture != (Object)null)
			{
				Log.Warning("atlas.diffuseTexture has unknown type");
			}
		}
		Texture normalTexture = textureAtlas.normalTexture;
		Texture2DArray val4 = (Texture2DArray)(object)((normalTexture is Texture2DArray) ? normalTexture : null);
		if (val4 != null)
		{
			for (int m = 0; m < val4.depth; m++)
			{
				string path = $"{text}/array.{m}.normal.png";
				if (name == "opaque")
				{
					OcbTextureDumper.DumpTexure(path, (Texture)(object)val4, m, linear: true, OcbTextureDumper.UnpackNormalPixels);
				}
				else
				{
					OcbTextureDumper.DumpTexure(path, (Texture)(object)val4, m);
				}
			}
		}
		else
		{
			Texture normalTexture2 = textureAtlas.normalTexture;
			Texture2D val5 = (Texture2D)(object)((normalTexture2 is Texture2D) ? normalTexture2 : null);
			if (val5 != null)
			{
				OcbTextureDumper.DumpTexure($"{text}/atlas.normal.png", val5, linear: true, OcbTextureDumper.UnpackNormalPixels);
			}
			else if ((Object)(object)textureAtlas.normalTexture != (Object)null)
			{
				Log.Warning("atlas.normalTexture has unknown type");
			}
		}
		Texture specularTexture = textureAtlas.specularTexture;
		Texture2DArray val6 = (Texture2DArray)(object)((specularTexture is Texture2DArray) ? specularTexture : null);
		if (val6 != null)
		{
			for (int n = 0; n < val6.depth; n++)
			{
				if (name == "opaque")
				{
					OcbTextureDumper.DumpTexure($"{text}/array.{n}.metallic.png", (Texture)(object)val6, n, linear: true, OcbTextureDumper.ExtractRedChannel);
					OcbTextureDumper.DumpTexure($"{text}/array.{n}.occlusion.png", (Texture)(object)val6, n, linear: true, OcbTextureDumper.ExtractGreenChannel);
					OcbTextureDumper.DumpTexure($"{text}/array.{n}.emission.png", (Texture)(object)val6, n, linear: true, OcbTextureDumper.ExtractBlueChannel);
					OcbTextureDumper.DumpTexure($"{text}/array.{n}.roughness.png", (Texture)(object)val6, n, linear: true, OcbTextureDumper.ExtractAlphaChannel);
				}
				else
				{
					OcbTextureDumper.DumpTexure($"{text}/array.{n}.specular.png", (Texture)(object)val6, n);
				}
			}
		}
		else
		{
			Texture specularTexture2 = textureAtlas.specularTexture;
			Texture2D val7 = (Texture2D)(object)((specularTexture2 is Texture2D) ? specularTexture2 : null);
			if (val7 != null)
			{
				OcbTextureDumper.DumpTexure($"{text}/atlas.specular.png", val7);
			}
			else if ((Object)(object)textureAtlas.specularTexture != (Object)null)
			{
				Log.Warning("atlas.specularTexture has unknown type");
			}
		}
	}

	public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
	{
		if (_params.Count == 1 && _params[0] == "test")
		{
			return;
		}
		if (_params.Count == 2)
		{
			string text = _params[0];
			if (!(text == "dump"))
			{
				if (text == "uvs")
				{
					UVRectTiling[] uvMapping = GetMesh(_params[1]).textureAtlas.uvMapping;
					for (int i = 0; i < uvMapping.Length; i++)
					{
						if (!string.IsNullOrEmpty(uvMapping[i].textureName))
						{
							Log.Out("{0}: {1} {2}", new object[3]
							{
								i,
								uvMapping[i].textureName,
								((object)System.Runtime.CompilerServices.Unsafe.As<UVRectTiling, UVRectTiling>(ref uvMapping[i])/*cast due to .constrained prefix*/).ToString()
							});
						}
					}
					Log.Out("With maximum size of {0}", new object[1] { uvMapping.Length });
				}
				else
				{
					Log.Warning("Unknown command " + _params[0]);
				}
			}
			else
			{
				Directory.CreateDirectory("export");
				DumpMeshAtlas(GetMesh(_params[1]), _params[1]);
			}
		}
		else if (_params.Count == 4)
		{
			if (_params[0] == "grasshelper")
			{
				if (GrassHelperRunner == null)
				{
					Log.Out("Start Develop Watcher");
					GrassHelperRunner = ((MonoBehaviour)GameManager.Instance).StartCoroutine(HelperGrassTextures.StartGrassHelper(_params[1], int.Parse(_params[2]), int.Parse(_params[3])));
					return;
				}
				Log.Out("Stop Develop Watcher");
				Log.Out("Execute again to start");
				((MonoBehaviour)GameManager.Instance).StopCoroutine(GrassHelperRunner);
				GrassHelperRunner = null;
			}
			else
			{
				Log.Warning("Unknown command " + _params[0]);
			}
		}
		else
		{
			Log.Warning("Invalid `ct` command");
		}
	}
}
