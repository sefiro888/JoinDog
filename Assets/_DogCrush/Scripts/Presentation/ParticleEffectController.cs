using System.Collections;
using System.Collections.Generic;
using DogCrush.Board;
using JoinDog.App;
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
        private int accentSpritesAlive;
        private readonly HashSet<GameObject> transientEffects = new HashSet<GameObject>();

        public void ApplyWorldTheme(DogCrush.Core.BoardTheme theme)
        {
            var impacts=GetComponent<MatchImpactController>();
            if(impacts==null) impacts=gameObject.AddComponent<MatchImpactController>();
            impacts.ApplyWorldTheme(theme);
        }

        public void PlayMatchImpact(IList<PieceView> pieces, int matchedCount, int cascadeDepth,
            float spacing, bool specialImpact)
        {
            var impacts = GetComponent<MatchImpactController>();
            if (impacts == null) impacts = gameObject.AddComponent<MatchImpactController>();
            impacts.Play(pieces, matchedCount, cascadeDepth, spacing, specialImpact);
        }

        public void PlayLineFormation(PieceView special, IList<PieceView> consumed, float spacing)
        {
            var impacts = GetComponent<MatchImpactController>();
            if (impacts == null) impacts = gameObject.AddComponent<MatchImpactController>();
            impacts.PlayLineFormation(special, consumed, spacing);
        }

        public void PlayBounceFormation(PieceView special, float spacing)
        {
            var impacts=GetComponent<MatchImpactController>();
            if(impacts==null) impacts=gameObject.AddComponent<MatchImpactController>();
            impacts.PlayBounceFormation(special,spacing);
        }

        public void PlayNovaFormation(PieceView special, IList<PieceView> consumed, float spacing)
        {
            var impacts = GetComponent<MatchImpactController>();
            if (impacts == null) impacts = gameObject.AddComponent<MatchImpactController>();
            impacts.PlayNovaFormation(special, consumed, spacing);
        }

        public void PlayColorFormation(PieceView special, IList<PieceView> consumed, float spacing)
        {
            var impacts = GetComponent<MatchImpactController>();
            if (impacts == null) impacts = gameObject.AddComponent<MatchImpactController>();
            impacts.PlayColorFormation(special, consumed, spacing);
        }

        public void PlayColorSweep(Vector3 origin, IList<PieceView> targets, float spacing)
        {
            var impacts = GetComponent<MatchImpactController>();
            if (impacts == null) impacts = gameObject.AddComponent<MatchImpactController>();
            impacts.PlayColorSweep(origin, targets, spacing);
        }

        public void PlayBallBounces(Vector3 origin, IList<Vector3> destinations, float spacing)
        {
            var impacts = GetComponent<MatchImpactController>();
            if (impacts == null) impacts = gameObject.AddComponent<MatchImpactController>();
            impacts.PlayBallBounces(origin, destinations, spacing);
        }

        public void ClearMatchImpacts()
        {
            // Only presentation coroutines live on this controller. Cancel delayed
            // waves and particle recycling before rebuilding the available pool.
            StopAllCoroutines();
            GetComponent<MatchImpactController>()?.Clear();
            foreach (var effect in transientEffects)
            {
                if (effect == null) continue;
                effect.SetActive(false);
                Destroy(effect);
            }
            transientEffects.Clear();
            accentSpritesAlive = 0;
            pool.Clear();
            foreach (var particles in GetComponentsInChildren<ParticleSystem>(true))
            {
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particles.gameObject.SetActive(false);
                if (pool.Count < 28) pool.Enqueue(particles);
                else Destroy(particles.gameObject);
            }
        }

        private void ReleaseTransient(GameObject effect)
        {
            transientEffects.Remove(effect);
            if (effect != null) Destroy(effect);
        }

        private void OnDisable() => ClearMatchImpacts();

        public void PlayCombinationAccent(Vector3 center, int matchedCount)
        {
            if (AccessibilitySettings.ReducedMotion || accentSpritesAlive >= 18) return;
            Sprite sprite = Resources.Load<Sprite>(matchedCount >= 4 ? "UI/icon-score-star" : "UI/icon-score-paw");
            if (sprite == null) sprite = pawSprite;
            if (sprite == null) return;
            int count = Mathf.Min(matchedCount >= 6 ? 10 : matchedCount >= 4 ? 6 : 3, 18 - accentSpritesAlive);
            StartCoroutine(CombinationAccentRoutine(center, sprite, count, matchedCount));
        }

        private IEnumerator CombinationAccentRoutine(Vector3 center, Sprite sprite, int count, int tier)
        {
            var renderers = new SpriteRenderer[count];
            accentSpritesAlive += count;
            float baseScale = .22f / Mathf.Max(.01f, sprite.bounds.size.x);
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("MatchSparkle", typeof(SpriteRenderer));
                transientEffects.Add(go);
                go.transform.SetParent(transform, false);
                renderers[i] = go.GetComponent<SpriteRenderer>();
                renderers[i].sprite = sprite;
                renderers[i].sortingOrder = 80;
            }
            float elapsed = 0f;
            while (elapsed < .55f)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / .55f);
                for (int i = 0; i < count; i++)
                {
                    float angle = i * Mathf.PI * 2f / count;
                    var tr = renderers[i].transform;
                    tr.position = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * (.12f + t * .65f);
                    tr.localScale = Vector3.one * baseScale * (1f - t * .65f);
                    tr.rotation = Quaternion.Euler(0f, 0f, t * 100f);
                    Color tint = tier >= 6 ? Color.HSVToRGB(i / (float)count, .5f, 1f) : Color.white;
                    tint.a = 1f - t;
                    renderers[i].color = tint;
                }
                yield return null;
            }
            foreach (var renderer in renderers) ReleaseTransient(renderer.gameObject);
            accentSpritesAlive -= count;
        }

        public void PlayMatchBurst(Vector3 position, Color color, int count = 14)
        {
            // Mobile WebGL is fill-rate limited. Keep the feedback crisp while
            // preventing large cascades from spawning hundreds of particles.
            int mobileCap = AccessibilitySettings.ReducedMotion
                ? 4
                : Screen.width <= 720 || Screen.height <= 1100 ? 9 : 18;
            count = Mathf.Clamp(count, 3, mobileCap);
            ParticleSystem ps = GetParticleSystem();
            ps.transform.position = position;

            var main = ps.main;
            main.startColor = color;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;
            emission.burstCount = 1;
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
            if (AccessibilitySettings.ReducedMotion)
            {
                PlayMatchBurst(center, new Color(1f, 0.84f, 0.24f), 4);
                return;
            }
            float halfWidth = Mathf.Max(2f, columns * spacing * 0.54f);
            float halfHeight = Mathf.Max(2f, rows * spacing * 0.54f);
            switch (special.SpecialType)
            {
                case PieceSpecialType.RowBlast:
                    PlayBoardLineSweep(center,
                        center + Vector3.left * special.gridX * spacing,
                        center + Vector3.right * (columns - 1 - special.gridX) * spacing,
                        new Color(0.10f, 0.90f, 1f), spacing);
                    break;
                case PieceSpecialType.ColumnBlast:
                    PlayBoardLineSweep(center,
                        center + Vector3.down * special.gridY * spacing,
                        center + Vector3.up * (rows - 1 - special.gridY) * spacing,
                        new Color(0.78f, 0.34f, 1f), spacing);
                    break;
                case PieceSpecialType.AreaBlast:
                    StartCoroutine(ShockwaveRoutine(center, new Color(1f, 0.22f, 0.68f), 1.75f, 0f));
                    StartCoroutine(ShockwaveRoutine(center, new Color(1f, 0.82f, 0.12f), 2.25f, 0.08f));
                    break;
                case PieceSpecialType.ColorBurst:
                    PlayEnergyBeam(center + new Vector3(-halfWidth, -halfHeight),
                        center + new Vector3(halfWidth, halfHeight), new Color(1f, 0.84f, 0.12f), 0.42f);
                    PlayEnergyBeam(center + new Vector3(-halfWidth, halfHeight),
                        center + new Vector3(halfWidth, -halfHeight), new Color(0.14f, 0.90f, 1f), 0.42f);
                    StartCoroutine(ShockwaveRoutine(center, new Color(1f, 0.28f, 0.76f), 2.8f, 0.06f));
                    break;
                case PieceSpecialType.MegaBurst:
                    PlayBoardLineSweep(center, center - Vector3.right * special.gridX * spacing,
                        center + Vector3.right * (columns - 1 - special.gridX) * spacing,
                        new Color(.18f,.96f,1f),spacing);
                    PlayBoardLineSweep(center, center - Vector3.up * special.gridY * spacing,
                        center + Vector3.up * (rows - 1 - special.gridY) * spacing,
                        new Color(1f,.28f,.88f),spacing);
                    break;
                case PieceSpecialType.BallBounce:
                    for (int bounce = 0; bounce < 5; bounce++)
                    {
                        float angle = bounce * Mathf.PI * 2f / 5f;
                        Vector3 end = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) *
                            Mathf.Min(halfWidth, halfHeight) * 1.25f;
                        PlayEnergyBeam(center, end, new Color(1f, 0.36f, 0.12f), 0.30f);
                    }
                    StartCoroutine(ShockwaveRoutine(center, new Color(1f, 0.82f, 0.12f), 3.1f, 0f));
                    break;
                case PieceSpecialType.Whistle:
                    PlayEnergyBeam(center + Vector3.left * halfWidth, center + Vector3.right * halfWidth,
                        new Color(0.12f, 1f, 0.58f), 0.44f);
                    StartCoroutine(ShockwaveRoutine(center, new Color(0.68f, 1f, 0.24f), 2.9f, 0.04f));
                    break;
                case PieceSpecialType.Comet:
                    float backA = Mathf.Min(special.gridX, special.gridY) * spacing;
                    float frontA = Mathf.Min(columns - 1 - special.gridX, rows - 1 - special.gridY) * spacing;
                    float backB = Mathf.Min(special.gridX, rows - 1 - special.gridY) * spacing;
                    float frontB = Mathf.Min(columns - 1 - special.gridX, special.gridY) * spacing;
                    PlayEnergyBeam(center - new Vector3(backA, backA), center + new Vector3(frontA, frontA),
                        new Color(.2f, 1f, 1f), .34f);
                    PlayEnergyBeam(center + new Vector3(-backB, backB), center + new Vector3(frontB, -frontB),
                        new Color(1f, .86f, .25f), .34f);
                    var ends = new List<Vector3>();
                    if (backA > 0f) ends.Add(center - new Vector3(backA, backA));
                    if (frontA > 0f) ends.Add(center + new Vector3(frontA, frontA));
                    if (backB > 0f) ends.Add(center + new Vector3(-backB, backB));
                    if (frontB > 0f) ends.Add(center + new Vector3(frontB, -frontB));
                    var cometImpacts = GetComponent<MatchImpactController>();
                    if (cometImpacts == null) cometImpacts = gameObject.AddComponent<MatchImpactController>();
                    cometImpacts.PlayCometFlights(center, ends, spacing);
                    break;
            }
        }

        public void PlaySpecialCreated(PieceView special)
        {
            if (special == null) return;
            if (AccessibilitySettings.ReducedMotion)
            {
                PlayMatchBurst(special.transform.position, new Color(1f, 0.84f, 0.24f), 4);
                return;
            }
            Color color = special.SpecialType == PieceSpecialType.MegaBurst
                ? new Color(1f, 0.20f, 0.84f)
                : special.SpecialType == PieceSpecialType.BallBounce
                ? new Color(1f, 0.32f, 0.12f)
                : special.SpecialType == PieceSpecialType.Whistle
                ? new Color(0.20f, 1f, 0.55f)
                : special.SpecialType == PieceSpecialType.ColorBurst
                ? new Color(1f, 0.88f, 0.12f)
                : special.SpecialType == PieceSpecialType.AreaBlast
                ? new Color(1f, 0.24f, 0.72f)
                : special.SpecialType == PieceSpecialType.ColumnBlast
                    ? new Color(0.76f, 0.34f, 1f)
                    : new Color(0.10f, 0.90f, 1f);
            StartCoroutine(ShockwaveRoutine(special.transform.position, color, 1.0f, 0f));
            StartCoroutine(ShockwaveRoutine(special.transform.position, new Color(1f, 0.84f, 0.12f), 1.35f, 0.07f));
            if (special.SpecialType == PieceSpecialType.MegaBurst)
                StartCoroutine(ShockwaveRoutine(special.transform.position, new Color(0.14f, 0.94f, 1f), 1.72f, 0.14f));
        }

        public void PlayMegaBlast(Vector3 center, int columns, int rows, float spacing)
        {
            if (AccessibilitySettings.ReducedMotion)
            {
                PlayMatchBurst(center, new Color(1f, 0.84f, 0.24f), 4);
                return;
            }
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

        public void PlayDoubleAreaFootprints(IList<PieceView> specials, BoardController board)
        {
            if(specials==null || board==null) return;
            int count=0;
            foreach(var area in specials)
            {
                if(area==null || area.SpecialType!=PieceSpecialType.AreaBlast) continue;
                Color tint=count==0?new Color(1f,.24f,.66f):new Color(1f,.86f,.12f);
                if(AccessibilitySettings.ReducedMotion)
                    PlayMatchBurst(area.transform.position,tint,4);
                else
                {
                    float margin=board.ActivePieceSpacing*.44f;
                    var lower=board.GridToWorldPosition(Mathf.Max(0,area.gridX-2),Mathf.Max(0,area.gridY-2));
                    var upper=board.GridToWorldPosition(Mathf.Min(board.Columns-1,area.gridX+2),Mathf.Min(board.Rows-1,area.gridY+2));
                    var a=lower+new Vector3(-margin,-margin);
                    var b=new Vector3(upper.x+margin,a.y,a.z);
                    var c=upper+new Vector3(margin,margin);
                    var d=new Vector3(a.x,c.y,a.z);
                    // Outline each real, clipped 5x5 footprint, rather than
                    // radiating circles from the midpoint of both specials.
                    PlayEnergyBeam(a,b,tint,.3f,.35f);PlayEnergyBeam(b,c,tint,.3f,.35f);
                    PlayEnergyBeam(c,d,tint,.3f,.35f);PlayEnergyBeam(d,a,tint,.3f,.35f);
                }
                if(++count==2) break;
            }
        }

        public void PlaySpecialCombo(
            SpecialComboKind comboKind,
            Vector3 center,
            int columns,
            int rows,
            float spacing,
            PieceView comboAnchor)
        {
            if (AccessibilitySettings.ReducedMotion)
            {
                PlayMatchBurst(center, new Color(1f, 0.84f, 0.24f), 4);
                return;
            }
            float halfWidth = Mathf.Max(2f, columns * spacing * 0.56f);
            float halfHeight = Mathf.Max(2f, rows * spacing * 0.56f);
            if (comboKind == SpecialComboKind.BoardNova)
            {
                PlayMegaBlast(center, columns, rows, spacing);
                return;
            }

            if (comboKind == SpecialComboKind.WideRow)
            {
                PlayWideLineSweep(comboAnchor, true, columns, rows, spacing);
            }
            else if (comboKind == SpecialComboKind.WideColumn)
            {
                PlayWideLineSweep(comboAnchor, false, columns, rows, spacing);
            }
            else if (comboKind == SpecialComboKind.DoubleArea)
            {
                StartCoroutine(ShockwaveRoutine(center, new Color(1f, 0.24f, 0.66f), 3.4f, 0f));
                StartCoroutine(ShockwaveRoutine(center, new Color(1f, 0.86f, 0.12f), 4.6f, 0.10f));
            }
            else if (comboKind == SpecialComboKind.ColorSweep)
            {
                PlayEnergyBeam(center + Vector3.left * halfWidth, center + Vector3.right * halfWidth,
                    new Color(1f, 0.84f, 0.12f), 0.48f);
                PlayEnergyBeam(center + Vector3.down * halfHeight, center + Vector3.up * halfHeight,
                    new Color(0.16f, 0.90f, 1f), 0.48f);
                StartCoroutine(ShockwaveRoutine(center, new Color(1f, 0.30f, 0.80f), 3.8f, 0.06f));
            }
            else if (comboKind == SpecialComboKind.DoubleRow)
            {
                for (int lane = -1; lane <= 1; lane += 2)
                {
                    Vector3 laneCenter = center + Vector3.up * lane * spacing * 0.34f;
                    PlayEnergyBeam(laneCenter + Vector3.left * halfWidth,
                        laneCenter + Vector3.right * halfWidth, new Color(0.10f, 0.92f, 1f), 0.38f);
                }
            }
            else if (comboKind == SpecialComboKind.DoubleColumn)
            {
                for (int lane = -1; lane <= 1; lane += 2)
                {
                    Vector3 laneCenter = center + Vector3.right * lane * spacing * 0.34f;
                    PlayEnergyBeam(laneCenter + Vector3.down * halfHeight,
                        laneCenter + Vector3.up * halfHeight, new Color(0.76f, 0.34f, 1f), 0.38f);
                }
            }
            else if (comboKind == SpecialComboKind.CrossBlast)
            {
                PlayEnergyBeam(center + Vector3.left * halfWidth, center + Vector3.right * halfWidth,
                    new Color(0.10f, 0.92f, 1f), 0.42f);
                PlayEnergyBeam(center + Vector3.down * halfHeight, center + Vector3.up * halfHeight,
                    new Color(0.76f, 0.34f, 1f), 0.42f);
                StartCoroutine(ShockwaveRoutine(center, Color.white, 2.2f, 0.04f));
            }
            else
            {
                Color color = comboKind == SpecialComboKind.DoubleColumn
                    ? new Color(0.72f, 0.34f, 1f)
                    : new Color(0.12f, 0.90f, 1f);
                StartCoroutine(ShockwaveRoutine(center, color, 2.7f, 0f));
            }
        }

        private void PlayWideLineSweep(PieceView area, bool horizontal, int columns, int rows, float spacing)
        {
            if (area == null) return;
            Vector3 axis = horizontal ? Vector3.right : Vector3.up;
            Vector3 lanes = horizontal ? Vector3.up : Vector3.right;
            int axisIndex = horizontal ? area.gridX : area.gridY;
            int axisCount = horizontal ? columns : rows;
            int laneIndex = horizontal ? area.gridY : area.gridX;
            int laneCount = horizontal ? rows : columns;
            for (int lane = -1; lane <= 1; lane++)
            {
                if (laneIndex + lane < 0 || laneIndex + lane >= laneCount) continue;
                Vector3 origin = area.transform.position + lanes * lane * spacing;
                Color color = horizontal
                    ? (lane == 0 ? new Color(1f, .90f, .18f) : new Color(1f, .36f, .18f))
                    : (lane == 0 ? new Color(1f, .36f, .82f) : new Color(.58f, .28f, 1f));
                PlayBoardLineSweep(origin, origin - axis * axisIndex * spacing,
                    origin + axis * (axisCount - 1 - axisIndex) * spacing, color, spacing);
            }
        }

        private void PlayBoardLineSweep(Vector3 origin, Vector3 start, Vector3 end, Color color, float spacing)
        {
            PlayEnergyBeam(start, end, color);
            var impacts = GetComponent<MatchImpactController>();
            if (impacts == null) impacts = gameObject.AddComponent<MatchImpactController>();
            impacts.PlayLineSweep(origin, start, end, color, spacing);
        }

        public void PlayCompanionRow(PieceView target, int columns, float spacing)
        {
            if (target == null || !isActiveAndEnabled || columns <= 0) return;
            Vector3 origin = target.transform.position;
            Vector3 start = origin - Vector3.right * target.gridX * spacing;
            Vector3 end = origin + Vector3.right * (columns - 1 - target.gridX) * spacing;
            Color color = new Color(1f, .78f, .26f);
            if (!AccessibilitySettings.ReducedMotion) PlayEnergyBeam(start, end, color, .24f);
            var impacts = GetComponent<MatchImpactController>();
            if (impacts == null) impacts = gameObject.AddComponent<MatchImpactController>();
            impacts.PlayLineSweep(origin, start, end, color, spacing);
        }

        private void PlayEnergyBeam(Vector3 start, Vector3 end, Color color, float duration = 0.34f, float widthScale = 1f)
        {
            GameObject root = new GameObject("JoinDogSpecialBeam");
            transientEffects.Add(root);
            root.transform.SetParent(transform, false);
            LineRenderer glow = CreateBeamLine(root.transform, "Glow", start, end, color, 0.34f * widthScale, 44);
            Color coreColor = Color.Lerp(color, Color.white, 0.78f);
            LineRenderer core = CreateBeamLine(root.transform, "Core", start, end, coreColor, 0.11f * widthScale, 45);
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
            float glowWidth=glow.startWidth;
            float coreWidth=core.startWidth;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (AccessibilitySettings.ReducedMotion) break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float pulse = 1f + Mathf.Sin(t * Mathf.PI * 5f) * 0.16f;
                float alpha = 1f - t * t;
                glow.startWidth = glow.endWidth = glowWidth * pulse * (1f - t * 0.45f);
                core.startWidth = core.endWidth = coreWidth * pulse;
                Color glowNow = glowColor; glowNow.a = alpha * 0.82f;
                Color coreNow = coreColor; coreNow.a = alpha;
                glow.startColor = glow.endColor = glowNow;
                core.startColor = core.endColor = coreNow;
                yield return null;
            }
            ReleaseTransient(root);
        }

        private IEnumerator ShockwaveRoutine(Vector3 center, Color color, float finalScale, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            GameObject go = new GameObject("JoinDogSpecialShockwave", typeof(SpriteRenderer));
            transientEffects.Add(go);
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
            ReleaseTransient(go);
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
            main.startLifetime = new ParticleSystem.MinMaxCurve(.28f, .48f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.1f, 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(.09f, .21f);
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
            if (pool.Count < 28)
                pool.Enqueue(ps);
            else
                Destroy(ps.gameObject);
        }
    }
}
