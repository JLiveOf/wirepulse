using Avalonia.Controls;
using Avalonia.Controls.Templates;
namespace WirePulse;
internal static class LocalizedChoices
{
    public static readonly IDataTemplate Template=new FuncDataTemplate<string>((value,_)=>new TextBlock{Text=L10n.Text(value)});
}
