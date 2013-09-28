using Plantronics.UC.SpokesWrapper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.XPath;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Linq;
using Microsoft.Win32;
using System.Windows.Media.Media3D;
using Plantronics.Innovation.PLTLabsAPI;

/*******
 * 
 * Head Tracking Diagnostics
 * 
 * A headtracking diagnostic demo app created for the Plantronics innovation
 * Concept 1 product for use with http://pltlabs.com/
 * 
 * This application shows the following:
 * 
 *   - An app that integrates support for Plantronics innovation head tracking
 *     
 *   - Displays headtracking sensor status and diagnostic info
 *   
 *   - Ability to Calibrate headtracking (zero the angles)
 *   
 *   - Demonstrate subscribing to and outputting in debug log the data from all supported headset services
 *      
 * PRE-REQUISITES for building this demo app:
 * To leverage the API and sample code presented in this developer guide your PC will require the following:
 *   - Microsoft Visual Studio 2010 SP1
 *   - Microsoft .NET Framework 4.0
 *   - Plantronics Spokes SDK 3.0 beta 2, available from PDC site here:
 *     http://developer.plantronics.com/community/nychack
 *     NOTE: you need to Log out of PDC and login again using the following user in order to access this beta:
 *     User: NYChack
 *     Password: IoThack
 *
 * INSTRUCTIONS FOR USE
 * 
 *   - If you turn headset on and leave it still on the desk.
 *     Watch the Sensors tab until is says Calibrated - BUT leave headset
 *     a bit longer until angles stop creeping.
 *     
 *   - Now put headset on and look at center of screen, after 2 second delay, the
 *     head tracking will "auto-calibrate" to 0 degrees heading/pitch/roll.
 *     
 *   - From that point on your head movements will be reflected by the angle
 *     sliders shown on the Sensor tab.
 *     
 * Lewis Collins
 * 
 * VERSION HISTORY:
 * ********************************************************************************
 * Version 1.0.0.0:
 * Date: 24th September 2013
 * Changed by: Lewis Collins
 * Changes:
 *   - Initial version.
 * ********************************************************************************
 *
 **/

