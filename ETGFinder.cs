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

        public static string GetMainName() {
            string os = GetPlatform().ToString().ToLower();
            return os.Contains("win") ? "EtG.exe" : "EtG.x86_64";
        }
        
        public static string GetSteamPath() {
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
            
            //string os = Environment.OSVersion.Platform.ToString().ToLower();
            //https://github.com/mono/mono/blob/master/mcs/class/corlib/System/Environment.cs
            //if MacOSX, OSVersion.Platform returns Unix.
            string os = GetPlatform().ToString().ToLower();
            
            if (path == null) {
                Console.WriteLine("Found no Steam executable");
                
                if (os.Contains("lin") || os.Contains("unix")) {
                    path = Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".local/share/Steam");
                    if (!Directory.Exists(path)) {
                        return null;
                    } else {
                        Console.WriteLine("At least Steam seems to be installed somewhere reasonable...");
                        path = Path.Combine(path, "distributionX_Y/steam");
                    }
                } else {
                    return null;
                }
            }
            
            
            if (os.Contains("win")) {
                //I think we're running in Windows right now...
                path = Directory.GetParent(path).Parent.FullName; //PF/Steam[/bin/steam.exe]
                Console.WriteLine("Windows Steam main dir " + path);
                
            } else if (os.Contains("mac") || os.Contains("osx")) {
                //Guyse, we need a test case here!
                return null;
                
            } else if (os.Contains("lin") || os.Contains("unix")) {
                //Are you sure you want to forcibly remove everything from your home directory?
                path = Directory.GetParent(path).Parent.FullName; //~/.local/share/Steam[/ubuntuX_Y/steam]
                Console.WriteLine("Linux Steam main dir " + path);
                
            } else {
                Console.WriteLine("Unknown platform: " + os);
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
            path = Path.Combine(path, ETGFinder.GetMainName());
            
            if (!File.Exists(path)) {
                Console.WriteLine("EtG not found at " + path + " (at least Steam found)");
                return null;
            }
            
            Console.WriteLine("EtG found at " + path);
            
            return path;
        }
        
        public static void FindETG() {
            string path;
            
            if ((path = ETGFinder.GetSteamPath()) != null) {
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
        
        public static void ExeSelected(this InstallerWindow ins, string path, string suffix = null) {
            ins.Invoke(delegate() {
                ins.ExeStatusLabel.Text = path == null ? ("No " + ETGFinder.GetMainName() + " selected") : "EtG [checking version]";
                if (path != null && suffix != null) {
                    ins.ExeStatusLabel.Text += suffix;
                }
                ins.ExeStatusLabel.BackColor = path == null ? Color.FromArgb(127, 255, 63, 63) : Color.FromArgb(127, 255, 255, 63);
                ins.ExePathBox.Text = path ?? "";
                ins.InstallButton.Enabled = false;
            });

            if (ins.MainMod == null || ins.MainMod.In.FullName != path) {
                if (path != null) {
                    path = Path.Combine(Directory.GetParent(path).FullName, "EtG_Data", "Managed", "Assembly-CSharp.dll");
                    if (!File.Exists(path)) {
                        path = null;
                    }
                }
            }

            if (path != null) {
                ins.MainMod = new MonoMod.MonoMod(path);
            } else {
                ins.MainMod = null;
                ins.ModVersion = null;
                return;
            }

            //We want to read the assembly now already. Writing is also handled manually.
            try {
                ins.MainMod.Read(true);
            } catch (BadImageFormatException) {
                //this is not the assembly we need...
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
                if (ModCctor == null) {
                    ins.ExeSelected(null);
                    return;
                }
                ins.ModVersion = null;
                for (int i = 0; i < ModCctor.Body.Instructions.Count; i++) {
                    if (!(ModCctor.Body.Instructions[i].Operand is FieldReference)) {
                        continue;
                    }
                    if (((FieldReference) ModCctor.Body.Instructions[i].Operand).Name == "BaseVersionString") {
                        ins.ModVersion = getString(ModCctor.Body.Instructions, i-1);
                        break;
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
                
                ins.InstallButton.Enabled = true;
            });
        }
        
        private static string getString(Collection<Instruction> instructions, int pos) {
            string s = null;
            for (; s == null && 0 <= pos; pos--) {
                s = instructions[pos].Operand as string;
            }
            return s;
        }

        public static PlatformID GetPlatform() {
            //for mono, get from
            //static extern PlatformID Platform
            PropertyInfo property_platform = typeof(Environment).GetProperty("Platform", BindingFlags.NonPublic | BindingFlags.Static);
            if (property_platform != null) {
                return (PlatformID) property_platform.GetValue(null);
            } else {

                //for .net, use default value
                return Environment.OSVersion.Platform;
            }
        }
        
    }
}
 