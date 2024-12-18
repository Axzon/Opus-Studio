/*
 * Copyright (c) 2023 RFMicron, Inc. dba Axzon Inc.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */
using System;

namespace AxzonTempSensor
{
    public class RfidUtility
    {
        public static byte[] StringInHexToByteArray(string str)
        {
            if (str == null || str.Length == 0) return new byte[0];
            if (str.Length % 2 != 0) return null; // It should be interpreted as an error
            int numBytes = str.Length / 2;
            byte[] byteArray = new byte[numBytes];
            for (int i = 0; i < numBytes; i++)
            {
                byteArray[i] = Convert.ToByte(str.Substring(i * 2, 2), 16);
            }
            return byteArray;
        }

        public static byte[] UshortArrayToByteArray(ushort[] shortArray)
        {
            if (shortArray == null) return null;
            byte[] byteArray = new byte[shortArray.Length * 2];
            for (int i = 0; i < shortArray.Length; i++)
            {
                byteArray[2 * i] = (byte)((shortArray[i] >> 8) & 0xFF);
                byteArray[2 * i + 1] = (byte)(shortArray[i] & 0xFF);
            }
            return byteArray;
        }

        public static ushort[] ByteArrayToUshortArray(byte[] byteArray)
        {
            if (byteArray == null) return null;
            ushort[] shortArray = new ushort[byteArray.Length / 2];
            for (int i = 0; i < shortArray.Length; i++)
            {
                shortArray[i] = (ushort)(((byteArray[2 * i] & 0x00FF) << 8) | (byteArray[2 * i + 1] & 0x00FF));
            }
            return shortArray;
        }

        // EPC Gen2 CRC-16 Algorithm
        // Poly = 0x1021; Initial Value = 0xFFFF; XOR Output;
        public static ushort Crc16(byte[] inputBytes)
        {
            ushort crcVal = 0xFFFF;
            foreach (byte inputByte in inputBytes)
            {
                crcVal = (ushort)(crcVal ^ (((uint)inputByte) << 8));
                for (int i = 0; i < 8; i++)
                {
                    if ((crcVal & 0x8000) == 0x8000)
                    {
                        crcVal = (ushort)((crcVal << 1) ^ 0x1021);
                    }
                    else
                    {
                        crcVal = (ushort)(crcVal << 1);
                    }
                }
                crcVal = (ushort)(crcVal & 0xffff);
            }
            crcVal = (ushort)(crcVal ^ 0xffff);
            return crcVal;
        }

        public static ushort Crc16(ushort[] inputUshorts)
        {
            return Crc16(UshortArrayToByteArray(inputUshorts));
        }
    }
}
