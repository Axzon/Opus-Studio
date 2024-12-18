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
using System.Collections.Generic;
using System.Linq;
using ThingMagic;

namespace AxzonTempSensor
{
    //--------------------------------------------------
    class RfidReaderM6e : IRfidReader
    {
        private bool disposedValue;
        private Reader reader;
        private int[] antennas;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    reader.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~M6e()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        void IDisposable.Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public int MaxNumberOfReadWords => 64;

        public void Connect(string comPortOrHostName, uint ipPortNumber = 0)
        {
            if (comPortOrHostName.StartsWith("ser://"))
            {
                comPortOrHostName = "tmr:///" + comPortOrHostName.Substring(6);
                reader = Reader.Create(comPortOrHostName);
                reader.Connect();
            }
        }

        public void Disconnect()
        {
            // Do nothing
        }

        public List<int> AvailableAntennas => ((int[]) reader.ParamGet("/reader/antenna/portList")).OfType<int>().ToList();

        public List<int> ConnectedAntennas => ((int[]) reader.ParamGet("/reader/antenna/connectedPortList")).OfType<int>().ToList();

        public List<int> Antennas 
        {   
            get => antennas.ToList(); 
            set => antennas = value.ToArray(); 
        }

        public void InitialSetup()
        {
            // Generic reader settings for the whole program
            reader.ParamSet("/reader/radio/readPower", 15 * 100);
            reader.ParamSet("/reader/radio/writePower", 15 * 100);
            reader.ParamSet("/reader/gen2/session", Gen2.Session.S0);
            //reader.ParamSet("/reader/gen2/target", Gen2.Target.A);
            reader.ParamSet("/reader/gen2/BLF", Gen2.LinkFrequency.LINK250KHZ);  // Before tagEncoding
            reader.ParamSet("/reader/gen2/tagEncoding", Gen2.TagEncoding.M2);    // After BLF
            reader.ParamSet("/reader/gen2/tari", Gen2.Tari.TARI_25US);
            reader.ParamSet("/reader/gen2/sendSelect", true);
            reader.ParamSet("/reader/commandTimeout", 500);
            reader.ParamSet("/reader/transportTimeout", 500);
        }

        public int[] SetFccBand()
        {
            int[] freqsOverride1 = new int[]
            {
                918250, 923250, 913250, 905250, 923750, 912750, 918750, 926250,
                921250, 905750, 915250, 904750, 911250, 916750, 926750, 921750,
                913750, 925250, 910750, 916250, 922750, 904250, 917250, 909750,
                903750, 911750, 906250, 919750, 927250, 922250, 907250, 920750,
                909250, 925750, 920250, 914750, 908750, 924750, 915750, 910250,
                903250, 908250, 919250, 924250, 914250, 902750, 907750, 917750,
                906750, 912250
            };

            int[] freqsOverride2 = new int[]
            {
                902750, 903250, 903750, 904250, 904750, 905250, 905750, 906250, 906750, 907250,
                907750, 908250, 908750, 909250, 909750, 910250, 910750, 911250, 911750, 912250,
                912750, 913250, 913750, 914250, 914750, 915250, 915750, 916250, 916750, 917250,
                917750, 918250, 918750, 919250, 919750, 920250, 920750, 921250, 921750, 922250,
                922750, 923250, 923750, 924250, 924750, 925250, 925750, 926250, 926750, 927250
            };

            int[] freqsOverride3 = new int[]
            {
                902750
            };

            reader.ParamSet("/reader/region/id", Reader.Region.NA);
            int[] freqs = (int[])reader.ParamGet("/reader/region/hopTable");

            // Override
            //freqs = freqsOverride1;
            //readerX.ParamSet("/reader/region/hopTable", freqs);

            return freqs;
        }

        public int[] SetEuBand()
        {
            int[] freqsOverride = new int[]
            {
                865700, 866300, 866900, 867500
            };

            reader.ParamSet("/reader/region/id", Reader.Region.EU3);
            int[] freqs = (int[])reader.ParamGet("/reader/region/hopTable");

            // Override
            //freqs = freqsOverride;
            //readerX.ParamSet("/reader/region/hopTable", freqs);

            return freqs;
        }