namespace HeadTrackingDiagnostics
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, PLTLabsCallbackHandler
    {
        PLTLabsAPI m_pltlabsapi = null;
        private PLTConnection m_pltConnection;

        Timer m_autoputoncalibratetimer;

        // New: debug window
        DebugWindow m_debugwin = null;

        private double m_lastheading = 0;
        private double m_lastpitch = 0;
        private double m_lastroll = 0;
        private int m_lastpedometerreset = -1; // to know where we last reset the pedometer
        private int m_lastpedometercount;

        public MainWindow()
        {
            InitializeComponent();

            // timer to auto calibrate
            m_autoputoncalibratetimer = new Timer();
            m_autoputoncalibratetimer.Interval = 2000;
            m_autoputoncalibratetimer.AutoReset = false;
            m_autoputoncalibratetimer.Elapsed += autoputoncalibratetimer_Elapsed;
        }

        void autoputoncalibratetimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            m_pltlabsapi.calibrateService(PLTService.MOTION_TRACKING_SVC);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            EnableDebugWindow();

            DebugPrint(MethodInfo.GetCurrentMethod().Name, "Head Track Demo - window loaded");
            try
            {
                DebugPrint(MethodInfo.GetCurrentMethod().Name, "About to instantiate PLTLabsAPI object (version: " + PLTLabsAPI.SDK_VERSION + ")");

                sdkversionlabel.Content = "Rev "+PLTLabsAPI.SDK_VERSION;

                m_pltlabsapi = new PLTLabsAPI(this);

                ConnectToPlantronicsDevice();
            }
            catch (Exception exc)
            {
                DebugPrint(MethodInfo.GetCurrentMethod().Name, "xception on connect! \r\n"+exc.ToString());

                MessageBox.Show("Exception on connect! " + exc.ToString(), "HeadTrackDiagnostics Error");
            }

            appversionlabel.Content = Assembly.GetEntryAssembly().GetName().Version;
        }

        void m_spokes_LineActiveChanged(object sender, LineActiveChangedArgs e)
        {
            //MessageBox.Show("line active changed!");
        }

        void UpdateBatteryLevelGUI(PLTBatteryState battstate)
        {
            PLTBatteryLevel batlev = battstate.m_batterylevel;

            batterylevlabel.Dispatcher.Invoke(new Action(delegate()
            {
                switch (batlev)
                {
                    case PLTBatteryLevel.BatteryLevel_Empty:
                        batterylevlabel.Content = "EMPTY!";
                        batterylevlabel.Foreground = new SolidColorBrush(Colors.Red);
                        break;
                    case PLTBatteryLevel.BatteryLevel_Low:
                        batterylevlabel.Content = "LOW";
                        batterylevlabel.Foreground = new SolidColorBrush(Colors.Orange);
                        break;
                    case PLTBatteryLevel.BatteryLevel_Medium:
                        batterylevlabel.Content = "MEDIUM";
                        batterylevlabel.Foreground = new SolidColorBrush(Colors.Yellow);
                        break;
                    case PLTBatteryLevel.BatteryLevel_High:
                        batterylevlabel.Content = "HIGH";
                        batterylevlabel.Foreground = new SolidColorBrush(Colors.GreenYellow);
                        break;
                    case PLTBatteryLevel.BatteryLevel_Full:
                        batterylevlabel.Content = "FULL";
                        batterylevlabel.Foreground = new SolidColorBrush(Colors.Green);
                        break;
                }
            }));
        }

        public DebugWindow GetDebugWin()
        {
            return m_debugwin;
        }

        public void NotifyUserCalibrated()
        {
        }

        public void DebugPrint(string methodname, string str)
        {
            if (m_debugwin != null)
            {
                m_debugwin.DebugPrint(methodname, str);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // INFO: clean shutdown of Headtracking API is required:
            if (m_pltlabsapi != null)
            {
                m_pltlabsapi.Shutdown();
            }

            if (m_debugwin != null)
            {
                m_debugwin.Close();
                m_debugwin = null;
            }
        }

        private void Calibrate_Button_Click(object sender, RoutedEventArgs e)
        {
            DoRecalibrate();
        }

        internal void HeadsetTrackingUpdateGUI(PLTMotionTrackingData headsetdata)
        {
            try
            {
                HeadingSlider.Dispatcher.Invoke(new Action(delegate()
                {
                    // Put orientation into GUI
                    Heading_Label.Content = Math.Round(headsetdata.m_orientation[0])+"°";
                    Pitch_Label.Content = Math.Round(headsetdata.m_orientation[1]) + "°";
                    Roll_Label.Content = Math.Round(headsetdata.m_orientation[2]) + "°";

                    HeadingSlider.Value = headsetdata.m_orientation[0];
                    Pitch_Slider.Value = headsetdata.m_orientation[1];
                    Roll_Slider.Value = headsetdata.m_orientation[2];

                    // Put headset headtracking firmware version number into GUI 
                    // (NOTE: will be available in connection object
                    //  once you have received the first headtracking update from headset)
                    UpdateVersionInfoGUI();

                    // for debug: raw report from headset
                    packetlabel.Content = headsetdata.m_rawreport;

                    m_lastheading = headsetdata.m_orientation[0];
                    m_lastpitch = headsetdata.m_orientation[1];
                    m_lastroll = headsetdata.m_orientation[2];
                }));
            }
            catch (Exception) { }
        }

        // Put headset headtracking firmware version number into GUI 
        // (NOTE: will be available once you have received a headtracking update from headset)
        private void UpdateVersionInfoGUI()
        {
            if (m_pltConnection != null)
            {
                headsetversionlabel.Dispatcher.Invoke(new Action(delegate()
                {
                    headsetversionlabel.Content =
                        m_pltConnection.vermaj + "." + m_pltConnection.vermin;
                }));
            }
            else
            {
                headsetversionlabel.Dispatcher.Invoke(new Action(delegate()
                {
                    headsetversionlabel.Content = "-";
                }));
            }
        }

        private void UpdateTapInfoGUI(PLTTapInfo headsetdata)
        {
            taplabel.Dispatcher.Invoke(new Action(delegate()
            {
                if (headsetdata.m_tapcount > 0)
                {
                    taplabel.Content =
                    headsetdata.m_tapcount + " tap" +
                    (headsetdata.m_tapcount > 1 ? "s ," : " ,") +
                    GetTapDirString(headsetdata.m_tapdirection);
                }
                else
                {
                    taplabel.Content = "-";
                }
            }));
        }

        private void UpdateFreeFallGUI(PLTFreeFall headsetdata)
        {
            freefall_label.Dispatcher.Invoke(new Action(delegate()
            {
                freefall_label.Content = headsetdata.m_isinfreefall ?
                    "Yes" : "No";
                freefall_label.Foreground = headsetdata.m_isinfreefall ?
                    new SolidColorBrush(Colors.Red) : new SolidColorBrush(Colors.Gray);
            }));
        }

        private void UpdateCalibrationGUI(PLTSensorCal headsetdata)
        {
            magnolabel.Dispatcher.Invoke(new Action(delegate()
            {
                magnolabel.Content =
                    headsetdata.m_ismagnetometercal ?
                    "Calibrated" : "Not Calibrated";
                magnolabel.Foreground = headsetdata.m_ismagnetometercal ?
                    new SolidColorBrush(Colors.DarkGreen) : new SolidColorBrush(Colors.Red);

                gyrolabel.Content =
                    headsetdata.m_isgyrocal ?
                    "Calibrated" : "Not Calibrated";
                gyrolabel.Foreground = headsetdata.m_isgyrocal ?
                    new SolidColorBrush(Colors.DarkGreen) : new SolidColorBrush(Colors.Red);
            }));
        }

        private string GetTapDirString(PLTTapDirection tapDirection)
        {
            string retval = "";
            switch (tapDirection)
            {
                case PLTTapDirection.XUp:
                    retval = "X Up";
                    break;
                case PLTTapDirection.XDown:
                    retval = "X Down";
                    break;
                case PLTTapDirection.YUp:
                    retval = "Y Up";
                    break;
                case PLTTapDirection.YDown:
                    retval = "Y Down";
                    break;
                case PLTTapDirection.ZUp:
                    retval = "Z Up";
                    break;
                case PLTTapDirection.ZDown:
                    retval = "Z Down";
                    break;
            }
            return retval;
        }

        private void DebugCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            EnableDebugWindow();
        }

        private void EnableDebugWindow()
        {
            if (m_debugwin == null)
            {
                m_debugwin = new DebugWindow(this);
                m_debugwin.Show();
                this.Activate();
            }
            else
            {
                m_debugwin.WindowState = System.Windows.WindowState.Normal;
                m_debugwin.Show();
            }
            if (DebugCheckbox.IsChecked == false) DebugCheckbox.IsChecked = true;
        }

        private void DebugCheckbox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (m_debugwin != null)
            {
                m_debugwin.Close();
                m_debugwin = null;
                Heading_Label.Content = "";
                Pitch_Label.Content = "";
                Roll_Label.Content = "";
            }
        }

        internal void DisableDebugging()
        {
            if (m_debugwin != null)
            {
                m_debugwin = null;
            }
            DebugCheckbox.IsChecked = false;
        }

        internal void ShowPacketData(string odppayload)
        {
            try
            {
                packetlabel.Dispatcher.Invoke(new Action(delegate()
                {
                    packetlabel.Content = odppayload;
                }));
            }
            catch (Exception) { }
        }

        internal void DoRecalibrate()
        {
            // initiate calibration
            m_pltlabsapi.calibrateService(PLTService.MOTION_TRACKING_SVC);
        }

        private void DebugListAvailableDevices(PLTDevice[] availableDevices)
        {
            StringBuilder sb = new StringBuilder();
            bool first = true;
            foreach (PLTDevice dev in availableDevices)
            {
                if (!first) sb.Append("\r\n");
                first = false;
                sb.Append("> ");
                sb.Append(dev.m_ProductName);
            }
            if (availableDevices.Count() == 0)
            {
                sb.Append("> No device. (Waiting for Plantronics \"Concept 1\" head tracking device to be switched on and paired with PC dongle...)");
            }
            DebugPrint(MethodInfo.GetCurrentMethod().Name, "List of available devices: \r\n" + sb.ToString());
        }

        public void ConnectionClosed(PLTDevice pltDevice)
        {
            m_pltConnection = null;
            UpdateVersionInfoGUI();
        }

        public void ConnectionFailed(PLTDevice pltDevice)
        {
            m_pltConnection = null;
            UpdateVersionInfoGUI();
        }

        public void ConnectionOpen(PLTConnection pltConnection)
        {
            m_pltConnection = pltConnection;

            if (pltConnection != null)
            {
                DebugPrint(MethodInfo.GetCurrentMethod().Name, "Success! Connection was opened!: " + pltConnection.m_device.m_ProductName);

                UpdateVersionInfoGUI();

                DebugPrint(MethodInfo.GetCurrentMethod().Name, "About to register for headset services.");

                // 1. MOTION_TRACKING_SVC Lets register for headtracking service:
                m_pltlabsapi.subscribe(PLTService.MOTION_TRACKING_SVC, PLTMode.On_Change);
                //m_pltlabsapi.subscribe(PLTService.MOTION_TRACKING_SVC, PLTMode.Periodic, 500);

                // Example: configure motion tracking to utilise uncalibrated (raw) quaternions:
                //m_pltlabsapi.configureService(PLTService.MOTION_TRACKING_SVC, PLTConfiguration.MotionSvc_Offset_Raw);

                // Example: configure motion tracking to utilise calibrated quaternions (based on current quaternion):
                m_pltlabsapi.configureService(PLTService.MOTION_TRACKING_SVC, PLTConfiguration.MotionSvc_Offset_Calibrated);

                // Example: configure motion tracking to utilise calibrated quaternions (based on user-specified quaternion):
                //m_pltlabsapi.configureService(PLTService.MOTION_TRACKING_SVC, PLTConfiguration.MotionSvc_Offset_Calibrated,
                //    new PLTQuaternion
                //    {
                //        m_quaternion = new double[4] { 1.0d, 0.0d, 0.0d, 0.0d }
                //    });

                // Example: configure motion tracking to provide angles in quaternion format:
                //m_pltlabsapi.configureService(PLTService.MOTION_TRACKING_SVC, PLTConfiguration.MotionSvc_Format_Quaternion);

                // Example: configure motion tracking to provide angles in orientation format (heading,pitch,roll):
                m_pltlabsapi.configureService(PLTService.MOTION_TRACKING_SVC, PLTConfiguration.MotionSvc_Format_Orientation);

                // 2. MOTION_STATE_SVC Lets register for motion state service 
                // (NOTE: not currently implemented, so commenting out)
                //m_pltlabsapi.subscribe(PLTService.MOTION_STATE_SVC, PLTMode.Periodic, 500);

                // 3. SENSOR_CAL_STATE_SVC
                m_pltlabsapi.subscribe(PLTService.SENSOR_CAL_STATE_SVC, PLTMode.On_Change);

                // 4. PEDOMETER_SVC
                m_pltlabsapi.subscribe(PLTService.PEDOMETER_SVC, PLTMode.On_Change);

                // 5. TAP_SVC
                m_pltlabsapi.subscribe(PLTService.TAP_SVC, PLTMode.On_Change);

                // 6. WEARING_STATE_SVC Lets register for wearing state service
                //m_pltlabsapi.subscribe(PLTService.WEARING_STATE_SVC, PLTMode.Periodic, 1000);
                m_pltlabsapi.subscribe(PLTService.WEARING_STATE_SVC, PLTMode.On_Change);

                // 7. FREE_FALL_SVC
                m_pltlabsapi.subscribe(PLTService.FREE_FALL_SVC, PLTMode.On_Change);

                // 8. PROXIMITY_SVC
                m_pltlabsapi.subscribe(PLTService.PROXIMITY_SVC, PLTMode.On_Change);

                // 9. CALLERID_SVC
                m_pltlabsapi.subscribe(PLTService.CALLERID_SVC, PLTMode.On_Change);

                // 10. CALLSTATE_SVC
                m_pltlabsapi.subscribe(PLTService.CALLSTATE_SVC, PLTMode.On_Change);

                // 11. DOCKSTATE_SVC
                m_pltlabsapi.subscribe(PLTService.DOCKSTATE_SVC, PLTMode.On_Change);

                // 12. CHARGESTATE_SVC Lets register for battery level service
                m_pltlabsapi.subscribe(PLTService.CHARGESTATE_SVC, PLTMode.Periodic, 2000);

                // check we subcribed ok...
                DebugPrintSubscribedServices();

                DebugPrint(MethodInfo.GetCurrentMethod().Name, "Connected ok? "
                    + (pltConnection != null ? "Yes" : "No"));
            }
        }

        private void DebugPrintSubscribedServices()
        {
            if (m_pltConnection != null)
            {
                PLTService[] services = m_pltlabsapi.getSubscribed();

                StringBuilder sb = new StringBuilder();
                bool first = true;
                foreach (PLTService service in services)
                {
                    if (!first) sb.Append("\r\n");
                    first = false;
                    sb.Append("> ");
                    sb.Append(service.ToString());
                }
                DebugPrint(MethodInfo.GetCurrentMethod().Name, "List of subscribed services: \r\n" + sb.ToString());
            }
        }

        // Plantronics device was added to system
        public void DeviceAdded(PLTDevice pltDevice)
        {
            ConnectToPlantronicsDevice();
        }

        public void ConnectToPlantronicsDevice()
        {
            if (m_pltlabsapi == null) return;

            PLTDevice[] availableDevices = m_pltlabsapi.availableDevices();

            DebugListAvailableDevices(availableDevices);

            if (availableDevices.Count() < 1) return;

            if (!m_pltlabsapi.getIsConnected(availableDevices[0]))
            {
                DebugPrint(MethodInfo.GetCurrentMethod().Name, "About to open connection to device: " + availableDevices[0].m_ProductName);
                m_pltlabsapi.openConnection(availableDevices[0]);  // NOTE: PC will only ever show 1 call control device
                // even if you have multiple Plantronics devices attached to PC. Change call control device
                // in Spokes 3.0 settings (system tray)
            }
        }

        public void infoUpdated(PLTConnection pltConnection, PLTInfo pltInfo)
        {
            // make sure we have some data...
            if (pltInfo != null && pltInfo.m_data != null)
            {
                switch (pltInfo.m_serviceType)
                {
                    case PLTService.MOTION_TRACKING_SVC:
                        PLTMotionTrackingData trackingdata = (PLTMotionTrackingData)pltInfo.m_data;
                        //DebugPrint(MethodInfo.GetCurrentMethod().Name, "Motion Tracking Update received:\r\n" +
                        //"raw q0: " + trackingdata.m_rawquaternion[0] + "\r\n" +
                        //"raw q1: " + trackingdata.m_rawquaternion[1] + "\r\n" +
                        //"raw q2: " + trackingdata.m_rawquaternion[2] + "\r\n" +
                        //"raw q3: " + trackingdata.m_rawquaternion[3]);
                        // great we got some angles - lets update the GUI!
                        HeadsetTrackingUpdateGUI(trackingdata);
                        break;
                    case PLTService.MOTION_STATE_SVC:
                        // NOTE: this service is not yet available, no data will come here
                        break;
                    case PLTService.SENSOR_CAL_STATE_SVC:
                        PLTSensorCal caldata = (PLTSensorCal)pltInfo.m_data;
                        UpdateCalibrationGUI(caldata);
                        break;
                    case PLTService.PEDOMETER_SVC:
                        PLTPedometerCount peddata = (PLTPedometerCount)pltInfo.m_data;
                        m_lastpedometercount = peddata.m_pedometercount;
                        if (m_lastpedometerreset == -1) m_lastpedometerreset = m_lastpedometercount;
                        UpdatePedometerGUI();
                        break;
                    case PLTService.TAP_SVC:
                        UpdateTapInfoGUI((PLTTapInfo)pltInfo.m_data);
                        break;
                    case PLTService.WEARING_STATE_SVC:
                        PLTWearingState wearingstate = (PLTWearingState)pltInfo.m_data;
                        DebugPrint(MethodInfo.GetCurrentMethod().Name, "Wearing State Update received:\r\n" +
                            "Is Worn?: " + wearingstate.m_worn + "\r\n" +
                            "Initial State?: " + wearingstate.m_isInitialStateEvent);
                        if (wearingstate.m_worn && !wearingstate.m_isInitialStateEvent)
                        {
                            // they have put headset on, start the auto calibrate timer
                            // to zero angles in 2 seconds time
                            m_autoputoncalibratetimer.Start();
                        }
                        break;
                    case PLTService.FREE_FALL_SVC:
                        UpdateFreeFallGUI((PLTFreeFall)pltInfo.m_data);
                        break;
                    case PLTService.PROXIMITY_SVC:
                        PLTProximity proximitystate = (PLTProximity)pltInfo.m_data;
                        DebugPrint(MethodInfo.GetCurrentMethod().Name, "Proximity State Update received:\r\n" +
                            "Proximity State: " + proximitystate.m_proximity);
                        break;
                    case PLTService.CALLERID_SVC:
                        PLTCallerId callerid = (PLTCallerId)pltInfo.m_data;
                        DebugPrint(MethodInfo.GetCurrentMethod().Name, "Caller Id Update received:\r\n" +
                            "Caller Id: " + callerid.m_callerid+ "\r\n" +
                            "Line type: " + callerid.m_calltype);
                        break;
                    case PLTService.CALLSTATE_SVC:
                        PLTCallStateInfo callinfo = (PLTCallStateInfo)pltInfo.m_data;
                        DebugPrint(MethodInfo.GetCurrentMethod().Name, "Call State Update received:\r\n" +
                            "Call State Event Type: " + callinfo.m_callstatetype + "\r\n" +
                            "Call Status: " + callinfo.m_callstate + "\r\n" +
                            "Internal Call Id: " + callinfo.m_callid + "\r\n" +
                            "Call Source: " + callinfo.m_callsource + "\r\n" +
                            "Was Incoming?: " + callinfo.m_incoming);
                        break;
                    case PLTService.DOCKSTATE_SVC:
                        PLTDock dockedstate = (PLTDock)pltInfo.m_data;
                        DebugPrint(MethodInfo.GetCurrentMethod().Name, "Docked State Update received:\r\n" +
                            "Docked State: " + dockedstate.m_isdocked + "\r\n" +
                            "Is Initial State?: " + dockedstate.m_isinitialstatus);
                        break;
                    case PLTService.CHARGESTATE_SVC:
                        UpdateBatteryLevelGUI((PLTBatteryState)pltInfo.m_data);
                        break;
                }
            }
            else
            {
                // no data...
                DebugPrint(MethodInfo.GetCurrentMethod().Name, "WARNING: no data for service subscription:\r\n" +
                    "SERVICE: " + (pltInfo != null ? pltInfo.m_serviceType.ToString() : "null") );
            }
        }

        private void clear_pedometer_btn_Click(object sender, RoutedEventArgs e)
        {
            // clear the pedometer count
            m_lastpedometerreset = m_lastpedometercount; // effective reset (without reset internally in headset, not currently possible on PC implementation)
            UpdatePedometerGUI();
        }

        private void UpdatePedometerGUI()
        {
            pedometer_label.Dispatcher.Invoke(new Action(delegate()
            {
                pedometer_label.Content =
                    (m_lastpedometercount - m_lastpedometerreset) + " steps";
            }));
        } 
    }
}
