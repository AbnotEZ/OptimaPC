using PcToolkit.Core.Model;

namespace PcToolkit.Core.Interpretation;

/// <summary>
/// Interpreta los valores de HKCU/HKLM\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved.
/// ADVERTENCIA: formato NO documentado por Microsoft (es el que usa el Administrador de tareas).
/// Solo se reconocen los valores observados habitualmente; cualquier otro se informa como desconocido.
/// </summary>
public static class StartupApprovedParser
{
    public const string Source = "Registry StartupApproved (undocumented format)";

    /// <param name="value">Contenido binario del valor, o null si no existe.</param>
    public static StartupState Parse(byte[]? value)
    {
        if (value is null)
        {
            // Sin entrada en StartupApproved: Windows trata el elemento como habilitado.
            return StartupState.Enabled;
        }
        if (value.Length == 0)
        {
            return StartupState.Unknown;
        }
        return value[0] switch
        {
            0x02 or 0x06 => StartupState.Enabled,
            0x01 or 0x03 or 0x07 => StartupState.Disabled,
            _ => StartupState.Unknown,
        };
    }
}
