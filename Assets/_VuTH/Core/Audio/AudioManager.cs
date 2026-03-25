using System;
using System.Collections.Generic;
using System.Threading;
using _VuTH.Common;
using _VuTH.Common.Log;
using _VuTH.Core.Audio.Events.MessagePipe;
using _VuTH.Core.GameCycle.Screen.Core;
using _VuTH.Core.GameCycle.Screen.Events.MessagePipe;
using _VuTH.Core.Persistant.DataPackage;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using UnityEngine.Audio;

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

        [Header("One Shot Pool")]
        [SerializeField, Min(1)] private int initialOneShotSources = 8;
        [SerializeField, Min(1)] private int maxOneShotSources = 24;

        [Header("Default Settings")]
        [SerializeField] private bool defaultMuted;
        [SerializeField, Range(0f, 1f)] private float defaultMasterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float defaultMusicVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float defaultSfxVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float defaultUiVolume = 1f;

        private readonly Dictionary<int, PlaybackEntry> _playbacks = new();
        private readonly Dictionary<AudioSource, int> _sourceToPlayback = new();
        private readonly Queue<AudioSource> _oneShotPool = new();
        private readonly List<AudioSource> _ownedOneShotSources = new();
        private readonly List<IDisposable> _subscriptions = new();

        private Transform _runtimeRoot;
        private Transform _oneShotRoot;
        private AudioSource _musicSourceA;
        private AudioSource _musicSourceB;
        private AudioSource _activeMusicSource;
        private int _activeMusicPlaybackId;
        private int _nextPlaybackId = 1;
        private CancellationTokenSource _musicTransitionCts;
        private AudioSettingsPackage _settingsPackage;
        private IDataPersistenceManager _persistenceManager;

        public float MasterVolume => _settingsPackage?.MasterVolume.Value ?? defaultMasterVolume;
        public float MusicVolume => _settingsPackage?.MusicVolume.Value ?? defaultMusicVolume;
        public float SfxVolume => _settingsPackage?.SfxVolume.Value ?? defaultSfxVolume;
        public float UiVolume => _settingsPackage?.UiVolume.Value ?? defaultUiVolume;
        public bool Muted => _settingsPackage?.Muted.Value ?? defaultMuted;

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
            WarmOneShotPool();
            InitializeSettingsPackage();
            RefreshAllVolumes();
        }

        protected override void DeinitializeBootstrap()
        {
            CancelMusicTransition();

            foreach (var subscription in _subscriptions)
            {
                subscription.Dispose();
            }
            _subscriptions.Clear();

            foreach (var playback in _playbacks.Values)
            {
                playback.CancellationTokenSource.Cancel();
                playback.CancellationTokenSource.Dispose();
                ResetSource(playback.Source);
            }

            _playbacks.Clear();
            _sourceToPlayback.Clear();
            _oneShotPool.Clear();
            _ownedOneShotSources.Clear();

            if (_settingsPackage != null)
            {
                _settingsPackage.SaveNowAsync().Forget();

                _persistenceManager?.UnregisterPackage(_settingsPackage);

                _settingsPackage.Dispose();
                _settingsPackage = null;
            }

            _persistenceManager = null;
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
            
            var hasA = _musicSourceA && _sourceToPlayback.TryGetValue(_musicSourceA, out musicAPlaybackId);
            var hasB = _musicSourceB && _sourceToPlayback.TryGetValue(_musicSourceB, out musicBPlaybackId);

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
            return handle.PlaybackId > 0 && _playbacks.ContainsKey(handle.PlaybackId);
        }

        public void SetMuted(bool muted)
        {
            EnsureSettingsPackage();
            _settingsPackage.Muted.Value = muted;
        }

        public void SetMasterVolume(float volume)
        {
            EnsureSettingsPackage();
            _settingsPackage.MasterVolume.Value = Mathf.Clamp01(volume);
        }

        public void SetMusicVolume(float volume)
        {
            EnsureSettingsPackage();
            _settingsPackage.MusicVolume.Value = Mathf.Clamp01(volume);
        }

        public void SetSfxVolume(float volume)
        {
            EnsureSettingsPackage();
            _settingsPackage.SfxVolume.Value = Mathf.Clamp01(volume);
        }

        public void SetUiVolume(float volume)
        {
            EnsureSettingsPackage();
            _settingsPackage.UiVolume.Value = Mathf.Clamp01(volume);
        }

        private AudioPlaybackHandle PlayMusicInternal(
            AudioClip clip,
            float baseVolume,
            AudioMixerGroup mixerGroup,
            bool loop,
            Vector2 pitchRange,
            int priority,
            bool ignoreListenerPause,
            float fadeDuration)
        {
            EnsureRuntimeObjects();

            if (_activeMusicSource &&
                _activeMusicPlaybackId != 0 &&
                _playbacks.TryGetValue(_activeMusicPlaybackId, out var activePlayback) &&
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

            var playbackId = RegisterPlayback(nextSource, AudioChannel.Music, Mathf.Clamp01(baseVolume), isMusic: true);
            ConfigureSource(
                nextSource,
                clip,
                mixerGroup,
                loop,
                pitchRange,
                priority,
                ignoreListenerPause);

            var entry = _playbacks[playbackId];
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
                cue.Priority,
                cue.IgnoreListenerPause,
                fadeDuration);
        }

        private AudioPlaybackHandle PlayOneShot(AudioCue cue, AudioChannel channel, float volumeScale)
        {
            if (cue == null || !cue.IsValid)
            {
                return AudioPlaybackHandle.Invalid;
            }

            EnsureRuntimeObjects();

            var source = AcquireOneShotSource();
            if (!source)
            {
                this.LogWarning("AudioManager: No available one-shot source.");
                return AudioPlaybackHandle.Invalid;
            }

            var playbackId = RegisterPlayback(source, channel, Mathf.Clamp01(cue.Volume * Mathf.Max(0f, volumeScale)), isMusic: false);
            ConfigureSource(
                source,
                cue.Clip,
                cue.OutputMixerGroup,
                cue.Loop,
                cue.PitchRange,
                cue.Priority,
                cue.IgnoreListenerPause);

            var entry = _playbacks[playbackId];
            entry.FadeFactor = 1f;
            ApplyResolvedVolume(entry);
            source.Play();

            if (!cue.Loop)
            {
                ReleaseWhenPlaybackCompletesAsync(source, playbackId, entry.CancellationTokenSource.Token).Forget();
            }

            return new AudioPlaybackHandle(this, playbackId);
        }

        private async UniTask StopPlaybackAsync(int playbackId, float fadeDuration)
        {
            if (!_playbacks.TryGetValue(playbackId, out var entry))
            {
                return;
            }

            entry.CancellationTokenSource.Cancel();

            if (fadeDuration > 0f && entry.Source && entry.Source.isPlaying)
            {
                await FadePlaybackAsync(entry, 0f, fadeDuration);
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
                var elapsed = 0f;
                while (elapsed < duration)
                {
                    transitionCts.Token.ThrowIfCancellationRequested();
                    elapsed += Time.unscaledDeltaTime;
                    var t = Mathf.Clamp01(elapsed / duration);

                    if (_playbacks.TryGetValue(fromPlaybackId, out var fromEntry))
                    {
                        fromEntry.FadeFactor = 1f - t;
                        ApplyResolvedVolume(fromEntry);
                    }

                    if (_playbacks.TryGetValue(toPlaybackId, out var toEntry))
                    {
                        toEntry.FadeFactor = t;
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

            if (_playbacks.TryGetValue(toPlaybackId, out var activeEntry))
            {
                activeEntry.FadeFactor = 1f;
                ApplyResolvedVolume(activeEntry);
            }

            if (fromPlaybackId != 0)
            {
                ReleaseMusicPlayback(fromPlaybackId);
            }
        }

        private async UniTask FadePlaybackAsync(PlaybackEntry entry, float targetFadeFactor, float duration)
        {
            var source = entry.Source;
            if (source == null)
            {
                return;
            }

            var startFade = entry.FadeFactor;
            var elapsed = 0f;

            while (elapsed < duration && source != null)
            {
                elapsed += Time.unscaledDeltaTime;
                entry.FadeFactor = Mathf.Lerp(startFade, targetFadeFactor, Mathf.Clamp01(elapsed / duration));
                ApplyResolvedVolume(entry);
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            if (source != null)
            {
                entry.FadeFactor = targetFadeFactor;
                ApplyResolvedVolume(entry);
            }
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

            bool WaitUntilCondition() => !source || !source.isPlaying;
        }

        private void HandlePostScreenEnter(PostScreenEnterEvent eventArgs)
        {
            if (eventArgs?.ToScreen is not ScreenModel screenModel)
            {
                return;
            }

            if (screenModel.backgroundMusic != null)
            {
                PlayMusic(screenModel.backgroundMusic, 1f, defaultMusicFadeDuration);
            }
            else
            {
                StopMusicAsync(defaultMusicFadeDuration).Forget();
            }
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
            EnsureSettingsPackage();

            if (!DataPersistenceManager.HasInstance) return;
            _persistenceManager = DataPersistenceManager.Instance;
            _persistenceManager.RegisterPackage(_settingsPackage);
        }

        private void EnsureSettingsPackage()
        {
            if (_settingsPackage != null)
            {
                return;
            }

            _settingsPackage = new AudioSettingsPackage(
                defaultMuted,
                defaultMasterVolume,
                defaultMusicVolume,
                defaultSfxVolume,
                defaultUiVolume);

            _subscriptions.Add(_settingsPackage.Muted.Subscribe(_ => RefreshAllVolumes()));
            _subscriptions.Add(_settingsPackage.MasterVolume.Subscribe(_ => RefreshAllVolumes()));
            _subscriptions.Add(_settingsPackage.MusicVolume.Subscribe(_ => RefreshAllVolumes()));
            _subscriptions.Add(_settingsPackage.SfxVolume.Subscribe(_ => RefreshAllVolumes()));
            _subscriptions.Add(_settingsPackage.UiVolume.Subscribe(_ => RefreshAllVolumes()));
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

        private void WarmOneShotPool()
        {
            for (var i = _ownedOneShotSources.Count; i < initialOneShotSources; i++)
            {
                var source = CreateManagedSource($"OneShot_{i + 1}", _oneShotRoot);
                _ownedOneShotSources.Add(source);
                _oneShotPool.Enqueue(source);
            }
        }

        private AudioSource AcquireOneShotSource()
        {
            if (_oneShotPool.Count > 0)
            {
                return _oneShotPool.Dequeue();
            }

            if (_ownedOneShotSources.Count >= maxOneShotSources)
            {
                return null;
            }

            var source = CreateManagedSource($"OneShot_{_ownedOneShotSources.Count + 1}", _oneShotRoot);
            _ownedOneShotSources.Add(source);
            return source;
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
            if (!_activeMusicSource)
            {
                return _musicSourceA;
            }

            return ReferenceEquals(_activeMusicSource, _musicSourceA) ? _musicSourceB : _musicSourceA;
        }

        private int RegisterPlayback(AudioSource source, AudioChannel channel, float baseVolume, bool isMusic)
        {
            ReleasePlaybackForSource(source, stopSource: true);

            var playbackId = _nextPlaybackId++;
            var entry = new PlaybackEntry(playbackId, source, channel, baseVolume, isMusic);
            _playbacks[playbackId] = entry;
            _sourceToPlayback[source] = playbackId;
            return playbackId;
        }

        private void ReleasePlaybackForSource(AudioSource source, bool stopSource)
        {
            if (!source || !_sourceToPlayback.TryGetValue(source, out var playbackId))
            {
                if (stopSource && source)
                {
                    ResetSource(source);
                }
                return;
            }

            if (!_playbacks.TryGetValue(playbackId, out var entry))
            {
                _sourceToPlayback.Remove(source);
                if (stopSource)
                {
                    ResetSource(source);
                }
                return;
            }

            entry.CancellationTokenSource.Cancel();
            entry.CancellationTokenSource.Dispose();

            _playbacks.Remove(playbackId);
            _sourceToPlayback.Remove(source);

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

            if (!_oneShotPool.Contains(source))
            {
                _oneShotPool.Enqueue(source);
            }
        }

        private void ReleaseOneShotPlayback(AudioSource source, int playbackId)
        {
            if (!source)
            {
                return;
            }

            if (!_sourceToPlayback.TryGetValue(source, out var currentPlaybackId) || currentPlaybackId != playbackId)
            {
                return;
            }

            ReleasePlaybackForSource(source, stopSource: true);
        }

        private void ReleaseMusicPlayback(int playbackId)
        {
            if (!_playbacks.TryGetValue(playbackId, out var entry))
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
            _playbacks.Remove(playbackId);
            _sourceToPlayback.Remove(entry.Source);

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
            foreach (var entry in _playbacks.Values)
            {
                ApplyResolvedVolume(entry);
            }
        }

        private void ApplyResolvedVolume(PlaybackEntry entry)
        {
            if (!entry.Source)
            {
                return;
            }

            entry.Source.volume = ResolveVolume(entry.Channel, entry.BaseVolume * entry.FadeFactor);
        }

        private float ResolveVolume(AudioChannel channel, float requestedVolume)
        {
            if (Muted)
            {
                return 0f;
            }

            var channelVolume = channel switch
            {
                AudioChannel.Music => MusicVolume,
                AudioChannel.Ui => UiVolume,
                _ => SfxVolume
            };

            return Mathf.Clamp01(requestedVolume) * Mathf.Clamp01(channelVolume) * Mathf.Clamp01(MasterVolume);
        }

        private static void ConfigureSource(
            AudioSource source,
            AudioClip clip,
            AudioMixerGroup mixerGroup,
            bool loop,
            Vector2 pitchRange,
            int priority,
            bool ignoreListenerPause)
        {
            source.clip = clip;
            source.loop = loop;
            source.outputAudioMixerGroup = mixerGroup;
            source.pitch = UnityEngine.Random.Range(
                Mathf.Max(0.01f, pitchRange.x),
                Mathf.Max(Mathf.Max(0.01f, pitchRange.x), pitchRange.y));
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
        }

        private sealed class PlaybackEntry
        {
            public PlaybackEntry(int playbackId, AudioSource source, AudioChannel channel, float baseVolume, bool isMusic)
            {
                PlaybackId = playbackId;
                Source = source;
                Channel = channel;
                BaseVolume = baseVolume;
                IsMusic = isMusic;
                CancellationTokenSource = new CancellationTokenSource();
            }

            public int PlaybackId { get; }
            public AudioSource Source { get; }
            public AudioChannel Channel { get; }
            public float BaseVolume { get; }
            public bool IsMusic { get; }
            public float FadeFactor { get; set; } = 1f;
            public CancellationTokenSource CancellationTokenSource { get; }
        }
    }
}
