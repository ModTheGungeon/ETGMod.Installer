using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Diagnostics;
using System.Windows.Forms;

namespace ETGModInstaller {
    public static class ETGInstallerSettings {

        public static string ConfigurationPath {
            get {
                string os = ETGFinder.GetPlatform().ToString().ToLower();
                if (os.Contains("win")) {
                    return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "etgmodconfig.txt");
                } else if (os.Contains("mac") || os.Contains("osx")) {
                    return Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".etgmodconfig.txt");
                } else if (os.Contains("lin") || os.Contains("unix")) {
                    string xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
                    if (string.IsNullOrWhiteSpace(xdg)) {
                        xdg = Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".config");
                    }
                    return Path.Combine(xdg, "", "etgmodconfig.txt");
                }
                return Path.Combine(".", "etgmodconfig.txt");
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
            string path = ConfigurationPath;
            if (!File.Exists(path)) {
                return;
            }
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++) {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) {
                    continue;
                }
                line = line.Trim();
                string[] data = line.Split(':');
                if (2 < data.Length) {
                    StringBuilder newData = new StringBuilder();
                    for (int ii = 1; ii < data.Length; ii++) {
                        newData.Append(data[ii]);
                        if (ii < data.Length - 1) {
                            newData.Append(':');
                        }
                    }
                    data = new string[] { data[0], newData.ToString() };
                }
                data[0] = data[0].Trim();
                data[1] = data[1].Trim();

                Action<string> d;
                if (data[1].Length != 0 && OnLoad.TryGetValue(data[0], out d)) {
                    d(data[1]);
                }
            }
        }

        public static void Save() {
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
                    writer.Write(nameGetPair.Key);
                    writer.Write(":");
                    writer.Write(value);
                    writer.WriteLine();
                }
            }
        }

    }
}
