using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace RawInput
{
    public sealed class InputDevice
    {
        #region const definitions

        // The following constants are defined in Windows.h

        private const int RIDEV_INPUTSINK = 0x00000100;
        private const int RID_INPUT = 0x10000003;

        private const int FAPPCOMMAND_MASK = 0xF000;
        private const int FAPPCOMMAND_MOUSE = 0x8000;
        private const int FAPPCOMMAND_OEM = 0x1000;

        private const int RIM_TYPEMOUSE = 0;
        private const int RIM_TYPEKEYBOARD = 1;
        private const int RIM_TYPEHID = 2;

        private const int RIDI_DEVICENAME = 0x20000007;

        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        public const int WM_INPUT = 0x00FF;
        public const int WM_CLIPBOARDUPDATE = 0x031D;
        private const int VK_OEM_CLEAR = 0xFE;
        private const int VK_LAST_KEY = VK_OEM_CLEAR; // this is a made up value used as a sentinel

        #endregion const definitions

        #region structs & enums

        /// <summary>
        /// An enum representing the different types of input devices.
        /// </summary>
        public enum DeviceType
        {
            Key,
            Mouse,
            OEM
        }

        /// <summary>
        /// Class encapsulating the information about a
        /// keyboard event, including the device it
        /// originated with and what key was pressed
        /// </summary>
        public class DeviceInfo
        {
            public string deviceName;
            public string deviceType;
            public IntPtr deviceHandle;
            public string Name;
            public string source;
            public ushort key;
            public string vKey;
        }

        #region Windows.h structure declarations

        // The following structures are defined in Windows.h

        [StructLayout(LayoutKind.Sequential)]
        internal struct RAWINPUTDEVICELIST
        {
            public IntPtr hDevice;
            [MarshalAs(UnmanagedType.U4)] public int dwType;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RawInput
        {
            /// <summary>
            /// Header for the data.
            /// </summary>
            public RAWINPUTHEADER Header;

            public Union Data;

            [StructLayout(LayoutKind.Explicit)]
            public struct Union
            {
                /// <summary>
                /// Mouse raw input data.
                /// </summary>
                [FieldOffset(0)] public RAWMOUSE Mouse;

                /// <summary>
                /// Keyboard raw input data.
                /// </summary>
                [FieldOffset(0)] public RAWKEYBOARD Keyboard;

                /// <summary>
                /// HID raw input data.
                /// </summary>
                [FieldOffset(0)] public RAWHID HID;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWINPUTHEADER
        {
            /// <summary>Type of device the input is coming from.</summary>
            public int Type;

            /// <summary>Size of the packet of data.</summary>
            public int Size;

            /// <summary>Handle to the device sending the data.</summary>
            public IntPtr Device;

            /// <summary>wParam from the window message.</summary>
            public IntPtr wParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWHID
        {
            [MarshalAs(UnmanagedType.U4)] public int dwSizHid;
            [MarshalAs(UnmanagedType.U4)] public int dwCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BUTTONSSTR
        {
            [MarshalAs(UnmanagedType.U2)] public ushort usButtonFlags;
            [MarshalAs(UnmanagedType.U2)] public ushort usButtonData;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct RAWMOUSE
        {
            [MarshalAs(UnmanagedType.U2)] [FieldOffset(0)]
            public ushort usFlags;

            [MarshalAs(UnmanagedType.U4)] [FieldOffset(4)]
            public uint ulButtons;

            [FieldOffset(4)] public BUTTONSSTR buttonsStr;

            [MarshalAs(UnmanagedType.U4)] [FieldOffset(8)]
            public uint ulRawButtons;

            [FieldOffset(12)] public int lLastX;
            [FieldOffset(16)] public int lLastY;

            [MarshalAs(UnmanagedType.U4)] [FieldOffset(20)]
            public uint ulExtraInformation;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWKEYBOARD
        {
            [MarshalAs(UnmanagedType.U2)] public ushort MakeCode;
            [MarshalAs(UnmanagedType.U2)] public ushort Flags;
            [MarshalAs(UnmanagedType.U2)] public ushort Reserved;
            [MarshalAs(UnmanagedType.U2)] public ushort VKey;
            [MarshalAs(UnmanagedType.U4)] public uint Message;
            [MarshalAs(UnmanagedType.U4)] public uint ExtraInformation;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWINPUTDEVICE
        {
            [MarshalAs(UnmanagedType.U2)] public ushort usUsagePage;
            [MarshalAs(UnmanagedType.U2)] public ushort usUsage;
            [MarshalAs(UnmanagedType.U4)] public int dwFlags;
            public IntPtr hwndTarget;
        }

        #endregion Windows.h structure declarations

        #endregion structs & enums

        #region DllImports

        [DllImport("User32.dll")]
        private static extern uint
            GetRawInputDeviceList(IntPtr pRawInputDeviceList, ref uint uiNumDevices, uint cbSize);

        [DllImport("User32.dll")]
        private static extern uint
            GetRawInputDeviceInfo(IntPtr hDevice, uint uiCommand, IntPtr pData, ref uint pcbSize);

        [DllImport("User32.dll")]
        private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevice, uint uiNumDevices,
            uint cbSize);

        [DllImport("User32.dll")]
        private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize,
            uint cbSizeHeader);

        #endregion DllImports

        #region Variables and event handling

        /// <summary>
        /// List of keyboard devices. Key: the device handle
        /// Value: the device info class
        /// </summary>
        private Hashtable deviceList = new Hashtable();

        /// <summary>
        /// The delegate to handle KeyPressed events.
        /// </summary>
        /// <param name="sender">The object sending the event.</param>
        /// <param name="e">A set of KeyControlEventArgs information about the key that was pressed and the device it was on.</param>
        public delegate void DeviceEventHandler(InputDevice sender, KeyControlEventArgs e);

        /// <summary>
        /// The event raised when InputDevice detects that a key was pressed.
        /// </summary>
        public event DeviceEventHandler KeyPressed;

        /// <summary>
        /// Arguments provided by the handler for the KeyPressed
        /// event.
        /// </summary>
        public class KeyControlEventArgs : EventArgs
        {
            private DeviceInfo m_deviceInfo;
            private DeviceType m_device;

            public KeyControlEventArgs(DeviceInfo dInfo, DeviceType device)
            {
                m_deviceInfo = dInfo;
                m_device = device;
            }

            public DeviceInfo Keyboard
            {
                get => m_deviceInfo;
                set => m_deviceInfo = value;
            }

            public DeviceType Device
            {
                get => m_device;
                set => m_device = value;
            }
        }

        #endregion Variables and event handling

        #region InputDevice( IntPtr hwnd )

        /// <summary>
        /// InputDevice constructor; registers the raw input devices
        /// for the calling window.
        /// </summary>
        /// <param name="hwnd">Handle of the window listening for key presses</param>
        public InputDevice(IntPtr hwnd)
        {
            //Create an array of all the raw input devices we want to 
            //listen to. In this case, only keyboard devices.
            //RIDEV_INPUTSINK determines that the window will continue
            //to receive messages even when it doesn't have the focus.
            var rid = new RAWINPUTDEVICE[1];

            rid[0].usUsagePage = 0x01;
            rid[0].usUsage = 0x06;
            rid[0].dwFlags = RIDEV_INPUTSINK;
            rid[0].hwndTarget = hwnd;

            if (!RegisterRawInputDevices(rid, (uint) rid.Length, (uint) Marshal.SizeOf(rid[0])))
                throw new ApplicationException("Failed to register raw input device(s).");
        }

        #endregion InputDevice( IntPtr hwnd )

        #region ReadReg( string item, ref bool isKeyboard )

        /// <summary>
        /// Reads the Registry to retrieve a friendly description
        /// of the device, and determine whether it is a keyboard.
        /// </summary>
        /// <param name="item">The device name to search for, as provided by GetRawInputDeviceInfo.</param>
        /// <param name="isKeyboard">Determines whether the device's class is "Keyboard".</param>
        /// <returns>The device description stored in the Registry entry's DeviceDesc value.</returns>
        private string ReadReg(string item, ref bool isKeyboard)
        {
            // Example Device Identification string
            // @"\??\ACPI#PNP0303#3&13c0b0c5&0#{884b96c3-56ef-11d1-bc8c-00a0c91405dd}";

            // remove the \??\
            Console.WriteLine(item);
            item = item.Substring(4);

            var split = item.Split('#');

            var id_01 = split[0]; // ACPI (Class code)
            var id_02 = split[1]; // PNP0303 (SubClass code)
            var id_03 = split[2]; // 3&13c0b0c5&0 (Protocol code)
            //The final part is the class GUID and is not needed here

            //Open the appropriate key as read-only so no permissions
            //are needed.
            var OurKey = Registry.LocalMachine;

            var findme = $@"System\CurrentControlSet\Enum\{id_01}\{id_02}\{id_03}";

            OurKey = OurKey.OpenSubKey(findme, false);

            //Retrieve the desired information and set isKeyboard
            var deviceDesc = (string) OurKey.GetValue("DeviceDesc");
            var deviceClass = (string) OurKey.GetValue("ClassGUID");

            if (deviceClass != null && deviceClass.Equals("{4d36e96b-e325-11ce-bfc1-08002be10318}"))
                isKeyboard = true;
            else
                isKeyboard = false;
            return deviceDesc;
        }

        #endregion ReadReg( string item, ref bool isKeyboard )

        #region int EnumerateDevices()

        /// <summary>
        /// Iterates through the list provided by GetRawInputDeviceList,
        /// counting keyboard devices and adding them to deviceList.
        /// </summary>
        /// <returns>The number of keyboard devices found.</returns>
        public int EnumerateDevices()
        {
            var NumberOfDevices = 0;
            uint deviceCount = 0;
            var dwSize = Marshal.SizeOf(typeof(RAWINPUTDEVICELIST));

            // Get the number of raw input devices in the list,
            // then allocate sufficient memory and get the entire list
            if (GetRawInputDeviceList(IntPtr.Zero, ref deviceCount, (uint) dwSize) == 0)
            {
                var pRawInputDeviceList = Marshal.AllocHGlobal((int) (dwSize * deviceCount));
                GetRawInputDeviceList(pRawInputDeviceList, ref deviceCount, (uint) dwSize);

                // Iterate through the list, discarding undesired items
                // and retrieving further information on keyboard devices
                for (var i = 0; i < deviceCount; i++)
                {
                    DeviceInfo dInfo;
                    string deviceName;
                    uint pcbSize = 0;

                    var rid = (RAWINPUTDEVICELIST) Marshal.PtrToStructure(
                        new IntPtr(pRawInputDeviceList.ToInt32() + dwSize * i),
                        typeof(RAWINPUTDEVICELIST));

                    GetRawInputDeviceInfo(rid.hDevice, RIDI_DEVICENAME, IntPtr.Zero, ref pcbSize);

                    if (pcbSize > 0)
                    {
                        var pData = Marshal.AllocHGlobal((int) pcbSize);
                        GetRawInputDeviceInfo(rid.hDevice, RIDI_DEVICENAME, pData, ref pcbSize);
                        deviceName = Marshal.PtrToStringAnsi(pData);

                        // Drop the "root" keyboard and mouse devices used for Terminal 
                        // Services and the Remote Desktop
                        if (deviceName.ToUpper().Contains("ROOT") || deviceName.Length == 0 ||
                            !deviceName.StartsWith("\\\\?\\HID")) continue;

                        Console.WriteLine("dWType: " + rid.dwType);

                        // If the device is identified in the list as a keyboard or 
                        // HID device, create a DeviceInfo object to store information 
                        // about it
                        if (rid.dwType == RIM_TYPEKEYBOARD || rid.dwType == RIM_TYPEHID)
                        {
                            dInfo = new DeviceInfo
                            {
                                deviceName = Marshal.PtrToStringAnsi(pData),
                                deviceHandle = rid.hDevice,
                                deviceType = GetDeviceType(rid.dwType)
                            };

                            // Check the Registry to see whether this is actually a 
                            // keyboard, and to retrieve a more friendly description.
                            var IsKeyboardDevice = false;
                            var DeviceDesc = ReadReg(deviceName, ref IsKeyboardDevice);
                            dInfo.Name = DeviceDesc;
                            Console.WriteLine(DeviceDesc);

                            // If it is a keyboard and it isn't already in the list,
                            // add it to the deviceList hashtable and increase the
                            // NumberOfDevices count

                            if (!deviceList.Contains(rid.hDevice) && IsKeyboardDevice)
                            {
                                NumberOfDevices++;
                                deviceList.Add(rid.hDevice, dInfo);
                                Console.WriteLine("Handle: " + rid.hDevice + "\n");
                            }
                        }

                        Marshal.FreeHGlobal(pData);
                    }
                }


                Marshal.FreeHGlobal(pRawInputDeviceList);

                return NumberOfDevices;
            }

            throw new ApplicationException("An error occurred while retrieving the list of devices.");
        }

        #endregion EnumerateDevices()

        #region ProcessInputCommand( Message message )

        /// <summary>
        /// Processes WM_INPUT messages to retrieve information about any
        /// keyboard events that occur.
        /// </summary>
        /// <param name="message">The WM_INPUT message to process.</param>
        public void ProcessInputCommand(Message message)
        {
            uint dwSize = 0;

            // First call to GetRawInputData sets the value of dwSize,
            // which can then be used to allocate the appropriate amount of memory,
            // storing the pointer in "buffer".
            GetRawInputData(message.LParam,
                RID_INPUT, IntPtr.Zero,
                ref dwSize,
                (uint) Marshal.SizeOf(typeof(RAWINPUTHEADER)));

            var buffer = Marshal.AllocHGlobal((int) dwSize);
            try
            {
                // Check that buffer points to something, and if so,
                // call GetRawInputData again to fill the allocated memory
                // with information about the input
                if (buffer != IntPtr.Zero &&
                    GetRawInputData(message.LParam,
                        RID_INPUT,
                        buffer,
                        ref dwSize,
                        (uint) Marshal.SizeOf(typeof(RAWINPUTHEADER))) == dwSize)
                {
                    // Store the message information in "raw", then check
                    // that the input comes from a keyboard device before
                    // processing it to raise an appropriate KeyPressed event.

                    var raw = (RawInput) Marshal.PtrToStructure(buffer, typeof(RawInput));

                    if (raw.Header.Type == RIM_TYPEKEYBOARD)
                        // Filter for Key Down events and then retrieve information 
                        // about the keystroke
                        if (raw.Data.Keyboard.Message == WM_KEYDOWN || raw.Data.Keyboard.Message == WM_SYSKEYDOWN)
                        {
                            var key = raw.Data.Keyboard.VKey;

                            // On most keyboards, "extended" keys such as the arrow or 
                            // page keys return two codes - the key's own code, and an
                            // "extended key" flag, which translates to 255. This flag
                            // isn't useful to us, so it can be disregarded.
                            if (key > VK_LAST_KEY) return;

                            // Retrieve information about the device and the
                            // key that was pressed.
                            DeviceInfo dInfo = null;

                            if (deviceList.Contains(raw.Header.Device))
                            {
                                dInfo = (DeviceInfo) deviceList[raw.Header.Device];

                                var myKey = (Keys) Enum.Parse(typeof(Keys), Enum.GetName(typeof(Keys), key));
                                dInfo.vKey = myKey.ToString();
                                dInfo.key = key;
                            }
                            else
                            {
                                var errMessage =
                                    $"Handle :{raw.Header.Device} was not in hashtable. The device may support more than one handle or usage page, and is probably not a standard keyboard.";
                                throw new ApplicationException(errMessage);
                            }

                            // If the key that was pressed is valid and there
                            // was no problem retrieving information on the device,
                            // raise the KeyPressed event.
                            if (KeyPressed != null && dInfo != null)
                            {
                                KeyPressed(this, new KeyControlEventArgs(dInfo, GetDevice(message.LParam.ToInt32())));
                            }
                            else
                            {
                                var errMessage = $"Received Unknown Key: {key}. Possibly an unknown device";
                                throw new ApplicationException(errMessage);
                            }
                        }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        #endregion ProcessInputCommand( Message message )

        #region DeviceType GetDevice( int param )

        /// <summary>
        /// Determines what type of device triggered a WM_INPUT message.
        /// (Used in the ProcessInputCommand method).
        /// </summary>
        /// <param name="param">The LParam from a WM_INPUT message.</param>
        /// <returns>A DeviceType enum value.</returns>
        private DeviceType GetDevice(int param)
        {
            DeviceType deviceType;

            switch ((ushort) (param >> 16) & FAPPCOMMAND_MASK)
            {
                case FAPPCOMMAND_OEM:
                    deviceType = DeviceType.OEM;
                    break;
                case FAPPCOMMAND_MOUSE:
                    deviceType = DeviceType.Mouse;
                    break;
                default:
                    deviceType = DeviceType.Key;
                    break;
            }

            return deviceType;
        }

        #endregion DeviceType GetDevice( int param )

        #region GetDeviceType( int device )

        /// <summary>
        /// Converts a RAWINPUTDEVICELIST dwType value to a string
        /// describing the device type.
        /// </summary>
        /// <param name="device">A dwType value (RIM_TYPEMOUSE, 
        /// RIM_TYPEKEYBOARD or RIM_TYPEHID).</param>
        /// <returns>A string representation of the input value.</returns>
        private string GetDeviceType(int device)
        {
            string deviceType;
            switch (device)
            {
                case RIM_TYPEMOUSE:
                    deviceType = "MOUSE";
                    break;
                case RIM_TYPEKEYBOARD:
                    deviceType = "KEYBOARD";
                    break;
                case RIM_TYPEHID:
                    deviceType = "HID";
                    break;
                default:
                    deviceType = "UNKNOWN";
                    break;
            }

            return deviceType;
        }

        #endregion GetDeviceType( int device )
    }
}