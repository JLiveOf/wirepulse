using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Input;
namespace WirePulse;
public static class GradientEditor
{
 public static Control Create(Window owner,Func<GradientSpec> get,Action<GradientSpec> set,Action? begin=null){var rows=new StackPanel{Spacing=8};var enabled=new CheckBox{Content=L10n.Text("Activar gradiente"),IsChecked=get().Enabled};rows.Children.Add(enabled);var end=ColorDialog.Button(owner,get().End,value=>{begin?.Invoke();set(get() with{End=value});});rows.Children.Add(new TextBlock{Text=L10n.Text("Segundo color")});rows.Children.Add(end);var caption=new TextBlock{Text=L10n.Text("Dirección del gradiente")};rows.Children.Add(caption);var angle=new Slider{Minimum=0,Maximum=360,Value=(get().Angle+360)%360,SmallChange=1,TickFrequency=1};rows.Children.Add(angle);bool gesture=false;angle.AddHandler(InputElement.PointerPressedEvent,(_,_)=>{begin?.Invoke();gesture=true;},Avalonia.Interactivity.RoutingStrategies.Tunnel);angle.AddHandler(InputElement.PointerReleasedEvent,(_,_)=>gesture=false,Avalonia.Interactivity.RoutingStrategies.Tunnel);angle.PointerCaptureLost+=(_,_)=>gesture=false;angle.ValueChanged+=(_,_)=>{if(!gesture)begin?.Invoke();set(get() with{Angle=angle.Value});};enabled.IsCheckedChanged+=(_,_)=>{begin?.Invoke();set(get() with{Enabled=enabled.IsChecked==true});end.IsEnabled=angle.IsEnabled=enabled.IsChecked==true;};end.IsEnabled=angle.IsEnabled=get().Enabled;return new Expander{Header=L10n.Text("Gradiente"),Content=rows,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch};}
}
