using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Diagnostics;
using System.Windows.Forms;
using System.Text.RegularExpressions;

namespace ETGModInstaller {
    public static class ETGInstallerSettings {

        public static bool Enabled = true;
        public static bool LoadEnabled = true;
        public static bool SaveEnabled = true;

		public const string CONFIG_NAME = "modthegungeon.conf";

        public static string ConfigurationPath {
            get {
                string home = null;
                if (ETGFinder.Platform.HasFlag(ETGPlatform.Linux) || ETGFinder.Platform.HasFlag(ETGPlatform.MacOS)) {
                   home = Environment.GetEnvironmentVariable("HOME");
                }

                if (ETGFinder.Platform.HasFlag(ETGPlatform.Windows)) {
                    return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), CONFIG_NAME);
                } else if (ETGFinder.Platform.HasFlag(ETGPlatform.MacOS)) {
                    string path = Path.Combine(home, $".{CONFIG_NAME}");
                    if (Directory.Exists(Path.Combine(home, "Library", "Application Support"))) {
                        string dir = Path.Combine(home, "Library", "Application Support", "ETGMod");
                        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                        path = Path.Combine(dir, CONFIG_NAME);
                    }
                    return path;
                } else if (ETGFinder.Platform.HasFlag(ETGPlatform.Linux)) {
                    string xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
                    string path = null;
                    if (xdg == null) {
                        if (Directory.Exists(Path.Combine(home, ".config"))) {
                            path = Path.Combine(home, ".config", CONFIG_NAME);
                        } else {
                            path = Path.Combine(home, $".{CONFIG_NAME}");
                        }
                    } else {
                        path = Path.Combine(xdg, CONFIG_NAME);
                    }
                    return path;
                }
                return Path.Combine(".", CONFIG_NAME);
            }
        }

        public static Dictionary<string, Action<string>> OnLoad = new Dictionary<string, Action<string>>();
        public static Dictionary<string, Func<string>> OnSave = new Dictionary<string, Func<string>>();

        static ETGInstallerSettings() {
            OnLoad["exe"] = (s) => InstallerWindow.Instance.ExeLoaded(s);
            OnSave["exe"] = () => InstallerWindow.Instance.ExePathBox.Text;

            OnLoad["autorun"] = (s) => InstallerWindow.Instance.AdvancedAutoRunCheckbox.Checked = bool.Parse(s);
            OnSave["autorun"] = () => InstallerWindow.Instance.AdvancedAutoRunCheckbox.Checked.ToString();

            OnLoad["advanced"] = (s) => InstallerWindow.Instance.SetAdvanced(s.Split(';'));
            OnSave["advanced"] = () => InstallerWindow.Instance.GetAdvanced(';');

            OnLoad["offline"] = (s) => InstallerWindow.Instance.AdvancedOfflineCheckbox.Checked = bool.Parse(s);
            OnSave["offline"] = () => InstallerWindow.Instance.AdvancedOfflineCheckbox.Checked.ToString();

            OnLoad["binarywrapped"] = (s) => InstallerWindow.Instance.AdvancedBinaryWrappedCheckbox.Checked = bool.Parse(s);
            OnSave["binarywrapped"] = () => InstallerWindow.Instance.AdvancedBinaryWrappedCheckbox.Checked.ToString();

            OnLoad["etgversion"] = (s) => ETGFinder.VersionLastRun = s;
            OnSave["etgversion"] = () => ETGFinder.Version;

            OnLoad["showlogoninstall"] = (s) => InstallerWindow.Instance.AdvancedShowLogOnInstallCheckbox.Checked = bool.Parse(s);
            OnSave["showlogoninstall"] = () => InstallerWindow.Instance.AdvancedShowLogOnInstallCheckbox.Checked.ToString();
        }

        public static void SetAdvanced(this InstallerWindow ins, string[] paths) {
            while (0 < ins.AdvancedPathBoxes.Count) {
                ins.AdvancedPanel.Controls.Remove(ins.AdvancedPathBoxes[0]);
                ins.AdvancedPathBoxes.RemoveAt(0);
            }
            while (0 < ins.AdvancedRemoveButtons.Count) {
                ins.AdvancedPanel.Controls.Remove(ins.AdvancedRemoveButtons[0]);
                ins.AdvancedRemoveButtons.RemoveAt(0);
            }

            ins.AddManualPathRows(paths);
        }
        public static string GetAdvanced(this InstallerWindow ins, char split) {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < ins.AdvancedPathBoxes.Count; i++) {
                sb.Append(ins.AdvancedPathBoxes[i].Text);
                if (i < ins.AdvancedPathBoxes.Count - 1) {
                    sb.Append(split);
                }
            }
            return sb.ToString();
        }

        public static void Load() {
            if (!Enabled || !LoadEnabled) {
                return;
            }
            string path = ConfigurationPath;
            if (!File.Exists(path)) {
                return;
            }
            var parsed = Parser.Parse(File.ReadAllText(path));
            foreach (KeyValuePair<string, string> kv in parsed) {
                Action<string> d;
                if (OnLoad.TryGetValue(kv.Key, out d)) {
                    d(kv.Value);
                }
            }
        }

        public static void Save() {
            if (!Enabled || !SaveEnabled) {
                return;
            }
            string path = ConfigurationPath;
            if (File.Exists(path)) {
                File.Delete(path);
            }
            using (StreamWriter writer = new StreamWriter(path)) {
                foreach (KeyValuePair<string, Func<string>> nameGetPair in OnSave) {
                    string value = nameGetPair.Value();
                    if (string.IsNullOrWhiteSpace(value)) {
                        continue;
                    }
                    string key = nameGetPair.Key;
                    writer.WriteLine(Parser.CreateEntry(key, value));
                }
            }
        }

    }
}
