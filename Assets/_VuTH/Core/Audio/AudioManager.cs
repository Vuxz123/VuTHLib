using System;
using System.Collections.Generic;
using System.Threading;
using _VuTH.Common;
using _VuTH.Common.Log;
using _VuTH.Core.Audio.Events.MessagePipe;
using _VuTH.Core.GameCycle.Screen.Core;
using _VuTH.Core.GameCycle.Screen.Events.MessagePipe;
using _VuTH.Core.Persistent.DataPackage;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using UnityEngine.Audio;
#if !VCONTAINER
using _VuTH.Common.MessagePipe;
#endif

#if VCONTAINER
using VContainer;
using VContainer.Unity;
#endif

namespace _VuTH.Core.Audio
{
    public sealed class AudioManager : VBootstrapManager<AudioManager, IAudioManager>, IAudioManager
    {
        [Header("Music")]
        [SerializeField, Min(0f)] private float defaultMusicFadeDuration = 0.35f;

        [Tooltip("Optional per-screen music as AudioCue. Overrides ScreenModel.backgroundMusic for that screen.")]
        [SerializeField] private ScreenMusicEntry[] screenMusic = Array.Empty<ScreenMusicEntry>();

        [Header("Volume")]
        [Tooltip("How the master / channel sliders map to gain. Perceptual (slider²) makes the slider feel even; " +
                 "Linear keeps the pre-1.0 behaviour.")]
        [SerializeField] private AudioVolumeCurve volumeCurve = AudioVolumeCurve.Perceptual;

        [Header("One Shot Pool")]
        [SerializeField, Min(1)] private int initialOneShotSources = 8;
        [SerializeField, Min(1)] private int maxOneShotSources = 24;

        [Header("Default Settings")]
        [SerializeField] private bool defaultMuted;
        [SerializeField, Range(0f, 1f)] private float defaultMasterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float defaultMusicVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float defaultSfxVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float defaultUiVolume = 1f;

        // Playback state is mutated from Unity's main thread only.
        private readonly AudioPlaybackRegistry _playbacks = new();
        private readonly List<IDisposable> _subscriptions = new();

        // Created lazily because they call back into this instance.
        private AudioSourcePool _oneShotPool;
        private AudioSettingsBinding _settings;

        private Transform _runtimeRoot;
        private Transform _oneShotRoot;
        private AudioSource _musicSourceA;
        private AudioSource _musicSourceB;
        private AudioSource _activeMusicSource;
        private int _activeMusicPlaybackId;
        private CancellationTokenSource _musicTransitionCts;

        private readonly AudioDuckController _ducking = new();
        private readonly HashSet<AudioChannel> _pausedChannels = new();

        public float MasterVolume => Settings.Package?.MasterVolume.Value ?? defaultMasterVolume;
        public float MusicVolume => Settings.Package?.MusicVolume.Value ?? defaultMusicVolume;
        public float SfxVolume => Settings.Package?.SfxVolume.Value ?? defaultSfxVolume;
        public float UiVolume => Settings.Package?.UiVolume.Value ?? defaultUiVolume;
        public bool Muted => Settings.Package?.Muted.Value ?? defaultMuted;

        private AudioSourcePool OneShotPool => _oneShotPool ??= new AudioSourcePool(CreateOneShotSource);
        private AudioSettingsBinding Settings => _settings ??= new AudioSettingsBinding(RefreshAllVolumes);

#if VCONTAINER
        [Inject]
        public void Construct(
            ISubscriber<PostScreenEnterEvent> postScreenEnterSubscriber,
            ISubscriber<PlayAudioCueEvent> playAudioCueSubscriber)
        {
            _subscriptions.Add(postScreenEnterSubscriber.Subscribe(HandlePostScreenEnter));
            _subscriptions.Add(playAudioCueSubscriber.Subscribe(HandlePlayAudioCueRequest));
        }

        public override void ConfigureRootScope(IContainerBuilder builder)
        {
            builder.RegisterComponent(this).AsImplementedInterfaces();
        }
#endif

