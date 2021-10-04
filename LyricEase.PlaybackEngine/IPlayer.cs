using LyricEase.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LyricEase
{
    public interface IPlayer
    {
        double Volume { get; set; }
        bool IsDirectSwitchEnabled { get; set; }
        bool IsAudioCrossfadingEnabled { get; set; }
        int AudioCrossfadingLength { get; set; }
        SoundQuality SoundQuality { get; set; }
        PlaybackMode PlaybackMode { get; set; }

        bool? IsPlaying { get; }
        object PlaybackSource { get; }
        ITrack CurrentItem { get; }

        List<ITrack> OriginalPlaybackList { get; }
        List<ITrack> UpNextPlaybackList { get; }
        List<int> PlaybackOrder { get; }
        List<ITrack> OrderedPlaybackList { get; }
        
        void PlayCollection(IEnumerable<ITrack> Items, ITrack StartingItem, object PlaybackSource);
        void AddToUpNext(IEnumerable<ITrack> Items);
        void Previous();
        void Next();
        void Play();
        void Pause();
        void PlayPause();
        void Stop();
        void Seek(TimeSpan TargetPosition);
        void SkipTo(ITrack targetTrack);
        void RemoveTrack(ITrack targetTrack);
        
        event EventHandler<PlaybackStatusChangedEventArgs> PlaybackStatusChanged;
        event EventHandler<EventArgs> PlaybackModeChanged;
        event EventHandler<PlaybackPositionChangedEventArgs> PlaybackPositionChanged;
        event EventHandler<CurrentlyPlayingItemChangedEventArgs> CurrentlyPlayingItemChanged;
        event EventHandler PlaybackQueueUpdated;
        event EventHandler PlaybackEnded;
        event EventHandler<PlaybackErrorEventArgs> PlaybackError;
    }
}
