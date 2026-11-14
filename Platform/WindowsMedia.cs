#if WINDOWS
using Windows.Media.Control;
using Windows.Storage.Streams;
namespace WirePulse;
public static class WindowsMedia {
 static GlobalSystemMediaTransportControlsSessionManager? manager;
 public static async Task<Track?> Read(string selected){
  manager??=await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();Track? paused=null;
  foreach(var session in manager.GetSessions()){
   var data=await session.TryGetMediaPropertiesAsync();if(string.IsNullOrWhiteSpace(data.Title))continue;var provider=MediaService.Provider(session.SourceAppUserModelId);
   if(selected!="Automático"&&!provider.Contains(selected,StringComparison.OrdinalIgnoreCase))continue;
   var timeline=session.GetTimelineProperties();bool playing=session.GetPlaybackInfo().PlaybackStatus==GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;byte[]? bytes=null;
   if(data.Thumbnail!=null){using var image=await data.Thumbnail.OpenReadAsync();if(image.Size<=20*1024*1024){using var reader=new DataReader(image.GetInputStreamAt(0));await reader.LoadAsync((uint)image.Size);bytes=new byte[(int)image.Size];reader.ReadBytes(bytes);}}
   var track=new Track(provider,data.Title,data.Artist,data.AlbumTitle,Math.Max(0,(timeline.EndTime-timeline.StartTime).TotalSeconds),Math.Max(0,(timeline.Position-timeline.StartTime).TotalSeconds),playing,ImageBytes:bytes);if(playing)return track;paused??=track;
  }return paused;
 }
}
#endif
