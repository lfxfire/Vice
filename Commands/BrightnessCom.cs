using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Vice.Commands
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public uint size;
        public RECT monitor;
        public RECT work;
        public uint flags;
    }

    public static class BrightnessCom
    {
        [DllImport("user32")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lpRect, MonitorEnumProc callback, int dwData);

        private delegate bool MonitorEnumProc(IntPtr hDesktop, IntPtr hdc, ref Rect pRect, int dwData);

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        public static void Test()
        {
            int monCount = 0;
            //Rect r = new Rect();
            MonitorEnumProc callback = (IntPtr hDesktop, IntPtr hdc, ref Rect prect, int d) => ++monCount > 0;
            if (EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, 0))
                Console.WriteLine("You have {0} monitors", monCount);
            else
                Console.WriteLine("An error occured while enumerating monitors");
        }

    }

    /*
    public class BrightnessCom1
    {
        public delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [DllImport("user32.dll")]
        public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll")]
        public static extern bool GetMonitorInfo(IntPtr hmon, ref MONITORINFO mi);

        public class MonitorInfoWithHandle
        {
            /// <summary>
            /// Gets the monitor handle.
            /// </summary>
            /// <value>
            /// The monitor handle.
            /// </value>
            public IntPtr MonitorHandle { get; private set; }

            /// <summary>
            /// Gets the monitor information.
            /// </summary>
            /// <value>
            /// The monitor information.
            /// </value>
            public MONITORINFO MonitorInfo { get; private set; }

            /// <summary>
            /// Initializes a new instance of the <see cref="MonitorInfoWithHandle"/> class.
            /// </summary>
            /// <param name="monitorHandle">The monitor handle.</param>
            /// <param name="monitorInfo">The monitor information.</param>
            public MonitorInfoWithHandle(IntPtr monitorHandle, MONITORINFO monitorInfo)
            {
                MonitorHandle = monitorHandle;
                MonitorInfo = monitorInfo;
            }
        }

        public static bool MonitorEnum(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData)
        {
            var mi = new MONITORINFO();
            mi.size = (uint)Marshal.SizeOf(mi);
            NativeMonitorHelper.GetMonitorInfo(hMonitor, ref mi);

            // Add to monitor info
            _monitorInfos.Add(new MonitorInfoWithHandle(hMonitor, mi));
            return true;
        }

        public static MonitorInfoWithHandle[] GetMonitors()
        {
            // New List
            _monitorInfos = new List<MonitorInfoWithHandle>();

            // Enumerate monitors
            NativeMonitorHelper.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, MonitorEnum, IntPtr.Zero);

            // Return list
            return _monitorInfos.ToArray();
        }

        #region Members

        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
        private IList<WindowAndMonitorHandle> _windowAndMonitorHandles;

        #endregion

        #region Methods

        /// <summary>
        /// Retrieves a list of all main window handles and their associated process id's.
        /// </summary>
        /// <returns></returns>
        public static WindowAndMonitorHandle[] GetWindowAndMonitorHandles()
        {
            // new list
            _windowAndMonitorHandles = new List<WindowAndMonitorHandle>();

            // Enumerate windows
            WindowHelper.EnumWindows(EnumTheWindows, IntPtr.Zero);

            // Return list
            return _windowAndMonitorHandles.ToArray();
        }

        /// <summary>
        /// Enumerates through each window.
        /// </summary>
        /// <param name="windowHandle">The window handle.</param>
        /// <param name="lParam">The l parameter.</param>
        /// <returns></returns>
        private static bool EnumTheWindows(IntPtr windowHandle, IntPtr lParam)
        {
            // Get window area
            var rect = new RECT();
            MonitorHelper.GetWindowRect(windowHandle, ref rect);

            // Get current monitor
            var monitorHandle = MonitorHelper.MonitorFromRect(ref rect, MONITOR_DEFAULTTONEAREST);

            // Add to enumerated windows
            _windowAndMonitorHandles.Add(new WindowAndMonitorHandle(windowHandle, monitorHandle));
            return true;
        }

        #endregion

        #region Native Methods

        /// <summary>
        /// EnumWindows Processor (delegate)
        /// </summary>
        /// <param name="windowHandle">The window handle.</param>
        /// <param name="lParam">The lparameter.</param>
        /// <returns></returns>
        public delegate bool EnumWindowsProc(IntPtr windowHandle, IntPtr lParam);

        /// <summary>
        /// Enums the windows.
        /// </summary>
        /// <param name="enumWindowsProcessorDelegate">The enum windows processor delegate.</param>
        /// <param name="lParam">The lparameter.</param>
        /// <returns></returns>
        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc enumWindowsProcessorDelegate, IntPtr lParam);

        /// <summary>
        /// Gets the rectangle representing the frame of a window.
        /// </summary>
        /// <param name="windowHandle">The window handle.</param>
        /// <param name="rectangle">The rectangle.</param>
        /// <returns></returns>
        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr windowHandle, ref RECT rectangle);

        /// <summary>
        /// Monitors from rect.
        /// </summary>
        /// <param name="rectPointer">The RECT pointer.</param>
        /// <param name="flags">The flags.</param>
        /// <returns></returns>
        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromRect([In] ref RECT rectPointer, uint flags);

        #endregion

        /// <summary>
        /// A simple class to group our handles.
        /// </summary>
        /// <returns></returns>
        public class WindowAndMonitorHandle
        {
            public IntPtr WindowHandle { get; }
            public IntPtr MonitorHandle { get; }

            public WindowAndMonitorHandle(IntPtr windowHandle, IntPtr monitorHandle)
            {
                WindowHandle = windowHandle;
                MonitorHandle = monitorHandle;
            }
        }

        public static void Test()
        {
            MonitorEnumDelegate med = new MonitorEnumDelegate(MonitorEnum);
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, med, IntPtr.Zero);
        }

        //private unsafe static extern bool SetDeviceGammaRamp(Int32 hdc, void* ramp);
        //
        //public unsafe static void Test()
        //{
        //    //SetGamma(100);
        //
        //    Int32 mon = Graphics.FromHwnd(IntPtr.Zero).GetHdc().ToInt32();
        //    
        //    short* gArray = stackalloc short[3 * 256];
        //    short* idx = gArray;
        //    
        //    for (int j = 0; j < 3; j++)
        //    {
        //        for (int i = 0; i < 256; i++)
        //        {
        //            int arrayVal = i * (200);
        //    
        //            if (arrayVal > 65535)
        //                arrayVal = 65535;
        //    
        //            *idx = (short)arrayVal;
        //            idx++;
        //        }
        //    }
        //    
        //    bool retVal = SetDeviceGammaRamp(mon, gArray);
        //    
        //}
        //
        //[DllImport("user32.dll")]
        //public static extern IntPtr GetDC(IntPtr hWnd);
        //
        //[DllImport("gdi32.dll")]
        //public static extern bool SetDeviceGammaRamp(IntPtr hDC, ref RAMP lpRamp);
        //
        //[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        //public struct RAMP
        //{
        //    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        //    public UInt16[] Red;
        //    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        //    public UInt16[] Green;
        //    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        //    public UInt16[] Blue;
        //}
        //
        //public static void SetGamma(int gamma)
        //{
        //    if (gamma <= 256 && gamma >= 1)
        //    {
        //        RAMP ramp = new RAMP();
        //        ramp.Red = new ushort[256];
        //        ramp.Green = new ushort[256];
        //        ramp.Blue = new ushort[256];
        //        for (int i = 1; i < 256; i++)
        //        {
        //            int iArrayValue = i * (gamma + 128);
        //
        //            if (iArrayValue > 65535)
        //                iArrayValue = 65535;
        //            ramp.Red[i] = ramp.Blue[i] = ramp.Green[i] = (ushort)iArrayValue;
        //        }
        //        SetDeviceGammaRamp(GetDC(IntPtr.Zero), ref ramp);
        //    }
        //}
    }
}

    */

}