using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using NDesk.Options;
using LowLevelDesign.Win32;
using System.Text;

namespace LowLevelDesign
{
    public static class ProcessGovernor
    {
        public static void Main(String[] args)
        {
            uint maxmem = 0;
            List<String> procargs = null;
            bool showhelp = false;
            int pid = 0;
			string profilerguid = null;

            var p = new OptionSet()
            {
                { "m|maxmem=", "Max committed memory usage in bytes (accepted suffixes: K, M or G).",
                    v => {
                        if (v == null) return;
                        if (v.EndsWith("K", StringComparison.OrdinalIgnoreCase)) {
                            maxmem = UInt32.Parse(v.Substring(0, v.Length - 1)) << 10;
                            return;
                        }
                        if (v.EndsWith("M", StringComparison.OrdinalIgnoreCase)) {
                            maxmem = UInt32.Parse(v.Substring(0, v.Length - 1)) << 20;
                            return;
                        }
                        if (v.EndsWith("G", StringComparison.OrdinalIgnoreCase)) {
                            maxmem = UInt32.Parse(v.Substring(0, v.Length - 1)) << 30;
                            return;
                        }
                        maxmem = UInt32.Parse(v); }},
				{ "profilerguid=", "Profiler GUID", v => profilerguid = v },
                { "p|pid=", "Attach to an already running process", (int v) => pid = v },
                { "h|help", "Show this message and exit", v => showhelp = v != null },
                { "?", "Show this message and exit", v => showhelp = v != null }
            };


            try {
                procargs = p.Parse(args);
            } catch (OptionException ex) {
                Console.Write("ERROR: invalid argument ");
                Console.WriteLine(ex.Message);
                Console.WriteLine();
                showhelp = true;
            } catch (FormatException) {
                Console.WriteLine("ERROR: invalid memory constraint");
                Console.WriteLine();
                showhelp = true;
            }

            if (!showhelp && (procargs.Count == 0 && pid == 0) || (pid > 0 && procargs.Count > 0)) {
                Console.WriteLine("ERROR: please provide either process name to start or PID of the already running process");
                Console.WriteLine();
                showhelp = true;
            }

            if (showhelp) {
                ShowHelp(p);
                return;
            }

            IntPtr hJob, hIOCP, hProcess;
            hJob = hIOCP = hProcess = IntPtr.Zero;
            PROCESS_INFORMATION pi = new PROCESS_INFORMATION();
            Thread listener = null;
            try {
                if (pid > 0) {
                    // open existing process
                    hProcess = CheckResult(ApiMethods.OpenProcess(ProcessAccessFlags.AllAccess, false, pid));
                } else {
                    // start suspended process
					if (profilerguid != null) {
						
					}
					Dictionary<string, string> additionalEnv = new Dictionary<string, string>();
					if (profilerguid != null) {
						additionalEnv["COR_ENABLE_PROFILING"] = "0x01";
						additionalEnv["COR_PROFILER"] = profilerguid;
					}
					
                    pi = StartSuspendedProcess(procargs, additionalEnv);
                    hProcess = pi.hProcess;
                }

                var securityAttributes = new SECURITY_ATTRIBUTES();
                securityAttributes.nLength = Marshal.SizeOf(securityAttributes);

                hJob = CheckResult(ApiMethods.CreateJobObject(ref securityAttributes, "procgov-" + Guid.NewGuid()));

                // create completion port
                hIOCP = CheckResult(ApiMethods.CreateIoCompletionPort(ApiMethods.INVALID_HANDLE_VALUE, IntPtr.Zero, IntPtr.Zero, 1));
                var assocInfo = new JOBOBJECT_ASSOCIATE_COMPLETION_PORT {
                    CompletionKey = IntPtr.Zero,
                    CompletionPort = hIOCP
                };
                uint size = (uint)Marshal.SizeOf(assocInfo);
                CheckResult(ApiMethods.SetInformationJobObject(hJob, JOBOBJECTINFOCLASS.AssociateCompletionPortInformation,
                        ref assocInfo, size));

                // start listening thread
                listener = new Thread(CompletionPortListener);
                listener.Start(hIOCP);

                if (maxmem > 0.0f) {
                    // configure constraints
                    var limitInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION {
                        BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION {
                            LimitFlags = JobInformationLimitFlags.JOB_OBJECT_LIMIT_PROCESS_MEMORY
                                        | JobInformationLimitFlags.JOB_OBJECT_LIMIT_BREAKAWAY_OK
                                        | JobInformationLimitFlags.JOB_OBJECT_LIMIT_SILENT_BREAKAWAY_OK
                        },
                        ProcessMemoryLimit = (UIntPtr)maxmem
                    };
                    size = (uint)Marshal.SizeOf(limitInfo);
                    CheckResult(ApiMethods.SetInformationJobObject(hJob, JOBOBJECTINFOCLASS.ExtendedLimitInformation,
                            ref limitInfo, size));
                }

                // assign a process to a job to apply constraints
                CheckResult(ApiMethods.AssignProcessToJobObject(hJob, hProcess));

                // resume process main thread (if it was started by us)
                if (pid == 0) {
                    CheckResult(ApiMethods.ResumeThread(pi.hThread));
                    // and we can close the thread handle
                    CloseHandle(pi.hThread);
                }

                if (ApiMethods.WaitForSingleObject(hProcess, ApiMethods.INFINITE) == 0xFFFFFFFF) {
                    throw new Win32Exception();
                }
            } finally {
                CloseHandle(hIOCP);
                // wait for the listener thread to finish
                if (listener != null && listener.IsAlive)
                    listener.Join();

                CloseHandle(hProcess);
                CloseHandle(hJob);
            }
        }

