using System;
using System.IO;
using System.Text;

namespace LowLevelDesign.Hexify
{
    public static class Hex
    {
        private readonly static HexEncoder encoder = new HexEncoder();

        /// <summary>
        /// Returns hex representation of the byte array.
        /// </summary>
        /// <param name="data">bytes to encode</param>
        /// <returns></returns>
        public static string ToHexString(byte[] data)
        {
            return ToHexString(data, 0, data.Length);
        }

        /// <summary>
        /// Returns hex representation of the byte array.
        /// </summary>
        /// <param name="data">bytes to encode</param>
        /// <param name="off">offset</param>
        /// <param name="length">number of bytes to encode</param>
        /// <returns></returns>
        public static string ToHexString(byte[] data, int off, int length)
        {
            return Encoding.ASCII.GetString(Encode(data, off, length));
        }

        private static byte[] Encode(byte[] data, int off, int length)
        {
            using (var stream = new MemoryStream()) {
                encoder.Encode(data, off, length, stream);
                return stream.ToArray();
            }
        }

        /// <summary>
        /// Decodes hex representation to a byte array.
        /// </summary>
        /// <param name="hex">hex string to decode</param>
        /// <returns></returns>
        public static byte[] FromHexString(string hex)
        {
            using (var stream = new MemoryStream()) {
                encoder.DecodeString(hex, stream);
                return stream.ToArray();
            }
        }

        /// <summary>
        /// Returns a string containing a nice representation  of the byte array 
        /// (similarly to the binary editors). 
        /// <param name="bytes">array of bytes to pretty print</param>
        /// <returns></returns>
        public static string PrettyPrint(byte[] bytes)
        {
            return PrettyPrint(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// Returns a string containing a nice representation  of the byte array 
        /// (similarly to the binary editors). 
        /// 
        /// Example output:
        /// 
        /// 0000: c8 83 93 8f b0 cb cb d3 d1 e5 7c ff 52 dc ea 92  E....ËËÓNa.yRÜe.
        /// 0010: 5b af 30 ca d8 7a 35 e9 2e 46 fa 85 b7 38 3f 4e  [.0EOz5é.Fú.8?N
        /// 0020: 8d 60 af 4a 00 00 00 00 57 4d a4 29 35 9e c2 6f  ...J....WM.)5.Âo
        /// 0030: 30 7b 92 40 33 6d 55 43 46 fe d6 8d ef 67 99 9c  0{.@3mUCF?Ö.ig..
        /// </summary>
        /// <param name="bytes">array of bytes to pretty print</param>
        /// <param name="offset">offset in the array</param>
        /// <param name="length">number of bytes to print</param>
        /// <returns></returns>
        public static string PrettyPrint(byte[] bytes, int offset, int length)
        {
            if (bytes.Length == 0) {
                return string.Empty;
            }

            var buffer = new StringBuilder();
            int maxLength = offset + length;
            if (offset < 0 || offset >= bytes.Length || maxLength > bytes.Length)
            {
                throw new ArgumentException();
            }

            int end = Math.Min(offset + 16, maxLength);
            int start = offset;

            while (end <= maxLength) {
                // print offset 
                buffer.Append($"{(start - offset):x4}:");

                // print hex bytes
                for (int i = start; i < end; i++) {
                    buffer.Append($" {bytes[i]:x2}");
                }
                for (int i = 0; i < 16 - (end - start); i++) {
                    buffer.Append("   ");
                }

                buffer.Append("  ");
                // print ascii characters
                for (int i = start; i < end; i++) {
                    char c = (char)bytes[i];
                    if (char.IsLetterOrDigit(c) || char.IsPunctuation(c)) {
                        buffer.Append($"{c}");
                    } else {
                        buffer.Append(".");
                    }
                }

                if (end == maxLength) {
                    break;
                }

                start = end;
                end = Math.Min(end + 16, maxLength);
                buffer.AppendLine();
            }

            return buffer.ToString();
        }
    }
}
