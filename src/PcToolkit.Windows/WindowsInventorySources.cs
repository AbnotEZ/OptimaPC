using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using PcToolkit.Core.Interpretation;
using PcToolkit.Core.Model;
using PcToolkit.Core.Ports;
using PcToolkit.Windows.Native;
using static PcToolkit.Windows.Native.NativeMethods;

namespace PcToolkit.Windows;

/// <summary>
/// Inventario real de Windows. SOLO LECTURA: no escribe en el registro ni cambia ninguna configuración.
/// No recopila números de serie, claves de producto ni identificadores de hardware únicos.
/// </summary>
public sealed class WindowsInventorySources : IInventorySources
{
    private const string CurrentVersionKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

    // Valores de relleno que algunos firmwares devuelven en lugar del fabricante o modelo real.
    private static readonly HashSet<string> FirmwarePlaceholders = new(StringComparer.OrdinalIgnoreCase)
    {
        "To Be Filled By O.E.M.", "System manufacturer", "System Product Name", "Default string", "O.E.M.", "OEM", "Not Applicable", "None",
    };

    // ---------------- Sistema operativo ----------------

    public OsInfo ReadOs()
    {
        Reading<string> caption;
        Reading<int> build;
        try
        {
            var row = Wmi.Query(Wmi.Cimv2, "Win32_OperatingSystem", "Caption", "BuildNumber").FirstOrDefault();
            caption = Reading<string>.FromNullable(row?.Str("Caption"), Wmi.Source("Win32_OperatingSystem", "Caption"));
            build = row?.Long("BuildNumber") is long b
                ? Reading<int>.Of((int)b, Wmi.Source("Win32_OperatingSystem", "BuildNumber"))
                : Reading<int>.Missing(Wmi.Source("Win32_OperatingSystem", "BuildNumber"));
        }
        catch (Exception ex)
        {
            caption = Reading<string>.Missing(Wmi.Source("Win32_OperatingSystem", "Caption"), Wmi.StatusOf(ex), ex.Message);
            build = Reading<int>.Missing(Wmi.Source("Win32_OperatingSystem", "BuildNumber"), Wmi.StatusOf(ex), ex.Message);
        }

        const string reg = @"Registry HKLM\" + CurrentVersionKey;
        using var key = Registry.LocalMachine.OpenSubKey(CurrentVersionKey, writable: false);
        var edition = Reading<string>.FromNullable(key?.GetValue("EditionID") as string, reg + ":EditionID");
        var display = Reading<string>.FromNullable(key?.GetValue("DisplayVersion") as string, reg + ":DisplayVersion");
        var ubr = key?.GetValue("UBR") is int u ? Reading<int>.Of(u, reg + ":UBR") : Reading<int>.Missing(reg + ":UBR");

        var arch = Reading<string>.Of(RuntimeInformation.OSArchitecture.ToString(), "RuntimeInformation.OSArchitecture");
        return new OsInfo(caption, edition, display, build, ubr, arch);
    }

    // ---------------- Equipo ----------------

    public MachineInfo ReadMachine()
    {
        const string cs = "Win32_ComputerSystem";
        Reading<string> manufacturer, model;
        int? pcSystemType = null;
        try
        {
            var row = Wmi.Query(Wmi.Cimv2, cs, "Manufacturer", "Model", "PCSystemType").FirstOrDefault();
            manufacturer = NotPlaceholder(row?.Str("Manufacturer"), Wmi.Source(cs, "Manufacturer"));
            model = NotPlaceholder(row?.Str("Model"), Wmi.Source(cs, "Model"));
            pcSystemType = (int?)row?.Long("PCSystemType");
        }
        catch (Exception ex)
        {
            manufacturer = Reading<string>.Missing(Wmi.Source(cs, "Manufacturer"), Wmi.StatusOf(ex), ex.Message);
            model = Reading<string>.Missing(Wmi.Source(cs, "Model"), Wmi.StatusOf(ex), ex.Message);
        }

        IReadOnlyList<int>? chassis = null;
        try
        {
            chassis = Wmi.Query(Wmi.Cimv2, "Win32_SystemEnclosure", "ChassisTypes").FirstOrDefault()?.IntArray("ChassisTypes");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Sin chasis: la clasificación usará PCSystemType o quedará como desconocida.
        }

        var (form, evidence) = HardwareMaps.ClassifyFormFactor(chassis, pcSystemType);
        const string formSource = "WMI Win32_SystemEnclosure.ChassisTypes / Win32_ComputerSystem.PCSystemType";
        var formReading = form == FormFactor.Unknown
            ? Reading<FormFactor>.Missing(formSource, ValueStatus.NotAvailable, evidence)
            : Reading<FormFactor>.Of(form, formSource, evidence);
        return new MachineInfo(manufacturer, model, formReading);
    }

