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

            var (s1, s2) = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ?
                                await CreateProxyWindows(pid, cts.Token) :
                                await CreateProxyUnix(pid, cts.Token);

            using (var outstream = File.Create("eventpipes.data"))
            {
                await SniffData(s1, s2, outstream, cts.Token);
            }
        }

        private static async Task<(Stream, Stream)> CreateProxyUnix(int pid, CancellationToken ct)
        {
            var tmp = Path.GetTempPath();
            var snoopedEndpointPath = Directory.GetFiles(tmp, $"dotnet-diagnostic-{pid}-*-socket").First();
            var snoopingEndpointPath = Path.Combine(tmp, $"dotnet-diagnostic-{pid}-1-socket");

            File.Delete(snoopingEndpointPath);

            var endpoint = new UnixDomainSocketEndPoint(snoopingEndpointPath);
            using var listenSocket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Unspecified);
            listenSocket.Bind(endpoint);

            using (var _ = ct.Register(() => listenSocket.Close()))
            {
                listenSocket.Listen();
            }

            if (ct.IsCancellationRequested)
            {
                return (Stream.Null, Stream.Null);
            }

            var socket = await listenSocket.AcceptAsync();
            Console.WriteLine($"[0]: connected");

            // random remote socket
            var senderSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            await senderSocket.ConnectAsync(new UnixDomainSocketEndPoint(snoopedEndpointPath));
            Console.WriteLine($"[1]: connected");

            return (new NetworkStream(socket, true), new NetworkStream(senderSocket, true));
        }

        private static async Task<(Stream, Stream)> CreateProxyWindows(int pid, CancellationToken ct)
        {
            var explorer = Process.GetProcessesByName("explorer").First();
            var pipeName = $"dotnet-diagnostic-{explorer.Id}";
            var listener = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                                    PipeOptions.Asynchronous, 0, 0);

            await listener.WaitForConnectionAsync(ct);
            Console.WriteLine("[0]: connected");

            if (ct.IsCancellationRequested)
            {
                return (Stream.Null, Stream.Null);
            }
            var senderPipeName = $"dotnet-diagnostic-{pid}";
            var sender = new NamedPipeClientStream(".", senderPipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await sender.ConnectAsync();
            Console.WriteLine("[1]: connected");

            return (listener, sender);
        }

        private static async Task SniffData(Stream s1, Stream s2, Stream snoop, CancellationToken ct)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var tasks = new List<Task>() {
                Forward(s1, s2, snoop, "s1 -> s2", cts.Token),
                Forward(s2, s1, snoop, "s2 -> s1", cts.Token)
            };

            var t = await Task.WhenAny(tasks);

            var ind = tasks.IndexOf(t);
            Console.WriteLine($"[{ind}]: disconnected");
            tasks.RemoveAt(ind);

            cts.Cancel();

            await Task.WhenAny(tasks);
            Console.WriteLine($"[{1 - ind}]: disconnected");

            s1.Dispose();
            s2.Dispose();
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

