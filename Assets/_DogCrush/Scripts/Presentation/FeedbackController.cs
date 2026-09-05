using System.Collections;
using TMPro;
using UnityEngine;

namespace DogCrush.Presentation
{
    public class FeedbackController : MonoBehaviour
    {
        public Camera mainCamera;
        public Canvas uiCanvas;
        public GameObject floatingTextPrefab;
        private Coroutine shakeCoroutine;
        private Vector3 cameraRestPosition;
        private bool cameraRestCaptured;

        public static string CelebrationTitle(int matchCount, int cascadeDepth)
        {
            if (cascadeDepth > 0) return $"¡COMBO ×{cascadeDepth + 1}!";
            return matchCount >= 6 ? "¡ESPECTACULAR!" : matchCount == 5 ? "¡INCREÍBLE!" :
                matchCount == 4 ? "¡GENIAL!" : string.Empty;
        }

        private void Awake()
        {
            if (mainCamera == null) mainCamera = Camera.main;
        }

        public void SpawnFloatingText(Vector3 worldPos, string message, Color textColor, float fontSize = 28f)
        {
            if (uiCanvas == null) return;

            GameObject go = new GameObject("FloatingText", typeof(RectTransform), typeof(CanvasGroup), typeof(TextMeshProUGUI));
            go.transform.SetParent(uiCanvas.transform, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            Vector2 screenPos = mainCamera != null ? (Vector2)mainCamera.WorldToScreenPoint(worldPos) : Vector2.zero;
            
            // Convert screen pos to canvas local pos
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                uiCanvas.transform as RectTransform,
                screenPos,
                uiCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCamera,
                out Vector2 localPos
            );

            rect.anchoredPosition = localPos;

            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = message;
            tmp.fontSize = fontSize;
            tmp.color = textColor;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = false;
            tmp.outlineColor = new Color(.03f, .12f, .20f);
            tmp.outlineWidth = .16f;
            rect.sizeDelta = new Vector2(480f, 90f);

            StartCoroutine(AnimateFloatingText(go, rect, tmp));
        }

        private IEnumerator AnimateFloatingText(GameObject go, RectTransform rect, TextMeshProUGUI tmp)
        {
            float duration = 0.8f;
            float elapsed = 0f;
            Vector2 startPos = rect.anchoredPosition;
            bool reduced = JoinDog.App.AccessibilitySettings.ReducedMotion;
            Vector2 endPos = startPos + new Vector2(0f, reduced ? 0f : 45f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                rect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
                tmp.alpha = Mathf.Lerp(1f, 0f, t);
                rect.localScale = Vector3.one * (reduced ? 1f : 1f + .12f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t * 3f)));
                yield return null;
            }

            Destroy(go);
        }

        public void TriggerCameraShake(float intensity = 0.15f, float duration = 0.2f)
        {
            if (mainCamera == null || !gameObject.activeInHierarchy || JoinDog.App.AccessibilitySettings.ReducedMotion) return;

            if (!cameraRestCaptured)
            {
                cameraRestPosition = mainCamera.transform.position;
                cameraRestCaptured = true;
            }

            if (shakeCoroutine != null)
            {
                StopCoroutine(shakeCoroutine);
                mainCamera.transform.position = cameraRestPosition;
            }
            shakeCoroutine = StartCoroutine(CameraShakeRoutine(intensity, duration));
        }

        public void InvalidateCameraRestPosition()
        {
            if (shakeCoroutine != null)
            {
                StopCoroutine(shakeCoroutine);
                shakeCoroutine = null;
            }
            cameraRestCaptured = false;
        }

        private IEnumerator CameraShakeRoutine(float intensity, float duration)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float falloff = 1f - Mathf.Clamp01(elapsed / duration);
                Vector3 randomOffset = (Vector3)Random.insideUnitCircle * intensity * falloff * falloff;
                mainCamera.transform.position = cameraRestPosition + randomOffset;
                yield return null;
            }

            mainCamera.transform.position = cameraRestPosition;
            shakeCoroutine = null;
        }
    }
}
