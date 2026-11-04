using System;
using System.Collections.Generic;
using System.Threading;
using _VuTH.Common;
using _VuTH.Core.Persistent.DataPackage;
using Cysharp.Threading.Tasks;

namespace _VuTH.Core.Audio
{
    /// <summary>
    /// Owns the <see cref="AudioSettingsPackage"/> used by <see cref="AudioManager"/>: binds either a package that is
    /// pre-registered in <see cref="DataPersistenceManager"/> or a self-owned one, registers the owned package with the
    /// persistence manager (immediately or once an instance appears), and invokes a change callback whenever any
    /// setting changes (R3 subscriptions also invoke it once on bind).
    /// </summary>
    public sealed class AudioSettingsBinding
    {
        private readonly List<IDisposable> _subscriptions = new();
        private readonly Action _onSettingsChanged;
        private readonly Func<IDataPersistenceManager> _persistenceProvider;

        private CancellationTokenSource _registrationCts;
        private IDataPersistenceManager _persistenceManager;

        /// <param name="onSettingsChanged">Invoked whenever a setting of the bound package emits a value.</param>
        /// <param name="persistenceProvider">
        /// Returns the persistence manager to use, or null when there is none (yet). Defaults to the
        /// <see cref="DataPersistenceManager"/> singleton, for setups without DI.
        /// </param>
        public AudioSettingsBinding(Action onSettingsChanged, Func<IDataPersistenceManager> persistenceProvider = null)
        {
            _onSettingsChanged = onSettingsChanged ?? throw new ArgumentNullException(nameof(onSettingsChanged));
            _persistenceProvider = persistenceProvider ?? DefaultPersistence;
        }

        private static IDataPersistenceManager DefaultPersistence() =>
            DataPersistenceManager.HasInstance ? DataPersistenceManager.Instance : null;

        /// <summary>The persistence manager currently available (alive), or null.</summary>
        public IDataPersistenceManager ResolvePersistence() => _persistenceProvider().OrNull();

        /// <summary>The bound settings package, or null when none is bound.</summary>
        public AudioSettingsPackage Package { get; private set; }

        /// <summary>True when the bound package was created by the audio manager (and must be disposed by it).</summary>
        public bool OwnsPackage { get; private set; }

        /// <summary>True when the bound package is registered in the persistence manager.</summary>
        public bool IsRegistered { get; private set; }

        /// <summary>
        /// Binds <paramref name="package"/>. If it is already bound only the ownership/registration flags are updated.
        /// Otherwise a previously owned package is unregistered (when registered) and disposed, old subscriptions are
        /// disposed and the new package's settings are subscribed.
        /// </summary>
        public void Bind(AudioSettingsPackage package, bool ownsPackage, bool isRegistered)
        {
            if (ReferenceEquals(Package, package))
            {
                OwnsPackage = ownsPackage;
                IsRegistered = isRegistered;
                return;
            }

            if (Package != null && OwnsPackage)
            {
                if (IsRegistered)
                {
                    _persistenceManager.OrNull()?.UnregisterPackage(Package);
                }

                Package.Dispose();
            }

            DisposeSubscriptions();

            Package = package;
            OwnsPackage = ownsPackage;
            IsRegistered = isRegistered;

            _subscriptions.Add(Package.Muted.Subscribe(_ => _onSettingsChanged()));
            _subscriptions.Add(Package.MasterVolume.Subscribe(_ => _onSettingsChanged()));
            _subscriptions.Add(Package.MusicVolume.Subscribe(_ => _onSettingsChanged()));
            _subscriptions.Add(Package.SfxVolume.Subscribe(_ => _onSettingsChanged()));
            _subscriptions.Add(Package.UiVolume.Subscribe(_ => _onSettingsChanged()));
        }

        /// <summary>
        /// Binds the <see cref="AudioSettingsPackage"/> already registered in <see cref="DataPersistenceManager"/>, if any.
        /// </summary>
        public bool TryBindRegisteredPackage()
        {
            var persistence = ResolvePersistence();
            if (persistence == null)
            {
                return false;
            }

            _persistenceManager = persistence;
            if (!_persistenceManager.TryGetPackage<AudioSettingsPackage>(out var package) || package == null)
            {
                return false;
            }

            Bind(package, ownsPackage: false, isRegistered: true);
            return true;
        }

        /// <summary>
        /// Registers the owned, not yet registered package in the persistence manager. If the manager already
        /// holds an <see cref="AudioSettingsPackage"/>, that one is bound instead. Returns false when there is nothing to
        /// register or no persistence manager instance exists yet.
        /// </summary>
        public bool TryRegisterOwnedPackage()
        {
            if (IsRegistered || !OwnsPackage || Package == null)
            {
                return false;
            }

            var persistence = ResolvePersistence();
            if (persistence == null)
            {
                return false;
            }

            _persistenceManager = persistence;
            if (_persistenceManager.TryGetPackage<AudioSettingsPackage>(out var package) && package != null)
            {
                Bind(package, ownsPackage: false, isRegistered: true);
                return true;
            }

            _persistenceManager.RegisterPackage(Package);
            IsRegistered = true;
            return true;
        }

        /// <summary>
        /// Cancels any pending deferred registration and starts a new one that retries
        /// <see cref="TryRegisterOwnedPackage"/> every Update until it succeeds or is cancelled.
        /// </summary>
        public void StartDeferredRegistration()
        {
            CancelRegistration();
            _registrationCts = new CancellationTokenSource();
            RegisterOwnedPackageWhenInstanceAvailableAsync(_registrationCts).Forget();
        }

        /// <summary>Cancels a pending deferred registration, if any.</summary>
        public void CancelRegistration()
        {
            if (_registrationCts == null)
            {
                return;
            }

            _registrationCts.Cancel();
            _registrationCts.Dispose();
            _registrationCts = null;
        }

        /// <summary>Disposes the subscriptions to the bound package's settings.</summary>
        public void DisposeSubscriptions()
        {
            foreach (var subscription in _subscriptions)
            {
                subscription.Dispose();
            }

            _subscriptions.Clear();
        }

        /// <summary>
        /// Releases the bound package: an owned package is saved (fire-and-forget), unregistered when registered and
        /// disposed; a borrowed one is only dropped. Resets the persistence manager reference and all flags.
        /// Does not touch subscriptions; call <see cref="DisposeSubscriptions"/> for that.
        /// </summary>
        public void ReleasePackage()
        {
            if (Package != null)
            {
                if (OwnsPackage)
                {
                    // Release usually happens while quitting / tearing down: an async save may never complete and the
                    // package is unregistered right below (so the persistence lifecycle flush won't see it either).
                    // Write synchronously when the save backend supports it; async only as a fallback.
                    if (Package.IsDirty && !Package.TrySaveImmediate())
                    {
                        Package.SaveNowAsync().Forget();
                    }

                    if (IsRegistered)
                    {
                        _persistenceManager.OrNull()?.UnregisterPackage(Package);
                    }

                    Package.Dispose();
                }
                Package = null;
            }

            _persistenceManager = null;
            OwnsPackage = false;
            IsRegistered = false;
        }

        private async UniTaskVoid RegisterOwnedPackageWhenInstanceAvailableAsync(CancellationTokenSource registrationCts)
        {
            try
            {
                while (!registrationCts.IsCancellationRequested)
                {
                    if (TryRegisterOwnedPackage())
                    {
                        return;
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, registrationCts.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (ReferenceEquals(_registrationCts, registrationCts))
                {
                    _registrationCts.Dispose();
                    _registrationCts = null;
                }
            }
        }
    }
}
