using System.Collections;
using System.Collections.Generic;
using DogCrush.Board;
using DogCrush.Core;
using JoinDog.App;
using UnityEngine;

namespace DogCrush.Presentation
{
    /// <summary>Short, pooled board-local impacts. Never delays gravity or input.</summary>
    public class MatchImpactController : MonoBehaviour
    {
        private const int Capacity = 40;
        private readonly Stack<SpriteRenderer> available = new Stack<SpriteRenderer>();
        private readonly HashSet<SpriteRenderer> active = new HashSet<SpriteRenderer>();
        private static Sprite ringSprite;
        private static Sprite glintSprite;
        private static Sprite spokeSprite;
        private static readonly Sprite[] worldSprites = new Sprite[10];
        private BoardTheme worldTheme;
        private bool worldConfigured;
        public void ApplyWorldTheme(BoardTheme theme)
        {
            if(worldConfigured && worldTheme != theme) Clear();
            worldTheme=theme; worldConfigured=true;
        }
        public int ActiveCount => active.Count;
        public int CreatedCount { get; private set; }

        public void Play(IList<PieceView> pieces, int matchCount, int cascadeDepth, float spacing, bool specialImpact)
        {
            if (!isActiveAndEnabled || pieces == null || pieces.Count == 0) return;
            spacing = Mathf.Max(.1f, spacing);
            Vector3 center = Vector3.zero;
            int valid = 0;
            foreach (var piece in pieces) if (piece != null) { center += piece.transform.position; valid++; }
            if (valid == 0) return;
            center /= valid;
            Color tint = cascadeDepth > 0 ? Color.Lerp(new Color(.35f, .95f, .88f),
                new Color(1f, .84f, .42f), Mathf.Clamp01((cascadeDepth - 1) / 7f)) :
                matchCount >= 6 ? new Color(1f, .4f, .82f) :
                matchCount >= 4 ? new Color(1f, .84f, .3f) : new Color(.62f, 1f, .84f);
            bool reduced = AccessibilitySettings.ReducedMotion;
            int shown = 0;
            int cellLimit = reduced ? 3 : cascadeDepth > 0 ? Mathf.Clamp(3 + cascadeDepth, 4, 6) : specialImpact ? 6 : 9;
            foreach (var piece in pieces)
            {
                if (piece == null) continue;
                // Copy positions now: gravity recycles the views before effects finish.
                Emit(piece.transform.position, tint, false, spacing * .3f,
                    spacing * (reduced ? .3f : cascadeDepth > 0 ? 1.05f : 1.45f), reduced ? .16f : cascadeDepth > 0 ? .24f : .38f,
                    reduced ? 0f : cascadeDepth > 0 ? shown * .01f : .07f + shown * .018f, Vector3.zero, reduced);
                if (cascadeDepth > 0 && !reduced)
                    Emit(piece.transform.position + new Vector3(.31f, .31f) * spacing,
                        Color.Lerp(tint, Color.white, .35f), true, spacing * .15f,
                        spacing * .04f, .22f, shown * .01f, Vector3.zero, false);
                if (++shown >= cellLimit) break;
            }
            if(worldConfigured) PlayWorldFragments(pieces,spacing,reduced,cascadeDepth);
            if (reduced || cascadeDepth > 0) return;
            if (matchCount == 3 && !specialImpact)
            {
                // Short decorative branches: unlike a special sweep these
                // never reach a whole row or mark neighbouring cells as hit.
                Emit(center, Color.Lerp(tint, Color.white, .45f), false,
                    spacing * .22f, spacing * .85f, .28f, 0f, Vector3.zero, false);
                for (int i = 0; i < 3; i++)
                {
                    float angle = (45f + i * 120f) * Mathf.Deg2Rad;
                    Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                    EmitTripleSpoke(center, direction, tint, spacing);
                    Emit(center + direction * spacing * .14f, Color.Lerp(tint, Color.white, .55f),
                        true, spacing * .25f, spacing * .06f, .32f, 0f,
                        direction * spacing * .64f, false);
                }
            }
            if (matchCount >= 4 || cascadeDepth > 0)
            {
                float radius = spacing * Mathf.Min(3.8f, 1.7f + matchCount * .22f + cascadeDepth * .12f);
                Emit(center, tint, false, spacing * .35f, radius, .52f, .06f, Vector3.zero, false);
                if (matchCount >= 5 || cascadeDepth >= 2)
                    Emit(center, Color.Lerp(tint, Color.white, .55f), false, spacing * .25f,
                        radius * .8f, .48f, .15f, Vector3.zero, false);
                int rays = Mathf.Clamp(matchCount + Mathf.Min(cascadeDepth, 2), 4, 8);
                for (int i = 0; i < rays; i++)
                {
                    float angle = (i + .25f) * Mathf.PI * 2f / rays;
                    Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                    Color sparkle = matchCount >= 6 ? Color.HSVToRGB(i / (float)rays, .42f, 1f) : tint;
                    Emit(center + direction * spacing * .2f, sparkle, true, spacing * .4f,
                        spacing * .05f, .58f, .08f, direction * radius * .6f, false);
                }
            }
        }

