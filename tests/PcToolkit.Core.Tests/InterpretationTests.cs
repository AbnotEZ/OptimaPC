using PcToolkit.Core.Interpretation;
using PcToolkit.Core.Model;

namespace PcToolkit.Core.Tests;

public sealed class ReadingTests
{
    [Fact]
    public void Missing_cannot_be_marked_available() =>
        Assert.Throws<ArgumentException>(() => Reading<int>.Missing("s", ValueStatus.Available));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromNullable_treats_empty_as_not_available(string? value)
    {
        var r = Reading<string>.FromNullable(value, "s");
        Assert.False(r.HasValue);
        Assert.Equal(ValueStatus.NotAvailable, r.Status);
    }

    [Fact]
    public void Map_preserves_missing_state()
    {
        var r = Reading<int>.Missing("s", ValueStatus.AccessDenied, "d").Map(v => v * 2);
        Assert.Equal(ValueStatus.AccessDenied, r.Status);
        Assert.Equal("d", r.Detail);
    }
}

public sealed class SeriesMathTests
{
    private static readonly DateTimeOffset T = DateTimeOffset.UnixEpoch;

    [Fact]
    public void Statistics_exclude_missing_and_invalid_samples()
    {
        var st = SeriesMath.Compute([Sample.Valid(T, 10), Sample.Missing(T), Sample.Valid(T, 30), Sample.Invalid(T), Sample.Valid(T, double.NaN)]);
        Assert.Equal(2, st.ValidCount);
        Assert.Equal(1, st.MissingCount);
        Assert.Equal(2, st.InvalidCount); // NaN cuenta como inválida
        Assert.Equal(10, st.Min);
        Assert.Equal(20, st.Mean);
        Assert.Equal(30, st.Max);
    }

    [Fact]
    public void No_valid_samples_gives_null_statistics_not_zero()
    {
        var st = SeriesMath.Compute([Sample.Missing(T), Sample.Invalid(T)]);
        Assert.Equal(0, st.ValidCount);
        Assert.Null(st.Min);
        Assert.Null(st.Mean);
        Assert.Null(st.P95);
    }

    [Fact]
    public void Percentile_uses_nearest_rank()
    {
        var values = Enumerable.Range(1, 20).Select(i => (double)i).ToList();
        Assert.Equal(19, SeriesMath.Percentile(values, 0.95));
        Assert.Equal(1, SeriesMath.Percentile(values, 0.01));
        Assert.Equal(20, SeriesMath.Percentile(values, 1));
        Assert.Equal(5, SeriesMath.Percentile([5.0], 0.95));
    }
}

public sealed class HardwareMapsTests
{
    [Theory]
    [InlineData(new[] { 10 }, null, FormFactor.Portable)]
    [InlineData(new[] { 9 }, 1, FormFactor.Portable)]       // el chasis prevalece
    [InlineData(new[] { 3 }, null, FormFactor.Desktop)]
    [InlineData(new[] { 35 }, null, FormFactor.Desktop)]     // Mini PC
    [InlineData(new[] { 9, 3 }, 2, FormFactor.Portable)]     // contradictorio → PCSystemType
    [InlineData(new[] { 2 }, null, FormFactor.Unknown)]      // "Unknown"
    [InlineData(new int[0], 2, FormFactor.Portable)]
    [InlineData(new int[0], null, FormFactor.Unknown)]
    public void Form_factor_classification(int[] chassis, int? pcSystemType, FormFactor expected)
    {
        var (form, evidence) = HardwareMaps.ClassifyFormFactor(chassis, pcSystemType);
        Assert.Equal(expected, form);
        Assert.False(string.IsNullOrEmpty(evidence));
    }

    [Theory]
    [InlineData(0x1A, "DDR4")]
    [InlineData(0x22, "DDR5")]
    [InlineData(0x23, "LPDDR5")]
    [InlineData(0, null)]
    [InlineData(2, null)]
    [InlineData(999, null)]
    public void Smbios_memory_types(int code, string? expected) => Assert.Equal(expected, HardwareMaps.SmbiosMemoryType(code));

    [Theory]
    [InlineData(3, StorageMedia.Hdd)]
    [InlineData(4, StorageMedia.Ssd)]
    [InlineData(0, StorageMedia.Unspecified)]
    [InlineData(42, StorageMedia.Unspecified)]
    public void Storage_media_is_never_guessed(int code, StorageMedia expected) => Assert.Equal(expected, HardwareMaps.StorageMediaFromMsft(code));

    [Fact]
    public void Driver_version_unpacks_four_16bit_fields() =>
        Assert.Equal("32.0.15.6094", HardwareMaps.FormatDriverVersion((32UL << 48) | (0UL << 32) | (15UL << 16) | 6094UL));

