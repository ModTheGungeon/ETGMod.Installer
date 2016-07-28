using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace ETGModInstaller {
    public static class RepoHelper {

        public static bool IsOffline = false;

        public static string ETGModURL = "http://modthegungeon.github.io/ETGMOD.zip";
        public static string ETGModRevisionURL = "http://modthegungeon.github.io/ETGMOD_REVISION.txt";
        public static string RevisionFile;

        public static int Revision {
            get {
                if (string.IsNullOrEmpty(RevisionFile) || !File.Exists(RevisionFile)) {
                    return int.MinValue;
                }

                try {
                    return int.Parse(File.ReadAllText(RevisionFile).Trim());
                } catch {
                    return int.MinValue;
                }
            }
            set {
                if (string.IsNullOrEmpty(RevisionFile)) {
                    return;
                }

                if (File.Exists(RevisionFile)) {
                    File.Delete(RevisionFile);
                }
                File.WriteAllText(RevisionFile, value.ToString());
            }
        }

        public static int RevisionOnline {
            get {
                if (IsOffline) {
                    return int.MaxValue;
                }
                try {
                    string data = null;
                    using (WebClient wc = new WebClient()) {
                        data = wc.DownloadString(ETGModRevisionURL);
                    }
                    return int.Parse(data.Trim());
                } catch {
                    return int.MinValue;
                }
            }
        }

        public static Action GetAPIModsCallback;
        public static List<Tuple<string, string>> GetAPIMods() {
            if (ETGModder.OverridePaths != null) {
                IsOffline = true;
            }
            if (IsOffline) {
                return new List<Tuple<string, string>>() {
                    Tuple.Create("OFFLINE MODE.", ""),
                    Tuple.Create("Deactivate in \"Advanced\" tab.", "")
                };
            }

            string data = null;
            using (WebClient wc = new WebClient()) {
                data = wc.DownloadString("http://modthegungeon.github.io/modlist.txt");
            }
            
            string[] lines = data.Split('\n');
            
            List<Tuple<string, string>> versions = new List<Tuple<string, string>>();
            versions.Add(Tuple.Create("Base", ""));
            for (int i = 0; i < lines.Length; i++) {
                if (string.IsNullOrEmpty(lines[i])) {
                    continue;
                }
                string[] split = lines[i].Trim().Split('|');
                versions.Add(Tuple.Create(split[0], split[1]));
            }

            GetAPIModsCallback?.Invoke();
            
            return versions;
        }

    }
}