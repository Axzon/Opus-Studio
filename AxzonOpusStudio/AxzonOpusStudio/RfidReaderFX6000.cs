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
using System.Threading;
using Symbol.RFID3;
using ThingMagic;

namespace AxzonTempSensor
{
    //-----------------------------------------------------
    class RfidReaderFX6000 : IRfidReader
    {
        private bool disposedValue;
        private RFIDReader reader;
        private int[] antennas;
        private AutoResetEvent AccessComplete;
        private List<OpusTagInfo> tagInfos = new List<OpusTagInfo>();
        
        // Variables that handle Reader's reads expecting one tag read
        private List<ushort> newDataWords = new List<ushort>();
        private List<ushort> newPcWords = new List<ushort>();
        private MEMORY_BANK requestedBankRead = MEMORY_BANK.MEMORY_BANK_RESERVED;
        private uint requestedBankWordAddress = 0;
        private int requestedNumberOfWords = 0;
        private String requestedEPC;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                   // reader.Dispose(); // Commented because Zebra's API throws an error. This is probably a bug in the API
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // Override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~FX6000()
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

        public int MaxNumberOfReadWords => 128;

        public void Connect(string comPortOrHostName, uint ipPortNumber = 0)
        {
            if (comPortOrHostName.StartsWith("tcp://"))
            {
                comPortOrHostName = comPortOrHostName.Substring(6);
                reader = new RFIDReader(comPortOrHostName, ipPortNumber, 2000);
                reader.Connect();

                // Register for Read Notification
                reader.Events.ReadNotify += Events_ReadNotify;
                reader.Events.AttachTagDataWithReadEvent = false;

                // Register for Status Notification
                reader.Events.StatusNotify += Events_StatusNotify;
                reader.Events.NotifyAccessStartEvent = true;
                reader.Events.NotifyAccessStopEvent = true;

                // Create Event to signify access sequence operation complete
                AccessComplete = new AutoResetEvent(false);

                tagInfos.Clear();
            }
        }

        public void Disconnect()
        {
            // Unregister for Read Notification
            reader.Events.ReadNotify -= Events_ReadNotify;

            // Unregister for Status Notification
            reader.Events.StatusNotify -= Events_StatusNotify;
            reader.Events.NotifyAccessStartEvent = false;
            reader.Events.NotifyAccessStopEvent = false;

            reader.Disconnect();
        }

        public List<int> AvailableAntennas
        {
            get
            {
                ushort[] antennas = reader.Config.Antennas.AvailableAntennas;
                int[] intAnt = Array.ConvertAll(antennas, val => checked((int)val));
                return intAnt.OfType<int>().ToList();
            }
        }

        public List<int> ConnectedAntennas
        {
            get
            {
                List<int> conAnts = new List<int>();
                List<int> availAnt = AvailableAntennas;
                foreach (int ant in availAnt)
                {
                    if (reader.Config.Antennas[ant].GetPhysicalProperties().IsConnected)
                    {
                        conAnts.Add(ant);
                    }
                }

                return conAnts;
            }
        }

        public List<int> Antennas
        {
            get => antennas.ToList();
            set => antennas = value.ToArray();
        }

        public void InitialSetup()
        {
            foreach (int ant in antennas)
            {
                Antennas.Config antConfig = reader.Config.Antennas[ant].GetConfig();
                antConfig.TransmitPowerIndex = PowerInDbmToTransmitPowerIndex(15.0); // 15.0 dBm
                reader.Config.Antennas[ant].SetConfig(antConfig);

                Antennas.RFMode rfMode = reader.Config.Antennas[ant].GetRFMode();
                rfMode.TableIndex = 3; // Mode3: Link Freq = 120kHz, Miller2, Tari=25us, PIE = 2.0
                reader.Config.Antennas[ant].SetRFMode(rfMode);

                // TODO: Add ReadPoint stuff?
            }

            // Enable larger read operations of the user bank (logged data)
            TagStorageSettings settings = reader.Config.GetTagStorageSettings();
            settings.MaxSizeMemoryBank = 256; // 128 words
            reader.Config.SetTagStorageSettings(settings);
        }

        public int[] SetFccBand()
        {
            // TODO: Update. For now it always returns the same info for all bands
            return GetFrequencyValues();
        }

        public int[] SetEuBand()
        {
            // TODO: Update. For now it always returns the same info for all bands
            return GetFrequencyValues();
        }

        public int[] SetOpenBand()
        {
            // TODO: Update. For now it always returns the same info for all bands
            return GetFrequencyValues();
        }

        public int[] UpdateFrequencyChannels(int[] channels)
        {
            // TODO: Update. For now it always returns the same info for all bands
            return GetFrequencyValues();
        }

        public double SetReadPower(double power, bool readBack = false)
        {
            double readPower = -100;

            foreach (int ant in antennas)
            {
                Antennas.Config antConfig = reader.Config.Antennas[ant].GetConfig();
                antConfig.TransmitPowerIndex = PowerInDbmToTransmitPowerIndex(power);
                reader.Config.Antennas[ant].SetConfig(antConfig);
                if (readBack)
                {
                    // TODO: do something regarding all antenna ports
                    readPower = TransmitPowerIndexToPowerInDbm(reader.Config.Antennas[ant].GetConfig().TransmitPowerIndex);
                }
            }

            return readPower;
        }

