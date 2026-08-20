using UnityEngine;

namespace DogCrush.Core
{
    [System.Flags]
    public enum TimerPauseReason
    {
        None = 0,
        Menu = 1 << 0,
        Resolving = 1 << 1,
        Intro = 1 << 2
    }

    public class GameTimer : MonoBehaviour
    {
        public float durationSeconds = 60.0f;
        public float RemainingTime { get; private set; }
        public bool IsRunning { get; private set; }
        private TimerPauseReason pauseReasons;
        public bool IsPaused => IsRunning && pauseReasons != TimerPauseReason.None;

        public float Progress01 => durationSeconds > 0 ? Mathf.Clamp01(RemainingTime / durationSeconds) : 0f;

        public System.Action<float> OnTimerTick;
        public System.Action OnTenSecondsLeft;
        public System.Action OnTimerExpired;
        public System.Action<float> OnTimeGranted;

        private bool warningFired = false;

        public void StartTimer(float customDuration = -1f)
        {
            durationSeconds = customDuration > 0 ? customDuration : 60.0f;
            RemainingTime = durationSeconds;
            IsRunning = true;
            pauseReasons = TimerPauseReason.None;
            warningFired = false;
            OnTimerTick?.Invoke(RemainingTime);
        }

        public void StopTimer()
        {
            IsRunning = false;
            pauseReasons = TimerPauseReason.None;
        }

        public void SetPaused(bool paused)
        {
            SetPaused(paused, TimerPauseReason.Menu);
        }

        public void SetPaused(bool paused, TimerPauseReason reason)
        {
            if (reason == TimerPauseReason.None) return;
            if (paused) pauseReasons |= reason;
            else pauseReasons &= ~reason;
        }

        public float AddTime(float seconds)
        {
            if (!IsRunning || seconds <= 0f) return 0f;
            float previous = RemainingTime;
            RemainingTime = Mathf.Min(durationSeconds, RemainingTime + seconds);
            float granted = RemainingTime - previous;
            if (granted <= 0f) return 0f;

            if (RemainingTime > 10f) warningFired = false;
            OnTimerTick?.Invoke(RemainingTime);
            OnTimeGranted?.Invoke(granted);
            return granted;
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
