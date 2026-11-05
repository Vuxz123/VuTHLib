using System;
using System.Collections.Generic;
using _VuTH.Core.Window.Transition.Workers;
using UnityEngine;

namespace _VuTH.Core.Window.Transition
{
    /// <summary>
    /// Creates transitions from settings or from a preset name. Preset names are resolved, in order, by the preset
    /// assets, the workers registered in code (<see cref="RegisterWorker"/>), then the built-in workers
    /// (Fade, Scale, Slide, Slide:Left|Right|Top|Bottom).
    /// Add this as a component and assign to WindowManager.transitionFactoryComponent.
    /// </summary>
    public sealed class UITransitionFactory : MonoBehaviour, IUITransitionFactory
    {
        [Tooltip("Named presets made in the Editor; they override built-in presets of the same name.")]
        [SerializeField] private List<UITransitionPresetAsset> presets = new();

        [Tooltip("Include the built-in Fade / Scale / Slide workers.")]
        [SerializeField] private bool useDefaultWorkers = true;

        private readonly List<UITransitionWorkerBase> _customWorkers = new();
        private readonly List<UITransitionWorkerBase> _defaultWorkers = new();

        private void Awake()
        {
            BuildDefaultWorkers();
        }

        private void BuildDefaultWorkers()
        {
            _defaultWorkers.Clear();
            if (!useDefaultWorkers) return;

            _defaultWorkers.Add(new FadeTransitionWorker());
            _defaultWorkers.Add(new ScaleTransitionWorker());
            _defaultWorkers.Add(new SlideTransitionWorker());
        }

        /// <summary>Adds a worker resolving preset names; checked before the built-in workers.</summary>
        public void RegisterWorker(UITransitionWorkerBase worker)
        {
            if (worker != null && !_customWorkers.Contains(worker)) _customWorkers.Add(worker);
        }

        /// <summary>Adds a preset asset at runtime.</summary>
        public void RegisterPreset(UITransitionPresetAsset preset)
        {
            if (preset && !presets.Contains(preset)) presets.Add(preset);
        }

        public IUITransition Create(UITransitionSettings settings)
        {
            return settings?.Create();
        }

        public IUITransition Create(string presetName)
        {
            if (string.IsNullOrWhiteSpace(presetName))
                return null;

            presetName = presetName.Trim();

            foreach (var preset in presets)
            {
                if (preset && string.Equals(preset.PresetName, presetName, StringComparison.OrdinalIgnoreCase))
                    return preset.Create();
            }

            if (_defaultWorkers.Count == 0 && useDefaultWorkers)
                BuildDefaultWorkers();

            return CreateWith(_customWorkers, presetName) ?? CreateWith(_defaultWorkers, presetName);
        }

        private static IUITransition CreateWith(List<UITransitionWorkerBase> workers, string presetName)
        {
            foreach (var worker in workers)
            {
                if (worker != null && worker.CanHandle(presetName)) return worker.Create(presetName);
            }

            return null;
        }
    }
}
