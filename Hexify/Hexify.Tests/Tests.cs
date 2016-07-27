using LowLevelDesign.Hexify;
using System.Text;
using Xunit;

namespace Hexify.Tests
{
    public class Tests
    {
        [Fact]
        public void TestByteArrayOutput()
        {
            byte[] testArray = { 0x01, 0x02, 0x03, 0x04, 0xFF, 0xFE, 0xFD };
            Assert.Equal("01020304fffefd", Hex.ToHexString(testArray));

            testArray = new byte[0];
            Assert.Equal("", Hex.ToHexString(testArray));

            var testString = "Zbożowa arystokracja";
            var hex = Hex.ToHexString(Encoding.UTF8.GetBytes(testString));
            Assert.Equal(testString, Encoding.UTF8.GetString(
                Hex.FromHexString(hex)));
        }

        [Fact]
        public void TestNicePrint()
        {
            byte[] data = {
                0x17, 0x6F, 0x59, 0x00, 0x00, 0x0A, 0x00, 0x02, 0x7B, 0x1B, 0x00, 0x00, 0x04, 0x72, 0xC7, 0x0A,
                0x00, 0x70, 0x6F, 0x4A, 0x00, 0x00, 0x0A, 0x00, 0x02, 0x7B, 0x1D, 0x00, 0x00, 0x04, 0x1F, 0x64,
                0x1F, 0x25, 0x73, 0x54, 0x00
            };
            string expectedOutput = "0000: 17 6f 59 00 00 0a 00 02 7b 1b 00 00 04 72 c7 0a  .oY.....{....rÇ.\r\n" +
                "0010: 00 70 6f 4a 00 00 0a 00 02 7b 1d 00 00 04 1f 64  .poJ.....{.....d\r\n" +
                "0020: 1f 25 73 54 00                                   .%sT.";
            Assert.Equal(expectedOutput, Hex.PrettyPrint(data));

            data = new byte[0];
            Assert.Equal("", Hex.PrettyPrint(data));

            data = new byte[] { 0x17, 0x6F, 0x59, 0x00, 0x00 };
            expectedOutput = "0000: 17 6f 59 00 00                                   .oY..";
            Assert.Equal(expectedOutput, Hex.PrettyPrint(data));

            data = new byte[] {
                0x17, 0x6F, 0x59, 0x00, 0x00, 0x0A, 0x00, 0x02, 0x7B, 0x1B, 0x00, 0x00, 0x04, 0x72, 0xC7, 0x0A,
                0x00, 0x70, 0x6F, 0x4A, 0x00, 0x00, 0x0A, 0x00, 0x02, 0x7B, 0x1D, 0x00, 0x00, 0x04, 0x1F, 0x64
            };
            expectedOutput = "0000: 17 6f 59 00 00 0a 00 02 7b 1b 00 00 04 72 c7 0a  .oY.....{....rÇ.\r\n" +
                "0010: 00 70 6f 4a 00 00 0a 00 02 7b 1d 00 00 04 1f 64  .poJ.....{.....d";
            Assert.Equal(expectedOutput, Hex.PrettyPrint(data));
            expectedOutput = "0000: 6f 59 00 00 0a 00 02 7b 1b 00 00 04 72 c7 0a 00  oY.....{....rÇ..\r\n" +
                "0010: 70 6f 4a 00 00 0a 00 02 7b 1d 00 00              poJ.....{...";
            Assert.Equal(expectedOutput, Hex.PrettyPrint(data, 1, data.Length - 4));
            expectedOutput = "0000: 00 70 6f 4a 00 00 0a 00 02 7b 1d 00 00 04 1f     .poJ.....{.....";
            Assert.Equal(expectedOutput, Hex.PrettyPrint(data, 16, data.Length - 16 - 1));
        }
    }
}
