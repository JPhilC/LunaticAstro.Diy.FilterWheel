//
// ASCOM FilterWheel hardware class for the Lunatic Astro Diy FilterWheel
//
// Implements:	ASCOM FilterWheel interface version: 1.0.
// Author:		Phil Crompton <phil@unitysoftware.co.uk>

// TODO: Customise the SetConnected and InitialiseHardware methods as needed for your hardware

using ASCOM;
using ASCOM.Astrometry;
using ASCOM.Astrometry.AstroUtils;
using ASCOM.Astrometry.NOVAS;
using ASCOM.DeviceInterface;
using ASCOM.LocalServer;
using ASCOM.LunaticAstro.FilterWheel.FilterWheelDriver.ViewModel;
using ASCOM.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Integration;

namespace ASCOM.LunaticAstro.FilterWheel.FilterWheelDriver
{
    //
    // TODO Customise the InitialiseHardware() method with code to set up a communication path to your hardware and validate that the hardware exists
    //
    // TODO Customise the SetConnected() method with code to connect to and disconnect from your hardware
    // NOTE You should not need to customise the code in the Connecting, Connect() and Disconnect() members as these are already fully implemented and call SetConnected() when appropriate.
    //
    // TODO Replace the not implemented exceptions with code to implement the functions or throw the appropriate ASCOM exceptions.
    //

    /// <summary>
    /// ASCOM FilterWheel hardware class for LunaticAstroDiyFilterWheel.
    /// </summary>
    [HardwareClass()] // Class attribute flag this as a device hardware class that needs to be disposed by the local server when it exits.
    internal static class FilterWheelHardware
    {
        // Constants used for Profile persistence
        internal const string ComPortProfileName = "COM Port";
        internal const string ComPortDefault = "COM1";
        internal const string TraceStateProfileName = "Trace Level";
        internal const string TraceStateDefault = "true";

        private static string DriverProgId = ""; // ASCOM DeviceID (COM ProgID) for this driver, the value is set by the driver's class initialiser.
        private static string DriverDescription = ""; // The value is set by the driver's class initialiser.
        internal static string ComPort {get; set;} // COM port name (if required)

        private static bool _connectedState; // Local server's connected state
        private static bool _runOnce = false; // Flag to enable "one-off" activities only to run once.
        private static FilterWheelService? _service;
        internal static Util Utilities; // ASCOM Utilities object for use as required
        internal static AstroUtils AstroUtilities; // ASCOM AstroUtilities object for use as required
        internal static TraceLogger Tl; // Local server's trace logger object for diagnostic log with information that you specify

        private static List<Guid> _uniqueIds = new List<Guid>(); // List of driver instance unique IDs

        /// <summary>
        /// Initializes a new instance of the device Hardware class.
        /// </summary>
        static FilterWheelHardware()
        {
            try
            {
                if (IsInDesignMode)
                    return;

                // Create the hardware trace logger in the static initialiser.
                // All other initialisation should go in the InitialiseHardware method.
                Tl = new TraceLogger("", "LunaticAstroDiyFilterWheel.Hardware");

                // DriverProgId has to be set here because it used by ReadProfile to get the TraceState flag.
                DriverProgId = FilterWheel.DriverProgId; // Get this device's ProgID so that it can be used to read the Profile configuration values

                // ReadProfile has to go here before anything is written to the log because it loads the TraceLogger enable / disable state.
                ReadProfile(); // Read device configuration from the ASCOM Profile store, including the trace state

                LogMessage("FilterWheelHardware", $"Static initialiser completed.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
            $"FilterWheelHardware static ctor failed:\r\n{ex}",
            "Driver Startup Error",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);

                //try { LogMessage("FilterWheelHardware", $"Initialisation exception: {ex}"); } catch { }
                //MessageBox.Show($"FilterWheelHardware - {ex.Message}\r\n{ex}", $"Exception creating {FilterWheel.DriverProgId}", MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw;
            }
        }

