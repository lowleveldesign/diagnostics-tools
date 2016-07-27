using System;
using System.Configuration;
using System.IO;
using System.Messaging;
using NDesk.Options;

namespace MessagePusher
{
    public class Program
    {
        private readonly static byte[] header = new byte[6];

        // returns number of bytes to read
        private static int ReadMessageHeader(FileStream fileStream) {
            if (fileStream.Read(header, 0, header.Length) <= 0) {
                return 0;
            }
            // check header
            if (header[0] == 0x1 && header[1] == 0x2) {
                // number of bytes to read
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(header, 2, 4);

                return BitConverter.ToInt32(header, 2);
            }
            throw new ArgumentException("Invalid header.");
        }

        public static void Main(String[] args)
        {
            String infile = null;
            String queue = null;
            bool showhelp = false;

            var p = new OptionSet() {
                { "i|input=", "Output base file name.", v => infile = v },
                { "q|queue=", "Queue address.", v => queue = v },
                { "h|help",  "show this message and exit", v => showhelp = v != null }
            };

            try {
                p.Parse (args);
            } catch (OptionException e) {
                Console.WriteLine(e.Message);
                Console.WriteLine("Try --help for more information.");
                return;
            }

            if (showhelp || String.IsNullOrEmpty(infile) || String.IsNullOrEmpty(queue)) {
                Console.WriteLine("Options:");
                p.WriteOptionDescriptions(Console.Out);
                return;
            }

            if (!MessageQueue.Exists(queue)) {
                Console.WriteLine("Cannot connect to the provided queue. Please provide a queue name in format: MachineName\\Private$\\QueueName or any other accepted by MessageQueue class.");
                return;
            }

            int cnt = 1;
            var msmq = new MessageQueue(queue);
            while (true)  {
                String file = String.Format("{0}.{1}", infile, cnt++);
                if (!File.Exists(file)) {
                    break;
                }
                using (var fileStream = new FileStream(file, FileMode.Open)) {
                    int msglen;
                    while ((msglen = ReadMessageHeader(fileStream)) > 0) {
                        using (var tran = new MessageQueueTransaction()) {
                            try {
                                tran.Begin();
                                var msg = new Message();
                                WriteTo(fileStream, msg.BodyStream, msglen);
                                msmq.Send(msg, tran);
                                tran.Commit();
                            } catch (Exception ex) {
                                tran.Abort();
                                Console.WriteLine(ex);
                                throw;
                            }
                        }
                    }
                }
            }
        }

        public static void WriteTo(Stream sourceStream, Stream targetStream, int len)
        {
            byte[] buffer = new byte[len];
            int n = sourceStream.Read(buffer, 0, buffer.Length);
            targetStream.Write(buffer, 0, n);
        }
    }
}

