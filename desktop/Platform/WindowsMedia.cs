#if WINDOWS
using Windows.Media.Control;
using Windows.Storage.Streams;
namespace WirePulse;
public static class WindowsMedia {
 static GlobalSystemMediaTransportControlsSessionManager? manager;
 sealed record Artwork(byte[]? Bytes,DateTimeOffset ReadAt);
 static readonly Dictionary<string,Artwork> art=[];
 public static async Task<Track?> Read(string selected){
  manager??=await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();Track? paused=null;
  foreach(var session in manager.GetSessions()){
   var data=await session.TryGetMediaPropertiesAsync();if(string.IsNullOrWhiteSpace(data.Title))continue;var provider=MediaService.Provider(session.SourceAppUserModelId);
   if(selected!="Automático"&&!provider.Contains(selected,StringComparison.OrdinalIgnoreCase))continue;
   var timeline=session.GetTimelineProperties();bool playing=session.GetPlaybackInfo().PlaybackStatus==GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
   string key=string.Join('|',session.SourceAppUserModelId,data.Title,data.Artist,data.AlbumTitle);byte[]? bytes=null;
   if(art.TryGetValue(key,out var cached))bytes=cached.Bytes;
   if(bytes==null&&(cached==null||(DateTimeOffset.UtcNow-cached.ReadAt).TotalSeconds>=5)){
    try{if(data.Thumbnail!=null){using var image=await data.Thumbnail.OpenReadAsync();if(image.Size>0&&image.Size<=20*1024*1024){using var reader=new DataReader(image.GetInputStreamAt(0));await reader.LoadAsync((uint)image.Size);bytes=new byte[(int)image.Size];reader.ReadBytes(bytes);}}}catch{bytes=null;}
    if(art.Count>8)art.Clear();art[key]=new(bytes,DateTimeOffset.UtcNow);
   }
   double duration=Math.Max(0,(timeline.EndTime-timeline.StartTime).TotalSeconds),position=Math.Max(0,(timeline.Position-timeline.StartTime).TotalSeconds);
   // SMTC position may be unchanged between events; never reset elapsed time back every poll.
   if(playing&&timeline.LastUpdatedTime> DateTimeOffset.UnixEpoch)position+=Math.Max(0,(DateTimeOffset.UtcNow-timeline.LastUpdatedTime).TotalSeconds);
   var track=new Track(provider,data.Title,data.Artist,data.AlbumTitle,duration,Math.Min(duration,position),playing,ImageBytes:bytes);if(playing)return track;paused??=track;
  }return paused;
 }
}
#endif
