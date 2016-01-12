using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace FezGame.Mod.Installer {
    public static class VersionHelper {
        
        public static List<Tuple<string, string>> GetStableVersions() {
            string data = null;
            using (WebClient wc = new WebClient()) {
                data = wc.DownloadString("http://fezmod.xyz/files/stable/");
            }
            
            string[] lines = data.Split('\n');
            
            //Stables are semi-ordered, ascending.
            List<Tuple<string, string>> versions = new List<Tuple<string, string>>();
            for (int i = 0; i < lines.Length; i++) {
                string v = lines[i].Trim();
                if (!v.StartsWith("<li><a ")) {
                    continue;
                }
                v = v.Substring(v.IndexOf("href=\"") + 6 + 7);
                v = v.Substring(0, v.IndexOf("\"") - 4);
                versions.Insert(0, Tuple.Create(v, "http://fezmod.xyz/files/stable/FEZMOD_" + v + ".zip"));
            }
            
            return versions;
        }
        
        public static List<Tuple<string, string>> GetNightlyVersions() {
            string data = null;
            using (WebClient wc = new WebClient()) {
                data = wc.DownloadString("http://fezmod.xyz/files/travis/jafm/");
            }
            
            string[] lines = data.Split('\n');
            
            //Nightlies are weirdly ordered - we need to sort them here.
            List<int> builds = new List<int>();
            for (int i = 0; i < lines.Length; i++) {
                string v = lines[i].Trim();
                if (!v.StartsWith("<li><a ")) {
                    continue;
                }
                v = v.Substring(v.IndexOf("href=\"") + 6);
                v = v.Substring(0, v.IndexOf("\"") - 1);
                builds.Add(int.Parse(v));
            }
            builds.Sort(delegate(int x, int y) {
                return y - x;
            });
            
            //Now we create the version-download-tuple list.
            List<Tuple<string, string>> versions = new List<Tuple<string, string>>(builds.Count);
            for (int i = 0; i < builds.Count; i++) {
                versions.Add(Tuple.Create(builds[i].ToString(), "http://fezmod.xyz/files/travis/jafm/" + builds[i] + "/JAFM-DEV.zip"));
            }
            
            return versions;
        }
        
    }
}