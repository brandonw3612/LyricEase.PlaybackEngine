using LyricEase.PlaybackEngine.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LyricEase.PlaybackEngine
{
    interface IPlayer
    {
        double Volume { get; set; }
        bool IsDirectSwitchEnabled { get; set; }
        SoundQuality SoundQuality { get; set; }
        PlaybackMode PlaybackMode { get; set; }

        bool? IsPlaying { get; }
        object PlaybackSource { get; }
        ITrack CurrentItem { get; }

        bool IsPreviousItemAvailable { get; }
        bool IsNextItemAvailable { get; }

        List<ITrack> OriginalPlaybackList { get; }
        Stack<ITrack> UpNextPlaybackStack { get; }
        List<int> PlaybackOrder { get; }
        List<ITrack> NextPlayingQueue { get; }
        
        void PlayList(IEnumerable<ITrack> Items, ITrack StartingItem, object PlaybackSource);
        void AddToUpNext(IEnumerable<ITrack> Items);
        void Previous();
        void Next();
        void Play();
        void Pause();
        void PlayPause();
        void Stop();
        void Seek(TimeSpan TargetPosition);
        void SkipToIndex(bool IsMainPlaybackQueue, int TargetItemIndex);
        void RemoveItem(bool IsMainPlaybackQueue, int TargetItemIndex);
        
        event EventHandler<PlaybackStatusChangedEventArgs> PlaybackStatusChanged;
        event EventHandler<EventArgs> PlaybackModeChanged;
        event EventHandler<PlaybackPositionChangedEventArgs> PlaybackPositionChanged;
        event EventHandler<CurrentlyPlayingItemChangedEventArgs> CurrentlyPlayingItemChanged;
        event EventHandler PlaybackQueueUpdated;
        event EventHandler PlaybackEnded;
        event EventHandler<PlaybackErrorEventArgs> PlaybackError;
    }
}
