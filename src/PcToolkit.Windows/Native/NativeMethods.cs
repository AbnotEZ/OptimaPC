using System.Runtime.InteropServices;

namespace PcToolkit.Windows.Native;

/// <summary>Declaraciones Win32 usadas en modo de solo lectura. Referencias en docs/data-sources.md.</summary>
internal static class NativeMethods
{
    // ---------- kernel32 ----------

    [StructLayout(LayoutKind.Sequential)]
    internal struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetPhysicallyInstalledSystemMemory(out ulong totalMemoryInKilobytes);

    internal const int RelationProcessorCore = 0;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetLogicalProcessorInformationEx(int relationshipType, IntPtr buffer, ref uint returnedLength);

    [StructLayout(LayoutKind.Sequential)]
    internal struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);

    [DllImport("kernel32.dll")]
    internal static extern IntPtr LocalFree(IntPtr hMem);

    // ---------- powrprof (solo lectura) ----------

    internal const uint ACCESS_SCHEME = 16;
    internal const uint ERROR_NO_MORE_ITEMS = 259;
    internal const uint ERROR_MORE_DATA = 234;

    [DllImport("powrprof.dll")]
    internal static extern uint PowerGetActiveScheme(IntPtr userRootPowerKey, out IntPtr activePolicyGuid);

    [DllImport("powrprof.dll")]
    internal static extern uint PowerEnumerate(IntPtr rootPowerKey, IntPtr schemeGuid, IntPtr subGroupOfPowerSettingsGuid,
        uint accessFlags, uint index, ref Guid buffer, ref uint bufferSize);

    [DllImport("powrprof.dll")]
    internal static extern uint PowerReadFriendlyName(IntPtr rootPowerKey, ref Guid schemeGuid, IntPtr subGroupOfPowerSettingsGuid,
        IntPtr powerSettingGuid, IntPtr buffer, ref uint bufferSize);

    /// <summary>Modo de energía efectivo (superposición). Documentado en powersetting.h; puede no existir en versiones antiguas.</summary>
    [DllImport("powrprof.dll")]
    internal static extern uint PowerGetEffectiveOverlayScheme(out Guid effectiveOverlayGuid);

    // ---------- user32 (pantallas) ----------

    internal const int ENUM_CURRENT_SETTINGS = -1;
    internal const int DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x1;
    internal const int DISPLAY_DEVICE_PRIMARY_DEVICE = 0x4;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public ushort dmSpecVersion;
        public ushort dmDriverVersion;
        public ushort dmSize;
        public ushort dmDriverExtra;
        public uint dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public uint dmDisplayOrientation;
        public uint dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public ushort dmLogPixels;
        public uint dmBitsPerPel;
        public uint dmPelsWidth;
        public uint dmPelsHeight;
        public uint dmDisplayFlags;
        public uint dmDisplayFrequency;
        public uint dmICMMethod;
        public uint dmICMIntent;
        public uint dmMediaType;
        public uint dmDitherType;
        public uint dmReserved1;
        public uint dmReserved2;
        public uint dmPanningWidth;
        public uint dmPanningHeight;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

    // ---------- pdh (contadores de rendimiento) ----------

    internal const uint PDH_FMT_DOUBLE = 0x00000200;
    internal const uint PDH_FMT_NOCAP100 = 0x00008000;
    internal const uint PDH_MORE_DATA = 0x800007D2;
    internal const uint PDH_CSTATUS_VALID_DATA = 0x00000000;
    internal const uint PDH_CSTATUS_NEW_DATA = 0x00000001;

    [StructLayout(LayoutKind.Explicit)]
    internal struct PDH_FMT_COUNTERVALUE
    {
        [FieldOffset(0)] public uint CStatus;
        [FieldOffset(8)] public double doubleValue;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PDH_FMT_COUNTERVALUE_ITEM
    {
        public IntPtr szName;
        public PDH_FMT_COUNTERVALUE FmtValue;
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    internal static extern uint PdhOpenQuery(string? szDataSource, IntPtr dwUserData, out IntPtr phQuery);

    /// <summary>Usa nombres de contador en inglés: funciona en cualquier idioma de Windows.</summary>
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    internal static extern uint PdhAddEnglishCounter(IntPtr hQuery, string szFullCounterPath, IntPtr dwUserData, out IntPtr phCounter);

    [DllImport("pdh.dll")]
    internal static extern uint PdhCollectQueryData(IntPtr hQuery);

    [DllImport("pdh.dll")]
    internal static extern uint PdhGetFormattedCounterValue(IntPtr hCounter, uint dwFormat, out uint lpdwType, out PDH_FMT_COUNTERVALUE pValue);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    internal static extern uint PdhGetFormattedCounterArray(IntPtr hCounter, uint dwFormat, ref uint lpdwBufferSize, out uint lpdwItemCount, IntPtr itemBuffer);

    [DllImport("pdh.dll")]
    internal static extern uint PdhCloseQuery(IntPtr hQuery);
}
