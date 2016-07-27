using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Topshelf.Builders;
using Topshelf.HostConfigurators;
using Topshelf.Runtime;

namespace Topshelf.Diagnostics
{
    public static class HostConfiguratorExtensions
    {
        [Obsolete("Please use the ApplyCommandLineWithDebuggerSupport")]
        public static void ApplyCommandLineWithInwrapSupport(this HostConfigurator hc) {
            ApplyCommandLineWithDebuggerSupport(hc);
        }

        public static void ApplyCommandLineWithDebuggerSupport(this HostConfigurator hc) {
            var svcpath = Assembly.GetExecutingAssembly().Location;

            var cmdline = Environment.CommandLine;
            var ind = cmdline.IndexOf(svcpath, StringComparison.InvariantCultureIgnoreCase);
            if (ind < 0) {
                hc.ApplyCommandLine();
            } else {
                ind = ind + svcpath.Length;
                if (cmdline.Length > ind && cmdline[ind] == '"') {
                    ind = ind + 1;
                }
                hc.ApplyCommandLine(cmdline.Substring(ind));
            }
        }

        public static void UseWindowsHostEnvironmentWithDebugSupport(this HostConfigurator hc) {
            hc.UseEnvironmentBuilder((hconf) => { return new WindowsHostEnvironmentBuilderWithDebugSupport(hconf); });
        }
    }

    class WindowsHostEnvironmentBuilderWithDebugSupport : EnvironmentBuilder
    {
        private readonly HostConfigurator hc;

        public WindowsHostEnvironmentBuilderWithDebugSupport(HostConfigurator hc)
        {
            this.hc = hc;
        }

        public HostEnvironment Build() {
            return new WindowsHostEnvironmentWithDebugSupport(hc);
        }
    }

}