        public void PlayLineFormation(PieceView special, IList<PieceView> consumed, float spacing)
        {
            if (!isActiveAndEnabled || special == null || consumed == null ||
                AccessibilitySettings.ReducedMotion ||
                (special.SpecialType != PieceSpecialType.RowBlast && special.SpecialType != PieceSpecialType.ColumnBlast &&
                 special.SpecialType != PieceSpecialType.Comet)) return;
            spacing = Mathf.Max(.1f, spacing);
            var kind = special.SpecialType;
            Color tint = kind == PieceSpecialType.Comet ? new Color(1f, .46f, .52f) :
                kind == PieceSpecialType.RowBlast ? new Color(.1f, .9f, 1f) : new Color(.76f, .34f, 1f);
            int shown = 0;
            foreach (var piece in consumed)
            {
                if (piece == null || piece == special) continue;
                var head = Acquire();
                if (head == null) break;
                PrepareTraveler(head, piece.transform.position, tint, spacing * .25f, true);
                StartCoroutine(FormLineSpecial(head, special, kind, piece.transform.position, Vector3.zero, tint, spacing));
                if (++shown == 3) break;
            }
            Vector3 axis = kind == PieceSpecialType.Comet ? Vector3.forward :
                kind == PieceSpecialType.RowBlast ? Vector3.right : Vector3.up;
            foreach (float sign in new[] { -1f, 1f })
            {
                var marker = Acquire();
                if (marker == null) break;
                PrepareTraveler(marker, special.transform.position, tint, spacing * .3f, true);
                StartCoroutine(FormLineSpecial(marker, special, kind, special.transform.position, axis * sign, tint, spacing));
            }
        }

        public void PlayColorFormation(PieceView special, IList<PieceView> consumed, float spacing)
        {
            if (!isActiveAndEnabled || special == null || consumed == null ||
                special.SpecialType != PieceSpecialType.ColorBurst || AccessibilitySettings.ReducedMotion) return;
            spacing = Mathf.Max(.1f, spacing);
            int shown = 0;
            foreach (var piece in consumed)
            {
                if (piece == null || piece == special) continue;
                var head = Acquire();
                if (head == null) break;
                Color tint = Color.HSVToRGB(.1f + shown * .17f, .45f, 1f);
                PrepareTraveler(head, piece.transform.position, tint, spacing * .3f, true);
                StartCoroutine(FormLineSpecial(head, special, PieceSpecialType.ColorBurst,
                    piece.transform.position, Vector3.zero, tint, spacing));
                if (++shown == 4) break;
            }
            for (int i = 0; i < 4; i++)
            {
                var marker = Acquire();
                if (marker == null) break;
                Color tint = Color.HSVToRGB(.1f + i * .17f, .45f, 1f);
                PrepareTraveler(marker, special.transform.position, tint, spacing * .3f, true);
                StartCoroutine(FormLineSpecial(marker, special, PieceSpecialType.ColorBurst,
                    special.transform.position, new Vector3(0,0,i + 1), tint, spacing));
            }
        }

