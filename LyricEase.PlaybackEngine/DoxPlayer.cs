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

        private System.Timers.Timer resourceReloadTimer;

        private TrackNode previousTrackNode;
        private TrackNode currentTrackNode;
        private TrackNode nextTrackNode;
        private NodeStatus nextTrackNodeStatus = NodeStatus.Unavailable;

        private System.Timers.Timer crossfadingTimer;
        private bool isInCrossfasingPhase = false;
        private double actualAudioCrossfadingLength;

        private int previousItemIndex = -1;
        private int currentItemIndex = -1;
        private int preparedItemIndex = -1;

        private readonly SystemMediaTransportControls SMTC;

        #region Settings properties

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
                PlaybackMode oldValue = ApplicationSettingsExtension.PlaybackMode;
                OnPlaybackModeChanged(oldValue, value);
                ApplicationSettingsExtension.PlaybackMode = value;
            }
        }
        public bool IsAudioCrossfadingEnabled
        {
            get => ApplicationSettingsExtension.IsAudioCrossfadingEnabled;
            set
            {
                ApplicationSettingsExtension.IsAudioCrossfadingEnabled = value;
            }
        }
        public int AudioCrossfadingLength
        {
            get => ApplicationSettingsExtension.AudioCrossfadingLength;
            set
            {
                ApplicationSettingsExtension.AudioCrossfadingLength = value;
            }
        }

        #endregion

        public bool? IsPlaying { get; set; }

        public object PlaybackSource { get; private set; }

        public ITrack CurrentItem { get => currentTrackNode?.Track; }

        public List<ITrack> OriginalPlaybackList { get; set; } = new();

        public List<ITrack> UpNextPlaybackList { get; set; } = new();

        public List<int> PlaybackOrder { get; private set; } = new();

        public List<ITrack> OrderedPlaybackList
        {
            get
            {
                int startIndex = Methods.GetNextIndex(currentItemIndex == -1 ? previousItemIndex : currentItemIndex, PlaybackOrder.Count);
                List<int> extendedIndices = new(PlaybackOrder.TakeLast(PlaybackOrder.Count - startIndex));
                if (startIndex > 0) extendedIndices.AddRange(PlaybackOrder.Take(startIndex));
                if (extendedIndices.Count > 100) extendedIndices = extendedIndices.GetRange(0, 100);
                return extendedIndices.Select(i => OriginalPlaybackList[i]).ToList();
            }
        }

        public event EventHandler<PlaybackStatusChangedEventArgs> PlaybackStatusChanged;
        public event EventHandler<EventArgs> PlaybackModeChanged;
        public event EventHandler<PlaybackPositionChangedEventArgs> PlaybackPositionChanged;
        public event EventHandler<CurrentlyPlayingItemChangedEventArgs> CurrentlyPlayingItemChanged;
        public event EventHandler PlaybackQueueUpdated;
        public event EventHandler PlaybackEnded;
        public event EventHandler<PlaybackErrorEventArgs> PlaybackError;

        public DoxPlayer()
        {
            InitializeAudioGraph().Wait();

            SMTC = SystemMediaTransportControls.GetForCurrentView();
            SMTC.IsEnabled = false;
            SMTC.IsPauseEnabled = true;
            SMTC.ButtonPressed += SMTC_ButtonPressed;
            SMTC.PlaybackPositionChangeRequested += (_, args) => Seek(args.RequestedPlaybackPosition);

            PlaybackPositionChanged += DoxPlayer_PlaybackPositionChanged;
            PlaybackStatusChanged += DoxPlayer_PlaybackStatusChanged;
            PlaybackQueueUpdated += DoxPlayer_PlaybackQueueUpdated;
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

        private void DisposeTrackNode(TrackNode trackNode)
        {
            trackNode.Node.Stop();
            trackNode.Node.RemoveOutgoingConnection(outputNode);
            trackNode.Node.Dispose();
        }

        #endregion

        private void OnGraphTimelineUpdated()
        {
            if (currentTrackNode is null) return;
            TimeSpan currentPosition = currentTrackNode.Node.Position;
            TimeSpan duration = currentTrackNode.Node.Duration;

            PlaybackPositionChanged?.Invoke(this, new() { Current = currentPosition, Total = duration });

            if ((duration - currentPosition).TotalSeconds < 60.0d)
            {
                if (nextTrackNodeStatus == NodeStatus.Unavailable)
                {
                    PrepareNextNode();
                }
            }
            if (!isInCrossfasingPhase &&
                IsAudioCrossfadingEnabled &&
                nextTrackNodeStatus == NodeStatus.Available &&
                currentTrackNode.IsNodeAvailableForCrossfading &&
                nextTrackNode.IsNodeAvailableForCrossfading &&
                !currentTrackNode.IsUserInterruptingCrossfading &&
                (duration - currentPosition).TotalSeconds <= AudioCrossfadingLength &&
                (duration - currentPosition).TotalSeconds > 3.0d)
            {
                EnterCrossfadingPhase();
            }
            if (isInCrossfasingPhase &&
                currentPosition.TotalSeconds >= AudioCrossfadingLength)
            {
                LeaveCrossfadingPhase();
            }
        }

        private void EnterCrossfadingPhase()
        {
            isInCrossfasingPhase = true;
            if (currentTrackNode is null) return;
            TimeSpan currentPosition = currentTrackNode.Node.Position;
            TimeSpan duration = currentTrackNode.Node.Duration;

            actualAudioCrossfadingLength = (duration - currentPosition).TotalSeconds;

            if (crossfadingTimer is not null) crossfadingTimer.Dispose();
            crossfadingTimer = new() { Interval = graphMonitorInterval, AutoReset = true };
            crossfadingTimer.Elapsed += (_1, _2) => UpdateCrossfadingProgress();

            if (previousTrackNode is not null)
            {
                DisposeTrackNode(previousTrackNode);
                previousTrackNode = null;
            }
            previousTrackNode = currentTrackNode;
            currentTrackNode = nextTrackNode;
            nextTrackNode = null;

            if (currentItemIndex != -1) previousItemIndex = currentItemIndex;
            currentItemIndex = preparedItemIndex;

            currentTrackNode.Node.OutgoingGain = 0.0d;
            currentTrackNode.Node.Start();

            crossfadingTimer.Start();
        }

        private void LeaveCrossfadingPhase()
        {
            isInCrossfasingPhase = false;

            if (crossfadingTimer.Enabled) crossfadingTimer.Stop();
            crossfadingTimer.Dispose();

            currentTrackNode.Node.OutgoingGain = 1.0d;
            
            previousTrackNode.Node.Stop();
        }

        private void UpdateCrossfadingProgress()
        {
            if (currentTrackNode is null || previousTrackNode is null) return;
            TimeSpan currentPosition = currentTrackNode.Node.Position;

            double x = currentPosition.TotalSeconds / actualAudioCrossfadingLength;
            if (x < 0.0d) x = 0.0d;
            if (x > 1.0d) x = 1.0d;

            double quad = x < 0.5d ? 2 * x * x : -2 * x * x + 4 * x - 1;

            currentTrackNode.Node.OutgoingGain = quad;
            previousTrackNode.Node.OutgoingGain = 1 - quad;
        }

        private async void PrepareNextNode(int retryTime = 0)
        {
            if (retryTime >= 3)
            {
                nextTrackNodeStatus = NodeStatus.Failed;
                return;
            }
            try
            {
                nextTrackNodeStatus = NodeStatus.Preparing;
                ITrack nextTrack;
                if (UpNextPlaybackList?.Count > 0)
                {
                    preparedItemIndex = -1;
                    nextTrack = UpNextPlaybackList[0];
                    UpNextPlaybackList.RemoveAt(0);
                }
                else
                {
                    int nextIndex = Methods.GetNextIndex(
                        currentItemIndex == -1 ? previousItemIndex : currentItemIndex,
                        OriginalPlaybackList.Count);
                    preparedItemIndex = nextIndex;
                    nextTrack = OriginalPlaybackList[PlaybackOrder[nextIndex]];
                }
                nextTrackNode = await CreateTrackNode(nextTrack);
            }
            catch
            {
                if (resourceReloadTimer is not null) resourceReloadTimer.Dispose();
                resourceReloadTimer = new() { AutoReset = false, Interval = 3000 };
                resourceReloadTimer.Elapsed += (_1, _2) => PrepareNextNode(retryTime + 1);
            }
        }

        private void OnPlaybackModeChanged(PlaybackMode oldValue, PlaybackMode newValue)
        {
            if (oldValue == newValue) return;
            if (OriginalPlaybackList?.Count > 0 || UpNextPlaybackList?.Count > 0)
            {
                // Only when Shuffle is involved we need to make changes to playback order and indices
                if (oldValue == PlaybackMode.Shuffle || newValue == PlaybackMode.Shuffle)
                {
                    int currentTrack = PlaybackOrder[currentItemIndex];

                    PlaybackOrder = newValue == PlaybackMode.Shuffle ?
                        Methods.GenerateShuffledSequence(OriginalPlaybackList.Count) :
                        Methods.GenerateAscendingSequence(OriginalPlaybackList.Count);

                    if (currentItemIndex == -1)
                    {
                        previousItemIndex = (OriginalPlaybackList?.Count ?? 0) - 1;
                    }
                    else
                    {
                        currentItemIndex = oldValue == PlaybackMode.Shuffle ?
                            currentTrack :
                            PlaybackOrder.IndexOf(currentTrack);
                        if (OriginalPlaybackList?.Count <= 0) previousItemIndex = -1;
                        else previousItemIndex = Methods.GetPreviousIndex(currentItemIndex, OriginalPlaybackList.Count);
                    }
                    PlaybackQueueUpdated?.Invoke(this, EventArgs.Empty);
                }
                PlaybackModeChanged?.Invoke(this, EventArgs.Empty);
            }
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
            SystemMediaTransportControlsTimelineProperties timelineProperties = new()
            {
                MinSeekTime = TimeSpan.Zero,
                MaxSeekTime = e.Total,
                StartTime = TimeSpan.Zero,
                Position = e.Current,
                EndTime = e.Total
            };
            SMTC?.UpdateTimelineProperties(timelineProperties);
        }

        private void DoxPlayer_PlaybackStatusChanged(object sender, PlaybackStatusChangedEventArgs e)
        {
            SMTC.PlaybackStatus = e.IsPlaying ? MediaPlaybackStatus.Playing : MediaPlaybackStatus.Paused;
        }

        private void DoxPlayer_PlaybackQueueUpdated(object sender, EventArgs e)
        {
            ITrack nextTrack = UpNextPlaybackList?.Count > 0 ? UpNextPlaybackList[0] :
                OriginalPlaybackList[PlaybackOrder[
                Methods.GetNextIndex(currentItemIndex == -1 ? previousItemIndex : currentItemIndex,
                    OriginalPlaybackList.Count)
                ]];
            if (nextTrackNodeStatus != NodeStatus.Unavailable)
            {
                if (nextTrackNodeStatus == NodeStatus.Available && nextTrackNode.Track != nextTrack)
                {
                    DisposeTrackNode(nextTrackNode);
                    nextTrackNode = null;
                }
                nextTrackNodeStatus = NodeStatus.Unavailable;
            }
        }

        public void AddToUpNext(IEnumerable<ITrack> Items)
        {
            if (Items?.Count() <= 0) return;
            foreach (ITrack track in Items.Reverse())
            {
                if (UpNextPlaybackList.Contains(track))
                    UpNextPlaybackList.Remove(track);
                UpNextPlaybackList.Insert(0, track);
            }
            PlaybackQueueUpdated?.Invoke(this, EventArgs.Empty);
        }

        public async void Next()
        {
            if (graphMonitor.Enabled) graphMonitor.Stop();
            if (isInCrossfasingPhase == true) LeaveCrossfadingPhase();
            int nextIndex = 0;
            if (nextTrackNodeStatus == NodeStatus.Available) nextIndex = preparedItemIndex;
            else
            {
                nextIndex = UpNextPlaybackList?.Count > 0 ? -1 :
                    Methods.GetNextIndex(currentItemIndex == -1 ? previousItemIndex : currentItemIndex,
                        OriginalPlaybackList.Count);
                ITrack nextTrack = nextIndex == -1 ? UpNextPlaybackList[0] :
                    OriginalPlaybackList[PlaybackOrder[nextIndex]];
                if (nextTrackNode is not null) DisposeTrackNode(nextTrackNode);
                nextTrackNode = await CreateTrackNode(nextTrack);
                if (nextIndex == -1) UpNextPlaybackList.RemoveAt(0);
            }

            if (previousTrackNode?.Track != OriginalPlaybackList[PlaybackOrder[previousItemIndex]])
            {
                DisposeTrackNode(previousTrackNode);
                previousTrackNode = null;
            }

            if (currentItemIndex == -1)
            {
                DisposeTrackNode(currentTrackNode);
                currentTrackNode = null;
            }
            else
            {
                DisposeTrackNode(previousTrackNode);
                previousTrackNode = currentTrackNode;
                previousItemIndex = currentItemIndex;
            }

            currentItemIndex = nextIndex;
            currentTrackNode = nextTrackNode;
            currentTrackNode.Node.OutgoingGain = 1;
            nextTrackNode = null;
            
            if (IsPlaying == true)
            {
                currentTrackNode.Node.Start();
                graphMonitor.Start();
            }

            CurrentlyPlayingItemChanged?.Invoke(this, new() { CurrentTrack = currentTrackNode.Track });
            PlaybackQueueUpdated?.Invoke(this, EventArgs.Empty);
        }

        public void Pause()
        {
            graphMonitor.Stop();
            if (isInCrossfasingPhase) LeaveCrossfadingPhase();
            currentTrackNode.Node.Stop();
            PlaybackStatusChanged?.Invoke(this, new() { IsPlaying = false });
        }

        public void Play()
        {
            currentTrackNode.Node.Start();
            PlaybackStatusChanged?.Invoke(this, new() { IsPlaying = true });
            graphMonitor.Start();
        }

        public async void PlayCollection(IEnumerable<ITrack> Items, ITrack StartingItem, object PlaybackSource)
        {
            //Release previousTrackNode,currentTrackNode,nextTrackNode;
            if (previousTrackNode is not null) DisposeTrackNode(previousTrackNode);
            if (currentTrackNode is not null) DisposeTrackNode(currentTrackNode);
            if (nextTrackNode is not null) DisposeTrackNode(nextTrackNode);

            if (Items?.Count() <= 0) return;
            Stop();
            this.PlaybackSource = PlaybackSource;

            UpNextPlaybackList = new();
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

            currentTrackNode = await CreateTrackNode(OriginalPlaybackList[PlaybackOrder[FirstItemIndex]]);
            currentTrackNode.Node.Start();
            IsPlaying = true;
            graphMonitor.Start();

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

        public async void Previous()
        {
            if (graphMonitor.Enabled) graphMonitor.Stop();
            if (isInCrossfasingPhase) LeaveCrossfadingPhase();

            if (!IsDirectSwitchEnabled && currentTrackNode?.Node.Position.TotalSeconds > 3.0d)
            {
                currentTrackNode.Node.Seek(TimeSpan.Zero);
                return;
            }

            if (nextTrackNode is not null)
            {
                DisposeTrackNode(nextTrackNode);
                nextTrackNode = null;
            }

            if (currentItemIndex == -1)
            {
                DisposeTrackNode(currentTrackNode);
                currentTrackNode = null;
            }
            else
            {
                currentTrackNode.Node.Stop();
                currentTrackNode.Node.Reset();
                nextTrackNode = currentTrackNode;
            }

            ITrack previousTrack = OriginalPlaybackList[PlaybackOrder[previousItemIndex]];
            if (previousTrackNode?.Track != previousTrack)
            {
                if (previousTrackNode is not null) DisposeTrackNode(previousTrackNode);
                previousTrackNode = await CreateTrackNode(previousTrack);
            }
            previousTrackNode.Node.OutgoingGain = 1.0d;
            currentTrackNode = previousTrackNode;

            currentItemIndex = previousItemIndex;
            previousItemIndex = Methods.GetPreviousIndex(currentItemIndex, OriginalPlaybackList.Count);

            previousTrackNode = null;
            if (IsPlaying == true)
            {
                currentTrackNode.Node.Start();
                graphMonitor.Start();
            }

            CurrentlyPlayingItemChanged?.Invoke(this, new() { CurrentTrack = currentTrackNode.Track });
            PlaybackQueueUpdated?.Invoke(this, EventArgs.Empty);
        }

        public void RemoveTrack(ITrack targetTrack)
        {
            bool changed = false;
            if (UpNextPlaybackList.Contains(targetTrack))
            {
                UpNextPlaybackList.Remove(targetTrack);
                changed = true;
            }
            else if (OriginalPlaybackList.Contains(targetTrack))
            {
                int index = OriginalPlaybackList.IndexOf(targetTrack);
                int order = PlaybackOrder.IndexOf(index);
                PlaybackOrder.RemoveAt(order);
                for (int i = 0; i < PlaybackOrder.Count; i++)
                    if (PlaybackOrder[i] > order) PlaybackOrder[i]--;
                changed = true;
            }
            if (changed) PlaybackQueueUpdated?.Invoke(this, EventArgs.Empty);
        }

        public void Seek(TimeSpan TargetPosition)
        {
            if (isInCrossfasingPhase) LeaveCrossfadingPhase();
            currentTrackNode.Node.Seek(TargetPosition);

            TimeSpan duration = currentTrackNode.Node.Duration;
            if ((duration - TargetPosition).TotalSeconds < 20.0d) currentTrackNode.IsUserInterruptingCrossfading = true;
        }

        public async void SkipTo(ITrack targetTrack)
        {
            if (UpNextPlaybackList.Contains(targetTrack))
            {
                int index = UpNextPlaybackList.IndexOf(targetTrack);
                if (index > 0) UpNextPlaybackList.RemoveRange(0, index);
                PlaybackQueueUpdated?.Invoke(this, EventArgs.Empty); // Remove?
                Next();
            }
            else if (OriginalPlaybackList.Contains(targetTrack))
            {
                UpNextPlaybackList.Clear();
                int index = OriginalPlaybackList.IndexOf(targetTrack);
                int order = PlaybackOrder.IndexOf(index);
                graphMonitor.Stop();

                if (previousTrackNode is not null)
                {
                    DisposeTrackNode(previousTrackNode);
                    previousTrackNode = null;
                }
                if (currentTrackNode is not null)
                {
                    DisposeTrackNode(currentTrackNode);
                    currentTrackNode = null;
                }
                if (nextTrackNode is not null)
                {
                    DisposeTrackNode(nextTrackNode);
                    nextTrackNode = null;
                }

                currentTrackNode = await CreateTrackNode(targetTrack);
                currentTrackNode.Node.Start();
                IsPlaying = true;
                graphMonitor.Start();

                PlaybackQueueUpdated?.Invoke(this, null);
                CurrentlyPlayingItemChanged?.Invoke(this, new CurrentlyPlayingItemChangedEventArgs { CurrentTrack = CurrentItem });
            }
        }

        public void Stop()
        {
            DisposeTrackNode(currentTrackNode); currentTrackNode = null;
            DisposeTrackNode(previousTrackNode); previousTrackNode = null;
            DisposeTrackNode(nextTrackNode); nextTrackNode = null;

            OriginalPlaybackList?.Clear();
            PlaybackOrder?.Clear();
            UpNextPlaybackList?.Clear();

            PlaybackEnded?.Invoke(this, EventArgs.Empty);
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
