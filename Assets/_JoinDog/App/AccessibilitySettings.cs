using System;
using UnityEngine;

namespace JoinDog.App
{
    /// <summary>
    /// Small, local-only accessibility profile shared by the campaign map and
    /// gameplay. PlayerPrefs keeps the choices available in WebGL/PWA builds.
    /// </summary>
    public static class AccessibilitySettings
    {
        private const string ReducedMotionKey = "JoinDog_ReducedMotion";
        private const string HighContrastObstaclesKey = "JoinDog_HighContrastObstacles";

        public static event Action Changed;

        public static bool ReducedMotion
        {
            get => PlayerPrefs.GetInt(ReducedMotionKey, 0) != 0;
            set => SetBool(ReducedMotionKey, value);
        }

        public static bool HighContrastObstacles
        {
            get => PlayerPrefs.GetInt(HighContrastObstaclesKey, 0) != 0;
            set => SetBool(HighContrastObstaclesKey, value);
        }

        private static void SetBool(string key, bool value)
        {
            int encoded = value ? 1 : 0;
            if (PlayerPrefs.GetInt(key, 0) == encoded) return;
            PlayerPrefs.SetInt(key, encoded);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