        public int[] SetOpenBand()
        {
            int[] freqsOverride = new int[]
            {
                865000, 866000, 867000, 868000, 869000, 902000, 903000, 904000,
                905000, 906000, 907000, 908000, 909000, 910000, 911000, 912000,
                913000, 914000, 915000, 916000, 917000, 918000, 919000, 920000,
                921000, 922000, 923000, 924000, 925000, 926000, 927000, 928000
            };

            reader.ParamSet("/reader/region/id", Reader.Region.OPEN);
            int[] freqs = (int[])reader.ParamGet("/reader/region/hopTable");

            reader.ParamSet("/reader/region/lbt/enable", false);
            //readerX.ParamSet("/reader/region/lbtThreshold", 0);
            reader.ParamSet("/reader/region/dwellTime/enable", false);
            //readerX.ParamSet("/reader/region/dwellTime", 0);
            reader.ParamSet("/reader/region/hopTime", 3975);
            reader.ParamSet("/reader/region/quantizationStep", 100000);
            reader.ParamSet("/reader/region/minimumFrequency", 840000);

            // Override
            //freqs = freqsOverride;
            //readerX.ParamSet("/reader/region/hopTable", freqs);

            return freqs;
        }

        public int[] UpdateFrequencyChannels(int[] channels)
        {
            reader.ParamSet("/reader/region/hopTable", channels);
            return (int[]) reader.ParamGet("/reader/region/hopTable");
        }

        public double SetReadPower(double power, bool readBack = false)
        {
            int rfPower100 = int.Parse((power * 100).ToString());
            reader.ParamSet("/reader/radio/readPower", rfPower100);
            if (readBack)
            {
                rfPower100 = (int) reader.ParamGet("/reader/radio/readPower");
                return rfPower100 / 100.0;

            }
            else
            {
                return 0.0;
            }
        }

        public double SetWritePower(double power, bool readBack = false)
        {
            int rfPower100 = int.Parse((power * 100).ToString());
            reader.ParamSet("/reader/radio/writePower", rfPower100);
            if (readBack)
            {
                rfPower100 = (int)reader.ParamGet("/reader/radio/writePower");
                return rfPower100 / 100.0;
            }
            else
            {
                return 0.0;
            }
        }

        public double GetReadPower()
        {
            int rfPower100 = (int)reader.ParamGet("/reader/radio/readPower");
            return rfPower100 / 100.0;
        }

        public double GetWritePower()
        {
            int rfPower100 = (int)reader.ParamGet("/reader/radio/writePower");
            return rfPower100 / 100.0;
        }

        public ushort[] ReadTagMemByEPC(String epc, MemoryBank bank, uint wordAddress, int numWords, double[] rfPowers, int readTimeMs, int readAttempts, out ushort[] pcWords)
        {
            ushort[] dataWords = null;
            pcWords = null;

            // Create Read Plan
            Gen2.Select epcFilter = GetEPCSelectFilter(0, 4, epc);
            Gen2.ReadData readOp = new Gen2.ReadData(BankToMercury(bank), wordAddress, (byte) numWords);
            StopOnTagCount sotc = new StopOnTagCount { N = 1 };
            StopTriggerReadPlan readplan = new StopTriggerReadPlan(sotc, antennas, TagProtocol.GEN2, epcFilter, readOp, true);
            reader.ParamSet("/reader/read/plan", readplan);

            SetReadSettingsForSingleTag(400);

            foreach (double power in rfPowers)
            {
                SetReadPower(power);

                for (int a = 0; a < readAttempts; a++)
                {
                    TagReadData[] results = reader.Read(readTimeMs);
                    foreach (TagReadData tag in results)
                    {
                        String readEpc = tag.EpcString;
                        if (readEpc == epc)
                        {
                            byte[] dataBytes = tag.Data;
                            ushort[] wordsRead = RfidUtility.ByteArrayToUshortArray(dataBytes);
                            if (wordsRead != null && wordsRead.Length == numWords)
                            {
                                Gen2.TagData tData = (Gen2.TagData)tag.Tag;
                                dataWords = wordsRead;
                                pcWords = RfidUtility.ByteArrayToUshortArray(tData.PcBytes);
                                break;
                            }
                        }
                    }
                    if (dataWords != null) break;
                }
                if (dataWords != null) break;
            }

            return dataWords;
        }