        public double SetWritePower(double power, bool readBack = false)
        {
            double writePower = -100;

            foreach (int ant in antennas)
            {
                Antennas.Config antConfig = reader.Config.Antennas[ant].GetConfig();
                antConfig.TransmitPowerIndex = PowerInDbmToTransmitPowerIndex(power);
                reader.Config.Antennas[ant].SetConfig(antConfig);
                if (readBack)
                {
                    // TODO: do something regarding all antenna ports
                    writePower = TransmitPowerIndexToPowerInDbm(reader.Config.Antennas[ant].GetConfig().TransmitPowerIndex);
                }
            }

            return writePower;
        }

        public double GetReadPower()
        {
            double readPower = -100;

            foreach (int ant in antennas)
            {
                // TODO: do something regarding all antenna ports
                readPower = TransmitPowerIndexToPowerInDbm(reader.Config.Antennas[ant].GetConfig().TransmitPowerIndex);
            }

            return readPower;
        }

        public double GetWritePower()
        {
            double writePower = -100;

            foreach (int ant in antennas)
            {
                // TODO: do something regarding all antenna ports
                writePower = TransmitPowerIndexToPowerInDbm(reader.Config.Antennas[ant].GetConfig().TransmitPowerIndex);
            }

            return writePower;
        }

        public ushort[] ReadTagMemByEPC(String epc, MemoryBank bank, uint wordAddress, int numWords, double[] rfPowers, int readTimeMs, int readAttempts, out ushort[] pcWords)
        {
            requestedBankRead = BankToRFID3(bank);
            requestedBankWordAddress = wordAddress;
            requestedNumberOfWords = numWords;
            requestedEPC = epc;

            reader.Actions.PreFilters.DeleteAll();
            foreach (int ant in antennas)
            {
                // Add EPC filter
                PreFilters.PreFilter epcFilter = new PreFilters.PreFilter();
                epcFilter.AntennaID = (ushort)ant;
                epcFilter.TagPattern = RfidUtility.StringInHexToByteArray(epc);
                epcFilter.TagPatternBitCount = (uint) (epc.Length * 4);
                epcFilter.BitOffset = 32;
                epcFilter.MemoryBank = MEMORY_BANK.MEMORY_BANK_EPC;
                //epcFilter.FilterAction = FILTER_ACTION.FILTER_ACTION_STATE_AWARE;
                //epcFilter.StateAwareAction.Target = TARGET.TARGET_SL;
                //epcFilter.StateAwareAction.Action = STATE_AWARE_ACTION.STATE_AWARE_ACTION_ASRT_SL_NOT_DSRT_SL;

                epcFilter.FilterAction = FILTER_ACTION.FILTER_ACTION_STATE_UNAWARE;
                epcFilter.StateAwareAction.Target = TARGET.TARGET_INVENTORIED_STATE_S0;
                epcFilter.StateAwareAction.Action = STATE_AWARE_ACTION.STATE_AWARE_ACTION_INV_B_NOT_INV_A;

                reader.Actions.PreFilters.Add(epcFilter);

                // Singulation Control
                Antennas.SingulationControl sc = new Antennas.SingulationControl();
                sc.Session = SESSION.SESSION_S0;
                sc.TagPopulation = 2; 
                //sc.Action.PerformStateAwareSingulationAction = true;
                //sc.Action.SLFlag = SL_FLAG.SL_FLAG_ASSERTED;
                //sc.Action.InventoryState = INVENTORY_STATE.INVENTORY_STATE_AB_FLIP; //It has to be AB_FLIP, otherwise the filters don't work

                sc.Action.PerformStateAwareSingulationAction = false;
                sc.Action.SLFlag = SL_FLAG.SL_ALL;
                sc.Action.InventoryState = INVENTORY_STATE.INVENTORY_STATE_B; 

                reader.Config.Antennas[ant].SetSingulationControl(sc);
            }

            // add operation sequence: Read Memory
            reader.Actions.TagAccess.OperationSequence.DeleteAll();
            TagAccess.Sequence.Operation op1 = new TagAccess.Sequence.Operation();
            op1.AccessOperationCode = ACCESS_OPERATION_CODE.ACCESS_OPERATION_READ;
            op1.ReadAccessParams.MemoryBank = BankToRFID3(bank);
            op1.ReadAccessParams.AccessPassword = 0;
            op1.ReadAccessParams.ByteOffset = 2 * wordAddress;
            op1.ReadAccessParams.ByteCount = (uint) (2 * numWords); 
            reader.Actions.TagAccess.OperationSequence.Add(op1);

            // Configure trigger
            TriggerInfo triggerInfo = new TriggerInfo();
            triggerInfo.StartTrigger.Type = START_TRIGGER_TYPE.START_TRIGGER_TYPE_IMMEDIATE;
            triggerInfo.StopTrigger.Type = STOP_TRIGGER_TYPE.STOP_TRIGGER_TYPE_TAG_OBSERVATION_WITH_TIMEOUT;
            triggerInfo.StopTrigger.TagObservation.N = 1;
            triggerInfo.StopTrigger.TagObservation.Timeout = (uint) readTimeMs;

            foreach (double power in rfPowers)
            {
                SetReadPower(power);

                for (int a = 0; a < readAttempts; a++)
                {
                    // perform access sequence
                    AntennaInfo antennaInfo = new AntennaInfo();
                    antennaInfo.AntennaID = Array.ConvertAll(antennas, val => checked((ushort)val));
                    newDataWords.Clear();
                    newPcWords.Clear();
                    reader.Actions.TagAccess.OperationSequence.PerformSequence(null, triggerInfo, antennaInfo);

                    // wait for access operation complete
                    AccessComplete.WaitOne();

                    // get the access operation result
                    uint successCount = 0;
                    uint failureCount = 0;
                    reader.Actions.TagAccess.GetLastAccessResult(ref successCount, ref failureCount);

                    if (newDataWords.Count == requestedNumberOfWords && newPcWords.Count == 2) break;
                }
                if (newDataWords.Count == requestedNumberOfWords && newPcWords.Count == 2) break;
            }

            if (newDataWords.Count == requestedNumberOfWords && newPcWords.Count == 2)
            {
                pcWords = newPcWords.ToArray<ushort>();
                return newDataWords.ToArray<ushort>();
            }
            else
            {
                pcWords = null;
                return null;
            }
        }

