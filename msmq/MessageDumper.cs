using System;
using System.Configuration;
using System.IO;
using System.Messaging;
using NDesk.Options;

namespace MessageDumper
{
    public class Program
    {
        private static readonly byte[] header = new byte[] { 0x1, 0x2 };

        public static void Main(String[] args)
        {
            String outfile = null;
            String queue = null;
            bool showhelp = false;
            int batchSize = 1000, numberOfFiles = 0;

            var p = new OptionSet() {
                { "b|batch=", "How many messages should be read into one file.", (int v) => batchSize = v },
                { "o|output=", "Base name for the output file. It will have numbers appended", v => outfile = v },
                { "n|filecnt=", "Number of files after which the program should stop. If 0 (default) it will finish after queue is empty", (int v) => numberOfFiles = v },
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

            if (showhelp || String.IsNullOrEmpty(outfile) || String.IsNullOrEmpty(queue) || batchSize <= 0) {
                Console.WriteLine("Options:");
                p.WriteOptionDescriptions(Console.Out);
                return;
            }

            if (!MessageQueue.Exists(queue)) {
                Console.WriteLine("Cannot connect to the provided queue. Please provide a queue name in format: MachineName\\Private$\\QueueName or any other accepted by MessageQueue class.");
                return;
            }

            int fcnt = 0;
            var msmq = new MessageQueue(queue);
            using (var msgHeadersWriter = new StreamWriter(String.Format("{0}.headers", outfile))) {
                while (numberOfFiles == 0 || fcnt < numberOfFiles) {
                    fcnt++;
                    var dumpFileName = String.Format("{0}.{1}", outfile, fcnt);
                    using (var fileStream = new FileStream(dumpFileName, FileMode.OpenOrCreate)) {
                        using (var memoryStream = new MemoryStream()) {
                            int cnt = 0;
                            while (cnt++ < batchSize) {
                                using (var tran = new MessageQueueTransaction()) {
                                    try {
                                        tran.Begin();
                                        var msg = msmq.Receive(TimeSpan.FromSeconds(10), tran);

                                        // log message header (for now only id)
                                        msgHeadersWriter.Write("FILE='{0}', OFFSET='{1}', MSGID='{2}' - ",
                                                                  dumpFileName, fileStream.Position, msg.Id);

                                        WriteTo(msg.BodyStream, memoryStream);
                                        WriteMessageHeader(fileStream, (int)memoryStream.Length);
                                        memoryStream.Position = 0; // set position to 0
                                        WriteTo(memoryStream, fileStream);
                                        memoryStream.SetLength(0);
                                        tran.Commit();

                                        msgHeadersWriter.WriteLine("SUCCESS");
                                    } catch (Exception ex) {
                                        tran.Abort();
                                        var mex = ex as MessageQueueException;
                                        if (mex != null && mex.MessageQueueErrorCode == MessageQueueErrorCode.IOTimeout) {
                                            return; // everything is ok
                                        }
                                        msgHeadersWriter.WriteLine("FAILED");
                                        throw;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void WriteMessageHeader(FileStream stream, int length)
        {
            stream.Write(header, 0, header.Length);
            byte[] b = BitConverter.GetBytes(length);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(b);
            stream.Write(b, 0, b.Length);
        }

        private static byte[] buffer = new byte[0x10000];
        public static void WriteTo(Stream sourceStream, Stream targetStream)
        {
            int n;
            while ((n = sourceStream.Read(buffer, 0, buffer.Length)) != 0)
                targetStream.Write(buffer, 0, n);
        }
    }
}

