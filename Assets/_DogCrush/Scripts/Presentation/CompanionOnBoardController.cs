using System.Collections;
using DogCrush.Board;
using UnityEngine;

namespace DogCrush.Presentation
{
    /// <summary>Small world-space companion that makes automatic help feel personal.</summary>
    public sealed class CompanionOnBoardController : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private Vector3 home;
        private Coroutine animation;
        private float restingScale = 0.10f;

        public void Setup(Sprite sprite, PieceView anchor)
        {
            spriteRenderer = GetComponent<SpriteRenderer>() ?? gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            // The companion is a friendly spectator, not another board piece.
            // Keep it behind the pieces and small enough to leave every cell readable.
            spriteRenderer.sortingOrder = 2;
            spriteRenderer.color = Color.white;
            if (anchor != null)
            {
                // Keep the dog centred in the breathing room below the board.
                // The small downward offset leaves the bottom row and objective
                // panel readable on narrow mobile screens.
                home = anchor.transform.position + new Vector3(0f, -0.72f, 0f);
                transform.position = home;
            }
            float spriteWidth = sprite != null ? Mathf.Max(0.01f, sprite.bounds.size.x) : 1f;
            restingScale = Mathf.Clamp(0.58f / spriteWidth, 0.055f, 0.12f);
            transform.localScale = Vector3.one * restingScale;
        }

        public void Celebrate(PieceView target)
        {
            if (target == null) return;
            if (animation != null) StopCoroutine(animation);
            animation = StartCoroutine(CelebrateRoutine(target.transform.position));
        }

        private IEnumerator CelebrateRoutine(Vector3 target)
        {
            Vector3 start = transform.position;
            Vector3 hop = target + new Vector3(0f, 0.38f, 0f);
            for (float t = 0f; t < 1f; t += Time.deltaTime * 3.5f)
            {
                transform.position = Vector3.Lerp(start, hop, Mathf.SmoothStep(0f, 1f, t));
                transform.localScale = Vector3.one * Mathf.Lerp(restingScale, restingScale * 1.22f, Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            for (float t = 0f; t < 1f; t += Time.deltaTime * 3.0f)
            {
                transform.position = Vector3.Lerp(hop, home, Mathf.SmoothStep(0f, 1f, t));
                transform.localScale = Vector3.one * Mathf.Lerp(restingScale * 1.22f, restingScale, t);
                yield return null;
            }
            transform.position = home;
            transform.localScale = Vector3.one * restingScale;
            animation = null;
        }
    }
}