        public bool WriteTagMemByEPC(String epc, MemoryBank bank, uint wordAddress, ushort[] wordsToWrite, double[] rfPowers, int writeAttempts, bool verifyReadBack, out ushort[] wordsReadBack, out ushort[] pcWords)
        {
            wordsReadBack = ReadTagMemByEPC(epc, bank, wordAddress, wordsToWrite.Length, rfPowers, 300, writeAttempts, out pcWords);
            if (wordsReadBack == null || wordsReadBack.Length != wordsToWrite.Length) return false;
            if (wordsReadBack.SequenceEqual(wordsToWrite)) return true;

            // Try to write words that are different from the requested values
            reader.Actions.PreFilters.DeleteAll();
            foreach (int ant in antennas)
            {
                // Add EPC filter
                PreFilters.PreFilter epcFilter = new PreFilters.PreFilter();
                epcFilter.AntennaID = (ushort)ant;
                epcFilter.TagPattern = RfidUtility.StringInHexToByteArray(epc);
                epcFilter.TagPatternBitCount = (uint)(epc.Length * 4);
                epcFilter.BitOffset = 32;
                epcFilter.MemoryBank = MEMORY_BANK.MEMORY_BANK_EPC;
                //epcFilter.FilterAction = FILTER_ACTION.FILTER_ACTION_STATE_AWARE;
                //epcFilter.StateAwareAction.Target = TARGET.TARGET_SL;
                //epcFilter.StateAwareAction.Action = STATE_AWARE_ACTION.STATE_AWARE_ACTION_ASRT_SL_NOT_DSRT_SL;

                epcFilter.FilterAction = FILTER_ACTION.FILTER_ACTION_STATE_UNAWARE;
                epcFilter.StateAwareAction.Target = TARGET.TARGET_INVENTORIED_STATE_S0;
                epcFilter.StateAwareAction.Action = STATE_AWARE_ACTION.STATE_AWARE_ACTION_INV_B_NOT_INV_A;

                reader.Actions.PreFilters.Add(epcFilter);

                // Singulation Control
                Antennas.SingulationControl sc = new Antennas.SingulationControl();
                sc.Session = SESSION.SESSION_S0;
                sc.TagPopulation = 2;
                //sc.Action.PerformStateAwareSingulationAction = true;
                //sc.Action.SLFlag = SL_FLAG.SL_FLAG_ASSERTED;
                //sc.Action.InventoryState = INVENTORY_STATE.INVENTORY_STATE_AB_FLIP; //It has to be AB_FLIP, otherwise the filters don't work

                sc.Action.PerformStateAwareSingulationAction = false;
                sc.Action.SLFlag = SL_FLAG.SL_ALL;
                sc.Action.InventoryState = INVENTORY_STATE.INVENTORY_STATE_B; 

                reader.Config.Antennas[ant].SetSingulationControl(sc);
            }

            // Configure trigger
            TriggerInfo triggerInfo = new TriggerInfo();
            triggerInfo.StartTrigger.Type = START_TRIGGER_TYPE.START_TRIGGER_TYPE_IMMEDIATE;
            triggerInfo.StopTrigger.Type = STOP_TRIGGER_TYPE.STOP_TRIGGER_TYPE_TAG_OBSERVATION_WITH_TIMEOUT;
            triggerInfo.StopTrigger.TagObservation.N = 1;
            triggerInfo.StopTrigger.TagObservation.Timeout = (uint)300;

            foreach (double power in rfPowers)
            {
                for (int a = 0; a < writeAttempts; a++)
                {
                    SetWritePower(power);
                    for (int wordNum = 0; wordNum < wordsToWrite.Length; wordNum++)
                    {
                        if (wordsReadBack[wordNum] == wordsToWrite[wordNum]) continue;

                        // add operation sequence: Write Memory
                        reader.Actions.TagAccess.OperationSequence.DeleteAll();
                        TagAccess.Sequence.Operation op1 = new TagAccess.Sequence.Operation();
                        op1.AccessOperationCode = ACCESS_OPERATION_CODE.ACCESS_OPERATION_WRITE;
                        op1.WriteAccessParams.AccessPassword = 0;
                        op1.WriteAccessParams.MemoryBank = BankToRFID3(bank);
                        op1.WriteAccessParams.ByteOffset = (uint) (2 * (wordAddress + wordNum));
                        op1.WriteAccessParams.WriteDataLength = 2; // FX9600 can only write one word at a time (this might be a bug)
                        op1.WriteAccessParams.WriteData = RfidUtility.UshortArrayToByteArray(new ushort[1] { wordsToWrite[wordNum] } );
                        reader.Actions.TagAccess.OperationSequence.Add(op1);

                        // add operation sequence: Read Memory
                        //TagAccess.Sequence.Operation op2 = new TagAccess.Sequence.Operation();
                        //op2.AccessOperationCode = ACCESS_OPERATION_CODE.ACCESS_OPERATION_READ;
                        //op2.ReadAccessParams.MemoryBank = BankToRFID3(bank);
                        //op2.ReadAccessParams.AccessPassword = 0;
                        //op2.ReadAccessParams.ByteOffset = (uint)(2 * (wordAddress + wordNum));
                        //op2.ReadAccessParams.ByteCount = 2;
                        //reader.Actions.TagAccess.OperationSequence.Add(op2);

                        //requestedBankRead = BankToRFID3(bank);
                        //requestedBankWordAddress = (uint) (wordAddress + wordNum);
                        //requestedNumberOfWords = 1;
                        //requestedEPC = epc;
                        //newDataWords.Clear();
                        //newPcWords.Clear();

                        // perform access sequence
                        AntennaInfo antennaInfo = new AntennaInfo();
                        antennaInfo.AntennaID = Array.ConvertAll(antennas, val => checked((ushort)val));
                        reader.Actions.TagAccess.OperationSequence.PerformSequence(null, triggerInfo, antennaInfo);

                        // wait for access operation complete
                        AccessComplete.WaitOne();

                        // get the access operation result
                        uint successCount = 0;
                        uint failureCount = 0;
                        reader.Actions.TagAccess.GetLastAccessResult(ref successCount, ref failureCount);
                    }

                    wordsReadBack = ReadTagMemByEPC(epc, bank, wordAddress, wordsToWrite.Length, rfPowers, 300, writeAttempts, out pcWords);
                    if (wordsReadBack == null) return false;
                    if (wordsReadBack.SequenceEqual(wordsToWrite)) return true;
                }
            }

            return false;
        }