        static void ShowHelp(OptionSet p) {
            Console.WriteLine("Usage: procgov [OPTIONS] args");
            Console.WriteLine();
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
        }

        static PROCESS_INFORMATION StartSuspendedProcess(IList<String> procargs, Dictionary<string, string> additionalEnv) {
            PROCESS_INFORMATION pi;
            STARTUPINFO si = new STARTUPINFO();
			StringBuilder envEntries = new StringBuilder();
			
			foreach( var env in Environment.GetEnvironmentVariables().Keys) {
				if (additionalEnv.ContainsKey((string) env)) continue; // overwrite existing env
				envEntries.Append(env);
				envEntries.Append("=");
				envEntries.Append(Environment.GetEnvironmentVariable((string) env));
				envEntries.Append("\0");
			}

			foreach( string env in additionalEnv.Keys) {
				envEntries.Append(env);
				envEntries.Append("=");
				envEntries.Append(additionalEnv[env]);
				envEntries.Append("\0");
			}

			if (envEntries.Length < 1) envEntries.Append("\0");
			envEntries.Append("\0");
            CheckResult(ApiMethods.CreateProcess(null, String.Join(" ", procargs), IntPtr.Zero, IntPtr.Zero, false,
                        CreateProcessFlags.CREATE_SUSPENDED | CreateProcessFlags.CREATE_NEW_CONSOLE,
                        envEntries, null, ref si, out pi));
            return pi;
        }

        static void CompletionPortListener(Object o) {
            var hIOCP = (IntPtr)o;
            uint msgIdentifier;
            IntPtr pCompletionKey, lpOverlapped;

            while (ApiMethods.GetQueuedCompletionStatus(hIOCP, out msgIdentifier, out pCompletionKey,
                        out lpOverlapped, ApiMethods.INFINITE)) {
                if (msgIdentifier == (uint)JobMsgInfoMessages.JOB_OBJECT_MSG_NEW_PROCESS) {
                    Console.WriteLine("{0}: process {1} has started", msgIdentifier, (int)lpOverlapped);
                } else if (msgIdentifier == (uint)JobMsgInfoMessages.JOB_OBJECT_MSG_EXIT_PROCESS) {
                    Console.WriteLine("{0}: process {1} exited", msgIdentifier, (int)lpOverlapped);
                } else if (msgIdentifier == (uint)JobMsgInfoMessages.JOB_OBJECT_MSG_ACTIVE_PROCESS_ZERO) {
                    // nothing
                } else if (msgIdentifier == (uint)JobMsgInfoMessages.JOB_OBJECT_MSG_PROCESS_MEMORY_LIMIT) {
                    Console.WriteLine("{0}: process {1} exceeded its memory limit", msgIdentifier, (int)lpOverlapped);
                } else {
                    Console.WriteLine("Unknown message: {0}", msgIdentifier);
                }
            }
        }

        /* Win32 API helper methods */

        public static void CloseHandle(IntPtr handle) {
            if (handle != IntPtr.Zero) {
                ApiMethods.CloseHandle(handle);
            }
        }

        public static bool CheckResult(bool result) {
            if (!result) {
                throw new Win32Exception();
            }
            return result;
        }

        public static IntPtr CheckResult(IntPtr result) {
            if (result == IntPtr.Zero) {
                throw new Win32Exception();
            }
            return result;
        }

        public static int CheckResult(int result) {
            if (result == -1) {
                throw new Win32Exception();
            }
            return result;
        }
    }
}