    private static Reading<string> NotPlaceholder(string? value, string source) =>
        value is not null && FirmwarePlaceholders.Contains(value)
            ? Reading<string>.Missing(source, ValueStatus.NotAvailable, $"firmware placeholder: \"{value}\"")
            : Reading<string>.FromNullable(value, source);

    // ---------------- CPU ----------------

    public CpuInfo ReadCpu()
    {
        const string cls = "Win32_Processor";
        Reading<string> name, maker;
        Reading<int> cores, logical, clock;
        try
        {
            var rows = Wmi.Query(Wmi.Cimv2, cls, "Name", "Manufacturer", "NumberOfCores", "NumberOfLogicalProcessors", "MaxClockSpeed");
            var first = rows.FirstOrDefault();
            name = Reading<string>.FromNullable(first?.Str("Name"), Wmi.Source(cls, "Name"),
                rows.Count > 1 ? $"sockets={rows.Count}" : null);
            maker = Reading<string>.FromNullable(first?.Str("Manufacturer"), Wmi.Source(cls, "Manufacturer"));
            cores = SumInt(rows, "NumberOfCores", Wmi.Source(cls, "NumberOfCores"));
            logical = SumInt(rows, "NumberOfLogicalProcessors", Wmi.Source(cls, "NumberOfLogicalProcessors"));
            clock = first?.Long("MaxClockSpeed") is long mhz and > 0
                ? Reading<int>.Of((int)mhz, Wmi.Source(cls, "MaxClockSpeed"), "base/rated clock reported by firmware")
                : Reading<int>.Missing(Wmi.Source(cls, "MaxClockSpeed"));
        }
        catch (Exception ex)
        {
            var st = Wmi.StatusOf(ex);
            name = Reading<string>.Missing(Wmi.Source(cls, "Name"), st, ex.Message);
            maker = Reading<string>.Missing(Wmi.Source(cls, "Manufacturer"), st, ex.Message);
            cores = Reading<int>.Missing(Wmi.Source(cls, "NumberOfCores"), st, ex.Message);
            clock = Reading<int>.Missing(Wmi.Source(cls, "MaxClockSpeed"), st, ex.Message);
            logical = Reading<int>.Of(Environment.ProcessorCount, "Environment.ProcessorCount");
        }

        return new CpuInfo(name, maker, cores, logical, clock, ReadCoreClasses());
    }

    private static Reading<int> SumInt(List<Dictionary<string, object?>> rows, string key, string source)
    {
        var values = rows.Select(r => r.Long(key)).ToList();
        return values.Count > 0 && values.All(v => v is > 0)
            ? Reading<int>.Of((int)values.Sum(v => v!.Value), source)
            : Reading<int>.Missing(source);
    }

