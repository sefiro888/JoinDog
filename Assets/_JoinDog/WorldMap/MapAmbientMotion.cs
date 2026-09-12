using UnityEngine;

namespace JoinDog.App
{
    /// <summary>
    /// Lightweight unscaled animation for decorative map elements. Each item
    /// remains independent from the path and can be pooled in future worlds.
    /// </summary>
    public sealed class MapAmbientMotion : MonoBehaviour
    {
        public Vector2 drift = new Vector2(12f, 8f);
        public float speed = 0.7f;
        public float phase;
        public float rotationAmplitude = 3f;

        private RectTransform rect;
        private Vector2 origin;
        private float nextVisibilityCheck;
        private bool visibleOnScreen = true;

        private void Awake()
        {
            rect = transform as RectTransform;
            if (rect != null) origin = rect.anchoredPosition;
        }

        private void Update()
        {
            if (rect == null) return;
            if (AccessibilitySettings.ReducedMotion)
            {
                rect.anchoredPosition = origin;
                rect.localRotation = Quaternion.identity;
                return;
            }

            if (Time.unscaledTime >= nextVisibilityCheck)
            {
                nextVisibilityCheck = Time.unscaledTime + 0.25f;
                visibleOnScreen = IsVisibleOnScreen();
            }
            if (!visibleOnScreen) return;

            float time = Time.unscaledTime * speed + phase;
            rect.anchoredPosition = origin + new Vector2(
                Mathf.Sin(time) * drift.x,
                Mathf.Cos(time * 0.83f) * drift.y);
            rect.localRotation = Quaternion.Euler(0f, 0f,
                Mathf.Sin(time * 0.61f) * rotationAmplitude);
        }

        private bool IsVisibleOnScreen()
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            const float margin = 96f;
            float minX = Mathf.Min(corners[0].x, corners[2].x);
            float maxX = Mathf.Max(corners[0].x, corners[2].x);
            float minY = Mathf.Min(corners[0].y, corners[2].y);
            float maxY = Mathf.Max(corners[0].y, corners[2].y);
            return maxX >= -margin && minX <= Screen.width + margin &&
                maxY >= -margin && minY <= Screen.height + margin;
        }
    }
}
