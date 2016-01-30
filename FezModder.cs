using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Globalization;
using System.IO.Compression;

namespace FezGame.Mod.Installer {
    public static class FezModder {
        
        public static void Install(this InstallerWindow ins) {
            try {
                ins.Install_();
            } catch (Exception e) {
                ins.LogLine(e.ToString());
            }
        }
        
        private static void Install_(this InstallerWindow ins) {
            ins.Invoke(() => ins.LogBox.Visible = true).SetMainEnabled(false);
            
            Directory.SetCurrentDirectory(ins.ExeMod.Dir.FullName);
            
            ins.Log("FEZ ").LogLine(ins.FezVersion);
            
            //Clean FEZ from any previous FEZMod installation
            ins.Uninstall();
            
            ins.Backup("Common.dll");
            ins.Backup("EasyStorage.dll");
            int v = int.Parse(ins.FezVersion.Substring(2));
            if (12 <= v) {
                ins.Backup("FNA.dll");
            } else {
                ins.Backup("MonoGame.Framework.dll");
            }
            ins.Backup("FezEngine.dll");
            ins.Backup("FEZ.exe");
            
            //Setup the files and MonoMod instances
            if (ins.VersionTabs.SelectedIndex == 0) {
                Tuple<string, string> t = ins.StableVersions[ins.StableVersionList.SelectedIndex];
                ins.Log("FEZMod Stable ").LogLine(t.Item1);
                if (!ins.UnzipMod(ins.Download(t.Item2))) {
                    return;
                }
                
            } else if (ins.VersionTabs.SelectedIndex == 1) {
                Tuple<string, string> t = ins.NightlyVersions[ins.NightlyVersionList.SelectedIndex];
                ins.Log("FEZMod Nightly ").LogLine(t.Item1);
                if (!ins.UnzipMod(ins.Download(t.Item2))) {
                    return;
                }
                
            } else if (ins.VersionTabs.SelectedIndex == 2) {
                string path = ins.ManualPathBox.Text;

                if (path.ToLower().EndsWith(".zip")) {
                    ins.LogLine("FEZMod Manual ZIP");
                    if (!ins.UnzipMod(File.OpenRead(path))) {
                        return;
                    }
                } else {
                    ins.LogLine("FEZMod Manual Folder");

                    string pathFez = ins.ExeMod.Dir.FullName;
                    string[] files = Directory.GetFiles(path);
                    ins.InitProgress("Copying FEZMod", files.Length);
                    for (int i = 0; i < files.Length; i++) {
                        string file = Path.GetFileName(files[i]);
                        if (!file.Contains(".mm.")) {
                            ins.SetProgress("Skipping: " + file, i);
                            continue;
                        }
                        ins.Log("Copying: ").LogLine(file);
                        ins.SetProgress("Copying: " + file, i);
                        string origPath = Path.Combine(pathFez, file);
                        File.Copy(files[i], origPath, true);
                    }
                    ins.EndProgress("Copying FEZMod complete.");
                }
            }
            
            ins.LogLine();
            ins.LogLine("Now comes the real \"modding\" / patching process.");
            ins.LogLine("If the installer were to output all the MonoMod stuff here,");
            ins.LogLine("the installer would slow down and maybe consume more RAM");
            ins.LogLine("than 65 open tabs in Chrome. :/");
            ins.LogLine("It may seem like the Installer may be stuck sometimes. Go make");
            ins.LogLine("yourself a coffee in the meantime - it doesn't get stuck.");
            ins.LogLine("It may *crash*, though - and in this case, debug stuff appears");
            ins.LogLine("here. Please put that debug stuff onto http://hastebin.com/ and");
            ins.LogLine("send it to @0x0ade on Twitter or FEZMod on GitHub.");
            ins.LogLine();
            
            ins.LogLine("Modding Common.dll").InitProgress("Modding Common.dll", 5);
            ins.LogLine("Common.dll is not that huge - not much to say here.");
            ins.LogLine();
            if (!ins.Mod("Common.dll")) {
                return;
            }
            
            ins.LogLine("Modding EasyStorage.dll").SetProgress("Modding EasyStorage.dll", 1);
            ins.LogLine("EasyStorage.dll also isn't huge - most probably Steam and Android stuff.");
            ins.LogLine();
            if (!ins.Mod("EasyStorage.dll")) {
                return;
            }
            
            if (12 <= v) {
                ins.LogLine("Modding FNA.dll").SetProgress("Modding FNA.dll", 2);
                ins.LogLine("Well, there's nothing to do here.. yet.");
                ins.LogLine("Future versions may replace \"modding\" with replacing.");
                ins.LogLine("FNA is the \"framework\" below FEZ and powering some other games, too.");
                ins.LogLine("It replaces MonoGame in FEZ 1.12+.");
                ins.LogLine();
                if (!ins.Mod("FNA.dll")) {
                    return;
                }
            } else {
                ins.LogLine("Modding MonoGame.Framework.dll").SetProgress("Modding MonoGame.Framework.dll", 2);
                ins.LogLine("Wait... where's FNA? Well, I guess you're using old FEZ.");
                ins.LogLine();
                if (!ins.Mod("MonoGame.Framework.dll")) {
                    return;
                }
            }
            
            ins.LogLine("Modding FezEngine.dll").SetProgress("Modding FezEngine.dll", 3);
            ins.LogLine("This may take some time as the Trixel Engine also becomes the");
            ins.LogLine("\"FEZMod Engine.\" If something low-level happens, for example");
            ins.LogLine("loading textures & music, handling geometry, ... it's here.");
            ins.LogLine("And every FEZ mod needs to use the \"FEZMod Engine\" to register");
            ins.LogLine("itself as mod to the FEZMod core. If it's complicated, don't worry:");
            ins.LogLine("This message simply means that modding FezEngine.dll takes time.");
            ins.LogLine();
            if (!ins.Mod("FezEngine.dll")) {
                return;
            }
            
            ins.LogLine("Modding FEZ.exe").SetProgress("Modding FEZ.exe", 4);
            ins.LogLine("Remember how long FezEngine.dll was? That was nothing!");
            ins.LogLine("Now it's time to get yourself some coffee.");
            ins.LogLine("It'd be nice to give you more exact progress here,");
            ins.LogLine("but as said before, it would kill the installer.");
            ins.LogLine("You won't see anything happening here, but don't panic:");
            ins.LogLine("If the installer crashes, an error log appears here.");
            ins.LogLine();
            if (!ins.Mod()) {
                return;
            }
            
            ins.EndProgress("Modding complete.");
            ins.LogLine("Back with the coffee? We're done! Look at the top-right!");
            ins.LogLine("You should see [just installed]. Feel free to start FEZ.");
            ins.LogLine("If FEZ crashes with FEZMod, go to the FEZ folder (that one");
            ins.LogLine("where FEZ.exe is, basically the path at the top-right),");
            ins.LogLine("upload JAFM Log.txt somewhere and give it @0x0ade.");
            ins.LogLine("Good luck - Have fun!");
            ins.ExeSelected(ins.ExeMod.In.FullName, " [just installed]");
            ins.SetMainEnabled(true);
        }
        