    private static Reading<IReadOnlyList<CoreClassCount>> ReadCoreClasses()
    {
        const string source = "GetLogicalProcessorInformationEx(RelationProcessorCore).EfficiencyClass";
        uint length = 0;
        GetLogicalProcessorInformationEx(RelationProcessorCore, IntPtr.Zero, ref length);
        if (length == 0)
        {
            return Reading<IReadOnlyList<CoreClassCount>>.Missing(source, ValueStatus.Failed, $"Win32 error {Marshal.GetLastWin32Error()}");
        }
        var buffer = Marshal.AllocHGlobal((int)length);
        try
        {
            if (!GetLogicalProcessorInformationEx(RelationProcessorCore, buffer, ref length))
            {
                return Reading<IReadOnlyList<CoreClassCount>>.Missing(source, ValueStatus.Failed, $"Win32 error {Marshal.GetLastWin32Error()}");
            }
            var counts = new Dictionary<int, int>();
            var offset = 0;
            while (offset < length)
            {
                var ptr = buffer + offset;
                var size = Marshal.ReadInt32(ptr, 4);
                // SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX: Relationship(4) Size(4) PROCESSOR_RELATIONSHIP{Flags(1) EfficiencyClass(1) ...}
                var efficiencyClass = Marshal.ReadByte(ptr, 9);
                counts[efficiencyClass] = counts.GetValueOrDefault(efficiencyClass) + 1;
                if (size <= 0)
                {
                    break;
                }
                offset += size;
            }
            return Reading<IReadOnlyList<CoreClassCount>>.Of(
                counts.OrderBy(kv => kv.Key).Select(kv => new CoreClassCount(kv.Key, kv.Value)).ToList(), source);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    // ---------------- GPU ----------------

    public Reading<IReadOnlyList<GpuInfo>> ReadGpus()
    {
        var wmiRows = TryWmiVideoControllers();
        try
        {
            return Reading<IReadOnlyList<GpuInfo>>.Of(ReadGpusDxCore(wmiRows), "DXCore (dxcore.dll)");
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or COMException or InvalidCastException)
        {
            // Sin DXCore (Windows antiguo o entorno sin GPU): solo nombres y controlador desde WMI.
            if (wmiRows is null)
            {
                return Reading<IReadOnlyList<GpuInfo>>.Missing("DXCore / WMI Win32_VideoController", ValueStatus.Failed, ex.Message);
            }
            return Reading<IReadOnlyList<GpuInfo>>.Of(wmiRows.Select(GpuFromWmiOnly).ToList(), "WMI Win32_VideoController", $"DXCore unavailable: {ex.Message}");
        }
    }

    private static List<Dictionary<string, object?>>? TryWmiVideoControllers()
    {
        try
        {
            return Wmi.Query(Wmi.Cimv2, "Win32_VideoController", "Name", "PNPDeviceID", "DriverVersion", "DriverDate");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return null;
        }
    }

    private static GpuInfo GpuFromWmiOnly(Dictionary<string, object?> row)
    {
        const string cls = "Win32_VideoController";
        var ids = HardwareMaps.ParsePciIds(row.Str("PNPDeviceID"));
        var vendor = ids is { } i ? HardwareMaps.GpuVendorName(i.Vendor) : null;
        return new GpuInfo
        {
            Name = Reading<string>.FromNullable(row.Str("Name"), Wmi.Source(cls, "Name")),
            Vendor = Reading<string>.FromNullable(vendor, Wmi.Source(cls, "PNPDeviceID")),
            Kind = Reading<GpuKind>.Missing("DXCore IsIntegrated", ValueStatus.NotSupported),
            // AdapterRAM de WMI es de 32 bits (máximo 4 GiB) y no distingue memoria dedicada; no se usa.
            DedicatedVideoMemoryBytes = Reading<ulong>.Missing("DXCore DedicatedAdapterMemory", ValueStatus.NotSupported, "WMI AdapterRAM is limited to 4 GiB and is not used"),
            SharedSystemMemoryBytes = Reading<ulong>.Missing("DXCore SharedSystemMemory", ValueStatus.NotSupported),
            DriverVersion = Reading<string>.FromNullable(row.Str("DriverVersion"), Wmi.Source(cls, "DriverVersion")),
            DriverDate = HardwareMaps.ParseCimDate(row.Str("DriverDate")) is DateOnly d
                ? Reading<DateOnly>.Of(d, Wmi.Source(cls, "DriverDate"))
                : Reading<DateOnly>.Missing(Wmi.Source(cls, "DriverDate")),
        };
    }

    private static List<GpuInfo> ReadGpusDxCore(List<Dictionary<string, object?>>? wmiRows)
    {
        Marshal.ThrowExceptionForHR(DxCore.DXCoreCreateAdapterFactory(DxCore.IID_IDXCoreAdapterFactory, out var factoryObj));
        var factory = (DxCore.IDXCoreAdapterFactory)factoryObj;
        Marshal.ThrowExceptionForHR(factory.CreateAdapterList(1, [DxCore.ATTRIBUTE_D3D11_GRAPHICS], DxCore.IID_IDXCoreAdapterList, out var listObj));
        var list = (DxCore.IDXCoreAdapterList)listObj;

        var result = new List<GpuInfo>();
        var count = list.GetAdapterCount();
        for (uint i = 0; i < count; i++)
        {
            if (list.GetAdapter(i, DxCore.IID_IDXCoreAdapter, out var adapterObj) < 0)
            {
                continue;
            }
            var adapter = (DxCore.IDXCoreAdapter)adapterObj;
            try
            {
                if (!adapter.IsValid())
                {
                    continue;
                }
                result.Add(DescribeAdapter(adapter, wmiRows));
            }
            finally
            {
                Marshal.FinalReleaseComObject(adapter);
            }
        }
        Marshal.FinalReleaseComObject(list);
        Marshal.FinalReleaseComObject(factory);
        return result;
    }

    private static GpuInfo DescribeAdapter(DxCore.IDXCoreAdapter adapter, List<Dictionary<string, object?>>? wmiRows)
    {
        static string Src(DxCore.Property p) => $"DXCore {p}";

        var name = GetString(adapter, DxCore.Property.DriverDescription);
        var hw = GetStruct<DxCore.HardwareId>(adapter, DxCore.Property.HardwareID);
        var isHardware = GetBool(adapter, DxCore.Property.IsHardware);
        var isIntegrated = GetBool(adapter, DxCore.Property.IsIntegrated);
        var luid = GetStruct<DxCore.Luid>(adapter, DxCore.Property.InstanceLuid);

        Reading<GpuKind> kind = isHardware == false
            ? Reading<GpuKind>.Of(GpuKind.Software, Src(DxCore.Property.IsHardware))
            : isIntegrated is bool integrated
                ? Reading<GpuKind>.Of(integrated ? GpuKind.Integrated : GpuKind.Dedicated, Src(DxCore.Property.IsIntegrated))
                : Reading<GpuKind>.Missing(Src(DxCore.Property.IsIntegrated), ValueStatus.NotSupported);

        Reading<ulong> U64(DxCore.Property p) =>
            GetStruct<ulong>(adapter, p) is ulong v ? Reading<ulong>.Of(v, Src(p)) : Reading<ulong>.Missing(Src(p), ValueStatus.NotSupported);

        var driver = GetStruct<ulong>(adapter, DxCore.Property.DriverVersion);
        var wmi = hw is { } h ? wmiRows?.FirstOrDefault(r => HardwareMaps.ParsePciIds(r.Str("PNPDeviceID")) is { } ids && ids.Vendor == h.VendorId && ids.Device == h.DeviceId) : null;
        var driverDate = HardwareMaps.ParseCimDate(wmi?.Str("DriverDate"));

        return new GpuInfo
        {
            Name = Reading<string>.FromNullable(name, Src(DxCore.Property.DriverDescription)),
            Vendor = Reading<string>.FromNullable(hw is { } hv ? HardwareMaps.GpuVendorName(hv.VendorId) ?? $"PCI 0x{hv.VendorId:X4}" : null, Src(DxCore.Property.HardwareID)),
            Kind = kind,
            DedicatedVideoMemoryBytes = U64(DxCore.Property.DedicatedAdapterMemory),
            SharedSystemMemoryBytes = U64(DxCore.Property.SharedSystemMemory),
            DriverVersion = driver is ulong dv
                ? Reading<string>.Of(HardwareMaps.FormatDriverVersion(dv), Src(DxCore.Property.DriverVersion))
                : Reading<string>.Missing(Src(DxCore.Property.DriverVersion), ValueStatus.NotSupported),
            DriverDate = driverDate is DateOnly d
                ? Reading<DateOnly>.Of(d, Wmi.Source("Win32_VideoController", "DriverDate"))
                : Reading<DateOnly>.Missing(Wmi.Source("Win32_VideoController", "DriverDate")),
            Luid = luid is { } l ? GpuCounterAggregator.FormatLuid(l.HighPart, l.LowPart) : null,
        };
    }

    private static T? GetStruct<T>(DxCore.IDXCoreAdapter adapter, DxCore.Property property) where T : struct
    {
        if (!adapter.IsPropertySupported(property))
        {
            return null;
        }
        var size = Marshal.SizeOf<T>();
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            return adapter.GetProperty(property, (nuint)size, buffer) >= 0 ? Marshal.PtrToStructure<T>(buffer) : null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static bool? GetBool(DxCore.IDXCoreAdapter adapter, DxCore.Property property) =>
        GetStruct<byte>(adapter, property) is byte b ? b != 0 : null;

    private static string? GetString(DxCore.IDXCoreAdapter adapter, DxCore.Property property)
    {
        if (!adapter.IsPropertySupported(property) || adapter.GetPropertySize(property, out var size) < 0 || size == 0 || size > 4096)
        {
            return null;
        }
        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            return adapter.GetProperty(property, size, buffer) >= 0 ? Marshal.PtrToStringUTF8(buffer)?.Trim() : null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    // ---------------- Memoria ----------------

    public MemoryInfo ReadMemory()
    {
        var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        var hasStatus = GlobalMemoryStatusEx(ref status);
        var usable = hasStatus ? Reading<ulong>.Of(status.ullTotalPhys, "GlobalMemoryStatusEx.ullTotalPhys") : Reading<ulong>.Missing("GlobalMemoryStatusEx", ValueStatus.Failed);
        var available = hasStatus ? Reading<ulong>.Of(status.ullAvailPhys, "GlobalMemoryStatusEx.ullAvailPhys") : Reading<ulong>.Missing("GlobalMemoryStatusEx", ValueStatus.Failed);
        var installed = GetPhysicallyInstalledSystemMemory(out var kb)
            ? Reading<ulong>.Of(kb * 1024, "GetPhysicallyInstalledSystemMemory (SMBIOS)")
            : Reading<ulong>.Missing("GetPhysicallyInstalledSystemMemory (SMBIOS)", ValueStatus.NotAvailable, $"Win32 error {Marshal.GetLastWin32Error()}");

        const string cls = "Win32_PhysicalMemory";
        Reading<IReadOnlyList<MemoryModule>> modules;
        try
        {
            var rows = Wmi.Query(Wmi.Cimv2, cls, "DeviceLocator", "Capacity", "SMBIOSMemoryType", "Speed", "ConfiguredClockSpeed", "Manufacturer", "PartNumber");
            modules = Reading<IReadOnlyList<MemoryModule>>.Of(rows.Select(r =>
            {
                var type = r.Long("SMBIOSMemoryType") is long t ? HardwareMaps.SmbiosMemoryType((int)t) : null;
                return new MemoryModule(
                    Reading<string>.FromNullable(r.Str("DeviceLocator"), Wmi.Source(cls, "DeviceLocator")),
                    r.Long("Capacity") is long cap and > 0 ? Reading<ulong>.Of((ulong)cap, Wmi.Source(cls, "Capacity")) : Reading<ulong>.Missing(Wmi.Source(cls, "Capacity")),
                    Reading<string>.FromNullable(type, Wmi.Source(cls, "SMBIOSMemoryType"), r.Long("SMBIOSMemoryType") is long code ? $"code={code}" : null),
                    PositiveInt(r, "Speed", Wmi.Source(cls, "Speed")),
                    PositiveInt(r, "ConfiguredClockSpeed", Wmi.Source(cls, "ConfiguredClockSpeed")),
                    NotPlaceholder(r.Str("Manufacturer"), Wmi.Source(cls, "Manufacturer")),
                    NotPlaceholder(r.Str("PartNumber"), Wmi.Source(cls, "PartNumber")));
            }).ToList(), Wmi.Source(cls, "*"));
        }
        catch (Exception ex)
        {
            modules = Reading<IReadOnlyList<MemoryModule>>.Missing(Wmi.Source(cls, "*"), Wmi.StatusOf(ex), ex.Message);
        }
        return new MemoryInfo(installed, usable, available, modules);
    }

    private static Reading<int> PositiveInt(Dictionary<string, object?> row, string key, string source) =>
        row.Long(key) is long v and > 0 ? Reading<int>.Of((int)v, source) : Reading<int>.Missing(source);

    // ---------------- Almacenamiento ----------------

    public StorageInfo ReadStorage()
    {
        const string pd = "MSFT_PhysicalDisk";
        Reading<IReadOnlyList<PhysicalDiskInfo>> disks;
        try
        {
            var rows = Wmi.Query(Wmi.Storage, pd, "DeviceId", "FriendlyName", "MediaType", "BusType", "Size");
            disks = Reading<IReadOnlyList<PhysicalDiskInfo>>.Of(rows
                .Select(r =>
                {
                    var media = r.Long("MediaType") is long m ? HardwareMaps.StorageMediaFromMsft((int)m) : StorageMedia.Unspecified;
                    var bus = r.Long("BusType") is long b ? HardwareMaps.StorageBusFromMsft((int)b) : null;
                    return new PhysicalDiskInfo(
                        int.TryParse(r.Str("DeviceId"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : -1,
                        Reading<string>.FromNullable(r.Str("FriendlyName"), Wmi.Source(pd, "FriendlyName")),
                        Reading<StorageMedia>.Of(media, Wmi.Source(pd, "MediaType"), media == StorageMedia.Unspecified ? "MediaType=Unspecified: not inferred" : null),
                        Reading<string>.FromNullable(bus, Wmi.Source(pd, "BusType")),
                        r.Long("Size") is long s and > 0 ? Reading<ulong>.Of((ulong)s, Wmi.Source(pd, "Size")) : Reading<ulong>.Missing(Wmi.Source(pd, "Size")));
                })
                .OrderBy(d => d.Number).ToList(), Wmi.Source(pd, "*"));
        }
        catch (Exception ex)
        {
            disks = Reading<IReadOnlyList<PhysicalDiskInfo>>.Missing(Wmi.Source(pd, "*"), Wmi.StatusOf(ex), ex.Message);
        }

        // Letra de unidad → número de disco físico (una partición pertenece a un único disco).
        var letterToDisk = new Dictionary<char, int>();
        try
        {
            foreach (var r in Wmi.Query(Wmi.Storage, "MSFT_Partition", "DiskNumber", "DriveLetter"))
            {
                var letter = r.Str("DriveLetter");
                if (letter is { Length: 1 } && char.IsLetter(letter[0]) && r.Long("DiskNumber") is long dn)
                {
                    letterToDisk[char.ToUpperInvariant(letter[0])] = (int)dn;
                }
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Sin asociación volumen-disco: se mostrará como desconocida.
        }

        Reading<IReadOnlyList<VolumeInfo>> volumes;
        try
        {
            var list = new List<VolumeInfo>();
            foreach (var d in DriveInfo.GetDrives().Where(d => d.DriveType is DriveType.Fixed or DriveType.Removable))
            {
                const string src = "System.IO.DriveInfo";
                var letter = char.ToUpperInvariant(d.Name[0]);
                var disk = letterToDisk.TryGetValue(letter, out var num) ? Reading<int>.Of(num, "WMI MSFT_Partition.DiskNumber") : Reading<int>.Missing("WMI MSFT_Partition.DiskNumber");
                if (!d.IsReady)
                {
                    list.Add(new VolumeInfo(d.Name, d.DriveType.ToString(), Reading<string>.Missing(src), Reading<ulong>.Missing(src, ValueStatus.NotAvailable, "not ready"), Reading<ulong>.Missing(src), disk));
                    continue;
                }
                // La etiqueta del volumen no se recopila: puede contener nombres personales.
                list.Add(new VolumeInfo(d.Name, d.DriveType.ToString(),
                    Reading<string>.FromNullable(d.DriveFormat, src + ".DriveFormat"),
                    Reading<ulong>.Of((ulong)d.TotalSize, src + ".TotalSize"),
                    Reading<ulong>.Of((ulong)d.AvailableFreeSpace, src + ".AvailableFreeSpace"),
                    disk));
            }
            volumes = Reading<IReadOnlyList<VolumeInfo>>.Of(list, "System.IO.DriveInfo");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            volumes = Reading<IReadOnlyList<VolumeInfo>>.Missing("System.IO.DriveInfo", ValueStatus.Failed, ex.Message);
        }

        return new StorageInfo(disks, volumes);
    }

    // ---------------- Energía ----------------

    public PowerInfo ReadPower()
    {
        const string sps = "GetSystemPowerStatus";
        Reading<PowerSource> source;
        Reading<bool> hasBattery, charging;
        Reading<int> percent;
        if (GetSystemPowerStatus(out var st))
        {
            source = st.ACLineStatus switch
            {
                1 => Reading<PowerSource>.Of(PowerSource.AC, sps + ".ACLineStatus"),
                0 => Reading<PowerSource>.Of(PowerSource.Battery, sps + ".ACLineStatus"),
                _ => Reading<PowerSource>.Missing(sps + ".ACLineStatus", ValueStatus.NotAvailable, $"ACLineStatus={st.ACLineStatus}"),
            };
            if (st.BatteryFlag == 255)
            {
                hasBattery = Reading<bool>.Missing(sps + ".BatteryFlag", ValueStatus.NotAvailable, "BatteryFlag=255 (unknown)");
                percent = Reading<int>.Missing(sps + ".BatteryLifePercent");
                charging = Reading<bool>.Missing(sps + ".BatteryFlag");
            }
            else
            {
                var present = (st.BatteryFlag & 128) == 0;
                hasBattery = Reading<bool>.Of(present, sps + ".BatteryFlag");
                percent = present && st.BatteryLifePercent <= 100
                    ? Reading<int>.Of(st.BatteryLifePercent, sps + ".BatteryLifePercent")
                    : Reading<int>.Missing(sps + ".BatteryLifePercent");
                charging = present ? Reading<bool>.Of((st.BatteryFlag & 8) != 0, sps + ".BatteryFlag") : Reading<bool>.Missing(sps + ".BatteryFlag");
            }
        }
        else
        {
            var err = $"Win32 error {Marshal.GetLastWin32Error()}";
            source = Reading<PowerSource>.Missing(sps, ValueStatus.Failed, err);
            hasBattery = Reading<bool>.Missing(sps, ValueStatus.Failed, err);
            percent = Reading<int>.Missing(sps, ValueStatus.Failed, err);
            charging = Reading<bool>.Missing(sps, ValueStatus.Failed, err);
        }

        return new PowerInfo(source, hasBattery, percent, charging, ReadActivePlan(), ReadPlans(), ReadPowerMode());
    }

    private static Reading<PowerPlan> ReadActivePlan()
    {
        const string src = "PowerGetActiveScheme + PowerReadFriendlyName";
        var rc = PowerGetActiveScheme(IntPtr.Zero, out var ptr);
        if (rc != 0 || ptr == IntPtr.Zero)
        {
            return Reading<PowerPlan>.Missing(src, ValueStatus.Failed, $"error {rc}");
        }
        try
        {
            var guid = Marshal.PtrToStructure<Guid>(ptr);
            return Reading<PowerPlan>.Of(new PowerPlan(guid, FriendlyName(guid) ?? guid.ToString("D")), src);
        }
        finally
        {
            LocalFree(ptr);
        }
    }

    private static Reading<IReadOnlyList<PowerPlan>> ReadPlans()
    {
        const string src = "PowerEnumerate(ACCESS_SCHEME)";
        var plans = new List<PowerPlan>();
        for (uint i = 0; i < 64; i++)
        {
            var guid = Guid.Empty;
            var size = (uint)Marshal.SizeOf<Guid>();
            var rc = PowerEnumerate(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, ACCESS_SCHEME, i, ref guid, ref size);
            if (rc == ERROR_NO_MORE_ITEMS)
            {
                break;
            }
            if (rc != 0)
            {
                return Reading<IReadOnlyList<PowerPlan>>.Missing(src, ValueStatus.Failed, $"error {rc}");
            }
            plans.Add(new PowerPlan(guid, FriendlyName(guid) ?? guid.ToString("D")));
        }
        return Reading<IReadOnlyList<PowerPlan>>.Of(plans, src);
    }

    private static string? FriendlyName(Guid scheme)
    {
        uint size = 0;
        PowerReadFriendlyName(IntPtr.Zero, ref scheme, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, ref size);
        if (size == 0 || size > 4096)
        {
            return null;
        }
        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            return PowerReadFriendlyName(IntPtr.Zero, ref scheme, IntPtr.Zero, IntPtr.Zero, buffer, ref size) == 0
                ? Marshal.PtrToStringUni(buffer)
                : null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static Reading<string> ReadPowerMode()
    {
        const string src = "PowerGetEffectiveOverlayScheme";
        try
        {
            var rc = PowerGetEffectiveOverlayScheme(out var overlay);
            if (rc != 0)
            {
                return Reading<string>.Missing(src, ValueStatus.NotAvailable, $"error {rc}");
            }
            // Si el GUID no es uno de los publicados, se muestra tal cual en lugar de adivinar.
            return Reading<string>.Of(HardwareMaps.PowerModeKey(overlay) ?? overlay.ToString("D"), src);
        }
        catch (EntryPointNotFoundException)
        {
            return Reading<string>.Missing(src, ValueStatus.NotSupported);
        }
    }

    // ---------------- Pantallas ----------------

    public Reading<IReadOnlyList<DisplayInfo>> ReadDisplays()
    {
        const string src = "EnumDisplayDevices + EnumDisplaySettings(ENUM_CURRENT_SETTINGS)";
        var list = new List<DisplayInfo>();
        for (uint i = 0; i < 32; i++)
        {
            var adapter = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
            if (!EnumDisplayDevices(null, i, ref adapter, 0))
            {
                break;
            }
            if ((adapter.StateFlags & DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) == 0)
            {
                continue;
            }
            var monitor = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
            var monitorName = EnumDisplayDevices(adapter.DeviceName, 0, ref monitor, 0) ? monitor.DeviceString : null;

            var mode = new DEVMODE { dmSize = (ushort)Marshal.SizeOf<DEVMODE>() };
            var hasMode = EnumDisplaySettings(adapter.DeviceName, ENUM_CURRENT_SETTINGS, ref mode);
            // dmDisplayFrequency 0 o 1 significa "valor predeterminado del hardware": no es una frecuencia real.
            Reading<int> I(uint v, string field, bool ok) =>
                hasMode && ok ? Reading<int>.Of((int)v, $"DEVMODE.{field}") : Reading<int>.Missing($"DEVMODE.{field}");
            list.Add(new DisplayInfo(
                adapter.DeviceName,
                (adapter.StateFlags & DISPLAY_DEVICE_PRIMARY_DEVICE) != 0,
                Reading<string>.FromNullable(monitorName, "EnumDisplayDevices(monitor).DeviceString"),
                I(mode.dmPelsWidth, nameof(mode.dmPelsWidth), mode.dmPelsWidth > 0),
                I(mode.dmPelsHeight, nameof(mode.dmPelsHeight), mode.dmPelsHeight > 0),
                I(mode.dmDisplayFrequency, nameof(mode.dmDisplayFrequency), mode.dmDisplayFrequency > 1),
                I(mode.dmBitsPerPel, nameof(mode.dmBitsPerPel), mode.dmBitsPerPel > 0)));
        }
        return Reading<IReadOnlyList<DisplayInfo>>.Of(list, src);
    }

    // ---------------- Programas de inicio ----------------

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved";

    public Reading<IReadOnlyList<StartupEntry>> ReadStartup()
    {
        var entries = new List<StartupEntry>();
        ReadRun(RegistryHive.CurrentUser, RegistryView.Default, StartupLocation.UserRun, "Run", entries);
        ReadRun(RegistryHive.LocalMachine, RegistryView.Registry64, StartupLocation.MachineRun, "Run", entries);
        ReadRun(RegistryHive.LocalMachine, RegistryView.Registry32, StartupLocation.MachineRun32, "Run32", entries);
        ReadFolder(Environment.SpecialFolder.Startup, RegistryHive.CurrentUser, StartupLocation.UserStartupFolder, entries);
        ReadFolder(Environment.SpecialFolder.CommonStartup, RegistryHive.LocalMachine, StartupLocation.CommonStartupFolder, entries);
        return Reading<IReadOnlyList<StartupEntry>>.Of(entries, "Registry Run keys + Startup folders",
            "Scheduled tasks, services and packaged (Store) app startup tasks are not listed in this version");
    }

    private static void ReadRun(RegistryHive hive, RegistryView view, StartupLocation location, string approvedSub, List<StartupEntry> into)
    {
        using var root = RegistryKey.OpenBaseKey(hive, view);
        using var run = root.OpenSubKey(RunKey, writable: false);
        if (run is null)
        {
            return;
        }
        using var approved = OpenApproved(hive, approvedSub);
        foreach (var name in run.GetValueNames().Where(n => !string.IsNullOrEmpty(n)))
        {
            var command = run.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string ?? "";
            into.Add(new StartupEntry(name, command, location, ApprovedState(approved, name)));
        }
    }

    private static void ReadFolder(Environment.SpecialFolder folder, RegistryHive hive, StartupLocation location, List<StartupEntry> into)
    {
        var path = Environment.GetFolderPath(folder);
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            return;
        }
        using var approved = OpenApproved(hive, "StartupFolder");
        var options = new EnumerationOptions { RecurseSubdirectories = false, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System, IgnoreInaccessible = true };
        foreach (var file in Directory.EnumerateFiles(path, "*", options))
        {
            var fileName = Path.GetFileName(file);
            into.Add(new StartupEntry(Path.GetFileNameWithoutExtension(file), file, location, ApprovedState(approved, fileName)));
        }
    }

    private static RegistryKey? OpenApproved(RegistryHive hive, string sub)
    {
        using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        return root.OpenSubKey($@"{ApprovedKey}\{sub}", writable: false);
    }

    private static Reading<StartupState> ApprovedState(RegistryKey? approved, string valueName)
    {
        var raw = approved?.GetValue(valueName) as byte[];
        var state = StartupApprovedParser.Parse(raw);
        var detail = raw is { Length: > 0 } ? $"StartupApproved[0]=0x{raw[0]:X2}" : "no StartupApproved value";
        return state == StartupState.Unknown
            ? Reading<StartupState>.Missing(StartupApprovedParser.Source, ValueStatus.NotAvailable, detail)
            : Reading<StartupState>.Of(state, StartupApprovedParser.Source, detail);
    }
}
