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

namespace AxzonTempSensor
{
    public interface IRfidReader : IDisposable
    {
        int MaxNumberOfReadWords { get; }

        void Connect(string comPortOrHostName, uint ipPortNumber = 0);

        void Disconnect();

        List<int> AvailableAntennas { get; }

        List<int> ConnectedAntennas { get; }

        List<int> Antennas { get; set; }

        void InitialSetup();

        int[] SetFccBand();

        int[] SetEuBand();

        int[] SetOpenBand();

        int[] UpdateFrequencyChannels(int[] channels);

        double SetReadPower(double power, bool readBack = false);

        double SetWritePower(double power, bool readBack = false);

        double GetReadPower();

        double GetWritePower();

        ushort[] ReadTagMemByEPC(String epc, MemoryBank bank, uint wordAddress, int numWords, double[] rfPowers, int readTimeMs, int readAttempts, out ushort[] pcWords);

        bool WriteTagMemByEPC(String epc, MemoryBank bank, uint wordAddress, ushort[] wordsToWrite, double[] rfPowers, int writeAttempts, bool verifyReadBack, out ushort[] wordsReadBack, out ushort[] pcWords);

        bool SetBapMode(String epc, double[] rfPowers, int readTimeMs, int readAttempts);  // TODO: remove readTimeMs

        void TransitionFromFinishedToStandby(String epc, double[] rfPowers, int numAttempts); 

        void SetSetupForInventoryOpusTags(int initialQVal, bool includeSensorMeas);

        List<OpusTagInfo> InventoryOpusTags(int readTimeInMs, bool includeSensorMeas);
    }
}