        public void PlayNovaFormation(PieceView special, IList<PieceView> consumed, float spacing)
        {
            if (!isActiveAndEnabled || special == null || consumed == null ||
                special.SpecialType != PieceSpecialType.MegaBurst || AccessibilitySettings.ReducedMotion) return;
            spacing = Mathf.Max(.1f, spacing);
            int shown = 0;
            foreach (var piece in consumed)
            {
                if (piece == null || piece == special) continue;
                var head = Acquire();
                if (head == null) break;
                Color tint = shown % 2 == 0 ? new Color(1f,.3f,.8f) : new Color(.2f,.95f,1f);
                PrepareTraveler(head,piece.transform.position,tint,spacing*.3f,true);
                StartCoroutine(FormLineSpecial(head,special,PieceSpecialType.MegaBurst,
                    piece.transform.position,Vector3.zero,tint,spacing));
                if (++shown == 5) break;
            }
            foreach (var axis in new[] {Vector3.right,Vector3.left,Vector3.up,Vector3.down})
            {
                var marker = Acquire();
                if (marker == null) break;
                Color tint = Mathf.Abs(axis.x) > .1f ? new Color(.2f,.95f,1f) : new Color(1f,.3f,.8f);
                PrepareTraveler(marker,special.transform.position,tint,spacing*.3f,true);
                StartCoroutine(FormLineSpecial(marker,special,PieceSpecialType.MegaBurst,
                    special.transform.position,axis,tint,spacing));
            }
        }

        public void PlayBounceFormation(PieceView special, float spacing)
        {
            if (!isActiveAndEnabled || special == null || special.SpecialType != PieceSpecialType.BallBounce ||
                AccessibilitySettings.ReducedMotion) return;
            spacing=Mathf.Max(.1f,spacing);
            Sprite ball=Resources.Load<Sprite>("Pieces/piece-ball-v2");
            for(int i=0;i<3;i++)
            {
                var marker=Acquire(); if(marker==null) break;
                PrepareTraveler(marker,special.transform.position,Color.white,spacing*.3f,true);
                if(ball!=null) marker.sprite=ball;
                StartCoroutine(FormLineSpecial(marker,special,PieceSpecialType.BallBounce,
                    special.transform.position,new Vector3(0,0,i+1),Color.white,spacing));
            }
        }

        private IEnumerator FormLineSpecial(SpriteRenderer renderer, PieceView target, PieceSpecialType kind,
            Vector3 from, Vector3 axis, Color tint, float spacing)
        {
            float elapsed = 0f;
            float duration = kind == PieceSpecialType.MegaBurst || kind == PieceSpecialType.BallBounce ? .42f :
                kind == PieceSpecialType.ColorBurst ? .4f : .32f;
            while (elapsed < duration && target != null && target.gameObject.activeInHierarchy &&
                target.SpecialType == kind && !AccessibilitySettings.ReducedMotion)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                bool marker = axis.sqrMagnitude > .1f;
                Vector3 direction = axis;
                if (kind == PieceSpecialType.Comet && marker)
                {
                    float angle = t * Mathf.PI * 1.5f + (axis.z < 0f ? Mathf.PI : 0f);
                    direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                }
                if (kind == PieceSpecialType.ColorBurst && marker)
                {
                    float angle = t * Mathf.PI * 2f + (axis.z - 1f) * Mathf.PI * .5f;
                    direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                }
                renderer.transform.position = marker
                    ? target.transform.position + direction * spacing * (kind == PieceSpecialType.ColorBurst
                        ? Mathf.Lerp(.25f,.65f,t) : kind == PieceSpecialType.MegaBurst
                        ? Mathf.Lerp(.18f,.72f,t) : Mathf.Lerp(.18f, .46f, t))
                    : Vector3.Lerp(from, target.transform.position, 1f - Mathf.Pow(1f - t, 3f));
                renderer.transform.localScale = Vector3.one * spacing * (marker ? .32f : .25f) * (1f - t * .35f);
                if(kind==PieceSpecialType.BallBounce)
                {
                    float angle=(axis.z-1f)*Mathf.PI*2f/3f;
                    renderer.transform.position=target.transform.position+spacing*new Vector3(
                        Mathf.Cos(angle)*.52f,Mathf.Sin(angle)*.3f+Mathf.Abs(Mathf.Sin(t*Mathf.PI*2f))*.24f,0f);
                    float spriteSize=Mathf.Max(.01f,Mathf.Max(renderer.sprite.bounds.size.x,renderer.sprite.bounds.size.y));
                    renderer.transform.localScale=Vector3.one*spacing*.3f/spriteSize;
                    renderer.transform.rotation=Quaternion.Euler(0,0,t*180f);
                }
                tint.a = Mathf.Sin(t * Mathf.PI) * .9f;
                renderer.color = tint;
                yield return null;
            }
            Release(renderer);
        }

