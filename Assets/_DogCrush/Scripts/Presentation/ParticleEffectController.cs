using System.Collections;
using System.Collections.Generic;
using DogCrush.Board;
using UnityEngine;

namespace DogCrush.Presentation
{
    public class ParticleEffectController : MonoBehaviour
    {
        public ParticleSystem particlePrefab;
        public Sprite pawSprite;
        public Sprite starSprite;

        private readonly Queue<ParticleSystem> pool = new Queue<ParticleSystem>();
        private static Material effectMaterial;
        private static Sprite shockwaveSprite;

        public void PlayMatchBurst(Vector3 position, Color color, int count = 14)
        {
            ParticleSystem ps = GetParticleSystem();
            ps.transform.position = position;

            var main = ps.main;
            main.startColor = color;

            var emission = ps.emission;
            emission.SetBurst(0, new ParticleSystem.Burst(0, count));

            ps.Play();
            StartCoroutine(RecycleRoutine(ps, main.duration + main.startLifetime.constantMax));
        }

        public void PlaySpecialActivation(
            PieceView special,
            int columns,
            int rows,
            float spacing)
        {
            if (special == null) return;
            Vector3 center = special.transform.position;
            float halfWidth = Mathf.Max(2f, columns * spacing * 0.54f);
            float halfHeight = Mathf.Max(2f, rows * spacing * 0.54f);
            switch (special.SpecialType)
            {
                case PieceSpecialType.RowBlast:
                    PlayEnergyBeam(
                        center + Vector3.left * halfWidth,
                        center + Vector3.right * halfWidth,
                        new Color(0.10f, 0.90f, 1f));
                    break;
                case PieceSpecialType.ColumnBlast:
                    PlayEnergyBeam(
                        center + Vector3.down * halfHeight,
                        center + Vector3.up * halfHeight,
                        new Color(0.78f, 0.34f, 1f));
                    break;
                case PieceSpecialType.AreaBlast:
                    StartCoroutine(ShockwaveRoutine(center, new Color(1f, 0.22f, 0.68f), 1.75f, 0f));
                    StartCoroutine(ShockwaveRoutine(center, new Color(1f, 0.82f, 0.12f), 2.25f, 0.08f));
                    break;
            }
        }

        public void PlaySpecialCreated(PieceView special)
        {
            if (special == null) return;
            Color color = special.SpecialType == PieceSpecialType.AreaBlast
                ? new Color(1f, 0.24f, 0.72f)
                : special.SpecialType == PieceSpecialType.ColumnBlast
                    ? new Color(0.76f, 0.34f, 1f)
                    : new Color(0.10f, 0.90f, 1f);
            StartCoroutine(ShockwaveRoutine(special.transform.position, color, 1.0f, 0f));
            StartCoroutine(ShockwaveRoutine(special.transform.position, new Color(1f, 0.84f, 0.12f), 1.35f, 0.07f));
        }

        public void PlayMegaBlast(Vector3 center, int columns, int rows, float spacing)
        {
            float halfWidth = Mathf.Max(2f, columns * spacing * 0.56f);
            float halfHeight = Mathf.Max(2f, rows * spacing * 0.56f);
            PlayEnergyBeam(center + Vector3.left * halfWidth, center + Vector3.right * halfWidth,
                new Color(1f, 0.26f, 0.76f), 0.48f);
            PlayEnergyBeam(center + Vector3.down * halfHeight, center + Vector3.up * halfHeight,
                new Color(0.18f, 0.88f, 1f), 0.48f);
            StartCoroutine(ShockwaveRoutine(center, new Color(1f, 0.86f, 0.12f), 3.2f, 0f));
            StartCoroutine(ShockwaveRoutine(center, new Color(1f, 0.22f, 0.72f), 4.2f, 0.09f));
            StartCoroutine(ShockwaveRoutine(center, new Color(0.15f, 0.88f, 1f), 5.2f, 0.18f));
        }

        private void PlayEnergyBeam(Vector3 start, Vector3 end, Color color, float duration = 0.34f)
        {
            GameObject root = new GameObject("JoinDogSpecialBeam");
            root.transform.SetParent(transform, false);
            LineRenderer glow = CreateBeamLine(root.transform, "Glow", start, end, color, 0.34f, 44);
            Color coreColor = Color.Lerp(color, Color.white, 0.78f);
            LineRenderer core = CreateBeamLine(root.transform, "Core", start, end, coreColor, 0.11f, 45);
            StartCoroutine(BeamRoutine(root, glow, core, color, coreColor, duration));
        }

