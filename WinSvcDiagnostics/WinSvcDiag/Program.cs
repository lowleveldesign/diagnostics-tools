using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Principal;
using Microsoft.Win32;

namespace LowLevelDesign.Diagnostics
{
    static class Program
    {
        static void Main(string[] args)
        {
            var result = CommandLine.Parser.Default.ParseArguments<Options>(args);
            if (result.Errors.Any()) {
                return;
            }
            if (!IsUserAdmin()) {
                Console.WriteLine("You need to be an administrator to use this application.");
                return;
            }
            if (result.Value.ShouldInstall && result.Value.ShouldUninstall) {
                Console.WriteLine("You need to decide whether you would like to install or uninstall a hook.");
                return;
            }
            if (result.Value.ListHooks) {
                ImageFileExecutionWrapper.ListHooks();
                return;
            }

            if (result.Value.Timeout > 0) {
                SetServiceTimeout(result.Value.Timeout);
            } else {
                if (GetServiceTimeout() < 60000) { 
                    // service will be killed if it does not respond under 1 min.
                    Console.WriteLine("Warning: current ServicePipeTimeout is set under 1 min - this time might not be enough " +
                        "to attach with the debugger to the service and start debugging it. It's highly recommended to set this " +
                        "value to at least 4 min (you may use -timeout option for this purpose)");
                }
            }

            var svcpath = result.Value.ServiceExePath;
            if (svcpath == null) {
                Console.WriteLine("A path to the service exe file must be provided.");
                return;
            }

            if (result.Value.ShouldInstall) {
                ImageFileExecutionWrapper.SetupHook(svcpath, true);
            } else if (result.Value.ShouldUninstall) {
                ImageFileExecutionWrapper.SetupHook(svcpath, false);
            } else {
                // here the logic will be a bit more complicated - we will start the process
                // as suspended and then wait for the debugger to appear
                var s = new Suspender(svcpath);
                s.StartProcessAndWaitForDebugger(result.Value.ServiceArgs);
            }
        }

        private static bool IsUserAdmin()
        {
            var identity = WindowsIdentity.GetCurrent();
            Debug.Assert(identity != null);
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static int GetServiceTimeout()
        {
            using (var regkey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control", true)) {
                return (int)regkey.GetValue("ServicesPipeTimeout");
            }
        }

        private static void SetServiceTimeout(int timeoutInSeconds)
        {
            using (var regkey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control", true)) {
                regkey.SetValue("ServicesPipeTimeout", timeoutInSeconds * 1000);
                Console.WriteLine("Timeout changed, but reboot is required for the option to take an effect.");
            }
        }
    }
}
