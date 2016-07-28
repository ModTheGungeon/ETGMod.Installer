using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Diagnostics;
using System.Threading;

namespace ETGModInstaller {
    public delegate bool DeobfuscatorPrepare(InstallerWindow ins);
    public delegate bool DeobfuscatorRun(InstallerWindow ins, string file);
    public static class DeobfuscatorExt {

        public static DeobfuscatorPrepare PrepareHook;
        public static bool PrepareDeobfuscator(this InstallerWindow ins) {
            if (PrepareHook == null) {
                return true;
            }
            return PrepareHook.Invoke(ins);
        }

        public static DeobfuscatorRun DeobfuscateHook;
        public static bool Deobfuscate(this InstallerWindow ins, string file) {
            if (DeobfuscateHook == null) {
                return true;
            }
            return DeobfuscateHook.Invoke(ins, file);
        }

    }
}
