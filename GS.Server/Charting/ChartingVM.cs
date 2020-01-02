﻿/* Copyright(C) 2019  Rob Morgan (robert.morgan.e@gmail.com)

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published
    by the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
using GS.Principles;
using GS.Server.Domain;
using GS.Server.Helpers;
using GS.Server.Main;
using GS.Server.Phd;
using GS.Server.SkyTelescope;
using GS.Shared;
using LiveCharts.Defaults;
using LiveCharts.Geared;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace GS.Server.Charting
{
    public class ChartingVM : ObservableObject, IPageVM, IDisposable
    {
        #region Fields

        // page context
        public string TopName => "";
        public string BottomName => "Charts";
        public int Uid => 1;
        // chart
        private readonly DispatcherTimer _xAxisTimer;
        private CancellationTokenSource _ctsPulse;
        private CancellationToken _ctPulse;
        private double _stepsPerSecond;
        private readonly string _version;
        private bool _disposed;
        // j1
        private long _jstartpos;
        private DateTime _jstarttime;
        // phd
        private GuiderImpl _phd;
        private CancellationTokenSource _ctsPhd;
        private CancellationToken _ctPhd;
        private bool _taskRunning;
        //test data
        private CancellationTokenSource _ctsChart;
        private MediaTimer mediaTimer;
        private int startJ;

        // chart output
        private Shared.Charting Charting;

        #endregion

        #region VM Items

        public ChartingVM()
        {
            try
            {
                using (new WaitCursor())
                {
                    var monitorItem = new MonitorEntry
                    { Datetime = HiResDateTime.UtcNow, Device = MonitorDevice.Server, Category = MonitorCategory.Interface, Type = MonitorType.Information, Method = MethodBase.GetCurrentMethod().Name, Thread = Thread.CurrentThread.ManagedThreadId, Message = " Loading ChartingVM" };
                    MonitorLog.LogToMonitor(monitorItem);

                    ChartSettings.Load();
                    //ChartSettings.LogChartSettings();
                    LoadChart();
                    _version = Assembly.GetExecutingAssembly().GetName().Version.ToString();

                    // Pulse events
                    MonitorQueue.StaticPropertyChanged += PropertyChangedMonitor;
                    // Phd events
                    GuiderImpl.PropertyChanged += PropertyChangedGuiding;
                    // Settings
                    ChartSettings.StaticPropertyChanged += PropertyChangedSettings;

                    // X axis second timer
                    _xAxisTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                    _xAxisTimer.Tick += XAxisTimer_Tick;

                    // combo selections
                    ColorsList = new List<string>();
                    foreach (KnownColor colorValue in Enum.GetValues(typeof(KnownColor)))
                    {
                        var color = Color.FromKnownColor(colorValue);
                        if (!ColorsList.Contains(color.Name) && !color.IsSystemColor)
                        { ColorsList.Add(color.Name); }
                    }
                }
            }
            catch (Exception ex)
            {
                var monitorItem = new MonitorEntry
                { Datetime = HiResDateTime.UtcNow, Device = MonitorDevice.Server, Category = MonitorCategory.Interface, Type = MonitorType.Error, Method = MethodBase.GetCurrentMethod().Name, Thread = Thread.CurrentThread.ManagedThreadId, Message = $" GuidingVM: {ex.Message}, {ex.StackTrace}" };
                MonitorLog.LogToMonitor(monitorItem);

                OpenDialog(ex.Message);
            }
        }

        private string _phdBadgeContent;
        public string PhdBadgeContent
        {
            get => _phdBadgeContent;
            set
            {
                if (_phdBadgeContent == value) return;
                _phdBadgeContent = value;
                OnPropertyChanged();
            }
        }

        private string _startBadgeContent;
        public string StartBadgeContent
        {
            get => _startBadgeContent;
            set
            {
                if (_startBadgeContent == value) return;
                _startBadgeContent = value;
                OnPropertyChanged();
            }
        }

        private void PropertyChangedMonitor(object sender, PropertyChangedEventArgs e)
        {
            try
            {
                ThreadContext.InvokeOnUiThread(
                    delegate
                    {
                        switch (e.PropertyName)
                        {
                            case "PulseEntry":
                                PulseProcess(MonitorQueue.PulseEntry);
                                break;
                            case "CmdjSentEntry":
                                Cmdj1Process(MonitorQueue.CmdjSentEntry);
                                break;
                        }
                    }, _ctPulse);
            }
            catch (Exception ex)
            {
                var monitorItem = new MonitorEntry
                { Datetime = HiResDateTime.UtcNow, Device = MonitorDevice.Server, Category = MonitorCategory.Interface, Type = MonitorType.Error, Method = MethodBase.GetCurrentMethod().Name, Thread = Thread.CurrentThread.ManagedThreadId, Message = $" {ex.Message}" };
                MonitorLog.LogToMonitor(monitorItem);

                OpenDialog(ex.Message);
            }
        }
        private void PropertyChangedGuiding(object sender, PropertyChangedEventArgs e)
        {
            try
            {
                ThreadContext.BeginInvokeOnUiThread(
                    delegate
                    {
                        switch (e.PropertyName)
                        {
                            case "PhdGuideStep":
                                PhdProcess(_phd.PhdGuideStep);
                                break;
                        }
                    });
            }
            catch (Exception ex)
            {
                var monitorItem = new MonitorEntry
                { Datetime = HiResDateTime.UtcNow, Device = MonitorDevice.Server, Category = MonitorCategory.Interface, Type = MonitorType.Error, Method = MethodBase.GetCurrentMethod().Name, Thread = Thread.CurrentThread.ManagedThreadId, Message = $" {ex.Message}" };
                MonitorLog.LogToMonitor(monitorItem);

                OpenDialog(ex.Message);
            }
        }
        private void PropertyChangedSettings(object sender, PropertyChangedEventArgs e)
        {
            try
            {
                ThreadContext.BeginInvokeOnUiThread(
                    delegate
                    {

                    });
            }
            catch (Exception ex)
            {
                var monitorItem = new MonitorEntry
                { Datetime = HiResDateTime.UtcNow, Device = MonitorDevice.Server, Category = MonitorCategory.Interface, Type = MonitorType.Error, Method = MethodBase.GetCurrentMethod().Name, Thread = Thread.CurrentThread.ManagedThreadId, Message = $" {ex.Message}" };
                MonitorLog.LogToMonitor(monitorItem);

                OpenDialog(ex.Message);
            }
        }
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(obj: this);
        }
        private void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.
            if (_disposed) return;
            // If disposing equals true, dispose all managed
            // and unmanaged resources.
            if (disposing)
            {
                // Dispose managed resources.
                _ctsChart?.Dispose();
                _ctsPulse?.Dispose();
                _phd?.Dispose();
                RaValues?.Dispose();
                DecValues?.Dispose();
                ThirdValues?.Dispose();
                FourthValues?.Dispose();
                mediaTimer?.Stop();
                Charting?.Dispose();
            }

            // Note disposing has been done.
            _disposed = true;
        }
        
        private ICommand _clickStartChartingCommand;
        public ICommand ClickStartChartingCommand
        {
            get
            {
                return _clickStartChartingCommand ?? (_clickStartChartingCommand = new RelayCommand(param => StartChartingCommand()));
            }
            set => _clickStartChartingCommand = value;
        }
        private void StartChartingCommand()
        {
            try
            {
                using (new WaitCursor())
                {
                    switch (DataType)
                    {
                        case ChartTypes.Rejected:
                        case ChartTypes.Steps:
                        case ChartTypes.Execute:
                        case ChartTypes.Duration:
                            PulseCharting();
                            break;
                        case ChartTypes.Tracking:
                            Cmdj1Charting();
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }
            catch (Exception ex)
            {
                IsCharting = false;

                var monitorItem = new MonitorEntry
                { Datetime = HiResDateTime.UtcNow, Device = MonitorDevice.Server, Category = MonitorCategory.Interface, Type = MonitorType.Error, Method = MethodBase.GetCurrentMethod().Name, Thread = Thread.CurrentThread.ManagedThreadId, Message = $" {ex.Message}, {ex.StackTrace}" };
                MonitorLog.LogToMonitor(monitorItem);

                OpenDialog(ex.Message);
            }
        }

        private ICommand _clickClearChartCommand;
        public ICommand ClickClearChartCommand
        {
            get
            {
                return _clickClearChartCommand ?? (_clickClearChartCommand = new RelayCommand(param => ClearChartCommand()));
            }
            set => _clickClearChartCommand = value;
        }
        private void ClearChartCommand()
        {
            try
            {
                using (new WaitCursor())
                {
                    ClearCharts();
                }
            }
            catch (Exception ex)
            {
                var monitorItem = new MonitorEntry
                { Datetime = HiResDateTime.UtcNow, Device = MonitorDevice.Server, Category = MonitorCategory.Interface, Type = MonitorType.Error, Method = MethodBase.GetCurrentMethod().Name, Thread = Thread.CurrentThread.ManagedThreadId, Message = $" {ex.Message}, {ex.StackTrace}" };
                MonitorLog.LogToMonitor(monitorItem);

                OpenDialog(ex.Message);
            }
        }

        private ICommand _clickPhdConnectCommand;
        public ICommand ClickPhdConnectCommand
        {
            get
            {
                return _clickPhdConnectCommand ?? (_clickPhdConnectCommand = new RelayCommand(async param => await PhdConnectCommand()));
            }
            set => _clickPhdConnectCommand = value;
        }
        private async Task PhdConnectCommand()
        {
            try
            {
                if (_phd == null)
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    await PhdAsync();
                }
                else
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    PhdClose();
                }
            }
            catch (Exception ex)
            {
                _phd = null;
                PhdBadgeContent = "";

                var monitorItem = new MonitorEntry
                {
                    Datetime = HiResDateTime.UtcNow,
                    Device = MonitorDevice.Server,
                    Category = MonitorCategory.Interface,
                    Type = MonitorType.Error,
                    Method = MethodBase.GetCurrentMethod().Name,
                    Thread = Thread.CurrentThread.ManagedThreadId,
                    Message = $" {ex.Message}, {ex.StackTrace}"
                };
                MonitorLog.LogToMonitor(monitorItem);

                OpenDialog(ex.Message);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private ICommand _clickChartZoomResetCommand;
        public ICommand ClickChartZoomResetCommand
        {
            get
            {
                return _clickChartZoomResetCommand ?? (_clickChartZoomResetCommand = new RelayCommand(param => ChartZoomResetCommand()));
            }
            set => _clickChartZoomResetCommand = value;
        }
        private void ChartZoomResetCommand()
        {
            try
            {
                AxisYmax -= double.NaN;
                AxisYmin += double.NaN;

            }
            catch (Exception ex)
            {
                var monitorItem = new MonitorEntry
                { Datetime = HiResDateTime.UtcNow, Device = MonitorDevice.Server, Category = MonitorCategory.Interface, Type = MonitorType.Error, Method = MethodBase.GetCurrentMethod().Name, Thread = Thread.CurrentThread.ManagedThreadId, Message = $" {ex.Message}" };
                MonitorLog.LogToMonitor(monitorItem);

                OpenDialog(ex.Message);
            }
        }

        #endregion

        #region Chart Settings

        private string _strRaLineOne;
        public string StrRaLineOne
        {
            get => _strRaLineOne;
            private set
            {
                if (_strRaLineOne == value) return;
                _strRaLineOne = value;
                OnPropertyChanged();
            }
        }

        private string _chartName;
        public string ChartName
        {
            get => _chartName;
            set
            {
                if (_chartName == value) return;
                _chartName = value;
                OnPropertyChanged();
            }
        }

        private string _strDecLineOne;
        public string StrDecLineOne
        {
            get => _strDecLineOne;
            set
            {
                if (_strDecLineOne == value) return;
                _strDecLineOne = value;
                OnPropertyChanged();
            }
        }

        private bool _decCheckBox;
        public bool DecCheckBox
        {
            get => _decCheckBox;
            set
            {
                if (value == _decCheckBox) return;
                _decCheckBox = value;
                DecRowHeight = value;
                OnPropertyChanged();
            }
        }

        private bool _raCheckBox;
        public bool RaCheckBox
        {
            get => _raCheckBox;
            set
            {
                if (value == _raCheckBox) return;
                _raCheckBox = value;
                RaRowHeight = value;
                OnPropertyChanged();
            }
        }

        private bool _isCharting;
        private bool IsCharting
        {
            get => _isCharting;
            set
            {
                if (value == _isCharting) return;
                _isCharting = value;
                if (_isCharting)
                {
                    Charting = new Shared.Charting();
                    DataToLog(ChartItemCode.Start, $"{DataType}");
                    ClearChartCommand();
                    _xAxisTimer.Start();
                    _ctsPulse = new CancellationTokenSource();
                    _ctPulse = _ctsPulse.Token;
                }
                else
                {
                    DataToLog(ChartItemCode.Stop, $"{DataType}");
                    _xAxisTimer.Stop();
                    Charting = null;
                    _ctsPulse?.Cancel();
                    _ctsPulse = null;
                }
                StartBadgeContent = value ? Application.Current.Resources["badgeCStart"].ToString() : "";

            }
        }

        public List<string> ColorsList { get; set; }

        private bool _logCharting;
        public bool LogCharting
        {
            get => _logCharting;
            set
            {
                if (_logCharting == value) return;
                _logCharting = value;
                Shared.Settings.LogCharting = value;
                OnPropertyChanged();
            }
        }

        public IList<double> MaxPointsRange { get; set; }
        private double _maxPoints;
        public double MaxPoints
        {
            get => _maxPoints;
            set
            {
                _maxPoints = value;
                OnPropertyChanged();
            }
        }

        public IList<int> Smoothness { get; set; }

        public IList<int> AnimationTimes { get; set; }

        public IList<int> AxisMinSecondsRange { get; set; }

        #endregion

        #region Settings

        public string ThirdColor
        {
            get => ChartSettings.ThirdColor;
            set
            {
                ChartSettings.ThirdColor = value;
                OnPropertyChanged();

            }
        }

        public string FourthColor
        {
            get => ChartSettings.FourthColor;
            set
            {
                ChartSettings.FourthColor = value;
                OnPropertyChanged();
            }
        }

        public string RaColor
        {
            get => ChartSettings.RaColor;
            set
            {
                ChartSettings.RaColor = value;
                OnPropertyChanged();
            }
        }

        public string DecColor
        {
            get => ChartSettings.DecColor;
            set
            {
                ChartSettings.DecColor = value;
                OnPropertyChanged();
            }
        }

        public bool InvertRa
        {
            get => ChartSettings.InvertRa;
            set
            {
                ChartSettings.InvertRa = value;
                OnPropertyChanged();
            }
        }

        public bool InvertDec
        {
            get => ChartSettings.InvertDec;
            set
            {
                ChartSettings.InvertDec = value;
                OnPropertyChanged();
            }
        }

        public bool InvertThird
        {
            get => ChartSettings.InvertThird;
            set
            {
                ChartSettings.InvertThird = value;
                OnPropertyChanged();
            }
        }

        public bool InvertFourth
        {
            get => ChartSettings.InvertFourth;
            set
            {
                ChartSettings.InvertFourth = value;
                OnPropertyChanged();
            }
        }

        public bool RaLine
        {
            get => ChartSettings.RaLine;
            set
            {
                ChartSettings.RaLine = value;
                OnPropertyChanged();
            }
        }

        public bool RaBar
        {
            get => ChartSettings.RaBar;
            set
            {
                ChartSettings.RaBar = value;
                OnPropertyChanged();
            }
        }

        public bool RaStep
        {
            get => ChartSettings.RaStep;
            set
            {
                ChartSettings.RaStep = value;
                OnPropertyChanged();
            }
        }

        public bool DecLine
        {
            get => ChartSettings.DecLine;
            set
            {
                ChartSettings.DecLine = value;
                OnPropertyChanged();
            }
        }

        public bool DecBar
        {
            get => ChartSettings.DecBar;
            set
            {
                ChartSettings.DecBar = value;
                OnPropertyChanged();
            }
        }

        public bool DecStep
        {
            get => ChartSettings.DecStep;
            set
            {
                ChartSettings.DecStep = value;
                OnPropertyChanged();
            }
        }

        public bool ThirdLine
        {
            get => ChartSettings.ThirdLine;
            set
            {
                ChartSettings.ThirdLine = value;
                OnPropertyChanged();
            }
        }

        public bool ThirdBar
        {
            get => ChartSettings.ThirdBar;
            set
            {
                ChartSettings.ThirdBar = value;
                OnPropertyChanged();
            }
        }

        public bool ThirdStep
        {
            get => ChartSettings.ThirdStep;
            set
            {
                ChartSettings.ThirdStep = value;
                OnPropertyChanged();
            }
        }

        public bool FourthLine
        {
            get => ChartSettings.FourthLine;
            set
            {
                ChartSettings.FourthLine = value;
                OnPropertyChanged();
            }
        }

        public bool FourthBar
        {
            get => ChartSettings.FourthBar;
            set
            {
                ChartSettings.FourthBar = value;
                OnPropertyChanged();
            }
        }

        public bool FourthStep
        {
            get => ChartSettings.FourthStep;
            set
            {
                ChartSettings.FourthStep = value;
                OnPropertyChanged();
            }
        }

        public string PhdHostText
        {
            get => ChartSettings.PhdHostText;
            set
            {
                ChartSettings.PhdHostText = value;
                OnPropertyChanged();
            }
        }

        public bool ShowInArcseconds
        {
            get => ChartSettings.ShowInArcseconds;
            set
            {
                ChartSettings.ShowInArcseconds = value;
                SetChartName();
                OnPropertyChanged();
            }
        }

        public bool BaseIndexPosition
        {
            get => ChartSettings.BaseIndexPosition;
            set
            {
                ChartSettings.BaseIndexPosition = value;
                OnPropertyChanged();
            }
        }

        public bool DisableAnimations
        {
            get => ChartSettings.DisableAnimations;
            set
            {
                ChartSettings.DisableAnimations = value;
                OnPropertyChanged();
            }
        }

        public int AxisMinSeconds
        {
            get => ChartSettings.AxisMinSeconds;
            set
            {
                ChartSettings.AxisMinSeconds = value;
                OnPropertyChanged();
            }
        }

        public int AnimationTime
        {
            get => ChartSettings.AnimationTime;
            set
            {
                ChartSettings.AnimationTime = value;
                AnimationsSpeed = TimeSpan.FromMilliseconds(value * 100);
                OnPropertyChanged();
            }
        }

        public int LineSmoothness
        {
            get => ChartSettings.LineSmoothness;
            set
            {
                ChartSettings.LineSmoothness = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region Chart

        private void XAxisTimer_Tick(object sender, EventArgs e)
        {
            SetXaxisLimits(DateTime.Now);
        }

        private bool _raRowHeight;
        public bool RaRowHeight
        {
            get => _raRowHeight;
            set
            {
                if (value == _raRowHeight) return;
                _raRowHeight = value;
                OnPropertyChanged();
            }
        }

        private bool _decRowHeight;
        public bool DecRowHeight
        {
            get => _decRowHeight;
            set
            {
                if (value == _decRowHeight) return;
                _decRowHeight = value;
                OnPropertyChanged();
            }
        }

        private ChartTypes _dataType;
        public ChartTypes DataType
        {
            get => _dataType;
            set
            {
                if (value == _dataType) return;
                IsCharting = false;
                _dataType = value;
                SetChartName();
                OnPropertyChanged();
            }
        }

        private double _axisXmax;
        public double AxisXmax
        {
            get => _axisXmax;
            set
            {
                _axisXmax = value;
                OnPropertyChanged();
            }
        }

        private double _axisXmin;
        public double AxisXmin
        {
            get => _axisXmin;
            set
            {
                _axisXmin = value;
                OnPropertyChanged();
            }
        }

        private double _axisYmax;
        public double AxisYmax
        {
            get => _axisYmax;
            set
            {
                _axisYmax = value;
                OnPropertyChanged();
            }
        }

        private double _axisYmin;
        public double AxisYmin
        {
            get => _axisYmin;
            set
            {
                _axisYmin = value;
                OnPropertyChanged();
            }
        }

        private TimeSpan _animationsSpeed;
        public TimeSpan AnimationsSpeed
        {
            get => _animationsSpeed;
            private set
            {
                _animationsSpeed = value;
                OnPropertyChanged();
            }
        }

        private Func<double, string> _yformatter;
        public Func<double, string> YFormatter
        {
            get => _yformatter;
            set
            {
                _yformatter = value;
                OnPropertyChanged();
            }
        }

        public Func<double, string> DateTimeFormatter { get; set; }
        public long AxisXunit { get; set; }

        //public long AxisYstep { get; set; }
        public double AxisYunit { get; set; }

        private GearedValues<DateTimePoint> _raValues;
        public GearedValues<DateTimePoint> RaValues
        {
            get => _raValues;
            private set
            {
                _raValues = value;
                OnPropertyChanged();
            }
        }

        private GearedValues<DateTimePoint> _decValues;
        public GearedValues<DateTimePoint> DecValues
        {
            get => _decValues;
            private set
            {
                _decValues = value;
                OnPropertyChanged();
            }
        }

        private GearedValues<DateTimePoint> _thirdValues;
        public GearedValues<DateTimePoint> ThirdValues
        {
            get => _thirdValues;
            private set
            {
                _thirdValues = value;
                OnPropertyChanged();
            }
        }

        private GearedValues<DateTimePoint> _fourthValues;
        public GearedValues<DateTimePoint> FourthValues
        {
            get => _fourthValues;
            private set
            {
                _fourthValues = value;
                OnPropertyChanged();
            }
        }

        private void LoadChart()
        {
            //the values property will store our values array
            RaValues = new GearedValues<DateTimePoint>().WithQuality(Quality.High);
            DecValues = new GearedValues<DateTimePoint>().WithQuality(Quality.High);
            ThirdValues = new GearedValues<DateTimePoint>().WithQuality(Quality.High);
            FourthValues = new GearedValues<DateTimePoint>().WithQuality(Quality.High);

            //lets set how to display the X Labels
            DateTimeFormatter = value => new DateTime((long)value).ToString("HH:mm:ss");
            YFormatter = value => value.ToString("N2");

            //AxisStep forces the distance between each separator in the X axis
            //AxisXstep = TimeSpan.FromSeconds(5).Ticks;
            //AxisUnit forces lets the axis know that we are plotting seconds
            //this is not always necessary, but it can prevent wrong labeling
            //AxisXunit = TimeSpan.TicksPerSecond;
            AxisXunit = TimeSpan.FromSeconds(1).Ticks; //AxisXunit = 10000000
            AxisYunit = .5;
            AxisYmax = 3;
            AxisYmin = -3;

            SetXaxisLimits(DateTime.Now);

            MaxPointsRange = new List<double>(Numbers.InclusiveRange(200, 2000, 100));
            AxisMinSecondsRange = new List<int>(Enumerable.Range(10, 51));
            AnimationTimes = new List<int>(Enumerable.Range(0, 10));
            Smoothness = new List<int>(Enumerable.Range(0, 10));
            MaxPoints = 1000;
            DecCheckBox = false;
            RaCheckBox = true;
            LogCharting = Shared.Settings.LogCharting;
            SetChartName();
        }

        private void ClearCharts()
        {
            RaValues?.Clear();
            DecValues?.Clear();
            ThirdValues?.Clear();
            FourthValues?.Clear();
            StrRaLineOne = string.Empty;
        }

        private void SetXaxisLimits(DateTime now)
        {
            AxisXmax = now.Ticks + AxisXunit * 2;
            AxisXmin = now.Ticks - AxisXunit * AxisMinSeconds;
        }

        private void SetChartName()
        {
            ChartName = string.Empty;
            switch (DataType)
            {
                case ChartTypes.Rejected:
                case ChartTypes.Steps:
                case ChartTypes.Execute:
                case ChartTypes.Duration:
                    ChartName = ShowInArcseconds ? $"{DataType} ({Application.Current.Resources["masArcSec"]})" : $"{DataType}";
                    break;
                case ChartTypes.Tracking:
                    ChartName = $"{DataType}";
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void EntryToLog(ChartItemCode itemcode, DateTimePoint entry)
        {
            if (!LogCharting || Charting == null) return;
            var item = new ChartEntry { ItemCode = itemcode, X = entry.DateTime, Y = entry.Value };
            Charting.AddEntry(item);
        }

        private void DataToLog(ChartItemCode chartItemCode, string data)
        {
            if (!LogCharting || Charting == null) return;
            var item = new ChartEntry { ItemCode = chartItemCode, Data = data };
            Charting.AddEntry(item);
        }

        #endregion

        #region Pulse

        private void PulseCharting()
        {
            try
            {
                if (!IsCharting)
                {
                    IsCharting = true;
                    if (TestData) RunTestdata();
                    if (PhdConnected()) _phd?.PixelScale();

                   // var res = _phd?.Call("get_camera_frame_size");

                    _stepsPerSecond = SkyServer.StepsPerRevolution[0] / 360.0 / 3600;
                    DataToLog(ChartItemCode.Data, $"Version={_version}");
                    DataToLog(ChartItemCode.Data, $"StepsPerRevolution={SkyServer.StepsPerRevolution[0]}");
                    DataToLog(ChartItemCode.Data, $"CurrentTrackingRate={SkyServer.CurrentTrackingRate() * 3600}");
                    DataToLog(ChartItemCode.Data, $"ShowInArcseconds={ShowInArcseconds}");
                    DataToLog(ChartItemCode.Data, $"Interval={SkySettings.DisplayInterval}");
                    MonitorLog.GetPulses = true;
                    SkyServer.MonitorPulse = true;
                }
                else
                {
                    StopTestdata();
                    SkyServer.MonitorPulse = false;
                    MonitorLog.GetPulses = false;
                    PhdConnected();
                    IsCharting = false;
                }
            }
            catch (Exception)
            {
                SkyServer.MonitorPulse = false;
                MonitorLog.GetPulses = false;
                StopTestdata();
                _xAxisTimer?.Stop();
                IsCharting = false;
                throw;
            }
        }

        private void PulseProcess(PulseEntry entry)
        {
            if (entry == null) return;

            switch (entry.Axis)
            {
                case 0:
                    if (RaCheckBox)
                    {
                        var ra = new DateTimePoint { DateTime = entry.StartTime.ToLocalTime(), Value = PulseConversion(entry) };
                        switch (DataType)
                        {
                            case ChartTypes.Steps:
                                if (entry.Rejected) break;
                                    RaValues.Add(ra);
                                    EntryToLog(ChartItemCode.RaValue, ra);
                                break;
                            case ChartTypes.Execute:
                                if (entry.Rejected) break;
                                    RaValues.Add(ra);
                                    EntryToLog(ChartItemCode.RaValue, ra);
                                break;
                            case ChartTypes.Duration:
                                if (entry.Rejected) break;
                                    RaValues.Add(ra);
                                    EntryToLog(ChartItemCode.RaValue, ra);
                                break;
                            case ChartTypes.Tracking:
                                if (entry.Rejected) break;
                                    RaValues.Add(ra);
                                    EntryToLog(ChartItemCode.RaValue, ra);
                                break;
                            case ChartTypes.Rejected:
                                if (!entry.Rejected) break;
                                    RaValues.Add(ra);
                                    EntryToLog(ChartItemCode.RaValue, ra);
                                break;
                            default:
                                return;
                        }
                    }
                    break;
                case 1:
                    if (DecCheckBox)
                    {
                        var dec = new DateTimePoint { DateTime = entry.StartTime.ToLocalTime(), Value = PulseConversion(entry) };
                        switch (DataType)
                        {
                            case ChartTypes.Steps:
                                if (entry.Rejected) break;
                                    DecValues.Add(dec);
                                    EntryToLog(ChartItemCode.DecValue, dec);
                                break;
                            case ChartTypes.Execute:
                                if (entry.Rejected) break;
                                    DecValues.Add(dec);
                                    EntryToLog(ChartItemCode.DecValue, dec);
                                break;
                            case ChartTypes.Duration:
                                if (entry.Rejected) break;
                                    DecValues.Add(dec);
                                    EntryToLog(ChartItemCode.DecValue, dec);
                                break;
                            case ChartTypes.Tracking:
                                if (entry.Rejected) break;
                                    DecValues.Add(dec);
                                    EntryToLog(ChartItemCode.DecValue, dec);
                                break;
                            case ChartTypes.Rejected:
                                if (!entry.Rejected) break;
                                    DecValues.Add(dec);
                                    EntryToLog(ChartItemCode.DecValue, dec);
                                break;
                            default:
                                return;
                        }
                    }

                    break;
            }

            //lets only use the last 150 values
            if (RaValues.Count > MaxPoints) RaValues.RemoveAt(0);
            if (DecValues.Count > MaxPoints) DecValues.RemoveAt(0);

        }

        private double PulseConversion(PulseEntry entry)
        {
            double val = 0;
            switch (DataType)
            {
                case ChartTypes.Steps:
                    var difsteps = Math.Abs(entry.PositionEnd - entry.PositionStart);
                    val = ShowInArcseconds ? difsteps / _stepsPerSecond : difsteps;
                    break;
                case ChartTypes.Execute:
                    var ex = (entry.EndTime - entry.StartTime).TotalMilliseconds;
                    val = ShowInArcseconds ? (ex / 1000.0) * Math.Abs(entry.Rate) * 3600 : ex;
                    break;
                case ChartTypes.Duration:
                    var dur = Math.Abs(entry.Duration);
                    val = ShowInArcseconds ? (dur / 1000.0) * Math.Abs(entry.Rate) * 3600 : dur;
                    break;
                case ChartTypes.Rejected:
                    var dur1 = Math.Abs(entry.Duration);
                    val = ShowInArcseconds ? (dur1 / 1000.0) * Math.Abs(entry.Rate) * 3600 : dur1;
                    break;
            }
            val = entry.Rate < 0 ? -val : +val;
            switch (entry.Axis)
            {
                case 0 when InvertRa:
                case 1 when InvertDec:
                    val *= -1;
                    break;
            }
            val = ((int)(val * 1000)) / 1000.00; // set decimal points for chart speed
            return val;
        }

        #endregion

        #region CmdJ

        private void Cmdj1Charting()
        {
            try
            {
                if (!IsCharting)
                {
                    if (SkyServer.Tracking)
                    {
                        IsCharting = true;
                        if (TestData) Timer4();
                        _jstarttime = new DateTime(1900, 01, 01);
                        _stepsPerSecond = SkyServer.StepsPerRevolution[0] / 360.0 / 3600;
                        DataToLog(ChartItemCode.Data, $"Version={_version}");
                        DataToLog(ChartItemCode.Data, $"StepsPerRevolution ={ SkyServer.StepsPerRevolution[0]}");
                        DataToLog(ChartItemCode.Data, $"CurrentTrackingRate={SkyServer.CurrentTrackingRate() * 3600}");
                        DataToLog(ChartItemCode.Data, $"Latitude={SkySettings.Latitude}");
                        DataToLog(ChartItemCode.Data, $"BaseIndexPosition={BaseIndexPosition}");
                        DataToLog(ChartItemCode.Data, $"Interval={SkySettings.DisplayInterval}");
                        MonitorLog.GetjEntries = true;
                    }
                    else
                    {
                        OpenDialog($"{Application.Current.Resources["msgCTurnOnTracking"]} {DataType}");
                    }
                }
                else
                {
                    MonitorLog.GetjEntries = false;
                    mediaTimer?.Stop();
                    IsCharting = false;
                }
            }
            catch (Exception)
            {
                MonitorLog.GetjEntries = false;
                mediaTimer?.Stop();
                IsCharting = false;
                throw;
            }
        }

        private void Cmdj1Process(MonitorEntry entry)
        {
            if (entry == null) return;

            if (RaCheckBox)
            {
                var ra = new DateTimePoint { DateTime = entry.Datetime.ToLocalTime(), Value = Cmdj1Conversion(entry) };
                RaValues.Add(ra);
                EntryToLog(ChartItemCode.RaValue, ra);
            }

            // Max items on the chart
            if (RaValues.Count > MaxPoints) RaValues.RemoveAt(0);
            if (DecValues.Count > MaxPoints) DecValues.RemoveAt(0);
        }

        private double Cmdj1Conversion(MonitorEntry entry)
        {
            var msg = entry.Message.Split(',');
            if (msg.Length < 2) return 0;
            var islong = long.TryParse(msg[2], out var position);
            if (!islong) position = 0;
            if (_jstarttime == new DateTime(1900, 01, 01))
            {
                _jstartpos = position;
                _jstarttime = entry.Datetime;
            }

            var trackingrate = SkyServer.CurrentTrackingRate();
            if (SkyServer.SouthernHemisphere) trackingrate = -trackingrate;
            var steps = (position - _jstartpos) / _stepsPerSecond;
            var time = (entry.Datetime - _jstarttime).TotalSeconds;
            var tracking = (trackingrate * 3600);
            var y = steps - time * tracking;

            // sets up the calculate rate
            var ratesteps = ((_jstartpos - position) / _stepsPerSecond);
            var ratetime = (_jstarttime - entry.Datetime).TotalSeconds;
            var rate = ratesteps / ratetime;
            StrRaLineOne = $"{Application.Current.Resources["msgCEstRate"]} {rate}";

            if (BaseIndexPosition)
            {
                _jstartpos = position;
                _jstarttime = entry.Datetime;
            }

            if (InvertRa) y *= -1;
            return y;
        }

        #endregion

        #region PHD2
        private double PhdPixelScale { get; set; }

        private async Task PhdAsync()
        {
            try
            {
                _ctsPhd = null;
                _ctsPhd = new CancellationTokenSource();
                _ctPhd = _ctsPhd.Token;

                _phd = new GuiderImpl(PhdHostText, 1, _ctsPhd);
                _phd.Connect();
                PhdBadgeContent = _phd.IsConnected() ? Application.Current.Resources["badgeCPhdStart"].ToString() : "";
                Mouse.OverrideCursor = null;

                _taskRunning = true;
                var task = Task.Run(() =>
                {
                    while (_taskRunning)
                    {
                        if (_ctPhd.IsCancellationRequested)
                        {
                            _taskRunning = false;
                        }
                        else
                        {
                            _phd.DoWork();
                        }
                    }
                }, _ctPhd);
                await task;
            }
            catch (GuiderException ex)
            {
                var monitorItem = new MonitorEntry
                { Datetime = HiResDateTime.UtcNow, Device = MonitorDevice.Server, Category = MonitorCategory.Server, Type = MonitorType.Error, Method = MethodBase.GetCurrentMethod().Name, Thread = Thread.CurrentThread.ManagedThreadId, Message = $"{ex.Message}" };
                MonitorLog.LogToMonitor(monitorItem);

                switch (ex.ErrorCode)
                {
                    case Phd.ErrorCode.LostConnection:
                    case Phd.ErrorCode.NewConnection:
                    case Phd.ErrorCode.Disconnected:
                    case Phd.ErrorCode.GuidingError:
                    case Phd.ErrorCode.NoResponse:
                        OpenDialog(ex.Message);
                        break;
                }
            }
            catch (Exception ex)
            {
                var monitorItem = new MonitorEntry
                { Datetime = HiResDateTime.UtcNow, Device = MonitorDevice.Server, Category = MonitorCategory.Server, Type = MonitorType.Error, Method = MethodBase.GetCurrentMethod().Name, Thread = Thread.CurrentThread.ManagedThreadId, Message = $"{ex.Message}" };
                MonitorLog.LogToMonitor(monitorItem);
                OpenDialog(ex.Message);
            }
            finally
            {
                PhdClose();
            }
        }

        private void PhdClose()
        {
            _ctsPhd?.Cancel();
            _ctsPhd = null;
            _phd?.Close();
            _phd = null;
            PhdBadgeContent = "";
        }

        private void PhdProcess(GuideStep entry)
        {
            if (entry == null) return;
            if (!PhdConnected()) return;

            if (Math.Abs(_phd.LastPixelScale) <= 0.0)
            {
                PhdPixelScale = _phd.PixelScale();
            }

            if (RaCheckBox)
            {
                //var val = LocalPixelScale * entry.RADistanceRaw;
                var val = (PhdPixelScale * entry.RADistanceRaw) / Math.Cos(SkyServer.Declination / 3600);
                if (InvertThird) val *= -1;
                val = ((int)(val * 1000)) / 1000.00; // set to 2 decimal points for chart speed
                var ra = new DateTimePoint { DateTime = entry.LocalTimeStamp, Value = val };
                ThirdValues.Add(ra);
                EntryToLog(ChartItemCode.ThirdValue, ra);
            }

            if (DecCheckBox)
            {
                var val = PhdPixelScale * entry.DecDistanceRaw;
                if (InvertFourth) val *= -1;
                val = ((int)(val * 1000)) / 1000.00; // set to 2 decimal points for chart speed
                var dec = new DateTimePoint { DateTime = entry.LocalTimeStamp, Value = val };
                FourthValues.Add(dec);
                EntryToLog(ChartItemCode.FourthValue, dec);
            }

            if (ThirdValues.Count > MaxPoints) ThirdValues.RemoveAt(0);
            if (FourthValues.Count > MaxPoints) FourthValues.RemoveAt(0);
        }

        private bool PhdConnected()
        {
            if (_phd == null)
            {
                PhdBadgeContent = "";
                return false;
            }
            var con = _phd.IsConnected();
            PhdBadgeContent = con ? "on" : "";
            return con;
        }

        #endregion

        #region Dialog  

        private string _DialogMsg;
        public string DialogMsg
        {
            get => _DialogMsg;
            set
            {
                if (_DialogMsg == value) return;
                _DialogMsg = value;
                OnPropertyChanged();
            }
        }

        private bool _isDialogOpen;
        public bool IsDialogOpen
        {
            get => _isDialogOpen;
            set
            {
                if (_isDialogOpen == value) return;
                _isDialogOpen = value;
                OnPropertyChanged();
            }
        }

        private object _dialogContent;
        public object DialogContent
        {
            get => _dialogContent;
            set
            {
                if (_dialogContent == value) return;
                _dialogContent = value;
                OnPropertyChanged();
            }
        }

        private ICommand _openDialogCommand;
        public ICommand OpenDialogCommand
        {
            get
            {
                return _openDialogCommand ?? (_openDialogCommand = new RelayCommand(
                           param => OpenDialog(null)
                       ));
            }
        }
        private void OpenDialog(string msg)
        {
            if (msg != null) DialogMsg = msg;
            DialogContent = new DialogOK();
            IsDialogOpen = true;

            var monitorItem = new MonitorEntry
            { Datetime = HiResDateTime.UtcNow, Device = MonitorDevice.Server, Category = MonitorCategory.Interface, Type = MonitorType.Information, Method = MethodBase.GetCurrentMethod().Name, Thread = Thread.CurrentThread.ManagedThreadId, Message = $" {msg}" };
            MonitorLog.LogToMonitor(monitorItem);
        }

        private ICommand _clickOkDialogCommand;
        public ICommand ClickOkDialogCommand
        {
            get
            {
                return _clickOkDialogCommand ?? (_clickOkDialogCommand = new RelayCommand(
                           param => ClickOkDialog()
                       ));
            }
        }
        private void ClickOkDialog()
        {
            IsDialogOpen = false;
        }

        private ICommand _cancelDialogCommand;
        public ICommand CancelDialogCommand
        {
            get
            {
                return _cancelDialogCommand ?? (_cancelDialogCommand = new RelayCommand(
                           param => CancelDialog()
                       ));
            }
        }
        private void CancelDialog()
        {
            IsDialogOpen = false;
        }

        private ICommand _runMessageDialog;
        public ICommand RunMessageDialogCommand
        {
            get
            {
                return _runMessageDialog ?? (_runMessageDialog = new RelayCommand(
                           param => ExecuteMessageDialog()
                       ));
            }
        }
        private async void ExecuteMessageDialog()
        {
            //let's set up a little MVVM, cos that's what the cool kids are doing:
            var view = new ErrorMessageDialog
            {
                DataContext = new ErrorMessageDialogVM()
            };

            //show the dialog
            await DialogHost.Show(view, "RootDialog", ClosingMessageEventHandler);
        }
        private void ClosingMessageEventHandler(object sender, DialogClosingEventArgs eventArgs)
        {
            Console.WriteLine(@"You can intercept the closing event, and cancel here.");
        }

        #endregion

        #region Test Data

        private bool _testData;
        public bool TestData
        {
            get => _testData;
            set
            {
                if (value == _testData) return;
                _testData = value;
                OnPropertyChanged();
            }
        }
        private bool IsDataOn { get; set; }
        private void RunTestdata()
        {
            if (IsDataOn) return;
            IsDataOn = true;

            ChartLoopAsync();
        }
        private void StopTestdata()
        {
            if (!IsDataOn) return;
            IsDataOn = false;
            _ctsChart?.Cancel();
            _ctsChart?.Dispose();
            _ctsChart = null;
            mediaTimer = null;
        }
        private async void ChartLoopAsync()
        {
            var r = new Random();
            var start = 0;
            try
            {
                if (_ctsChart == null) _ctsChart = new CancellationTokenSource();
                var ct = _ctsChart.Token;
                var IsReading = true;
                var task = Task.Run(() =>
                {
                    while (IsReading)
                    {
                        if (ct.IsCancellationRequested)
                        {
                            // Clean up here, then...
                            // ct.ThrowIfCancellationRequested();
                            IsReading = false;
                        }
                        else
                        {
                            switch (DataType)
                            {
                                case ChartTypes.Steps:
                                case ChartTypes.Execute:
                                case ChartTypes.Duration:
                                    Thread.Sleep(300);
                                    var end = start + r.Next(0, 6);

                                    var pulse = new PulseEntry { PositionStart = start, PositionEnd = end };
                                    if (end % 2 == 0)
                                    {
                                        pulse.Rate = -0.002089028;
                                    }
                                    else
                                    {
                                        pulse.Rate = 0.002089028;
                                    }

                                    pulse.StartTime = HiResDateTime.UtcNow - TimeSpan.FromMilliseconds(r.Next(20, 150));
                                    pulse.EndTime = HiResDateTime.UtcNow;

                                    pulse.Duration = r.Next(1, 50);
                                    start = end;

                                    pulse.Axis = 0;
                                    var axis = r.Next(0, 2);
                                    if (axis == 1) pulse.Axis = 1;

                                    PulseProcess(pulse);
                                    break;
                                case ChartTypes.Tracking:
                                    Thread.Sleep(500);

                                    var hex = $"{Numbers.LongToHex(start)}";
                                    var hexstr = $"={hex}";
                                    //var test = Strings.StringToLong(hex);
                                    start += 65;

                                    var entry = new MonitorEntry
                                    {
                                        Device = MonitorDevice.Telescope,
                                        Category = MonitorCategory.Mount,
                                        Type = MonitorType.Data,
                                        Datetime = HiResDateTime.UtcNow,
                                        Message = $":j1,{hexstr}"
                                    };

                                    Cmdj1Process(entry);
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }

                        }
                    }
                }, ct);
                await task;
                task.Wait(ct);
                IsDataOn = false;
            }
            catch (Exception ex)
            {
                var monitorItem = new MonitorEntry
                { Datetime = HiResDateTime.UtcNow, Device = MonitorDevice.Server, Category = MonitorCategory.Interface, Type = MonitorType.Error, Method = MethodBase.GetCurrentMethod().Name, Thread = Thread.CurrentThread.ManagedThreadId, Message = $" Message:{ex.Message} Stack:{ex.StackTrace}" };
                MonitorLog.LogToMonitor(monitorItem);

                IsDataOn = false;
                OpenDialog(ex.Message);
            }
        }
        // multimedia  /.06-.08 0-1.0%CPU intermidant jumps(2)
        private void Timer4()
        {
            SkyServer.StepsPerRevolution = new long[] { 11136000, 11136000 };
            mediaTimer = new MediaTimer { Period = 500 };
            mediaTimer.Tick += TestDatajEvent;
            mediaTimer.Start();
        }
        private void TestDatajEvent(object sender, EventArgs e)
        {
            var hex = $"{Numbers.LongToHex(startJ)}";
            var hexstr = $"={hex}";
            var num = Strings.StringToLong(hexstr);
            startJ += 65;

            var entry = new MonitorEntry
            {
                Device = MonitorDevice.Telescope,
                Category = MonitorCategory.Mount,
                Type = MonitorType.Data,
                Datetime = HiResDateTime.UtcNow,
                Message = $":j1,{hexstr},{num}"
            };
            // MonitorQueue.WriteOutCmdj(entry);
            Cmdj1Process(entry);
        }

        #endregion
    }

    public enum ChartTypes { Steps, Execute, Duration, Tracking, Rejected }
}
