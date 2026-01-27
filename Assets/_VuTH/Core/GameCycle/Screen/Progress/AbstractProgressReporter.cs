using System;
using Cysharp.Threading.Tasks;

namespace _VuTH.Core.GameCycle.Screen.Progress
{
    public abstract class AbstractProgressReporter : IProgress<float>
    {
        public abstract UniTask CompleteAsync();
        public abstract void Report(float value);
    }
}