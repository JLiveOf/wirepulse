using System.Globalization;
namespace WirePulse;
public static class ClockText {
 public static string Format(DateTime time,bool hours24,CultureInfo? culture=null)=>time.ToString(hours24?"HH:mm":"hh:mm tt",culture??CultureInfo.CurrentCulture);
}
