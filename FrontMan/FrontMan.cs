using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;

namespace LowLevelDesign.Diagnostics
{
    public static class Program
    {
        private enum RegistryOperation {
            INSTALL,
            UNINSTALL,
            NONE
        }
    
        private readonly static char[] charsToOmit = new [] { '"', '@' };

        public static void Main(String[] args) {
            if (args.Length == 0) {
                PrintHelp();
                return;
            }
            // install or uninstall
            var oper = RegistryOperation.NONE;
            if (String.Equals(args[0], "-install", StringComparison.OrdinalIgnoreCase)) {
                oper = RegistryOperation.INSTALL;
            } else if (String.Equals(args[0], "-uninstall", StringComparison.OrdinalIgnoreCase)) {
                oper = RegistryOperation.UNINSTALL;
            }
            if (oper != RegistryOperation.NONE) {
                if (args.Length != 2) {
                    PrintHelp();
                    return;
                }
                SetupRegistryForFrontMan(args[1], oper);
                return;
            }

            // save call with arguments
            var argstr = "\"" + String.Join("\" \"", args) + "\"";

            String outdir = Path.Combine(ConfigurationManager.AppSettings["OutputDirectory"] ??
                                Path.GetTempPath(), "frontman",
                                String.Format("{0:yyyyMMdd_HHmmss.fff}_{1}", DateTime.UtcNow,
                                Path.GetFileName(args[0])));
            // create the outdir if necessary
            Directory.CreateDirectory(outdir);
            bool enabled;
            if (Boolean.TryParse(ConfigurationManager.AppSettings["Enabled"] ?? "true", out enabled)
                    && enabled) {
                // scan arguments in order to find a file
                bool copyFiles;
                if (Boolean.TryParse(ConfigurationManager.AppSettings["CopyFiles"] ?? "true",
                      out copyFiles) && copyFiles) {
                    for (int i = 1; i < args.Length; i++) {
                        var path = args[i].Trim(charsToOmit);
                        if (File.Exists(path)) {
                            var fileName = String.Format("arg_{0}_{1}", i, Path.GetFileName(path));
                            // copy the file from argument
                            File.Copy(path, Path.Combine(outdir, fileName));
                        }
                    }
                }
                File.WriteAllText(Path.Combine(outdir, "callargs.txt"), argstr);
            }

            // finally create the destination process and exit
            var procdumpPath = ConfigurationManager.AppSettings["ProcdumpPath"] ?? "procdump.exe";
            if (!procdumpPath.EndsWith("procdump.exe", StringComparison.OrdinalIgnoreCase)) {
                procdumpPath = Path.Combine(procdumpPath, "procdump.exe");
            }
            var dumpOptions = ConfigurationManager.AppSettings["ProcdumpDumpOptions"] ?? "-ma -e";

            var proc = new Process();
            proc.StartInfo.FileName = procdumpPath;
            proc.StartInfo.Arguments = String.Format("-accepteula {0} -x {1} {2}", dumpOptions, outdir, argstr);
            proc.StartInfo.UseShellExecute = false;
            proc.Start();
            proc.WaitForExit();
        }

        private static void PrintHelp() {
            Console.WriteLine("frontman <app-to-start> [args]");
            Console.WriteLine("frontman -install <app-image-name>");
            Console.WriteLine("frontman -uninstall <app-image-name>");
        }

        private static bool IsUserAdmin() {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static void SetupRegistryForFrontMan(String appImageExe, RegistryOperation oper) {
            if (!IsUserAdmin()) {
                Console.WriteLine("You must be admin to do that. Run the app from the administrative console.");
                return;
            }
            // extrace image.exe if path is provided
            appImageExe = Path.GetFileName(appImageExe);
            var regkey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options", true);
            // add to image file execution path
            if (oper == RegistryOperation.INSTALL) {
                regkey = regkey.CreateSubKey(appImageExe);
                regkey.SetValue("Debugger", Assembly.GetExecutingAssembly().Location);
            } else if (oper == RegistryOperation.UNINSTALL) {
                regkey.DeleteSubKey(appImageExe, false);
            }
        }
    }
}