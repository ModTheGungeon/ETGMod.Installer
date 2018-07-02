using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using System.Reflection;
using MonoMod;

namespace ETGModInstaller {
    public static class ETGModder {

        public static Action<bool> OnInstalled = (bool installed) => InstallerWindow.Instance.SetMainEnabled(true);

        public static bool IsOffline = false;

        public static string LogPath;
        public static string ExePath;
        public static string ExeBackupPath;
        public static bool AutoRun;

        public static List<Tuple<byte[], byte[]>> NativeResourceReplacementMap = GenOrigReplacementMap(
            "UnityEngine.Resources", "Load",
            "UnityEngine.Resources", "LoadAsync",
            "UnityEngine.Resources", "LoadAll",
            "UnityEngine.Resources", "GetBuiltinResource",
            "UnityEngine.Resources", "UnloadAsset",
            "UnityEngine.Resources", "UnloadUnusedAssets"
        );

        public static List<string> OverridePaths;
        public static List<string> Blacklist = new List<string>();

        public static void Install(this InstallerWindow ins) {
            ins.ChangeRobot(ins.RobotImageInstalling);
#if DEBUG
            ins.Install_();
#else
            try {
                ins.Install_();
            } catch (Exception e) {
                ins.ChangeRobot(ins.RobotImageError);
                ins.Invoke(() => ins.ViewLogButton.Text = "ERROR! Click here to get the log!");
                ins.LogLine(e.ToString());
                return;
            }
#endif
            ins.ChangeRobot(ins.RobotImageFinished);
        }

