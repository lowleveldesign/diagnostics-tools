using CommandLine;
using System;
using System.Collections.Generic;

namespace LowLevelDesign.Diagnostics
{
    class Options
    {
        [Option("install", Required = false, HelpText = "Install a hook on a service", SetName = "opt")]
        public bool ShouldInstall { get; set; }

        [Option("uninstall", Required = false, HelpText = "Uninstall a hook set on a service", SetName = "opt")]
        public bool ShouldUninstall { get; set; }

        [Option("list", Required = false, HelpText = "Lists already installed service hooks", SetName = "opt")]
        public bool ListHooks { get; set; }

        [Option("timeout", Required = false, HelpText = "Set the timeout (in seconds) after which the unresponsive service will be killed by the system")]
        public int Timeout { get; set; }

        [Value(0, Required = false)]
        public String ServiceExePath { get; set; }

        [Value(1)]
        public IList<String> ServiceArgs { get; set; }
    }
}