        public bool SetBapMode(String epc, double[] rfPowers, int readTimeMs, int readAttempts)
        {
            requestedBankRead = MEMORY_BANK.MEMORY_BANK_USER;
            requestedBankWordAddress = 0x05;
            requestedNumberOfWords = 1;
            requestedEPC = epc;

            reader.Actions.PreFilters.DeleteAll();
            foreach (int ant in antennas)
            {
                // Add EPC filter
                PreFilters.PreFilter epcFilter = new PreFilters.PreFilter();
                epcFilter.AntennaID = (ushort)ant;
                epcFilter.TagPattern = RfidUtility.StringInHexToByteArray(epc);
                epcFilter.TagPatternBitCount = (uint)(epc.Length * 4);
                epcFilter.BitOffset = 0xC0;
                epcFilter.MemoryBank = MEMORY_BANK.MEMORY_BANK_USER;
                //epcFilter.FilterAction = FILTER_ACTION.FILTER_ACTION_STATE_AWARE;
                //epcFilter.StateAwareAction.Target = TARGET.TARGET_SL;
                //epcFilter.StateAwareAction.Action = STATE_AWARE_ACTION.STATE_AWARE_ACTION_ASRT_SL_NOT_DSRT_SL;

                epcFilter.FilterAction = FILTER_ACTION.FILTER_ACTION_STATE_UNAWARE;
                epcFilter.StateAwareAction.Target = TARGET.TARGET_INVENTORIED_STATE_S0;
                epcFilter.StateAwareAction.Action = STATE_AWARE_ACTION.STATE_AWARE_ACTION_INV_B_NOT_INV_A;

                reader.Actions.PreFilters.Add(epcFilter);

                // Singulation Control
                Antennas.SingulationControl sc = new Antennas.SingulationControl();
                sc.Session = SESSION.SESSION_S0;
                sc.TagPopulation = 2;
               // sc.Action.PerformStateAwareSingulationAction = true;
                //sc.Action.SLFlag = SL_FLAG.SL_FLAG_ASSERTED;
                //sc.Action.InventoryState = INVENTORY_STATE.INVENTORY_STATE_AB_FLIP; //It has to be AB_FLIP, otherwise the filters don't work

                sc.Action.PerformStateAwareSingulationAction = false;
                sc.Action.SLFlag = SL_FLAG.SL_ALL;
                sc.Action.InventoryState = INVENTORY_STATE.INVENTORY_STATE_B; 

                reader.Config.Antennas[ant].SetSingulationControl(sc);
            }

            // add operation sequence: Read Memory
            reader.Actions.TagAccess.OperationSequence.DeleteAll();
            TagAccess.Sequence.Operation op1 = new TagAccess.Sequence.Operation();
            op1.AccessOperationCode = ACCESS_OPERATION_CODE.ACCESS_OPERATION_READ;
            op1.ReadAccessParams.MemoryBank = MEMORY_BANK.MEMORY_BANK_USER;
            op1.ReadAccessParams.AccessPassword = 0;
            op1.ReadAccessParams.ByteOffset = 2 * 5;
            op1.ReadAccessParams.ByteCount = (uint)(2 * 1);
            reader.Actions.TagAccess.OperationSequence.Add(op1);

            // Configure trigger
            TriggerInfo triggerInfo = new TriggerInfo();
            triggerInfo.StartTrigger.Type = START_TRIGGER_TYPE.START_TRIGGER_TYPE_IMMEDIATE;
            triggerInfo.StopTrigger.Type = STOP_TRIGGER_TYPE.STOP_TRIGGER_TYPE_TAG_OBSERVATION_WITH_TIMEOUT;
            triggerInfo.StopTrigger.TagObservation.N = 1;
            triggerInfo.StopTrigger.TagObservation.Timeout = (uint)readTimeMs;

            bool bapSuccess = false;
            foreach (double power in rfPowers)
            {
                SetReadPower(power);

                for (int a = 0; a < readAttempts; a++)
                {
                    // perform access sequence
                    newDataWords.Clear();
                    newPcWords.Clear();
                    AntennaInfo antennaInfo = new AntennaInfo();
                    antennaInfo.AntennaID = Array.ConvertAll(antennas, val => checked((ushort)val));
                    reader.Actions.TagAccess.OperationSequence.PerformSequence(null, triggerInfo, antennaInfo);

                    // wait for access operation complete
                    AccessComplete.WaitOne();

                    // get the access operation result
                    uint successCount = 0;
                    uint failureCount = 0;
                    reader.Actions.TagAccess.GetLastAccessResult(ref successCount, ref failureCount);

                    if (newDataWords.Count > 0)
                    {
                        OpusState stateFromPacketPC = OpusStateEx.StateFromPcWordsToOpusState(newPcWords.ToArray<ushort>());
                        if (stateFromPacketPC == OpusState.STANDBY || stateFromPacketPC == OpusState.BAP_MODE)
                        {
                            bapSuccess = true;
                            break;
                        }
                        OpusState stateFromUserBank = OpusStateEx.StateFromUserBankToOpusState(newDataWords.ToArray<ushort>());
                        if (stateFromUserBank == OpusState.STANDBY || stateFromUserBank == OpusState.BAP_MODE)
                        {
                            bapSuccess = true;
                            break;
                        }
                    }
                }
                if (bapSuccess) break;
            }

            return bapSuccess;
        }

