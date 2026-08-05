using System.Collections;
using UnityEngine;

namespace DogCrush.Board
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class PieceView : MonoBehaviour
    {
        public PieceType type = PieceType.None;
        public int gridX;
        public int gridY;

        [Header("Renderers & Visuals")]
        public SpriteRenderer mainRenderer;
        public SpriteRenderer selectionGlow;
        public SpriteRenderer shadowRenderer;

        private Vector3 defaultScale = Vector3.one * 0.95f;
        private Color baseColor = Color.white;
        private int defaultMainSortingOrder;
        private int defaultGlowSortingOrder;
        private CircleCollider2D interactionCollider;
        private Coroutine moveCoroutine;
        private Coroutine pulseCoroutine;
        private Coroutine specialPulseCoroutine;
        private Coroutine specialActionCoroutine;
        private Transform specialVisualRoot;
        private SpriteRenderer specialRingRenderer;
        private SpriteRenderer specialBarA;
        private SpriteRenderer specialBarB;
        private static Sprite whiteSquareSprite;
        private static Sprite specialRingSprite;
        private static Sprite specialArrowSprite;
        private static Sprite specialBurstSprite;

        public bool IsSelected { get; private set; }
        public PieceSpecialType SpecialType { get; private set; }
        public bool IsSpecial => SpecialType != PieceSpecialType.None;

        private void Awake()
        {
            if (mainRenderer == null)
                mainRenderer = GetComponent<SpriteRenderer>();
            interactionCollider = GetComponent<CircleCollider2D>();
            defaultMainSortingOrder = mainRenderer != null ? mainRenderer.sortingOrder : 10;
            defaultGlowSortingOrder = selectionGlow != null ? selectionGlow.sortingOrder : 9;

            defaultScale = transform.localScale;
            if (defaultScale == Vector3.zero) defaultScale = Vector3.one * 0.95f;

            SetSelected(false);
        }

        public void Initialize(PieceType pieceType, int x, int y, Sprite iconSprite, Color pieceColor)
        {
            type = pieceType;
            gridX = x;
            gridY = y;
            name = $"Piece_{x}_{y}_{pieceType}";

            if (mainRenderer != null)
            {
                mainRenderer.sprite = iconSprite;
                mainRenderer.color = pieceColor;
                mainRenderer.sortingOrder = defaultMainSortingOrder;
                baseColor = pieceColor;
            }

            NormalizeVisualSize(pieceType, iconSprite);
            SetSpecial(PieceSpecialType.None);
            SetSelected(false);
        }

        private void NormalizeVisualSize(PieceType pieceType, Sprite iconSprite)
        {
            if (iconSprite == null)
            {
                defaultScale = Vector3.one;
                transform.localScale = defaultScale;
                return;
            }

            Vector2 spriteSize = iconSprite.bounds.size;
            float largestSide = Mathf.Max(spriteSize.x, spriteSize.y);
            float targetVisualSize = pieceType switch
            {
                PieceType.Dog => 0.60f,
                PieceType.Bone => 0.53f,
                PieceType.Ball => 0.55f,
                PieceType.Food => 0.57f,
                PieceType.Collar => 0.57f,
                _ => 0.55f
            };
            float uniformScale = largestSide > 0.001f
                ? targetVisualSize / largestSide
                : 1f;

            defaultScale = Vector3.one * uniformScale;
            transform.localScale = defaultScale;
            RefreshSpecialVisualScale();

            // The illustrations are normalized by scaling the piece root.
            // Compensate the collider so its world-space hit area remains
            // finger-friendly instead of shrinking to just a few pixels.
            if (interactionCollider != null && uniformScale > 0.001f)
            {
                const float desiredWorldRadius = 0.25f;
                interactionCollider.radius = desiredWorldRadius / uniformScale;
            }
        }

        public void SetGridPosition(int x, int y)
        {
            gridX = x;
            gridY = y;
        }

        public void SetSpecial(PieceSpecialType specialType)
        {
            SpecialType = specialType;
            EnsureSpecialVisuals();
            if (specialVisualRoot != null)
                specialVisualRoot.gameObject.SetActive(specialType != PieceSpecialType.None);

            if (specialType == PieceSpecialType.None)
            {
                if (specialActionCoroutine != null)
                {
                    StopCoroutine(specialActionCoroutine);
                    specialActionCoroutine = null;
                }
                transform.localScale = defaultScale;
                if (mainRenderer != null) mainRenderer.color = baseColor;
                RefreshSpecialVisualScale();
                if (specialPulseCoroutine != null)
                {
                    StopCoroutine(specialPulseCoroutine);
                    specialPulseCoroutine = null;
                }
                return;
            }

            ConfigureSpecialMarkers(specialType);
            if (specialRingRenderer != null)
            {
                specialRingRenderer.color = specialType switch
                {
                    PieceSpecialType.RowBlast => new Color(0.10f, 0.88f, 1f, 0.82f),
                    PieceSpecialType.ColumnBlast => new Color(0.72f, 0.32f, 1f, 0.84f),
                    _ => new Color(1f, 0.28f, 0.72f, 0.90f)
                };
            }
            if (specialPulseCoroutine == null && gameObject.activeInHierarchy)
                specialPulseCoroutine = StartCoroutine(SpecialPulseAnimation());
        }

        public void PlaySpecialCreationAnimation()
        {
            if (!gameObject.activeInHierarchy || !IsSpecial) return;
            if (specialActionCoroutine != null) StopCoroutine(specialActionCoroutine);
            specialActionCoroutine = StartCoroutine(SpecialCreationRoutine());
        }

        public void PlaySpecialChargeAnimation(float duration = 0.18f)
        {
            if (!gameObject.activeInHierarchy || !IsSpecial) return;
            if (specialActionCoroutine != null) StopCoroutine(specialActionCoroutine);
            specialActionCoroutine = StartCoroutine(SpecialChargeRoutine(duration));
        }

        private IEnumerator SpecialCreationRoutine()
        {
            Vector3 visualScale = specialVisualRoot != null ? specialVisualRoot.localScale : Vector3.one;
            float elapsed = 0f;
            const float duration = 0.42f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float bounce = t < 0.58f
                    ? Mathf.Lerp(0.20f, 1.24f, Mathf.SmoothStep(0f, 1f, t / 0.58f))
                    : Mathf.Lerp(1.24f, 1f, Mathf.SmoothStep(0f, 1f, (t - 0.58f) / 0.42f));
                if (specialVisualRoot != null)
                {
                    specialVisualRoot.localScale = visualScale * bounce;
                    specialVisualRoot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-22f, 0f, t));
                }
                transform.localScale = defaultScale * Mathf.Lerp(0.88f, 1f, t);
                if (mainRenderer != null)
                    mainRenderer.color = Color.Lerp(Color.white, baseColor, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            transform.localScale = defaultScale;
            if (mainRenderer != null) mainRenderer.color = baseColor;
            RefreshSpecialVisualScale();
            if (specialVisualRoot != null) specialVisualRoot.localRotation = Quaternion.identity;
            specialActionCoroutine = null;
        }

        private IEnumerator SpecialChargeRoutine(float duration)
        {
            duration = Mathf.Max(0.08f, duration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float pulse = Mathf.Sin(t * Mathf.PI);
                transform.localScale = defaultScale * (1f + pulse * 0.22f);
                if (mainRenderer != null)
                    mainRenderer.color = Color.Lerp(baseColor, Color.white, pulse * 0.86f);
                if (specialVisualRoot != null)
                    specialVisualRoot.localRotation = Quaternion.Euler(0f, 0f, pulse * 12f);
                yield return null;
            }

            transform.localScale = defaultScale;
            if (mainRenderer != null) mainRenderer.color = baseColor;
            RefreshSpecialVisualScale();
            if (specialVisualRoot != null) specialVisualRoot.localRotation = Quaternion.identity;
            specialActionCoroutine = null;
        }

        private void EnsureSpecialVisuals()
        {
            if (specialVisualRoot != null) return;
            GameObject root = new GameObject("SpecialVisual");
            root.transform.SetParent(transform, false);
            specialVisualRoot = root.transform;

            GameObject ring = new GameObject("SpecialRing", typeof(SpriteRenderer));
            ring.transform.SetParent(specialVisualRoot, false);
            specialRingRenderer = ring.GetComponent<SpriteRenderer>();
            specialRingRenderer.sprite = GetSpecialRingSprite();
            specialRingRenderer.sortingOrder = defaultMainSortingOrder - 1;
            ring.transform.localScale = Vector3.one * 0.78f;

            specialBarA = CreateSpecialBar("SpecialBarA");
            specialBarB = CreateSpecialBar("SpecialBarB");
            specialBarB.color = new Color(0.32f, 0.92f, 1f, 0.92f);
            RefreshSpecialVisualScale();
            root.SetActive(false);
        }

        private SpriteRenderer CreateSpecialBar(string objectName)
        {
            GameObject bar = new GameObject(objectName, typeof(SpriteRenderer));
            bar.transform.SetParent(specialVisualRoot, false);
            bar.transform.localScale = new Vector3(0.58f, 0.095f, 1f);
            SpriteRenderer renderer = bar.GetComponent<SpriteRenderer>();
            renderer.sprite = GetWhiteSquareSprite();
            renderer.sortingOrder = defaultMainSortingOrder + 4;
            return renderer;
        }

        private void ConfigureSpecialMarkers(PieceSpecialType specialType)
        {
            if (specialBarA == null || specialBarB == null) return;
            bool area = specialType == PieceSpecialType.AreaBlast;
            specialBarA.gameObject.SetActive(true);
            specialBarB.gameObject.SetActive(true);

            if (area)
            {
                specialBarA.sprite = GetSpecialBurstSprite();
                specialBarB.sprite = GetSpecialRingSprite();
                specialBarA.transform.localPosition = Vector3.zero;
                specialBarB.transform.localPosition = Vector3.zero;
                specialBarA.transform.localRotation = Quaternion.identity;
                specialBarB.transform.localRotation = Quaternion.identity;
                specialBarA.transform.localScale = Vector3.one * 0.76f;
                specialBarB.transform.localScale = Vector3.one * 0.55f;
                specialBarA.sortingOrder = defaultMainSortingOrder - 2;
                specialBarB.sortingOrder = defaultMainSortingOrder + 3;
                specialBarA.color = new Color(1f, 0.22f, 0.70f, 0.72f);
                specialBarB.color = new Color(1f, 0.88f, 0.18f, 0.92f);
                return;
            }

            specialBarA.sprite = GetSpecialArrowSprite();
            specialBarB.sprite = GetSpecialArrowSprite();
            specialBarA.sortingOrder = defaultMainSortingOrder + 3;
            specialBarB.sortingOrder = defaultMainSortingOrder + 3;
            specialBarA.transform.localScale = Vector3.one * 0.20f;
            specialBarB.transform.localScale = Vector3.one * 0.20f;
            Color markerColor = specialType == PieceSpecialType.RowBlast
                ? new Color(1f, 0.82f, 0.12f, 1f)
                : new Color(0.88f, 0.54f, 1f, 1f);
            specialBarA.color = markerColor;
            specialBarB.color = markerColor;

            if (specialType == PieceSpecialType.RowBlast)
            {
                specialBarA.transform.localPosition = new Vector3(-0.34f, 0f, 0f);
                specialBarB.transform.localPosition = new Vector3(0.34f, 0f, 0f);
                specialBarA.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
                specialBarB.transform.localRotation = Quaternion.identity;
            }
            else
            {
                specialBarA.transform.localPosition = new Vector3(0f, 0.34f, 0f);
                specialBarB.transform.localPosition = new Vector3(0f, -0.34f, 0f);
                specialBarA.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                specialBarB.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
            }
        }

        private static Sprite GetWhiteSquareSprite()
        {
            if (whiteSquareSprite != null) return whiteSquareSprite;
            Texture2D texture = new Texture2D(4, 4, TextureFormat.RGBA32, false)
            {
                name = "JoinDogSpecialWhite"
            };
            Color[] pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            texture.SetPixels(pixels);
            texture.Apply();
            whiteSquareSprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
            whiteSquareSprite.name = "JoinDogSpecialSquare";
            return whiteSquareSprite;
        }

        private static Sprite GetSpecialRingSprite()
        {
            if (specialRingSprite != null) return specialRingSprite;
            const int size = 96;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "JoinDogSpecialRing",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size * 2f - 1f;
                    float ny = (y + 0.5f) / size * 2f - 1f;
                    float radius = Mathf.Sqrt(nx * nx + ny * ny);
                    float ring = 1f - Mathf.Clamp01(Mathf.Abs(radius - 0.78f) / 0.16f);
                    float glow = (1f - Mathf.Clamp01(Mathf.Abs(radius - 0.78f) / 0.34f)) * 0.38f;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Max(ring, glow));
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            specialRingSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            specialRingSprite.name = "JoinDogSpecialRingSprite";
            return specialRingSprite;
        }

        private static Sprite GetSpecialArrowSprite()
        {
            if (specialArrowSprite != null) return specialArrowSprite;
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "JoinDogSpecialArrow",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size * 2f - 1f;
                    float ny = Mathf.Abs((y + 0.5f) / size * 2f - 1f);
                    bool shaft = nx >= -0.82f && nx <= 0.05f && ny <= 0.22f;
                    float headLimit = Mathf.Clamp01((0.88f - nx) / 0.88f) * 0.64f;
                    bool head = nx >= -0.02f && nx <= 0.88f && ny <= headLimit;
                    float alpha = shaft || head ? 1f : 0f;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            specialArrowSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            specialArrowSprite.name = "JoinDogSpecialArrowSprite";
            return specialArrowSprite;
        }

        private static Sprite GetSpecialBurstSprite()
        {
            if (specialBurstSprite != null) return specialBurstSprite;
            const int size = 96;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "JoinDogSpecialBurst",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size * 2f - 1f;
                    float ny = (y + 0.5f) / size * 2f - 1f;
                    float radius = Mathf.Sqrt(nx * nx + ny * ny);
                    float angle = Mathf.Atan2(ny, nx);
                    float ray = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 4f)), 7f);
                    float limit = Mathf.Lerp(0.48f, 0.96f, ray);
                    float alpha = radius <= limit && radius >= 0.28f
                        ? Mathf.Clamp01((limit - radius) * 8f)
                        : 0f;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            specialBurstSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            specialBurstSprite.name = "JoinDogSpecialBurstSprite";
            return specialBurstSprite;
        }

        private void RefreshSpecialVisualScale()
        {
            if (specialVisualRoot == null) return;
            float scale = Mathf.Abs(defaultScale.x) > 0.001f ? 1f / Mathf.Abs(defaultScale.x) : 1f;
            specialVisualRoot.localScale = Vector3.one * scale;
        }

        private IEnumerator SpecialPulseAnimation()
        {
            while (SpecialType != PieceSpecialType.None)
            {
                float wave = (Mathf.Sin(Time.time * 5.5f) + 1f) * 0.5f;
                if (specialRingRenderer != null)
                {
                    Color color = specialRingRenderer.color;
                    color.a = Mathf.Lerp(0.48f, 0.92f, wave);
                    specialRingRenderer.color = color;
                    specialRingRenderer.transform.localScale = Vector3.one * Mathf.Lerp(0.72f, 0.86f, wave);
                    specialRingRenderer.transform.localRotation = Quaternion.Euler(
                        0f, 0f, SpecialType == PieceSpecialType.AreaBlast ? Time.time * 32f : 0f);
                }
                if (specialBarA != null && specialBarB != null && SpecialType != PieceSpecialType.AreaBlast)
                {
                    float offset = Mathf.Lerp(0.31f, 0.37f, wave);
                    if (SpecialType == PieceSpecialType.RowBlast)
                    {
                        specialBarA.transform.localPosition = new Vector3(-offset, 0f, 0f);
                        specialBarB.transform.localPosition = new Vector3(offset, 0f, 0f);
                    }
                    else
                    {
                        specialBarA.transform.localPosition = new Vector3(0f, offset, 0f);
                        specialBarB.transform.localPosition = new Vector3(0f, -offset, 0f);
                    }
                }
                else if (specialBarA != null && specialBarB != null)
                {
                    specialBarA.transform.localRotation = Quaternion.Euler(0f, 0f, -Time.time * 45f);
                    specialBarB.transform.localScale = Vector3.one * Mathf.Lerp(0.48f, 0.62f, wave);
                }
                yield return null;
            }
            specialPulseCoroutine = null;
        }

        public void SetSelected(bool isSelected)
        {
            SetSelected(isSelected, 0);
        }

        public void SetSelected(bool isSelected, int selectionOrder)
        {
            IsSelected = isSelected;

            if (selectionGlow != null)
            {
                selectionGlow.gameObject.SetActive(isSelected);
                selectionGlow.sortingOrder = isSelected
                    ? 19 + Mathf.Clamp(selectionOrder, 0, 8)
                    : defaultGlowSortingOrder;
            }

            if (mainRenderer != null)
            {
                mainRenderer.sortingOrder = isSelected
                    ? 20 + Mathf.Clamp(selectionOrder, 0, 8)
                    : defaultMainSortingOrder;
            }

            if (isSelected)
            {
                if (pulseCoroutine == null && gameObject.activeInHierarchy)
                {
                    pulseCoroutine = StartCoroutine(PulseAnimation());
                }
            }
            else
            {
                if (pulseCoroutine != null)
                {
                    StopCoroutine(pulseCoroutine);
                    pulseCoroutine = null;
                }
                transform.localScale = defaultScale;
                if (mainRenderer != null) mainRenderer.color = baseColor;
            }
        }

        private IEnumerator PulseAnimation()
        {
            while (true)
            {
                float wave = Mathf.Sin(Time.time * 9f);
                float scale = 1.14f + wave * 0.025f;
                transform.localScale = defaultScale * scale;

                if (selectionGlow != null)
                {
                    Color glowColor = selectionGlow.color;
                    glowColor.a = 0.58f + wave * 0.16f;
                    selectionGlow.color = glowColor;
                }
                yield return null;
            }
        }

        public void MoveToWorldPosition(Vector3 targetPos, float speed, System.Action onComplete = null)
        {
            MoveToWorldPosition(targetPos, speed, 0f, onComplete);
        }

        public void MoveToWorldPosition(
            Vector3 targetPos,
            float speed,
            float delay,
            System.Action onComplete = null)
        {
            if (moveCoroutine != null)
                StopCoroutine(moveCoroutine);

            if (gameObject.activeInHierarchy)
            {
                moveCoroutine = StartCoroutine(MoveWithFluidBounceRoutine(targetPos, speed, delay, onComplete));
            }
            else
            {
                transform.position = targetPos;
                onComplete?.Invoke();
            }
        }

        private IEnumerator MoveWithFluidBounceRoutine(
            Vector3 targetPos,
            float speed,
            float delay,
            System.Action onComplete)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            Vector3 startPos = transform.position;
            float totalDistance = Vector3.Distance(startPos, targetPos);
            if (totalDistance < 0.001f)
            {
                transform.position = targetPos;
                onComplete?.Invoke();
                yield break;
            }

            float duration = Mathf.Clamp(totalDistance / speed, 0.08f, 0.45f);
            float elapsed = 0f;

            // Accelerated Ease-In Falling Curve (Candy Crush feel)
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float easedT = t * t; // Quadratic acceleration
                transform.position = Vector3.Lerp(startPos, targetPos, easedT);
                yield return null;
            }

            transform.position = targetPos;

            // Double Elastic Landing Bounce (Compress -> Overshoot -> Settle)
            float bounceTime = 0.16f;
            elapsed = 0f;
            Vector3 compressScale = new Vector3(defaultScale.x * 1.22f, defaultScale.y * 0.78f, defaultScale.z);
            Vector3 stretchScale = new Vector3(defaultScale.x * 0.90f, defaultScale.y * 1.12f, defaultScale.z);

            while (elapsed < bounceTime)
            {
                elapsed += Time.deltaTime;
                float b = elapsed / bounceTime;

                if (b < 0.4f)
                {
                    transform.localScale = Vector3.Lerp(defaultScale, compressScale, b / 0.4f);
                }
                else if (b < 0.75f)
                {
                    transform.localScale = Vector3.Lerp(compressScale, stretchScale, (b - 0.4f) / 0.35f);
                }
                else
                {
                    transform.localScale = Vector3.Lerp(stretchScale, defaultScale, (b - 0.75f) / 0.25f);
                }

                yield return null;
            }

            transform.localScale = defaultScale;
            moveCoroutine = null;
            onComplete?.Invoke();
        }

        public void AnimateDespawn(System.Action onComplete)
        {
            AnimateDespawn(0f, onComplete);
        }

        public void AnimateDespawn(float delay, System.Action onComplete)
        {
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(DespawnRoutine(delay, onComplete));
            }
            else
            {
                onComplete?.Invoke();
            }
        }

        private IEnumerator DespawnRoutine(float delay, System.Action onComplete)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            float elapsed = 0f;
            float duration = 0.22f;
            Vector3 startScale = transform.localScale;
            Vector3 popScale = startScale * 1.28f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                if (t < 0.28f)
                {
                    float popT = t / 0.28f;
                    transform.localScale = Vector3.Lerp(startScale, popScale, popT);
                    if (mainRenderer != null)
                    {
                        mainRenderer.color = Color.Lerp(baseColor, Color.white, popT);
                    }
                }
                else
                {
                    float vanishT = (t - 0.28f) / 0.72f;
                    transform.localScale = Vector3.Lerp(popScale, Vector3.zero, vanishT);
                    if (mainRenderer != null)
                    {
                        Color fadingColor = Color.Lerp(Color.white, baseColor, vanishT);
                        fadingColor.a = 1f - vanishT;
                        mainRenderer.color = fadingColor;
                    }
                }

                yield return null;
            }

            transform.localScale = defaultScale;
            if (mainRenderer != null) mainRenderer.color = baseColor;
            onComplete?.Invoke();
        }
    }
}