    [Theory]
    [InlineData(@"PCI\VEN_10DE&DEV_2504&SUBSYS_00000000&REV_A1\4&1", 0x10DEu, 0x2504u)]
    [InlineData(@"pci\ven_8086&dev_a7a0", 0x8086u, 0xA7A0u)]
    public void Pci_ids_are_parsed(string pnp, uint ven, uint dev) => Assert.Equal((ven, dev), HardwareMaps.ParsePciIds(pnp));

    [Theory]
    [InlineData(null)]
    [InlineData("ROOT\\BasicRender")]
    [InlineData("PCI\\VEN_ZZZZ&DEV_0000")]
    public void Invalid_pci_ids_return_null(string? pnp) => Assert.Null(HardwareMaps.ParsePciIds(pnp));

    [Fact]
    public void Cim_date_is_parsed() =>
        Assert.Equal(new DateOnly(2025, 3, 7), HardwareMaps.ParseCimDate("20250307000000.000000-000"));

    [Fact]
    public void Unknown_power_overlay_is_not_mapped() =>
        Assert.Null(HardwareMaps.PowerModeKey(Guid.NewGuid()));

    [Fact]
    public void Balanced_overlay_is_empty_guid() =>
        Assert.Equal("PowerMode.Balanced", HardwareMaps.PowerModeKey(Guid.Empty));
}

public sealed class StartupApprovedParserTests
{
    [Theory]
    [InlineData(null, StartupState.Enabled)]
    [InlineData(new byte[] { 0x02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, StartupState.Enabled)]
    [InlineData(new byte[] { 0x06, 0 }, StartupState.Enabled)]
    [InlineData(new byte[] { 0x03, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8 }, StartupState.Disabled)]
    [InlineData(new byte[] { 0x01 }, StartupState.Disabled)]
    [InlineData(new byte[] { 0x07 }, StartupState.Disabled)]
    [InlineData(new byte[] { 0x42 }, StartupState.Unknown)]
    [InlineData(new byte[0], StartupState.Unknown)]
    public void Parses_known_values_only(byte[]? value, StartupState expected) =>
        Assert.Equal(expected, StartupApprovedParser.Parse(value));
}

public sealed class GpuCounterAggregatorTests
{
    private const string A = "0x00000000_0x0000aaaa";
    private const string B = "0x00000000_0x0000bbbb";

    private static KeyValuePair<string, double> I(string name, double v) => new(name, v);

    [Fact]
    public void Sums_processes_per_engine_and_takes_busiest_engine()
    {
        var result = GpuCounterAggregator.UtilizationByAdapter(
        [
            I("pid_100_luid_0x00000000_0x0000AAAA_phys_0_eng_0_engtype_3D", 30),
            I("pid_200_luid_0x00000000_0x0000AAAA_phys_0_eng_0_engtype_3D", 25),
            I("pid_100_luid_0x00000000_0x0000AAAA_phys_0_eng_3_engtype_VideoDecode", 40),
        ]);
        Assert.Equal(55, result[A]); // 3D: 30 + 25 > VideoDecode: 40
    }

    [Fact]
    public void Separates_multiple_adapters()
    {
        var result = GpuCounterAggregator.UtilizationByAdapter(
        [
            I("pid_1_luid_0x00000000_0x0000AAAA_phys_0_eng_0_engtype_3D", 10),
            I("pid_1_luid_0x00000000_0x0000BBBB_phys_0_eng_0_engtype_3D", 90),
        ]);
        Assert.Equal(10, result[A]);
        Assert.Equal(90, result[B]);
    }

    [Fact]
    public void Caps_at_100_and_ignores_invalid_values()
    {
        var result = GpuCounterAggregator.UtilizationByAdapter(
        [
            I("pid_1_luid_0x00000000_0x0000AAAA_phys_0_eng_0_engtype_3D", 80),
            I("pid_2_luid_0x00000000_0x0000AAAA_phys_0_eng_0_engtype_3D", 70),
            I("pid_3_luid_0x00000000_0x0000AAAA_phys_0_eng_0_engtype_3D", double.NaN),
            I("garbage", 50),
        ]);
        Assert.Equal(100, result[A]);
        Assert.Single(result);
    }

    [Fact]
    public void Adapter_memory_is_summed_by_luid() =>
        Assert.Equal(300, GpuCounterAggregator.SumByAdapter([I("luid_0x00000000_0x0000AAAA_phys_0", 100), I("luid_0x00000000_0x0000AAAA_phys_1", 200)])[A]);

    [Fact]
    public void Luid_format_matches_counter_instances() =>
        Assert.Equal("0x00000000_0x0000aaaa", GpuCounterAggregator.FormatLuid(0, 0xAAAA));
}