        public void TransitionFromFinishedToStandby(String epc, double[] rfPowers, int numAttempts)
        {
            // Try to write words that are different from the requested values
            ushort newStoredPC = (ushort)(0x0500 | ((ushort)((epc.Length / 4) << 11))); // Write zero to bit 9 to re-use logger
            ushort[] writeWords = new ushort[] { newStoredPC };

            reader.Actions.PreFilters.DeleteAll();
            foreach (int ant in antennas)
            {
                // Add EPC filter
                PreFilters.PreFilter epcFilter = new PreFilters.PreFilter();
                epcFilter.AntennaID = (ushort)ant;
                epcFilter.TagPattern = RfidUtility.StringInHexToByteArray(epc);
                epcFilter.TagPatternBitCount = (uint)(epc.Length * 4);
                epcFilter.BitOffset = 32;
                epcFilter.MemoryBank = MEMORY_BANK.MEMORY_BANK_EPC;
                //epcFilter.FilterAction = FILTER_ACTION.FILTER_ACTION_STATE_AWARE;
                //epcFilter.StateAwareAction.Target = TARGET.TARGET_SL;
                //epcFilter.StateAwareAction.Action = STATE_AWARE_ACTION.STATE_AWARE_ACTION_ASRT_SL_NOT_DSRT_SL;

                epcFilter.FilterAction = FILTER_ACTION.FILTER_ACTION_STATE_UNAWARE;
                epcFilter.StateAwareAction.Target = TARGET.TARGET_INVENTORIED_STATE_S0;
                epcFilter.StateAwareAction.Action = STATE_AWARE_ACTION.STATE_AWARE_ACTION_INV_B_NOT_INV_A;

                reader.Actions.PreFilters.Add(epcFilter);

                // Singulation Control
                Antennas.SingulationControl sc = new Antennas.SingulationControl();
                sc.Session = SESSION.SESSION_S0;
                sc.TagPopulation = 2;
                //sc.Action.PerformStateAwareSingulationAction = true;
                //sc.Action.SLFlag = SL_FLAG.SL_FLAG_ASSERTED;
                //sc.Action.InventoryState = INVENTORY_STATE.INVENTORY_STATE_AB_FLIP; //It has to be AB_FLIP, otherwise the filters don't work

                sc.Action.PerformStateAwareSingulationAction = false;
                sc.Action.SLFlag = SL_FLAG.SL_ALL;
                sc.Action.InventoryState = INVENTORY_STATE.INVENTORY_STATE_B; 

                reader.Config.Antennas[ant].SetSingulationControl(sc);
            }

            // Configure trigger
            TriggerInfo triggerInfo = new TriggerInfo();
            triggerInfo.StartTrigger.Type = START_TRIGGER_TYPE.START_TRIGGER_TYPE_IMMEDIATE;
            triggerInfo.StopTrigger.Type = STOP_TRIGGER_TYPE.STOP_TRIGGER_TYPE_TAG_OBSERVATION_WITH_TIMEOUT;
            triggerInfo.StopTrigger.TagObservation.N = 1;
            triggerInfo.StopTrigger.TagObservation.Timeout = (uint)300;

            foreach (double power in rfPowers)
            {
                SetWritePower(power);
                for (int a = 0; a < numAttempts; a++)
                {
                    // add operation sequence: Write Memory
                    reader.Actions.TagAccess.OperationSequence.DeleteAll();
                    TagAccess.Sequence.Operation op1 = new TagAccess.Sequence.Operation();
                    op1.AccessOperationCode = ACCESS_OPERATION_CODE.ACCESS_OPERATION_WRITE;
                    op1.WriteAccessParams.AccessPassword = 0;
                    op1.WriteAccessParams.MemoryBank = MEMORY_BANK.MEMORY_BANK_EPC;
                    op1.WriteAccessParams.ByteOffset = 2;
                    op1.WriteAccessParams.WriteDataLength = 2; 
                    op1.WriteAccessParams.WriteData = RfidUtility.UshortArrayToByteArray(writeWords);
                    reader.Actions.TagAccess.OperationSequence.Add(op1);

                    // add operation sequence: Read Memory
                    TagAccess.Sequence.Operation op2 = new TagAccess.Sequence.Operation();
                    op2.AccessOperationCode = ACCESS_OPERATION_CODE.ACCESS_OPERATION_READ;
                    op2.ReadAccessParams.MemoryBank = MEMORY_BANK.MEMORY_BANK_EPC;
                    op2.ReadAccessParams.AccessPassword = 0;
                    op2.ReadAccessParams.ByteOffset = 2;
                    op2.ReadAccessParams.ByteCount = 2;
                    reader.Actions.TagAccess.OperationSequence.Add(op2);

                    requestedBankRead = MEMORY_BANK.MEMORY_BANK_EPC;
                    requestedBankWordAddress = 1;
                    requestedNumberOfWords = 1;
                    requestedEPC = epc;
                    newDataWords.Clear();
                    newPcWords.Clear();

                    // perform access sequence
                    AntennaInfo antennaInfo = new AntennaInfo();
                    antennaInfo.AntennaID = Array.ConvertAll(antennas, val => checked((ushort)val));
                    reader.Actions.TagAccess.OperationSequence.PerformSequence(null, triggerInfo, antennaInfo);

                    // wait for access operation complete
                    AccessComplete.WaitOne();

                    // get the access operation result
                    uint successCount = 0;
                    uint failureCount = 0;
                    reader.Actions.TagAccess.GetLastAccessResult(ref successCount, ref failureCount);

                    if (newDataWords.Count == 1)
                    {
                        break;
                    }
                }
            }
        }

