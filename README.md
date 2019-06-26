# GSServer - ASCOM Synta/SkyWatcher Mount Driver
GS Server is SkyWatcher ASCOM telescope driver for use with astronomy software and SkyWatcher mounts.  It is built using C#, WPF, and a variation of MVVM.

## Features

* Gamepad support
* GPS NMEA support for lat/long/elevation information
* CdC Observatory lat/long/elevation push/pull
* Local DarkSky weather conditions
* PHD2 plotting along with mount steps or pulse information for tracking
* Log Viewer for viewing Charting logs
* Keep observing notes or logs
* Session, Error, and Charting Logs
* Built in simulator for testing
* Synthesized speach commands
* No Sleep mode to keep screensaver off
* Monitor driver, server, and mount data live

![Alt text](Docs/GSServer.jpg?raw=true "GSServer")

You can download the installable version at https://groups.yahoo.com/neo/groups/GreenSwamp/info.  Its located in the files section under the Testing folder.

## Solution Projects

* ASCOM.GS.Sky.Telescope - COM/.Net Class Library implementing the ASCOM device interface for V3 telescope driver.
* ColorPicker - from another source
* GS.LogView - Log viewer for the charting data
* Principles - Class Library that contains a number of fundamental methods including Coordinates, Conversions, Hi Resoulution dates,               Julian dates, Timers, Time, and unit functions.
* GS.SerialTester - WPF application that connects then runs in a loop getting the axes poistions.  Good for testing cables.
* GS.Server - ASCOM local server and organizes the view models 
* GS.Shared - Common code
* GS.Simulator - Complete simulator that mimic a synta mount
* GS.SkyApi - API for the server
* GS.SkyWatcher - Synta codes and mount controls

![Alt text](Docs/GSScreens.jpg?raw=true "GSScreens")

## Built With

* Visual Studio 2017 Community edition
* .Net Framework 4.6.1
* DarkSkyApi for weather information https://darksky.net/dev
* Live Charts and Live Charts Geared - for plotting https://lvcharts.net/
* Material Design In XAML - https://github.com/MaterialDesignInXAML
* SharpDX - gamepad support http://sharpdx.org/
* ASCOM platform 6.4 and developer tools
* Inno Setup Compiler version 5.5.8 http://www.innosetup.com
* Microsoft.Xaml.Behaviors.Wpf
* Newtonsoft.Json

## Authors

* **Robert Morgan** - *Initial work* - https://github.com/rmorgan001

See also the list of [contributors](https://github.com/your/project/contributors) who participated in this project.

## License

This project is licensed under the GNU Affero General Public License - see the [LICENSE.md](LICENSE.md) file for details

Copyright(C) 2019  Rob Morgan (robert.morgan.e@gmail.com)

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

## Acknowledgments

* Hat tip to anyone whose code was used
* ASCOM development team
* Andrew Johansen & Colm Brazel
* SkywatcherEQ8 members at https://groups.yahoo.com/neo/groups/SkywatcherEQ8/info
