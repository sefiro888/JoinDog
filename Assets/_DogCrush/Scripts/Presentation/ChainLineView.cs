using System.Collections.Generic;
using DogCrush.Board;
using UnityEngine;

namespace DogCrush.Presentation
{
    [RequireComponent(typeof(LineRenderer))]
    public class ChainLineView : MonoBehaviour
    {
        public LineRenderer lineRenderer;
        public float startWidth = 0.13f;
        public float endWidth = 0.13f;

        private Color activeColor = new Color(1f, 0.82f, 0.22f, 0.88f);

        private void Awake()
        {
            if (lineRenderer == null)
                lineRenderer = GetComponent<LineRenderer>();

            lineRenderer.useWorldSpace = true;
            lineRenderer.startWidth = startWidth;
            lineRenderer.endWidth = endWidth;
            lineRenderer.positionCount = 0;
            lineRenderer.sortingOrder = 18;
            lineRenderer.textureMode = LineTextureMode.Tile;
            lineRenderer.numCapVertices = 8;
            lineRenderer.numCornerVertices = 8;
            ApplyActiveColor();
        }

        private void Update()
        {
            if (lineRenderer != null && lineRenderer.positionCount > 0)
            {
                float pulse = startWidth + Mathf.Sin(Time.time * 8f) * 0.018f;
                lineRenderer.startWidth = pulse;
                lineRenderer.endWidth = pulse;
            }
        }

        public void SetChainType(PieceType type)
        {
            activeColor = type switch
            {
                PieceType.Dog => new Color(1f, 0.66f, 0.18f, 0.90f),
                PieceType.Bone => new Color(1f, 0.95f, 0.72f, 0.90f),
                PieceType.Ball => new Color(0.24f, 0.78f, 1f, 0.90f),
                PieceType.Food => new Color(1f, 0.34f, 0.28f, 0.90f),
                PieceType.Collar => new Color(0.32f, 0.95f, 0.48f, 0.90f),
                _ => new Color(1f, 0.82f, 0.22f, 0.88f)
            };
            ApplyActiveColor();
        }

        private void ApplyActiveColor()
        {
            if (lineRenderer == null) return;

            Color tailColor = activeColor;
            tailColor.a *= 0.72f;
            lineRenderer.startColor = activeColor;
            lineRenderer.endColor = tailColor;
        }

        public void UpdateLine(List<PieceView> chain)
        {
            RenderChain(chain, Vector2.zero, false);
        }

        public void UpdateLine(List<PieceView> chain, Vector2 pointerWorldPos)
        {
            RenderChain(chain, pointerWorldPos, true);
        }

        private void RenderChain(List<PieceView> chain, Vector2 pointerWorldPos, bool includePointer)
        {
            if (lineRenderer == null) return;

            if (chain == null || chain.Count == 0)
            {
                lineRenderer.positionCount = 0;
                return;
            }

            bool hasPointer = includePointer &&
                              chain.Count > 0 &&
                              Vector2.Distance(
                                  pointerWorldPos,
                                  chain[chain.Count - 1].transform.position) > 0.08f;
            int count = chain.Count + (hasPointer ? 1 : 0);
            lineRenderer.positionCount = count;

            for (int i = 0; i < chain.Count; i++)
            {
                Vector3 piecePos = chain[i].transform.position;
                piecePos.z = -0.9f;
                lineRenderer.SetPosition(i, piecePos);
            }

            if (hasPointer)
            {
                Vector2 lastPosition = chain[chain.Count - 1].transform.position;
                Vector2 delta = pointerWorldPos - lastPosition;

                // Keep the preview tail orthogonal so the gesture communicates
                // the same horizontal/vertical rule used by the board logic.
                if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
                    delta.y = 0f;
                else
                    delta.x = 0f;

                delta = Vector2.ClampMagnitude(delta, 0.72f);
                Vector2 snappedPointer = lastPosition + delta;
                Vector3 dragPos = new Vector3(snappedPointer.x, snappedPointer.y, -0.9f);
                lineRenderer.SetPosition(count - 1, dragPos);
            }
        }

        public void ClearLine()
        {
            if (lineRenderer != null)
            {
                lineRenderer.positionCount = 0;
            }
        }
    }
}