        /// <summary>
        /// Place device initialisation code here
        /// </summary>
        /// <remarks>Called every time a new instance of the driver is created.</remarks>
        internal static void InitialiseHardware()
        {
            // This method will be called every time a new ASCOM client loads your driver
            LogMessage("InitialiseHardware", $"Start.");

            // Add any code that you want to run every time a client connects to your driver here

            // Add any code that you only want to run when the first client connects in the if (runOnce == false) block below
            if (_runOnce == false)
            {
                LogMessage("InitialiseHardware", $"Starting one-off initialisation.");

                DriverDescription = FilterWheel.DriverDescription; // Get this device's Chooser description

                LogMessage("InitialiseHardware", $"ProgID: {DriverProgId}, Description: {DriverDescription}");

                _connectedState = false; // Initialise connected to false
                Utilities = new Util(); //Initialise ASCOM Utilities object
                AstroUtilities = new AstroUtils(); // Initialise ASCOM Astronomy Utilities object

                LogMessage("InitialiseHardware", "Completed basic initialisation");

                // Add your own "one off" device initialisation here e.g. validating existence of hardware and setting up communications
                // If you are using a serial COM port you will find the COM port name selected by the user through the setup dialogue in the comPort variable.

                LogMessage("InitialiseHardware", $"One-off initialisation complete.");
                _runOnce = true; // Set the flag to ensure that this code is not run again
            }
        }

        public static bool IsInDesignMode =>
            System.ComponentModel.DesignerProperties.GetIsInDesignMode(
                new System.Windows.DependencyObject());

        // PUBLIC COM INTERFACE IFilterWheelV3 IMPLEMENTATION

        #region Common properties and methods.
        /// <summary>Returns the list of custom action names supported by this driver.</summary>
        /// <value>An ArrayList of strings (SafeArray collection) containing the names of supported actions.</value>
        public static ArrayList SupportedActions
        {
            get
            {
                LogMessage("SupportedActions Get", "Returning empty ArrayList");
                return new ArrayList();
            }
        }

        /// <summary>Invokes the specified device-specific custom action.</summary>
        /// <param name="ActionName">A well known name agreed by interested parties that represents the action to be carried out.</param>
        /// <param name="ActionParameters">List of required parameters or an <see cref="String.Empty">Empty String</see> if none are required.</param>
        /// <returns>A string response. The meaning of returned strings is set by the driver author.
        /// <para>Suppose filter wheels start to appear with automatic wheel changers; new actions could be <c>QueryWheels</c> and <c>SelectWheel</c>. The former returning a formatted list
        /// of wheel names and the second taking a wheel name and making the change, returning appropriate values to indicate success or failure.</para>
        /// </returns>
        public static string Action(string actionName, string actionParameters)
        {
            LogMessage("Action", $"Action {actionName}, parameters {actionParameters} is not implemented");
            throw new ActionNotImplementedException("Action " + actionName + " is not implemented by this driver");
        }

        /// <summary>
        /// Transmits an arbitrary string to the device and does not wait for a response.
        /// Optionally, protocol framing characters may be added to the string before transmission.
        /// </summary>
        /// <param name="Command">The literal command string to be transmitted.</param>
        /// <param name="Raw">
        /// if set to <c>true</c> the string is transmitted 'as-is'.
        /// If set to <c>false</c> then protocol framing characters may be added prior to transmission.
        /// </param>
        public static void CommandBlind(string command, bool raw)
        {
            CheckConnected("CommandBlind");
            // TODO The optional CommandBlind method should either be implemented OR throw a MethodNotImplementedException
            // If implemented, CommandBlind must send the supplied command to the mount and return immediately without waiting for a response

            throw new MethodNotImplementedException($"CommandBlind - Command:{command}, Raw: {raw}.");
        }

        /// <summary>
        /// Transmits an arbitrary string to the device and waits for a boolean response.
        /// Optionally, protocol framing characters may be added to the string before transmission.
        /// </summary>
        /// <param name="Command">The literal command string to be transmitted.</param>
        /// <param name="Raw">
        /// if set to <c>true</c> the string is transmitted 'as-is'.
        /// If set to <c>false</c> then protocol framing characters may be added prior to transmission.
        /// </param>
        /// <returns>
        /// Returns the interpreted boolean response received from the device.
        /// </returns>
        public static bool CommandBool(string command, bool raw)
        {
            CheckConnected("CommandBool");
            // TODO The optional CommandBool method should either be implemented OR throw a MethodNotImplementedException
            // If implemented, CommandBool must send the supplied command to the mount, wait for a response and parse this to return a True or False value

            throw new MethodNotImplementedException($"CommandBool - Command:{command}, Raw: {raw}.");
        }

