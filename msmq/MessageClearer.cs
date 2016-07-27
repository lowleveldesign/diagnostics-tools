using System;
using System.Configuration;
using System.IO;
using System.Messaging;
using NDesk.Options;

namespace MessageClearer
{
    public class Program
    {
        private static readonly byte[] header = new byte[] { 0x1, 0x2 };

        public static void Main(String[] args)
        {
            String queue = null;
            bool showhelp = false;

            var p = new OptionSet() {
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

            if (showhelp || String.IsNullOrEmpty(queue)) {
                Console.WriteLine("Options:");
                p.WriteOptionDescriptions(Console.Out);
                return;
            }

            if (!MessageQueue.Exists(queue)) {
                Console.WriteLine("Cannot connect to the provided queue. Please provide a queue name in format: MachineName\\Private$\\QueueName or any other accepted by MessageQueue class.");
                return;
            }

            var msmq = new MessageQueue(queue);
            int cnt = 0;
            while (true) {
                using (var tran = new MessageQueueTransaction()) {
                    try {
                        tran.Begin();
                        msmq.Receive(TimeSpan.FromSeconds(10), tran);
                        tran.Commit();
                        cnt++;
                    } catch (Exception ex) {
                        tran.Abort();
                        var mex = ex as MessageQueueException;
                        if (mex != null && mex.MessageQueueErrorCode == MessageQueueErrorCode.IOTimeout) {
                            break; // everything is ok
                        }
                        throw;
                    }
                }
            }
            Console.WriteLine("Cleared: {0} messages", cnt);
        }
    }
}

