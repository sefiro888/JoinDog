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

            StartCoroutine(AnimateFloatingText(go, rect, tmp));
        }

        private IEnumerator AnimateFloatingText(GameObject go, RectTransform rect, TextMeshProUGUI tmp)
        {
            float duration = 0.8f;
            float elapsed = 0f;
            Vector2 startPos = rect.anchoredPosition;
            Vector2 endPos = startPos + new Vector2(0f, 60f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                rect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
                tmp.alpha = Mathf.Lerp(1f, 0f, t);
                yield return null;
            }

            Destroy(go);
        }

        public void TriggerCameraShake(float intensity = 0.15f, float duration = 0.2f)
        {
            if (mainCamera != null && gameObject.activeInHierarchy)
            {
                StartCoroutine(CameraShakeRoutine(intensity, duration));
            }
        }

        private IEnumerator CameraShakeRoutine(float intensity, float duration)
        {
            Vector3 originalPos = mainCamera.transform.position;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                Vector3 randomOffset = (Vector3)Random.insideUnitCircle * intensity;
                mainCamera.transform.position = originalPos + randomOffset;
                yield return null;
            }

            mainCamera.transform.position = originalPos;
        }
    }
}
