using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace epsnoop
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length != 1 || !int.TryParse(args[0], out var pid))
            {
                Console.WriteLine("Usage: epsnoop <pid>");
                return;
            }

            using var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (o, ev) => { ev.Cancel = true; cts.Cancel(); };

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                await StartProxyWindows(pid, cts.Token);
            }
            else
            {
                await StartProxyUnix(pid, cts.Token);
            }
        }

        private static async Task StartProxyUnix(int pid, CancellationToken ct)
        {
            var tmp = Path.GetTempPath();
            var snoopedEndpointPath = Directory.GetFiles(tmp, $"dotnet-diagnostic-{pid}-*-socket").First();
            var snoopingEndpointPath = Path.Combine(tmp, $"dotnet-diagnostic-{pid}-1-socket");

            File.Delete(snoopingEndpointPath);

            var endpoint = new UnixDomainSocketEndPoint(snoopingEndpointPath);
            using var listenSocket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Unspecified);
            listenSocket.Bind(endpoint);

            using var r = ct.Register(() => listenSocket.Close());

            try
            {
                var id = 1;
                while (!ct.IsCancellationRequested)
                {
                    listenSocket.Listen();

                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }

                    var socket = await listenSocket.AcceptAsync();
                    Console.WriteLine($"[{id}]: s1 connected");

                    // random remote socket
                    var senderSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                    await senderSocket.ConnectAsync(new UnixDomainSocketEndPoint(snoopedEndpointPath));
                    Console.WriteLine($"[{id}]: s2 connected");

                    _ = SniffData(new NetworkStream(socket, true), new NetworkStream(senderSocket, true), id, ct);
                    id += 1;
                }
            }
            catch (SocketException)
            {
                /* cancelled listen */
                Console.WriteLine($"Stopped ({snoopingEndpointPath})");
            }
            finally
            {
                File.Delete(snoopingEndpointPath);
            }
        }

        private static async Task StartProxyWindows(int pid, CancellationToken ct)
        {
            var targetPipeName = $"dotnet-diagnostic-{pid}";
            var explorer = Process.GetProcessesByName("explorer").First();
            var pipeName = $"dotnet-diagnostic-{explorer.Id}";
            try
            {
                var id = 1;
                while (!ct.IsCancellationRequested)
                {
                    var listener = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 10, PipeTransmissionMode.Byte,
                                            PipeOptions.Asynchronous, 0, 0);
                    await listener.WaitForConnectionAsync(ct);
                    Console.WriteLine($"[{id}]: s1 connected");

                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }
                    var sender = new NamedPipeClientStream(".", targetPipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                    await sender.ConnectAsync();
                    Console.WriteLine($"[{id}]: s2 connected");

                    _ = SniffData(listener, sender, id, ct);
                    id += 1;
                }
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine($"Stopped ({pipeName})");
            }
        }

        private static async Task SniffData(Stream s1, Stream s2, int id, CancellationToken ct)
        {
            var outstream = File.Create($"eventpipes.{id}.data");
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var tasks = new List<Task>() {
                    Forward(s1, s2, outstream, $"{id}: s1 -> s2", cts.Token),
                    Forward(s2, s1, outstream, $"{id}: s2 -> s1", cts.Token)
                };

                var t = await Task.WhenAny(tasks);

                var ind = tasks.IndexOf(t);
                Console.WriteLine($"[{id}]: s{ind + 1} disconnected");
                tasks.RemoveAt(ind);

                cts.Cancel();

                await Task.WhenAny(tasks);
                Console.WriteLine($"[{id}]: s{1 - ind + 1} disconnected");
            }
            catch (TaskCanceledException) { }
            finally
            {
                outstream.Close();
                s1.Dispose();
                s2.Dispose();
            }
        }

        private static async Task Forward(Stream sin, Stream sout, Stream snoop, string id, CancellationToken ct)
        {
            var buffer = new byte[1024];
            while (true)
            {
                var read = await sin.ReadAsync(buffer, 0, buffer.Length, ct);
                if (read == 0)
                {
                    break;
                }
                Console.WriteLine($"[{id}] read: {read}");
                snoop.Write(buffer, 0, read);
                await sout.WriteAsync(buffer, 0, read, ct);
            }
        }
    }
}