        public bool WriteTagMemByEPC(String epc, MemoryBank bank, uint wordAddress, ushort[] wordsToWrite, double[] rfPowers, int writeAttempts, bool verifyReadBack, out ushort[] wordsReadBack, out ushort[] pcWords)
        {
            bool wasDataWritten = false;
            wordsReadBack = null;
            pcWords = null;

            // Create Write and Read Plan
            Gen2.Select epcFilter = GetEPCSelectFilter(0, 4, epc);
            Gen2.WriteData wData = new Gen2.WriteData(BankToMercury(bank), wordAddress, wordsToWrite);
            Gen2.ReadData rData = new Gen2.ReadData(BankToMercury(bank), wordAddress, (byte)wordsToWrite.Length);
            TagOpList tagopList = new TagOpList();
            tagopList.list.Add(wData);
            tagopList.list.Add(rData);
            StopOnTagCount sotc = new StopOnTagCount { N = 1 };
            StopTriggerReadPlan srp = new StopTriggerReadPlan(sotc, antennas, TagProtocol.GEN2, epcFilter, tagopList, false);
            reader.ParamSet("/reader/read/plan", srp);

            SetReadSettingsForSingleTag(400);

            foreach (double power in rfPowers)
            {
                SetReadPower(power);
                SetWritePower(power);

                for (int a = 0; a < writeAttempts; a++)
                {
                    TagReadData[] results = reader.Read(100);
                    foreach (TagReadData tag in results)
                    {
                        if (tag.EpcString == epc)
                        {
                            byte[] dataBytes = tag.Data;
                            ushort[] wordsRead = RfidUtility.ByteArrayToUshortArray(dataBytes);
                            if (wordsRead != null && wordsRead.Length == wordsToWrite.Length)
                            {
                                Gen2.TagData tData = (Gen2.TagData)tag.Tag;
                                pcWords = RfidUtility.ByteArrayToUshortArray(tData.PcBytes);
                                wordsReadBack = wordsRead;
                                wasDataWritten = verifyReadBack ? wordsRead.SequenceEqual(wordsToWrite) : true;
                                break;
                            }
                        }
                    }
                    if (wasDataWritten) break;
                }
                if (wasDataWritten) break;
            }
            return wasDataWritten;
        }

        public bool SetBapMode(String epc, double[] rfPowers, int readTimeMs, int readAttempts)
        {
            // Create Filter
            byte[] epcByteArray = RfidUtility.StringInHexToByteArray(epc);
            if (epcByteArray == null || epcByteArray.Length == 0)
            {
                return false;
            }
            int numBits = epcByteArray.Length * 8;
            Gen2.Select setBapFilter = CreateGen2Select(0, 4, Gen2.Bank.USER, 0xC0, numBits, epcByteArray);

            // Create Read Plan
            Gen2.ReadData operation = new Gen2.ReadData(Gen2.Bank.USER, 0x5, (byte)1); 
            StopOnTagCount sotc = new StopOnTagCount { N = 1 };
            StopTriggerReadPlan config = new StopTriggerReadPlan(sotc, antennas, TagProtocol.GEN2, setBapFilter, operation, false);
            reader.ParamSet("/reader/read/plan", config);

            SetReadSettingsForSingleTag(50000); // T4=50ms to give time for Bap to activate

            bool bapSuccess = false;
            foreach (double power in rfPowers)
            {
                SetReadPower(power);
                for (int i = 1; i <= readAttempts; i++)
                {
                    TagReadData[] results = reader.Read(readTimeMs);

                    foreach (TagReadData tag in results)
                    {
                        if (epc == tag.EpcString)
                        {
                            Gen2.TagData tData = (Gen2.TagData)tag.Tag;
                            ushort[] pcWords = RfidUtility.ByteArrayToUshortArray(tData.PcBytes);
                            OpusState stateFromPacketPC = OpusStateEx.StateFromPcWordsToOpusState(pcWords);
                            if (stateFromPacketPC == OpusState.STANDBY || stateFromPacketPC == OpusState.BAP_MODE)
                            {
                                bapSuccess = true;
                                break;
                            }
                            byte[] dataBytes = tag.Data;
                            ushort[] dataFromUserBank = RfidUtility.ByteArrayToUshortArray(dataBytes);
                            OpusState stateFromUserBank = OpusStateEx.StateFromUserBankToOpusState(dataFromUserBank);
                            if (stateFromUserBank == OpusState.STANDBY || stateFromUserBank == OpusState.BAP_MODE)
                            {
                                bapSuccess = true;
                                break;
                            }
                        }
                    }
                    if (bapSuccess) break;
                }
                if (bapSuccess) break;
            }

            return bapSuccess;
        }

