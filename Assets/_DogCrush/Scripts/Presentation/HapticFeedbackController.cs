using System.Runtime.InteropServices;
using DogCrush.Board;
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

        /// <summary>
        /// A short tactile signature for every special. The WebGL bridge only
        /// supports one vibration duration, so each effect is recognisable by
        /// a deliberately tuned pulse length.
        /// </summary>
        public void PulseSpecial(PieceSpecialType type, bool mega = false)
        {
            int duration = mega ? 118 : type switch
            {
                PieceSpecialType.RowBlast => 54,
                PieceSpecialType.ColumnBlast => 62,
                PieceSpecialType.AreaBlast => 78,
                PieceSpecialType.ColorBurst => 92,
                PieceSpecialType.MegaBurst => 118,
                PieceSpecialType.BallBounce => 126,
                PieceSpecialType.Whistle => 86,
                _ => 46
            };
            Pulse(duration);
        }

        public void PulseSpecialCombo(SpecialComboKind combo)
        {
            int duration = combo switch
            {
                SpecialComboKind.BoardNova => 142,
                SpecialComboKind.ColorSweep => 116,
                SpecialComboKind.DoubleArea => 104,
                SpecialComboKind.WideRow or SpecialComboKind.WideColumn => 90,
                _ => 74
            };
            Pulse(duration);
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
