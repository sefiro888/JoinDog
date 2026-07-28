using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DogCrush.Presentation
{
    public class ParticleEffectController : MonoBehaviour
    {
        public ParticleSystem particlePrefab;
        public Sprite pawSprite;
        public Sprite starSprite;

        private readonly Queue<ParticleSystem> pool = new Queue<ParticleSystem>();

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
