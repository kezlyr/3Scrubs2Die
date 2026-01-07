using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace ZZ_DiskStorage
{
    public class ModConfig
    {
        public int DiskCapacityT0 { get; set; } = 90;
        public int DiskCapacityT1 { get; set; } = 180;
        public int DiskCapacityT2 { get; set; } = 360;
        public int ReaderVisibleRows { get; set; } = 7;
        public int ReaderVisibleCols { get; set; } = 10;

        private static ModConfig _instance;
        public static ModConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    Load();
                }
                return _instance;
            }
        }

        public static void Load()
        {
            // Try to find the mod path
            string modPath = null;
            if (ModManager.GetMod("ZZ_DiskStorage") != null)
            {
                modPath = ModManager.GetMod("ZZ_DiskStorage").Path;
            }
            else
            {
                // Fallback
                modPath = Path.Combine(Application.dataPath, "..", "Mods", "ZZ_DiskStorage");
            }

            string configPath = Path.Combine(modPath, "Config.json");
            
            if (File.Exists(configPath))
            {
                try
                {
                    string json = File.ReadAllText(configPath);
                    _instance = JsonConvert.DeserializeObject<ModConfig>(json);
                    Log.Out($"[ZZ_DiskStorage] Loaded config from {configPath}");
                }
                catch (Exception ex)
                {
                    Log.Error($"[ZZ_DiskStorage] Failed to load config: {ex.Message}");
                    _instance = new ModConfig();
                }
            }
            else
            {
                Log.Warning($"[ZZ_DiskStorage] Config file not found at {configPath}, using defaults.");
                _instance = new ModConfig();
            }
        }
    }
}
