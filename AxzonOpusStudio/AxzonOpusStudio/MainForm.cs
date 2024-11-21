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
using System.IO.Ports;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Threading;
using System.Drawing;

namespace AxzonTempSensor
{
    public partial class MainForm : Form
    {
        // For tab: Reader
        IRfidReader reader;
        public int[] antennas = null; // TODO: Change to list
        CheckBox[] AntennaCheckBoxes = new CheckBox[8];

        // For tab: Opus
        String selectedEPC = "";
        Dictionary<string, OpusTag> opusTags = new Dictionary<string, OpusTag>();

        public MainForm()
        {
            InitializeComponent();

            AntennaCheckBoxes[0] = Antenna1CheckBox;
            AntennaCheckBoxes[1] = Antenna2CheckBox;
            AntennaCheckBoxes[2] = Antenna3CheckBox;
            AntennaCheckBoxes[3] = Antenna4CheckBox;
            AntennaCheckBoxes[4] = Antenna5CheckBox;
            AntennaCheckBoxes[5] = Antenna6CheckBox;
            AntennaCheckBoxes[6] = Antenna7CheckBox;
            AntennaCheckBoxes[7] = Antenna8CheckBox;

            // TODO: add column sorting capabilities
            //lvwColumnSorter = new ListViewColumnSorter();
            //this.FindTagListView.ListViewItemSorter = lvwColumnSorter;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            RefreshComPorts();
            reader = null;
            ReaderSelectionComboBox.SelectedIndex = 0;
            ComPortsComboBox.SelectedIndex = ComPortsComboBox.Items.Count - 1;
            ConnectReader();
            mainTabCtrl.SelectedTab = opusTabPage;

            loggerPlot.Plot.XLabel("Time[s]");
            loggerPlot.Plot.YLabel("Temperature[°C]");
            loggerPlot.Plot.Title("Opus Temperature Logged Data");
            loggerPlot.Plot.XAxis.DateTimeFormat(true);
            loggerPlot.Refresh();
        }

        //---------------- HIGH LEVEL READER UTILITIES

        private void RefreshComPorts()
        {
            string[] ports = SerialPort.GetPortNames();

            ComPortsComboBox.Items.Clear();
            ComPortsComboBox.Text = "";
            foreach (string port in ports)
            {
                ComPortsComboBox.Items.Add(port);
            }
            if (ports.Length > 0) ComPortsComboBox.SelectedIndex = 0;
        }

        private void ConnectReader()
        {
            statusTextBox.Text = "Connecting to reader...";
            Application.DoEvents();

            if (reader != null) reader.Dispose();
            reader = null;

            switch (ReaderSelectionComboBox.SelectedIndex)
            {
                case 0: // ThingMagicM6e
                    reader = new RfidReaderM6e();
                    break;
                case 1: // ZebraFX9600
                    reader = new RfidReaderFX6000();
                    break;
                default:
                    StatusBoxAppend("ERROR: Reader selection not valid");
                    break;
            }

            string connectionURI;
            if (ComPortRadioButton.Checked)
            {
                connectionURI = ComPortsComboBox.Text;
                StatusBoxAppend("OPERATION: Reader connecting using serial port: " + ComPortsComboBox.Text);
            }
            else
            {
                connectionURI = IpAddressTextBox.Text;
                StatusBoxAppend("OPERATION: Reader connecting using Ethernet Host Name: " + IpAddressTextBox.Text + " Port: " + EthernetPortTextBox.Text);
            }

            // Reader Connect
            try
            {
                reader.Connect(connectionURI, uint.Parse(EthernetPortTextBox.Text));
                StatusBoxAppend("SUCCESS: Reader connected.");
                ConnectReaderButton.Text = "Disconnect Reader";
            }
            catch (Exception ex)
            {
                StatusBoxAppend("ERROR: Unable to connect to reader. " + ex.Message);
                return;
            }

            // Configure Antennas
            try
            {
                List<int> ants = reader.AvailableAntennas;
                List<int> conAnts = reader.ConnectedAntennas;
                if (conAnts.Count == 0 && ants.Count > 0) conAnts.Add(ants[0]);
                int maxAntennas = AntennaCheckBoxes.Length;
                int counter = 0;
                foreach (int ant in ants)
                {
                    AntennaCheckBoxes[counter].CheckedChanged -= AntennaCheckBox_CheckedChanged;
                    AntennaCheckBoxes[counter].Enabled = true;
                    AntennaCheckBoxes[counter].Visible = true;
                    AntennaCheckBoxes[counter].Text = "0" + ant.ToString();
                    AntennaCheckBoxes[counter].Checked = conAnts.Contains(ant);
                    AntennaCheckBoxes[counter].Tag = ant; // Store antenna ID in the checkbox's Tag field
                    AntennaCheckBoxes[counter].CheckedChanged += AntennaCheckBox_CheckedChanged;
                    counter++;
                }

                for (int i = counter; i < maxAntennas; i++)
                {
                    AntennaCheckBoxes[i].CheckedChanged -= AntennaCheckBox_CheckedChanged;
                    AntennaCheckBoxes[i].Enabled = false;
                    AntennaCheckBoxes[i].Visible = false;
                    AntennaCheckBoxes[i].Tag = null;
                    AntennaCheckBoxes[i].CheckedChanged += AntennaCheckBox_CheckedChanged;
                }
                reader.Antennas = conAnts;
            }
            catch (Exception ex)
            {
                StatusBoxAppend("ERROR: Reader unable to configure antennas. " + ex.Message);
                return;
            }

            // Initial Setup
            try
            {
                reader.InitialSetup();
            }
            catch (Exception ex)
            {
                StatusBoxAppend("ERROR: Reader unable to perform initial setup. " + ex.Message);
                return;
            }

            // Set frequency band
            try
            {
                int[] freqs = reader.SetFccBand();
                UpdateFreqChannelsTextBox(freqs);
            }
            catch (Exception ex)
            {
                StatusBoxAppend("ERROR: Reader unable to set FCC frequency band. " + ex.Message);
            }
        }

        private void DisconnectReader()
        {
            try
            {
                if (reader != null)
                {
                    reader.Disconnect();
                    reader.Dispose();
                }
                reader = null;
                StatusBoxAppend("SUCCESS: Reader disconnected. ");
            }
            catch (Exception ex)
            {
                StatusBoxAppend("ERROR: While disconnecting reader: " + ex.Message);
            }

            ConnectReaderButton.Text = "Connect Reader";
        }

        private void ReviewAntennaCheckBoxes()
        {
            List<int> ants = new List<int>();
            foreach (CheckBox cb in AntennaCheckBoxes)
            {
                if (cb.Enabled && cb.Checked)
                {
                    ants.Add((int)cb.Tag);
                }
            }
            reader.Antennas = ants;

            reader.InitialSetup();
        }

        private void UpdateFreqChannelsTextBox(int[] freqs)
        {
            string sfreqs = "";
            if (freqs != null)
            {
                sfreqs = String.Join(", ", freqs);
            }
            FreqChanTextBox.Text = sfreqs;
        }

        private void UpdateFreqChans()
        {
            string sfreqs = FreqChanTextBox.Text;
            int[] freqs = Array.ConvertAll(sfreqs.Split(','), int.Parse);
            int[] updatedFreqs = reader.UpdateFrequencyChannels(freqs);
            UpdateFreqChannelsTextBox(updatedFreqs);
        }

        private void SetReadPower(double power, bool displayStatus = false)
        {
            double readBack = reader.SetReadPower(power, displayStatus);
            if (displayStatus)
            {
                if (power == readBack)
                {
                    StatusBoxAppend("SUCCESS: Read power set to: " + string.Format("{0:N1}", power) + " dBm", true);
                }
                else
                {
                    StatusBoxAppend("ERROR: Setting read power to: " + string.Format("{0:N1}", power) + " dBm", true);
                }
            }
        }

        private void SetWritePower(double power, bool displayStatus = false)
        {
            double readBack = reader.SetWritePower(power, displayStatus);
            if (displayStatus)
            {
                if (power == readBack)
                {
                    StatusBoxAppend("SUCCESS: Write power set to: " + string.Format("{0:N1}", power) + " dBm", displayStatus);
                }
                else
                {
                    StatusBoxAppend("ERROR: Setting write power to: " + string.Format("{0:N1}", power) + " dBm");
                }
            }
        }

        private ushort[] ReadTagMemByEPC(String epc, MemoryBank bank, uint wordAddress, byte numWords, out ushort[] pcWords, String label = null)
        {
            Thread.Sleep(100);
            ushort[] dataWords = reader.ReadTagMemByEPC(epc, bank, wordAddress, numWords, GetRfPowers(), int.Parse(ReadTimeTextBox.Text), int.Parse(NumRetriesTextBox.Text), out pcWords);

            if (dataWords != null)
            {
                if (label != null) StatusBoxAppend("SUCCESS: Read of " + label);
            }
            else
            {
                if (label != null) StatusBoxAppend("FAIL: Unable to read " + label);
            }
            return dataWords;
        }
        
        private bool WriteTagMemByEPC(String epc, MemoryBank bank, uint wordAddress, ushort[] wordsToWrite, out ushort[] wordsReadBack, out ushort[] pcWords, bool verifyReadBack = true, String label = null)
        {
            bool wasDataWritten = reader.WriteTagMemByEPC(epc, bank, wordAddress, wordsToWrite, GetRfPowers(), int.Parse(NumRetriesTextBox.Text), verifyReadBack, out wordsReadBack, out pcWords);
            if (wasDataWritten)
            {
                if (label != null) StatusBoxAppend("SUCCESS: Words written: " + label);
            }
            else
            {
                if (label != null) StatusBoxAppend("FAIL:  Unable to write words: " + label);
            }

            return wasDataWritten;
        }

        //---------------- HIGH LEVEL OPUS SPECIFIC FUNCTIONS

        private OpusState ReadLoggerState(String epc)
        {
            ushort[] words = ReadTagMemByEPC(epc, MemoryBank.USER, 0x05, 1, out _, "logger state");
            return OpusStateEx.StateFromUserBankToOpusState(words);
        }

        private bool ClearAlarmsInTheStoredPC(ushort[] pcWords = null)
        {
            if (pcWords != null && pcWords.Length >= 1 && (pcWords[0] & 0x0007) == 0x0000) return true;

            ushort epcLenInWords = (ushort)(selectedEPC.Length / 4);
            ushort StoredPC = (ushort)((epcLenInWords << 11) | 0x0500);
            ushort[] wordToWrite = new ushort[] { StoredPC };

            ushort[] wordsReadBack;
            bool dataWritten = WriteTagMemByEPC(selectedEPC, MemoryBank.EPC, 0x01, wordToWrite, out wordsReadBack, out _, false, "StoredPC");
            dataWritten = dataWritten ? ((ushort)(wordsReadBack[0] & 0xFD07)) == StoredPC : false;

            if (dataWritten)
            {
                StatusBoxAppend("SUCCESS: Alarms in the StoredPC were cleared");
            }
            else
            {
                StatusBoxAppend("FAIL:  Unable to clear alarms in the StoredPC");
            }

            return dataWritten;
        }

        private bool ClearAlarmInTheXPC_X1(ushort[] pcWords = null)
        {
            if (pcWords != null && pcWords.Length >= 1 && (pcWords[0] & 0x0007) == 0x0000) return true;


            ushort xpc_X1 = 0x0400;
            ushort[] wordToWrite = new ushort[] { xpc_X1 };

            ushort[] wordsReadBack;
            bool dataWritten = WriteTagMemByEPC(selectedEPC, MemoryBank.EPC, 0x21, wordToWrite, out wordsReadBack, out _, false, "XPC_X1");
            dataWritten = dataWritten ? ((ushort)(wordsReadBack[0] & 0x0C00)) == xpc_X1 : false;

            if (dataWritten)
            {
                StatusBoxAppend("SUCCESS: Alarm in the XPC_W1 was cleared");
            }
            else
            {
                StatusBoxAppend("FAIL:  Unable to clear alarm in the XPC_W1");
            }

            return dataWritten;
        }


        //--------------- OPUS TAB - Callbacks

        private void RefreshComPortsButton_Click(object sender, EventArgs e)
        {
            RefreshComPorts();
        }

        private void ReaderSelectionComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (ReaderSelectionComboBox.SelectedIndex)
            {
                case 0: // ThingMagicM6e
                    ComPortRadioButton.Checked = true;
                    ReaderInfoLabel.Text = "RF Power 5 to 30 dBm";
                    break;
                case 1: // ZebraFX9600
                    IpAddressRadioButton.Checked = true;
                    ReaderInfoLabel.Text = "RF Power 10 to 32 dBm";
                    break;
                default:
                    ReaderInfoLabel.Text = "ERROR: Reader selection not valid";
                    StatusBoxAppend(ReaderInfoLabel.Text);
                    break;
            }
        }

