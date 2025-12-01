#define FR2_DEBUG

using System;
using UnityEditor;
using UnityEngine;

namespace vietlabs.fr2
{
    public class FR2_TimeSlice
    {
        private readonly Action onCompleteCallback;
        private readonly Action<int> processingAction;
        private readonly Func<int> targetCountFunc;

        private int _currentIndex;
        public readonly float timeSlice = 1 / 25f;
        public int currentIndex => _currentIndex;
        
        public FR2_TimeSlice(Func<int> countFunc, Action<int> action, Action onComplete = null)
        {
            targetCountFunc = countFunc;
            processingAction = action;
            onCompleteCallback = onComplete;
        }

        public void Start()
        {
            _currentIndex = 0;
            
            EditorApplication.update -= ProcessQueue;
            EditorApplication.update += ProcessQueue;
        }

        public void Stop()
        {
            EditorApplication.update -= ProcessQueue;
        }

        private void ProcessQueue()
        {
            var startTime = Time.realtimeSinceStartup;
            var targetCount = targetCountFunc.Invoke();
            
            while (_currentIndex < targetCount)
            {
                processingAction.Invoke(_currentIndex);
                _currentIndex++;

                if (Time.realtimeSinceStartup - startTime < timeSlice) continue;
                
                var pct = _currentIndex * 100f / targetCount;
                FR2_LOG.Log($"Progress: {pct:0.00}% -  {_currentIndex}/{targetCount}");
                return;
            }

            targetCount = targetCountFunc.Invoke();

            // Stop processing if we've reached the target count
            if (_currentIndex < targetCount) return;

            EditorApplication.update -= ProcessQueue;
            onCompleteCallback?.Invoke();
        }
    }
}
