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

using System;
using System.ComponentModel;
using System.Reflection;
using System.Threading;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using ASCOM.Utilities;
using GS.Principles;
using GS.Server.Domain;
using GS.Server.Helpers;
using GS.Server.Main;
using GS.Server.SkyTelescope;
using GS.Shared;
using HelixToolkit.Wpf;
using MaterialDesignThemes.Wpf;


namespace GS.Server.Model3D
{
    public class Model3DVM : ObservableObject, IPageVM
    {
        public string TopName => "";
        public string BottomName => "3D";
        public int Uid => 5;

        #region Model

        private readonly string _directoryPath = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetName().CodeBase);
        private readonly Util _util = new Util();

        public Model3DVM()
        {
            var monitorItem = new MonitorEntry
                { Datetime = HiResDateTime.UtcNow, Device = MonitorDevice.Server, Category = MonitorCategory.Interface, Type = MonitorType.Information, Method = MethodBase.GetCurrentMethod().Name, Thread = Thread.CurrentThread.ManagedThreadId, Message = " Loading Model3D" };
            MonitorLog.LogToMonitor(monitorItem);

            SkyServer.StaticPropertyChanged += PropertyChangedSkyServer;

            LoadTopBar();
            LoadCompass();
            LoadGEM();
            Rotate();

            ScreenEnabled = SkyServer.IsMountRunning;
        }

        /// <summary>
        /// Property changes from the server
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PropertyChangedSkyServer(object sender, PropertyChangedEventArgs e)
        {
            try
            {
                ThreadContext.BeginInvokeOnUiThread(
             delegate
             {
                 switch (e.PropertyName)
                 {
                     case "Altitude":
                         Altitude = _util.DegreesToDMS(SkyServer.Altitude, "° ", ":", "", 2);
                         break;
                     case "Azimuth":
                         Azimuth = _util.DegreesToDMS(SkyServer.Azimuth, "° ", ":", "", 2);
                         break;
                     case "DeclinationXform":
                         Declination = _util.DegreesToDMS(SkyServer.DeclinationXform, "° ", ":", "", 2);
                         break;
                     case "RightAscensionXform":
                         RightAscension = _util.HoursToHMS(SkyServer.RightAscensionXform, "h ", ":", "", 2);
                         Rotate();
                         break;
                     case "IsMountRunning":
                         ScreenEnabled = SkyServer.IsMountRunning;
                         break;
                 }
             });
            }
            catch (Exception ex)
            {
                var monitorItem = new MonitorEntry
                {
                    Datetime = HiResDateTime.UtcNow,
                    Device = MonitorDevice.Telescope,
                    Category = MonitorCategory.Interface,
                    Type = MonitorType.Error,
                    Method = MethodBase.GetCurrentMethod().Name,
                    Thread = Thread.CurrentThread.ManagedThreadId,
                    Message = $"{ex.Message}"
                };
                MonitorLog.LogToMonitor(monitorItem);

                SkyServer.AlertState = true;
                OpenDialog(ex.Message);
            }
        }