        private static void Install_(this InstallerWindow ins) {
            ins
                .Invoke(() => ExePath = ins.ExePathBox.Text)
                .Invoke(() => AutoRun = ins.AdvancedAutoRunCheckbox.Checked)
                .Invoke(ETGInstallerSettings.Save)
                .SetMainEnabled(false)
                .Wait();

            if (ETGFinder.IsBinaryWrapped) {
                ExePath = Path.Combine(Directory.GetParent(ExePath).FullName, ETGFinder.MainName);
            }

            Directory.SetCurrentDirectory(ins.MainModDir);

            if (AutoRun) {
                string etg = ETGFinder.ProcessName;
                Process[] processes = Process.GetProcesses(".");
                for (int i = 0; i < processes.Length; i++) {
                    Process p = processes[i];
                    try {
                        if (p.ProcessName != etg) {
                            p.Dispose();
                            continue;
                        }
                        p.Kill();
                        p.Dispose();
                        Thread.Sleep(250);
                    } catch (Exception) {
                        //probably the service acting up or a process quitting
                        p.Dispose();
                    }
                }
            }

            ins.LogLine("Entering the Modgeon");

            //Clean the game from any previous installation
            if (ETGFinder.Version != ETGFinder.VersionLastRun && ETGFinder.VersionLastRun != null) {
                ins.ClearBackup();
            } else {
                ins.Uninstall();
            }

            // We need to reload the main dependencies anyway.
            // As they've been patched, Assembly-CSharp.dll will otherwise refer to the .mm assemblies.
            // And as their dependencies get patched, we need to actually unload their symbol readers here.
            ins.MainMod?.Dispose();

            ins.Backup("UnityEngine.dll");
            ins.Backup("Assembly-CSharp.dll");
            ins.BackupETG();

            ins.PrepareDeobfuscator();
            ins.Deobfuscate("Assembly-CSharp.dll");

            //Setup the files and MonoMod instances
            int mi = -1;
            if (OverridePaths == null && !IsOffline) {
                mi = 0;
                ins.LogLine("Mod #0: Base");
                //Check if the revision online is newer
                RepoHelper.RevisionFile = Path.Combine(ins.MainModDir, "ModCache", "ETGMOD_REVISION.txt");
                int revisionOnline = RepoHelper.RevisionOnline;
                if (RepoHelper.Revision < revisionOnline) {
                    string cachedPath = Path.Combine(ins.MainModDir, "ModCache", "ETGMOD.zip");
                    if (File.Exists(cachedPath)) {
                        File.Delete(cachedPath);
                    }
                }
                if (!IsOffline && !ins.UnzipMod(ins.DownloadCached(RepoHelper.ETGModURL, "ETGMOD.zip"))) {
                    OnInstalled?.Invoke(false);
                    return;
                }
                RepoHelper.Revision = revisionOnline;
            }


            int[] selectedIndices = null;
            ins.Invoke(delegate () {
                int[] _selectedIndices = new int[ins.APIModsList.SelectedIndices.Count];
                ins.APIModsList.SelectedIndices.CopyTo(_selectedIndices, 0);
                selectedIndices = _selectedIndices;
            });
            while (selectedIndices == null) {
                Thread.Sleep(100);
            }

            for (int i = 0; i < selectedIndices.Length; i++) {
                Tuple<string, string> t = ins.APIMods[selectedIndices[i]];
                if (string.IsNullOrEmpty(t.Item2)) {
                    continue;
                }
                ins.Log("Mod #").Log((++mi).ToString()).Log(": ").LogLine(t.Item1);
                if (!ins.UnzipMod(ins.DownloadCached(t.Item2, t.Item1 + ".zip"))) {
                    OnInstalled?.Invoke(false);
                    return;
                }
            }

            List<string> paths = OverridePaths;
            if (paths == null) {
                paths = new List<string>();
                for (int pi = 0; pi < ins.AdvancedPathBoxes.Count; pi++) {
                    paths.Add(ins.AdvancedPathBoxes[pi].Text);
                }
            }

            for (int pi = 0; pi < paths.Count; pi++) {
                string path = paths[pi];

                if (path.ToLower().EndsWith(".zip")) {
                    ins.Log("Mod #").Log((++mi).ToString()).Log(": ZIP: ").LogLine(path);
                    if (!ins.UnzipMod(File.OpenRead(path))) {
                        OnInstalled?.Invoke(false);
                        return;
                    }
                } else if (path.ToLower().EndsWith(".mm.dll")) {
                    ins.Log("Mod #").Log((++mi).ToString()).Log(": DLL: ").LogLine(path);
                    File.Copy(path, Path.Combine(ins.MainModDir, Path.GetFileName(path)), true);
                    string pdb = Path.ChangeExtension(path, "pdb");
                    string mdb = path + ".mdb";
                    if (File.Exists(pdb)) {
                        File.Copy(pdb, Path.Combine(ins.MainModDir, Path.GetFileName(pdb)), true);
                    } else if (File.Exists(mdb)) {
                        File.Copy(mdb, Path.Combine(ins.MainModDir, Path.GetFileName(mdb)), true);
                    }
                } else {
                    ins.Log("Mod #").Log((++mi).ToString()).Log(": Folder: ").LogLine(path);

                    string pathGame = ins.MainModDir;
                    string[] files = Directory.GetFiles(path);
                    ins.InitProgress("Copying ETGMod", files.Length);
                    for (int i = 0; i < files.Length; i++) {
                        string file = Path.GetFileName(files[i]);
                        if (!file.Contains(".mm.")) {
                            ins.SetProgress("Skipping: " + file, i);
                            continue;
                        }
                        ins.Log("Copying: ").LogLine(file);
                        ins.SetProgress("Copying: " + file, i);
                        string origPath = Path.Combine(pathGame, file);
                        File.Copy(files[i], origPath, true);
                    }
                    ins.EndProgress("Copying ETGMod complete.");
                }
            }

            if (Blacklist.Count != 0) {
                ins.LogLine();
                ins.Log(Blacklist.Count.ToString()).LogLine(" mods on the blacklist - removing them!");
                for (int i = 0; i < Blacklist.Count; i++) {
                    string blacklisted = Blacklist[i];
                    string pathGame = ins.MainModDir;
                    string blacklistedPath = Path.Combine(pathGame, blacklisted);
                    ins.Log(blacklisted).Log(" blacklisted - ");
                    if (!File.Exists(blacklistedPath)) {
                        ins.LogLine("Not found though.");
                        continue;
                    }
                    ins.LogLine("BURN THE WITCH!");
                    File.Delete(blacklistedPath);
                }
                ins.LogLine();
            }

            LogPath = Path.Combine(ins.MainModDir, "ETGModInstallLog.txt");
            if (File.Exists(LogPath)) {
                File.Delete(LogPath);
            }

            ins.LogLine();
            ins.LogLine("Now comes the real \"modding\" / patching process.");
            ins.LogLine("It may seem like the Installer may be stuck sometimes. Go make");
            ins.LogLine("yourself a coffee in the meantime - it doesn't get stuck.");
            ins.LogLine("It may *crash*, though - and in this case, debug stuff appears");
            ins.LogLine("here. Please put that debug stuff onto http://pastebin.com/ and");
            ins.LogLine("send it to the #modding channel in https://discord.gg/etg");
            ins.LogLine();

            ins.PatchExe();

            if (!ins.Mod("UnityEngine.dll")) {
                OnInstalled?.Invoke(false);
                return;
            }

            if (!ins.Mod("Assembly-CSharp.dll")) {
                OnInstalled?.Invoke(false);
                return;
            }

            ins.EndProgress("Modding complete.");
            ins.LogLine("Back with the coffee? We're done! Look at the top-right!");
            ins.LogLine("You should see [just installed]. Feel free to start EtG.");
            ins.LogLine("If EtG crashes with ETGMod, go to the EtG folder (that one");
            ins.Log("where ").Log(ETGFinder.MainName).LogLine(" is, basically the path at the top-right),");
            ins.LogLine("then go to EtG_Data (that scary folder with many files) and");
            ins.LogLine("upload output_log.txt to #modding in https://discord.gg/etg");
            ins.LogLine("Good luck - Have fun!");
            ins.ExeSelected(ExePath, " [just installed]");

            if (AutoRun) {
                Process etg = new Process();
                etg.StartInfo.FileName = ExePath;
                etg.Start();
            }
            OnInstalled?.Invoke(true);
        }

