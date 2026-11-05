using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace _VuTH.Core.Window.Transition
{
    /// <summary>
    /// Plays several transitions together (<see cref="Parallel"/>) or one after another. E.g. Fade + Slide in parallel
    /// for a sliding fade. When two steps drive the same property (the alpha of Fade and Scale), the last one wins.
    /// </summary>
    public sealed class SequenceTransition : IUITransition
    {
        private readonly IReadOnlyList<IUITransition> _steps;

        public bool Parallel { get; }

        public float Duration { get; }

        public SequenceTransition(IReadOnlyList<IUITransition> steps, bool parallel)
        {
            _steps = steps ?? Array.Empty<IUITransition>();
            Parallel = parallel;

            var duration = 0f;
            foreach (var step in _steps)
            {
                if (step == null) continue;
                duration = parallel ? Math.Max(duration, step.Duration) : duration + step.Duration;
            }

            Duration = duration;
        }

        public UniTask In(IUIView view) => Run(step => step.In(view));

        public UniTask Out(IUIView view) => Run(step => step.Out(view));

        private async UniTask Run(Func<IUITransition, UniTask> play)
        {
            if (Parallel)
            {
                var running = new List<UniTask>(_steps.Count);
                foreach (var step in _steps)
                {
                    if (step != null) running.Add(play(step));
                }

                await UniTask.WhenAll(running);
                return;
            }

            foreach (var step in _steps)
            {
                if (step != null) await play(step);
            }
        }
    }

    [Serializable]
    [TransitionSettingsName("Sequence")]
    public sealed class SequenceTransitionSettings : UITransitionSettings
    {
        [UnityEngine.Tooltip("Play the steps together (true) or one after another (false). The duration field is unused.")]
        public bool parallel = true;

        [UnityEngine.SerializeReference]
        [TransitionSettingsReference]
        public List<UITransitionSettings> steps = new();

        public override IUITransition Create()
        {
            var created = new List<IUITransition>(steps.Count);
            foreach (var step in steps)
            {
                // A sequence inside itself would recurse forever.
                if (step != null && !ReferenceEquals(step, this)) created.Add(step.Create());
            }

            return new SequenceTransition(created, parallel);
        }
    }
}
