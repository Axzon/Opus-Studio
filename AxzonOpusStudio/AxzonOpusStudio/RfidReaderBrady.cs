/*
 * Copyright (c) 2024 RFMicron, Inc. dba Axzon Inc.
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
using System.Threading;
using NurApiDotNet;

namespace AxzonTempSensor
{
    //--------------------------------------------------
    class RfidReaderBrady : IRfidReader
    {
        private bool disposedValue;
        private NurApi reader;
        private NurApi.DeviceCapabilites deviceCaps;
        private int[] antennas;
        private AutoResetEvent connectionStatusChanged = new AutoResetEvent(false);

        private NurApi.InventoryExParams invParams = new NurApi.InventoryExParams();
        private NurApi.InventoryExFilter[] invFilters;
        private NurApi.InventoryExFilter onChipRssiFilter;
        private NurApi.InventoryExFilter temperatureFilter;
        private NurApi.InventoryExFilter batteryVolFilter;

        public RfidReaderBrady()
        {
            // Let's initialize first the USBTransport and SerialTransport.
            // TCPTransport is part of the NurApi assembly.
            NordicID.NurApi.USBTransport.Support.Init();
            NurApiDotNet.SerialTransport.Support.Init();
            reader = new NurApi();
            reader.ConnectedEvent += Reader_ConnectedEvent;
            reader.DisconnectedEvent += Reader_DisconnectedEvent;
            reader.ConnectionStatusEvent += Reader_ConnectionStatusEvent;
        }

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
        // ~NordicId()
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

        public int MaxNumberOfReadWords => 32;

        /// <summary>
        /// Connects to the RFID reader.
        /// </summary>
        /// <param name="comPortOrHostName">Name of the COM-port or the Host or the IP address</param>
        /// <param name="ipPortNumber">Port number</param>
        public void Connect(string comPortOrHostName, uint ipPortNumber = 0)
        {
            if (comPortOrHostName.StartsWith("ser://"))
            {
                reader.Connect(comPortOrHostName);
            }
            else if (comPortOrHostName.StartsWith("tcp://"))
            {
                reader.Connect($"{comPortOrHostName}:{ipPortNumber}");
            }
            else
            {
                reader.Connect($"tcp://{comPortOrHostName}:{ipPortNumber}");
            }
            while (connectionStatusChanged.WaitOne())
            {
                if (reader.ConnectionStatus == NurTransportStatus.Connected)
                    break;
                if (reader.ConnectionStatus == NurTransportStatus.Disconnected)
                    throw new Exception("Disconnected");
            }
        }

        /// <summary>
        /// Disconnects from the RFID reader.
        /// </summary>
        public void Disconnect()
        {
            reader.Disconnect();
        }

        public List<int> AvailableAntennas => reader.GetAntennaList().Select(antenna => antenna.AntennaId).ToList();

        public List<int> ConnectedAntennas => reader.EnabledAntennas;

        public List<int> Antennas
        {
            get => antennas.ToList();
            set => antennas = value.ToArray();
        }

        public void InitialSetup()
        {
            // Generic reader settings for the whole program
            reader.TxLevel = 0;
            reader.InventorySession = NurApi.SESSION_S0;
            reader.InventoryTarget = NurApi.INVTARGET_A;
            try
            {
                reader.RfProfile = NurApi.RFPROFILE_NOMINAL;
            }
            catch (NurApiException)
            {
                // Older devices (like a Stix and Sampo S1) do not support RF profiles
                reader.LinkFrequency = 256000;
                reader.RxDecoding = NurApi.RXDECODING_M2;
            }
            reader.CommTimeoutMilliSec = 500;
        }

        private int[] GetFreqs()
        {
            var regionInfo = reader.GetRegionInfo();
            int[] freqs = new int[regionInfo.channelCount];
            for (int i = 0; i < regionInfo.channelCount; i++)
            {
                freqs[i] = (int)(regionInfo.baseFrequency + (i * regionInfo.channelSpacing));
            }

            return freqs;
        }

        public int[] SetFccBand()
        {
            try
            {
                // Set region to FCC
                reader.Region = NurApi.REGIONID_FCC;
            }
            catch (NurApiException)
            {
                // The region may be locked
            }
            return GetFreqs();
        }

        public int[] SetEuBand()
        {
            try
            {
                // Set region to FCC
                reader.Region = NurApi.REGIONID_EU;
            }
            catch (NurApiException)
            {
                // The region may be locked
            }
            return GetFreqs();
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

            // Not implemented
            return freqsOverride;
        }

        public int[] UpdateFrequencyChannels(int[] channels)
        {
            // Not implemented
            return channels;
        }

        private double SetTxLevel(double powerdBm, bool readBack = false)
        {
            // Convert power (dBm) to TxLevel
            int txLevel = deviceCaps.maxTxdBm - (int)powerdBm;
            // TxLevel = 0 is the highest power
            if (txLevel < 0)
                txLevel = 0;
            else if (txLevel >= deviceCaps.txSteps)
                txLevel = deviceCaps.txSteps - 1;
            reader.TxLevel = txLevel;
            if (readBack)
            {
                return deviceCaps.maxTxdBm - (reader.TxLevel * deviceCaps.txAttnStep);
            }
            else
            {
                return 0.0;
            }
        }

        public double SetReadPower(double power, bool readBack = false)
        {
            return SetTxLevel(power, readBack);
        }

        public double SetWritePower(double power, bool readBack = false)
        {
            return SetTxLevel(power, readBack);
        }

        private double GetTxLevel()
        {
            return deviceCaps.maxTxdBm - (reader.TxLevel * deviceCaps.txAttnStep);
        }

        public double GetReadPower()
        {
            return GetTxLevel();
        }

        public double GetWritePower()
        {
            return GetTxLevel();
        }

        public ushort[] ReadTagMemByEPC(String epc, MemoryBank bank, uint wordAddress, int numWords, double[] rfPowers, int readTimeMs, int readAttempts, out ushort[] pcWords)
        {
            ushort[] dataWords = null;
            pcWords = null;
            byte[] epcBytes = NurApi.HexStringToBin(epc);

            reader.InventoryRead(true, NurApi.NUR_IR_EPCDATA, (byte)bank, wordAddress, (uint)numWords);
            foreach (double power in rfPowers)
            {
                SetReadPower(power);
                for (int a = 0; a < readAttempts; a++)
                {
                    reader.ClearTags();
                    reader.InventorySelectByEPC(1, 2, 0, false, epcBytes);
                    NurApi.TagStorage tagStorage = reader.FetchTags();
                    foreach (NurApi.Tag tag in tagStorage)
                    {
                        if (tag.GetEpcString() == epc)
                        {
                            ushort[] wordsRead = RfidUtility.ByteArrayToUshortArray(tag.irData);
                            if (wordsRead != null && wordsRead.Length == numWords)
                            {
                                dataWords = wordsRead;
                                pcWords = new ushort[] { tag.pc, tag.xpc_w1 };
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
            byte[] epcBytes = NurApi.HexStringToBin(epc);

            byte[] bytesToWrite = RfidUtility.UshortArrayToByteArray(wordsToWrite);
            reader.InventoryRead(true, NurApi.NUR_IR_EPCDATA, (byte)bank, wordAddress, (uint)wordsToWrite.Length);

            foreach (double power in rfPowers)
            {
                for (int a = 0; a < writeAttempts; a++)
                {
                    try
                    {
                        SetWritePower(power);
                        reader.WriteTagByEPC(0, false, epcBytes, (byte)bank, wordAddress, bytesToWrite);
                        SetReadPower(power);
                        reader.ClearTags();
                        reader.InventorySelectByEPC(1, 2, 0, false, epcBytes);
                        NurApi.TagStorage tagStorage = reader.FetchTags();
                        foreach (NurApi.Tag tag in tagStorage)
                        {
                            if (tag.GetEpcString() == epc)
                            {
                                ushort[] wordsRead = RfidUtility.ByteArrayToUshortArray(tag.irData);
                                if (wordsRead != null && wordsRead.Length == wordsToWrite.Length)
                                {
                                    pcWords = new ushort[] { tag.pc, tag.xpc_w1 };
                                    wordsReadBack = wordsRead;
                                    wasDataWritten = verifyReadBack ? wordsRead.SequenceEqual(wordsToWrite) : true;
                                    break;
                                }
                            }
                        }
                    }
                    catch (NurApiException)
                    {

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
            byte[] epcByteArray = NurApi.HexStringToBin(epc);
            if (epcByteArray == null || epcByteArray.Length == 0)
            {
                return false;
            }
            int numBits = epcByteArray.Length * 8;
            reader.InventoryRead(true, NurApi.NUR_IR_EPCDATA, NurApi.BANK_USER, 0x5, 1);

            bool bapSuccess = false;
            foreach (double power in rfPowers)
            {
                SetReadPower(power);
                for (int i = 1; i <= readAttempts; i++)
                {
                    reader.ClearTags();
                    reader.InventorySelect(1, 2, 0, false, NurApi.BANK_USER, 0xC0, numBits, epcByteArray);
                    NurApi.TagStorage tagStorage = reader.FetchTags();
                    foreach (NurApi.Tag tag in tagStorage)
                    {
                        if (epc == tag.GetEpcString())
                        {
                            ushort[] pcWords = new ushort[] { tag.pc, tag.xpc_w1 };
                            OpusState stateFromPacketPC = OpusStateEx.StateFromPcWordsToOpusState(pcWords);
                            if (stateFromPacketPC == OpusState.STANDBY || stateFromPacketPC == OpusState.BAP_MODE)
                            {
                                bapSuccess = true;
                                break;
                            }
                            ushort[] dataFromUserBank = RfidUtility.ByteArrayToUshortArray(tag.irData);
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
            byte[] epcByteArray = NurApi.HexStringToBin(epc);

            ushort newStoredPC = (ushort)(0x0500 | ((ushort)((epc.Length / 4) << 11))); // Write zero to bit 9 to re-use logger
            byte[] writeBytes = RfidUtility.UshortArrayToByteArray(new ushort[] { newStoredPC });

            foreach (double power in rfPowers)
            {
                SetWritePower(power);
                for (int a = 0; a < numAttempts; a++)
                {
                    try
                    {
                        reader.WriteTagByEPC(0, false, epcByteArray, NurApi.BANK_EPC, 1, writeBytes);
                    }
                    catch (NurApiException)
                    {
                    }
                }
            }
        }

        public void SetSetupForInventoryOpusTags(int initialQVal, bool includeSensorMeas)
        {
            // Create Filters
            // Activate On-Chip RSSI measurement with no filters
            onChipRssiFilter.maskData = new byte[2] { 0x0B, 0xE0 }; // Max = 31, Min = 0
            onChipRssiFilter.maskBitLength = onChipRssiFilter.maskData.Length * 8;
            onChipRssiFilter.address = 0x90;
            onChipRssiFilter.bank = NurApi.BANK_USER;
            onChipRssiFilter.action = NurApi.FACTION_1;
            onChipRssiFilter.target = NurApi.SESSION_SL;
            onChipRssiFilter.truncate = false;

            if (includeSensorMeas)
            {
                // Activate temperature measurement
                temperatureFilter.maskData = new byte[2] { 0x0B, 0xE0 }; // Max = 31, Min = 0
                temperatureFilter.maskBitLength = temperatureFilter.maskData.Length * 8;
                temperatureFilter.address = 0xB0;
                temperatureFilter.bank = NurApi.BANK_USER;
                temperatureFilter.action = NurApi.FACTION_1;
                temperatureFilter.target = NurApi.SESSION_SL;
                temperatureFilter.truncate = false;

                // Activate battery voltage measurement
                batteryVolFilter.maskData = new byte[2] { 0x0B, 0xE0 }; // Max = 31, Min = 0
                batteryVolFilter.maskBitLength = batteryVolFilter.maskData.Length * 8;
                batteryVolFilter.address = 0xD0;
                batteryVolFilter.bank = NurApi.BANK_USER;
                batteryVolFilter.action = NurApi.FACTION_1;
                batteryVolFilter.target = NurApi.SESSION_SL;
                batteryVolFilter.truncate = false;

                invFilters = new NurApi.InventoryExFilter[] { onChipRssiFilter, temperatureFilter, batteryVolFilter };
            }
            else
            {
                invFilters = new NurApi.InventoryExFilter[] { onChipRssiFilter };
            }

            // Configure invParams
            invParams.inventorySelState = NurApi.SELSTATE_SL;
            invParams.inventoryTarget = NurApi.INVTARGET_A;
            invParams.Q = initialQVal;
            invParams.rounds = 0;
            invParams.session = 0;
            invParams.transitTime = 0; // Disable

            // Configure data read
            reader.InventoryRead(true, NurApi.NUR_IR_EPCDATA, NurApi.BANK_USER, 0, 4);
        }

        public List<OpusTagInfo> InventoryOpusTags(int readTimeInMs, bool includeSensorMeas)
        {
            List<OpusTagInfo> tags = new List<OpusTagInfo>();
            reader.InventoryEx(invParams, invFilters);
            NurApi.TagStorage tagStorage = reader.FetchTags();
            foreach (var tag in tagStorage)
            {
                if (tag.epc.Length <= 0) continue; // Zero len EPCs are not allowed in this program
                if (tag.irData == null || tag.irData.Length != 8) continue;

                String epc = tag.GetEpcString();
                ushort[] sensorWords = RfidUtility.ByteArrayToUshortArray(tag.irData);
                ushort[] pcWords = new ushort[] { tag.pc, tag.xpc_w1 };
                OpusTagInfo tagInfo = new OpusTagInfo(tag.GetEpcString(), (int)tag.frequency, tag.rssi, sensorWords, pcWords, includeSensorMeas);
                if (!tagInfo.valid) continue;
                tags.Add(tagInfo);
            }

            return tags;
        }

        private void Reader_ConnectedEvent(object sender, NurApi.NurEventArgs e)
        {
            NurApi reader = (NurApi)sender;
            deviceCaps = reader.GetDeviceCaps();
            reader.ClearTagsEx();
            connectionStatusChanged.Set();
        }

        private void Reader_DisconnectedEvent(object sender, NurApi.NurEventArgs e)
        {
            NurApi reader = (NurApi)sender;
            connectionStatusChanged.Set();
        }

        private void Reader_ConnectionStatusEvent(object sender, NurTransportStatus e)
        {
            NurApi reader = (NurApi)sender;
            System.Diagnostics.Debug.WriteLine("Connection status: " + e.ToString());
            connectionStatusChanged.Set();
        }
    }
}
