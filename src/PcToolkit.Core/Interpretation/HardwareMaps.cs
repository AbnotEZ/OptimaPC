using System.Globalization;
using PcToolkit.Core.Model;

namespace PcToolkit.Core.Interpretation;

/// <summary>
/// Traducciones de códigos de las APIs de Windows a valores del modelo.
/// Cada tabla cita su especificación. Un código desconocido nunca se adivina.
/// </summary>
public static class HardwareMaps
{
    /// <summary>Identificadores de fabricante PCI más comunes en GPU.</summary>
    public static string? GpuVendorName(uint vendorId) => vendorId switch
    {
        0x10DE => "NVIDIA",
        0x1002 or 0x1022 => "AMD",
        0x8086 => "Intel",
        0x1414 => "Microsoft",
        0x5143 or 0x4D4F4351 => "Qualcomm",
        _ => null,
    };

    /// <summary>SMBIOS 3.x, tabla "Memory Device — Type" (campo SMBIOSMemoryType de Win32_PhysicalMemory).</summary>
    public static string? SmbiosMemoryType(int code) => code switch
    {
        0x12 => "DDR",
        0x13 => "DDR2",
        0x14 => "DDR2 FB-DIMM",
        0x18 => "DDR3",
        0x1A => "DDR4",
        0x1B => "LPDDR",
        0x1C => "LPDDR2",
        0x1D => "LPDDR3",
        0x1E => "LPDDR4",
        0x1F => "Logical non-volatile device",
        0x20 => "HBM",
        0x21 => "HBM2",
        0x22 => "DDR5",
        0x23 => "LPDDR5",
        0x24 => "HBM3",
        _ => null, // 0 = desconocido, 2 = "Unknown", otros sin uso en PC actuales
    };

    /// <summary>MSFT_PhysicalDisk.MediaType (API de administración de almacenamiento de Windows).</summary>
    public static StorageMedia StorageMediaFromMsft(int code) => code switch
    {
        3 => StorageMedia.Hdd,
        4 => StorageMedia.Ssd,
        5 => StorageMedia.Scm,
        _ => StorageMedia.Unspecified,
    };

    /// <summary>MSFT_PhysicalDisk.BusType.</summary>
    public static string? StorageBusFromMsft(int code) => code switch
    {
        1 => "SCSI",
        2 => "ATAPI",
        3 => "ATA",
        4 => "IEEE 1394",
        5 => "SSA",
        6 => "Fibre Channel",
        7 => "USB",
        8 => "RAID",
        9 => "iSCSI",
        10 => "SAS",
        11 => "SATA",
        12 => "SD",
        13 => "MMC",
        15 => "File-backed virtual",
        16 => "Storage Spaces",
        17 => "NVMe",
        18 => "SCM",
        19 => "UFS",
        _ => null,
    };

    // SMBIOS "System Enclosure or Chassis Types" (Win32_SystemEnclosure.ChassisTypes).
    private static readonly HashSet<int> PortableChassis = [8, 9, 10, 11, 14, 30, 31, 32];
    private static readonly HashSet<int> DesktopChassis = [3, 4, 5, 6, 7, 13, 15, 16, 24, 35, 36];

    /// <summary>
    /// Clasifica el equipo como escritorio o portátil. Prioriza el tipo de chasis SMBIOS y usa
    /// Win32_ComputerSystem.PCSystemType (2 = Mobile) solo si el chasis no es concluyente.
    /// Devuelve también la evidencia utilizada.
    /// </summary>
    public static (FormFactor FormFactor, string Evidence) ClassifyFormFactor(IReadOnlyList<int>? chassisTypes, int? pcSystemType)
    {
        if (chassisTypes is { Count: > 0 })
        {
            var portable = chassisTypes.Where(PortableChassis.Contains).ToList();
            var desktop = chassisTypes.Where(DesktopChassis.Contains).ToList();
            if (portable.Count > 0 && desktop.Count == 0)
            {
                return (FormFactor.Portable, $"ChassisTypes={string.Join(",", chassisTypes)}");
            }
            if (desktop.Count > 0 && portable.Count == 0)
            {
                return (FormFactor.Desktop, $"ChassisTypes={string.Join(",", chassisTypes)}");
            }
        }

        return pcSystemType switch
        {
            2 => (FormFactor.Portable, $"PCSystemType={pcSystemType}"),
            1 or 3 or 4 or 5 or 7 => (FormFactor.Desktop, $"PCSystemType={pcSystemType}"),
            _ => (FormFactor.Unknown, $"ChassisTypes={(chassisTypes is null ? "n/a" : string.Join(",", chassisTypes))}; PCSystemType={pcSystemType?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}"),
        };
    }

    /// <summary>Versión de controlador empaquetada en 64 bits (cuatro campos de 16 bits), formato de DXCore/DXGI.</summary>
    public static string FormatDriverVersion(ulong packed) =>
        string.Create(CultureInfo.InvariantCulture,
            $"{(packed >> 48) & 0xFFFF}.{(packed >> 32) & 0xFFFF}.{(packed >> 16) & 0xFFFF}.{packed & 0xFFFF}");

    /// <summary>
    /// GUID de superposición del modo de energía de Windows (Configuración &gt; Energía).
    /// Devuelve una clave de texto localizable o null si el GUID no es uno de los publicados.
    /// </summary>
    public static string? PowerModeKey(Guid overlay) => overlay.ToString("D") switch
    {
        "961cc777-2547-4f9d-8174-7d86181b8a7a" => "PowerMode.BestEfficiency",
        "00000000-0000-0000-0000-000000000000" => "PowerMode.Balanced",
        "3af9b8d9-7c97-431d-ad78-34a8bfea439f" => "PowerMode.BetterPerformance",
        "ded574b5-45a0-4f42-8737-46345c09c238" => "PowerMode.BestPerformance",
        _ => null,
    };

    /// <summary>Extrae VEN y DEV de un PNPDeviceID como "PCI\VEN_10DE&amp;DEV_2504&amp;...".</summary>
    public static (uint Vendor, uint Device)? ParsePciIds(string? pnpDeviceId)
    {
        if (string.IsNullOrEmpty(pnpDeviceId))
        {
            return null;
        }
        uint? Find(string tag)
        {
            var i = pnpDeviceId.IndexOf(tag, StringComparison.OrdinalIgnoreCase);
            if (i < 0 || i + tag.Length + 4 > pnpDeviceId.Length)
            {
                return null;
            }
            return uint.TryParse(pnpDeviceId.AsSpan(i + tag.Length, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v) ? v : null;
        }
        var ven = Find("VEN_");
        var dev = Find("DEV_");
        return ven is null || dev is null ? null : (ven.Value, dev.Value);
    }

    /// <summary>Convierte una fecha CIM_DATETIME ("yyyyMMddHHmmss.ffffff+zzz") en fecha.</summary>
    public static DateOnly? ParseCimDate(string? cim)
    {
        if (cim is null || cim.Length < 8)
        {
            return null;
        }
        return DateOnly.TryParseExact(cim[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    }
}