        public void PlayLineSweep(Vector3 origin, Vector3 start, Vector3 end, Color tint, float spacing)
        {
            if (!isActiveAndEnabled || AccessibilitySettings.ReducedMotion) return;
            spacing = Mathf.Max(.1f, spacing);
            foreach (var target in new[] { start, end })
            {
                if ((target - origin).sqrMagnitude < .001f) continue;
                Emit(origin, Color.Lerp(tint, Color.white, .65f), true, spacing * .8f,
                    spacing * .28f, .18f, 0f, target - origin, false);
            }
            // Actual cell centres along the affected lane, reached from the
            // origin in both directions before ordinary special removal.
            float distance = Vector3.Distance(start, end);
            int cells = Mathf.Clamp(Mathf.RoundToInt(distance / spacing) + 1, 1, 12);
            float longest = Mathf.Max(Vector3.Distance(origin, start), Vector3.Distance(origin, end), spacing);
            for (int i = 0; i < cells; i++)
            {
                Vector3 cell = cells == 1 ? start : Vector3.Lerp(start, end, i / (float)(cells - 1));
                float delay = Vector3.Distance(origin, cell) / longest * .18f;
                Emit(cell, tint, false, spacing * .28f, spacing * .88f,
                    .14f, delay, Vector3.zero, false);
            }
        }

        // Targets are copied immediately; pooled pieces may move or disappear later.
        public void PlayBallBounces(Vector3 origin, IList<Vector3> destinations, float spacing)
            => PlayToyFlights(origin, destinations, spacing, false);

        public void PlayCometFlights(Vector3 origin, IList<Vector3> destinations, float spacing)
            => PlayToyFlights(origin, destinations, spacing, true);

        private void PlayToyFlights(Vector3 origin, IList<Vector3> destinations, float spacing, bool comet)
        {
            if (!isActiveAndEnabled || destinations == null) return;
            spacing = Mathf.Max(.1f, spacing);
            var seen = new HashSet<Vector3>();
            bool reduced = AccessibilitySettings.ReducedMotion;
            int shown = 0;
            foreach (var target in destinations)
            {
                if (!seen.Add(target)) continue;
                if (shown >= (reduced ? 3 : 8)) break;
                var tint = comet ? new Color(1f, .46f, .52f, 1f) : new Color(.35f, .84f, 1f, 1f);
                if (reduced)
                    Emit(target, tint, false, spacing * .65f, spacing * .65f,
                        .18f, 0f, Vector3.zero, true);
                else
                {
                    var head = Acquire();
                    if (head == null) break;
                    PrepareTraveler(head, origin, Color.white, spacing * .48f, true);
                    var ball = Resources.Load<Sprite>(comet ? "Magic/frisbee-coral-v2" : "Pieces/piece-ball-v2");
                    if (ball != null)
                    {
                        head.sprite = ball;
                        head.transform.localScale = Vector3.one * spacing * .48f /
                            Mathf.Max(.01f, Mathf.Max(ball.bounds.size.x, ball.bounds.size.y));
                    }
                    StartCoroutine(ToyFlightToTarget(head, origin, target, spacing, shown * .004f, comet, tint));
                }
                shown++;
            }
        }

