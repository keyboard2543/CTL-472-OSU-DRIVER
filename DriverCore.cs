using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32.SafeHandles;

namespace CTL472_OsuDriver
{
    public class TabletStateEventArgs : EventArgs
    {
        public double RawX { get; set; }
        public double RawY { get; set; }
        public double TabletXmm { get; set; }
        public double TabletYmm { get; set; }
        public int ScreenX { get; set; }
        public int ScreenY { get; set; }
        public int Pressure { get; set; }
        public bool InProximity { get; set; }
        public bool TipDown { get; set; }
        public bool Button1 { get; set; }
        public bool Button2 { get; set; }
        public double Hz { get; set; }
    }

    public class DriverCore : IDisposable
    {
        // CTL-472 Physical Specifications
        public const double PHYSICAL_WIDTH_MM = 152.0;
        public const double PHYSICAL_HEIGHT_MM = 95.0;
        public const double RAW_MAX_X = 15200.0;
        public const double RAW_MAX_Y = 9500.0;
        public const ushort WACOM_VID = 0x056A;
        public const ushort CTL472_PID = 0x037A;

        #region Win32 API Imports
        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod", SetLastError = true)]
        private static extern uint TimeBeginPeriod(uint uMilliseconds);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod", SetLastError = true)]
        private static extern uint TimeEndPeriod(uint uMilliseconds);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, [In] INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUT
        {
            [FieldOffset(0)]
            public uint type;
            [FieldOffset(4)]
            public MOUSEINPUT mi;
        }

        private const uint INPUT_MOUSE = 0;
        private const uint MOUSEEVENTF_MOVE = 0x0001;
        private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
        private const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern SafeFileHandle CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadFile(
            SafeFileHandle hFile,
            byte[] lpBuffer,
            uint nNumberOfBytesToRead,
            out uint lpNumberOfBytesRead,
            IntPtr lpOverlapped);

