using System.Windows.Markup;
using PcToolkit.Core.Localization;

namespace PcToolkit.App;

/// <summary>Texto localizado en XAML: Text="{app:Tr Home.Title}". El idioma se fija al iniciar.</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TrExtension : MarkupExtension
{
    public TrExtension()
    {
    }

    public TrExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.T(Key);
}