        protected override void InitializeBootstrap()
        {
            EnsureRuntimeObjects();
            OneShotPool.Warm(initialOneShotSources);
            SubscribeWithoutContainer();
            InitializeSettingsPackage();
            RefreshAllVolumes();
        }

        protected override void DeinitializeBootstrap()
        {
            CancelMusicTransition();
            _ducking.Clear();
            _pausedChannels.Clear();
            Settings.CancelRegistration();

            foreach (var subscription in _subscriptions)
            {
                subscription.Dispose();
            }
            _subscriptions.Clear();
            Settings.DisposeSubscriptions();

            foreach (var playback in _playbacks.Entries)
            {
                playback.CancellationTokenSource.Cancel();
                playback.CancellationTokenSource.Dispose();
                ResetSource(playback.Source);
            }

            _playbacks.Clear();
            OneShotPool.Clear();

            Settings.ReleasePackage();
            _activeMusicPlaybackId = 0;
            _activeMusicSource = null;
        }

        public AudioPlaybackHandle PlayCue(AudioCue cue, float volumeScale = 1f)
        {
            if (cue == null)
            {
                return AudioPlaybackHandle.Invalid;
            }

            return cue.Channel switch
            {
                AudioChannel.Music => PlayMusicCue(cue, volumeScale, -1f),
                AudioChannel.Ui => PlayOneShot(cue, AudioChannel.Ui, volumeScale),
                _ => PlayOneShot(cue, AudioChannel.Sfx, volumeScale)
            };
        }

        public AudioPlaybackHandle PlayMusic(AudioCue cue, float fadeDuration = -1f)
        {
            return PlayMusicCue(cue, 1f, fadeDuration);
        }

        public AudioPlaybackHandle PlayMusic(AudioClip clip, float volume = 1f, float fadeDuration = -1f)
        {
            if (!clip)
            {
                return AudioPlaybackHandle.Invalid;
            }

            return PlayMusicInternal(
                clip,
                volume,
                null,
                true,
                Vector2.one,
                false,
                64,
                false,
                fadeDuration);
        }

        public AudioPlaybackHandle PlaySfx(AudioCue cue, float volumeScale = 1f)
        {
            return PlayOneShot(cue, AudioChannel.Sfx, volumeScale);
        }

        public AudioPlaybackHandle PlayUi(AudioCue cue, float volumeScale = 1f)
        {
            return PlayOneShot(cue, AudioChannel.Ui, volumeScale);
        }

        public UniTask StopMusicAsync(float fadeDuration = 0f)
        {
            var musicAPlaybackId = 0;
            var musicBPlaybackId = 0;
            
            var hasA = _musicSourceA && _playbacks.TryGetPlaybackId(_musicSourceA, out musicAPlaybackId);
            var hasB = _musicSourceB && _playbacks.TryGetPlaybackId(_musicSourceB, out musicBPlaybackId);

            CancelMusicTransition();
            _activeMusicPlaybackId = 0;
            _activeMusicSource = null;

            if (hasA && hasB)
            {
                return StopBothMusicAsync(musicAPlaybackId, musicBPlaybackId, fadeDuration);
            }

            if (hasA)
            {
                return StopPlaybackAsync(musicAPlaybackId, fadeDuration);
            }

            if (hasB)
            {
                return StopPlaybackAsync(musicBPlaybackId, fadeDuration);
            }

            return UniTask.CompletedTask;
        }

        public UniTask StopAsync(AudioPlaybackHandle handle, float fadeDuration = 0f)
        {
            return StopPlaybackAsync(handle.PlaybackId, fadeDuration);
        }

        public bool IsValid(AudioPlaybackHandle handle)
        {
            return handle.PlaybackId > 0 && _playbacks.Contains(handle.PlaybackId);
        }

        public void SetMuted(bool muted)
        {
            EnsureOwnedSettingsPackage();
            Settings.Package.Muted.Value = muted;
        }

