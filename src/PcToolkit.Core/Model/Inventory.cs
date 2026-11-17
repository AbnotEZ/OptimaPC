using System.Text.Json.Serialization;

namespace PcToolkit.Core.Model;

public sealed record SystemInventory
{
    public required DateTimeOffset CapturedAt { get; init; }
    public required TimeSpan Duration { get; init; }
    public required OsInfo Os { get; init; }
    public required MachineInfo Machine { get; init; }
    public required CpuInfo Cpu { get; init; }
    public required Reading<IReadOnlyList<GpuInfo>> Gpus { get; init; }
    public required MemoryInfo Memory { get; init; }
    public required StorageInfo Storage { get; init; }
    public required PowerInfo Power { get; init; }
    public required Reading<IReadOnlyList<DisplayInfo>> Displays { get; init; }
    public required Reading<IReadOnlyList<StartupEntry>> Startup { get; init; }
    public IReadOnlyList<CollectorIssue> Issues { get; init; } = [];
}

public sealed record OsInfo(
    Reading<string> Name,
    Reading<string> Edition,
    Reading<string> DisplayVersion,
    Reading<int> Build,
    Reading<int> Revision,
    Reading<string> Architecture);

[JsonConverter(typeof(JsonStringEnumConverter<FormFactor>))]
public enum FormFactor { Desktop, Portable, Unknown }

public sealed record MachineInfo(
    Reading<string> Manufacturer,
    Reading<string> Model,
    Reading<FormFactor> FormFactor);

public sealed record CoreClassCount(int EfficiencyClass, int Cores);

public sealed record CpuInfo(
    Reading<string> Name,
    Reading<string> Manufacturer,
    Reading<int> PhysicalCores,
    Reading<int> LogicalProcessors,
    Reading<int> MaxClockMHz,
    Reading<IReadOnlyList<CoreClassCount>> CoreClasses);

[JsonConverter(typeof(JsonStringEnumConverter<GpuKind>))]
public enum GpuKind { Integrated, Dedicated, Software, Unknown }

public sealed record GpuInfo
{
    public required Reading<string> Name { get; init; }
    public required Reading<string> Vendor { get; init; }
    public required Reading<GpuKind> Kind { get; init; }
    /// <summary>Memoria de vídeo dedicada (VRAM propia). En GPU integradas suele ser una reserva pequeña.</summary>
    public required Reading<ulong> DedicatedVideoMemoryBytes { get; init; }
    /// <summary>Memoria del sistema que la GPU puede compartir. No es memoria de vídeo propia.</summary>
    public required Reading<ulong> SharedSystemMemoryBytes { get; init; }
    public required Reading<string> DriverVersion { get; init; }
    public required Reading<DateOnly> DriverDate { get; init; }
    /// <summary>Identificador local del adaptador (cambia entre reinicios). Se usa para asociar contadores de rendimiento.</summary>
    public string? Luid { get; init; }
}

public sealed record MemoryModule(
    Reading<string> Slot,
    Reading<ulong> CapacityBytes,
    Reading<string> Type,
    Reading<int> RatedSpeedMTs,
    Reading<int> ConfiguredSpeedMTs,
    Reading<string> Manufacturer,
    Reading<string> PartNumber);

public sealed record MemoryInfo(
    Reading<ulong> InstalledBytes,
    Reading<ulong> UsableBytes,
    Reading<ulong> AvailableBytes,
    Reading<IReadOnlyList<MemoryModule>> Modules);

[JsonConverter(typeof(JsonStringEnumConverter<StorageMedia>))]
public enum StorageMedia { Unspecified, Hdd, Ssd, Scm }

public sealed record PhysicalDiskInfo(
    int Number,
    Reading<string> Model,
    Reading<StorageMedia> Media,
    Reading<string> Bus,
    Reading<ulong> SizeBytes);

public sealed record VolumeInfo(
    string Root,
    string DriveType,
    Reading<string> FileSystem,
    Reading<ulong> TotalBytes,
    Reading<ulong> FreeBytes,
    Reading<int> DiskNumber);

public sealed record StorageInfo(
    Reading<IReadOnlyList<PhysicalDiskInfo>> Disks,
    Reading<IReadOnlyList<VolumeInfo>> Volumes);

[JsonConverter(typeof(JsonStringEnumConverter<PowerSource>))]
public enum PowerSource { AC, Battery, Unknown }

public sealed record PowerPlan(Guid Id, string Name);

public sealed record PowerInfo(
    Reading<PowerSource> Source,
    Reading<bool> HasBattery,
    Reading<int> BatteryPercent,
    Reading<bool> Charging,
    Reading<PowerPlan> ActivePlan,
    Reading<IReadOnlyList<PowerPlan>> AvailablePlans,
    Reading<string> PowerMode);

public sealed record DisplayInfo(
    string DeviceName,
    bool IsPrimary,
    Reading<string> MonitorName,
    Reading<int> Width,
    Reading<int> Height,
    Reading<int> RefreshRateHz,
    Reading<int> BitsPerPixel);

[JsonConverter(typeof(JsonStringEnumConverter<StartupLocation>))]
public enum StartupLocation { UserRun, MachineRun, MachineRun32, UserStartupFolder, CommonStartupFolder }

[JsonConverter(typeof(JsonStringEnumConverter<StartupState>))]
public enum StartupState { Enabled, Disabled, Unknown }

public sealed record StartupEntry(
    string Name,
    string Command,
    StartupLocation Location,
    Reading<StartupState> State)
{
    [JsonIgnore]
    public bool IsMachineWide => Location is StartupLocation.MachineRun or StartupLocation.MachineRun32 or StartupLocation.CommonStartupFolder;
}
