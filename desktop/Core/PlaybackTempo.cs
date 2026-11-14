using System.Globalization;
using System.Text.Json.Nodes;
namespace WirePulse;

public static class PlaybackTempo {
 public static double Valid(double value)=>double.IsFinite(value)&&value>=20&&value<=400?value:0;
 public static double Read(JsonNode? info){if(info is not JsonObject obj)return 0;foreach(var node in new[]{obj,obj["attributes"] as JsonObject,obj["audioFeatures"] as JsonObject,obj["audioAnalysis"] as JsonObject}){if(node==null)continue;foreach(string key in new[]{"bpm","tempo"})if(node[key] is JsonValue value){double number=0;if(value.TryGetValue<double>(out var numeric))number=numeric;else if(value.TryGetValue<string>(out var text))double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out number);if(Valid(number)>0)return number;}}return 0;}
 public static double FromCider(Track track)=>track.Provider=="Cider"&&track.Title.Length>0&&track.Playing&&(DateTimeOffset.UtcNow-track.Updated).TotalSeconds<6?Valid(track.Bpm):0;
 public static double Resolve(Track track,double detected){double bpm=FromCider(track);return bpm>0?bpm:detected;}
}