        [DllImport("hid.dll", SetLastError = true, EntryPoint = "HidD_GetHidGuid")]
        private static extern void HidD_GetHidGuid(out Guid hidGuid);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr SetupDiGetClassDevs(
            ref Guid ClassGuid,
            string Enumerator,
            IntPtr hwndParent,
            uint Flags);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto, EntryPoint = "SetupDiGetDeviceInterfaceDetail")]
        private static extern bool SetupDiGetDeviceInterfaceDetail(
            IntPtr DeviceInfoSet,
            ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData,
            IntPtr DeviceInterfaceDetailData,
            uint DeviceInterfaceDetailDataSize,
            out uint RequiredSize,
            IntPtr DeviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInterfaces(
            IntPtr DeviceInfoSet,
            IntPtr DeviceInfoData,
            ref Guid InterfaceClassGuid,
            uint MemberIndex,
            ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_GetAttributes(IntPtr HidDeviceObject, ref HIDD_ATTRIBUTES Attributes);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_SetFeature(SafeFileHandle HidDeviceObject, byte[] ReportBuffer, uint ReportBufferLength);

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVICE_INTERFACE_DATA
        {
            public uint cbSize;
            public Guid InterfaceClassGuid;
            public uint Flags;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HIDD_ATTRIBUTES
        {
            public uint Size;
            public ushort VendorID;
            public ushort ProductID;
            public ushort VersionNumber;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [DllImport("user32.dll")]
        private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

        [DllImport("user32.dll")]
        private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern uint GetRawInputDeviceInfo(IntPtr hDevice, uint uiCommand, IntPtr pData, ref uint pcbSize);

        private const uint RIDI_DEVICENAME = 0x20000007;
        private const uint RID_INPUT = 0x10000003;

        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;
        private const uint FILE_FLAG_OVERLAPPED = 0x40000000;
        private const uint DIGCF_PRESENT = 0x00000002;
        private const uint DIGCF_DEVICEINTERFACE = 0x00000010;
        #endregion

        private DriverConfig _config;
        private Thread _driverThread;
        private volatile bool _isRunning = false;

        public event EventHandler<TabletStateEventArgs> TabletStateUpdated;

        private long _packetCount = 0;
        private Stopwatch _hzStopwatch = new Stopwatch();
        private double _currentHz = 0;

        public bool IsTabletConnected { get; private set; }

        public DriverCore(DriverConfig config)
        {
            _config = config;
            TimeBeginPeriod(1); // Set 1ms high precision timer for zero-lag responsiveness
        }

        public void UpdateConfig(DriverConfig config)
        {
            _config = config;
        }

        private long _lastHzUpdate = 0;

        public void RegisterRawInput(IntPtr hwnd)
        {
            RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[2];

            // Pen Digitizer
            rid[0].usUsagePage = 0x0D;
            rid[0].usUsage = 0x02;
            rid[0].dwFlags = 0x00000100; // RIDEV_INPUTSINK
            rid[0].hwndTarget = hwnd;

            // Touch Screen / Digitizer
            rid[1].usUsagePage = 0x0D;
            rid[1].usUsage = 0x01;
            rid[1].dwFlags = 0x00000100; // RIDEV_INPUTSINK
            rid[1].hwndTarget = hwnd;

            RegisterRawInputDevices(rid, 2, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
            
            _hzStopwatch.Start();
            _lastHzUpdate = _hzStopwatch.ElapsedMilliseconds;
        }

        public void ProcessRawInput(IntPtr lParam)
        {
            uint dwSize = 0;
            int headerSize = (IntPtr.Size == 8) ? 24 : 16;
            GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref dwSize, (uint)headerSize);

            if (dwSize == 0) return;

            IntPtr pin = Marshal.AllocHGlobal((int)dwSize);
            try
            {
                if (GetRawInputData(lParam, RID_INPUT, pin, ref dwSize, (uint)headerSize) == dwSize)
                {
                    IntPtr hDevice = Marshal.ReadIntPtr(pin, 8); // Offset of hDevice in RAWINPUTHEADER is 8
                    uint dwType = (uint)Marshal.ReadInt32(pin, 0);

                    if (dwType == 2) // RIM_TYPEHID
                    {
                        string devName = GetDeviceName(hDevice);
                        string upperName = devName != null ? devName.ToUpperInvariant() : "";
                        if (upperName.Contains("VID_056A") || upperName.Contains("PID_037A") || upperName.Contains("WACOM"))
                        {
                            int dwSizeHid = Marshal.ReadInt32(pin, headerSize);
                            int dwCount = Marshal.ReadInt32(pin, headerSize + 4);
                            int dataOffset = headerSize + 8;
                            int totalBytes = dwSizeHid * dwCount;

                            if (totalBytes > 0)
                            {
                                byte[] rawData = new byte[totalBytes];
                                Marshal.Copy(new IntPtr(pin.ToInt64() + dataOffset), rawData, 0, totalBytes);

                                ProcessHidPacket(rawData, totalBytes);

                                IsTabletConnected = true;
                                _packetCount++;
                                long now = _hzStopwatch.ElapsedMilliseconds;
                                if (now - _lastHzUpdate >= 500)
                                {
                                    _currentHz = (_packetCount * 1000.0) / (now - _lastHzUpdate);
                                    _packetCount = 0;
                                    _lastHzUpdate = now;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error processing raw input: " + ex.Message);
            }
            finally
            {
                Marshal.FreeHGlobal(pin);
            }
        }

        private string GetDeviceName(IntPtr hDevice)
        {
            uint size = 0;
            GetRawInputDeviceInfo(hDevice, RIDI_DEVICENAME, IntPtr.Zero, ref size);
            if (size == 0) return string.Empty;

            IntPtr pName = Marshal.AllocHGlobal((int)size);
            try
            {
                if (GetRawInputDeviceInfo(hDevice, RIDI_DEVICENAME, pName, ref size) > 0)
                {
                    return Marshal.PtrToStringAuto(pName);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pName);
            }
            return string.Empty;
        }

        private Thread _smoothThread;
        private double _targetNormX = 0;
        private double _targetNormY = 0;
        private double _currentNormX = 0;
        private double _currentNormY = 0;
        private bool _lastInProximity = false;
        private ushort _lastPressure = 0;
        private bool _lastTipDown = false;
        private bool _lastButton1 = false;
        private bool _lastButton2 = false;

        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;
            _driverThread = new Thread(DriverLoop);
            _driverThread.IsBackground = true;
            _driverThread.Priority = ThreadPriority.Highest;
            _driverThread.Start();

            _smoothThread = new Thread(SmoothLoop);
            _smoothThread.IsBackground = true;
            _smoothThread.Priority = ThreadPriority.Highest;
            _smoothThread.Start();
        }

        public void Stop()
        {
            _isRunning = false;
            if (_driverThread != null && _driverThread.IsAlive)
            {
                _driverThread.Join(500);
            }
            if (_smoothThread != null && _smoothThread.IsAlive)
            {
                _smoothThread.Join(500);
            }
        }

        private void DriverLoop()
        {
            _hzStopwatch.Start();
            long lastHzUpdate = _hzStopwatch.ElapsedMilliseconds;

            while (_isRunning)
            {
                string devicePath = FindWacomDevicePath();
                if (string.IsNullOrEmpty(devicePath))
                {
                    IsTabletConnected = false;
                    Thread.Sleep(500); // Retry connecting to tablet
                    continue;
                }

                SafeFileHandle handle = CreateFile(
                    devicePath,
                    GENERIC_READ | GENERIC_WRITE,
                    FILE_SHARE_READ | FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    OPEN_EXISTING,
                    0,
                    IntPtr.Zero);

                if (handle.IsInvalid)
                {
                    handle = CreateFile(
                        devicePath,
                        GENERIC_READ,
                        FILE_SHARE_READ | FILE_SHARE_WRITE,
                        IntPtr.Zero,
                        OPEN_EXISTING,
                        0,
                        IntPtr.Zero);
                }

                if (handle.IsInvalid)
                {
                    IsTabletConnected = false;
                    Thread.Sleep(500);
                    continue;
                }

                IsTabletConnected = true;
                SendWacomInitFeatureReports(handle);
                byte[] buffer = new byte[64];

                using (handle)
                {
                    while (_isRunning && IsTabletConnected)
                    {
                        uint bytesRead = 0;
                        bool readSuccess = ReadFile(handle, buffer, (uint)buffer.Length, out bytesRead, IntPtr.Zero);

                        if (readSuccess && bytesRead > 0)
                        {
                            ProcessHidPacket(buffer, (int)bytesRead);

                            if (!_config.Enable1000Hz && !_config.Force200Hz)
                            {
                                _packetCount++;
                                long now = _hzStopwatch.ElapsedMilliseconds;
                                if (now - lastHzUpdate >= 500)
                                {
                                    _currentHz = (_packetCount * 1000.0) / (now - lastHzUpdate);
                                    _packetCount = 0;
                                    lastHzUpdate = now;
                                }
                            }
                        }
                        else
                        {
                            IsTabletConnected = false;
                            break;
                        }
                    }
                }
            }
        }

        private double _lastValidRawX = 0;
        private double _lastValidRawY = 0;
        private double _lastValidXmm = 0;
        private double _lastValidYmm = 0;
        private int _lastValidScreenX = 0;
        private int _lastValidScreenY = 0;

        public void ProcessHidPacket(byte[] data, int length)
        {
            if (!_config.EnableDriver || length < 7) return;

            // Wacom CTL-472 HID Report parsing
            byte reportId = data[0];
            byte status = data[1];

            // In proximity bit
            bool tipDown = (status & 0x01) != 0;
            bool button1 = (status & 0x02) != 0;
            bool button2 = (status & 0x04) != 0;
            bool inProximity = (status & 0x20) != 0 || (status & 0x40) != 0 || (status & 0x80) != 0 || tipDown || button1 || button2;

            // Coordinates calculation (Raw range: 0..15200 X, 0..9500 Y)
            ushort rawX_us = (ushort)(data[2] | (data[3] << 8));
            ushort rawY_us = (ushort)(data[4] | (data[5] << 8));
            ushort pressure = (length >= 10) ? (ushort)(data[8] | (data[9] << 8)) : (length >= 8) ? (ushort)(data[6] | (data[7] << 8)) : (ushort)0;

            if (rawX_us > RAW_MAX_X * 1.05 || rawY_us > RAW_MAX_Y * 1.05)
            {
                if (length >= 10 && (data[1] == 0x02 || data[1] == 0x16))
                {
                    rawX_us = (ushort)(data[3] | (data[4] << 8));
                    rawY_us = (ushort)(data[5] | (data[6] << 8));
                }
            }

            bool isZeroCoord = (rawX_us == 0 && rawY_us == 0);
            _lastInProximity = inProximity && !isZeroCoord;

            if (inProximity && !isZeroCoord)
            {
                double rawX = Math.Min(RAW_MAX_X, (double)rawX_us);
                double rawY = Math.Min(RAW_MAX_Y, (double)rawY_us);

                // Convert to physical Tablet mm
                double mmX = rawX * (PHYSICAL_WIDTH_MM / RAW_MAX_X);
                double mmY = rawY * (PHYSICAL_HEIGHT_MM / RAW_MAX_Y);

                // Apply Rotations & Transformations
                double transX = mmX;
                double transY = mmY;

                if (_config.Rotate180)
                {
                    transX = PHYSICAL_WIDTH_MM - transX;
                    transY = PHYSICAL_HEIGHT_MM - transY;
                }

                if (_config.LeftHanded)
                {
                    transX = PHYSICAL_WIDTH_MM - transX;
                }

                // Map Active Area (mm) to Screen Resolution
                double areaW = Math.Max(5.0, _config.AreaWidth);
                double areaH = Math.Max(5.0, _config.AreaHeight);
                double offX = _config.OffsetX;
                double offY = _config.OffsetY;

                double normX = (transX - offX) / areaW;
                double normY = (transY - offY) / areaH;

                // Clamp normalized values [0.0, 1.0]
                normX = Math.Max(0.0, Math.Min(1.0, normX));
                normY = Math.Max(0.0, Math.Min(1.0, normY));

                // Screen bounds
                int screenWidth = SystemInformation.VirtualScreen.Width;
                int screenHeight = SystemInformation.VirtualScreen.Height;
                int screenLeft = SystemInformation.VirtualScreen.Left;
                int screenTop = SystemInformation.VirtualScreen.Top;

                int targetScreenX = screenLeft + (int)Math.Round(normX * (screenWidth - 1));
                int targetScreenY = screenTop + (int)Math.Round(normY * (screenHeight - 1));

                _targetNormX = normX;
                _targetNormY = normY;
                _lastPressure = pressure;
                _lastTipDown = tipDown;
                _lastButton1 = button1;
                _lastButton2 = button2;

                _lastValidRawX = rawX;
                _lastValidRawY = rawY;
                _lastValidXmm = mmX;
                _lastValidYmm = mmY;
                _lastValidScreenX = targetScreenX;
                _lastValidScreenY = targetScreenY;

                if (!_config.Enable1000Hz && !_config.Force200Hz)
                {
                    if (_config.AbsoluteMode)
                    {
                        INPUT[] inputs = new INPUT[1];
                        inputs[0].type = INPUT_MOUSE;
                        inputs[0].mi.dx = (int)Math.Round(normX * 65535.0);
                        inputs[0].mi.dy = (int)Math.Round(normY * 65535.0);
                        inputs[0].mi.dwFlags = MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK | MOUSEEVENTF_MOVE;

                        SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
                    }

                    SetCursorPos(targetScreenX, targetScreenY);
                }
            }

            // Fire state updated event (uses last valid position if keep-alive/idle packet, ensuring Polling Rate and status stay real-time without cursor jumping)
            EventHandler<TabletStateEventArgs> handler = TabletStateUpdated;
            if (handler != null)
            {
                handler(this, new TabletStateEventArgs
                {
                    RawX = _lastValidRawX,
                    RawY = _lastValidRawY,
                    TabletXmm = _lastValidXmm,
                    TabletYmm = _lastValidYmm,
                    ScreenX = _lastValidScreenX,
                    ScreenY = _lastValidScreenY,
                    Pressure = pressure,
                    InProximity = inProximity,
                    TipDown = tipDown,
                    Button1 = button1,
                    Button2 = button2,
                    Hz = _currentHz
                });
            }
        }

        private string FindWacomDevicePath()
        {
            Guid hidGuid;
            try
            {
                HidD_GetHidGuid(out hidGuid);
            }
            catch
            {
                hidGuid = new Guid("4D1E55B2-F16F-11CF-88CB-001111000030");
            }

            IntPtr deviceInfoSet = SetupDiGetClassDevs(ref hidGuid, null, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
            if (deviceInfoSet == IntPtr.Zero) return null;

            System.Collections.Generic.List<string> candidatePaths = new System.Collections.Generic.List<string>();

            try
            {
                SP_DEVICE_INTERFACE_DATA deviceInterfaceData = new SP_DEVICE_INTERFACE_DATA();
                deviceInterfaceData.cbSize = (uint)Marshal.SizeOf(deviceInterfaceData);

                for (uint index = 0; SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref hidGuid, index, ref deviceInterfaceData); index++)
                {
                    uint requiredSize = 0;
                    SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref deviceInterfaceData, IntPtr.Zero, 0, out requiredSize, IntPtr.Zero);

                    if (requiredSize == 0) continue;

                    IntPtr detailDataBuffer = Marshal.AllocHGlobal((int)requiredSize);
                    try
                    {
                        Marshal.WriteInt32(detailDataBuffer, (IntPtr.Size == 8) ? 8 : 4 + Marshal.SystemDefaultCharSize);

                        if (SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref deviceInterfaceData, detailDataBuffer, requiredSize, out requiredSize, IntPtr.Zero))
                        {
                            IntPtr pDevicePath = new IntPtr(detailDataBuffer.ToInt64() + 4);
                            string devicePath = Marshal.PtrToStringAuto(pDevicePath);

                            if (!string.IsNullOrEmpty(devicePath))
                            {
                                string upperPath = devicePath.ToUpperInvariant();
                                if ((upperPath.Contains("VID_056A") || upperPath.Contains("WACOM")) &&
                                    (upperPath.Contains("PID_037A") || upperPath.Contains("037A")))
                                {
                                    candidatePaths.Add(devicePath);
                                }
                            }
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(detailDataBuffer);
                    }
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(deviceInfoSet);
            }

            // PRIORITIZE RAW DIGITIZER COLLECTION (COL02, COL03) WITH VERIFIED READ PERMISSION
            string bestPath = null;
            foreach (string path in candidatePaths)
            {
                string upper = path.ToUpperInvariant();
                if ((upper.Contains("COL02") || upper.Contains("COL03")) && CanOpenRead(path))
                {
                    bestPath = path;
                    break;
                }
            }

            if (bestPath == null)
            {
                foreach (string path in candidatePaths)
                {
                    string upper = path.ToUpperInvariant();
                    if (!upper.Contains("COL01") && CanOpenRead(path))
                    {
                        bestPath = path;
                        break;
                    }
                }
            }

            if (bestPath == null)
            {
                foreach (string path in candidatePaths)
                {
                    if (CanOpenRead(path))
                    {
                        bestPath = path;
                        break;
                    }
                }
            }

            return bestPath;
        }

        private void SmoothLoop()
        {
            Stopwatch sw = Stopwatch.StartNew();
            long ticksPerMs = Stopwatch.Frequency / 1000;
            long nextTick = sw.ElapsedTicks;

            while (_isRunning)
            {
                long intervalTicks = _config.Force200Hz ? (ticksPerMs * 5) : ticksPerMs;
                nextTick += intervalTicks;

                if ((_config.Enable1000Hz || _config.Force200Hz) && IsTabletConnected && _lastInProximity)
                {
                    double lerpFactor = _config.Force200Hz ? 0.95 : 0.65;
                    _currentNormX += (_targetNormX - _currentNormX) * lerpFactor;
                    _currentNormY += (_targetNormY - _currentNormY) * lerpFactor;

                    int screenWidth = SystemInformation.VirtualScreen.Width;
                    int screenHeight = SystemInformation.VirtualScreen.Height;
                    int screenLeft = SystemInformation.VirtualScreen.Left;
                    int screenTop = SystemInformation.VirtualScreen.Top;

                    int targetScreenX = screenLeft + (int)Math.Round(_currentNormX * (screenWidth - 1));
                    int targetScreenY = screenTop + (int)Math.Round(_currentNormY * (screenHeight - 1));

                    if (_config.AbsoluteMode)
                    {
                        INPUT[] inputs = new INPUT[1];
                        inputs[0].type = INPUT_MOUSE;
                        inputs[0].mi.dx = (int)Math.Round(_currentNormX * 65535.0);
                        inputs[0].mi.dy = (int)Math.Round(_currentNormY * 65535.0);
                        inputs[0].mi.dwFlags = MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK | MOUSEEVENTF_MOVE;

                        SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
                    }

                    SetCursorPos(targetScreenX, targetScreenY);

                    _packetCount++;
                    long now = _hzStopwatch.ElapsedMilliseconds;
                    if (now - _lastHzUpdate >= 500)
                    {
                        _currentHz = (_packetCount * 1000.0) / (now - _lastHzUpdate);
                        _packetCount = 0;
                        _lastHzUpdate = now;
                    }

                    EventHandler<TabletStateEventArgs> handler = TabletStateUpdated;
                    if (handler != null)
                    {
                        handler(this, new TabletStateEventArgs
                        {
                            RawX = _lastValidRawX,
                            RawY = _lastValidRawY,
                            TabletXmm = _lastValidXmm,
                            TabletYmm = _lastValidYmm,
                            ScreenX = targetScreenX,
                            ScreenY = targetScreenY,
                            Pressure = _lastPressure,
                            InProximity = _lastInProximity,
                            TipDown = _lastTipDown,
                            Button1 = _lastButton1,
                            Button2 = _lastButton2,
                            Hz = _currentHz
                        });
                    }
                }

                while (sw.ElapsedTicks < nextTick)
                {
                    Thread.SpinWait(10);
                }
            }
        }

        private void SendWacomInitFeatureReports(SafeFileHandle handle)
        {
            try
            {
                byte[] feat2 = new byte[] { 0x02, 0x02 };
                HidD_SetFeature(handle, feat2, (uint)feat2.Length);

                byte[] feat3 = new byte[] { 0x03, 0x01 };
                HidD_SetFeature(handle, feat3, (uint)feat3.Length);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Wacom feature init error: " + ex.Message);
            }
        }

        private bool CanOpenRead(string path)
        {
            SafeFileHandle testHandle = CreateFile(path, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            if (!testHandle.IsInvalid)
            {
                testHandle.Close();
                return true;
            }
            return false;
        }

        public void Dispose()
        {
            Stop();
            TimeEndPeriod(1);
        }
    }
}
