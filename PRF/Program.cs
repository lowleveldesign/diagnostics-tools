using NDesk.Options;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace PRF
{
    class Program
    {
        static void Main(string[] args)
        {
            string label = null, secretFilePath = null, dataFilePath = null, outputFilePath = null;
            int length = 0;
            bool showHelp = false;
            string tls = "TLS1.0", sha = "256";

            var p = new OptionSet {
                { "l|label=", "label",  v => label = v },
                { "s|secret=", "a path to a binary file containing secret", v => secretFilePath = v },
                { "d|data=", "a path to a binary file conatining data", v => dataFilePath = v },
                { "n|length=", "length of the KDK to generate", (int v) => length = v },
                { "o|output=", "a path to a binary file which will contain the generated key", v => outputFilePath = v },
                { "h|help", "show help usage", v => showHelp = v != null },
                { "t|tls=", "TLS version: TLS1.0, TLS1.1, TLS1.2", v => tls = v },
                { "sha=", "SHA length (only for TLS1.2)", v => sha = v }
            };

            try {
                p.Parse(args);
            } catch (OptionException e) {
                Console.WriteLine("prf: ");
                Console.WriteLine(e.Message);
                Console.WriteLine("Try 'prf --help' for more information.");
                return;
            }
            if (showHelp) {
                Console.WriteLine("Usage: prf [OPTIONS]+");
                Console.WriteLine("A tool to generate PRF output used in TLS transmissions");
                Console.WriteLine();
                Console.WriteLine("Options:");
                p.WriteOptionDescriptions(Console.Out);
                return;
            }
            if (label == null || secretFilePath == null || dataFilePath == null || outputFilePath == null || length == 0) {
                Console.WriteLine("ERROR: one of the parameters is missing (all are required). Try 'prf --help' for more information.");
                return;
            }

            if (!File.Exists(secretFilePath)) {
                Console.WriteLine("ERROR: secret file does not exist.");
                return;
            }
            if (!File.Exists(dataFilePath)) {
                Console.WriteLine("ERROR: data file does not exist.");
                return;
            }


            try {
                File.WriteAllBytes(outputFilePath, PRF(File.ReadAllBytes(secretFilePath), label,
                    File.ReadAllBytes(dataFilePath), length, tls, sha));
            } catch (Exception ex) {
                Console.WriteLine("ERROR: generating PRF failed with exception: {0}", ex.Message);
            }
        }

        public static byte[] PRF(byte[] secret, string label, byte[] data, int length, 
            string tls, string sha)
        {
            /* Secret Length calc exmplain from the RFC2246. Section 5
			 * 
			 * S1 and S2 are the two halves of the secret and each is the same
			 * length. S1 is taken from the first half of the secret, S2 from the
			 * second half. Their length is created by rounding up the length of the
			 * overall secret divided by two; thus, if the original secret is an odd
			 * number of bytes long, the last byte of S1 will be the same as the
			 * first byte of S2.
			 */

            // split secret in 2
            int secretLen = secret.Length >> 1;
            // rounding up
            if ((secret.Length & 0x1) == 0x1)
                secretLen++;

            // Seed
            TlsStream seedStream = new TlsStream();
            seedStream.Write(Encoding.ASCII.GetBytes(label));
            seedStream.Write(data);
            byte[] seed = seedStream.ToArray();
            seedStream.Reset();

            byte[] masterSecret;
            if ("TLS1.2".Equals(tls, StringComparison.OrdinalIgnoreCase)) {
                masterSecret = Expand("SHA" + sha, secret, seed, length);
            } else {
                // Secret 1
                byte[] secret1 = new byte[secretLen];
                Buffer.BlockCopy(secret, 0, secret1, 0, secretLen);

                // Secret2
                byte[] secret2 = new byte[secretLen];
                Buffer.BlockCopy(secret, (secret.Length - secretLen), secret2, 0, secretLen);

                // Secret 1 processing
                byte[] p_md5 = Expand("MD5", secret1, seed, length);

                // Secret 2 processing
                byte[] p_sha = Expand("SHA1", secret2, seed, length);

                // Perfor XOR of both results
                masterSecret = new byte[length];
                for (int i = 0; i < masterSecret.Length; i++) {
                    masterSecret[i] = (byte)(p_md5[i] ^ p_sha[i]);
                }
            }

            return masterSecret;
        }

        public static byte[] Expand(string hashName, byte[] secret, byte[] seed, int length)
        {
            HMAC hmac;
            switch (hashName)
            {
                case "MD5":
                    hmac = new HMACMD5(secret);
                    break;
                case "SHA1":
                    hmac = new HMACSHA1(secret);
                    break;
                case "SHA384":
                    hmac = new HMACSHA384(secret);
                    break;
                case "SHA512":
                    hmac = new HMACSHA512(secret);
                    break;
                default: // SHA256
                    hmac = new HMACSHA256(secret);
                    break;
            }

            int hashLength = hmac.HashSize >> 3; // length in bytes
            int iterations = (int)(length / hashLength);
            if ((length % hashLength) > 0) {
                iterations++;
            }

            TlsStream resMacs = new TlsStream();

            byte[][] hmacs = new byte[iterations + 1][];
            hmacs[0] = seed;
            for (int i = 1; i <= iterations; i++) {
                TlsStream hcseed = new TlsStream();
                hmac.ComputeHash(hmacs[i - 1], 0, hmacs[i - 1].Length);
                hmacs[i] = hmac.Hash;
                hcseed.Write(hmacs[i]);
                hcseed.Write(seed);
                hmac.ComputeHash(hcseed.ToArray(), 0, (int)hcseed.Length);
                resMacs.Write(hmac.Hash);
                hcseed.Reset();
            }

            byte[] res = new byte[length];

            Buffer.BlockCopy(resMacs.ToArray(), 0, res, 0, res.Length);

            resMacs.Reset();

            return res;
        }
    }
}