        private static LineRenderer CreateBeamLine(
            Transform parent,
            string objectName,
            Vector3 start,
            Vector3 end,
            Color color,
            float width,
            int sortingOrder)
        {
            GameObject go = new GameObject(objectName, typeof(LineRenderer));
            go.transform.SetParent(parent, false);
            LineRenderer line = go.GetComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = color;
            line.numCapVertices = 8;
            line.sortingOrder = sortingOrder;
            line.material = GetEffectMaterial();
            return line;
        }

        private IEnumerator BeamRoutine(
            GameObject root,
            LineRenderer glow,
            LineRenderer core,
            Color glowColor,
            Color coreColor,
            float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float pulse = 1f + Mathf.Sin(t * Mathf.PI * 5f) * 0.16f;
                float alpha = 1f - t * t;
                glow.startWidth = glow.endWidth = 0.34f * pulse * (1f - t * 0.45f);
                core.startWidth = core.endWidth = 0.11f * pulse;
                Color glowNow = glowColor; glowNow.a = alpha * 0.82f;
                Color coreNow = coreColor; coreNow.a = alpha;
                glow.startColor = glow.endColor = glowNow;
                core.startColor = core.endColor = coreNow;
                yield return null;
            }
            Destroy(root);
        }

        private IEnumerator ShockwaveRoutine(Vector3 center, Color color, float finalScale, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            GameObject go = new GameObject("JoinDogSpecialShockwave", typeof(SpriteRenderer));
            go.transform.SetParent(transform, false);
            go.transform.position = center;
            SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = GetShockwaveSprite();
            renderer.sortingOrder = 46;
            float duration = 0.46f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                go.transform.localScale = Vector3.one * Mathf.Lerp(0.15f, finalScale, eased);
                Color current = color;
                current.a = Mathf.Sin(t * Mathf.PI) * 0.92f;
                renderer.color = current;
                yield return null;
            }
            Destroy(go);
        }

        private static Material GetEffectMaterial()
        {
            if (effectMaterial != null) return effectMaterial;
            Shader shader = Shader.Find("Sprites/Default");
            effectMaterial = shader != null ? new Material(shader) : null;
            return effectMaterial;
        }

        private static Sprite GetShockwaveSprite()
        {
            if (shockwaveSprite != null) return shockwaveSprite;
            const int size = 96;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "JoinDogShockwave",
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
                    float alpha = 1f - Mathf.Clamp01(Mathf.Abs(radius - 0.78f) / 0.12f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            shockwaveSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            shockwaveSprite.name = "JoinDogShockwaveSprite";
            return shockwaveSprite;
        }

        private ParticleSystem GetParticleSystem()
        {
            if (pool.Count > 0)
            {
                ParticleSystem ps = pool.Dequeue();
                ps.gameObject.SetActive(true);
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                return ps;
            }
            return CreateNewParticleSystem();
        }

        private ParticleSystem CreateNewParticleSystem()
        {
            GameObject go = new GameObject("CandyMatchParticleSystem");
            go.transform.SetParent(transform);

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            // A ParticleSystem starts playing as soon as it is added. Stop it
            // before changing duration or lifetime; Unity rejects those
            // settings while the system is already running.
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.duration = 0.45f;
            main.loop = false;
            main.startLifetime = 0.55f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            main.gravityModifier = 0.35f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.4f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            colorOverLifetime.color = grad;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0.0f, 1.0f);
            sizeCurve.AddKey(0.7f, 1.2f);
            sizeCurve.AddKey(1.0f, 0.0f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

            ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.sortingOrder = 30;

            // Use safe shader lookup - avoid Shader.Find which returns null in stripped WebGL builds
            try
            {
                Shader spriteShader = Shader.Find("Sprites/Default");
                if (spriteShader != null)
                {
                    Material mat = new Material(spriteShader);
                    if (pawSprite != null) mat.mainTexture = pawSprite.texture;
                    psr.material = mat;
                }
            }
            catch (System.Exception) { /* Silently handle shader not found in stripped builds */ }

            return ps;
        }

        private IEnumerator RecycleRoutine(ParticleSystem ps, float delay)
        {
            yield return new WaitForSeconds(delay);
            ps.gameObject.SetActive(false);
            pool.Enqueue(ps);
        }
    }
}
