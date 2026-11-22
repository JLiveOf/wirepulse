using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Markup;
namespace WirePulse.WindowsSettings;
public sealed class LocalizedTextConverter:IValueConverter
{
 public object Convert(object value,Type targetType,object parameter,string language)=>L10n.Text(value?.ToString());
 public object ConvertBack(object value,Type targetType,object parameter,string language)=>throw new NotSupportedException();
}
internal static class LocalizedChoices
{
 public static DataTemplate Template {get {Application.Current.Resources["WirePulseLocalizedText"]=new LocalizedTextConverter();return (DataTemplate)XamlReader.Load("<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><TextBlock Text='{Binding Converter={StaticResource WirePulseLocalizedText}}'/></DataTemplate>");}}
}