        public void SetSetupForInventoryOpusTags(int initialQVal, bool includeSensorMeas) 
        {
            // Do nothing with includeSensorMeas since it is not possible to perform these measurements with this reader

            reader.Actions.PreFilters.DeleteAll();
            foreach (int ant in antennas)
            {
                // Activate On-Chip RSSI measurement with no filters
                PreFilters.PreFilter onChipRssiFilter = new PreFilters.PreFilter();
                onChipRssiFilter.AntennaID = (ushort) ant;
                onChipRssiFilter.TagPattern = new byte[2] { 0x0B, 0xE0 }; // Max = 31, Min = 0
                onChipRssiFilter.TagPatternBitCount = 16;
                onChipRssiFilter.BitOffset = 0x90;
                onChipRssiFilter.MemoryBank = MEMORY_BANK.MEMORY_BANK_USER;
                onChipRssiFilter.FilterAction = FILTER_ACTION.FILTER_ACTION_STATE_AWARE;
                onChipRssiFilter.StateAwareAction.Target = TARGET.TARGET_SL;

                if (includeSensorMeas)
                {
                    // Activate temperature measurement
                    PreFilters.PreFilter temperatureFilter = new PreFilters.PreFilter();
                    temperatureFilter.AntennaID = (ushort)ant;
                    temperatureFilter.TagPattern = new byte[2] { 0x0B, 0xE0 }; // Max = 31, Min = 0
                    temperatureFilter.TagPatternBitCount = 16;
                    temperatureFilter.BitOffset = 0xB0;
                    temperatureFilter.MemoryBank = MEMORY_BANK.MEMORY_BANK_USER;
                    temperatureFilter.FilterAction = FILTER_ACTION.FILTER_ACTION_STATE_AWARE;
                    temperatureFilter.StateAwareAction.Target = TARGET.TARGET_SL;
                    temperatureFilter.StateAwareAction.Action = STATE_AWARE_ACTION.STATE_AWARE_ACTION_ASRT_SL_NOT_DSRT_SL;
                    reader.Actions.PreFilters.Add(temperatureFilter);

                    // Activate battery voltage measurement
                    PreFilters.PreFilter batteryVolFilter = new PreFilters.PreFilter();
                    batteryVolFilter.AntennaID = (ushort)ant;
                    batteryVolFilter.TagPattern = new byte[2] { 0x0B, 0xE0 }; // Max = 31, Min = 0
                    batteryVolFilter.TagPatternBitCount = 16;
                    batteryVolFilter.BitOffset = 0xD0;
                    batteryVolFilter.MemoryBank = MEMORY_BANK.MEMORY_BANK_USER;
                    batteryVolFilter.FilterAction = FILTER_ACTION.FILTER_ACTION_STATE_AWARE;
                    batteryVolFilter.StateAwareAction.Target = TARGET.TARGET_SL;
                    batteryVolFilter.StateAwareAction.Action = STATE_AWARE_ACTION.STATE_AWARE_ACTION_NOT_DSRT_SL;
                    reader.Actions.PreFilters.Add(batteryVolFilter);

                    onChipRssiFilter.StateAwareAction.Action = STATE_AWARE_ACTION.STATE_AWARE_ACTION_NOT_DSRT_SL;
                }
                else
                {
                    onChipRssiFilter.StateAwareAction.Action = STATE_AWARE_ACTION.STATE_AWARE_ACTION_ASRT_SL_NOT_DSRT_SL;
                }

                reader.Actions.PreFilters.Add(onChipRssiFilter); // Add this filter at the end to provide additional time for the sensor measurements

                // Singulation Control
                Antennas.SingulationControl sc = new Antennas.SingulationControl();
                sc.Session = SESSION.SESSION_S0;
                sc.TagPopulation = (ushort) (2 * Math.Pow(2, initialQVal)); // TODO: Find better way to handle Q a/o tag population
                sc.Action.PerformStateAwareSingulationAction = true;
                sc.Action.SLFlag = SL_FLAG.SL_FLAG_ASSERTED;
                sc.Action.InventoryState = INVENTORY_STATE.INVENTORY_STATE_AB_FLIP; //It has to be AB_FLIP, otherwise the filters don't work
                reader.Config.Antennas[ant].SetSingulationControl(sc);
            }

            // add operation sequence: Read Sensor Code and On-Chip RSSI
            reader.Actions.TagAccess.OperationSequence.DeleteAll();
            TagAccess.Sequence.Operation op1 = new TagAccess.Sequence.Operation();
            op1.AccessOperationCode = ACCESS_OPERATION_CODE.ACCESS_OPERATION_READ;
            op1.ReadAccessParams.MemoryBank = MEMORY_BANK.MEMORY_BANK_USER;
            op1.ReadAccessParams.AccessPassword = 0;
            op1.ReadAccessParams.ByteOffset = 0; // word address 0x0
            op1.ReadAccessParams.ByteCount = 8; // read 4 words: Sensor Code, On-Chip RSSI, Temperature and Battery Voltage
            reader.Actions.TagAccess.OperationSequence.Add(op1);
        }

