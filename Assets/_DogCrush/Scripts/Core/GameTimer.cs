using UnityEngine;

namespace DogCrush.Core
{
    public class GameTimer : MonoBehaviour
    {
        public float durationSeconds = 60.0f;
        public float RemainingTime { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsPaused { get; private set; }

        public float Progress01 => durationSeconds > 0 ? Mathf.Clamp01(RemainingTime / durationSeconds) : 0f;

        public System.Action<float> OnTimerTick;
        public System.Action OnTenSecondsLeft;
        public System.Action OnTimerExpired;

        private bool warningFired = false;

        public void StartTimer(float customDuration = -1f)
        {
            durationSeconds = customDuration > 0 ? customDuration : 60.0f;
            RemainingTime = durationSeconds;
            IsRunning = true;
            IsPaused = false;
            warningFired = false;
            OnTimerTick?.Invoke(RemainingTime);
        }

        public void StopTimer()
        {
            IsRunning = false;
            IsPaused = false;
        }

        public void SetPaused(bool paused)
        {
            IsPaused = paused && IsRunning;
        }

        public void AddTime(float seconds)
        {
            if (!IsRunning || seconds <= 0f) return;
            RemainingTime = Mathf.Min(durationSeconds, RemainingTime + seconds);
            OnTimerTick?.Invoke(RemainingTime);
        }

        private void Update()
        {
            if (!IsRunning || IsPaused) return;

            RemainingTime -= Time.deltaTime;
            if (RemainingTime < 0) RemainingTime = 0;

            OnTimerTick?.Invoke(RemainingTime);

            if (RemainingTime <= 10.0f && !warningFired)
            {
                warningFired = true;
                OnTenSecondsLeft?.Invoke();
            }

            if (RemainingTime <= 0)
            {
                IsRunning = false;
                OnTimerExpired?.Invoke();
            }
        }
    }
}
