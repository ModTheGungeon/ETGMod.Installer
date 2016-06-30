using System;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.IO;
using System.Drawing.Text;
using Mono.Cecil;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Diagnostics;
using Mono.Collections.Generic;
using Mono.Cecil.Cil;

namespace ETGModInstaller {
    public static class ETGFinder {

        public static string MainName {
            get {
                if (Platform.HasFlag(ETGPlatform.Windows)) {
                    return "EtG.exe";

                } else if (Platform.HasFlag(ETGPlatform.MacOS)) {
                    // MacOS is weird.
                    // /Users/$USER/Library/Application Support/Steam/SteamApps/common/Enter the Gungeon/EtG_OSX.app/Contents/MacOS/EtG_OSX
                    return "EtG_OSX";

                } else if (Platform.HasFlag(ETGPlatform.Linux)) {
                    return IntPtr.Size == 4 ? "EtG.x86" : "EtG.x86_64";

                } else {
                    return null;
                }
            }
        }

        public static string ProcessName {
            get {
                if (Platform.HasFlag(ETGPlatform.Windows)) {
                    return "EtG";

                } else if (Platform.HasFlag(ETGPlatform.MacOS)) {
                    // TODO
                    return "EtG_OSX";

                } else if (Platform.HasFlag(ETGPlatform.Linux)) {
                    return IntPtr.Size == 4 ? "EtG.x86" : "EtG.x86_64";

                } else {
                    return null;
                }
            }
        }

        public static string SteamPath {
            get {
                Process[] processes = Process.GetProcesses(".");
                string path = null;
            
                for (int i = 0; i < processes.Length; i++) {
                    Process p = processes[i];
                
                    try {
                        if (!p.ProcessName.Contains("steam") || path != null) {
                            p.Dispose();
                            continue;
                        }
                    
                        if (p.MainModule.ModuleName.ToLower().Contains("steam")) {
                            path = p.MainModule.FileName;
                            Console.WriteLine("Steam found at " + path);
                            p.Dispose();
                        }
                    } catch (Exception) {
                        //probably the service acting up or a process quitting
                        p.Dispose();
                    }
                }
            
                if (path == null || Platform.HasFlag(ETGPlatform.MacOS)) { // TODO handle steam process path
                    Console.WriteLine("Found no Steam executable");
                
                    if (Platform.HasFlag(ETGPlatform.Linux)) {
                        path = Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".local/share/Steam");
                        if (!Directory.Exists(path)) {
                            return null;
                        } else {
                            Console.WriteLine("At least Steam seems to be installed somewhere reasonable...");
                            path = Path.Combine(path, "distributionX_Y/steam");
                        }

                    } else if (Platform.HasFlag(ETGPlatform.MacOS)) {
                        //Users/$USER/Library/Application Support/Steam/SteamApps/common/Enter the Gungeon/EtG_OSX.app/
                        path = Path.Combine("Users", Environment.GetEnvironmentVariable("USER"), "Library/Application Support/Steam");
                        if (!Directory.Exists(path)) {
                            return null;
                        } else {
                            Console.WriteLine("At least Steam seems to be installed somewhere reasonable...");
                            //path = Path.Combine(path, "bin/steam"); //?
                        }
                    } else {
                        return null;
                    }
                }
            
            
                if (Platform.HasFlag(ETGPlatform.Windows)) {
                    //I think we're running in Windows right now...
                    path = Directory.GetParent(path).Parent.FullName; //PF/Steam[/bin/steam.exe]
                    Console.WriteLine("Windows Steam main dir " + path);
                
                } else if (Platform.HasFlag(ETGPlatform.MacOS)) {
                    //Guyse, we need a test case here!
                    Console.WriteLine("MacOS Steam main dir " + path);
                    if (!Directory.Exists(path)) {
                        return null;
                    }
                
                } else if (Platform.HasFlag(ETGPlatform.Linux)) {
                    //Are you sure you want to forcibly remove everything from your home directory?
                    path = Directory.GetParent(path).Parent.FullName; //~/.local/share/Steam[/ubuntuX_Y/steam]
                    Console.WriteLine("Linux Steam main dir " + path);
                
                } else {
                    return null;
                }
            
                //PF/Steam/SteamApps //~/.local/share/Steam/SteamApps
                if (Directory.Exists(Path.Combine(path, "SteamApps"))) {
                    path = Path.Combine(path, "SteamApps");
                } else {
                    path = Path.Combine(path, "steamapps");
                }
                path = Path.Combine(path, "common"); //SA/common
            
                path = Path.Combine(path, "Enter the Gungeon");
                path = Path.Combine(path, ETGFinder.MainName);
            
                if (!File.Exists(path)) {
                    Console.WriteLine("EtG not found at " + path + " (at least Steam found)");
                    return null;
                }
            
                Console.WriteLine("EtG found at " + path);
            
                return path;
            }
        }

        public static void FindETG() {
            string path;
            
            if ((path = ETGFinder.SteamPath) != null) {
                try {
                    InstallerWindow.Instance.ExeSelected(path, " [auto - Steam]");
                } catch (Exception e) {
                    Console.WriteLine(e.ToString());
                }
            } else if (false) {
                //TODO check other paths
            } else {
                InstallerWindow.Instance.ExeSelected(null);
            }
        }

        public static void ExeLoaded(this InstallerWindow ins, string path) {
            ins.ExePathBox.Text = path;
            Task.Run(delegate () {
                ins.ExeSelected(path, " [saved]");
            });
        }


