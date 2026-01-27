using System;
using System.Collections.Generic;
using UnityEngine;

namespace _VuTH.Core.Window.Transition
{
    /// <summary>
    /// Simple built-in transition factory based on preset name.
    /// Add this as a component and assign to WindowManager.transitionFactoryComponent.
    /// </summary>
    public sealed class UITransitionPresetFactory : MonoBehaviour, IUITransitionFactory
    {
        [Serializable]
        private struct Preset
        {
            public string name;
            public TransitionKind kind;

            [Header("Common")]
            public float duration;

            [Header("Scale")]
            public AnimationCurve scaleCurve;

            [Header("Slide")]
            public SlideTransition.Direction slideDirection;
            public float slideDistance;
        }

        private enum TransitionKind
        {
            Fade,
            Scale,
            Slide
        }

        [SerializeField] private Preset[] presets =
        {
            new Preset
            {
                name = "Fade",
                kind = TransitionKind.Fade,
                duration = 0.25f,
            },
            new Preset
            {
                name = "Scale",
                kind = TransitionKind.Scale,
                duration = 0.25f,
                scaleCurve = null
            }
        };

        private Dictionary<string, Preset> _map;

        private void Awake()
        {
            EnsureMap();
        }

        private void EnsureMap()
        {
            if (_map != null) return;

            _map = new Dictionary<string, Preset>(StringComparer.OrdinalIgnoreCase);
            if (presets == null) return;

            foreach (var p in presets)
            {
                if (string.IsNullOrWhiteSpace(p.name)) continue;
                _map[p.name] = p;
            }
        }

        public IUITransition Create(UITransitionSettings settings)
        {
            return settings?.Create();
        }

        public IUITransition Create(string presetName)
        {
            if (string.IsNullOrWhiteSpace(presetName))
                return null;

            EnsureMap();

            if (!_map.TryGetValue(presetName, out var p))
            {
                // Back-compat defaults
                if (presetName.Equals("Scale", StringComparison.OrdinalIgnoreCase))
                    return new ScaleTransition();

                if (presetName.Equals("Fade", StringComparison.OrdinalIgnoreCase))
                    return new FadeTransition();

                if (presetName.Equals("Slide", StringComparison.OrdinalIgnoreCase))
                    return new SlideTransition(SlideTransition.Direction.Right);

                return null;
            }

            return p.kind switch
            {
                TransitionKind.Fade => new FadeTransition(p.duration <= 0 ? 0.25f : p.duration),
                TransitionKind.Scale => new ScaleTransition(p.duration <= 0 ? 0.25f : p.duration, p.scaleCurve),
                TransitionKind.Slide => new SlideTransition(p.slideDirection, p.duration <= 0 ? 0.25f : p.duration,
                    p.slideDistance <= 0 ? 1000f : p.slideDistance),
                _ => null
            };
        }
    }
}