        public void TransitionFromFinishedToStandby(String epc, double[] rfPowers, int numAttempts)
        {
            // Create Filter
            Gen2.Select epcFilter = GetEPCSelectFilter(0, 4, epc);

            // Create Write and Read Plan
            ushort newStoredPC = (ushort)(0x0500 | ((ushort)((epc.Length / 4) << 11))); // Write zero to bit 9 to re-use logger
            ushort[] writeWords = new ushort[] { newStoredPC };
            Gen2.WriteData wData = new Gen2.WriteData(Gen2.Bank.EPC, 0x01, writeWords);
            Gen2.ReadData rData = new Gen2.ReadData(Gen2.Bank.EPC, 0x01, 1);
            TagOpList tagopList = new TagOpList();
            tagopList.list.Add(wData);
            tagopList.list.Add(rData);
            StopOnTagCount sotc = new StopOnTagCount { N = 1 };
            StopTriggerReadPlan srp = new StopTriggerReadPlan(sotc, antennas, TagProtocol.GEN2, epcFilter, tagopList, false);
            reader.ParamSet("/reader/read/plan", srp);

            SetReadSettingsForSingleTag(500);

            foreach (double power in rfPowers)
            {
                SetReadPower(power);
                SetWritePower(power);

                for (int a = 0; a < numAttempts; a++)
                {
                    TagReadData[] results = reader.Read(200);
                    foreach (TagReadData tag in results)
                    {
                        String readEpc = tag.EpcString;
                        if (readEpc == epc)
                        {
                            byte[] dataBytes = tag.Data;
                            ushort[] wordsRead = RfidUtility.ByteArrayToUshortArray(dataBytes);
                            // wordsRead can not confirm if the operation was succesful because bit 9 is XI when it is read
                            break;
                        }
                    }
                }
            }
        }

        public void SetSetupForInventoryOpusTags(int initialQVal, bool includeSensorMeas)
        {
            // Create Filters
            Gen2.Select ocrssiFilter = CreateGen2Select(4, 1, Gen2.Bank.USER, 0x90, 16, new byte[] { 0x0B, 0xE0 }); // Max = 31, Min = 0
            Gen2.Select batteryFilter;
            Gen2.Select temperatureFilter;
            MultiFilter filters;
            if (includeSensorMeas)
            {
                batteryFilter = CreateGen2Select(4, 1, Gen2.Bank.USER, 0xD0, 16, new byte[] { 0x0F, 0xE0 });
                temperatureFilter = CreateGen2Select(4, 1, Gen2.Bank.USER, 0xB0, 16, new byte[] { 0x0F, 0xE0 });
                filters = new MultiFilter(new Gen2.Select[] { ocrssiFilter, batteryFilter, temperatureFilter }); //temperatureFilter
            }
            else
            {
                filters = new MultiFilter(new Gen2.Select[] { ocrssiFilter });
            }

            // Create Read Plan
            Gen2.ReadData operation = new Gen2.ReadData(Gen2.Bank.USER, 0x0, (byte)4);
            SimpleReadPlan config = new SimpleReadPlan(antennas, TagProtocol.GEN2, filters, operation, false);
            reader.ParamSet("/reader/read/plan", config);

            SetReadSettingsForMultipleTags((uint)(includeSensorMeas ? 24000 : 400), initialQVal);
        }

