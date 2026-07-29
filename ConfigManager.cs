using System;
using System.IO;
using System.Globalization;

namespace CTL472_OsuDriver
{
    public class DriverConfig
    {
        public double AreaWidth { get; set; }
        public double AreaHeight { get; set; }
        public double OffsetX { get; set; }
        public double OffsetY { get; set; }
        
        public bool Rotate180 { get; set; }
        public double RotationAngle { get; set; }
        public bool LeftHanded { get; set; }
        public bool LockAspectRatio { get; set; }
        public double AspectRatioValue { get; set; }
        
        public bool AbsoluteMode { get; set; }
        public bool Enable1000Hz { get; set; }
        public bool Force200Hz { get; set; }
        public bool EnableDriver { get; set; }
        public bool EnableTipClick { get; set; }
        public bool MinimizeToTray { get; set; }

        public DriverConfig()
        {
            AreaWidth = 96.0;
            AreaHeight = 54.0;
            OffsetX = 28.0;
            OffsetY = 20.5;
            Rotate180 = false;
            RotationAngle = 0.0;
            LeftHanded = false;
            LockAspectRatio = true;
            AspectRatioValue = 16.0 / 9.0;
            AbsoluteMode = true;
            Enable1000Hz = false;
            Force200Hz = true;
            EnableDriver = true;
            EnableTipClick = true;
            MinimizeToTray = false;
        }

        public static string ConfigPath
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string dir = Path.Combine(appData, "CTL472_OsuDriver");
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                return Path.Combine(dir, "config.ini");
            }
        }

        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(ConfigPath))
                {
                    writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "AreaWidth={0}", AreaWidth));
                    writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "AreaHeight={0}", AreaHeight));
                    writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "OffsetX={0}", OffsetX));
                    writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "OffsetY={0}", OffsetY));
                    writer.WriteLine("Rotate180=" + Rotate180);
                    writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "RotationAngle={0}", RotationAngle));
                    writer.WriteLine("LeftHanded=" + LeftHanded);
                    writer.WriteLine("LockAspectRatio=" + LockAspectRatio);
                    writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "AspectRatioValue={0}", AspectRatioValue));
                    writer.WriteLine("AbsoluteMode=" + AbsoluteMode);
                    writer.WriteLine("Enable1000Hz=" + Enable1000Hz);
                    writer.WriteLine("Force200Hz=" + Force200Hz);
                    writer.WriteLine("EnableDriver=" + EnableDriver);
                    writer.WriteLine("EnableTipClick=" + EnableTipClick);
                    writer.WriteLine("MinimizeToTray=" + MinimizeToTray);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error saving config: " + ex.Message);
            }
        }

        public static DriverConfig Load()
        {
            DriverConfig config = new DriverConfig();
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string[] lines = File.ReadAllLines(ConfigPath);
                    foreach (string line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
                        string[] parts = line.Split(new char[] { '=' }, 2);
                        if (parts.Length != 2) continue;

                        string key = parts[0].Trim();
                        string value = parts[1].Trim();

                        double dVal;
                        bool bVal;

                        switch (key)
                        {
                            case "AreaWidth":
                                if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out dVal)) config.AreaWidth = dVal;
                                break;
                            case "AreaHeight":
                                if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out dVal)) config.AreaHeight = dVal;
                                break;
                            case "OffsetX":
                                if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out dVal)) config.OffsetX = dVal;
                                break;
                            case "OffsetY":
                                if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out dVal)) config.OffsetY = dVal;
                                break;
                            case "Rotate180":
                                if (bool.TryParse(value, out bVal)) config.Rotate180 = bVal;
                                break;
                            case "RotationAngle":
                                if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out dVal)) config.RotationAngle = dVal;
                                break;
                            case "LeftHanded":
                                if (bool.TryParse(value, out bVal)) config.LeftHanded = bVal;
                                break;
                            case "LockAspectRatio":
                                if (bool.TryParse(value, out bVal)) config.LockAspectRatio = bVal;
                                break;
                            case "AbsoluteMode":
                                if (bool.TryParse(value, out bVal)) config.AbsoluteMode = bVal;
                                break;
                            case "Enable1000Hz":
                                if (bool.TryParse(value, out bVal)) config.Enable1000Hz = bVal;
                                break;
                            case "Force200Hz":
                                if (bool.TryParse(value, out bVal)) config.Force200Hz = bVal;
                                break;
                            case "AspectRatioValue":
                                if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out dVal)) config.AspectRatioValue = dVal;
                                break;
                            case "EnableDriver":
                                if (bool.TryParse(value, out bVal)) config.EnableDriver = bVal;
                                break;
                            case "EnableTipClick":
                                if (bool.TryParse(value, out bVal)) config.EnableTipClick = bVal;
                                break;
                            case "MinimizeToTray":
                                if (bool.TryParse(value, out bVal)) config.MinimizeToTray = bVal;
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error loading config: " + ex.Message);
            }
            return config;
        }
    }
}