        public List<OpusTagInfo> InventoryOpusTags(int readTimeInMs, bool includeSensorMeas)
        {
            tagInfos.Clear();

            // Configure trigger
            TriggerInfo triggerInfo = new TriggerInfo();
            triggerInfo.StartTrigger.Type = START_TRIGGER_TYPE.START_TRIGGER_TYPE_IMMEDIATE;
            triggerInfo.StopTrigger.Type = STOP_TRIGGER_TYPE.STOP_TRIGGER_TYPE_N_ATTEMPTS_WITH_TIMEOUT;
            triggerInfo.StopTrigger.NumAttempts.N = 1;
            triggerInfo.StopTrigger.NumAttempts.Timeout = (uint) readTimeInMs;

            // perform access sequence
            AntennaInfo antennaInfo = new AntennaInfo();
            antennaInfo.AntennaID = Array.ConvertAll(antennas, val => checked((ushort)val));
            reader.Actions.TagAccess.OperationSequence.PerformSequence(null, triggerInfo, antennaInfo);

            // wait for access operation complete
            AccessComplete.WaitOne();

            // get the access operation result
            uint successCount = 0;
            uint failureCount = 0;
            reader.Actions.TagAccess.GetLastAccessResult(ref successCount, ref failureCount);

            return tagInfos;
        }