        public static void ExeSelected(this InstallerWindow ins, string path, string suffix = null) {
            if (string.IsNullOrEmpty(path)) {
                path = null;
            }

            string origPath = path;
            ins.Invoke(delegate() {
                ins.InstallButton.Enabled = false;
                ins.ExePathBox.Text = origPath;
                ins.ExeStatusLabel.Text = "EtG [checking version]";
                if (suffix != null) {
                    ins.ExeStatusLabel.Text += suffix;
                }
                ins.ExeStatusLabel.BackColor = Color.FromArgb(127, 255, 255, 63);
            });

            if (path != null && (ins.MainMod == null || ins.MainMod.In.FullName != path)) {
                path = Path.Combine(Directory.GetParent(path).FullName, "EtG_Data", "Managed", "Assembly-CSharp.dll");
                if (!File.Exists(path)) {
                    path = null;
                }
            }

            ins.ModVersion = null;
            if (path == null) {
                ins.MainMod = null;
                ins.Invoke(delegate () {
                    ins.ExeStatusLabel.Text = "No " + MainName + " selected";
                    ins.ExeStatusLabel.BackColor = Color.FromArgb(127, 255, 63, 63);
                    ins.ExePathBox.Text = "";
                    ins.InstallButton.Enabled = false;
                });
                return;
            }
            ins.MainMod = new MonoMod.MonoMod(path);

            //We want to read the assembly now already. Writing is also handled manually.
            try {
                ins.MainMod.Read(true);
            } catch (BadImageFormatException) {
                //this is not the assembly we need...
                ins.ExeSelected(null);
                return;
            } catch (Exception e) {
                //Something went wrong.
                ins.Log("Something went horribly wrong after you've selected ").LogLine(MainName);
                ins.LogLine("Blame 0x0ade and send him this log ASAP!");
                ins.Log("PATH: ").LogLine(path);
                ins.Log("DIR: ").LogLine(ins.MainMod.Dir.FullName);
                ins.LogLine(e.ToString());
                ins.ExeSelected(null);
                return;
            }
            
            TypeDefinition ModType = ins.MainMod.Module.GetType("ETGMod");
            if (ModType != null) {
                MethodDefinition ModCctor = null;
                for (int i = 0; i < ModType.Methods.Count; i++) {
                    if (ModType.Methods[i].IsStatic && ModType.Methods[i].IsConstructor) {
                        ModCctor = ModType.Methods[i];
                        break;
                    }
                }
                if (ModCctor != null) {
                    for (int i = 0; i < ModCctor.Body.Instructions.Count; i++) {
                        if (!(ModCctor.Body.Instructions[i].Operand is FieldReference)) {
                            continue;
                        }
                        if (((FieldReference) ModCctor.Body.Instructions[i].Operand).Name == "BaseVersionString") {
                            ins.ModVersion = getString(ModCctor.Body.Instructions, i - 1);
                            break;
                        }
                    }
                }
            }
            
            ins.Invoke(delegate() {
                ins.ExeStatusLabel.Text = "Enter The Gungeon";
                ins.ExeStatusLabel.BackColor = Color.FromArgb(127, 63, 255, 91);
                
                if (ins.ModVersion != null) {
                    ins.ExeStatusLabel.Text += " [Mod:";
                    ins.ExeStatusLabel.Text += ins.ModVersion;
                    ins.ExeStatusLabel.Text += "]";
                }
                
                if (suffix != null) {
                    ins.ExeStatusLabel.Text += suffix;
                }

                ins.ExePathBox.Text = origPath;
                ins.InstallButton.Enabled = true;
                ETGInstallerSettings.Save();
            });
        }
        
        private static string getString(Collection<Instruction> instructions, int pos) {
            string s = null;
            for (; s == null && 0 <= pos; pos--) {
                s = instructions[pos].Operand as string;
            }
            return s;
        }

        private static ETGPlatform _platform = ETGPlatform.Unknown;
        public static ETGPlatform Platform {
            get {
                if (!_platform.HasFlag(ETGPlatform.Unknown)) {
                    return _platform;
                }

                //string os = Environment.OSVersion.Platform.ToString().ToLower();
                //https://github.com/mono/mono/blob/master/mcs/class/corlib/System/Environment.cs
                //if MacOSX, OSVersion.Platform returns Unix.

                //for mono, get from
                //static extern PlatformID Platform
                PropertyInfo property_platform = typeof(Environment).GetProperty("Platform", BindingFlags.NonPublic | BindingFlags.Static);
                string platID;
                if (property_platform != null) {
                    platID = property_platform.GetValue(null).ToString();
                } else {
                    //for .net, use default value
                    platID = Environment.OSVersion.Platform.ToString();
                }
                platID = platID.ToLowerInvariant();

                _platform = ETGPlatform.Unknown;
                if (platID.Contains("win")) {
                    _platform = ETGPlatform.Windows;
                } else if (platID.Contains("mac") || platID.Contains("osx")) {
                    _platform = ETGPlatform.MacOS;
                } else if (platID.Contains("lin") || platID.Contains("unix")) {
                    _platform = ETGPlatform.Linux;
                }
                _platform |= (IntPtr.Size == 4 ? ETGPlatform.X86 : ETGPlatform.X64);

                return _platform;
            }
        }
        
    }

    public enum ETGPlatform : int {
        None = 0,

        // Underlying platform categories
        OS = 1,

        X86 = 0,
        X64 = 2,

        NT   = 4,
        Unix = 8,

        // Operating systems (OSes are always "and-equal" to OS)
        Unknown   = OS |         16,
        Windows   = OS | NT   |  32,
        MacOS     = OS | Unix |  64,
        Linux     = OS | Unix | 128,

        // AMD64 (64bit) variants (always "and-equal" to X64)
        Unknown64 = Unknown | X64,
        Windows64 = Windows | X64,
        MacOS64   = MacOS   | X64,
        Linux64   = Linux   | X64,
    }
}
 