        /// <summary>
        /// Transmits an arbitrary string to the device and waits for a string response.
        /// Optionally, protocol framing characters may be added to the string before transmission.
        /// </summary>
        /// <param name="Command">The literal command string to be transmitted.</param>
        /// <param name="Raw">
        /// if set to <c>true</c> the string is transmitted 'as-is'.
        /// If set to <c>false</c> then protocol framing characters may be added prior to transmission.
        /// </param>
        /// <returns>
        /// Returns the string response received from the device.
        /// </returns>
        public static string CommandString(string command, bool raw)
        {
            CheckConnected("CommandString");
            // TODO The optional CommandString method should either be implemented OR throw a MethodNotImplementedException
            // If implemented, CommandString must send the supplied command to the mount and wait for a response before returning this to the client

            throw new MethodNotImplementedException($"CommandString - Command:{command}, Raw: {raw}.");
        }

        /// <summary>
        /// Deterministically release both managed and unmanaged resources that are used by this class.
        /// </summary>
        /// <remarks>
        /// TODO: Release any managed or unmanaged resources that are used in this class.
        /// 
        /// Do not call this method from the Dispose method in your driver class.
        ///
        /// This is because this hardware class is decorated with the <see cref="HardwareClassAttribute"/> attribute and this Dispose() method will be called 
        /// automatically by the  local server executable when it is irretrievably shutting down. This gives you the opportunity to release managed and unmanaged 
        /// resources in a timely fashion and avoid any time delay between local server close down and garbage collection by the .NET runtime.
        ///
        /// For the same reason, do not call the SharedResources.Dispose() method from this method. Any resources used in the static shared resources class
        /// itself should be released in the SharedResources.Dispose() method as usual. The SharedResources.Dispose() method will be called automatically 
        /// by the local server just before it shuts down.
        /// 
        /// </remarks>
        public static void Dispose()
        {
            try { LogMessage("Dispose", $"Disposing of assets and closing down."); } catch { }

            try
            {
                // Clean up the trace logger and utility objects
                Tl.Enabled = false;
                Tl.Dispose();
                Tl = null;
            }
            catch { }

            try
            {
                Utilities.Dispose();
                Utilities = null;
            }
            catch { }

            try
            {
                AstroUtilities.Dispose();
                AstroUtilities = null;
            }
            catch { }
        }

        private static object hardwareLock = new object(); // Lock object to synchronise access to the serialPorts dictionary

        /// <summary>
        /// Synchronously connects to or disconnects from the hardware
        /// </summary>
        /// <param name="uniqueId">Driver's unique ID</param>
        /// <param name="newState">New state: Connected or Disconnected</param>
        public static void SetConnected(Guid uniqueId, bool newState)
        {
            bool firstConnection = false;
            lock (hardwareLock)
            {
                if (newState)
                {
                    if (!_uniqueIds.Contains(uniqueId))
                    {
                        if (_uniqueIds.Count == 0)
                        {
                            firstConnection = true;
                            _service = new FilterWheelService(ComPort);
                        }
                        _connectedState = true;
                        _uniqueIds.Add(uniqueId);
                        LogMessage("SetConnected", $"Unique id {uniqueId} added.");
                    }
                }
                else
                {
                    if (_uniqueIds.Contains(uniqueId))
                    {
                        _uniqueIds.Remove(uniqueId);
                        LogMessage("SetConnected", $"Unique id {uniqueId} removed.");

                        if (_uniqueIds.Count == 0)
                        {
                            _service?.Disconnect();
                            _service?.Dispose();
                            _service = null;
                            _connectedState = false;
                        }
                    }
                }
            }

            // -----------------------------------------
            // Perform hardware actions OUTSIDE the lock
            // -----------------------------------------

            if (firstConnection)
            {
                try
                {
                    LogMessage("SetConnected", "Connecting to hardware.");
                    _initialised = false;
                    _currentPosition = -1;  // Signal filter wheel is in motion during connection.
                    _service.Connect();
                    LogMessage("SetConnected", $"Hardware connected.");
                    Task.Run(() => InitialiseHardwareAsync()); // Initialise hardware asynchronously but wait for it to complete before proceeding
                }
                catch (Exception ex)
                {
                    LogMessage("SetConnected", $"Hardware connection failed: {ex.Message}");
                    throw new ASCOM.DriverException("Failed to connect to filter wheel hardware.", ex);
                }
            }

            // Log state
            lock (hardwareLock)
            {
                LogMessage("SetConnected", "Currently connected driver ids:");
                foreach (var id in _uniqueIds)
                    LogMessage("SetConnected", $" ID {id} is connected");
            }
        }