        private void Events_ReadNotify(object sender, Events.ReadEventArgs e)
        {
            // fetch tags from the Dll by specifying the number of expected tags
            Symbol.RFID3.TagData[] tagsData = reader.Actions.GetReadTags(100);

            if (tagsData != null)
            {                   
                foreach (Symbol.RFID3.TagData tag in tagsData)
                {
                    if (tag.OpCode == ACCESS_OPERATION_CODE.ACCESS_OPERATION_READ &&
                            tag.OpStatus == ACCESS_OPERATION_STATUS.ACCESS_SUCCESS)
                    {
                        // Get tag info 
                        if (tag.MemoryBankData != String.Empty &&
                            tag.MemoryBank == MEMORY_BANK.MEMORY_BANK_USER &&
                            tag.MemoryBankDataOffset == 0x000) // For now the assumption is if it is USER bank and address zero then it is tag info
                        {
                            String epc = tag.TagID;
                            if (epc.Length <= 0) continue; // Zero len EPCs are not allowed in this program
                            byte[] dataBytes = RfidUtility.StringInHexToByteArray(tag.MemoryBankData);
                            if (dataBytes == null || dataBytes.Length != 8) continue;
                            ushort[] sensorWords = RfidUtility.ByteArrayToUshortArray(dataBytes);
                            //sensorWords = sensorWords.Concat(new ushort[] { 0, 0 }).ToArray(); // FX9600 can't measure temperature and voltage
                            ushort[] pcWords = new ushort[] { tag.PC, tag.XPC_W1 };
                            OpusTagInfo tagInfo = new OpusTagInfo(epc, GetFrequencyValues()[tag.ChannelIndex], tag.PeakRSSI, sensorWords, pcWords, true);
                            if (!tagInfo.valid) continue;
                            tagInfos.Add(tagInfo);
                        }
                        else
                        {
                            if (tag.MemoryBankData != String.Empty &&
                                tag.TagID == requestedEPC &&
                                tag.MemoryBank == requestedBankRead &&
                                tag.MemoryBankDataOffset == (2 * requestedBankWordAddress) &&
                                tag.MemoryBankData.Length == (4 * requestedNumberOfWords))
                                {
                                    byte[] dataBytes = RfidUtility.StringInHexToByteArray(tag.MemoryBankData);
                                    newDataWords.Clear();
                                    newDataWords.AddRange(RfidUtility.ByteArrayToUshortArray(dataBytes));
                                    newPcWords.Clear();
                                    newPcWords.AddRange(new ushort[] { tag.PC, tag.XPC_W1 });
                                }
                        }
                    }
                }
            }
        }
       
        private void Events_StatusNotify(object sender, Events.StatusEventArgs e)
        {
            switch (e.StatusEventData.StatusEventType)
            {
                case Events.STATUS_EVENT_TYPE.ACCESS_START_EVENT:
                    AccessComplete.Reset();
                    break;

                case Events.STATUS_EVENT_TYPE.ACCESS_STOP_EVENT:
                    AccessComplete.Set();
                    break;

                default:
                    break;
            }
        }

        private double TransmitPowerIndexToPowerInDbm(int index)
        {
            int intPower = reader.ReaderCapabilities.TransmitPowerLevelValues[index];
            return intPower / 100.0;
        }

        private ushort PowerInDbmToTransmitPowerIndex(double power)
        {
            double lowestPower = reader.ReaderCapabilities.TransmitPowerLevelValues[0] / 100.0;
            int numIndexes = reader.ReaderCapabilities.TransmitPowerLevelValues.Length;
            int index = (int) Math.Round(10.0 * (power - lowestPower));
            if (index < 0) index = 0;
            if (index > (numIndexes - 1)) index = numIndexes - 1;
            return (ushort) index;
        }

        private int[] GetFrequencyValues()
        {
            int[] freqs;

            if (reader.ReaderCapabilities.IsHoppingEnabled)
            {
                freqs = reader.ReaderCapabilities.FrequencyHopInfo[0].FrequencyHopValues; // TODO: find better alternative than [0]
            }
            else
            {
                freqs = reader.ReaderCapabilities.FixedFreqValues;
            }

            return freqs;
        }

        private MEMORY_BANK BankToRFID3(MemoryBank bank)
        {
            switch (bank)
            {
                case MemoryBank.RESERVED:
                    return MEMORY_BANK.MEMORY_BANK_RESERVED;
                case MemoryBank.EPC:
                    return MEMORY_BANK.MEMORY_BANK_EPC;
                case MemoryBank.TID:
                    return MEMORY_BANK.MEMORY_BANK_TID;
                case MemoryBank.USER:
                    return MEMORY_BANK.MEMORY_BANK_USER;
                default:
                    throw new Exception("Unknown Memory Bank: " + bank.ToString());
            }
        }

    }

}