        /// <summary>
        /// Enable or Disable screen items if connected
        /// </summary>
        private bool _screenEnabled;
        public bool ScreenEnabled
        {
            get => _screenEnabled;
            set
            {
                if (_screenEnabled == value) return;
                _screenEnabled = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region Viewport3D

        private System.Windows.Media.Media3D.Model3D _model;
        public System.Windows.Media.Media3D.Model3D Model
        {
            get => _model;
            set
            {
                if (_model == value) return;
                _model = value;
                OnPropertyChanged();
            }
        }

        private double _xaxis;
        public double Xaxis
        {
            get => _xaxis;
            set
            {
                _xaxis = value;
                OnPropertyChanged();
            }
        }

        private double _yaxis;
        public double Yaxis
        {
            get => _yaxis;
            set
            {
                _yaxis = value;
                OnPropertyChanged();
            }
        }

        private double _zaxis;
        public double Zaxis
        {
            get => _zaxis;
            set
            {
                _zaxis = value;
                OnPropertyChanged();
            }
        }

        private Material _compass;
        public Material Compass
        {
            get => _compass;
            set
            {
                _compass = value;
                OnPropertyChanged();
            }
        }

        private void LoadGEM()
        {
            try
            {
                const string gpModel = @"Models/GEM1.obj";
                var filePath = System.IO.Path.Combine(_directoryPath ?? throw new InvalidOperationException(), gpModel);
                var file = new Uri(filePath).LocalPath;
                var import = new ModelImporter();
                Material material = new DiffuseMaterial(new SolidColorBrush(Colors.Crimson));
                import.DefaultMaterial = material;
                Model = import.Load(file);

                Xaxis = -90;
                Yaxis = 90;
                Zaxis = -20;

            }
            catch (Exception ex)
            {
                var monitorItem = new MonitorEntry
                {
                    Datetime = HiResDateTime.UtcNow,
                    Device = MonitorDevice.Telescope,
                    Category = MonitorCategory.Interface,
                    Type = MonitorType.Error,
                    Method = MethodBase.GetCurrentMethod().Name,
                    Thread = Thread.CurrentThread.ManagedThreadId,
                    Message = $"{ex.Message}"
                };
                MonitorLog.LogToMonitor(monitorItem);
                OpenDialog(ex.Message);
            }
        }

        private void LoadCompass()
        {
            try
            {
                const string compassN = @"Models/compassN.png";
                const string compassS = @"Models/compassS.png";
                var compassFile = SkyServer.SouthernHemisphere ? compassS : compassN;
                var filePath = System.IO.Path.Combine(_directoryPath ?? throw new InvalidOperationException(), compassFile);
                var file = new Uri(filePath).LocalPath;
                Compass = MaterialHelper.CreateImageMaterial(file, 20);
            }
            catch (Exception ex)
            {
                var monitorItem = new MonitorEntry
                {
                    Datetime = HiResDateTime.UtcNow,
                    Device = MonitorDevice.Telescope,
                    Category = MonitorCategory.Interface,
                    Type = MonitorType.Error,
                    Method = MethodBase.GetCurrentMethod().Name,
                    Thread = Thread.CurrentThread.ManagedThreadId,
                    Message = $"{ex.Message}"
                };
                MonitorLog.LogToMonitor(monitorItem);
                OpenDialog(ex.Message);
            }
        }

        private void Rotate()
        {
            switch (SkySystem.Mount)
            {
                case MountType.Simulator:
                    Yaxis = Math.Round(SkyServer.ActualAxisX, 3);
                    Xaxis = SkyServer.SouthernHemisphere ? Math.Round(SkyServer.ActualAxisY * -1.0, 3) : Math.Round(SkyServer.ActualAxisY - 180, 3);
                    break;
                case MountType.SkyWatcher:
                    Yaxis = Math.Round(SkyServer.ActualAxisX, 3);
                    Xaxis = Math.Round(SkyServer.ActualAxisY * -1.0, 3);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        #endregion

        #region Top Coord Bar

        private void LoadTopBar()
        {
            RightAscension = "00h 00m 00s";
            Declination = "00° 00m 00s";
            Azimuth = "00° 00m 00s";
            Altitude = "00° 00m 00s";
        }

        private string _altitude;
        public string Altitude
        {
            get => _altitude;
            set
            {
                if (value == _altitude) return;
                _altitude = value;
                OnPropertyChanged();
            }
        }

        private string _azimuth;
        public string Azimuth
        {
            get => _azimuth;
            set
            {
                if (value == _azimuth) return;
                _azimuth = value;
                OnPropertyChanged();
            }
        }

        private string _declination;
        public string Declination
        {
            get => _declination;
            set
            {
                if (value == _declination) return;
                _declination = value;
                OnPropertyChanged();
            }
        }

        private string _rightAscension;
        public string RightAscension
        {
            get => _rightAscension;
            set
            {
                if (value == _rightAscension) return;
                _rightAscension = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region Dialog Message

        private string _dialogMsg;
        public string DialogMsg
        {
            get => _dialogMsg;
            set
            {
                if (_dialogMsg == value) return;
                _dialogMsg = value;
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
            {
                Datetime = HiResDateTime.UtcNow,
                Device = MonitorDevice.Telescope,
                Category = MonitorCategory.Interface,
                Type = MonitorType.Information,
                Method = MethodBase.GetCurrentMethod().Name,
                Thread = Thread.CurrentThread.ManagedThreadId,
                Message = $"{msg}"
            };
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

        private ICommand _clickCancelDialogCommand;
        public ICommand ClickCancelDialogCommand
        {
            get
            {
                return _clickCancelDialogCommand ?? (_clickCancelDialogCommand = new RelayCommand(
                           param => ClickCancelDialog()
                       ));
            }
        }
        private void ClickCancelDialog()
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
    }
}