        private static bool _initialised = false; // Flag to indicate whether the hardware has been initialised

        private static async Task InitialiseHardwareAsync()
        {
            try
            {
                LogMessage("InitialiseHardwareAsync", $"Hardware initialising .");
                // 1. Wait for firmware to finish homing
                await _service.WaitForReadyAsync();
                
                // 2. Now safe to query hardware
                _slotCount = (short)await _service.GetFilterSlotCountAsync();
                _filterOffsets = await _service.GetOffsetsAsync(_slotCount);
                _filterNames = await _service.GetNamesAsync(_slotCount);
                _currentPosition = (short)await _service.GetCurrentPositionAsync();
                _initialised = true;
                LogMessage("InitialiseHardwareAsync", $"Initialised. Current position: {_currentPosition}");

            }
            catch (Exception ex)
            {
                LogMessage("InitialiseHardwareAsync", $"Initialisation failed: {ex.Message}");
                _initialised = false;
            }
        }

        private static void EnsureInitialised()
        {
            LogMessage("EnsureInitialised", $"Checking initialisation. Initialised: {_initialised}");
            if (_initialised)
                return;

            if (_service == null)
                throw new ASCOM.DriverException("Filter wheel is not connected.");

            var sw = Stopwatch.StartNew();

            while (!_initialised && sw.ElapsedMilliseconds < 5000)
                Thread.Sleep(50);

            if (!_initialised)
                throw new ASCOM.DriverException("Filter wheel failed to initialise within 5 seconds.");
        }



        /// <summary>
        /// Returns a description of the device, such as manufacturer and model number. Any ASCII characters may be used.
        /// </summary>
        /// <value>The description.</value>
        public static string Description
        {
            // TODO customise this device description if required
            get
            {
                LogMessage("Description Get", DriverDescription);
                return DriverDescription;
            }
        }

        /// <summary>
        /// Descriptive and version information about this ASCOM driver.
        /// </summary>
        public static string DriverInfo
        {
            get
            {
                Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                // TODO customise this driver description if required
                string driverInfo = $"Information about the driver itself. Version: {version.Major}.{version.Minor}";
                LogMessage("DriverInfo Get", driverInfo);
                return driverInfo;
            }
        }

        /// <summary>
        /// A string containing only the major and minor version of the driver formatted as 'm.n'.
        /// </summary>
        public static string DriverVersion
        {
            get
            {
                Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                string driverVersion = $"{version.Major}.{version.Minor}";
                LogMessage("DriverVersion Get", driverVersion);
                return driverVersion;
            }
        }

        /// <summary>
        /// The interface version number that this device supports.
        /// </summary>
        public static short InterfaceVersion
        {
            // set by the driver wizard
            get
            {
                LogMessage("InterfaceVersion Get", "3");
                return Convert.ToInt16("3");
            }
        }

        /// <summary>
        /// The short name of the driver, for display purposes
        /// </summary>
        public static string Name
        {
            get
            {
                string name = "Lunatic Astro Nano Filter Wheel";
                LogMessage("Name Get", name);
                return name;
            }
        }

        #endregion

        #region IFilerWheel Implementation
        private static int[] _filterOffsets = new int[0];       //class level variable to hold filter positional offsets
        private static int[] _focusOffsets = new int[0];        // class level variable to hold filter focus offsets
        private static string[] _filterNames = new string[0];   // class level variable to hold filter names
        private static short _currentPosition = -1;              // class level variable to retain the current filter wheel position
        private static short _slotCount = 0;                    // class level variable to hold the number of filter slots in the wheel
        
