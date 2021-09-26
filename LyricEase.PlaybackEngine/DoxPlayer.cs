using LyricEase.PlaybackEngine.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Windows.Media.Audio;
using Windows.Media.Core;

namespace LyricEase.PlaybackEngine
{
    internal sealed class DoxPlayer : IPlayer
    {
        private AudioGraph audioGraph;
        private AudioDeviceOutputNode outputNode;
        private List<TrackNode> trackNodes;

        private TrackNode previousTrackNode;
        private TrackNode currentTrackNode;
        private TrackNode nextTrackNode;

        public DoxPlayer()
        {
            InitializeAudioGraph().Wait();
        }

        #region AudioGraph and TrackNode Manipulations

        private async Task InitializeAudioGraph()
        {
            AudioGraphSettings settings = new AudioGraphSettings(Windows.Media.Render.AudioRenderCategory.Media);

            var createAudioGraphResult = await AudioGraph.CreateAsync(settings);
            if (createAudioGraphResult.Status != AudioGraphCreationStatus.Success)
            {
                throw new Exception("AudioGraph creation error: " + createAudioGraphResult.Status.ToString());
            }
            audioGraph = createAudioGraphResult.Graph;

            var createAudioDeviceOutputNodeResult = await audioGraph.CreateDeviceOutputNodeAsync();
            if (createAudioDeviceOutputNodeResult.Status != AudioDeviceNodeCreationStatus.Success)
            {
                throw new Exception("AudioGraph configuration error: " + createAudioDeviceOutputNodeResult.Status.ToString());
            }
            outputNode = createAudioDeviceOutputNodeResult.DeviceOutputNode;

            audioGraph.Start();
        }

        private async Task<TrackNode> CreateTrackNode(ITrack track)
        {
            if (audioGraph == null) throw new Exception("Audio graph not available.");

            var mediaSource = await track.GetAudioMediaSource(SoundQuality);

            var createNodeResult = await audioGraph.CreateMediaSourceAudioInputNodeAsync(mediaSource);

            if (createNodeResult.Status != MediaSourceAudioInputNodeCreationStatus.Success)
            {
                throw new Exception("Audio node creation error: " + createNodeResult.Status.ToString());
            }

            TrackNode node = new() { Node = createNodeResult.Node, Track = track };
            // make sure the node does not start automatically
            node.Node.Stop();
            node.Node.Reset();
            node.Node.AddOutgoingConnection(outputNode);

            return node;
        }

        private void RemoveTrackNode(TrackNode trackNode)
        {
            if (!trackNodes.Contains(trackNode)) return;

            trackNode.Node.Stop();
            trackNode.Node.RemoveOutgoingConnection(outputNode);
            trackNode.Node.Dispose();

            trackNodes.Remove(trackNode);
        }

        #endregion

        public double Volume
        {
            get => ApplicationSettingsExtension.Volume;
            set
            {
                ApplicationSettingsExtension.Volume = value;
            }
        }
        public bool IsDirectSwitchEnabled
        {
            get => ApplicationSettingsExtension.IsDirectSwitchEnabled;
            set
            {
                ApplicationSettingsExtension.IsDirectSwitchEnabled = value;
            }
        }
        public SoundQuality SoundQuality
        {
            get => ApplicationSettingsExtension.SoundQuality;
            set
            {
                ApplicationSettingsExtension.SoundQuality = value;
            }
        }
        public PlaybackMode PlaybackMode
        {
            get => ApplicationSettingsExtension.PlaybackMode;
            set
            {
                ApplicationSettingsExtension.PlaybackMode = value;
            }
        }

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

        private sealed class TrackNode
        {
            public ITrack Track { get; set; }
            public MediaSourceAudioInputNode Node;

            // Judge if the track is longer than 60sec. 
            public bool IsNodeAvailableForCrossfading => Track?.Duration.TotalSeconds > 60.0d;
            /// <summary>
            /// If user use methods like <see cref="Seek(TimeSpan)">Seek</see> in some scenarios we disable the crossfading
            /// </summary>
            public bool IsUserInterruptingCrossfading { get; set; } = false;
        }
    }
}