        public static bool Backup(this InstallerWindow ins, string file) {
            string pathGame = ins.MainModDir;
            string pathBackup = Path.Combine(pathGame, "ModBackup");
            if (!Directory.Exists(pathBackup)) {
                Directory.CreateDirectory(pathBackup);
            }

            string origPath = Path.Combine(pathGame, file);
            if (!File.Exists(origPath)) {
                return false;
            }

            ins.Log("Backing up: ").LogLine(file);
            File.Copy(origPath, Path.Combine(pathBackup, file), true);
            return true;
        }

        public static bool BackupETG(this InstallerWindow ins) {
            string pathGame = ins.MainModDir;
            string pathBackup = Path.Combine(pathGame, "ModBackup");
            if (!Directory.Exists(pathBackup)) {
                Directory.CreateDirectory(pathBackup);
            }

            if (!File.Exists(ExePath)) {
                return false;
            }

            ins.Log("Backing up: ").LogLine(ETGFinder.MainName);
            ExeBackupPath = Path.Combine(pathBackup, ETGFinder.MainName);
            if (File.Exists(ExeBackupPath)) {
                File.Delete(ExeBackupPath);
            }
            File.Move(ExePath, ExeBackupPath);
            return true;
        }

        public static void Uninstall(this InstallerWindow ins) {
            if (ins.MainMod == null) {
                return;
            }
            ins.MainMod.Dispose();

            // Uninstall can be invoked without the installer running
            ins.Invoke(() => ExePath = ins.ExePathBox.Text).Wait();
            if (ETGFinder.IsBinaryWrapped) {
                ExePath = Path.Combine(Directory.GetParent(ExePath).FullName, ETGFinder.MainName);
            }

            string pathGame = ins.MainModDir;
            string pathBackup = Path.Combine(pathGame, "ModBackup");
            if (!Directory.Exists(pathBackup)) {
                return;
            }

            string[] files = Directory.GetFiles(pathGame);
            ins.InitProgress("Removing leftover files", files.Length + 1);
            for (int i = 0; i < files.Length; i++) {
                string file = Path.GetFileName(files[i]);
                if (!file.Contains(".mm.")) {
                    continue;
                }
                ins.Log("Removing: ").LogLine(file);
                ins.SetProgress("Removing: " + file, i);
                File.Delete(files[i]);
            }

            if (ins.ModVersion != null) {
                ins.Log("Found previous mod installation: ").LogLine(ins.ModVersion);
                ins.LogLine("Reverting to unmodded backup...");
            } else {
                ins.LogLine("No previous mod installation found.");
                ins.LogLine("Still reverting to unmodded backup...");
            }

            string etgBackup = Path.Combine(pathBackup, ETGFinder.MainName);
            ins.Log("Reverting: ").LogLine(ETGFinder.MainName);
            if (File.Exists(etgBackup)) {
                File.Delete(ExePath);
                File.Move(etgBackup, ExePath);
            } else {
                ins.Log("WARNING: Backup not found for ").LogLine(ETGFinder.MainName);
            }

            files = Directory.GetFiles(pathBackup);
            ins.InitProgress("Uninstalling ETGMod", files.Length + 1);
            for (int i = 0; i < files.Length; i++) {
                string file = Path.GetFileName(files[i]);
                ins.Log("Reverting: ").LogLine(file);
                ins.SetProgress("Reverting: " + file, i);
                string origPath = Path.Combine(pathGame, file);
                File.Delete(origPath);
                File.Move(files[i], origPath);
            }

            ins.LogLine("Reloading Assembly-CSharp.dll");
            ins.SetProgress("Reloading Assembly-CSharp.dll", files.Length);
            ins.MainMod = new MonoModder() {
                InputPath = ins.MainModIn
            };
            ins.MainMod.SetupETGModder();
#if DEBUG
            if (LogPath == null) {
                ins.MainMod.Read(); // Read main module first
                ins.MainMod.ReadMod(ins.MainModDir); // ... then mods
                ins.MainMod.MapDependencies(); // ... then all dependencies
            } else
                using (FileStream fileStream = File.Open(LogPath, FileMode.Append)) {
                    using (StreamWriter streamWriter = new StreamWriter(fileStream)) {
                        ins.MainMod.Logger = (string s) => ins.OnActivity();
                        ins.MainMod.Logger += (string s) => streamWriter.WriteLine(s);
                        // MonoMod.MonoModSymbolReader.MDBDEBUG = true;
#endif

                        ins.MainMod.Read(); // Read main module first
                        ins.MainMod.ReadMod(ins.MainModDir); // ... then mods
                        ins.MainMod.MapDependencies(); // ... then all dependencies
#if DEBUG
                        Mono.Cecil.TypeDefinition etgMod = ins.MainMod.Module.GetType("ETGMod");
                        if (etgMod != null) {
                            for (int i = 0; i < etgMod.Methods.Count; i++) {
                                Mono.Cecil.Cil.MethodBody body = etgMod.Methods[i].Body;
                            }
                        }
                    }
                }
            ins.MainMod.Logger = null;
#endif
            ins.EndProgress("Uninstalling complete.");
        }