        public static bool Backup(this InstallerWindow ins, string file) {
            string pathFez = ins.ExeMod.Dir.FullName;
            string pathBackup = Path.Combine(pathFez, "FEZModBackup");
            if (!Directory.Exists(pathBackup)) {
                Directory.CreateDirectory(pathBackup);
            }
            
            string origPath = Path.Combine(pathFez, file);
            if (!File.Exists(origPath)) {
                return false;
            }
            
            ins.Log("Backing up: ").LogLine(file);
            File.Copy(origPath, Path.Combine(pathBackup, file), true);
            return true;
        }
        
        public static void Uninstall(this InstallerWindow ins) {
            if (ins.ExeMod == null) {
                return;
            }

            string pathFez = ins.ExeMod.Dir.FullName;
            string pathBackup = Path.Combine(pathFez, "FEZModBackup");
            if (!Directory.Exists(pathBackup)) {
                return;
            }

            if (ins.FezModVersion != null) {
                ins.Log("Found previous FEZMod installation: ").LogLine(ins.FezModVersion);
                ins.LogLine("Reverting to non-FEZMod backup...");
            } else {
                ins.LogLine("No previous FEZMod installation found.");
                ins.LogLine("Still reverting to non-FEZMod backup...");
            }

            string[] files = Directory.GetFiles(pathBackup);
            ins.InitProgress("Uninstalling FEZMod", files.Length + 1);
            for (int i = 0; i < files.Length; i++) {
                string file = Path.GetFileName(files[i]);
                ins.Log("Reverting: ").LogLine(file);
                ins.SetProgress("Reverting: " + file, i);
                string origPath = Path.Combine(pathFez, file);
                File.Delete(origPath);
                File.Move(files[i], origPath);
            }

            ins.LogLine("Reloading FEZ.exe");
            ins.SetProgress("Reloading FEZ.exe", files.Length);
            ins.ExeMod = new MonoMod.MonoMod(Path.Combine(pathFez, "FEZ.exe"));
            ins.ExeMod.Read(true);
            ins.EndProgress("Uninstalling complete.");
        }
        
