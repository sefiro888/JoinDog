using UnityEngine;
using UnityEngine.EventSystems;

namespace JoinDog.App
{
    public sealed class JoinDogButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private Vector3 restingScale;
        private Vector3 targetScale;

        private void Awake()
        {
            restingScale = transform.localScale;
            targetScale = restingScale;
        }

        private void Update()
        {
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale,
                1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            targetScale = restingScale * 0.955f;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            targetScale = restingScale * 1.025f;
            CancelInvoke(nameof(ReturnToRest));
            Invoke(nameof(ReturnToRest), 0.08f);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ReturnToRest();
        }

        private void ReturnToRest()
        {
            targetScale = restingScale;
        }
    }
}
