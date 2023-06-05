using System;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OmicronLab.VectorNetworkAnalysis.AutomationInterface;
using OmicronLab.VectorNetworkAnalysis.AutomationInterface.Interfaces;
using OmicronLab.VectorNetworkAnalysis.AutomationInterface.Interfaces.Measurements;
using OmicronLab.VectorNetworkAnalysis.AutomationInterface.Enumerations;
using OmicronLab.VectorNetworkAnalysis.AutomationInterface.DataTypes;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows.Forms;
using OxyPlot;
using OxyPlot.Series;
using MathNet.Numerics;
using MathNet.Numerics.LinearRegression;

namespace BodeGUIPneuma
{
    public class TaskLog
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsTaskSuccessful { get; set; }
        public Dictionary<string, string> ReaderOut { get; set; }
        public TaskLog()
        {
            Name = string.Empty;
            Description = string.Empty;
            IsTaskSuccessful = false;
            ReaderOut = new Dictionary<string, string>
            { {"Initialize","Waiting to connect bode, make sure bode100 is connected to computer and Bode Analyzer Suite app is closed before connecting" },
                {"Connecting","Bode Connecting Please wait" },
                {"ConnectFailed","Bode connection failed, make sure bode100 is connected to computer and Bode Analyzer Suite app is closed before connecting" },
                {"Calibrate","Please run calibration procedure before making measurement" },
                {"CalLoad", "Please run load calibration with 50Ω resistor" },
                {"Open", "Performing Open Calibration" },
                {"Short", "Perfroming Short Calibration" },
                {"Load", "Performing Load Calibration" },
                {"Run", "Performing Test Please wait" },
                {"Export", "Exporting to CSV"},
                {"Ready", "Ready to collect data" } };
        }
    }

    /* Standard orgization of data for measuring horn charateristics */
    public class Data
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public double Resfreq { get; set; }
        public double Antifreq { get; set; }
        public double Res_impedance { get; set; }
        public double Anti_impedance { get; set; }
        public double Capacitance { get; set; }
        public double Resistance { get; set; }
        public double QualityFactor { get; set; }

        public Data()
        {
            Index = 0;
            Name = "Name";
            Capacitance = 0;
            Resfreq = 0;
            Antifreq = 0;
            Res_impedance = 0;
            Anti_impedance = 0;
            QualityFactor = 0;
        }
    }
    /* Main class with methods for measurements of horn characteristics */
    public class Horn_Characteristic
    {
        private OnePortMeasurement measurement;
        private BodeDevice bode;
        private ExecutionState state;
        private BodeAutomationInterface auto = new BodeAutomation();
        public Data horn_data = new Data();
        private List<double> SweepFreqs;
        private List<double> SweepImpedances;
        public List<DataPoint> BodePoints { get; set; }
        private List<double> PeakFrequencyRange;
        private List<double> PeakImpedanceRange;
        public List<DataPoint> PeakPoints { get; set; }
        public List<DataPoint> QFPoints { get; set; }
        private (double, double) ThreshLine;
        public List<DataPoint> ThreshPoints { get; private set; }
        public List<PeakData> PeakDataList { get; private set; }
        private PeakData PeakDataN;                                 //Temporary storage for current peak data item to be added to PeakDataList
        public Horn_Characteristic()
        {
            /* Initialize Variables For Sweep */
            Sweep_PTS = 201;                        //default number of points per sweep
            sweep_LOW = 180000;                     //Defalut low sweep range
            sweep_HIGH = 190000;                    //Default high sweep range
            Bandwidth = 100000;                     //Reciever bandwidth in mHz
            IsQF_Checked = false;

            /* Initialize Variables for PeakTrack */
            peak_LOW = 100000;
            peak_HIGH = 1000000;
            peak_BW = 100000;
            numberPoints = 500;
            CurrentDip = 0;
            CurrentPeak = 0;
            AverageQF = 0;
            PeakFrequencyRange = new List<double>();
            PeakImpedanceRange = new List<double>();

            /* Initialize Ploting Variables For Debugging */
            PeakDataList = new List<PeakData>();
            BodePoints = new List<DataPoint>();
            PeakPoints = new List<DataPoint>();
            QFPoints = new List<DataPoint>();
            ThreshPoints = new List<DataPoint>();
            BodePoints.Add(new DataPoint(1, 1));
            BodePoints.Add(new DataPoint(100, 2000));
            BodePoints.Add(new DataPoint(1000, 1500));
            BodePoints.Add(new DataPoint(10000, 2000));
            ThreshPoints.Add(new DataPoint(1, 500));
            ThreshPoints.Add(new DataPoint(100, 500));
            ThreshPoints.Add(new DataPoint(1000, 500));
            ThreshPoints.Add(new DataPoint(10000, 500));
            foreach (DataPoint point in BodePoints) PeakPoints.Add(point);
        }
        /* Automatically searches for first availible bode100 device */
        public void Connect()
        {
            bode = auto.Connect();
            measurement = bode.Impedance.CreateOnePortMeasurement();
        }
        /* Sweeps relevant frequencies for Bluesky pushmode piezo */
        public void Sweep()
        {
            /* Run sweeps to dtermine res and anti-res frequencies */
            measurement.ReceiverBandwidth = (ReceiverBandwidth)Bandwidth;                   //Sets reciever bandwidth to value of GUI reciever bandwidth box
            SweepPtMeasurement(sweep_LOW, sweep_HIGH, Sweep_PTS, SweepMode.Logarithmic);    //Run initial sweep based on user defined frequency range
            ClearPlotData(BodePoints);
            /* Fill bode plot data points */
            FillPlotData(BodePoints, measurement.Results.MeasurementFrequencies.ToList(), measurement.Results.Magnitude(MagnitudeUnit.Lin).ToList(), measurement.Results.MeasurementFrequencies.Length);
            /* Calculate Resonance and AntiResonance */
            horn_data.Resfreq = measurement.Results.CalculateFResQValues(false, true, FResQFormats.Magnitude).ResonanceFrequency;
            horn_data.Antifreq = measurement.Results.CalculateFResQValues(true, true, FResQFormats.Magnitude).ResonanceFrequency;
            try
            {
                /* Use data from CalcResFreq to measure impedance at resonance as single measurement */
                SinglePtMeasurement(horn_data.Resfreq);
                horn_data.Res_impedance = measurement.Results.MagnitudeAt(0, MagnitudeUnit.Lin);
                horn_data.QualityFactor = measurement.Results.QAt(0);

                /* Use data from CalcResFreq to measure impedance at anti-resonance as single measurement */
                SinglePtMeasurement(horn_data.Antifreq);
                horn_data.Anti_impedance = measurement.Results.MagnitudeAt(0, MagnitudeUnit.Lin);
            }
            catch (ExecuteStateException ex)
            {
                horn_data.Res_impedance = 0;
                horn_data.QualityFactor = 0;
                horn_data.Anti_impedance = 0;
            }
            /* Magnitude data for GUI output */
            horn_data.Resfreq = horn_data.Resfreq / 1000.0;
            horn_data.Antifreq = horn_data.Antifreq / 1000.0;
            horn_data.Anti_impedance = horn_data.Anti_impedance / 1000.0;
            /* Measure Capacitance value at 1000 Hz */
            SinglePtMeasurement(1000);
            double cap = measurement.Results.CsAt(0);
            horn_data.Capacitance = cap * 1e12;
        }

        private void SweepPtMeasurement(double Low, double High, int NumPts, SweepMode Type)
        {
            measurement.ConfigureSweep(Low, High, NumPts, Type);
            state = measurement.ExecuteMeasurement();
            if (state != ExecutionState.Ok)
            {
                bode.ShutDown();
                throw new ExecuteStateException("Frequency Sweep");
            }
        }

        private void SinglePtMeasurement(double frequency)
        {
            measurement.ConfigureSinglePoint(frequency);
            state = measurement.ExecuteMeasurement();
            if (state != ExecutionState.Ok)
            {
                bode.ShutDown();
                throw new ExecuteStateException("Resonant Frequency Measurement");
            }
        }
        private void FillPlotData(List<DataPoint> Pts, List<double> Frequencies, List<double> Impedances, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Pts.Add(new DataPoint(measurement.Results.MeasurementFrequencies[i], measurement.Results.Magnitude(MagnitudeUnit.Lin)[i]));
            }
        }
        private void ClearPlotData(List<DataPoint> Pts)
        {
            Pts.Clear();
        }
        public void OpenCal()
        {
            /* Bode Automation Suite method runs open calibration */
            ExecutionState state = measurement.Calibration.FullRange.ExecuteOpen();
        }

        public void ShortCal()
        {
            ExecutionState state = measurement.Calibration.FullRange.ExecuteShort();
        }

        public void LoadCal()
        {
            ExecutionState state = measurement.Calibration.FullRange.ExecuteLoad();
        }
        /* Tests bode calibrataion by executing SinglePtMeasurement @1000Hz on fixed load */
        public double TestCal()
        {
            SinglePtMeasurement(1000);
            return measurement.Results.MagnitudeAt(0, MagnitudeUnit.Lin);
        }

        /* Disconnects Bode100 device from computer */
        public void Disconnect()
        {
            if (bode != null) bode.ShutDown();      //If bode is connectect run bode disconnect method
        }

        /* Eport path opens file explorer and returns a path to save data entered by user */
        public string ExportPath()
        {
            string fileSelected = "";
            Microsoft.Win32.SaveFileDialog openFileDialog = new Microsoft.Win32.SaveFileDialog();
            openFileDialog.InitialDirectory = "c:\\";
            openFileDialog.Filter = "csv files (*.csv)|*.csv|All files (*.*)|*.*";
            openFileDialog.Title = "Select file loaction";
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == true)
            {
                fileSelected = openFileDialog.FileName;
            }
            return fileSelected;
        }

        public void PeakTrack()
        {
            ///* Set a threshold constant for the measurement.... Look into removing this part for release */
            //double threshConstant = 0.4;
            //int j = 0;
            //int i = 0;
            //measurement.ReceiverBandwidth = (ReceiverBandwidth)peak_BW;
            ///* Add all data points to PeakPoints to be plotted */
            //SweepPtMeasurement(peak_LOW, peak_HIGH, numberPoints, SweepMode.Logarithmic);
            //PeakDataList.Clear();
            //PeakFrequencyRange.Clear();
            //PeakImpedanceRange.Clear();
            //ClearPlotData(PeakPoints);
            //ClearPlotData(ThreshPoints);
            ///* Generate Threshold Function for Visualizing threshold level on plot */
            //ThreshLine = Fit.Power(measurement.Results.MeasurementFrequencies, measurement.Results.Magnitude(MagnitudeUnit.Lin));
            ///* Add Measurements to freq and impedance lists and fill ThreshPoints for plot */
            //double pt;
            //AverageQF = measurement.Results.Q().Average();
            //foreach (double element in measurement.Results.MeasurementFrequencies)
            //{
            //    PeakFrequencyRange.Add(element);
            //    pt = ThreshLine.Item1 * Math.Pow(element, ThreshLine.Item2);
            //    ThreshPoints.Add(new DataPoint(element, pt - (pt * threshConstant))); //Transform threshold line down below the average by a percentage of value at that point
            //    PeakImpedanceRange.Add(measurement.Results.Magnitude(MagnitudeUnit.Lin)[i]);
            //    QFPoints.Add(new DataPoint(element, measurement.Results.Q()[i]));
            //    i++;
            //}
            //i = 0;
            //FillPlotData(PeakPoints, PeakFrequencyRange, PeakImpedanceRange, PeakFrequencyRange.Count);
            //foreach (double element in PeakImpedanceRange)
            //{
            //    i++;
            //    if (i >= PeakImpedanceRange.Count) break;   //prevent indexing out of range
            //    if (PeakImpedanceRange[i] < ThreshPoints[i].Y && element > ThreshPoints[i - 1].Y)         //search for dip below threshold
            //    {
            //        PeakDataN = new PeakData();
            //        FindPeak(PeakFrequencyRange[i]);
            //        PeakDataN.ResFreq = CurrentDip;
            //        PeakDataN.AntiFreq = CurrentPeak;
            //        SinglePtMeasurement(PeakDataN.ResFreq);
            //        PeakDataN.ResImp = measurement.Results.MagnitudeAt(0, MagnitudeUnit.Lin);
            //        PeakDataN.ResQ = measurement.Results.QAt(0);
            //        PeakDataN.Capacitance = measurement.Results.CsAt(0);
            //        SinglePtMeasurement(PeakDataN.AntiFreq);
            //        PeakDataN.AntiImp = measurement.Results.MagnitudeAt(0, MagnitudeUnit.Lin);
            //        PeakDataN.AntiQ = measurement.Results.QAt(0);
            //        PeakDataN.peakNumber = j;
            //        PeakDataList.Add(PeakDataN);
            //        j++;
            //    }
            //}

        }
        private void FindPeak(double threshFrequency)
        {
            int peakTrackingPoints = 201;
            double startFreq = threshFrequency;
            double stopFreq = threshFrequency + threshFrequency / 36;
            SweepPtMeasurement(startFreq, stopFreq, peakTrackingPoints, SweepMode.Linear);
            CurrentDip = measurement.Results.CalculateFResQValues(false, false, FResQFormats.Magnitude).ResonanceFrequency;
            CurrentPeak = measurement.Results.CalculateFResQValues(true, false, FResQFormats.Magnitude).ResonanceFrequency;
        }
        /* Variables Used By Sweep to Gather Horn Characteristics Data */
        private int Sweep_PTS;
        private double _sweep_LOW;
        public double sweep_LOW
        {
            get { return _sweep_LOW; }
            set { _sweep_LOW = value; }
        }
        private double _sweep_HIGH;
        public double sweep_HIGH
        {
            get { return _sweep_HIGH; }
            set { _sweep_HIGH = value; }
        }
        private double _bandwidth;
        public double Bandwidth
        {
            get { return _bandwidth; }
            set { _bandwidth = value; }
        }
        private bool _isQF_Checked;
        public bool IsQF_Checked
        {
            get { return _isQF_Checked; }
            set { _isQF_Checked = value; }
        }

        /* Variables Used by PeakTrack to Determine Number of Peaks/QF of Peaks in User Defined Range */
        private double _peak_LOW;
        public double peak_LOW
        {
            get { return _peak_LOW; }
            set { _peak_LOW = value; }
        }
        private double _peak_HIGH;
        public double peak_HIGH
        {
            get { return _peak_HIGH; }
            set { _peak_HIGH = value; }
        }
        private double _peak_BW;
        public double peak_BW
        {
            get { return _peak_BW; }
            set { _peak_BW = value; }
        }
        private int _numberPoints;
        public int numberPoints
        {
            get { return _numberPoints; }
            set
            {
                _numberPoints = value;
            }
        }

        private double CurrentDip;
        private double CurrentPeak;
        public double AverageQF { get; private set; }
    }

    public class PeakData
    {
        public PeakData()
        {
            peakNumber = 0;
            ResFreq = 0;
            ResImp = 0;
            ResQ = 0;
            AntiFreq = 0;
            AntiImp = 0;
            AntiQ = 0;
        }
        public int peakNumber { get; set; }
        public double ResFreq { get; set; }
        public double ResImp { get; set; }
        public double ResQ { get; set; }
        public double AntiFreq { get; set; }
        public double AntiImp { get; set; }
        public double AntiQ { get; set; }
        public double Capacitance { get; set; }
    }
}
