using System.Runtime.InteropServices;

namespace PcToolkit.Windows.Native;

/// <summary>
/// Interop mínimo con DXCore (dxcore.dll, Windows 10 2004+). Firmas y GUID tomados de
/// dxcore_interface.h (microsoft/DirectX-Headers, licencia MIT). Solo se declaran los métodos
/// que se usan, respetando el orden de la tabla virtual hasta el último método llamado.
/// </summary>
internal static class DxCore
{
    internal static readonly Guid IID_IDXCoreAdapterFactory = new("78ee5945-c36e-4b13-a669-005dd11c0f06");
    internal static readonly Guid IID_IDXCoreAdapterList = new("526c7776-40e9-459b-b711-f32ad76dfc28");
    internal static readonly Guid IID_IDXCoreAdapter = new("f0db4c7f-fe5a-42a2-bd62-f2a6cf6fc83e");
    internal static readonly Guid ATTRIBUTE_D3D11_GRAPHICS = new("8c47866b-7583-450d-f0f0-6bada895af4b");

    internal enum Property : uint
    {
        InstanceLuid = 0,
        DriverVersion = 1,
        DriverDescription = 2,
        HardwareID = 3,
        DedicatedAdapterMemory = 7,
        DedicatedSystemMemory = 8,
        SharedSystemMemory = 9,
        IsHardware = 11,
        IsIntegrated = 12,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HardwareId
    {
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Luid
    {
        public uint LowPart;
        public int HighPart;
    }

    [DllImport("dxcore.dll", ExactSpelling = true)]
    internal static extern int DXCoreCreateAdapterFactory(in Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object factory);

    [ComImport, Guid("78ee5945-c36e-4b13-a669-005dd11c0f06"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDXCoreAdapterFactory
    {
        [PreserveSig]
        int CreateAdapterList(uint numAttributes, [In, MarshalAs(UnmanagedType.LPArray)] Guid[] filterAttributes, in Guid riid,
            [MarshalAs(UnmanagedType.IUnknown)] out object adapterList);
    }

    [ComImport, Guid("526c7776-40e9-459b-b711-f32ad76dfc28"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDXCoreAdapterList
    {
        [PreserveSig]
        int GetAdapter(uint index, in Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object adapter);

        [PreserveSig]
        uint GetAdapterCount();
    }

    [ComImport, Guid("f0db4c7f-fe5a-42a2-bd62-f2a6cf6fc83e"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDXCoreAdapter
    {
        [PreserveSig]
        [return: MarshalAs(UnmanagedType.U1)]
        bool IsValid();

        [PreserveSig]
        [return: MarshalAs(UnmanagedType.U1)]
        bool IsAttributeSupported(in Guid attribute);

        [PreserveSig]
        [return: MarshalAs(UnmanagedType.U1)]
        bool IsPropertySupported(Property property);

        [PreserveSig]
        int GetProperty(Property property, nuint bufferSize, IntPtr propertyData);

        [PreserveSig]
        int GetPropertySize(Property property, out nuint bufferSize);
    }
}
