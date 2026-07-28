using System.Runtime.InteropServices;
using UnityEngine;

namespace DogCrush.Presentation
{
    public class HapticFeedbackController : MonoBehaviour
    {
        private const string HapticsPreference = "DogCrush_HapticsEnabled";

        public bool HapticsEnabled { get; private set; } = true;
        public int LastPulseDurationMs { get; private set; }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void DogCrushVibrate(int durationMs);
#endif

        private void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            HapticsEnabled = PlayerPrefs.GetInt(HapticsPreference, 1) == 1;
        }

        public bool ToggleHaptics()
        {
            SetHapticsEnabled(!HapticsEnabled);
            return HapticsEnabled;
        }

        public void SetHapticsEnabled(bool enabled)
        {
            HapticsEnabled = enabled;
            PlayerPrefs.SetInt(HapticsPreference, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void PulseSelection()
        {
            Pulse(8);
        }

        public void PulseMatch(int chainLength)
        {
            Pulse(Mathf.Clamp(18 + chainLength * 3, 24, 48));
        }

        public void PulseGameOver()
        {
            Pulse(55);
        }

        private void Pulse(int durationMs)
        {
            if (!HapticsEnabled)
            {
                return;
            }

            LastPulseDurationMs = durationMs;

#if UNITY_WEBGL && !UNITY_EDITOR
            DogCrushVibrate(durationMs);
#elif UNITY_ANDROID && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }
    }
}
