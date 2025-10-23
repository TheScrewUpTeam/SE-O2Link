using System;
using Sandbox.ModAPI;
using VRage.Utils;

namespace TSUT.O2Link
{
    public class Config
    {
        public static string Version = "1.0.0";

        public string SYSTEM_VERSION = "1.0.0";
        public bool SYSTEM_AUTO_UPDATE = true;
        public float O2_FROM_H2_RATIO = 0.5f; // Amount of O2 required per unit of H2 consumed
        private static Config _instance;
        private const string CONFIG_FILE = "TSUT_O2Link_Config.xml";

        public static Config Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Load();
                return _instance;
            }
        }

        public static Config Load()
        {
            Config config = new Config();
            if (MyAPIGateway.Utilities.FileExistsInWorldStorage(CONFIG_FILE, typeof(Config)))
            {
                try
                {
                    string contents;
                    using (var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(CONFIG_FILE, typeof(Config)))
                    {
                        contents = reader.ReadToEnd();
                    }

                    // Check if version exists in the XML before deserializing
                    bool hasVersion = contents.Contains("<SYSTEM_VERSION>");

                    config = MyAPIGateway.Utilities.SerializeFromXML<Config>(contents);

                    var defaultConfig = new Config();

                    var configUpdateNeeded = !hasVersion || config.SYSTEM_AUTO_UPDATE && config.SYSTEM_VERSION != defaultConfig.SYSTEM_VERSION;

                    MyLog.Default.WriteLine($"[O2Link] AutoUpdate: {config.SYSTEM_AUTO_UPDATE}, VersionMatches: {hasVersion && config.SYSTEM_VERSION == defaultConfig.SYSTEM_VERSION}, UpdateNeeded: {configUpdateNeeded}");

                    // Check version and auto-update if needed
                    if (configUpdateNeeded)
                    {
                        MyAPIGateway.Utilities.ShowMessage("O2Link", $"Config version mismatch. Auto-updating from {(hasVersion ? config.SYSTEM_VERSION : "Unknown")} to {defaultConfig.SYSTEM_VERSION}");
                        // Keep auto-update setting but reset everything else to defaults
                        bool autoUpdate = config.SYSTEM_AUTO_UPDATE;
                        config = new Config();
                        config.SYSTEM_AUTO_UPDATE = autoUpdate;
                        return config;
                    }
                }
                catch (Exception e)
                {
                    MyAPIGateway.Utilities.ShowMessage("O2Link", $"Failed to load config, using defaults. {e.Message}");
                }
            }

            return config;
        }

        public void Save()
        {
            try
            {
                using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(CONFIG_FILE, typeof(Config)))
                {
                    writer.Write(MyAPIGateway.Utilities.SerializeToXML(this));
                }
            }
            catch (Exception e)
            {
                MyLog.Default.Warning("O2Link", $"Failed to save config: {e.Message}");
            }
        }
    }
}