        public void SetMasterVolume(float volume)
        {
            EnsureOwnedSettingsPackage();
            Settings.Package.MasterVolume.Value = Mathf.Clamp01(volume);
        }

        public void SetMusicVolume(float volume)
        {
            EnsureOwnedSettingsPackage();
            Settings.Package.MusicVolume.Value = Mathf.Clamp01(volume);
        }

        public void SetSfxVolume(float volume)
        {
            EnsureOwnedSettingsPackage();
            Settings.Package.SfxVolume.Value = Mathf.Clamp01(volume);
        }

        public void SetUiVolume(float volume)
        {
            EnsureOwnedSettingsPackage();
            Settings.Package.UiVolume.Value = Mathf.Clamp01(volume);
        }

        /// <inheritdoc />
        public AudioPlaybackHandle PlaySfxAt(AudioCue cue, Vector3 position, float volumeScale = 1f)
        {
            return PlayOneShot(cue, AudioChannel.Sfx, volumeScale, position);
        }

        /// <inheritdoc />
        public void SetChannelPaused(AudioChannel channel, bool paused)
        {
            var changed = paused ? _pausedChannels.Add(channel) : _pausedChannels.Remove(channel);
            if (!changed) return;

            foreach (var entry in _playbacks.Entries)
            {
                if (entry.Channel != channel || !entry.Source) continue;
                if (paused) entry.Source.Pause();
                else entry.Source.UnPause();
            }
        }

        /// <inheritdoc />
        public bool IsChannelPaused(AudioChannel channel) => _pausedChannels.Contains(channel);

        /// <inheritdoc />
        public AudioDuckHandle Duck(AudioChannel channel, float volumeFactor, float fadeDuration = 0.25f)
        {
            var id = _ducking.Add(channel, volumeFactor, fadeDuration);
            ApplyDuckingNow();
            return new AudioDuckHandle(ReleaseDuck, id, fadeDuration);
        }

        /// <inheritdoc />
        public UniTask StopChannelAsync(AudioChannel channel, float fadeDuration = 0f)
        {
            if (channel == AudioChannel.Music)
            {
                return StopMusicAsync(fadeDuration);
            }

            var ids = new List<int>();
            foreach (var entry in _playbacks.Entries)
            {
                if (entry.Channel == channel && !entry.IsMusic) ids.Add(entry.PlaybackId);
            }

            if (ids.Count == 0) return UniTask.CompletedTask;

            var stops = new UniTask[ids.Count];
            for (var i = 0; i < ids.Count; i++) stops[i] = StopPlaybackAsync(ids[i], fadeDuration);
            return UniTask.WhenAll(stops);
        }

        private void Update()
        {
            if (_ducking.Tick(Time.unscaledDeltaTime))
            {
                RefreshAllVolumes();
            }
        }

        private void ReleaseDuck(int id, float fadeDuration)
        {
            if (this == null) return; // manager destroyed before the handle was disposed
            _ducking.Remove(id, fadeDuration);
            ApplyDuckingNow();
        }

        /// <summary>Instant ducks (fade 0) must apply this frame, not on the next Update.</summary>
        private void ApplyDuckingNow()
        {
            if (_ducking.Tick(0f)) RefreshAllVolumes();
        }

        private bool IsPlaybackPaused(int playbackId)
        {
            return _playbacks.TryGet(playbackId, out var entry) && _pausedChannels.Contains(entry.Channel);
        }