        private void EthernetPortTextBox_TextChanged(object sender, EventArgs e)
        {
            uint portNum = 0;

            try
            {
                portNum = uint.Parse(EthernetPortTextBox.Text);
            }
            catch (Exception ex)
            {
                StatusBoxAppend("ERROR: Ethernet port is not an unsigned integer number" + ex.Message);
                portNum = 0;
            }

            EthernetPortTextBox.Text = portNum.ToString();
        }

        private void ConnectReaderButton_Click(object sender, EventArgs e)
        {
            ConnectReaderButton.Enabled = false;
            if (ConnectReaderButton.Text == "Connect Reader")
            {
                ConnectReader();
            }
            else
            {
                DisconnectReader();
            }
            ConnectReaderButton.Enabled = true;
        }

        private void AntennaCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            ReviewAntennaCheckBoxes();
        }

        private void FccRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                if (FccRadioButton.Checked)
                {
                    EuRadioButton.Checked = false;
                    OpenRadioButton.Checked = false;
                    int[] freqs = reader.SetFccBand();
                    UpdateFreqChannelsTextBox(freqs);
                }

                StatusBoxAppend("SUCCESS: Band changed to FCC");
            }
            catch (Exception ex)
            {
                StatusBoxAppend("ERROR: While setting reader parameter: " + ex.Message);
            }
        }

        private void EuRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                if (EuRadioButton.Checked)
                {
                    FccRadioButton.Checked = false;
                    OpenRadioButton.Checked = false;
                    int[] freqs = reader.SetEuBand();
                    UpdateFreqChannelsTextBox(freqs);
                }

                StatusBoxAppend("SUCCESS: Band changed to EU3");
            }
            catch (Exception ex)
            {
                StatusBoxAppend("ERROR: while setting reader parameter: " + ex.Message);
            }
        }

        private void OpenRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                if (OpenRadioButton.Checked)
                {
                    FccRadioButton.Checked = false;
                    EuRadioButton.Checked = false;
                    int[] freqs = reader.SetOpenBand();
                    UpdateFreqChannelsTextBox(freqs);
                }

                StatusBoxAppend("SUCCESS: Band changed to OPEN");
            }
            catch (Exception ex)
            {
                StatusBoxAppend("ERROR: While setting reader parameter: " + ex.Message);
            }
        }

        private void UpdateFreqChansButton_Click(object sender, EventArgs e)
        {
            try
            {
                UpdateFreqChans();
                StatusBoxAppend("SUCCESS: Updated frequency channels");
            }
            catch (Exception ex)
            {
                StatusBoxAppend("ERROR: While updating frequency channels: " + ex.Message);
            }
        }

        private void FindOpusTagsButton_Click(object sender, EventArgs e)
        {
            SetAllControlsEnableProperty(false);
            StatusBoxAppend("OPERATION: Started looking for Opus tags...");

            try
            {
                int numRetries = int.Parse(NumRetriesTextBox.Text);
                reader.SetSetupForInventoryOpusTags(int.Parse(InitialQTextBox.Text), ActivateMeasCheckBox.Checked);

                double[] powers = GetRfPowers();
                foreach (double power in powers)
                {
                    reader.SetReadPower(power);
                    for (int i = 1; i <= numRetries; i++)
                    {
                        List<OpusTagInfo> tags = reader.InventoryOpusTags(int.Parse(ReadTimeTextBox.Text), ActivateMeasCheckBox.Checked);

                        foreach (OpusTagInfo tag in tags)
                        {
                            ListViewItem item;
                            if (OpusListView.Items.ContainsKey(tag.epc))
                            {
                                item = OpusListView.Items.Find(tag.epc, true)[0];
                                if (item.Checked)
                                {
                                    opusTags[tag.epc].Info = tag;
                                    int itemNum = 1;
                                    item.SubItems[itemNum++].Text = tag.SensorCode.ToString();
                                    item.SubItems[itemNum++].Text = tag.OnChipRSSI.ToString();
                                    item.SubItems[itemNum++].Text = tag.TemperatureInC_String == "NaN" ? "NA" : tag.TemperatureInC_String;
                                    item.SubItems[itemNum++].Text = tag.BatteryInV_String == "NaN" ? "NA" : tag.BatteryInV_String;
                                    item.SubItems[itemNum++].Text = tag.BatteryPresent_String;
                                    item.SubItems[itemNum++].Text = OpusStateEx.Description(tag.State);
                                    item.SubItems[itemNum++].Text = tag.Alarms_String;
                                    item.SubItems[itemNum++].Text = tag.frequency.ToString();
                                    item.SubItems[itemNum++].Text = tag.PacketPC_String;
                                    item.SubItems[itemNum++].Text = tag.XPC_W1_String;
                                    item.SubItems[itemNum++].Text = String.Format("{0:0.0}", power);
                                    item.SubItems[itemNum++].Text = tag.rssi.ToString();
                                }
                            }
                            else
                            {
                                opusTags.Add(tag.epc, new OpusTag(tag));
                                item = new ListViewItem(tag.epc);
                                item.Name = tag.epc;
                                item.SubItems.Add(tag.SensorCode.ToString());
                                item.SubItems.Add(tag.OnChipRSSI.ToString());
                                item.SubItems.Add(tag.TemperatureInC_String == "NaN" ? "NA" : tag.TemperatureInC_String);
                                item.SubItems.Add(tag.BatteryInV_String == "NaN" ? "NA" : tag.BatteryInV_String);
                                item.SubItems.Add(tag.BatteryPresent_String);
                                item.SubItems.Add(OpusStateEx.Description(tag.State));
                                item.SubItems.Add(tag.Alarms_String);
                                item.SubItems.Add(tag.frequency.ToString());
                                item.SubItems.Add(tag.PacketPC_String);
                                item.SubItems.Add(tag.XPC_W1_String);
                                item.SubItems.Add(String.Format("{0:0.0}", power));
                                item.SubItems.Add(tag.rssi.ToString());

                                item.Checked = true;
                                OpusListView.Items.Add(item);
                                OpusListView.EnsureVisible(item.Index);
                                if (OpusListView.Items.Count == 1)
                                {
                                    item.Selected = true;
                                    item.Focused = true;
                                }
                            }
                            Application.DoEvents();
                        }

                        tags = null;
                    }
                }
            }
            catch (Exception ex)
            {
                StatusBoxAppend("ERROR: while looking for Opus tags" + ex.Message);
            }

            SetDefaultEnabledControlsEnableProperty(true);
            UpdateActionControls();
        }

        private void ClearListButton_Click(object sender, EventArgs e)
        {
            ClearListButton.Enabled = false;
            OpusListView.SelectedItems.Clear();
            OpusListView.Items.Clear();
            opusTags.Clear();
            ClearListButton.Enabled = true;
        }

        private void OpusListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (OpusListView.SelectedItems != null && OpusListView.SelectedItems.Count > 0)
            {
                selectedEPC = OpusListView.SelectedItems[0].Name;
                UpdateLoggerStatusPanel(opusTags[selectedEPC].Status);
                if (UpdateConfigCheckBox.Checked) UpdateLoggerConfigPanel(opusTags[selectedEPC].Config);
                UpdateLoggedDataPanel(opusTags[selectedEPC].LoggedData);
                SetActionControlsEnableProperty(true);
            }
            else
            {
                selectedEPC = "";
                ClearLoggerStatusPanel();
                if (UpdateConfigCheckBox.Checked)
                {
                    ClearLoggerConfigPanel();
                }
                else
                {
                    ReadConfigurationLED.CheckState = CheckState.Indeterminate;
                }
                ClearLoggerArmingPanel();
                ClearLoggedDataPanel();
                SetActionControlsEnableProperty(false);
            }
        }

        private void ReadLoggerStatusButton_Click(object sender, EventArgs e)
        {
            SetAllControlsEnableProperty(false);
            ClearLoggerStatusPanel();

            try
            {
                ushort[] pcWords;
                Thread.Sleep(100);
                ushort[] stateAndClock = ReadTagMemByEPC(selectedEPC, MemoryBank.USER, 0x05, 3, out _, "logger state and clock");
                Thread.Sleep(100);
                ushort[] loggerStatusTID = ReadTagMemByEPC(selectedEPC, MemoryBank.TID, 0x08, 20, out pcWords, "logger status TID 0x08_to_0x1B");
                opusTags[selectedEPC].Status = new OpusTagStatus(stateAndClock, loggerStatusTID, pcWords);
                UpdateLoggerStatusPanel(opusTags[selectedEPC].Status);
                opusTags[selectedEPC].LoggedData.Status = opusTags[selectedEPC].Status;
                UpdateLoggedDataPanel(opusTags[selectedEPC].LoggedData);

                if (opusTags[selectedEPC].Status.valid)
                {
                    StatusBoxAppend("SUCCESS: Read Logger Status Operation");
                }
                else
                {
                    StatusBoxAppend("FAIL: Read Logger Status Operation");
                }
            }
            catch (Exception ex)
            {
                StatusBoxAppend("ERROR: Read Logger Status Operation: " + ex.Message);
            }

            SetAllControlsEnableProperty(true);
        }

        private void ReadLoggerConfigButton_Click(object sender, EventArgs e)
        {
            SetAllControlsEnableProperty(false);
            ClearLoggerConfigPanel();

            try
            {
                ushort[] loggerConfig_tid_0x08_to_0x1F = ReadTagMemByEPC(selectedEPC, MemoryBank.TID, 0x08, 24, out _, "logger config TID 0x08_to_0x1F");
                OpusTagConfiguration tagConfig = new OpusTagConfiguration(loggerConfig_tid_0x08_to_0x1F);
                opusTags[selectedEPC].Config = tagConfig;
                UpdateLoggerConfigPanel(tagConfig);
                opusTags[selectedEPC].LoggedData.Config = tagConfig;
                UpdateLoggedDataPanel(opusTags[selectedEPC].LoggedData);

                if (tagConfig.valid)
                {
                    StatusBoxAppend("SUCCESS: Read Logger Configuration Operation");
                }
                else
                {
                    StatusBoxAppend("FAIL: Read Logger Configuration Operation");
                }
            }
            catch (Exception ex)
            {
                StatusBoxAppend("ERROR: Read Logger Configuration Operation: " + ex.Message);
            }

            SetAllControlsEnableProperty(true);
        }

        private void SetDefaultConfigurationButton_Click(object sender, EventArgs e)
        {
            SetDefaultConfigurationButton.Enabled = false;
            OpusTagConfiguration config = new OpusTagConfiguration();
            UpdateLoggerConfigPanel(config);
            SetDefaultConfigurationButton.Enabled = true;
            OpusListView.Focus();
        }

        private void SetBapModeButton_Click(object sender, EventArgs e)
        {
            if (selectedEPC == null || selectedEPC.Length == 0)
            {
                SetBapModeLED.CheckState = CheckState.Checked;
                StatusBoxAppend("ERROR: Unable to set BAP mode. Empty EPC.");
                return;
            }

            SetAllControlsEnableProperty(false);
            SetBapModeLED.CheckState = CheckState.Indeterminate;
            StatusBoxAppend("OPERATION: Started setting BAP mode...");

            try
            {
                if (FinishedToStandbyCheckBox.Checked)
                {
                    OpusState state = ReadLoggerState(selectedEPC);
                    if (state != OpusState.FINISHED)
                    {
                        SetBapModeLED.CheckState = CheckState.Checked;
                        StatusBoxAppend("FAIL: Logger state is not FINISHED");
                        SetAllControlsEnableProperty(true);
                        return;
                    }
                    reader.TransitionFromFinishedToStandby(selectedEPC, GetRfPowers(), int.Parse(NumRetriesTextBox.Text));
                }

                int numRetries = int.Parse(NumRetriesTextBox.Text);
                double[] powers = GetRfPowers();
                bool bapSuccess = reader.SetBapMode(selectedEPC, powers, 300, numRetries);               
                if (bapSuccess)
                {
                    StatusBoxAppend("SUCCESS: BAP Mode Set");
                    SetBapModeLED.CheckState = CheckState.Unchecked;
                }
                else
                {
                    StatusBoxAppend("FAIL: BAP Mode Set");
                    SetBapModeLED.CheckState = CheckState.Checked;
                }

                Application.DoEvents();
            }
            catch (Exception ex)
            {
                StatusBoxAppend("ERROR: " + ex.Message);
            }

            SetAllControlsEnableProperty(true);
        }

        private void WriteConfigurationButton_Click(object sender, EventArgs e)
        {
            SetAllControlsEnableProperty(false);
            WriteConfigurationLED.CheckState = CheckState.Indeterminate;
            StatusBoxAppend("OPERATION: Started writing the logger configuration...");

            try
            {
                OpusState state = OpusState.FINISHED; // ReadLoggerState(selectedEPC);
                if (state == OpusState.READY || state == OpusState.ARMED || state == OpusState.LOGGING)
                {
                    WriteConfigurationLED.CheckState = CheckState.Checked;
                    StatusBoxAppend("FAIL: Logger is in " + OpusStateEx.Description(state) + " state");
                    SetAllControlsEnableProperty(true);
                    return;
                }

                // Clear status and logged data
                opusTags[selectedEPC].Status = null;
                opusTags[selectedEPC].LoggedData = new OpusLoggedData();

                OpusTagConfiguration config = GuiConfigToOpusConfig();
                if (config == null || !config.valid)
                {
                    WriteConfigurationLED.CheckState = CheckState.Checked;
                    StatusBoxAppend("FAIL: Invalid configuration data");
                    SetAllControlsEnableProperty(true);
                    return;
                }

                ushort[] tid_data_0x08_to_0x10 = config.tid_0x08_to_0x1F.Take(9).ToArray();
                // Don't write addresses 0x11 and 0x12 because these are the addresses for the timestamp that starts the logger
                ushort[] tid_data_0x13_to_0x29 = config.tid_0x08_to_0x1F.Skip(11).Concat(config.tid_0x20_to_0x29).ToArray();

                ushort[] pcWords = null;
                bool wasDataWritten = false;
                wasDataWritten = WriteTagMemByEPC(selectedEPC, MemoryBank.TID, 0x08, tid_data_0x08_to_0x10, out _, out _, true, "logger config data, TID 0x08 to 0x10");
                if (wasDataWritten) wasDataWritten = WriteTagMemByEPC(selectedEPC, MemoryBank.TID, 0x13, tid_data_0x13_to_0x29, out _, out pcWords, true, "logger config data, TID 0x13 to 0x29");

                bool wereAlarmsCleared = false;

                //wasDataWritten = true;
                //wereAlarmsCleared = true;
                if (wasDataWritten) wereAlarmsCleared = ClearAlarmsInTheStoredPC(pcWords);
                if (wereAlarmsCleared) wereAlarmsCleared = ClearAlarmInTheXPC_X1(pcWords);

                bool additional = true;
                //additional &= WriteTagMemByEPC(selectedEPC, MemoryBank.RESERVED, 0x04, new ushort[] { 0x2300 }, out _, out _, true, "Raw data"); // raw data mode
                //additional &= WriteTagMemByEPC(selectedEPC, MemoryBank.TID, 0x0F, new ushort[] { 0x7000 }, out _, out _, true, "Log voltage Word"); // logging voltage and 5 minutes log interval
                //additional &= WriteTagMemByEPC(selectedEPC, MemoryBank.TID, 0x1F, new ushort[] { 0x0008 }, out _, out _, true, "Averaging and BAP Word"); // Averaging off
                //additional &= WriteTagMemByEPC(selectedEPC, MemoryBank.TID, 0x1C, new ushort[] { 0x0000 }, out _, out _, true, "Battery voltage limit"); // 2.6V = 0x0A28 2.0V = 07D0

                if (wereAlarmsCleared && additional)
                {
                    opusTags[selectedEPC].Config = config;
                    StatusBoxAppend("SUCCESS:  Logger configuration written to tag and the logger status was cleared");
                    WriteConfigurationLED.CheckState = CheckState.Unchecked;
                }
                else
                {
                    opusTags[selectedEPC].Config = null;
                    StatusBoxAppend("FAIL:  Unable to write the logger configuration or clear the logger status");
                    WriteConfigurationLED.CheckState = CheckState.Checked;
                }
            }
            catch (Exception ex)
            {
                StatusBoxAppend("ERROR: " + ex.Message);
            }

            SetAllControlsEnableProperty(true);
        }

        private void StartTheLoggerButton_Click(object sender, EventArgs e)
        {
            SetAllControlsEnableProperty(false);
            StartTheLoggerLED.CheckState = CheckState.Indeterminate;
            LoggerStartTimestampWrittenTextBox.Text = "";
            StatusBoxAppend("OPERATION: Started starting the logger...");

            try
            {
                if (ReadLoggerState(selectedEPC) != OpusState.STANDBY)
                {
                    StartTheLoggerLED.CheckState = CheckState.Checked;
                    LoggerStartTimestampWrittenTextBox.Text = "Logger is not in Standby state";
                    StatusBoxAppend("FAIL: Logger is not in Standby state.");
                    SetAllControlsEnableProperty(true);
                    return;
                }

                // Clear status and logged data
                opusTags[selectedEPC].Status = null;
                opusTags[selectedEPC].LoggedData = new OpusLoggedData();

                // Create TimeStamp
                DateTime timestampDateTime = DateTime.Now;
                ushort[] timestampInUtc = UtcBasedTime.DateTimeToUtc(timestampDateTime);

                WriteTagMemByEPC(selectedEPC, MemoryBank.TID, 0x11, timestampInUtc, out _, out _, false, "Start TimeStamp");
                ushort[] dataWritten = ReadTagMemByEPC(selectedEPC, MemoryBank.TID, 0x11, 2, out _, "Start TimeStamp");
                bool timestampWritten = dataWritten != null && dataWritten.Length == 2 && dataWritten[0] == timestampInUtc[0] && dataWritten[0] == timestampInUtc[0];

                bool stateIsReadyArmedOrLogging = false;
                if (timestampWritten)
                {
                    LoggerStartTimestampWrittenTextBox.Text = timestampDateTime.ToString("yyyy/MM/dd hh:mm:ss tt");
                    Thread.Sleep(100);
                    OpusState state = ReadLoggerState(selectedEPC);
                    stateIsReadyArmedOrLogging = (state == OpusState.READY || state == OpusState.ARMED || state == OpusState.LOGGING);
                }

                if (timestampWritten && !stateIsReadyArmedOrLogging)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        Thread.Sleep(300);
                        OpusState state = ReadLoggerState(selectedEPC);
                        stateIsReadyArmedOrLogging = (state == OpusState.READY || state == OpusState.ARMED || state == OpusState.LOGGING);
                        if (stateIsReadyArmedOrLogging) break;
                    }
                }

                if (timestampWritten && stateIsReadyArmedOrLogging)
                {
                    StatusBoxAppend("SUCCESS:  Logger state is READY, ARMED or LOGGING");
                    StartTheLoggerLED.CheckState = CheckState.Unchecked;
                }
                else
                {
                    if (!timestampWritten) StatusBoxAppend("FAIL: Unable to write the Start Timestamp");
                    if (!stateIsReadyArmedOrLogging) StatusBoxAppend("FAIL: Logger state is not Ready, Armed nor Logging");
                    StatusBoxAppend("FAIL:  Unable to start the logger");
                    StartTheLoggerLED.CheckState = CheckState.Checked;
                }
            }
            catch (Exception ex)
            {
                StatusBoxAppend("ERROR: " + ex.Message);
            }

            SetAllControlsEnableProperty(true);
        }

        private void ReadLoggerInfoButton_Click(object sender, EventArgs e)
        {
            SetAllControlsEnableProperty(false);
            ClearLoggerStatusPanel();
            ClearLoggerConfigPanel();
            ClearLoggedDataPanel();

            try
            {
                ushort[] pcWords;
                Thread.Sleep(100);
                ushort[] stateAndClock = ReadTagMemByEPC(selectedEPC, MemoryBank.USER, 0x05, 3, out _, "logger state and clock");
                Thread.Sleep(100);
                ushort[] loggerStatusTID = ReadTagMemByEPC(selectedEPC, MemoryBank.TID, 0x08, 20, out pcWords, "logger status TID 0x08_to_0x1B");
                OpusTagStatus tagStatus = new OpusTagStatus(stateAndClock, loggerStatusTID, pcWords);
                opusTags[selectedEPC].Status = tagStatus;
                opusTags[selectedEPC].LoggedData.Status = tagStatus;
                UpdateLoggerStatusPanel(tagStatus);
                if (tagStatus.valid)
                {
                    StatusBoxAppend("SUCCESS: Read Logger Status Operation");
                }
                else
                {
                    StatusBoxAppend("FAIL: Read Logger Status Operation");
                }

                Thread.Sleep(100);
                ushort[] loggerConfig_tid_0x08_to_0x1F = ReadTagMemByEPC(selectedEPC, MemoryBank.TID, 0x08, 24, out _, "logger config TID 0x08_to_0x1F");
                OpusTagConfiguration tagConfig = new OpusTagConfiguration(loggerConfig_tid_0x08_to_0x1F);
                opusTags[selectedEPC].Config = tagConfig;
                opusTags[selectedEPC].LoggedData.Config = tagConfig;
                UpdateLoggerConfigPanel(tagConfig);
                if (tagConfig.valid)
                {
                    StatusBoxAppend("SUCCESS: Read Logger Configuration Operation");
                }
                else
                {
                    StatusBoxAppend("FAIL: Read Logger Configuration Operation");
                }
            }
            catch (Exception ex)
            {
                StatusBoxAppend("ERROR: Read Logger Info Operation: " + ex.Message);
            }

            UpdateLoggedDataPanel(opusTags[selectedEPC].LoggedData);
            SetAllControlsEnableProperty(true);
        }

        private void StartSampleNumberTextBox_Validated(object sender, EventArgs e)
        {
            int startNum = 0;

            try
            {
                startNum = int.Parse(StartSampleNumberTextBox.Text);
            }
            catch (Exception ex)
            {
                StatusBoxAppend("ERROR: Invalid Start Sample Number: " + StartSampleNumberTextBox.Text + ", Exception: " + ex.Message);
                startNum = 0;
            }

            if (startNum < 0)
            {
                StatusBoxAppend("ERROR: Start Sample Number can not be less than zero: " + StartSampleNumberTextBox.Text);
                startNum = 0;
            }

            int untilNum = int.Parse(UntilSampleNumberTextBox.Text);
            if (startNum > untilNum)
            {
                StatusBoxAppend("ERROR: Start Sample Number can not be greater than the End Sample Number: " + StartSampleNumberTextBox.Text);
                startNum = untilNum;
            }

            StartSampleNumberTextBox.Text = startNum.ToString();

            if (StartSampleTimeDateTimePicker.Enabled)
            {
                StartSampleTimeDateTimePicker.Value = opusTags[selectedEPC].LoggedData.FirstSampleTime.AddSeconds(opusTags[selectedEPC].Config.LogInterval.ToSeconds() * startNum);
            }
        }

        private void StartSampleTimeDateTimePicker_Validated(object sender, EventArgs e)
        {
            // TODO: Improve this event handler
            if (StartSampleTimeDateTimePicker.Value < opusTags[selectedEPC].LoggedData.FirstSampleTime)
            {
                StatusBoxAppend("ERROR: Start Sample Time can not be earlier than the First Sample Time");
                StartSampleTimeDateTimePicker.Value = opusTags[selectedEPC].LoggedData.FirstSampleTime;
            }

            if (StartSampleTimeDateTimePicker.Value > UntilSampleTimeDateTimePicker.Value)
            {
                StatusBoxAppend("ERROR: Start Sample Time can not be after the End Sample Time");
                StartSampleTimeDateTimePicker.Value = UntilSampleTimeDateTimePicker.Value;
            }

            double diffInSeconds = (StartSampleTimeDateTimePicker.Value - opusTags[selectedEPC].LoggedData.FirstSampleTime).TotalSeconds;
            int startNum = (int)(diffInSeconds / opusTags[selectedEPC].Config.LogInterval.ToSeconds());
            startNum = startNum < 0 ? 0 : startNum;
            startNum = startNum > 4095 ? 4095 : startNum;
            StartSampleNumberTextBox.Text = startNum.ToString();
        }

        private void StartSampleTimeDateTimePicker_CloseUp(object sender, EventArgs e)
        {
            // TODO: Improve this event handler
            if (StartSampleTimeDateTimePicker.Value < opusTags[selectedEPC].LoggedData.FirstSampleTime)
            {
                StatusBoxAppend("ERROR: Start Sample Time can not be earlier than the First Sample Time");
                StartSampleTimeDateTimePicker.Value = opusTags[selectedEPC].LoggedData.FirstSampleTime;
            }

            if (StartSampleTimeDateTimePicker.Value > UntilSampleTimeDateTimePicker.Value)
            {
                StatusBoxAppend("ERROR: Start Sample Time can not be after the End Sample Time");
                StartSampleTimeDateTimePicker.Value = UntilSampleTimeDateTimePicker.Value;
            }

            double diffInSeconds = (StartSampleTimeDateTimePicker.Value - opusTags[selectedEPC].LoggedData.FirstSampleTime).TotalSeconds;
            int startNum = (int)(diffInSeconds / opusTags[selectedEPC].Config.LogInterval.ToSeconds());
            startNum = startNum < 0 ? 0 : startNum;
            startNum = startNum > 4095 ? 4095 : startNum;
            StartSampleNumberTextBox.Text = startNum.ToString();
        }

        private void UntilSampleNumberTextBox_Validated(object sender, EventArgs e)
        {
            int untilNum = 4095;

            try
            {
                untilNum = int.Parse(UntilSampleNumberTextBox.Text);
            }
            catch (Exception ex)
            {
                StatusBoxAppend("ERROR: Invalid Until Sample Number: " + UntilSampleNumberTextBox.Text + ", Exception: " + ex.Message);
                untilNum = 4095;
            }

            if (untilNum > 4095)
            {
                StatusBoxAppend("ERROR: Until Sample Number can not be greater than 4095: " + UntilSampleNumberTextBox.Text);
                untilNum = 4095;
            }

            int startNum = int.Parse(StartSampleNumberTextBox.Text);
            if (untilNum < startNum)
            {
                StatusBoxAppend("ERROR: Until Sample Number can not be less than the Start Sample Number: " + UntilSampleNumberTextBox.Text);
                UntilSampleNumberTextBox.Text = StartSampleNumberTextBox.Text;
                untilNum = startNum;
            }

            UntilSampleNumberTextBox.Text = untilNum.ToString();

            if (UntilSampleTimeDateTimePicker.Enabled)
            {
                UntilSampleTimeDateTimePicker.Value = opusTags[selectedEPC].LoggedData.FirstSampleTime.AddSeconds(opusTags[selectedEPC].Config.LogInterval.ToSeconds() * untilNum);
            }
        }

        private void UntilSampleTimeDateTimePicker_Validated(object sender, EventArgs e)
        {
            // TODO: Improve this event handler
            double diffInSeconds = (UntilSampleTimeDateTimePicker.Value - opusTags[selectedEPC].LoggedData.FirstSampleTime).TotalSeconds;
            int endNum = (int)(diffInSeconds / opusTags[selectedEPC].Config.LogInterval.ToSeconds());
            if (endNum > 4095)
            {
                StatusBoxAppend("ERROR: End Sample Time can not be later than the First Sample Time + Maximum Log Time");
                UntilSampleTimeDateTimePicker.Value = opusTags[selectedEPC].LoggedData.FirstSampleTime.AddSeconds(opusTags[selectedEPC].Config.LogInterval.ToSeconds() * 4095);
            }

            if (UntilSampleTimeDateTimePicker.Value < StartSampleTimeDateTimePicker.Value)
            {
                StatusBoxAppend("ERROR: End Sample Time can not be earlier than the Start Sample Time");
                UntilSampleTimeDateTimePicker.Value = StartSampleTimeDateTimePicker.Value;
            }

            diffInSeconds = (UntilSampleTimeDateTimePicker.Value - opusTags[selectedEPC].LoggedData.FirstSampleTime).TotalSeconds;
            endNum = (int)(diffInSeconds / opusTags[selectedEPC].Config.LogInterval.ToSeconds());
            endNum = endNum < 0 ? 0 : endNum;
            endNum = endNum > 4095 ? 4095 : endNum;
            UntilSampleNumberTextBox.Text = endNum.ToString();
        }

        private void UntilSampleTimeDateTimePicker_CloseUp(object sender, EventArgs e)
        {
            // TODO: Improve this event handler
            double diffInSeconds = (UntilSampleTimeDateTimePicker.Value - opusTags[selectedEPC].LoggedData.FirstSampleTime).TotalSeconds;
            int endNum = (int)(diffInSeconds / opusTags[selectedEPC].Config.LogInterval.ToSeconds());
            if (endNum > 4095)
            {
                StatusBoxAppend("ERROR: End Sample Time can not be later than the First Sample Time + Maximum Log Time");
                UntilSampleTimeDateTimePicker.Value = opusTags[selectedEPC].LoggedData.FirstSampleTime.AddSeconds(opusTags[selectedEPC].Config.LogInterval.ToSeconds() * 4095);
            }

            if (UntilSampleTimeDateTimePicker.Value < StartSampleTimeDateTimePicker.Value)
            {
                StatusBoxAppend("ERROR: End Sample Time can not be earlier than the Start Sample Time");
                UntilSampleTimeDateTimePicker.Value = StartSampleTimeDateTimePicker.Value;
            }

            diffInSeconds = (UntilSampleTimeDateTimePicker.Value - opusTags[selectedEPC].LoggedData.FirstSampleTime).TotalSeconds;
            endNum = (int)(diffInSeconds / opusTags[selectedEPC].Config.LogInterval.ToSeconds());
            endNum = endNum < 0 ? 0 : endNum;
            endNum = endNum > 4095 ? 4095 : endNum;
            UntilSampleNumberTextBox.Text = endNum.ToString();
        }

        private void OpusReadLoggedDataButton_Click(object sender, EventArgs e)
        {
            SetAllControlsEnableProperty(false);
            LoggedDataListView.Visible = false;
            LoggedDataListView.Items.Clear();
            loggerPlot.Plot.Clear();
            loggerPlot.Render();
            NumReadSamplesTextBox.Text = "";

            if (!StartSampleTimeDateTimePicker.Enabled)
            {
                ClearLoggerStatusPanel();
                ClearLoggerConfigPanel();
                ClearLoggedDataPanel(false);

                try
                {
                    ushort[] pcWords;
                    Thread.Sleep(100);
                    ushort[] stateAndClock = ReadTagMemByEPC(selectedEPC, MemoryBank.USER, 0x05, 3, out _, "logger state and clock");
                    Thread.Sleep(100);
                    ushort[] loggerStatusTID = ReadTagMemByEPC(selectedEPC, MemoryBank.TID, 0x08, 20, out pcWords, "logger status TID 0x08_to_0x1B");
                    OpusTagStatus tagStatus = new OpusTagStatus(stateAndClock, loggerStatusTID, pcWords);
                    opusTags[selectedEPC].Status = tagStatus;
                    opusTags[selectedEPC].LoggedData.Status = tagStatus;
                    UpdateLoggerStatusPanel(tagStatus);
                    if (tagStatus.valid)
                    {
                        StatusBoxAppend("SUCCESS: Read Logger Status Operation");
                    }
                    else
                    {
                        StatusBoxAppend("FAIL: Read Logger Status Operation");
                    }
                    Thread.Sleep(100);
                    ushort[] loggerConfig_tid_0x08_to_0x1F = ReadTagMemByEPC(selectedEPC, MemoryBank.TID, 0x08, 24, out _, "logger config TID 0x08_to_0x1F");
                    OpusTagConfiguration tagConfig = new OpusTagConfiguration(loggerConfig_tid_0x08_to_0x1F);
                    opusTags[selectedEPC].Config = tagConfig;
                    opusTags[selectedEPC].LoggedData.Config = tagConfig;
                    UpdateLoggerConfigPanel(tagConfig);
                    if (tagConfig.valid)
                    {
                        StatusBoxAppend("SUCCESS: Read Logger Configuration Operation");
                    }
                    else
                    {
                        StatusBoxAppend("FAIL: Read Logger Configuration Operation");
                    }
                }
                catch (Exception ex)
                {
                    StatusBoxAppend("ERROR: Read Logger Info Operation: " + ex.Message);
                }

                UpdateLoggedDataPanel(opusTags[selectedEPC].LoggedData, true);
            }

            if (!StartSampleTimeDateTimePicker.Enabled)
            {
                SetAllControlsEnableProperty(true);
                return;
            }

            StatusBoxAppend("OPERATION: Started reading logged data...");
            DateTime startTime = System.DateTime.Now;
            try
            {
                int startSample = int.Parse(StartSampleNumberTextBox.Text);
                int untilSample = int.Parse(UntilSampleNumberTextBox.Text);
                int logIntervalInSeconds = opusTags[selectedEPC].LoggedData.Config.LogInterval.ToSeconds();
                DateTime startLogTime = StartSampleTimeDateTimePicker.Value;

                double samplesPerDay = 24 * 3600 / logIntervalInSeconds;
                double[] tempValues = new double[4096];
                int epcLen = selectedEPC.Length;
                int epcTailIndex = epcLen - 4 < 0 ? 0 : epcLen - 4;
                string plotTag = selectedEPC.Substring(epcTailIndex);

                const int MIN_ADDRESS = 0x00A0;
                const int MAX_ADDRESS = 0x109F;
                int bankAddress = MIN_ADDRESS + startSample;
                int maxRetries = 10;
                int numberLeftToRead = untilSample - startSample + 1;
                if (bankAddress + numberLeftToRead - 1 > MAX_ADDRESS)
                {
                    numberLeftToRead = MAX_ADDRESS - bankAddress + 1;
                }
                int numRead = 0;
                int counter = 0;
                bool stop = false;
                while (maxRetries > 0 && numberLeftToRead > 0 && !stop)
                {
                    int MAX_WORD_READ = reader.MaxNumberOfReadWords;
                    int numberToRead = MAX_WORD_READ;
                    if (numberToRead > numberLeftToRead)
                    {
                        numberToRead = numberLeftToRead;
                    }
                    if ((bankAddress + numberToRead - 1) > MAX_ADDRESS)
                    {
                        numberToRead = MAX_ADDRESS - bankAddress + 1;
                    }

                    Thread.Sleep(100);
                    ushort[] dataWords = ReadTagMemByEPC(selectedEPC, MemoryBank.USER, (uint)bankAddress, (byte)(numberToRead), out _, "logged data. Address: " + bankAddress.ToString());
                    if (dataWords == null || dataWords.Length != numberToRead)
                    {
                        maxRetries -= 1;
                        continue;
                    }
                    foreach (ushort us in dataWords)
                    {
                        uint ui = us; // < 8192 ? (uint)(us + 0xFFF0) : us;
                        if (ui == 0xFFFF)
                        {
                           // stop = true; // No more real measurements, flash memory is 0xFFFF
                           // break;
                        }
                        double tempInC = (short)(ui & 0xFFF0) / 256.00; // Degrees C
                        //double tempInC = (ui & 0xFFF0) / 16000.0; // Voltage


                        ListViewItem item = new ListViewItem();
                        item = new ListViewItem(startLogTime.AddSeconds(logIntervalInSeconds * counter).ToString("yyyy/MM/dd hh:mm:ss tt"));
                        //item.SubItems.Add(string.Format("{0:N2}", tempInC));
                        item.SubItems.Add(string.Format("{0:N3}", tempInC) + "," + ui.ToString("X4"));
                        LoggedDataListView.Items.Add(item);
                        tempValues[counter++] = tempInC;

                        if ((counter % 100) == 0)
                        {
                            loggerPlot.Plot.Clear();
                            loggerPlot.Plot.Legend();
                            ScottPlot.Plottable.SignalPlot signal = loggerPlot.Plot.AddSignal(tempValues, sampleRate: samplesPerDay, color: Color.Blue, plotTag);
                            signal.MaxRenderIndex = counter - 1;
                            signal.OffsetX = startLogTime.ToOADate();
                            loggerPlot.Plot.AxisAuto();
                            loggerPlot.Render();
                            Application.DoEvents();
                        }

                        //double tempInC = ((double)(ui & 0xFFF0)) / 16000.00; // for voltage
                        //loggedData = loggedData + string.Format("{0:N2},", tempInC) + ui.ToString() + Environment.NewLine;
                        //loggedData = loggedData + string.Format("{0:N2}", tempInC) + Environment.NewLine;
                    }
                    bankAddress += MAX_WORD_READ;
                    numberLeftToRead -= numberToRead;
                    numRead += numberToRead;
                    NumReadSamplesTextBox.Text = numRead.ToString();
                }

                loggerPlot.Plot.Clear();
                loggerPlot.Plot.Legend();
                ScottPlot.Plottable.SignalPlot signalEnd = loggerPlot.Plot.AddSignal(tempValues, sampleRate: samplesPerDay, color: Color.Blue, plotTag);
                signalEnd.MaxRenderIndex = counter - 1;
                signalEnd.OffsetX = startLogTime.ToOADate();
                loggerPlot.Plot.AxisAuto();
                loggerPlot.Render();

                opusTags[selectedEPC].LoggedData.ListViewItems = new List<ListViewItem>();
                if (LoggedDataListView.Items != null)
                {
                    foreach (ListViewItem item in LoggedDataListView.Items)
                    {
                        opusTags[selectedEPC].LoggedData.ListViewItems.Add(item);
                    }
                }

                opusTags[selectedEPC].LoggedData.Temperatures = tempValues;
                opusTags[selectedEPC].LoggedData.NumSamples = counter;
                opusTags[selectedEPC].LoggedData.StartLogTime = startLogTime;
                opusTags[selectedEPC].LoggedData.PlotTag = plotTag;
                opusTags[selectedEPC].LoggedData.SamplesPerDay = samplesPerDay;
            }
            catch (Exception ex)
            {
                StatusBoxAppend("ERROR: Read Logger Data Operation: " + ex.Message);
            }

            long elapsedTicks = DateTime.Now.Ticks - startTime.Ticks;
            //readLoggerTimeTextBox.Text = (elapsedTicks / 10000000.0).ToString("N2");

            LoggedDataListView.Visible = true;
            SetAllControlsEnableProperty(true);
        }

        private void CopyLoggedDataToClipButton_Click(object sender, EventArgs e)
        {
            String text = "";
            foreach (ListViewItem item in LoggedDataListView.Items)
            {
                if (OnlyTempCheckBox.Checked)
                {
                    text += item.SubItems[1].Text;
                }
                else
                {
                    text += item.SubItems[0].Text + "," + item.SubItems[1].Text;
                }
                text += "\r\n";
            }
            if (text == "") text = "No Items Were Found";
            Clipboard.SetText(text);
        }

        //--------------- OPUS TAB -- Utils -- GUI

        private void StatusBoxAppend(String newText, bool displayStatus = true)
        {
            if (!displayStatus) return;
            const int MAXLEN = 10000;
            const int CLEANLEN = 8000;
            statusTextBox.AppendText(System.Environment.NewLine + newText);
            int statusLength = statusTextBox.Text.Length;
            if (statusLength > MAXLEN)
            {
                statusTextBox.Text = statusTextBox.Text.Substring(statusLength - CLEANLEN);
                statusTextBox.SelectionStart = statusTextBox.TextLength;
                statusTextBox.ScrollToCaret();
            }
            Application.DoEvents();
        }

        private double[] GetRfPowers()
        {
            string sPowers = RfPowersTextBox.Text;
            if (sPowers == "")
            {
                sPowers = "7.0, 9.0";
                RfPowersTextBox.Text = sPowers;
                Application.DoEvents();
            }

            double[] powers = Array.ConvertAll<string, double>(sPowers.Split(','), double.Parse);
            return powers;
        }

        private void SetActionControlsEnableProperty(bool enableProp)
        {
            ReadLoggerStatusButton.Enabled = enableProp;
            ReadLoggerConfigButton.Enabled = enableProp;
            LoadConfigurationButton.Enabled = enableProp;
            SaveConfigurationButton.Enabled = enableProp;
            SetBapModeButton.Enabled = enableProp;
            WriteConfigurationButton.Enabled = enableProp;
            StartTheLoggerButton.Enabled = enableProp;
            ReadLoggerInfoButton.Enabled = enableProp;
            ReadLoggedDataButton.Enabled = enableProp;
            CopyLoggedDataToClipButton.Enabled = enableProp;
            OpusListView.Focus();
            Application.DoEvents();
        }

        private void SetDefaultEnabledControlsEnableProperty(bool enableProp)
        {
            OpusListView.Enabled = enableProp;
            FindOpusTagsButton.Enabled = enableProp;
            ClearListButton.Enabled = enableProp;
            SetDefaultConfigurationButton.Enabled = enableProp;
        }

        private void SetAllControlsEnableProperty(bool enableProp)
        {
            SetDefaultEnabledControlsEnableProperty(enableProp);
            SetActionControlsEnableProperty(enableProp);
        }

        private void UpdateActionControls()
        {
            if (OpusListView.SelectedItems != null && OpusListView.SelectedItems.Count > 0)
            {
                SetActionControlsEnableProperty(true);
            }
            else
            {
                SetActionControlsEnableProperty(false);
            }
        }

        private void ClearLoggerStatusPanel()
        {
            ReadStatusResultLED.CheckState = CheckState.Indeterminate;
            LoggerStateTextBox.Text = "";
            RtcTextBox.Text = "";
            NextLogAddressTextBox.Text = "";
            LoggerStartTimestampTextBox.Text = "";
            FingerSpotStartTimeStampTextBox.Text = "";
            HighTempAlarmLED.CheckState = CheckState.Indeterminate;
            LowTempAlarmLED.CheckState = CheckState.Indeterminate;
            BatteryAlarmLED.CheckState = CheckState.Indeterminate;
            TamperAlarmLED.CheckState = CheckState.Indeterminate;
            InitLogBatteryAlarmLED.CheckState = CheckState.Indeterminate;
            TemperatureViolationAddrTextBox.Text = "";
            TamperViolationAddrTextBox.Text = "";
            Application.DoEvents();
        }

        private void ClearLoggerConfigPanel()
        {
            ReadConfigurationLED.CheckState = CheckState.Indeterminate;
            LogIntervalComboBox.SelectedItem = null;
            DelayedStartComboBox.SelectedItem = null;
            NumSamplesToLogComboBox.SelectedItem = null;
            TemperatureLowerLimitTextBox.Text = "";
            AlarmDelayLowerLimitComboBox.SelectedItem = null;
            TemperatureUpperLimitTextBox.Text = "";
            AlarmDelayUpperLimitComboBox.SelectedItem = null;
            FingerSpotEnabledCheckBox.Checked = false;
            StartLoggingWithFingerSpotCheckBox.Checked = false;
            LedEnabledCheckBox.Checked = false;
            LedModeCheckBox.Checked = false;
            LedOffTimeComboBox.SelectedItem = null;
            LedOnTimeComboBox.SelectedItem = null;
            AntiTamperEnabledCheckBox.Checked = false;
            AntiTamperPolarityCheckBox.Checked = false;
            Application.DoEvents();
        }

        private void ClearLoggerArmingPanel()
        {
            SetBapModeLED.CheckState = CheckState.Indeterminate;
            WriteConfigurationLED.CheckState = CheckState.Indeterminate;
            StartTheLoggerLED.CheckState = CheckState.Indeterminate;
            LoggerStartTimestampWrittenTextBox.Text = "";
        }

        private void ClearLoggedDataPanel(bool clearSampleNumber = true)
        {
            ReadLoggerInfoResultLED.CheckState = CheckState.Indeterminate;
            readLoggedDataResultLED.CheckState = CheckState.Indeterminate;
            loggerIntervalTextBox.Text = "";
            FirstSampleTimeTextBox.Text = "";
            if (clearSampleNumber) StartSampleNumberTextBox.Text = "0";
            StartSampleTimeDateTimePicker.Enabled = false;
            StartSampleTimeDateTimePicker.CustomFormat = " ";
            if (clearSampleNumber) UntilSampleNumberTextBox.Text = "4095";
            UntilSampleTimeDateTimePicker.Enabled = false;
            UntilSampleTimeDateTimePicker.CustomFormat = " ";
            NumReadSamplesTextBox.Text = "0";
            LoggedDataListView.Items.Clear();
            loggerPlot.Plot.Clear();
            loggerPlot.Render();
        }

        private void UpdateLoggerStatusPanel(OpusTagStatus tagStatus)
        {
            if (tagStatus != null && tagStatus.valid)
            {
                LoggerStateTextBox.Text = OpusStateEx.Description(tagStatus.State);
                NextLogAddressTextBox.Text = tagStatus.NextLogAddress.ToString();
                LoggerStartTimestampTextBox.Text = tagStatus.startTimestamp.timeInSecondsSince1970 > 0 ? tagStatus.StartTimestampDateTime.ToString("MM/dd/yyyy HH:mm:ss") : "Blank";
                if (tagStatus.State == OpusState.SLEEP || tagStatus.State == OpusState.STANDBY || tagStatus.State == OpusState.READY)
                {
                    RtcTextBox.Text = "Not Running";
                    FingerSpotStartTimeStampTextBox.Text = "Num RTC Cycles: " + tagStatus.fingerSpotStartTimestamp.numberOfCycles.ToString();
                }
                else
                {
                    RtcTextBox.Text = tagStatus.RtcClockDateTime.ToString("MM/dd/yyyy HH:mm:ss");
                    FingerSpotStartTimeStampTextBox.Text = tagStatus.FingerTouchLogEnabled ? tagStatus.FingerSpotStartTimestampDateTime.ToString("MM/dd/yyyy HH:mm:ss") : "Not Enabled";
                }
                HighTempAlarmLED.CheckState = tagStatus.HighTemperatureAlarm ? CheckState.Checked : CheckState.Unchecked;
                LowTempAlarmLED.CheckState = tagStatus.LowTemperatureAlarm ? CheckState.Checked : CheckState.Unchecked;
                BatteryAlarmLED.CheckState = tagStatus.BatteryAlarm ? CheckState.Checked : CheckState.Unchecked;
                TamperAlarmLED.CheckState = tagStatus.TamperAlarm ? CheckState.Checked : CheckState.Unchecked;
                InitLogBatteryAlarmLED.CheckState = tagStatus.BatteryInitAlarm ? CheckState.Checked : CheckState.Unchecked;
                TemperatureViolationAddrTextBox.Text = tagStatus.TemperatureViolationAddress.ToString();
                TamperViolationAddrTextBox.Text = tagStatus.TamperViolationAddress.ToString();
                ReadStatusResultLED.CheckState = CheckState.Unchecked;
            }
            else
            {
                ClearLoggerStatusPanel();
                ReadStatusResultLED.CheckState = tagStatus == null ? CheckState.Indeterminate : CheckState.Checked;
            }
        }

        private void UpdateLoggerConfigPanel(OpusTagConfiguration tagConfig)
        {
            if (tagConfig != null && tagConfig.valid)
            {
                LogIntervalComboBox.SelectedIndex = tagConfig.LogInterval.Index;
                DelayedStartComboBox.SelectedIndex = tagConfig.DelayedLoggerStartPeriods - 1;  // GUI shows 1 to 7, Indexes are 0 to 6
                NumSamplesToLogComboBox.SelectedIndex = (int)tagConfig.NumberOfSamplesToLog;
                TemperatureLowerLimitTextBox.Text = String.Format("{0:0.0}", tagConfig.TemperatureLowerLimit);
                TemperatureUpperLimitTextBox.Text = String.Format("{0:0.0}", tagConfig.TemperatureUpperLimit);
                AlarmDelayLowerLimitComboBox.SelectedIndex = tagConfig.TemperatureLowerLimitAlarmDelay;
                AlarmDelayUpperLimitComboBox.SelectedIndex = tagConfig.TemperatureUpperLimitAlarmDelay;
                FingerSpotEnabledCheckBox.Checked = tagConfig.FingerSpotEnabled;
                StartLoggingWithFingerSpotCheckBox.Checked = tagConfig.StartLoggingWithFingerSpot;
                LedEnabledCheckBox.Checked = tagConfig.LedEnabled;
                LedModeCheckBox.Checked = tagConfig.LedOnDemandMode;
                LedOffTimeComboBox.SelectedIndex = tagConfig.LedOffTimeInTwoSecondsUnits; // 0 means 2 seconds, 1 means 4 seconds, ...
                LedOnTimeComboBox.SelectedIndex = tagConfig.LedOnTimeInRtcCycles; // 0 means 1 RTC cycle, 1 means 2 RTC cycles, ... (1 RTC cycle = 10ms)
                AntiTamperEnabledCheckBox.Checked = tagConfig.AntiTamperEnabled;
                AntiTamperPolarityCheckBox.Checked = tagConfig.AntiTamperPolarity;
                ReadConfigurationLED.CheckState = CheckState.Unchecked;
            }
            else
            {
                ClearLoggerConfigPanel();
                ReadConfigurationLED.CheckState = tagConfig == null ? CheckState.Indeterminate : CheckState.Checked;
            }
        }

        private void UpdateLoggedDataPanel(OpusLoggedData tagLoggedData, bool keepSampleNumbers = false)
        {
            if (tagLoggedData.Status != null && tagLoggedData.Status.valid && tagLoggedData.Status.NextLogAddress > 0 &&
                tagLoggedData.Config != null && tagLoggedData.Config.valid)
            {
                loggerIntervalTextBox.Text = tagLoggedData.Config.LogInterval.ToString();
                if (tagLoggedData.Config.StartLoggingWithFingerSpot)
                {
                    tagLoggedData.FirstSampleTime = tagLoggedData.Status.FingerSpotStartTimestampDateTime;
                }
                else
                {
                    tagLoggedData.FirstSampleTime = tagLoggedData.Status.StartTimestampDateTime;
                }

                tagLoggedData.FirstSampleTime = tagLoggedData.FirstSampleTime.AddSeconds(tagLoggedData.Config.DelayedLoggerStartPeriods * tagLoggedData.Config.LogInterval.ToSeconds());
                FirstSampleTimeTextBox.Text = tagLoggedData.FirstSampleTime.ToString("yyyy/MM/dd hh:mm:ss tt");

                if (keepSampleNumbers)
                {
                    tagLoggedData.StartSampleNumber = int.Parse(StartSampleNumberTextBox.Text);
                    tagLoggedData.UntilSampleNumber = int.Parse(UntilSampleNumberTextBox.Text);
                }
                else
                {
                    tagLoggedData.StartSampleNumber = tagLoggedData.StartSampleNumber == -1 ? 0 : tagLoggedData.StartSampleNumber;
                    tagLoggedData.UntilSampleNumber = tagLoggedData.Status.NextLogAddress - 1;
                }
                StartSampleNumberTextBox.Text = tagLoggedData.StartSampleNumber.ToString();
                UntilSampleNumberTextBox.Text = tagLoggedData.UntilSampleNumber.ToString();

                StartSampleTimeDateTimePicker.Enabled = true;
                StartSampleTimeDateTimePicker.CustomFormat = "yyyy/MM/dd hh:mm:ss tt";
                StartSampleTimeDateTimePicker.Value = tagLoggedData.FirstSampleTime.AddSeconds(tagLoggedData.StartSampleNumber * tagLoggedData.Config.LogInterval.ToSeconds());

                UntilSampleTimeDateTimePicker.Enabled = true;
                UntilSampleTimeDateTimePicker.CustomFormat = "yyyy/MM/dd hh:mm:ss tt";
                UntilSampleTimeDateTimePicker.Value = tagLoggedData.FirstSampleTime.AddSeconds(tagLoggedData.UntilSampleNumber * tagLoggedData.Config.LogInterval.ToSeconds());

                LoggedDataListView.Items.Clear();
                if (opusTags[selectedEPC].LoggedData.ListViewItems != null)
                {
                    LoggedDataListView.Visible = false;
                    foreach (ListViewItem item in opusTags[selectedEPC].LoggedData.ListViewItems)
                    {
                        LoggedDataListView.Items.Add(item);
                    }
                    LoggedDataListView.Visible = true;
                }

                loggerPlot.Plot.Clear();
                loggerPlot.Plot.Legend();
                if (opusTags[selectedEPC].LoggedData.Temperatures != null && opusTags[selectedEPC].LoggedData.Temperatures.Length >= 1)
                {
                    ScottPlot.Plottable.SignalPlot signalEnd = loggerPlot.Plot.AddSignal(
                        opusTags[selectedEPC].LoggedData.Temperatures,
                        sampleRate: opusTags[selectedEPC].LoggedData.SamplesPerDay,
                        color: Color.Blue,
                        opusTags[selectedEPC].LoggedData.PlotTag
                        );
                    signalEnd.MaxRenderIndex = opusTags[selectedEPC].LoggedData.NumSamples - 1;
                    signalEnd.OffsetX = opusTags[selectedEPC].LoggedData.StartLogTime.ToOADate();
                    loggerPlot.Plot.AxisAuto();
                }
                loggerPlot.Render();

                ReadLoggerInfoResultLED.CheckState = CheckState.Unchecked;
                readLoggedDataResultLED.CheckState = CheckState.Unchecked;
            }
            else
            {
                ClearLoggedDataPanel();
                if ((tagLoggedData.Status != null && !tagLoggedData.Status.valid) ||
                    (tagLoggedData.Config != null && !tagLoggedData.Config.valid)
                    )
                {
                    ReadLoggerInfoResultLED.CheckState = CheckState.Checked;
                    readLoggedDataResultLED.CheckState = CheckState.Checked;
                }
            }
        }

        private OpusTagConfiguration GuiConfigToOpusConfig()
        {
            double temperatureUpperLimit = double.NaN, temperatureLowerLimit = double.NaN;
            try
            {
                if (TemperatureLowerLimitTextBox.Text == "" || TemperatureUpperLimitTextBox.Text == "")
                {
                    throw new Exception("Temperature alarm limit text boxs can not be empty");
                }

                temperatureLowerLimit = double.Parse(TemperatureLowerLimitTextBox.Text);
                temperatureUpperLimit = double.Parse(TemperatureUpperLimitTextBox.Text);

                if (temperatureLowerLimit < -128.0 || temperatureLowerLimit > 127.996 ||
                    temperatureUpperLimit < -128.0 || temperatureUpperLimit > 127.996
                    )
                {
                    throw new Exception("Temperature alarm limit are out of range [-128.000, 127.996]");
                }
            }
            catch (Exception ex)
            {
                StatusBoxAppend("ERROR: " + ex.Message);
                return null;
            }

            if (LogIntervalComboBox.SelectedItem == null ||
                DelayedStartComboBox.SelectedItem == null ||
                NumSamplesToLogComboBox.SelectedItem == null ||
                AlarmDelayLowerLimitComboBox.SelectedItem == null ||
                AlarmDelayUpperLimitComboBox.SelectedItem == null ||
                LedOffTimeComboBox.SelectedItem == null ||
                LedOnTimeComboBox.SelectedItem == null
               )
            {
                StatusBoxAppend("ERROR: There is one or more combo boxes without selection");
                return null;
            }

            if (StartLoggingWithFingerSpotCheckBox.Checked && !FingerSpotEnabledCheckBox.Checked)
            {
                StatusBoxAppend("ERROR: FingerSpot must be enabled if the tag is configured to start logging using the FingerSpot");
                return null;
            }

            OpusTagConfiguration config = new OpusTagConfiguration();
            config.LogInterval = new OpusLogInterval(LogIntervalComboBox.SelectedIndex);
            config.DelayedLoggerStartPeriods = DelayedStartComboBox.SelectedIndex + 1; // GUI shows 1 to 7, Indexes are 0 to 6
            config.NumberOfSamplesToLog = (OpusNumberOfSamplesToLog)NumSamplesToLogComboBox.SelectedIndex;
            config.TemperatureLowerLimit = temperatureLowerLimit;
            config.TemperatureUpperLimit = temperatureUpperLimit;
            config.TemperatureLowerLimitAlarmDelay = AlarmDelayLowerLimitComboBox.SelectedIndex;
            config.TemperatureUpperLimitAlarmDelay = AlarmDelayUpperLimitComboBox.SelectedIndex;
            config.FingerSpotEnabled = FingerSpotEnabledCheckBox.Checked;
            config.StartLoggingWithFingerSpot = StartLoggingWithFingerSpotCheckBox.Checked;
            config.LedEnabled = LedEnabledCheckBox.Checked;
            config.LedOnDemandMode = LedModeCheckBox.Checked;
            config.LedOffTimeInTwoSecondsUnits = LedOffTimeComboBox.SelectedIndex;
            config.LedOnTimeInRtcCycles = LedOnTimeComboBox.SelectedIndex;
            config.AntiTamperEnabled = AntiTamperEnabledCheckBox.Checked;
            config.AntiTamperPolarity = AntiTamperPolarityCheckBox.Checked;

            return config;
        }
    }

    // TODO: Integrate all Opus classes into a single Opus Class

    public enum OpusState
    {
        SLEEP = 0,
        STANDBY = 1,
        INTERNAL_02 = 2,
        READY = 3,
        ARMED = 4,
        BAP_MODE = 5,
        LOGGING = 6,
        FINISHED = 7,
        INTERNAL_08 = 8,
        INTERNAL_09 = 9,
        INTERNAL_10 = 10,
        INTERNAL_11 = 11,
        INTERNAL_12 = 12,
        INTERNAL_13 = 13,
        INTERNAL_14 = 14,
        INTERNAL_15 = 15,
        INTERNAL_16 = 16,
        INTERNAL_17 = 17,
        INTERNAL_18 = 18,
        INTERNAL_19 = 19,
        INTERNAL_20 = 20,
        INVALID = 21
    }

    public class OpusStateEx
    {
        readonly public OpusState state;

        public static string Description(OpusState par_state)
        {
            switch (par_state)
            {
                case OpusState.SLEEP:
                    return "Sleep";
                case OpusState.STANDBY:
                    return "Standby";
                case OpusState.INTERNAL_02:
                    return "INTERNAL 02";
                case OpusState.READY:
                    return "Ready";
                case OpusState.ARMED:
                    return "Armed";
                case OpusState.BAP_MODE:
                    return "BAP Mode";
                case OpusState.LOGGING:
                    return "Logging";
                case OpusState.FINISHED:
                    return "Finished";
                case OpusState.INTERNAL_08:
                    return "INTERNAL 08";
                case OpusState.INTERNAL_09:
                    return "INTERNAL 09";
                case OpusState.INTERNAL_10:
                    return "INTERNAL 10";
                case OpusState.INTERNAL_11:
                    return "INTERNAL 11";
                case OpusState.INTERNAL_12:
                    return "INTERNAL 12";
                case OpusState.INTERNAL_13:
                    return "INTERNAL 13";
                case OpusState.INTERNAL_14:
                    return "INTERNAL 14";
                case OpusState.INTERNAL_15:
                    return "INTERNAL 15";
                case OpusState.INTERNAL_16:
                    return "INTERNAL 16";
                case OpusState.INTERNAL_17:
                    return "INTERNAL 17";
                case OpusState.INTERNAL_18:
                    return "INTERNAL 18";
                case OpusState.INTERNAL_19:
                    return "INTERNAL 19";
                case OpusState.INTERNAL_20:
                    return "INTERNAL 20";
                case OpusState.INVALID:
                    return "INVALID";
                default:
                    return "ERROR_INVALID";
            }
        }

        public string Description()
        {
            return Description(state);
        }

        public static ushort StateFromUserBankToStateNumber(ushort word_0x05)
        {
            return (ushort)(word_0x05 & 0x001F);
        }

        public static ushort StateFromPacketPcToStateNumber(ushort word_0x01)
        {
            return (ushort)((word_0x01 & 0x00E0) >> 5);
        }

        public static OpusState StateNumToOpusState(ushort stateNum)
        {
            if (stateNum >= 0 && stateNum <= 20)
            {
                return (OpusState)stateNum;
            }
            else
            {
                return OpusState.INVALID;
            }
        }

        public static OpusState StateFromUserBankToOpusState(ushort word)
        {
            ushort stateNum = StateFromUserBankToStateNumber(word);
            return StateNumToOpusState(stateNum);
        }

        public static OpusState StateFromUserBankToOpusState(ushort[] words)
        {
            if (words == null || words.Length < 1)
            {
                return OpusState.INVALID;
            }

            return StateFromUserBankToOpusState(words[0]);
        }

        public static OpusState StateFromPacketPcToOpusState(ushort word)
        {
            ushort stateNum = StateFromPacketPcToStateNumber(word);
            return StateNumToOpusState(stateNum);
        }

        public static OpusState StateFromPcWordsToOpusState(ushort[] words)
        {
            if (words == null || words.Length < 1)
            {
                return OpusState.INVALID;
            }

            return StateFromPacketPcToOpusState(words[0]);
        }

        public OpusStateEx(ushort word, bool fromUserBank = true) // from User Bank or PacketPC
        {
            state = fromUserBank ? StateFromUserBankToOpusState(word) : StateFromPacketPcToOpusState(word);
        }

        public OpusStateEx(ushort[] words, bool fromUserBank = true)
        {
            state = fromUserBank ? StateFromUserBankToOpusState(words) : StateFromPcWordsToOpusState(words);
        }

    }

    public enum OpusAlarm
    {
        Temp = 0,
        HighTemp = 1,
        LowTemp = 2,
        Battery = 3,
        InitBat = 4,
        Tamper = 5
    }

    public class OpusLogInterval
    {
        readonly public bool valid = false;
        readonly private int logIntervalIndex = 0;
        readonly private bool ssdSamplingRegimeOverride = false;
        readonly private ushort samplingRegime = 0;

        public int Index => logIntervalIndex;
        public bool SsdSamplingRegimeOverride => ssdSamplingRegimeOverride;
        public ushort SamplingRegime => samplingRegime;

        public override string ToString()
        {
            switch (Index)
            {
                case 0:
                    return "1 second";
                case 1:
                    return "5 seconds";
                case 2:
                    return "10 seconds";
                case 3:
                    return "15 seconds";
                case 4:
                    return "20 seconds";
                case 5:
                    return "25 seconds";
                case 6:
                    return "30 seconds";
                case 7:
                    return "1 minute";
                case 8:
                    return "2 minutes";
                case 9:
                    return "3 minutes";
                case 10:
                    return "4 minutes";
                case 11:
                    return "5 minutes";
                case 12:
                    return "6 minutes";
                case 13:
                    return "7 minutes";
                case 14:
                    return "10 minutes";
                case 15:
                    return "15 minutes";
                case 16:
                    return "20 minutes";
                case 17:
                    return "25 minutes";
                case 18:
                    return "30 minutes";
                case 19:
                    return "35 minutes";
                case 20:
                    return "40 minutes";
                case 21:
                    return "1 hour";
                case 22:
                    return "2 hours";
                case 23:
                    return "3 hours";
                case 24:
                    return "4 hours";
                case 25:
                    return "5 hours";
                case 26:
                    return "6 hours";
                case 27:
                    return "7 hours";
                case 28:
                    return "8 hours";
                default:
                    return "ERROR: Undefined";
            }
        }

        public int ToSeconds()
        {
            switch (Index)
            {
                case 0:
                    return 1;
                case 1:
                    return 5;
                case 2:
                    return 10;
                case 3:
                    return 15;
                case 4:
                    return 20;
                case 5:
                    return 25;
                case 6:
                    return 30;
                case 7:
                    return 1 * 60;
                case 8:
                    return 2 * 60;
                case 9:
                    return 3 * 60;
                case 10:
                    return 4 * 60;
                case 11:
                    return 5 * 60;
                case 12:
                    return 6 * 60;
                case 13:
                    return 7 * 60;
                case 14:
                    return 10 * 60;
                case 15:
                    return 15 * 60;
                case 16:
                    return 20 * 60;
                case 17:
                    return 25 * 60;
                case 18:
                    return 30 * 60;
                case 19:
                    return 35 * 60;
                case 20:
                    return 40 * 60;
                case 21:
                    return 1 * 3600;
                case 22:
                    return 2 * 3600;
                case 23:
                    return 3 * 3600;
                case 24:
                    return 4 * 3600;
                case 25:
                    return 5 * 3600;
                case 26:
                    return 6 * 3600;
                case 27:
                    return 7 * 3600;
                case 28:
                    return 8 * 3600;
                default:
                    return 0; // ERROR
            }
        }

        public OpusLogInterval(bool par_ssdSamplingRegimeOverride, ushort par_samplingRegime)
        {
            valid = true;
            ssdSamplingRegimeOverride = par_ssdSamplingRegimeOverride;
            samplingRegime = par_samplingRegime;

            if (ssdSamplingRegimeOverride)
            {
                switch (samplingRegime)
                {
                    case 0x00:
                        logIntervalIndex = 0;   // 1 second
                        break;
                    case 0x01:
                        logIntervalIndex = 1;   // 5 seconds
                        break;
                    case 0x02:
                        logIntervalIndex = 2;   // 10 seconds
                        break;
                    case 0x03:
                        logIntervalIndex = 3;   // 15 seconds
                        break;
                    case 0x04:
                        logIntervalIndex = 4;   // 20 seconds
                        break;
                    case 0x05:
                        logIntervalIndex = 5;   // 25 seconds
                        break;
                    case 0x06:
                        logIntervalIndex = 6;   // 30 seconds
                        break;
                    case 0x07:
                        logIntervalIndex = 7;   // 1 minute
                        break;
                    case 0x08:
                        logIntervalIndex = 8;   // 2 minutes
                        break;
                    case 0x09:
                        logIntervalIndex = 9;   // 3 minutes
                        break;
                    case 0x0A:
                        logIntervalIndex = 10;  // 4 minutes
                        break;
                    case 0x0B:
                        logIntervalIndex = 12;  // 6 minutes
                        break;
                    case 0x0C:
                        logIntervalIndex = 13;  // 7 minutes
                        break;
                    default:
                        valid = false;
                        break;
                }
            }
            else
            {
                switch (samplingRegime)
                {
                    case 0x00:
                        logIntervalIndex = 11;   // 5 minutes
                        break;
                    case 0x01:
                        logIntervalIndex = 14;   // 10 minutes
                        break;
                    case 0x02:
                        logIntervalIndex = 15;   // 15 minutes
                        break;
                    case 0x03:
                        logIntervalIndex = 16;   // 20 minutes
                        break;
                    case 0x04:
                        logIntervalIndex = 17;   // 25 minutes
                        break;
                    case 0x05:
                        logIntervalIndex = 18;   // 30 minutes
                        break;
                    case 0x06:
                        logIntervalIndex = 19;   // 35 minutes
                        break;
                    case 0x07:
                        logIntervalIndex = 20;   // 40 minutes
                        break;
                    case 0x08:
                        logIntervalIndex = 21;   // 1 hour
                        break;
                    case 0x09:
                        logIntervalIndex = 22;   // 2 hours
                        break;
                    case 0x0A:
                        logIntervalIndex = 23;   // 3 hours
                        break;
                    case 0x0B:
                        logIntervalIndex = 24;   // 4 hours
                        break;
                    case 0x0C:
                        logIntervalIndex = 25;   // 5 hours
                        break;
                    case 0x0D:
                        logIntervalIndex = 26;   // 6 hours
                        break;
                    case 0x0E:
                        logIntervalIndex = 27;   // 7 hours
                        break;
                    case 0x0F:
                        logIntervalIndex = 28;   // 8 hours
                        break;
                    default:
                        valid = false;
                        break;
                }
            }
        }

        public OpusLogInterval(int par_logIntervalIndex)
        {
            valid = true;
            logIntervalIndex = par_logIntervalIndex;
            switch (logIntervalIndex)
            {
                case 0: // 1 second
                    ssdSamplingRegimeOverride = true;
                    samplingRegime = 0x00;
                    break;
                case 1: // 5 seconds
                    ssdSamplingRegimeOverride = true;
                    samplingRegime = 0x01;
                    break;
                case 2: // 10 seconds
                    ssdSamplingRegimeOverride = true;
                    samplingRegime = 0x02;
                    break;
                case 3: // 15 seconds
                    ssdSamplingRegimeOverride = true;
                    samplingRegime = 0x03;
                    break;
                case 4: // 20 seconds
                    ssdSamplingRegimeOverride = true;
                    samplingRegime = 0x04;
                    break;
                case 5: // 25 seconds
                    ssdSamplingRegimeOverride = true;
                    samplingRegime = 0x05;
                    break;
                case 6: // 30 seconds
                    ssdSamplingRegimeOverride = true;
                    samplingRegime = 0x06;
                    break;
                case 7: // 1 minute
                    ssdSamplingRegimeOverride = true;
                    samplingRegime = 0x07;
                    break;
                case 8:  // 2 minutes
                    ssdSamplingRegimeOverride = true;
                    samplingRegime = 0x08;
                    break;
                case 9:  // 3 minutes
                    ssdSamplingRegimeOverride = true;
                    samplingRegime = 0x09;
                    break;
                case 10:  // 4 minutes
                    ssdSamplingRegimeOverride = true;
                    samplingRegime = 0x0A;
                    break;
                case 11:  // 5 minutes
                    ssdSamplingRegimeOverride = false;
                    samplingRegime = 0x00;
                    break;
                case 12:  // 6 minutes
                    ssdSamplingRegimeOverride = true;
                    samplingRegime = 0x0B;
                    break;
                case 13:  // 7 minutes
                    ssdSamplingRegimeOverride = true;
                    samplingRegime = 0x0C;
                    break;
                case 14:  // 10 minutes
                    ssdSamplingRegimeOverride = false;
                    samplingRegime = 0x01;
                    break;
                case 15:  // 15 minutes
                    ssdSamplingRegimeOverride = false;
                    samplingRegime = 0x02;
                    break;
                case 16:  // 20 minutes
                    ssdSamplingRegimeOverride = false;
                    samplingRegime = 0x03;
                    break;
                case 17:  // 25 minutes
                    ssdSamplingRegimeOverride = false;
                    samplingRegime = 0x04;
                    break;
                case 18:  // 30 minutes
                    ssdSamplingRegimeOverride = false;
                    samplingRegime = 0x05;
                    break;
                case 19:  // 35 minutes
                    ssdSamplingRegimeOverride = false;
                    samplingRegime = 0x06;
                    break;
                case 20:  // 40 minutes
                    ssdSamplingRegimeOverride = false;
                    samplingRegime = 0x07;
                    break;
                case 21:  // 1 hour
                    ssdSamplingRegimeOverride = false;
                    samplingRegime = 0x08;
                    break;
                case 22:  // 2 hours
                    ssdSamplingRegimeOverride = false;
                    samplingRegime = 0x09;
                    break;
                case 23:  // 3 hours
                    ssdSamplingRegimeOverride = false;
                    samplingRegime = 0x0A;
                    break;
                case 24:  // 4 hours
                    ssdSamplingRegimeOverride = false;
                    samplingRegime = 0x0B;
                    break;
                case 25:  // 5 hours
                    ssdSamplingRegimeOverride = false;
                    samplingRegime = 0x0C;
                    break;
                case 26:  // 6 hours
                    ssdSamplingRegimeOverride = false;
                    samplingRegime = 0x0D;
                    break;
                case 27:  // 7 hours
                    ssdSamplingRegimeOverride = false;
                    samplingRegime = 0x0E;
                    break;
                case 28:  // 8 hours
                    ssdSamplingRegimeOverride = false;
                    samplingRegime = 0x0F;
                    break;
                default:
                    valid = false;
                    break;
            }
        }
    }

    public enum OpusNumberOfSamplesToLog
    {
        a0_5_KiB = 0,
        a1_0_KiB = 1,
        a1_5_KiB = 2,
        a2_0_KiB = 3,
        a2_5_KiB = 4,
        a3_0_KiB = 5,
        a3_5_KiB = 6,
        a4_0_KiB = 7
    }

    public class UtcBasedTime
    {
        public readonly ulong timeInSecondsSince1970;

        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public static ushort[] DateTimeToUtc(DateTime dateTime)
        {
            long utcInSeconds = (long)Math.Round((dateTime - UnixEpoch).TotalSeconds);
            ushort[] timestamp = new ushort[] { (ushort)(utcInSeconds >> 16), (ushort)(utcInSeconds & 0x0000FFFF) };
            return timestamp;
        }
        public DateTime ToDateTime => UnixEpoch.AddSeconds((double)timeInSecondsSince1970);

        public UtcBasedTime(ushort msb, ushort lsb)
        {
            timeInSecondsSince1970 = (ulong)(((msb & 0x7FFF) << 16) | (lsb & 0xFFFF));
        }
    }

    public class RtcBasedTime
    {
        public readonly ulong numberOfCycles;
        public readonly DateTime startTimeStamp;
        public DateTime ToDateTime => startTimeStamp.AddSeconds(30.0 * numberOfCycles); // Opus RTC counts up every 30 seconds

        public RtcBasedTime(ushort msb, ushort lsb, DateTime par_startTimeStamp)
        {
            numberOfCycles = (ulong)(((msb & 0x00FF) << 16) | (lsb & 0xFFFF));
            startTimeStamp = par_startTimeStamp;
        }
    }

    public class OpusTagInfo
    {
        readonly public bool valid = false;
        readonly public String epc;
        readonly public int frequency;
        readonly public int rssi;
        readonly public ushort[] sensors_user_0x00_to_0x03 = null;
        readonly public ushort[] pcWords_epc_0x01_0x21 = null;
        readonly public bool includeSensorMeas;
        public ushort SensorCode => valid ? sensors_user_0x00_to_0x03[0] : (ushort)512; // 512 is not a valid Sensor Code
        public ushort OnChipRSSI => valid ? sensors_user_0x00_to_0x03[1] : (ushort)32; // 32 is not a valid On-Chip RSSI value
        public ushort TemperatureCode => valid ? sensors_user_0x00_to_0x03[2] : (ushort)4096; // 4096 is not a valid Temperature Code
        public ushort BatteryCode => valid ? sensors_user_0x00_to_0x03[3] : (ushort)4096; // 4096 is not a valid Battery Code
        public double TemperatureInC => valid && includeSensorMeas ? ((double)((short)TemperatureCode)) / 256.00 : double.NaN; // TODO: remove -105
        public String TemperatureInC_String => valid && includeSensorMeas ? string.Format("{0:N1}", TemperatureInC) : "NaN";
        public double BatteryInV => valid && includeSensorMeas ? ((double)((BatteryCode & 0xFFF0)) / 16000.00) : double.NaN; // TODO: remove 0.3
        public String BatteryInV_String => valid && includeSensorMeas ? string.Format("{0:N2}", BatteryInV) : "NaN";
        public bool BatteryPresent => valid ? (packetPC & 0x0010) == 0x0010 : false;
        public String BatteryPresent_String => valid ? ((packetPC & 0x0010) == 0x0010 ? "Yes" : "No") : "NA";
        public OpusState State => valid ? OpusStateEx.StateFromPacketPcToOpusState(packetPC) : OpusState.INVALID;
        public String Alarms_String => valid ? (alarms.Count > 0 ? string.Join(",", alarms) : "None") : "INVALID";
        public ushort PacketPC => valid ? packetPC : (ushort)0xFFFF;
        public String PacketPC_String => valid ? packetPC.ToString("X4") : "INVALID";
        public ushort XPC_W1 => valid ? xpc_W1 : (ushort)0xFFFF;
        public String XPC_W1_String => valid ? xpc_W1.ToString("X4") : "INVALID";

        private ushort packetPC;
        private ushort xpc_W1;
        private List<OpusAlarm> alarms = new List<OpusAlarm>();

        public OpusTagInfo(String parEpc, int parFreq, int parRssi, ushort[] parSensors_user_0x00_to_0x03, ushort[] parPcWords_epc_0x01_0x21, bool parIncludeSensorMeas)
        {
            epc = parEpc;
            frequency = parFreq;
            rssi = parRssi;
            includeSensorMeas = parIncludeSensorMeas;
            if (parSensors_user_0x00_to_0x03 != null && parSensors_user_0x00_to_0x03.Length == 4)
            {
                sensors_user_0x00_to_0x03 = parSensors_user_0x00_to_0x03;
            }
            else
            {
                return;
            }
            if (parPcWords_epc_0x01_0x21 != null && parPcWords_epc_0x01_0x21.Length == 2)
            {
                pcWords_epc_0x01_0x21 = parPcWords_epc_0x01_0x21;
                packetPC = pcWords_epc_0x01_0x21[0];
                xpc_W1 = pcWords_epc_0x01_0x21[1];
                if ((packetPC & 0x0004) == 0x0004) alarms.Add(OpusAlarm.Temp);
                if ((packetPC & 0x0002) == 0x0002) alarms.Add(OpusAlarm.Battery);
                if ((packetPC & 0x0001) == 0x0001) alarms.Add(OpusAlarm.Tamper);
                if ((xpc_W1 & 0x0800) == 0x0800) alarms.Add(OpusAlarm.InitBat);

                valid = true;
            }
        }
    }

    public class OpusTagStatus
    {
        readonly public bool valid = false;
        readonly public ushort[] stateAndRtcClock_user_0x05_to_0x07 = null;
        readonly public ushort[] tagStatus_tid_0x08_to_0x1B = null;
        readonly public ushort xpcW1_epc_0x21;
        readonly public UtcBasedTime startTimestamp;
        readonly public RtcBasedTime rtcClock;
        readonly public RtcBasedTime fingerSpotStartTimestamp;

        public OpusState State => valid ? OpusStateEx.StateFromUserBankToOpusState(stateAndRtcClock_user_0x05_to_0x07) : OpusState.INVALID;
        public DateTime RtcClockDateTime => valid ? rtcClock.ToDateTime : default;
        public ushort NextLogAddress => valid ? ((ushort)(tagStatus_tid_0x08_to_0x1B[19] & 0x1FFF)) : (ushort)0xFFFF; // 0xFFFF is not valid
        public DateTime StartTimestampDateTime => valid ? startTimestamp.ToDateTime : default;
        public DateTime FingerSpotStartTimestampDateTime => valid ? fingerSpotStartTimestamp.ToDateTime : default;
        public bool HighTemperatureAlarm => valid ? (tagStatus_tid_0x08_to_0x1B[8] & 0x0002) == 0x0002 : default;
        public bool LowTemperatureAlarm => valid ? (tagStatus_tid_0x08_to_0x1B[8] & 0x0001) == 0x0001 : default;
        public bool BatteryAlarm => valid ? (tagStatus_tid_0x08_to_0x1B[8] & 0x0008) == 0x0008 : default;
        public bool TamperAlarm => valid ? (tagStatus_tid_0x08_to_0x1B[8] & 0x0004) == 0x0004 : default;
        public bool BatteryInitAlarm => valid ? (xpcW1_epc_0x21 & 0x0800) == 0x0800 : default;
        public ushort TemperatureViolationAddress => valid ? tagStatus_tid_0x08_to_0x1B[3] : (ushort)0xFFFF; // 0xFFFF is not valid 
        public ushort TamperViolationAddress => valid ? tagStatus_tid_0x08_to_0x1B[4] : (ushort)0xFFFF; // 0xFFFF is not valid 
        public bool FingerTouchLogEnabled => valid ? (tagStatus_tid_0x08_to_0x1B[0] & 0x0008) == 0x0008 : default;
        public OpusTagStatus(ushort[] par_stateAndRtcClock_user_0x05_to_0x07, ushort[] par_tagStatus_tid_0x08_to_0x1B, ushort[] pcWords)
        {
            if (par_stateAndRtcClock_user_0x05_to_0x07 != null && par_tagStatus_tid_0x08_to_0x1B != null && pcWords != null && pcWords.Length >= 2)
            {
                stateAndRtcClock_user_0x05_to_0x07 = par_stateAndRtcClock_user_0x05_to_0x07;
                tagStatus_tid_0x08_to_0x1B = par_tagStatus_tid_0x08_to_0x1B;
                xpcW1_epc_0x21 = pcWords[1];
                startTimestamp = new UtcBasedTime(tagStatus_tid_0x08_to_0x1B[9], tagStatus_tid_0x08_to_0x1B[10]);
                rtcClock = new RtcBasedTime(stateAndRtcClock_user_0x05_to_0x07[2], stateAndRtcClock_user_0x05_to_0x07[1], startTimestamp.ToDateTime);
                fingerSpotStartTimestamp = new RtcBasedTime(tagStatus_tid_0x08_to_0x1B[5], tagStatus_tid_0x08_to_0x1B[6], startTimestamp.ToDateTime);
                valid = true;
            }
        }
    }

    public class OpusTagConfiguration
    {
        readonly public bool valid = false;
        readonly public ushort[] tid_0x08_to_0x1F = null;
        readonly public ushort[] tid_0x20_to_0x29 = new ushort[] { 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0A00, 0x000F, 0x0200, 0x0011 };
        private OpusLogInterval logInterval = null;

        public OpusLogInterval LogInterval
        {
            get => logInterval;
            set
            {
                logInterval = value;
                tid_0x08_to_0x1F[0] = logInterval.SsdSamplingRegimeOverride ? (ushort)(tid_0x08_to_0x1F[0] | 0x0100) : (ushort)(tid_0x08_to_0x1F[0] & 0xFEFF);
                tid_0x08_to_0x1F[7] = (ushort)(tid_0x08_to_0x1F[7] & 0xFF87);
                tid_0x08_to_0x1F[7] = (ushort)(tid_0x08_to_0x1F[7] | ((logInterval.SamplingRegime & 0x000F) << 3));
            }
        }

        public int DelayedLoggerStartPeriods
        {
            get => (tid_0x08_to_0x1F[8] & 0x1C00) >> 10;
            set
            {
                tid_0x08_to_0x1F[8] = (ushort)(tid_0x08_to_0x1F[8] & 0xE3FF);
                tid_0x08_to_0x1F[8] = (ushort)(tid_0x08_to_0x1F[8] | ((value & 0x0007) << 10));
            }
        }

        public OpusNumberOfSamplesToLog NumberOfSamplesToLog
        {
            get => (OpusNumberOfSamplesToLog)((tid_0x08_to_0x1F[18] & 0x1C00) >> 10);
            set
            {
                tid_0x08_to_0x1F[18] = (ushort)(tid_0x08_to_0x1F[18] & 0xE3FF);
                tid_0x08_to_0x1F[18] = (ushort)(tid_0x08_to_0x1F[18] | ((((int)value) & 0x0007) << 10));
            }
        }

        public double TemperatureLowerLimit
        {
            get => (short)((tid_0x08_to_0x1F[2] & 0x0FFF) << 4) / 256.0;
            set
            {
                tid_0x08_to_0x1F[2] = (ushort)(tid_0x08_to_0x1F[2] & 0xF000);
                tid_0x08_to_0x1F[2] = (ushort)(tid_0x08_to_0x1F[2] | (Convert.ToInt32(Math.Truncate(value * 16.0))) & 0x0FFF);
            }
        }

        public double TemperatureUpperLimit
        {
            get => (short)((tid_0x08_to_0x1F[1] & 0x0FFF) << 4) / 256.0;
            set
            {
                tid_0x08_to_0x1F[1] = (ushort)(tid_0x08_to_0x1F[1] & 0xF000);
                tid_0x08_to_0x1F[1] = (ushort)(tid_0x08_to_0x1F[1] | (Convert.ToInt32(Math.Truncate(value * 16.0))) & 0x0FFF);
            }
        }

        public int TemperatureLowerLimitAlarmDelay
        {
            get => (tid_0x08_to_0x1F[8] & 0x0070) >> 4;
            set
            {
                tid_0x08_to_0x1F[8] = (ushort)(tid_0x08_to_0x1F[8] & 0xFF8F);
                tid_0x08_to_0x1F[8] = (ushort)(tid_0x08_to_0x1F[8] | ((value & 0x0007) << 4));
            }
        }

        public int TemperatureUpperLimitAlarmDelay
        {
            get => (tid_0x08_to_0x1F[8] & 0x0380) >> 7;
            set
            {
                tid_0x08_to_0x1F[8] = (ushort)(tid_0x08_to_0x1F[8] & 0xFC7F);
                tid_0x08_to_0x1F[8] = (ushort)(tid_0x08_to_0x1F[8] | ((value & 0x0007) << 7));
            }
        }

        public bool FingerSpotEnabled
        {
            get => (tid_0x08_to_0x1F[0] & 0x1000) == 0x1000;
            set
            {
                tid_0x08_to_0x1F[0] = (ushort)(tid_0x08_to_0x1F[0] & 0xEFFF);
                tid_0x08_to_0x1F[0] = (ushort)(tid_0x08_to_0x1F[0] | (value ? 0x1000 : 0x0000));
            }
        }

        public bool StartLoggingWithFingerSpot
        {
            get => (tid_0x08_to_0x1F[0] & 0x0008) == 0x0008;
            set
            {
                tid_0x08_to_0x1F[0] = (ushort)(tid_0x08_to_0x1F[0] & 0xFFF7);
                tid_0x08_to_0x1F[0] = (ushort)(tid_0x08_to_0x1F[0] | (value ? 0x0008 : 0x0000));
            }
        }

        public bool LedEnabled
        {
            get => (tid_0x08_to_0x1F[22] & 0x2000) == 0x2000;
            set
            {
                tid_0x08_to_0x1F[22] = (ushort)(tid_0x08_to_0x1F[22] & 0xDFFF);
                tid_0x08_to_0x1F[22] = (ushort)(tid_0x08_to_0x1F[22] | (value ? 0x2000 : 0x0000));
            }
        }

        public bool LedOnDemandMode
        {
            get => (tid_0x08_to_0x1F[22] & 0x1000) == 0x1000;
            set
            {
                tid_0x08_to_0x1F[22] = (ushort)(tid_0x08_to_0x1F[22] & 0xEFFF);
                tid_0x08_to_0x1F[22] = (ushort)(tid_0x08_to_0x1F[22] | (value ? 0x1000 : 0x0000));
            }
        }

        public int LedOffTimeInTwoSecondsUnits
        {
            get => (tid_0x08_to_0x1F[22] & 0x03E0) >> 5;
            set
            {
                tid_0x08_to_0x1F[22] = (ushort)(tid_0x08_to_0x1F[22] & 0xFC1F);
                tid_0x08_to_0x1F[22] = (ushort)(tid_0x08_to_0x1F[22] | ((value & 0x001F) << 5));
            }
        }

        public int LedOnTimeInRtcCycles
        {
            get => tid_0x08_to_0x1F[22] & 0x001F;
            set
            {
                tid_0x08_to_0x1F[22] = (ushort)(tid_0x08_to_0x1F[22] & 0xFFE0);
                tid_0x08_to_0x1F[22] = (ushort)(tid_0x08_to_0x1F[22] | (value & 0x001F));
            }
        }

        public bool AntiTamperEnabled
        {
            get => (tid_0x08_to_0x1F[0] & 0x0400) == 0x0400;
            set
            {
                tid_0x08_to_0x1F[0] = (ushort)(tid_0x08_to_0x1F[0] & 0xFBFF);
                tid_0x08_to_0x1F[0] = (ushort)(tid_0x08_to_0x1F[0] | (value ? 0x0400 : 0x0000));
            }
        }

        public bool AntiTamperPolarity
        {
            get => (tid_0x08_to_0x1F[0] & 0x0800) == 0x0800;
            set
            {
                tid_0x08_to_0x1F[0] = (ushort)(tid_0x08_to_0x1F[0] & 0xF7FF);
                tid_0x08_to_0x1F[0] = (ushort)(tid_0x08_to_0x1F[0] | (value ? 0x0800 : 0x0000));
            }
        }

        private bool InitObjectVariables()
        {
            logInterval = new OpusLogInterval((tid_0x08_to_0x1F[0] & 0x0100) == 0x0100, (ushort)((tid_0x08_to_0x1F[7] & 0x0078) >> 3));
            return (logInterval.valid);
        }

        public OpusTagConfiguration(ushort[] par_loggerConfig_tid_0x08_to_0x1F)
        {
            if (par_loggerConfig_tid_0x08_to_0x1F != null)
            {
                tid_0x08_to_0x1F = par_loggerConfig_tid_0x08_to_0x1F;
                valid = InitObjectVariables();
            }
        }

        public OpusTagConfiguration()
        {
            tid_0x08_to_0x1F = new ushort[] { // This is the "Default" configuration
                0x9100, 0x02A0, 0x0FB0, 0x0000, 0x0000, 0x0000, 0x0000, 0x0030, 0x0490, //0x08 to 0x10
                0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, //0x11 to 0x19, unused
                0x1E08, 0x0000, 0x07D0, 0x09C4, 0x3004, 0x0108 //0x1A to 0x1F  
            };
            valid = InitObjectVariables();
        }
    }

    public class OpusLoggedData
    {
        public OpusTagStatus Status;
        public OpusTagConfiguration Config;
        public int StartSampleNumber = -1;
        public int UntilSampleNumber = -1;
        public DateTime FirstSampleTime;
        public DateTime StartLogTime;
        public List<ListViewItem> ListViewItems = null;
        public double[] Temperatures = null;
        public int NumSamples = 0;
        public double SamplesPerDay = 1;
        public String PlotTag;
    }

    public class OpusTag
    {
        public OpusTagInfo Info;
        public OpusTagStatus Status;
        public OpusTagConfiguration Config;
        public OpusLoggedData LoggedData;

        public OpusTag(OpusTagInfo tagInfo)
        {
            LoggedData = new OpusLoggedData();
            if (tagInfo.valid) Info = tagInfo;
        }
    }

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

    //******************************************************************************
    //-------- READER INTERFACE and READER IMPLEMENTATIONS

    public enum MemoryBank
    {
        RESERVED,
        EPC,
        TID,
        USER
    }
}
