using System;

namespace Core.GameCycle.Screen.Loading
{
    public class LoadingHandler
    {
        private readonly IProgress<float> _reporter;
        private readonly int _totalAmount;
        
        private readonly float _startProgress;
        private readonly float _totalProgress;
        private int _currentAmount;
        
        public LoadingHandler(IProgress<float> reporter, int totalAmount, float startProgress = 0f)
        {
            _reporter = reporter;
            _totalAmount = totalAmount;
            _currentAmount = 0;
            _startProgress = startProgress;
            _totalProgress = 1f - startProgress;
        }

        public void Increment()
        {
            _currentAmount++;
            var progress = _startProgress + _totalProgress * ((float)_currentAmount / _totalAmount);
            _reporter.Report(progress);
        }
    }
}