        /// <summary>
        /// Focus offset of each filter in the wheel
        /// </summary>
        internal static int[] Offsets
        {
            get
            {
                EnsureInitialised();
                foreach (int fwOffset in _filterOffsets) // Write filter offsets to the log
                {
                    LogMessage("FocusOffsets Get", fwOffset.ToString());
                }

                return _filterOffsets;
            }
        }

        /// <summary>
        /// Name of each filter in the wheel
        /// </summary>
        internal static string[] Names
        {
            get
            {
                EnsureInitialised();
                foreach (string fwName in _filterNames) // Write filter names to the log
                {
                    LogMessage("Names Get", fwName);
                }

                return _filterNames;
            }
        }

        /// <summary>
        /// Sets or returns the current filter wheel position
        /// </summary>
        internal static short Position
        {
            get
            {
                LogMessage("Position Get", _currentPosition.ToString());

                // Do NOT call EnsureInitialised() here.
                // Conform will call Position immediately after Connect().
                // If initialisation is still running, return last known value.
                return _currentPosition;
            }

            set
            {
                LogMessage("Position Set", value.ToString());

                // Movement MUST NOT start until initialised
                EnsureInitialised();

                if (value < 0 || value > _slotCount - 1)
                {
                    LogMessage("", $"Throwing InvalidValueException - Position: {value}, Range: 0 to {_slotCount - 1}");
                    throw new InvalidValueException("Position", value.ToString(), $"0 to {_slotCount - 1}");
                }

                // Indicate movement
                _currentPosition = -1;

                // Fire-and-forget movement task
                Task.Run(async () =>
                {
                    try
                    {
                        var result = await _service.GoToPositionAsync(value + 1);
                        _currentPosition = (short)(result - 1);
                    }
                    catch (Exception ex)
                    {
                        LogMessage("Position Set", $"Move failed: {ex.Message}");
                        // Leave _currentPosition = -1 or set to last known safe value
                    }
                });

            }
        }
        #endregion

        #region Private properties and methods
        // Useful methods that can be used as required to help with driver development

        /// <summary>
        /// Returns true if there is a valid connection to the driver hardware
        /// </summary>
        private static bool IsConnected
        {
            get
            {
                // TODO check that the driver hardware connection exists and is connected to the hardware
                return _connectedState;
            }
        }

        /// <summary>
        /// Use this function to throw an exception if we aren't connected to the hardware
        /// </summary>
        /// <param name="message"></param>
        private static void CheckConnected(string message)
        {
            if (!IsConnected)
            {
                throw new NotConnectedException(message);
            }
        }

        /// <summary>
        /// Read the device configuration from the ASCOM Profile store
        /// </summary>
        internal static void ReadProfile()
        {
            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "FilterWheel";
                Tl.Enabled = Convert.ToBoolean(driverProfile.GetValue(DriverProgId, TraceStateProfileName, string.Empty, TraceStateDefault));
                ComPort = driverProfile.GetValue(DriverProgId, ComPortProfileName, string.Empty, ComPortDefault);
            }
        }

