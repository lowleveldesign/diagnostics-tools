using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace LowLevelDesign.Diagnostics
{
    static class ImageFileExecutionWrapper
    {
        public static void SetupHook(String appImageExe, bool enabled)
        {
            // extrace image.exe if path is provided
            appImageExe = Path.GetFileName(appImageExe);
            using (var regkey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options", true)) {
                Debug.Assert(regkey != null);
                // add to image file execution path
                if (enabled) {
                    using (var sk = regkey.CreateSubKey(appImageExe)) {
                        sk.SetValue("Debugger", "\"" + Assembly.GetExecutingAssembly().Location + "\"");
                    }
                } else {
                    regkey.DeleteSubKey(appImageExe, false);
                }
            }
        }

        public static void ListHooks()
        {
            var asmpath = "\"" + Assembly.GetExecutingAssembly().Location + "\"";

            using (var regkey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options", true)) {
                Debug.Assert(regkey != null);

                var hooks = new List<String>();
                foreach (var skn in regkey.GetSubKeyNames()) {
                    using (var sk = regkey.OpenSubKey(skn, false)) {
                        var v = sk.GetValue("Debugger") as String;
                        if (v != null && v.StartsWith(asmpath, StringComparison.OrdinalIgnoreCase)) {
                            hooks.Add(skn);
                        }
                    }
                }

                if (hooks.Count == 0) {
                    Console.WriteLine("No hooks found.");
                } else {
                    Console.WriteLine("Hooks installed for:");
                    foreach (var h in hooks) {
                        Console.WriteLine(" * " + h);
                    }
                }
            }
        }

    }
}
