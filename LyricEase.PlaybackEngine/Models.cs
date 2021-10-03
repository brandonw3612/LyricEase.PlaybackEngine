using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.Core;

namespace LyricEase.PlaybackEngine.Models
{
    public enum SoundQuality { Normal, High, VeryHigh, Lossless }

    public enum PlaybackMode { RepeatAll, RepeatOne, Shuffle }

    public enum NodeStatus { Unavailable, Preparing, Available }

    public interface ITrack
    {
        ulong TrackID { get; }
        string TitleString { get; }
        string ArtistString { get; }
        string AlbumString { get; }
        TimeSpan Duration { get; }
        string AlbumCoverImageUrl { get; }
        Task<MediaSource> GetAudioMediaSource(SoundQuality soundQuality);
    }

    public sealed class PlaybackStatusChangedEventArgs
    {
        public bool IsPlaying { get; set; }
    }

    public sealed class PlaybackPositionChangedEventArgs
    {
        public TimeSpan Current { get; set; }
        public TimeSpan Total { get; set; }
    }

    public sealed class CurrentlyPlayingItemChangedEventArgs
    {
        public ITrack CurrentTrack { get; set; }
    }

    public sealed class PlaybackErrorEventArgs
    {
        public string Message { get; set; }
    }
}