        /// <summary>
        /// Write the device configuration to the  ASCOM  Profile store
        /// </summary>
        internal static void WriteProfile()
        {
            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "FilterWheel";
                driverProfile.WriteValue(DriverProgId, TraceStateProfileName, Tl.Enabled.ToString());
                driverProfile.WriteValue(DriverProgId, ComPortProfileName, ComPort.ToString());
            }
        }

        /// <summary>
        /// Reads the filter offsets from the ASCOM Profile store and updates the internal _filterOffsets array. This should be called during initialisation to load any saved offsets from the profile. The offsets are logged for diagnostic purposes.
        /// </summary>
        internal static int[] ReadFocusOffsetsFromProfile()
        {
            // Ensure the array is the correct size
            _focusOffsets = new int[_slotCount];

            using (var driverProfile = new Profile())
            {
                driverProfile.DeviceType = "FilterWheel";

                for (int i = 0; i < _slotCount; i++)
                {
                    string key = $"Filter{i + 1}Offset";
                    string offsetStr = driverProfile.GetValue(DriverProgId, key, string.Empty, "0");

                    if (int.TryParse(offsetStr, out int offset))
                    {
                        _focusOffsets[i] = offset;
                        LogMessage("ReadFocusOffsetsFromProfile",
                            $"Filter {i + 1} offset read from profile: {offset}");
                    }
                    else
                    {
                        _focusOffsets[i] = 0;
                        LogMessage("ReadFocusOffsetsFromProfile",
                            $"Invalid offset for Filter {i + 1}: '{offsetStr}'. Defaulting to 0.");
                    }
                }
            }

            return _focusOffsets;
        }

        /// <summary>
        /// Writes the filter offsets to the ASCOM profile
        /// </summary>
        internal static void WriteFocusOffsetsToProfile(int[] focusOffsets)
        {
            _focusOffsets = focusOffsets;

            using (var driverProfile = new Profile())
            {
                driverProfile.DeviceType = "FilterWheel";

                for (int i = 0; i < _slotCount; i++)
                {
                    string key = $"Filter{i + 1}Offset";
                    driverProfile.WriteValue(DriverProgId, key, _focusOffsets[i].ToString());
                    LogMessage("WriteFocusOffsetsToProfile", $"Filter {i + 1} offset written to profile: {_focusOffsets[i]}");
                }
            }
        }


        /// <summary>
        /// Log helper function that takes identifier and message strings
        /// </summary>
        /// <param name="identifier"></param>
        /// <param name="message"></param>
        internal static void LogMessage(string identifier, string message)
        {
            System.Diagnostics.Debug.WriteLine($"{identifier}: {message}");
            Tl.LogMessageCrLf(identifier, message);
        }

        /// <summary>
        /// Log helper function that takes formatted strings and arguments
        /// </summary>
        /// <param name="identifier"></param>
        /// <param name="message"></param>
        /// <param name="args"></param>
        internal static void LogMessage(string identifier, string message, params object[] args)
        {
            var msg = string.Format(message, args);
            LogMessage(identifier, msg);
        }
        #endregion

        #region Helper methods

        public static async Task ConnectAsync(Guid uniqueId)
        {
            if (!IsConnected)
                await Task.Run(() => SetConnected(uniqueId, true));
        }

        public static void Disconnect(Guid uniqueId)
        {
            if (!IsConnected)
                return;

            SetConnected(uniqueId, false);
        }

        private static void EnsureConnected()
        {
            if (!IsConnected)
                throw new InvalidOperationException("Filter wheel is not connected.");
        }

        public static void SetSlotCount(short newCount)
        {
            // Resize names
            var newNames = new string[newCount];
            for (int i = 0; i < Math.Min(newCount, _filterNames.Length); i++)
                newNames[i] = _filterNames[i];
            _filterNames = newNames;

            // Resize offsets
            var newOffsets = new int[newCount];
            for (int i = 0; i < Math.Min(newCount, _filterOffsets.Length); i++)
                newOffsets[i] = _filterOffsets[i];
            _filterOffsets = newOffsets;


            _slotCount = newCount;
        }

        public static async Task<string[]> GetFilterNamesAsync()
        {
            EnsureConnected();

            int count = _slotCount; // already known from firmware
            return await _service.GetNamesAsync(count);
        }

        public static async Task<int[]> GetOffsetsAsync()
        {
            EnsureConnected();

            int count = _slotCount;
            return await _service.GetOffsetsAsync(count);
        }

        public static async Task SetFilterNamesAsync(string[] names)
        {
            EnsureConnected();

            for (int i = 0; i < names.Length; i++)
            {
                int firmwareIndex = i + 1; // convert 0‑based → 1‑based
                await _service.SetNameAsync(firmwareIndex, names[i]);
            }
        }

        public static async Task SetOffsetsAsync(int[] offsets)
        {
            EnsureConnected();

            for (int i = 0; i < offsets.Length; i++)
            {
                int firmwareIndex = i + 1;
                await _service.SetOffsetAsync(firmwareIndex, offsets[i]);
            }
        }


        #endregion
    }
}

