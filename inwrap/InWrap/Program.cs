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
    sealed class InWrap
    {
        private readonly TraceSource logger = new TraceSource("LowLevelDesign.Diagnostics.InWrap");
        private readonly IDictionary<Type, IList<MethodInfo>> exceptionHandlers;

        public InWrap(String handlersFolder)
        {
            exceptionHandlers = new Dictionary<Type, IList<MethodInfo>>();
            var resolveHandlers = new HashSet<String>(StringComparer.OrdinalIgnoreCase);

            // add default assembly folder as it's searched by default
            resolveHandlers.Add(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

            if (handlersFolder != null)
            {
                var di = new DirectoryInfo(handlersFolder);
                if (di.Exists)
                {
                    // list all .dll files which name ends with Exception or Exceptions and try to load them
                    var asemblyFiles = di.EnumerateFiles("*Exception?.???", SearchOption.TopDirectoryOnly);

                    foreach (var af in asemblyFiles)
                    {
                        if (!af.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) && !af.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var assemblyFile = af;
                        if (!resolveHandlers.Contains(assemblyFile.DirectoryName))
                        {
                            resolveHandlers.Add(assemblyFile.DirectoryName);
                            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
                            {
                                Debug.Assert(assemblyFile.DirectoryName != null, "assemblyFile.DirectoryName != null");
                                var asmPath = Path.Combine(assemblyFile.DirectoryName, new AssemblyName(args.Name).Name + ".dll");
                                if (!File.Exists(asmPath))
                                {
                                    asmPath = Path.Combine(assemblyFile.DirectoryName, new AssemblyName(args.Name).Name + ".exe");
                                    if (!File.Exists(asmPath))
                                    {
                                        return null;
                                    }
                                }
                                logger.TraceEvent(TraceEventType.Verbose, 0, "Loading dependent assembly '{0}'.", asmPath);
                                return Assembly.LoadFile(asmPath);
                            };
                        }

                        logger.TraceEvent(TraceEventType.Verbose, 0, "Looking for handlers in '{0}'.", af);
                        try
                        {
                            var asm = Assembly.LoadFile(af.FullName);
                            // look for static types
                            var types = asm.GetTypes();
                            foreach (var t in types.Where(t => t.IsAbstract && t.IsSealed))
                            {
                                var methods = t.GetMethods(BindingFlags.Static | BindingFlags.Public);
                                // find static method which accepts a subtype of exception
                                foreach (var m in methods)
                                {
                                    var args = m.GetParameters();
                                    if (args.Length == 2)
                                    {
                                        var extype = args[1].ParameterType;
                                        if (typeof(TraceSource).Equals(args[0].ParameterType) && typeof(Exception).IsAssignableFrom(extype))
                                        {
                                            // add this method 
                                            IList<MethodInfo> handlers;
                                            if (!exceptionHandlers.TryGetValue(extype, out handlers))
                                            {
                                                handlers = new List<MethodInfo>();
                                                exceptionHandlers.Add(extype, handlers);
                                            }
                                            handlers.Add(m);

                                            logger.TraceEvent(TraceEventType.Information, 0, "Found handler method: {0} for exception: {1} (assembly {2})",
                                                m.Name, extype.FullName, asm.FullName);
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.TraceEvent(TraceEventType.Verbose, 0, "Loading handlers failed for '{0}' with exception: {1}", af, ex);
                        }
                    }
                }
            }
        }

        public void LogException(object sender, FirstChanceExceptionEventArgs firstChanceExceptionEventArgs)
        {
            IList<MethodInfo> handlers;
            if (exceptionHandlers.TryGetValue(firstChanceExceptionEventArgs.Exception.GetType(), out handlers))
            {
                Debug.Assert(handlers != null && handlers.Count > 0);
                foreach (var handler in handlers)
                {
                    logger.TraceEvent(TraceEventType.Error, 0, "------------------ HANDLER ---------------------");
                    try {
                        handler.Invoke(null, new Object[] { logger, firstChanceExceptionEventArgs.Exception });
                    } catch (Exception ex) {
                        logger.TraceEvent(TraceEventType.Error, 0, "Exception thrown by the handler: {0}", ex);
                    }
                }
            }

            // default handling - dumps exception object
            logger.TraceEvent(TraceEventType.Warning, 0, "------------------ DEFAULT ---------------------");
            logger.TraceEvent(TraceEventType.Warning, 0, "First-chance exception: {0}", firstChanceExceptionEventArgs.Exception);
        }
    }


    static class Program
    {
        static void Main(string[] args)
        {
            // command line arguments
            String handlersFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), imagePath = null;
            bool? isInstallation = null;
            int lastArgumentIndex = args.Length;

            if (args.Length == 0)
            {
                PrintHelp();
                return;
            }

            for (int i = 0; i < args.Length; i++)
            {
                if (String.Equals(args[i], "-handlers", StringComparison.OrdinalIgnoreCase))
                {
                    if (i == args.Length - 1 || args[i + 1].StartsWith("-"))
                    {
                        Console.Error.Write("ERROR: -handlers parameter requires a valid path to handlers folder.\r\n");
                        PrintHelp();
                        return;
                    }
                    // next argument must be a folder for handlers
                    handlersFolder = Path.GetFullPath(args[++i]);
                    if (!Directory.Exists(handlersFolder))
                    {
                        Console.Error.Write("ERROR: '{0}' is not a valid path.\r\n", handlersFolder);
                        PrintHelp();
                        return;
                    }
                }
                else if (String.Equals(args[i], "-install", StringComparison.OrdinalIgnoreCase))
                {
                    // next argument must be an exe file
                    if (i == args.Length - 1 || args[i + 1].StartsWith("-"))
                    {
                        Console.Error.Write("ERROR: -install parameter requires an exe file to hook on.\r\n");
                        PrintHelp();
                        return;
                    }
                    isInstallation = true;
                    imagePath = args[++i];
                }
                else if (String.Equals(args[i], "-uninstall", StringComparison.OrdinalIgnoreCase))
                {
                    // next argument must be an exe file
                    if (i == args.Length - 1 || args[i + 1].StartsWith("-"))
                    {
                        Console.Error.Write("ERROR: -uninstall parameter requires an exe file for which the hook should be removed.\r\n");
                        PrintHelp();
                        return;
                    }
                    isInstallation = false;
                    imagePath = args[++i];
                }
                else
                {
                    // it means that we finished reading inwrap parameters
                    lastArgumentIndex = i;
                    break;
                }
            }

            if (isInstallation.HasValue)
            {
                Debug.Assert(imagePath != null, "imagePath != null");
                // special mode use to install inwrap as a static wrapper
                SetupRegistryForFrontMan(imagePath, handlersFolder, isInstallation.Value);
            }
            else
            {
                if (lastArgumentIndex == args.Length)
                {
                    Console.Error.Write("ERROR: no application to run defined.\r\n");
                    PrintHelp();
                    return;
                }

                // create inwrap instance and load handlers
                var inwrap = new InWrap(handlersFolder);

                // set listener on first chance exception in the domain
                AppDomain.CurrentDomain.FirstChanceException += inwrap.LogException;

                // call child assembly
                if (lastArgumentIndex + 1 < args.Length)
                {
                    var childArgs = new string[args.Length - lastArgumentIndex - 1];
                    Array.Copy(args, lastArgumentIndex + 1, childArgs, 0, childArgs.Length);
                    AppDomain.CurrentDomain.ExecuteAssembly(args[lastArgumentIndex], childArgs);
                }
                else
                {
                    AppDomain.CurrentDomain.ExecuteAssembly(args[lastArgumentIndex]);
                }
            }
        }

        private static void PrintHelp()
        {
            Console.WriteLine("inwrap.exe [-handlers <handlers-path>] -install <exe-file> | -uninstall <exe-file> | exe-file arg1 arg2 ...");
        }

        private static bool IsUserAdmin()
        {
            var identity = WindowsIdentity.GetCurrent();
            Debug.Assert(identity != null);
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static void SetupRegistryForFrontMan(String appImageExe, String handlersFolder, bool isInstallation)
        {
            Debug.Assert(appImageExe != null);
            if (!IsUserAdmin())
            {
                Console.WriteLine("You must be admin to do that. Run the app from the administrative console.");
                return;
            }
            // extrace image.exe if path is provided
            appImageExe = Path.GetFileName(appImageExe);
            var regkey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options", true);
            Debug.Assert(regkey != null);
            // add to image file execution path
            if (isInstallation)
            {
                regkey = regkey.CreateSubKey(appImageExe);
                Debug.Assert(regkey != null, "regkey != null");
                regkey.SetValue("Debugger", "\"" + Assembly.GetExecutingAssembly().Location + "\"" +
                    (handlersFolder != null ? " -handlers \"" + handlersFolder + "\"" : String.Empty));
            }
            else
            {
                regkey.DeleteSubKey(appImageExe, false);
            }
        }
    }
}
