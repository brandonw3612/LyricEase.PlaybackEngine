using LyricEase.PlaybackEngine.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media;
using Windows.Media.Audio;
using Windows.Media.Core;

namespace LyricEase.PlaybackEngine
{
    internal sealed class DoxPlayer : IPlayer
    {
        private AudioGraph audioGraph;
        private AudioDeviceOutputNode outputNode;
        private System.Timers.Timer graphMonitor;
        private const double graphMonitorInterval = 50.0d; 

        private TrackNode previousTrackNode;
        private TrackNode currentTrackNode;
        private TrackNode nextTrackNode;

        private readonly SystemMediaTransportControls SMTC;

        public DoxPlayer()
        {
            InitializeAudioGraph().Wait();

            SMTC = SystemMediaTransportControls.GetForCurrentView();
            SMTC.IsEnabled = false;
            SMTC.IsPauseEnabled = true;
            SMTC.ButtonPressed += SMTC_ButtonPressed;
            SMTC.PlaybackPositionChangeRequested += (_, args) => Seek(args.RequestedPlaybackPosition);

            PlaybackPositionChanged += DoxPlayer_PlaybackPositionChanged;
            PlaybackStatusChanged += DoxePlayer_PlaybackStatusChanged;
            PlaybackQueueUpdated += DoxePlayer_PlaybackQueueUpdated;
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

            graphMonitor = new() { AutoReset = true, Interval = graphMonitorInterval };
            graphMonitor.Elapsed += (_1, _2) => OnGraphTimelineUpdated();
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
            trackNode.Node.Stop();
            trackNode.Node.RemoveOutgoingConnection(outputNode);
            trackNode.Node.Dispose();
        }

        #endregion

        private void OnGraphTimelineUpdated()
        {
            throw new NotImplementedException();
        }

        private void SMTC_ButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
        {
            switch (args.Button)
            {
                case SystemMediaTransportControlsButton.Next: Next(); break;
                case SystemMediaTransportControlsButton.Previous: Previous(); break;
                case SystemMediaTransportControlsButton.Play: Play(); break;
                case SystemMediaTransportControlsButton.Pause: Pause(); break;
                default: break;
            }
        }

        private void DoxPlayer_PlaybackPositionChanged(object sender, PlaybackPositionChangedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void DoxePlayer_PlaybackStatusChanged(object sender, PlaybackStatusChangedEventArgs e)
        {
            SMTC.PlaybackStatus = e.IsPlaying ? MediaPlaybackStatus.Playing : MediaPlaybackStatus.Paused;
        }

        private void DoxePlayer_PlaybackQueueUpdated(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

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

        public bool? IsPlaying { get; set; }

        public object PlaybackSource { get; private set; }

        public ITrack CurrentItem { get => currentTrackNode?.Track; }

        public bool IsPreviousItemAvailable => throw new NotImplementedException();

        public bool? IsNextItemAvailable { get; set; }

        public List<ITrack> OriginalPlaybackList { get; set; }

        public Stack<ITrack> UpNextPlaybackStack { get; set; }

        public List<int> PlaybackOrder { get; private set; }

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
            currentTrackNode.Node.Stop();
            PlaybackStatusChanged?.Invoke(this, new() { IsPlaying = IsPlaying == true, NextAvailable = IsNextItemAvailable == true, PreviousAvailable = IsPreviousItemAvailable });
            graphMonitor.Stop();

        }

        public void Play()
        {
            currentTrackNode.Node.Start();
            PlaybackStatusChanged?.Invoke(this, new() { IsPlaying = IsPlaying == true, NextAvailable = IsNextItemAvailable == true, PreviousAvailable = IsPreviousItemAvailable });
            graphMonitor.Start();
        }

        public async void PlayCollection(IEnumerable<ITrack> Items, ITrack StartingItem, object PlaybackSource)
        {
            //Release previousTrackNode,currentTrackNode,nextTrackNode;
            if (previousTrackNode is not null) RemoveTrackNode(previousTrackNode);
            if (currentTrackNode is not null) RemoveTrackNode(currentTrackNode);
            if (nextTrackNode is not null) RemoveTrackNode(nextTrackNode);

            if (Items?.Count() <= 0) return;
            Stop();
            this.PlaybackSource = PlaybackSource;

            UpNextPlaybackStack = new Stack<ITrack>();
            OriginalPlaybackList = Items.ToList();

            PlaybackOrder = PlaybackMode == PlaybackMode.Shuffle ?
                Methods.GenerateShuffledSequence(Items.Count()) :
                Methods.GenerateAscendingSequence(Items.Count());

            int FirstItemIndex;
            if (StartingItem == null) FirstItemIndex = 0;
            else
            {
                int OriginalItemIndex = OriginalPlaybackList.IndexOf(StartingItem);
                if (PlaybackMode == PlaybackMode.Shuffle) FirstItemIndex = PlaybackOrder.IndexOf(OriginalItemIndex);
                else FirstItemIndex = OriginalItemIndex; 
            }
            TrackNode firstTrackNode = await CreateTrackNode(OriginalPlaybackList[PlaybackOrder[FirstItemIndex]]);

            currentTrackNode = firstTrackNode;
            currentTrackNode.Node.Start();
            IsPlaying = true;
            graphMonitor.Start();

            IsNextItemAvailable = null;
            PlaybackQueueUpdated?.Invoke(this, null);
            CurrentlyPlayingItemChanged?.Invoke(this, new CurrentlyPlayingItemChangedEventArgs { CurrentTrack = CurrentItem}); 
        }

        public void PlayPause()
        {
            if (IsPlaying ==false)
                Play();
            else if (IsPlaying == true)
                Pause();
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
