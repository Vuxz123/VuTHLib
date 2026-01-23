using System.Collections.Generic;
using UnityEngine;
using Core.Window.Transition.Workers;
using ZLinq;

namespace Core.Window.Transition
{
    /// <summary>
    /// Worker-based transition factory.
    /// Add this as a component and assign to WindowManager.transitionFactoryComponent.
    /// </summary>
    public sealed class UITransitionFactory : MonoBehaviour, IUITransitionFactory
    {
        [Tooltip("If empty, uses built-in default workers (Fade/Scale/Slide).")]
        [SerializeField] private bool useDefaultWorkers = true;

        private readonly List<UITransitionWorkerBase> _workers = new();

        private void Awake()
        {
            RebuildWorkers();
        }

        private void RebuildWorkers()
        {
            _workers.Clear();

            if (useDefaultWorkers)
            {
                _workers.Add(new FadeTransitionWorker());
                _workers.Add(new ScaleTransitionWorker());
                _workers.Add(new SlideTransitionWorker());
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

            if (_workers.Count == 0)
                RebuildWorkers();

            return (_workers
                .AsValueEnumerable()
                .Where(w => w != null)
                .Where(w => w.CanHandle(presetName))
                .Select(w => w.Create(presetName))).FirstOrDefault();
        }
    }
}