        public List<OpusTagInfo> InventoryOpusTags(int readTimeInMs, bool includeSensorMeas)
        {
            List<OpusTagInfo> tags = new List<OpusTagInfo>();
            TagReadData[] results = reader.Read(readTimeInMs);

            foreach (TagReadData tag in results)
            {
                String epc = tag.EpcString;
                if (epc.Length <= 0) continue; // Zero len EPCs are not allowed in this program
                byte[] dataBytes = tag.Data;
                if (dataBytes == null || dataBytes.Length != 8) continue;
                ushort[] sensorWords = RfidUtility.ByteArrayToUshortArray(dataBytes);
                Gen2.TagData tData = (Gen2.TagData)tag.Tag;
                ushort[] pcWords = RfidUtility.ByteArrayToUshortArray(tData.PcBytes);
                OpusTagInfo tagInfo = new OpusTagInfo(epc, tag.Frequency, tag.Rssi, sensorWords, pcWords, includeSensorMeas);
                if (!tagInfo.valid) continue;
                tags.Add(tagInfo);
            }

            return tags;
        }

        private static Gen2.Select CreateGen2Select(int target, int action, Gen2.Bank bank, int pointer, int length, byte[] mask)
        {
            Gen2.Select select = new Gen2.Select(false, bank, (uint)pointer, (ushort)length, mask);
            switch (target)
            {
                case 0:
                    select.target = Gen2.Select.Target.Inventoried_S0;
                    break;
                case 1:
                    select.target = Gen2.Select.Target.Inventoried_S1;
                    break;
                case 2:
                    select.target = Gen2.Select.Target.Inventoried_S2;
                    break;
                case 3:
                    select.target = Gen2.Select.Target.Inventoried_S3;
                    break;
                case 4:
                    select.target = Gen2.Select.Target.Select;
                    break;
                default:
                    throw new ArgumentException("invalid target value");
            }
            switch (action)
            {
                case 0:
                    select.action = Gen2.Select.Action.ON_N_OFF;
                    break;
                case 1:
                    select.action = Gen2.Select.Action.ON_N_NOP;
                    break;
                case 2:
                    select.action = Gen2.Select.Action.NOP_N_OFF;
                    break;
                case 3:
                    select.action = Gen2.Select.Action.NEG_N_NOP;
                    break;
                case 4:
                    select.action = Gen2.Select.Action.OFF_N_ON;
                    break;
                case 5:
                    select.action = Gen2.Select.Action.OFF_N_NOP;
                    break;
                case 6:
                    select.action = Gen2.Select.Action.NOP_N_ON;
                    break;
                case 7:
                    select.action = Gen2.Select.Action.NOP_N_NEG;
                    break;
                default:
                    throw new ArgumentException("invalid action value");
            }
            return select;
        }

        private Gen2.Select GetEPCSelectFilter(int target, int action, string epc)
        {
            byte[] EpcByteArray = RfidUtility.StringInHexToByteArray(epc);
            if (EpcByteArray == null) return null;
            int numBits = EpcByteArray.Length * 8;
            return CreateGen2Select(target, action, Gen2.Bank.EPC, 32, numBits, EpcByteArray);
        }

        private void SetReadSettingsForSingleTag(UInt32 t4_inUs)
        {
            reader.ParamSet("/reader/gen2/target", Gen2.Target.B);
            reader.ParamSet("/reader/gen2/t4", t4_inUs);
            reader.ParamSet("/reader/gen2/q", new Gen2.StaticQ(2));
            reader.ParamSet("/reader/gen2/initQ", new Gen2.InitQ() { qEnable = false, initialQ = 2 });
        }

        private void SetReadSettingsForMultipleTags(UInt32 t4_inUs, int initialQVal)
        {
            reader.ParamSet("/reader/gen2/target", Gen2.Target.A);
            reader.ParamSet("/reader/gen2/t4", t4_inUs);
            reader.ParamSet("/reader/gen2/q", new Gen2.DynamicQ());
            reader.ParamSet("/reader/gen2/initQ", new Gen2.InitQ() { qEnable = true, initialQ = initialQVal });
        }

        private Gen2.Bank BankToMercury(MemoryBank bank)
        {
            switch (bank)
            {
                case MemoryBank.RESERVED: 
                    return Gen2.Bank.RESERVED;
                case MemoryBank.EPC:
                    return Gen2.Bank.EPC;
                case MemoryBank.TID:
                    return Gen2.Bank.TID;
                case MemoryBank.USER:
                    return Gen2.Bank.USER;
                default:
                    throw new Exception("Unknown Memory Bank: " + bank.ToString());                
            }
        }
    }

}
