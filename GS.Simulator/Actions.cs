﻿/* Copyright(C) 2019  Rob Morgan (robert.morgan.e@gmail.com)

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as published
    by the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
using System;
using System.Diagnostics;
using GS.Shared;

namespace GS.Simulator
{
    /// <summary>
    /// Actions or commands send to the mount  
    /// </summary>
    public class Actions
    {
        #region Fields

        private readonly IOSerial _ioserial;
        private readonly double[] _stepsPerSec = { 0.0, 0.0 };
        private readonly int[] _revSteps = { 0, 0 };

        #endregion

        #region Properties
        internal static bool IsConnected => IOSerial.IsConnected;
        internal MountInfo MountInfo { get; private set; }
        internal bool MonitorPulse { private get; set; }

        #endregion

        internal Actions()
        {
            _ioserial = new IOSerial();
        }

        #region Action Commands

        /// <summary>
        /// Angle to step
        /// </summary>
        /// <param name="axis"></param>
        /// <param name="angle"></param>
        /// <returns></returns>
        internal long AngleToStep(Axis axis, double angle)
        {
            switch (axis)
            {
                case Axis.Axis1:
                    return (long)(angle * 36000);
                case Axis.Axis2:
                    return (long)(angle * 36000);
                default:
                    throw new ArgumentOutOfRangeException(nameof(axis), axis, null);
            }
        }

        /// <summary>
        /// Get axes in degrees
        /// </summary>
        /// <returns></returns>
        internal double[] AxesDegrees()
        {
            var x = Convert.ToDouble(_ioserial.Send($"degrees,{Axis.Axis1}"));
            var y = Convert.ToDouble(_ioserial.Send($"degrees,{Axis.Axis2}"));
            var d = new[] { x, y };
            return d;
        }

        /// <summary>
        /// Slew to target
        /// </summary>
        /// <param name="axis"></param>
        /// <param name="target"></param>
        internal void AxisGoToTarget(Axis axis, double target)
        {
            _ioserial.Send($"gototarget,{axis},{target}");
        }

        /// <summary>
        /// Set an axis in degrees
        /// </summary>
        /// <param name="axis"></param>
        /// <param name="degrees"></param>
        internal void AxisToDegrees(Axis axis, double degrees)
        {
            _ioserial.Send($"setdegrees,{axis},{degrees}");
        }

        /// <summary>
        /// Send pulse
        /// </summary>
        /// <param name="axis"></param>
        /// <param name="guiderate"></param>
        /// <param name="duration"></param>
        /// <param name="backlash"></param>
        /// <param name="declination"></param>
        /// <returns></returns>
        internal bool AxisPulse(Axis axis, double guiderate, int duration, int backlash, double declination = 0)
        {
            var arcsecs = duration / 1000.0 * Principles.Conversions.Deg2ArcSec(Math.Abs(guiderate));

            switch (axis)
            {
                case Axis.Axis1:
                    //Check for minimum pulse or a pulse less than 1 step
                    if (arcsecs < .0002) return false;
                    break;
                case Axis.Axis2:
                    if (arcsecs < .0002) return false;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(axis), axis, null);
            }

            // setup to log and graph the pulse
            var pulseEntry = new PulseEntry();
            if (MonitorPulse)
            {
                pulseEntry.Axis = (int)axis;
                pulseEntry.Duration = duration;
                pulseEntry.Rate = guiderate;
                pulseEntry.BacklashSteps = backlash;
                pulseEntry.Declination = declination;
                var loc = AxisSteps();
                if (MonitorPulse) pulseEntry.PositionStart = loc[(int)axis];
                pulseEntry.StartTime = Principles.HiResDateTime.UtcNow;
            }

            // execute pulse
            _ioserial.Send($"slew,{axis},{guiderate}");
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed.TotalMilliseconds < duration)
            {
                //do something while waiting
            }
            sw.Reset();
            _ioserial.Send($"slew,{axis},{0}");

            
            if (MonitorPulse)
            {
                var loc1 = AxisSteps();
                pulseEntry.PositionEnd = loc1[(int) axis];
                pulseEntry.EndTime = Principles.HiResDateTime.UtcNow;
                pulseEntry.AltPPECon = false;
                pulseEntry.PPECon = false;
                MonitorLog.LogToMonitor(pulseEntry);
            }
            
            return false;
        }

        /// <summary>
        /// Start axis tracking
        /// </summary>
        /// <param name="axis"></param>
        /// <param name="degrees"></param>
        internal void AxisTracking(Axis axis, double degrees)
        {
            _ioserial.Send($"tracking,{axis},{degrees}");
        }

        /// <summary>
        /// Slew an axis
        /// </summary>
        /// <param name="axis"></param>
        /// <param name="degrees"></param>
        internal void AxisSlew(Axis axis, double degrees)
        {
            _ioserial.Send($"slew,{axis},{degrees}");
        }

        /// <summary>
        /// Get axis status
        /// </summary>
        /// <param name="axis"></param>
        /// <returns></returns>
        internal AxisStatus AxisStatus(Axis axis)
        {
            var a = _ioserial.Send($"axisstatus,{axis}");
            var strings = a.Split(',');
            var b = new AxisStatus()
            {
                Axis = axis,
                Slewing = Convert.ToBoolean(strings[0]),
                Stopped = Convert.ToBoolean(strings[1]),
                Tracking = Convert.ToBoolean(strings[2]),
                Rate = Convert.ToBoolean(strings[3])
            };
            return b;
        }

        /// <summary>
        /// Stop an axis
        /// </summary>
        /// <param name="axis"></param>
        internal void AxisStop(Axis axis)
        {
            _ioserial.Send($"stop,{axis}");
        }

        /// <summary>
        /// Get axis in steps
        /// </summary>
        /// <returns></returns>
        internal int[] AxisSteps()
        {
            var x = Convert.ToInt32(_ioserial.Send($"steps,{Axis.Axis1}"));
            var y = Convert.ToInt32(_ioserial.Send($"steps,{Axis.Axis2}"));
            var i = new[] { x, y };
            return i;
        }

        /// <summary>
        /// Hand control slew
        /// </summary>
        /// <param name="axis"></param>
        /// <param name="degrees"></param>
        internal void HcSlew(Axis axis, double degrees)
        {
            _ioserial.Send($"hc,{axis},{degrees}");
        }

        /// <summary>
        /// Initialize
        /// </summary>
        internal void InitializeAxes()
        {
            _ioserial.Send("initialize");
            LoadDefaults();
        }

        /// <summary>
        /// Loads mount defaults, both axes are the same size
        /// </summary>
        private void LoadDefaults()
        {
            //steps per revolution
            var a = Convert.ToInt32(_ioserial.Send("getrevsteps"));
            _revSteps[0] = a;
            _revSteps[1] = a;
            //steps per second
            var b = Principles.Conversions.StepPerArcsec(a);
            _stepsPerSec[0] = b;
            _stepsPerSec[1] = b;

            var c = _ioserial.Send("capabilities");
            var d = c.Split(',');
            var mountInfo = new MountInfo
            {
                CanAxisSlewsIndependent = Convert.ToBoolean(d[0]),
                CanAzEq = Convert.ToBoolean(d[1]),
                CanDualEncoders = Convert.ToBoolean(d[2]),
                CanHalfTrack = Convert.ToBoolean(d[3]),
                CanHomeSensors = Convert.ToBoolean(d[4]),
                CanPolarLed = Convert.ToBoolean(d[5]),
                CanPpec = Convert.ToBoolean(d[6]),
                CanWifi = Convert.ToBoolean(d[7]),
                MountName = d[8],
                MountVersion = d[9]
            };
            MountInfo = mountInfo;
        }

        /// <summary>
        /// Get name
        /// </summary>
        /// <returns></returns>
        internal string MountName()
        {
            return _ioserial.Send("mountname");
        }

        /// <summary>
        /// Gets version
        /// </summary>
        /// <returns></returns>
        internal string MountVersion()
        {
            return _ioserial.Send("mountversion");
        }

        /// <summary>
        /// Gets Steps Per Revolution
        /// </summary>
        /// <returns></returns>
        internal long Spr()
        {
            return Convert.ToInt32(_ioserial.Send("spr"));
        }

        /// <summary>
        /// Rates from driver
        /// </summary>
        /// <param name="axis"></param>
        /// <param name="degrees"></param>
        internal void Rate(Axis axis, double degrees)
        {
            _ioserial.Send($"rate,{axis},{degrees}");
        }

        /// <summary>
        /// Axis rates from driver
        /// </summary>
        /// <param name="axis"></param>
        /// <param name="degrees"></param>
        internal void RateAxis(Axis axis, double degrees)
        {
            _ioserial.Send($"rateaxis,{axis},{degrees}");
        }

        /// <summary>
        /// Shutdown
        /// </summary>
        internal void Shutdown()
        {
            _ioserial.Send("shutdown");
        }

        #endregion
    }
}
