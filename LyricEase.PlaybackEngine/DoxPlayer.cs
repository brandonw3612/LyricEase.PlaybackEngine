using LyricEase.PlaybackEngine.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LyricEase.PlaybackEngine
{
    internal sealed class DoxPlayer : IPlayer
    {
        public double Volume { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool IsDirectSwitchEnabled { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public SoundQuality SoundQuality { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public PlaybackMode PlaybackMode { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public bool? IsPlaying => throw new NotImplementedException();

        public object PlaybackSource => throw new NotImplementedException();

        public ITrack CurrentItem => throw new NotImplementedException();

        public bool IsPreviousItemAvailable => throw new NotImplementedException();

        public bool IsNextItemAvailable => throw new NotImplementedException();

        public List<ITrack> OriginalPlaybackList => throw new NotImplementedException();

        public Stack<ITrack> UpNextPlaybackStack => throw new NotImplementedException();

        public List<int> PlaybackOrder => throw new NotImplementedException();

        public List<ITrack> NextPlayingQueue => throw new NotImplementedException();

        public event EventHandler<PlaybackStatusChangedEventArgs> PlaybackStatusChanged;
        public event EventHandler<EventArgs> PlaybackModeChanged;
        public event EventHandler<PlaybackPositionChangedEventArgs> PlaybackPositionChanged;
        public event EventHandler<CurrentlyPlayingItemChangedEventArgs> CurrentlyPlayingItemChanged;
        public event EventHandler PlaybackQueueUpdated;
        public event EventHandler PlaybackEnded;
        public event EventHandler<PlaybackErrorEventArgs> PlaybackError;

        public void AddToUpNext(IEnumerable<ITrack> Items)
        {
            throw new NotImplementedException();
        }

        public void Next()
        {
            throw new NotImplementedException();
        }

        public void Pause()
        {
            throw new NotImplementedException();
        }

        public void Play()
        {
            throw new NotImplementedException();
        }

        public void PlayList(IEnumerable<ITrack> Items, ITrack StartingItem, object PlaybackSource)
        {
            throw new NotImplementedException();
        }

        public void PlayPause()
        {
            throw new NotImplementedException();
        }

        public void Previous()
        {
            throw new NotImplementedException();
        }

        public void RemoveItem(bool IsMainPlaybackQueue, int TargetItemIndex)
        {
            throw new NotImplementedException();
        }

        public void Seek(TimeSpan TargetPosition)
        {
            throw new NotImplementedException();
        }

        public void SkipToIndex(bool IsMainPlaybackQueue, int TargetItemIndex)
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }
    }
}
