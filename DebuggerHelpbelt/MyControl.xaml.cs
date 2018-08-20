using EnvDTE;
using EnvDTE100;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace LowLevelDesign.DebuggerHelpbelt
{
    /// <summary>
    /// Interaction logic for MyControl.xaml
    /// </summary>
    public partial class MyControl : UserControl
    {
        private readonly MyToolWindow parent;

        public MyControl(MyToolWindow parent)
        {
            this.parent = parent;

            InitializeComponent();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1300:SpecifyMessageBoxOptions")]
        private void button1_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(string.Format(System.Globalization.CultureInfo.CurrentUICulture, "We are inside {0}.button1_Click()", this.ToString()),
                            "Debugger HelpBelt");

        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1300:SpecifyMessageBoxOptions")]
        private void btnJustMyCode_Click(object sender, RoutedEventArgs e)
        {
            var dte = (DTE2)parent.DTE;
            var prop = dte.Properties["Debugging", "General"].Item("EnableJustMyCode");
            prop.Value = true;
        }

        private String GetSymbolsFolder() {
            return System.IO.Path.Combine(System.IO.Path.GetTempPath(), "_symbols");
        }

        private void ClearCache(String symbolFolder) {
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1300:SpecifyMessageBoxOptions")]
        private void btnDotPeekSymbolServer_Click(object sender, RoutedEventArgs e)
        {
            var dte = (DTE2)parent.DTE;
            var prop = dte.Properties["Debugging", "General"].Item("EnableJustMyCode");
            prop.Value = false;

            var sympath = System.IO.Path.Combine(GetSymbolsFolder(), "dotpeek");
            var v = String.Format("SRV*{0}*http://localhost:33417;SRV*{0}*http://referencesource.microsoft.com/symbols;SRV*{0}*http://msdl.microsoft.com/download/symbols", sympath);
            Environment.SetEnvironmentVariable("_NT_SYMBOL_PATH", v, EnvironmentVariableTarget.Process);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1300:SpecifyMessageBoxOptions")]
        private void btnNetFrameworkSource_Click(object sender, RoutedEventArgs e)
        {
            var dte = (DTE2)parent.DTE;
            var prop = dte.Properties["Debugging", "General"].Item("EnableJustMyCode");
            prop.Value = false;

            var sympath = System.IO.Path.Combine(GetSymbolsFolder(), "ms");
            var v = String.Format("SRV*{0}*http://referencesource.microsoft.com/symbols;SRV*{0}*http://msdl.microsoft.com/download/symbols", sympath);
            Environment.SetEnvironmentVariable("_NT_SYMBOL_PATH", v, EnvironmentVariableTarget.Process);
        }

        private void btnEmptyCache_Click(object sender, RoutedEventArgs e)
        {
            var symbolFolder = GetSymbolsFolder();
            try {
                Directory.Delete(System.IO.Path.Combine(symbolFolder, "ms"), true);
            } catch (Exception ex) {
                Trace.WriteLine("Exception occured while clearing cache: " + ex);
            }
            try {
                Directory.Delete(System.IO.Path.Combine(symbolFolder, "dotpeek"), true);
            } catch (Exception ex) {
                Trace.WriteLine("Exception occured while clearing cache: " + ex);
            }
        }
    }
}