        private IEnumerator ToyFlightToTarget(SpriteRenderer head, Vector3 origin, Vector3 target, float spacing,
            float delay, bool comet, Color arrivalTint)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            float elapsed = 0f;
            while (elapsed < .16f)
            {
                if (AccessibilitySettings.ReducedMotion) break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / .16f);
                // Keep the path between actual board cells. The small lateral
                // arc and spin suggest a bounce without flying into the HUD.
                head.transform.position = comet ? Vector3.Lerp(origin, target, t) : SweepPosition(origin, target, t, spacing, 1f);
                head.transform.rotation = Quaternion.Euler(0, 0, -t * 270f);
                head.color = new Color(1f, 1f, 1f, Mathf.Min(1f, t * 10f));
                yield return null;
            }
            if (!AccessibilitySettings.ReducedMotion)
            {
                head.sprite = GetSprite(false);
                yield return Animate(head, target, arrivalTint, false,
                    spacing * .3f, spacing * 1.2f, .14f, 0, Vector3.zero, false);
            }
            Release(head);
        }

        public void PlayColorSweep(Vector3 origin, IList<PieceView> targets, float spacing)
        {
            if (!isActiveAndEnabled || targets == null) return;
            spacing = Mathf.Max(.1f, spacing);
            var positions = new List<Vector3>();
            foreach (var piece in targets)
                if (piece != null && (piece.transform.position - origin).sqrMagnitude > .001f &&
                    !positions.Contains(piece.transform.position)) positions.Add(piece.transform.position);
            bool reduced = AccessibilitySettings.ReducedMotion;
            int limit = reduced ? 3 : 10;
            int stride = Mathf.Max(1, Mathf.CeilToInt(positions.Count / (float)limit));
            int shown = 0;
            for (int i = 0; i < positions.Count && shown < limit; i += stride, shown++)
            {
                Color tint = Color.HSVToRGB(.10f + shown * .075f, .38f, 1f);
                if (reduced)
                    Emit(positions[i], tint, false, spacing * .6f, spacing * .6f,
                        .18f, 0f, Vector3.zero, true);
                else
                {
                    var head = Acquire();
                    if (head == null) break;
                    var halo = Acquire();
                    PrepareTraveler(head, origin, tint, spacing * .42f, true);
                    if (halo != null) PrepareTraveler(halo, origin, tint, spacing * .7f, false);
                    StartCoroutine(TravelToTarget(head, halo, origin, positions[i], tint,
                        spacing, shown * .006f, shown % 2 == 0 ? 1f : -1f));
                }
            }
        }

        private static void PrepareTraveler(SpriteRenderer renderer, Vector3 origin,
            Color tint, float size, bool glint)
        {
            renderer.sprite = GetSprite(glint);
            renderer.transform.position = origin;
            renderer.transform.rotation = Quaternion.identity;
            renderer.transform.localScale = Vector3.one * size;
            tint.a = 0f;
            renderer.color = tint;
        }

        private static Vector3 SweepPosition(Vector3 start, Vector3 end, float t, float spacing, float direction)
        {
            Vector3 delta = end - start;
            Vector3 normal = new Vector3(-delta.y, delta.x, 0f).normalized;
            // A small arc keeps the flight within the local board neighbourhood.
            return Vector3.Lerp(start, end, t) + normal *
                (Mathf.Sin(t * Mathf.PI) * Mathf.Min(spacing * .38f, delta.magnitude * .12f) * direction);
        }

        private IEnumerator TravelToTarget(SpriteRenderer head, SpriteRenderer halo,
            Vector3 origin, Vector3 target, Color tint, float spacing, float delay, float direction)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            const float flight = .28f;
            float elapsed = 0f;
            while (elapsed < flight)
            {
                if (AccessibilitySettings.ReducedMotion) break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / flight);
                head.transform.position = SweepPosition(origin, target, t, spacing, direction);
                head.transform.rotation = Quaternion.Euler(0f, 0f, t * 150f);
                tint.a = Mathf.Min(1f, t * 8f);
                head.color = tint;
                if (halo != null)
                {
                    halo.transform.position = SweepPosition(origin, target, Mathf.Max(0f, t - .1f), spacing, direction);
                    halo.color = new Color(tint.r, tint.g, tint.b, tint.a * .48f);
                }
                yield return null;
            }
            if (!AccessibilitySettings.ReducedMotion)
            {
                head.sprite = GetSprite(false);
                // Reuse the travelling head for the arrival explosion, rather than allocating another effect.
                yield return Animate(head, target, tint, false, spacing * .28f,
                    spacing * 1.25f, .16f, 0f, Vector3.zero, false);
            }
            Release(head);
            Release(halo);
        }

        private void PlayWorldFragments(IList<PieceView> pieces,float spacing,bool reduced,int cascadeDepth)
        {
            Color tint=WorldTint(worldTheme);
            int shown=0,limit=reduced?1:cascadeDepth>0?2:3;
            foreach(var piece in pieces)
            {
                if(piece==null) continue;
                float angle=(shown*137f+31f)*Mathf.Deg2Rad;
                Vector3 direction=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0f);
                Emit(piece.transform.position,tint,!reduced,spacing*.28f,spacing*(reduced?.28f:.12f),
                    reduced?.12f:.3f,0f,reduced?Vector3.zero:direction*spacing*.38f,reduced,GetWorldSprite(worldTheme));
                if(++shown==limit) break;
            }
        }

        private static Color WorldTint(BoardTheme theme) => theme switch
        {
            BoardTheme.Meadow => new Color(1f,.83f,.52f),
            BoardTheme.Forest => new Color(.58f,.97f,.45f),
            BoardTheme.Festival => new Color(1f,.42f,.77f),
            BoardTheme.Coast => new Color(.38f,.94f,1f),
            BoardTheme.Mountain => new Color(.82f,.95f,1f),
            BoardTheme.Aurora => new Color(.48f,1f,.84f),
            BoardTheme.LuminousSummit => new Color(.78f,.65f,1f),
            BoardTheme.CelestialGarden => new Color(1f,.67f,.91f),
            BoardTheme.RubyCanyon => new Color(1f,.46f,.32f),
            _ => new Color(1f,.86f,.37f)
        };

        private static Sprite GetWorldSprite(BoardTheme theme)
        {
            int index=Mathf.Clamp((int)theme,0,9);
            if(worldSprites[index]!=null) return worldSprites[index];
            const int size=64;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
            texture.name="WorldImpact_"+theme;
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                float nx=(x+.5f)/size*2f-1f,ny=(y+.5f)/size*2f-1f;
                float r=Mathf.Sqrt(nx*nx+ny*ny),a=Mathf.Atan2(ny,nx);
                bool hit=theme switch
                {
                    BoardTheme.Meadow => r<.5f+.23f*Mathf.Cos(a*4f),
                    BoardTheme.Forest => Mathf.Pow((nx+.3f*ny)/.34f,2)+ny*ny/.64f<1f,
                    BoardTheme.Festival => Mathf.Abs(nx)<.2f && Mathf.Abs(ny)<.75f,
                    BoardTheme.Coast => r<.8f && Mathf.Abs(nx)<.48f-.38f*ny,
                    BoardTheme.Mountain => r<.82f && Mathf.Abs(Mathf.Sin(a*3f))*r<.065f,
                    BoardTheme.Aurora => Mathf.Abs(nx)<.82f && Mathf.Abs(ny-.25f*Mathf.Sin(nx*5f))<.09f,
                    BoardTheme.LuminousSummit => Mathf.Abs(nx)/.48f+Mathf.Abs(ny)/.85f<1f,
                    BoardTheme.CelestialGarden => r<.5f+.24f*Mathf.Cos(a*5f),
                    BoardTheme.RubyCanyon => ny>-.65f && ny<.7f && Mathf.Abs(nx+ny*.2f)<(.7f-ny)*.35f,
                    _ => r<.5f+.28f*Mathf.Cos(a*8f)
                };
                pixels[y*size+x]=new Color(1f,1f,1f,hit?1f:0f);
            }
            texture.SetPixels(pixels);texture.Apply(false,true);
            worldSprites[index]=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),size);
            return worldSprites[index];
        }

        private void Emit(Vector3 origin, Color color, bool glint, float from, float to,
            float duration, float delay, Vector3 travel, bool reduced,Sprite motif=null)
        {
            var renderer = Acquire();
            if (renderer == null) return;
            renderer.sprite = motif ?? GetSprite(glint);
            renderer.transform.position = origin;
            renderer.transform.localScale = Vector3.one * from;
            renderer.color = new Color(color.r, color.g, color.b, 0f);
            StartCoroutine(Animate(renderer, origin, color, glint, from, to, duration, delay, travel, reduced));
        }

        private void EmitTripleSpoke(Vector3 origin, Vector3 direction, Color tint, float spacing)
        {
            SpriteRenderer renderer = Acquire();
            if (renderer == null) return;
            renderer.sprite = GetSpokeSprite();
            renderer.color = new Color(tint.r, tint.g, tint.b, 0f);
            StartCoroutine(AnimateTripleSpoke(renderer, origin, direction, tint, spacing));
        }

        private IEnumerator AnimateTripleSpoke(SpriteRenderer renderer, Vector3 origin,
            Vector3 direction, Color tint, float spacing)
        {
            float elapsed = 0f;
            const float duration = .32f;
            while (elapsed < duration && !AccessibilitySettings.ReducedMotion)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float length = spacing * Mathf.Lerp(.12f, .8f, 1f - Mathf.Pow(1f - t, 3f));
                renderer.transform.position = origin + direction * (length * .5f);
                renderer.transform.rotation = Quaternion.Euler(0f, 0f,
                    Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                renderer.transform.localScale = new Vector3(length, spacing * .22f * (1f - t * .45f), 1f);
                tint.a = Mathf.Sin(t * Mathf.PI) * .88f;
                renderer.color = tint;
                yield return null;
            }
            Release(renderer);
        }

        private static Sprite GetSpokeSprite()
        {
            if (spokeSprite != null) return spokeSprite;
            const int width = 64, height = 16;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = "TripleSpokeTexture";
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                float side = Mathf.Abs((y + .5f) / height * 2f - 1f);
                float end = Mathf.Sin((x + .5f) / width * Mathf.PI);
                pixels[y * width + x] = new Color(1f, 1f, 1f,
                    Mathf.Pow(Mathf.Clamp01(1f - side), 2f) * end);
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            spokeSprite = Sprite.Create(texture, new Rect(0, 0, width, height),
                new Vector2(.5f, .5f), width);
            return spokeSprite;
        }

        private SpriteRenderer Acquire()
        {
            SpriteRenderer renderer;
            if (available.Count > 0) renderer = available.Pop();
            else
            {
                if (CreatedCount >= Capacity) return null;
                var go = new GameObject("JoinDogMatchImpact", typeof(SpriteRenderer));
                go.transform.SetParent(transform, false);
                renderer = go.GetComponent<SpriteRenderer>();
                renderer.sortingOrder = 48;
                CreatedCount++;
            }
            active.Add(renderer);
            renderer.gameObject.SetActive(true);
            return renderer;
        }

        private IEnumerator Animate(SpriteRenderer renderer, Vector3 origin, Color color, bool glint,
            float from, float to, float duration, float delay, Vector3 travel, bool reduced)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                // Switching accessibility settings also stops effects already running.
                if (!reduced && AccessibilitySettings.ReducedMotion) break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = 1f - Mathf.Pow(1f - t, 3f);
                renderer.transform.localScale = Vector3.one * Mathf.Lerp(from, to, ease);
                renderer.transform.position = origin + travel * ease;
                renderer.transform.rotation = Quaternion.Euler(0, 0, glint ? t * 95f : 0f);
                color.a = reduced ? .65f * (1f - t) : Mathf.Sin(Mathf.PI * t) * (glint ? .95f : .72f);
                renderer.color = color;
                yield return null;
            }
            Release(renderer);
        }

        private void Release(SpriteRenderer renderer)
        {
            if (renderer == null || !active.Remove(renderer)) return;
            renderer.gameObject.SetActive(false);
            available.Push(renderer);
        }

        public void Clear()
        {
            StopAllCoroutines();
            foreach (var renderer in active)
            {
                if (renderer == null) continue;
                renderer.gameObject.SetActive(false);
                available.Push(renderer);
            }
            active.Clear();
        }

        private void OnEnable() => AccessibilitySettings.Changed += HandleAccessibilityChanged;
        private void HandleAccessibilityChanged()
        {
            if (AccessibilitySettings.ReducedMotion) Clear();
        }
        private void OnDisable()
        {
            AccessibilitySettings.Changed -= HandleAccessibilityChanged;
            Clear();
        }

        private static Sprite GetSprite(bool glint)
        {
            if (glint && glintSprite != null) return glintSprite;
            if (!glint && ringSprite != null) return ringSprite;
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = glint ? "MatchGlintTexture" : "MatchRingTexture";
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float nx = (x + .5f) / size * 2f - 1f;
                float ny = (y + .5f) / size * 2f - 1f;
                float radius = Mathf.Sqrt(nx * nx + ny * ny);
                float alpha = glint ? Mathf.Clamp01(1f - Mathf.Min(Mathf.Abs(nx), Mathf.Abs(ny)) * 10f) *
                    Mathf.Clamp01(1f - radius) : Mathf.Clamp01(1f - Mathf.Abs(radius - .75f) / .1f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
            if (glint) glintSprite = sprite; else ringSprite = sprite;
            return sprite;
        }
    }
}