        public static byte[] Download(this InstallerWindow ins, string url) {
            byte[] data = null;
            
            ins.Log("Downloading ").Log(url).LogLine("...");
            ins.InitProgress("Starting download", 1);
            
            DateTime timeStart = DateTime.Now;
            using (WebClient wc = new WebClient()) {
                using (Stream s = wc.OpenRead(url)) {
                    long sLength;
					if (s.CanSeek) {
						//Mono
						sLength = s.Length;
                    } else {
						//.NET
						sLength = getLength(url);
                    }
					data = new byte[sLength];

					long progressSize = sLength;
                    int progressScale = 1;
                    while (progressSize > int.MaxValue) {
                        progressScale *= 10;
						progressSize = sLength / progressScale;
                    }
                    
                    ins.InitProgress("Downloading", (int) progressSize);
                    
                    DateTime timeLast = timeStart;
                    
                    //if downloading to another stream, use CopyTo
                    int read;
                    int readForSpeed = 0;
                    int pos = 0;
                    int speed = 0;
                    TimeSpan td;
                    while (pos < data.Length) {
                        read = s.Read(data, pos, Math.Min(2048, data.Length - pos));
                        pos += read;
                        readForSpeed += read;
                        
                        td = (DateTime.Now - timeLast);
                        if (td.TotalMilliseconds > 100) {
                            speed = (int) ((readForSpeed / 1024D) / (double) td.TotalSeconds);
                            readForSpeed = 0;
                            timeLast = DateTime.Now;
                        }
                        
                        ins.SetProgress(
                            "Downloading - "  +
                                (int) (Math.Round(100D * ((double) (pos / progressScale) / (double) progressSize))) + "%, " +
                                speed + " KiB/s",
                            (int) (pos / progressScale)
                        );
                        
                    }
                    
                }
            }
            
            ins.EndProgress("Download complete");
            
            string logSize = (data.Length / 1024D).ToString(CultureInfo.InvariantCulture);
            logSize = logSize.Substring(0, Math.Min(logSize.IndexOf('.') + 3, logSize.Length));
            string logTime = (DateTime.Now - timeStart).TotalSeconds.ToString(CultureInfo.InvariantCulture);
            logTime = logTime.Substring(0, Math.Min(logTime.IndexOf('.') + 3, logTime.Length));
            ins.Log("Download complete, ").Log(logSize).Log(" KiB in ").Log(logTime).LogLine(" s.");
            
            return data;
        }

		private static long getLength(string url) {
			HttpWebRequest request = (HttpWebRequest) WebRequest.Create(url);
			request.UserAgent = "FEZMod Installer";
			request.Method = "HEAD";

			using (HttpWebResponse response = (HttpWebResponse) request.GetResponse()) {
				return response.ContentLength;
			}
		}
        
        public static bool UnzipMod(this InstallerWindow ins, byte[] data) {
            using (MemoryStream ms = new MemoryStream(data, 0, data.Length, false, true)) {
                return ins.UnzipMod(ms);
            }
        }
        