        public static void ClearBackup(this InstallerWindow ins) {
            if (ins.MainMod == null) {
                return;
            }

            string pathGame = ins.MainModDir;
            string pathCache = Path.Combine(pathGame, "ModBackup");
            if (!Directory.Exists(pathCache)) {
                return;
            }

            ins.LogLine("Clearing mod backup...");

            string[] files = Directory.GetFiles(pathCache);
            ins.InitProgress("Clearing mod backup", files.Length + 1);
            for (int i = 0; i < files.Length; i++) {
                string file = Path.GetFileName(files[i]);
                ins.Log("Removing: ").LogLine(file);
                ins.SetProgress("Removing: " + file, i);
                File.Delete(files[i]);
            }

            ins.EndProgress("Clearing backup complete.");
        }

        public static byte[] Download(this InstallerWindow ins, string url) {
            if (IsOffline) {
                return null;
            }

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
                        read = s.Read(data, pos, Math.Min(2048, data.Length - pos));
                        pos += read;
                        readForSpeed += read;

                        td = (DateTime.Now - timeLast);
                        if (td.TotalMilliseconds > 100) {
                            speed = (int) ((readForSpeed / 1024D) / (double) td.TotalSeconds);
                            readForSpeed = 0;
                            timeLast = DateTime.Now;
                        }

                        ins.SetProgress(
                            "Downloading - " +
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

        public static byte[] DownloadCached(this InstallerWindow ins, string url, string cached) {
            byte[] data = ins.ReadDataFromCache(cached);
            if (data != null) {
                return data;
            }

            data = ins.Download(url);
            if (data == null) {
                return null;
            }

            ins.WriteDataToCache(cached, data);
            return data;
        }

        public static byte[] ReadDataFromCache(this InstallerWindow ins, string cached) {
            string pathGame = ins.MainModDir;
            string pathCache = Path.Combine(pathGame, "ModCache");
            if (!Directory.Exists(pathCache)) {
                Directory.CreateDirectory(pathCache);
            }

            string cachedPath = Path.Combine(pathCache, cached);
            if (!File.Exists(cachedPath)) {
                return null;
            }

            ins.Log("Reading from cache: ").LogLine(cached);
            return File.ReadAllBytes(cachedPath);
        }

        public static void WriteDataToCache(this InstallerWindow ins, string cached, byte[] data) {
            string pathGame = ins.MainModDir;
            string pathCache = Path.Combine(pathGame, "ModCache");
            if (!Directory.Exists(pathCache)) {
                Directory.CreateDirectory(pathCache);
            }

            string cachedPath = Path.Combine(pathCache, cached);
            if (File.Exists(cachedPath)) {
                File.Delete(cachedPath);
            }

            ins.Log("Writing to cache: ").LogLine(cached);
            File.WriteAllBytes(cachedPath, data);
        }

        public static void ClearCache(this InstallerWindow ins) {
            if (ins.MainMod == null) {
                return;
            }

            string pathGame = ins.MainModDir;
            string pathCache = Path.Combine(pathGame, "ModCache");
            if (!Directory.Exists(pathCache)) {
                return;
            }

            ins.LogLine("Clearing mod cache...");

            string[] files = Directory.GetFiles(pathCache);
            ins.InitProgress("Clearing mod cache", files.Length + 1);
            for (int i = 0; i < files.Length; i++) {
                string file = Path.GetFileName(files[i]);
                ins.Log("Removing: ").LogLine(file);
                ins.SetProgress("Removing: " + file, i);
                File.Delete(files[i]);
            }

            ins.EndProgress("Clearing cache complete.");
        }

        private static long getLength(string url) {
            HttpWebRequest request = (HttpWebRequest) WebRequest.Create(url);
            request.UserAgent = "ETGMod Installer";
            request.Method = "HEAD";

            using (HttpWebResponse response = (HttpWebResponse) request.GetResponse()) {
                return response.ContentLength;
            }
        }

        public static bool UnzipMod(this InstallerWindow ins, byte[] data) {
            if (data == null) {
                return false;
            }
            using (MemoryStream ms = new MemoryStream(data, 0, data.Length, false, true)) {
                return ins.UnzipMod(ms);
            }
        }

        public static bool UnzipMod(this InstallerWindow ins, Stream zs) {
            string platform = "";
            if (ETGFinder.Platform.HasFlag(ETGPlatform.Windows)) {
                platform = "win32";

            } else if (ETGFinder.Platform.HasFlag(ETGPlatform.MacOS)) {
                platform = "osx";

            } else if (ETGFinder.Platform.HasFlag(ETGPlatform.Linux)) {
                platform = ETGFinder.Platform.HasFlag(ETGPlatform.X64) ? "lib64" : "lib";

            }

            string fallback = "ETGMOD";
            string prefix = fallback;
            if (ETGFinder.Version.Contains("b")) {
                prefix += "-BETA";
            }

            fallback += "/";
            prefix += "/";

            string pathGame = ins.MainModDir;

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
                                Version minv = new Version(sr.ReadLine().Trim());
                                if (InstallerWindow.Version < minv) {
                                    ins.LogLine("There's a new ETGMod Installer version!");
                                    ins.LogLine("Visit https://modthegungeon.github.io/#download to download it.");
                                    ins.Log("(Minimum installer version for this ETGMod version: ").LogLine(minv.ToString()).Log(")");
                                    ins.Invoke(() => ins.Progress.BrushProgress =
                                        new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(255, 63, 63, 91))
                                    );
                                    ins.InitProgress("Installer update required!", 1).SetProgress(1);
                                    return false;
                                }
                            }
                        }