        private AudioPlaybackHandle PlayMusicInternal(
            AudioClip clip,
            float baseVolume,
            AudioMixerGroup mixerGroup,
            bool loop,
            Vector2 pitchRange,
            bool randomizePitch,
            int priority,
            bool ignoreListenerPause,
            float fadeDuration)
        {
            EnsureRuntimeObjects();

            if (_activeMusicSource &&
                _activeMusicPlaybackId != 0 &&
                _playbacks.TryGet(_activeMusicPlaybackId, out var activePlayback) &&
                activePlayback.Source &&
                activePlayback.Source.clip == clip &&
                activePlayback.Source.isPlaying)
            {
                return new AudioPlaybackHandle(this, _activeMusicPlaybackId);
            }

            var effectiveFade = fadeDuration < 0f ? defaultMusicFadeDuration : fadeDuration;

            CancelMusicTransition();

            var nextSource = GetInactiveMusicSource();
            var previousSource = _activeMusicSource;
            var previousPlaybackId = _activeMusicPlaybackId;

            if (nextSource == null)
            {
                return AudioPlaybackHandle.Invalid;
            }

            ReleasePlaybackForSource(nextSource, stopSource: true);

            var entry = RegisterPlayback(nextSource, AudioChannel.Music, Mathf.Clamp01(baseVolume), isMusic: true);
            var playbackId = entry.PlaybackId;
            ConfigureSource(
                nextSource,
                clip,
                mixerGroup,
                loop,
                pitchRange,
                randomizePitch,
                priority,
                ignoreListenerPause);

            entry.FadeFactor = previousSource != null && previousSource.isPlaying && effectiveFade > 0f ? 0f : 1f;
            ApplyResolvedVolume(entry);
            nextSource.Play();

            _activeMusicSource = nextSource;
            _activeMusicPlaybackId = playbackId;

            if (!previousSource || !previousSource.isPlaying || effectiveFade <= 0f)
            {
                if (previousSource)
                {
                    ReleasePlaybackForSource(previousSource, stopSource: true);
                }

                return new AudioPlaybackHandle(this, playbackId);
            }

            _musicTransitionCts = new CancellationTokenSource();
            RunMusicCrossFadeAsync(previousPlaybackId, playbackId, effectiveFade, _musicTransitionCts).Forget();
            return new AudioPlaybackHandle(this, playbackId);
        }

        private AudioPlaybackHandle PlayMusicCue(AudioCue cue, float volumeScale, float fadeDuration)
        {
            if (!cue || !cue.IsValid)
            {
                return AudioPlaybackHandle.Invalid;
            }

            return PlayMusicInternal(
                cue.Clip,
                cue.Volume * Mathf.Max(0f, volumeScale),
                cue.OutputMixerGroup,
                cue.Loop,
                cue.PitchRange,
                cue.RandomizePitch,
                cue.Priority,
                cue.IgnoreListenerPause,
                fadeDuration);
        }

        private AudioPlaybackHandle PlayOneShot(AudioCue cue, AudioChannel channel, float volumeScale, Vector3? position = null)
        {
            if (cue == null || !cue.IsValid)
            {
                return AudioPlaybackHandle.Invalid;
            }

            EnsureRuntimeObjects();

            var source = OneShotPool.Acquire(maxOneShotSources);
            if (!source)
            {
                // Pool exhausted: cut the least important (then oldest) one-shot, unless everything playing matters more.
                var victim = AudioVoiceStealing.SelectVictim(_playbacks.Entries, cue.Priority);
                if (victim == null)
                {
                    this.LogWarning($"AudioManager: No available one-shot source for '{cue.name}' (all active sounds have higher priority).");
                    return AudioPlaybackHandle.Invalid;
                }

                ReleasePlaybackForSource(victim.Source, stopSource: true);
                source = OneShotPool.Acquire(maxOneShotSources);
                if (!source)
                {
                    return AudioPlaybackHandle.Invalid;
                }
            }

            var entry = RegisterPlayback(source, channel, Mathf.Clamp01(cue.Volume * Mathf.Max(0f, volumeScale)), isMusic: false);
            entry.Priority = cue.Priority;
            entry.StartedAt = Time.realtimeSinceStartup;
            var playbackId = entry.PlaybackId;
            ConfigureSource(
                source,
                cue.Clip,
                cue.OutputMixerGroup,
                cue.Loop,
                cue.PitchRange,
                cue.RandomizePitch,
                cue.Priority,
                cue.IgnoreListenerPause);

            if (position.HasValue)
            {
                source.transform.position = position.Value;
                source.spatialBlend = cue.SpatialBlend;
                source.minDistance = cue.MinDistance;
                source.maxDistance = cue.MaxDistance;
                source.rolloffMode = cue.RolloffMode;
            }

            entry.FadeFactor = 1f;
            ApplyResolvedVolume(entry);
            source.Play();

            if (_pausedChannels.Contains(channel))
            {
                source.Pause(); // resumes with the channel
            }

            if (cue.DuckMusic < 1f && channel != AudioChannel.Music)
            {
                entry.AutoDuck = Duck(AudioChannel.Music, cue.DuckMusic, cue.DuckFadeDuration);
            }

            if (!cue.Loop)
            {
                ReleaseWhenPlaybackCompletesAsync(source, playbackId, entry.CancellationTokenSource.Token).Forget();
            }

            return new AudioPlaybackHandle(this, playbackId);
        }

