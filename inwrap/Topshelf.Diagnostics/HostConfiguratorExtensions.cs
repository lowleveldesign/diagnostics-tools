using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Topshelf.HostConfigurators;

namespace Topshelf.Diagnostics
{
    public static class HostConfiguratorExtensions
    {
        public static void ApplyCommandLineWithInwrapSupport(this HostConfigurator hc) {
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
    }
}