                        continue;
                    }

                    string entryName = entry.FullName;
                    if (entry.FullName.StartsWith(prefix)) {
                        prefixCount++;
                    } else if (entry.FullName.StartsWith(fallback)) {
                        fallbackCount++;
                    } else {
                        noneCount++;
                    }
                }

                if (0 < prefixCount) {
                    ins.Log(prefix).LogLine(" found.");
                    ins.InitProgress("Extracting ZIP", prefixCount);
                } else if (0 < fallbackCount) {
                    ins.Log("Didn't find ").Log(prefix).Log(", falling back to ").Log(fallback).LogLine(".");
                    prefix = fallback;
                    ins.InitProgress("Extracting ZIP", fallbackCount);
                } else {
                    ins.LogLine("Is this even a ETGMod ZIP? uh...");
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

                    string path = Path.Combine(pathGame, entryName);
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

        public static void PatchExe(this InstallerWindow ins) {
            using (FileStream fo = File.OpenWrite(ExePath)) {
                using (FileStream fi = File.OpenRead(ExeBackupPath)) {
                    using (BinaryWriter bo = new BinaryWriter(fo)) {
                        using (BinaryReader bi = new BinaryReader(fi)) {
                            ins.PatchExe(bi, bo);
                        }
                    }
                }
            }

            if (ETGFinder.Platform.HasFlag(ETGPlatform.Windows)) {
                // Windows doesn't have an executable bit

            } else if (ETGFinder.Platform.HasFlag(ETGPlatform.Unix)) {
                Process chmod = new Process();
                chmod.StartInfo.FileName = "chmod";
                chmod.StartInfo.Arguments = "a+x \"" + ExePath + "\"";
                chmod.StartInfo.CreateNoWindow = true;
                chmod.StartInfo.UseShellExecute = false;
                chmod.Start();
                chmod.WaitForExit();
            }
        }
        public static void PatchExe(this InstallerWindow ins, BinaryReader bi, BinaryWriter bo) {
            BinaryHelper.Replace(bi, bo, NativeResourceReplacementMap);
        }

        public static bool Mod(this InstallerWindow ins, string file) {
            string inPath = Path.Combine(ins.MainModDir, file);
            string outPath = Path.Combine(ins.MainModDir, file + ".tmp.dll");
            MonoModder monomod = new MonoModder() {
                InputPath = inPath,
                OutputPath = outPath
            };
            monomod.SetupETGModder();
            using (FileStream fileStream = File.Open(LogPath, FileMode.Append)) {
                using (StreamWriter streamWriter = new StreamWriter(fileStream)) {
                    monomod.Logger = (string s) => ins.OnActivity();
                    monomod.Logger += (string s) => streamWriter.WriteLine(s);
                    // Unity wants .mdbs
                    // monomod.WriterParameters.SymbolWriterProvider = new Mono.Cecil.Mdb.MdbWriterProvider();
                    string db = Path.ChangeExtension(inPath, "pdb");
                    string dbTmp = Path.ChangeExtension(outPath, "pdb");
                    if (!File.Exists(db)) {
                        db = inPath + ".mdb";
                        dbTmp = outPath + ".mdb";
                    }
#if !DEBUG
                    RETRY:
                    try {
#endif
                        monomod.Read(); // Read main module first
                        monomod.ReadMod(Directory.GetParent(inPath).FullName); // ... then mods
                        monomod.MapDependencies(); // ... then all dependencies
                        monomod.AutoPatch(); // Patch,
                        monomod.Write(); // and write.
                        monomod.Dispose(); // Finally, dispose, because file access happens now.

                        File.Delete(inPath);
                        File.Move(outPath, inPath);
                        if (File.Exists(db)) {
                            File.Delete(db);
                        }
                        if (File.Exists(dbTmp)) {
                            File.Move(dbTmp, db);
                        }
                        return true;
#if !DEBUG
                    } catch (ArgumentException e) {
                        monomod.Dispose();
                        if (File.Exists(db)) {
                            File.Delete(db);
                            if (File.Exists(dbTmp)) {
                                File.Delete(dbTmp);
                            }
                            goto RETRY;
                        }
                        ins.LogLine(e.ToString());
                        throw;
                        return false;
                    } catch (Exception e) {
                        monomod.Dispose();
                        ins.LogLine(e.ToString());
                        throw;
                        return false;
                    }
#endif
                }
            }
        }

        public static void ClearSymbols(this InstallerWindow ins, string path = null) {
            if (path == null) {
                if (ins.MainMod == null) {
                    return;
                }

                path = ins.MainModDir;
                ins.LogLine("Clearing symbols...");
            }

            string[] files = Directory.GetFiles(path);
            ins.InitProgress("Clearing symbols", files.Length + 1);
            for (int i = 0; i < files.Length; i++) {
                string file = Path.GetFileName(files[i]);
                if (!file.EndsWith(".pdb") && !file.EndsWith(".mdb")) {
                    continue;
                }
                ins.Log("Removing: ").LogLine(file);
                ins.SetProgress("Removing: " + file, i);
                File.Delete(files[i]);
            }

            ins.EndProgress("Clearing symbols complete.");
        }

        public static void RestartAndClearSymbols(this InstallerWindow ins) {
            if (ins.MainMod == null) {
                return;
            }

            if (ETGFinder.Version != ETGFinder.VersionLastRun && ETGFinder.VersionLastRun != null) {
                ins.ClearBackup();
            }

            Process reboot = new Process();
            reboot.StartInfo.FileName = Assembly.GetEntryAssembly().Location;
            reboot.StartInfo.Arguments = "--clearsymbols \"" + ins.MainModDir + "\"";
            reboot.StartInfo.CreateNoWindow = true;
            reboot.StartInfo.UseShellExecute = true;

            if (ETGFinder.Platform.HasFlag(ETGPlatform.Unix)) {
                reboot.StartInfo.Arguments = "\"" + reboot.StartInfo.FileName + "\" " + reboot.StartInfo.Arguments;
                reboot.StartInfo.FileName = "mono";
            }

            reboot.Start();
            Environment.Exit(1);
        }

        public static List<Tuple<byte[], byte[]>> GenOrigReplacementMap(params string[] sa) {
            List<Tuple<byte[], byte[]>> l = new List<Tuple<byte[], byte[]>>(sa.Length / 2);
            for (int i = 0; i < sa.Length; i += 2) {
                string c = sa[i];
                string m = sa[i + 1];
                l.Add(Tuple.Create(
                    Encoding.ASCII.GetBytes(c + "::" + m),
                    Encoding.ASCII.GetBytes(c + "::O" + m.Substring(1))
                ));
            }
            return l;
        }

        public static string ToHexadecimalString(this byte[] data) {
            return BitConverter.ToString(data).Replace("-", string.Empty);
        }

        public static bool ChecksumsEqual(this string[] a, string[] b) {
            if (a.Length != b.Length) {
                return false;
            }
            for (int i = 0; i < a.Length; i++) {
                if (a[i].Trim() != b[i].Trim()) {
                    return false;
                }
            }
            return true;
        }

        public static void SetupETGModder(this MonoModder mod) {
            mod.ReaderParameters.ReadSymbols = false;
            mod.WriterParameters.WriteSymbols = false;
            mod.WriterParameters.SymbolWriterProvider = null;
        }

    }
}
