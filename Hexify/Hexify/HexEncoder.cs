using System;
using System.IO;

namespace LowLevelDesign.Hexify
{
    /**
     * Class imported from BouncyCastle library. 
     * 
     * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED 
     * TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL 
     * THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION 
     * OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE 
     * OR OTHER DEALINGS IN THE SOFTWARE. 
     */
    public class HexEncoder
    {
        protected readonly byte[] encodingTable =
        {
            (byte)'0', (byte)'1', (byte)'2', (byte)'3', (byte)'4', (byte)'5', (byte)'6', (byte)'7',
            (byte)'8', (byte)'9', (byte)'a', (byte)'b', (byte)'c', (byte)'d', (byte)'e', (byte)'f'
        };

        /*
         * set up the decoding table.
         */
        protected readonly byte[] decodingTable = new byte[128];

        private static void FillArray(byte[] buf, byte b)
        {
            int i = buf.Length;
            while (i > 0) {
                buf[--i] = b;
            }
        }

        protected void InitialiseDecodingTable()
        {
            FillArray(decodingTable, (byte)0xff);

            for (int i = 0; i < encodingTable.Length; i++) {
                decodingTable[encodingTable[i]] = (byte)i;
            }

            decodingTable['A'] = decodingTable['a'];
            decodingTable['B'] = decodingTable['b'];
            decodingTable['C'] = decodingTable['c'];
            decodingTable['D'] = decodingTable['d'];
            decodingTable['E'] = decodingTable['e'];
            decodingTable['F'] = decodingTable['f'];
        }

        public HexEncoder()
        {
            InitialiseDecodingTable();
        }

        /**
        * encode the input data producing a Hex output stream.
        *
        * @return the number of bytes produced.
        */
        public int Encode(byte[] data, int off, int length, Stream outStream)
        {
            for (int i = off; i < (off + length); i++) {
                int v = data[i];

                outStream.WriteByte(encodingTable[v >> 4]);
                outStream.WriteByte(encodingTable[v & 0xf]);
            }

            return length * 2;
        }

        private static bool Ignore(char c)
        {
            return c == '\n' || c == '\r' || c == '\t' || c == ' ';
        }

        /**
        * decode the Hex encoded byte data writing it to the given output stream,
        * whitespace characters will be ignored.
        *
        * @return the number of bytes produced.
        */
        public int Decode(byte[] data, int off, int length, Stream outStream)
        {
            byte b1, b2;
            int outLen = 0;
            int end = off + length;

            while (end > off) {
                if (!Ignore((char)data[end - 1])) {
                    break;
                }

                end--;
            }

            int i = off;
            while (i < end) {
                while (i < end && Ignore((char)data[i])) {
                    i++;
                }

                b1 = decodingTable[data[i++]];

                while (i < end && Ignore((char)data[i])) {
                    i++;
                }

                b2 = decodingTable[data[i++]];

                if ((b1 | b2) >= 0x80)
                    throw new IOException("invalid characters encountered in Hex data");

                outStream.WriteByte((byte)((b1 << 4) | b2));

                outLen++;
            }

            return outLen;
        }

        /**
        * decode the Hex encoded string data writing it to the given output stream,
        * whitespace characters will be ignored.
        *
        * @return the number of bytes produced.
        */
        public int DecodeString(string data, Stream outStream)
        {
            byte b1, b2;
            int length = 0;

            int end = data.Length;

            while (end > 0) {
                if (!Ignore(data[end - 1])) {
                    break;
                }

                end--;
            }

            int i = 0;
            while (i < end) {
                while (i < end && Ignore(data[i])) {
                    i++;
                }

                b1 = decodingTable[data[i++]];

                while (i < end && Ignore(data[i])) {
                    i++;
                }

                b2 = decodingTable[data[i++]];

                if ((b1 | b2) >= 0x80)
                    throw new IOException("invalid characters encountered in Hex data");

                outStream.WriteByte((byte)((b1 << 4) | b2));

                length++;
            }

            return length;
        }
    }
}