        private async UniTask StopPlaybackAsync(int playbackId, float fadeDuration)
        {
            if (!_playbacks.TryGet(playbackId, out var entry))
            {
                return;
            }

            entry.CancellationTokenSource.Cancel();

            if (fadeDuration > 0f && entry.Source && entry.Source.isPlaying)
            {
                await FadePlaybackAsync(playbackId, entry, 0f, fadeDuration);
            }

            if (entry.IsMusic)
            {
                ReleaseMusicPlayback(playbackId);
            }
            else
            {
                ReleaseOneShotPlayback(entry.Source, playbackId);
            }
        }

        private async UniTask RunMusicCrossFadeAsync(
            int fromPlaybackId,
            int toPlaybackId,
            float duration,
            CancellationTokenSource transitionCts)
        {
            try
            {
                // Start from where each side currently is: when a previous crossfade was interrupted, the outgoing
                // music may be at half volume — it must keep fading down, not jump back to full.
                var fromStart = _playbacks.TryGet(fromPlaybackId, out var fromAtStart) ? fromAtStart.FadeFactor : 0f;
                var toStart = _playbacks.TryGet(toPlaybackId, out var toAtStart) ? toAtStart.FadeFactor : 0f;

                var elapsed = 0f;
                while (elapsed < duration)
                {
                    transitionCts.Token.ThrowIfCancellationRequested();
                    elapsed += Time.unscaledDeltaTime;
                    var t = Mathf.Clamp01(elapsed / duration);
                    if (_playbacks.TryGet(fromPlaybackId, out var fromEntry))
                    {
                        fromEntry.FadeFactor = Mathf.Lerp(fromStart, 0f, t);
                        ApplyResolvedVolume(fromEntry);
                    }
                    if (_playbacks.TryGet(toPlaybackId, out var toEntry))
                    {
                        toEntry.FadeFactor = Mathf.Lerp(toStart, 1f, t);
                        ApplyResolvedVolume(toEntry);
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, transitionCts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            finally
            {
                if (ReferenceEquals(_musicTransitionCts, transitionCts))
                {
                    _musicTransitionCts.Dispose();
                    _musicTransitionCts = null;
                }
            }

            if (_playbacks.TryGet(toPlaybackId, out var activeEntry))
            {
                activeEntry.FadeFactor = 1f;
                ApplyResolvedVolume(activeEntry);
            }

            if (fromPlaybackId != 0)
            {
                ReleaseMusicPlayback(fromPlaybackId);
            }
        }

        private async UniTask FadePlaybackAsync(int playbackId, AudioPlaybackEntry entry, float targetFadeFactor, float duration)
        {
            if (!entry.Source)
            {
                return;
            }

            var startFade = entry.FadeFactor;
            var elapsed = 0f;
            while (elapsed < duration && IsCurrent(playbackId, entry))
            {
                elapsed += Time.unscaledDeltaTime;
                entry.FadeFactor = Mathf.Lerp(startFade, targetFadeFactor, Mathf.Clamp01(elapsed / duration));
                ApplyResolvedVolume(entry);
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            if (IsCurrent(playbackId, entry))
            {
                entry.FadeFactor = targetFadeFactor;
                ApplyResolvedVolume(entry);
            }
        }

        /// <summary>
        /// True while <paramref name="entry"/> is still the live playback for <paramref name="playbackId"/>.
        /// A fade must stop touching the AudioSource as soon as that source is released or reused by a new playback
        /// (otherwise a fade-out would keep pulling the NEXT music's volume to 0).
        /// </summary>
        private bool IsCurrent(int playbackId, AudioPlaybackEntry entry)
        {
            return entry.Source && _playbacks.TryGet(playbackId, out var current) && ReferenceEquals(current, entry);
        }

        private async UniTask ReleaseWhenPlaybackCompletesAsync(
            AudioSource source,
            int playbackId,
            CancellationToken cancellationToken)
        {
            try
            {
                await UniTask.WaitUntil(
                    WaitUntilCondition,
                    cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            ReleaseOneShotPlayback(source, playbackId);
            
            return;

            // A paused source also reports isPlaying == false: only treat it as finished when its channel is not paused.
            bool WaitUntilCondition() => !source || (!source.isPlaying && !IsPlaybackPaused(playbackId));
        }

        /// <summary>
        /// Screen music rules:
        /// <list type="number">
        /// <item>a <see cref="ScreenMusicEntry"/> for the screen → play its AudioCue;</item>
        /// <item>else <see cref="ScreenModel.backgroundMusic"/> → play that clip;</item>
        /// <item>else <see cref="ScreenModel.stopMusicIfNoBackgroundMusic"/> → stop music;</item>
        /// <item>else keep the current music (popups / pushed screens no longer cut the music, and popping back
        /// does not restart it).</item>
        /// </list>
        /// </summary>
        private void HandlePostScreenEnter(PostScreenEnterEvent eventArgs)
        {
            if (eventArgs?.ToScreen is not ScreenModel screenModel)
            {
                return;
            }

            if (TryGetScreenMusic(screenModel, out var entry))
            {
                PlayMusic(entry.music, entry.fadeDuration > 0f ? entry.fadeDuration : defaultMusicFadeDuration);
                return;
            }

            if (screenModel.backgroundMusic != null)
            {
                PlayMusic(screenModel.backgroundMusic, 1f, defaultMusicFadeDuration);
                return;
            }

            if (screenModel.stopMusicIfNoBackgroundMusic)
            {
                StopMusicAsync(defaultMusicFadeDuration).Forget();
            }
        }

        private bool TryGetScreenMusic(ScreenModel screen, out ScreenMusicEntry entry)
        {
            if (screenMusic != null)
            {
                foreach (var candidate in screenMusic)
                {
                    if (candidate.screen == screen && candidate.music && candidate.music.IsValid)
                    {
                        entry = candidate;
                        return true;
                    }
                }
            }

            entry = default;
            return false;
        }

        /// <summary>
        /// Without VContainer nobody calls <c>Construct</c>, so subscribe through the global MessagePipe broker
        /// (same fallback ScreenManager uses for publishing). Otherwise screen music and PlayAudioCueEvent silently
        /// did nothing.
        /// </summary>
        private void SubscribeWithoutContainer()
        {
#if !VCONTAINER
            if (_subscriptions.Count > 0) return;
            try
            {
                _subscriptions.Add(MessagePipeHelper.GetSubscriber<PostScreenEnterEvent>().Subscribe(HandlePostScreenEnter));
                _subscriptions.Add(MessagePipeHelper.GetSubscriber<PlayAudioCueEvent>().Subscribe(HandlePlayAudioCueRequest));
            }
            catch (Exception e)
            {
                this.LogWarning($"AudioManager: MessagePipe subscription failed ({e.Message}); screen music and PlayAudioCueEvent are disabled.");
            }
#endif
        }

        private void HandlePlayAudioCueRequest(PlayAudioCueEvent eventArgs)
        {
            if (eventArgs?.Cue == null)
            {
                return;
            }

            PlayCue(eventArgs.Cue, eventArgs.VolumeScale);
        }

        private async UniTask StopBothMusicAsync(int musicAPlaybackId, int musicBPlaybackId, float fadeDuration)
        {
            await UniTask.WhenAll(
                StopPlaybackAsync(musicAPlaybackId, fadeDuration),
                StopPlaybackAsync(musicBPlaybackId, fadeDuration));
        }

        private void InitializeSettingsPackage()
        {
            if (Settings.TryBindRegisteredPackage())
            {
                return;
            }

            if (DataPersistenceManager.HasInstance)
            {
                this.LogWarning("AudioManager: AudioSettingsPackage is not pre-registered in DataPersistenceManager. Falling back to self-registration.");
            }

            EnsureOwnedSettingsPackage();
            if (Settings.TryRegisterOwnedPackage())
            {
                return;
            }

            Settings.StartDeferredRegistration();
        }

        private void EnsureOwnedSettingsPackage()
        {
            if (Settings.Package != null)
            {
                return;
            }

            Settings.Bind(
                new AudioSettingsPackage(
                defaultMuted,
                defaultMasterVolume,
                defaultMusicVolume,
                defaultSfxVolume,
                defaultUiVolume),
                ownsPackage: true,
                isRegistered: false);
        }

        private void EnsureRuntimeObjects()
        {
            if (!_runtimeRoot)
            {
                var rootObject = new GameObject("_AudioRuntime");
                rootObject.transform.SetParent(transform, false);
                _runtimeRoot = rootObject.transform;
            }

            if (!_oneShotRoot)
            {
                var oneShotObject = new GameObject("OneShots");
                oneShotObject.transform.SetParent(_runtimeRoot, false);
                _oneShotRoot = oneShotObject.transform;
            }

            if (!_musicSourceA)
            {
                _musicSourceA = CreateManagedSource("Music_A");
            }

            if (!_musicSourceB)
            {
                _musicSourceB = CreateManagedSource("Music_B");
            }
        }

        private AudioSource CreateOneShotSource(int index)
        {
            return CreateManagedSource($"OneShot_{index}", _oneShotRoot);
        }

        private AudioSource CreateManagedSource(string sourceName, Transform parent = null)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(parent != null ? parent : _runtimeRoot, false);

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            return source;
        }

        private AudioSource GetInactiveMusicSource()
        {
            if (_activeMusicSource)
            {
                return ReferenceEquals(_activeMusicSource, _musicSourceA) ? _musicSourceB : _musicSourceA;
            }

            // No active music (e.g. right after StopMusicAsync): prefer a source that is not still fading out.
            var aBusy = _playbacks.TryGetPlaybackId(_musicSourceA, out _);
            var bBusy = _playbacks.TryGetPlaybackId(_musicSourceB, out _);
            if (!aBusy) return _musicSourceA;
            if (!bBusy) return _musicSourceB;

            // Both still fading out: reuse the quieter one (the replaced playback stops immediately).
            return _musicSourceA.volume <= _musicSourceB.volume ? _musicSourceA : _musicSourceB;
        }

        private AudioPlaybackEntry RegisterPlayback(AudioSource source, AudioChannel channel, float baseVolume, bool isMusic)
        {
            ReleasePlaybackForSource(source, stopSource: true);

            return _playbacks.Register(source, channel, baseVolume, isMusic);
        }

        private void ReleasePlaybackForSource(AudioSource source, bool stopSource)
        {
            if (!source || !_playbacks.TryGetPlaybackId(source, out var playbackId))
            {
                if (stopSource && source)
                {
                    ResetSource(source);
                }
                return;
            }

            if (!_playbacks.TryGet(playbackId, out var entry))
            {
                _playbacks.RemoveSource(source);
                if (stopSource)
                {
                    ResetSource(source);
                }
                return;
            }

            entry.CancellationTokenSource.Cancel();
            entry.CancellationTokenSource.Dispose();
            entry.AutoDuck?.Dispose();
            entry.AutoDuck = null;
            _playbacks.Remove(playbackId, source);

            if (entry.IsMusic)
            {
                if (ReferenceEquals(_activeMusicSource, source))
                {
                    _activeMusicSource = null;
                    _activeMusicPlaybackId = 0;
                }

                if (stopSource)
                {
                    ResetSource(source);
                }
                return;
            }

            if (stopSource)
            {
                ResetSource(source);
            }

            OneShotPool.Return(source);
        }

        private void ReleaseOneShotPlayback(AudioSource source, int playbackId)
        {
            if (!source)
            {
                return;
            }

            if (!_playbacks.TryGetPlaybackId(source, out var currentPlaybackId) || currentPlaybackId != playbackId)
            {
                return;
            }

            ReleasePlaybackForSource(source, stopSource: true);
        }

        private void ReleaseMusicPlayback(int playbackId)
        {
            if (!_playbacks.TryGet(playbackId, out var entry))
            {
                return;
            }

            if (!entry.IsMusic)
            {
                ReleaseOneShotPlayback(entry.Source, playbackId);
                return;
            }

            entry.CancellationTokenSource.Cancel();
            entry.CancellationTokenSource.Dispose();
            _playbacks.Remove(playbackId, entry.Source);

            if (ReferenceEquals(_activeMusicSource, entry.Source))
            {
                _activeMusicSource = null;
                _activeMusicPlaybackId = 0;
            }

            ResetSource(entry.Source);
        }

        private void CancelMusicTransition()
        {
            if (_musicTransitionCts == null)
            {
                return;
            }

            _musicTransitionCts.Cancel();
            _musicTransitionCts.Dispose();
            _musicTransitionCts = null;
        }

        private void RefreshAllVolumes()
        {
            foreach (var entry in _playbacks.Entries)
            {
                ApplyResolvedVolume(entry);
            }
        }

        private void ApplyResolvedVolume(AudioPlaybackEntry entry)
        {
            if (!entry.Source)
            {
                return;
            }

            entry.Source.volume = ResolveVolume(entry.Channel, entry.BaseVolume * entry.FadeFactor);
        }

        private float ResolveVolume(AudioChannel channel, float requestedVolume)
        {
            return AudioVolumeMixer.Resolve(
                channel,
                requestedVolume,
                Muted,
                MasterVolume,
                MusicVolume,
                SfxVolume,
                UiVolume,
                volumeCurve,
                _ducking.GetFactor(channel));
        }

        private static void ConfigureSource(
            AudioSource source,
            AudioClip clip,
            AudioMixerGroup mixerGroup,
            bool loop,
            Vector2 pitchRange,
            bool randomizePitch,
            int priority,
            bool ignoreListenerPause)
        {
            var minPitch = Mathf.Max(0.01f, pitchRange.x);
            var maxPitch = Mathf.Max(minPitch, pitchRange.y);

            source.clip = clip;
            source.loop = loop;
            source.outputAudioMixerGroup = mixerGroup;
            source.pitch = randomizePitch
                ? UnityEngine.Random.Range(minPitch, maxPitch)
                : minPitch;
            source.priority = priority;
            source.ignoreListenerPause = ignoreListenerPause;
            source.time = 0f;
        }

        private static void ResetSource(AudioSource source)
        {
            if (!source)
            {
                return;
            }

            source.Stop();
            if (source.clip != null)
            {
                source.time = 0f;
            }

            source.clip = null;
            source.loop = false;
            source.outputAudioMixerGroup = null;
            source.volume = 1f;
            source.pitch = 1f;
            source.priority = 128;
            source.ignoreListenerPause = false;
            source.spatialBlend = 0f;
            source.minDistance = 1f;
            source.maxDistance = 500f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.transform.localPosition = Vector3.zero;
        }
    }
}