        public static bool UnzipMod(this InstallerWindow ins, Stream zs) {
            string platform = "";
            string os = FezFinder.GetPlatform().ToString().ToLower();
            if (os.Contains("win")) {
                platform = "win32";

            } else if (os.Contains("mac") || os.Contains("osx")) {
                platform = "osx";

            } else if (os.Contains("lin") || os.Contains("unix")) {
                platform = IntPtr.Size == 4 ? "lib" : /*== 8*/ "lib64";

            }

            string prefix = "FEZMOD";
            int v = int.Parse(ins.FezVersion.Substring(2));
            if (12 <= v) {
                prefix += "-FNA";
                ins.LogLine("FEZ 1.12 has switched from MonoGame to FNA.");
                ins.LogLine("Make sure the version you've picked is supported!");
            }
            prefix += "/";
            
            string pathFez = ins.ExeMod.Dir.FullName;
            
            ins.Log("Checking for ").Log(prefix).LogLine("...");
            
            using (ZipArchive zip = new ZipArchive(zs, ZipArchiveMode.Read)) {
                int prefixCount = 0;
                int fallbackCount = 0;
                int noneCount = 0;
                ins.InitProgress("Scanning ZIP", zip.Entries.Count);
                for (int i = 0; i < zip.Entries.Count; i++) {
                    ins.SetProgress(i);
                    ZipArchiveEntry entry = zip.Entries[i];
                    ins.Log("Entry: ").Log(entry.FullName).Log(": ").Log(entry.Length.ToString()).LogLine(" bytes");
                    
                    if (entry.FullName == "InstallerVersion.txt") {
                        ins.LogLine("Found version file.");
                        
                        using (Stream s = entry.Open()) {
                            using (StreamReader sr = new StreamReader(s)) {
                                string minv = sr.ReadLine().Trim();
                                if (InstallerWindow.Version < new Version(minv)) {
                                    ins.LogLine("There's a new FEZMod Installer version!");
                                    ins.LogLine("Visit https://fezmod.xyz/#download to download it.");
                                    ins.Log("(Minimum installer version for this FEZMod version: ").LogLine(minv).Log(")");
                                    return false;
                                }
                            }
                        }
                        
                        continue;
                    }
                    
                    string entryName = entry.FullName;
                    if (entry.FullName.StartsWith(prefix)) {
                        prefixCount++;
                    } else if (entry.FullName.StartsWith("FEZMOD/")) {
                        fallbackCount++;
                    } else {
                        noneCount++;
                    }
                }
                
                if (0 < prefixCount) {
                    ins.Log(prefix).LogLine(" found.");
                    ins.InitProgress("Extracting ZIP", prefixCount);
                } else if (0 == prefixCount && 0 < fallbackCount) {
                    ins.Log("Didn't find ").Log(prefix).LogLine(" - HALT THE GEARS!");
                    ins.EndProgress("Halted.").SetProgress(0);
                    return false;
                } else {
                    ins.LogLine("Is this even a FEZMod ZIP? uh...");
                    prefix = "";
                    ins.InitProgress("Extracting ZIP", noneCount);
                }
                
                int extracted = 0;
                for (int i = 0; i < zip.Entries.Count; i++) {
                    ZipArchiveEntry entry = zip.Entries[i];
                    if (!entry.FullName.StartsWith(prefix) || entry.FullName == prefix) {
                        continue;
                    }
                    ins.SetProgress(++extracted);
                    
                    string entryName = entry.FullName.Substring(prefix.Length);

                    if (entryName.StartsWith("LIBS/")) {
                        entryName = entryName.Substring(5);
                        if (!entryName.StartsWith(platform + "/")) {
                            continue;
                        }
                        entryName = entryName.Substring(platform.Length + 1);
                    }

                    entryName = entryName.Replace('/', Path.DirectorySeparatorChar);

                    string path = Path.Combine(pathFez, entryName);
                    ins.Log("Extracting: ").Log(entry.FullName).Log(" -> ").LogLine(path);
                    if (entry.Length == 0 && entry.CompressedLength == 0) {
                        Directory.CreateDirectory(path);
                    } else {
                        entry.ExtractToFile(path, true);
                    }
                }
                ins.EndProgress("Extracted ZIP.");
                
            }
            
            return true;
        }
        
        public static bool Mod(this InstallerWindow ins, string file) {
            MonoMod.MonoMod monomod = new MonoMod.MonoMod(Path.Combine(ins.ExeMod.Dir.FullName, file));
            monomod.Out = monomod.In;
            //monomod.Logger = (string s) => ins.LogLine(s);
            //TODO log to file
            try {
                monomod.AutoPatch(true, true);
                return true;
            } catch (Exception e) {
                ins.LogLine(e.ToString());
                return false;
            }
        }
        
        public static bool Mod(this InstallerWindow ins) {
            ins.ExeMod.Out = ins.ExeMod.In;
            //We need to reload the FEZ.exe dependencies here.
            //As they've been patched, FEZ.exe will otherwise refer to the .mm assemblies.
            ins.ExeMod.Module = null;
            ins.ExeMod.Dependencies.Clear();

            //ins.ExeMod.Logger = (string s) => ins.LogLine(s);
            //TODO log to file
            try {
                ins.ExeMod.AutoPatch(true, true);
                return true;
            } catch (Exception e) {
                ins.LogLine(e.ToString());
                return false;
            }
        }
        
    }
}