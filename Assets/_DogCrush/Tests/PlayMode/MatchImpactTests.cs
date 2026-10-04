using System.Collections;
using System.Collections.Generic;
using DogCrush.Board;
using DogCrush.Presentation;
using JoinDog.App;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DogCrush.Tests.PlayMode
{
    public class MatchImpactTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private bool previousMotion;
        [SetUp] public void Setup() { previousMotion = AccessibilitySettings.ReducedMotion; AccessibilitySettings.ReducedMotion = false; }
        [TearDown] public void Cleanup()
        {
            AccessibilitySettings.ReducedMotion = previousMotion;
            foreach (var go in objects) if (go != null) Object.DestroyImmediate(go);
            objects.Clear();
        }
        [UnityTest]
        public IEnumerator WorldImpacts_HaveTenCachedMotifsNoRngAndStaticReducedSignals()
        {
            var effects=Create(out var pieces);
            var motifs=new HashSet<Sprite>();
            foreach(DogCrush.Core.BoardTheme theme in System.Enum.GetValues(typeof(DogCrush.Core.BoardTheme)))
            {
                effects.ApplyWorldTheme(theme);
                var rng=Random.state;
                effects.Play(pieces.GetRange(0,3),3,0,.7f,true);
                Assert.That(Random.state,Is.EqualTo(rng));
                int count=0;
                foreach(var renderer in effects.GetComponentsInChildren<SpriteRenderer>())
                    if(renderer.sprite.texture.name=="WorldImpact_"+theme) {motifs.Add(renderer.sprite);count++;}
                Assert.That(count,Is.EqualTo(3));
                effects.Clear();
                effects.Play(pieces.GetRange(0,3),3,0,.7f,true);
                foreach(var renderer in effects.GetComponentsInChildren<SpriteRenderer>())
                    if(renderer.sprite.texture.name.StartsWith("WorldImpact_")) Assert.That(motifs.Contains(renderer.sprite),Is.True);
                effects.Clear();
            }
            Assert.That(motifs.Count,Is.EqualTo(10));
            Assert.That(effects.CreatedCount,Is.EqualTo(6));
            AccessibilitySettings.ReducedMotion=true;
            effects.Play(pieces.GetRange(0,3),3,0,.7f,true);
            Assert.That(effects.ActiveCount,Is.EqualTo(4));
            var renderers=effects.GetComponentsInChildren<SpriteRenderer>();
            var positions=new List<Vector3>();foreach(var renderer in renderers) positions.Add(renderer.transform.position);
            var rotations=new List<Quaternion>();foreach(var renderer in renderers) rotations.Add(renderer.transform.rotation);
            yield return new WaitForSeconds(.05f);
            for(int i=0;i<renderers.Length;i++)
            {
                Assert.That(renderers[i].transform.position,Is.EqualTo(positions[i]));
                Assert.That(renderers[i].transform.rotation,Is.EqualTo(rotations[i]));
            }
            yield return new WaitForSeconds(.2f);
            Assert.That(effects.ActiveCount,Is.Zero);
        }

        [UnityTest]
        public IEnumerator WorldImpacts_RenderAllTenMotifsForVisualReview()
        {
            var cameraObject=new GameObject("WorldImpactReviewCamera");objects.Add(cameraObject);
            var camera=cameraObject.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=.75f;
            camera.transform.position=new Vector3(0,0,-10);camera.cullingMask=1<<30;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.07f,.14f);
            var effects=Create(out var pieces);
            for(int i=0;i<3;i++) pieces[i].transform.position=new Vector3((i-1)*.65f,0,0);
            var frame=new RenderTexture(640,360,24);
            var pixels=new Texture2D(640,360,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try
            {
                camera.targetTexture=frame;
                foreach(DogCrush.Core.BoardTheme theme in System.Enum.GetValues(typeof(DogCrush.Core.BoardTheme)))
                {
                    effects.ApplyWorldTheme(theme);effects.Play(pieces.GetRange(0,3),3,0,.7f,true);
                    foreach(var child in effects.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=30;
                    yield return new WaitForSeconds(.12f);
                    camera.Render();RenderTexture.active=frame;
                    pixels.ReadPixels(new Rect(0,0,640,360),0,0);pixels.Apply();
                    string directory=System.IO.Path.Combine(Application.dataPath,"../Builds/visual-qa");
                    System.IO.Directory.CreateDirectory(directory);
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory,"world-impact-"+theme+".png"),pixels.EncodeToPNG());
                    effects.Clear();
                }
            }
            finally {RenderTexture.active=previous;camera.targetTexture=null;frame.Release();Object.Destroy(frame);Object.Destroy(pixels);}
        }

        [UnityTest]
        public IEnumerator BounceFormation_UsesThreeLocalBallsAndStopsWithReducedMotion()
        {
            var effects=Create(out var pieces);
            var target=pieces[0];target.SetSpecial(PieceSpecialType.BallBounce);
            var state=Random.state;
            effects.PlayBounceFormation(target,.7f);
            Assert.That(effects.ActiveCount,Is.EqualTo(3));
            Assert.That(Random.state,Is.EqualTo(state));
            yield return new WaitForSeconds(.1f);
            target.transform.position+=Vector3.down*2f; yield return null;
            foreach(var renderer in effects.GetComponentsInChildren<SpriteRenderer>())
            {
                Assert.That(renderer.sprite,Is.EqualTo(Resources.Load<Sprite>("Pieces/piece-ball-v2")));
                Assert.That(Vector3.Distance(renderer.transform.position,target.transform.position),Is.LessThan(.7f*.8f));
            }
            AccessibilitySettings.ReducedMotion=true;yield return null;
            Assert.That(effects.ActiveCount,Is.Zero);
        }

        [UnityTest]
        public IEnumerator NovaActivation_OnlyDrawsRealCrossWithinBoard()
        {
            var root=new GameObject("NovaReachTest");objects.Add(root);
            var effects=root.AddComponent<ParticleEffectController>();
            var pieceObject=new GameObject("NovaEdge");objects.Add(pieceObject);
            var piece=pieceObject.AddComponent<PieceView>();piece.gridX=0;piece.gridY=0;
            piece.transform.position=new Vector3(-2.1f,-2.1f);
            piece.SetSpecial(PieceSpecialType.MegaBurst);
            effects.PlaySpecialActivation(piece,8,8,.6f);
            var lines=root.GetComponentsInChildren<LineRenderer>();
            Assert.That(lines.Length,Is.EqualTo(4));
            foreach(var line in lines)
            {
                var start=line.GetPosition(0);var end=line.GetPosition(1);
                Assert.That(Vector3.Distance(start,piece.transform.position),Is.LessThan(.001f));
                Assert.That(Mathf.Abs(end.x-start.x)<.001f || Mathf.Abs(end.y-start.y)<.001f,Is.True,
                    "Single supernova never draws a diagonal blast.");
                Assert.That(end.x,Is.InRange(-2.101f,2.101f));
                Assert.That(end.y,Is.InRange(-2.101f,2.101f));
            }
            yield return new WaitForSeconds(.5f);
            Assert.That(root.GetComponent<MatchImpactController>().ActiveCount,Is.Zero);
        }

        [UnityTest]
        public IEnumerator NovaFormation_UsesFourCardinalDirectionsAndClearsOnRestart()
        {
            var effects=Create(out var pieces);
            var target=pieces[0]; target.SetSpecial(PieceSpecialType.MegaBurst);
            effects.PlayNovaFormation(target,pieces.GetRange(1,5),.7f);
            Assert.That(effects.ActiveCount,Is.EqualTo(9));
            yield return new WaitForSeconds(.1f);
            target.transform.position+=Vector3.down*2f;
            yield return null;
            int cross=0;
            foreach(var renderer in effects.GetComponentsInChildren<SpriteRenderer>())
            {
                var offset=renderer.transform.position-target.transform.position;
                if(offset.magnitude>.1f && offset.magnitude<.7f*.73f &&
                    (Mathf.Abs(offset.x)<.001f || Mathf.Abs(offset.y)<.001f)) cross++;
            }
            Assert.That(cross,Is.EqualTo(4));
            effects.Clear(); Assert.That(effects.ActiveCount,Is.Zero);
            AccessibilitySettings.ReducedMotion=true;
            effects.PlayNovaFormation(target,pieces.GetRange(1,5),.7f);
            Assert.That(effects.ActiveCount,Is.Zero);
        }

        [UnityTest]
        public IEnumerator ColorFormation_HasFourOrbitingColorsTracksSurvivorAndCleansUp()
        {
            var effects = Create(out var pieces);
            var target = pieces[0];
            target.SetSpecial(PieceSpecialType.ColorBurst);
            // Keep converging heads distinct from the orbit's radius when the
            // surviving special is moved to simulate gravity.
            for (int i = 1; i <= 4; i++) pieces[i].transform.position += Vector3.right * 5f;
            var state = Random.state;
            effects.PlayColorFormation(target,pieces.GetRange(1,4),.7f);
            Assert.That(effects.ActiveCount, Is.EqualTo(8));
            Assert.That(Random.state, Is.EqualTo(state));
            yield return new WaitForSeconds(.12f);
            target.transform.position += Vector3.down * 2f;
            yield return null;
            var colors = new HashSet<Color>();
            int orbit = 0;
            foreach (var renderer in effects.GetComponentsInChildren<SpriteRenderer>())
            {
                float radius = Vector3.Distance(renderer.transform.position,target.transform.position);
                if (radius > .7f*.24f && radius < .7f*.66f)
                {
                    orbit++;
                    var tint = renderer.color; tint.a = 1f; colors.Add(tint);
                }
            }
            Assert.That(orbit, Is.EqualTo(4));
            Assert.That(colors.Count, Is.EqualTo(4));
            yield return new WaitForSeconds(.4f);
            Assert.That(effects.ActiveCount, Is.Zero);
            AccessibilitySettings.ReducedMotion = true;
            effects.PlayColorFormation(target,pieces.GetRange(1,4),.7f);
            Assert.That(effects.ActiveCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator LineSweep_MarksOnlyActualLaneCellsAndRemainsBounded()
        {
            var effects = Create(out var pieces);
            Vector3 start = new Vector3(-2.1f,0,0), end = new Vector3(2.8f,0,0);
            effects.PlayLineSweep(Vector3.zero,start,end,Color.cyan,.7f);
            Assert.That(effects.ActiveCount, Is.EqualTo(10));
            yield return new WaitForSeconds(.1f);
            int rings = 0;
            foreach (var renderer in effects.GetComponentsInChildren<SpriteRenderer>())
            {
                if (renderer.sprite.texture.name != "MatchRingTexture") continue;
                Assert.That(renderer.transform.position.y, Is.EqualTo(0f).Within(.001f));
                Assert.That(renderer.transform.position.x, Is.InRange(start.x-.001f,end.x+.001f));
                float index = (renderer.transform.position.x-start.x)/.7f;
                Assert.That(index, Is.EqualTo(Mathf.Round(index)).Within(.001f));
                rings++;
            }
            Assert.That(rings, Is.EqualTo(8));
            yield return new WaitForSeconds(.4f);
            Assert.That(effects.ActiveCount, Is.Zero);
            AccessibilitySettings.ReducedMotion = true;
            effects.PlayLineSweep(Vector3.zero,start,end,Color.cyan,.7f);
            Assert.That(effects.ActiveCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator LineFormation_FollowsSurvivingSpecialAndStopsOnReuseOrReducedMotion()
        {
            var effects = Create(out var pieces);
            foreach (var kind in new[] { PieceSpecialType.RowBlast, PieceSpecialType.ColumnBlast, PieceSpecialType.Comet })
            {
                var target = pieces[0];
                target.SetSpecial(kind);
                var random = Random.state;
                effects.PlayLineFormation(target, pieces.GetRange(1,3), .7f);
                Assert.That(effects.ActiveCount, Is.EqualTo(5));
                Assert.That(Random.state, Is.EqualTo(random));
                yield return new WaitForSeconds(.08f);
                target.transform.position += Vector3.down * 2f;
                yield return null;
                int markers = 0;
                foreach (var renderer in effects.GetComponentsInChildren<SpriteRenderer>())
                {
                    var offset = renderer.transform.position - target.transform.position;
                    bool onAxis = kind == PieceSpecialType.Comet ? true :
                        kind == PieceSpecialType.RowBlast ? Mathf.Abs(offset.y) < .001f : Mathf.Abs(offset.x) < .001f;
                    if (onAxis && offset.magnitude > .1f && offset.magnitude < .7f * .47f) markers++;
                }
                Assert.That(markers, Is.EqualTo(2), "Both direction markers follow the falling special.");
                target.SetSpecial(PieceSpecialType.None);
                yield return null;
                yield return null;
                Assert.That(effects.ActiveCount, Is.Zero, "Recycled/changed special cannot retain formation.");
            }
            AccessibilitySettings.ReducedMotion = true;
            pieces[0].SetSpecial(PieceSpecialType.RowBlast);
            effects.PlayLineFormation(pieces[0], pieces.GetRange(1,3), .7f);
            Assert.That(effects.ActiveCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator TripleImpact_UsesThreeLocalDirectionsReusesPoolAndHonorsReducedMotion()
        {
            var effects = Create(out var allPieces);
            var pieces = allPieces.GetRange(0, 3);
            Vector3 center = (pieces[0].transform.position + pieces[1].transform.position + pieces[2].transform.position) / 3f;
            var rng = Random.state;
            effects.Play(pieces, 3, 0, .7f, false);
            Assert.That(Random.state, Is.EqualTo(rng));
            yield return new WaitForSeconds(.08f);
            var directions = new List<Vector3>();
            foreach (var renderer in effects.GetComponentsInChildren<SpriteRenderer>())
            {
                if (renderer.sprite.texture.name != "TripleSpokeTexture") continue;
                Vector3 offset = renderer.transform.position - center;
                Assert.That(offset.magnitude, Is.InRange(.01f, .7f * .41f));
                Assert.That(renderer.color.a, Is.GreaterThan(0f));
                directions.Add(offset.normalized);
            }
            Assert.That(directions.Count, Is.EqualTo(3));
            for (int i = 0; i < 3; i++)
                for (int j = i + 1; j < 3; j++)
                    Assert.That(Vector3.Dot(directions[i], directions[j]), Is.EqualTo(-.5f).Within(.02f));
            yield return new WaitForSeconds(.6f);
            Assert.That(effects.ActiveCount, Is.Zero);
            int created = effects.CreatedCount;
            for (int i = 0; i < 20; i++) { effects.Play(pieces, 3, 0, .7f, false); effects.Clear(); }
            Assert.That(effects.CreatedCount, Is.EqualTo(created));
            AccessibilitySettings.ReducedMotion = true;
            effects.Play(pieces, 3, 0, .7f, false);
            Assert.That(effects.ActiveCount, Is.EqualTo(3));
            foreach (var renderer in effects.GetComponentsInChildren<SpriteRenderer>())
                Assert.That(renderer.sprite.texture.name, Is.Not.EqualTo("TripleSpokeTexture"));
            yield return new WaitForSeconds(.3f);
            Assert.That(effects.ActiveCount, Is.Zero);
        }
        [UnityTest]
        public IEnumerator ClearFeedback_RemovesOnlyOwnedTextsAndRestoresCamera()
        {
            var canvasObject = new GameObject("FeedbackTestCanvas", typeof(Canvas)); objects.Add(canvasObject);
            var host = new GameObject("FeedbackTest"); objects.Add(host);
            var cameraObject = new GameObject("FeedbackTestCamera", typeof(Camera)); objects.Add(cameraObject);
            var feedback = host.AddComponent<FeedbackController>();
            feedback.mainCamera = cameraObject.GetComponent<Camera>();
            feedback.uiCanvas = canvasObject.GetComponent<Canvas>();
            var unrelated = new GameObject("PersistentLabel", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            unrelated.transform.SetParent(canvasObject.transform, false);
            var rest = feedback.mainCamera.transform.position;
            feedback.SpawnFloatingText(Vector3.zero,"OLD SCORE",Color.white);
            feedback.TriggerCameraShake(.3f,.5f);
            yield return null;
            feedback.ClearTransientFeedback();
            Assert.That(feedback.mainCamera.transform.position,Is.EqualTo(rest));
            Assert.That(canvasObject.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Length,Is.EqualTo(1));
            Assert.That(unrelated.activeSelf,Is.True);
            AccessibilitySettings.ReducedMotion = true;
            feedback.SpawnFloatingText(Vector3.zero,"NEW SCORE",Color.white);
            yield return new WaitForSeconds(.2f);
            Assert.That(canvasObject.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Length,Is.EqualTo(2));
            yield return new WaitForSeconds(1f);
            Assert.That(canvasObject.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Length,Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DisableFeedback_CleansCanvasTextsAndAllowsFreshFeedback()
        {
            var canvasObject = new GameObject("FeedbackDisableCanvas", typeof(Canvas)); objects.Add(canvasObject);
            var host = new GameObject("FeedbackDisableTest"); objects.Add(host);
            var feedback = host.AddComponent<FeedbackController>();
            feedback.uiCanvas = canvasObject.GetComponent<Canvas>();
            feedback.SpawnFloatingText(Vector3.zero,"OLD",Color.white);
            host.SetActive(false);
            Assert.That(canvasObject.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Length,Is.Zero);
            feedback.SpawnFloatingText(Vector3.zero,"DISABLED",Color.white);
            yield return null;
            Assert.That(canvasObject.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true).Length,Is.Zero);
            host.SetActive(true);
            feedback.SpawnFloatingText(Vector3.zero,"FRESH",Color.white);
            Assert.That(canvasObject.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Length,Is.EqualTo(1));
            yield return new WaitForSeconds(1.1f);
            Assert.That(canvasObject.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Length,Is.Zero);
        }

        private MatchImpactController Create(out List<PieceView> pieces)
        {
            var go = new GameObject("MatchImpactTest"); objects.Add(go);
            var effects = go.AddComponent<MatchImpactController>();
            pieces = new List<PieceView>();
            for (int i=0;i<7;i++)
            {
                var p = new GameObject("ImpactPiece"); objects.Add(p);
                p.transform.position = new Vector3((i-3)*.45f,0,0);
                pieces.Add(p.AddComponent<PieceView>());
            }
            return effects;
        }

        [UnityTest]
        public IEnumerator CascadeFeedback_StaysLocalBoundedAndUsesCachedMusicalSteps()
        {
            var effects=Create(out var pieces);
            effects.Play(pieces,7,99,.5f,false);
            Assert.That(effects.ActiveCount,Is.LessThanOrEqualTo(12));
            yield return new WaitForSeconds(.09f);
            foreach(var renderer in effects.GetComponentsInChildren<SpriteRenderer>())
            {
                if(!renderer.gameObject.activeSelf) continue;
                float nearest=float.MaxValue;
                foreach(var piece in pieces) nearest=Mathf.Min(nearest,Vector3.Distance(renderer.transform.position,piece.transform.position));
                Assert.That(nearest,Is.LessThan(.25f));
                Assert.That(renderer.transform.localScale.x,Is.LessThanOrEqualTo(.53f));
            }
            yield return new WaitForSeconds(.35f);
            Assert.That(effects.ActiveCount,Is.Zero);
            var host=new GameObject("CascadeSignatureTest");objects.Add(host);
            var audio=host.AddComponent<AudioPlaceholderController>();
            var previous=Random.state;
            Assert.That(audio.GetCascadeSignature(99),Is.SameAs(audio.GetCascadeSignature(8)));
            Assert.That(audio.GetCascadeSignature(0),Is.SameAs(audio.GetCascadeSignature(1)));
            for(int depth=1;depth<=8;depth++)
            {
                var clip=audio.GetCascadeSignature(depth);
                Assert.That(clip.length,Is.InRange(.16f,.18f));
                var samples=new float[clip.samples];clip.GetData(samples,0);
                float peak=0;foreach(var sample in samples) peak=Mathf.Max(peak,Mathf.Abs(sample));
                Assert.That(peak,Is.InRange(.05f,.28f));
            }
            Assert.That(Random.state,Is.EqualTo(previous));
            float oldVolume=audio.SfxVolume;
            try
            {
                audio.SetSfxVolume(1f);
                audio.PlayCascadeSound(4);
                audio.PlaySelectSound(7);
                var source=(AudioSource)typeof(AudioPlaceholderController).GetField("cascadeSource",
                    System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(audio);
                Assert.That(source,Is.Not.SameAs(audio.sfxSource));
                Assert.That(source.pitch,Is.EqualTo(1f));
                audio.SetSfxVolume(0f);
                Assert.That(source.volume,Is.Zero);
            }
            finally { audio.SetSfxVolume(oldVolume); }
        }

        [UnityTest]
        public IEnumerator SpecialAudio_HasDistinctBoundedCachedSignaturesWithoutRandomness()
        {
            var host=new GameObject("SpecialAudioTest");objects.Add(host);
            var audio=host.AddComponent<AudioPlaceholderController>();
            var previous=Random.state;
            var clips=new HashSet<AudioClip>();
            foreach(var type in new[]{PieceSpecialType.RowBlast,PieceSpecialType.ColumnBlast,PieceSpecialType.AreaBlast,
                PieceSpecialType.ColorBurst,PieceSpecialType.MegaBurst,PieceSpecialType.BallBounce,PieceSpecialType.Comet,PieceSpecialType.Whistle})
            {
                var clip=audio.GetSpecialActivationClip(type);
                Assert.That(clips.Add(clip),Is.True);
                Assert.That(audio.GetSpecialActivationClip(type),Is.SameAs(clip));
                Assert.That(clip.length,Is.InRange(.2f,.24f));
                var samples=new float[clip.samples];clip.GetData(samples,0);
                float peak=0f;foreach(var sample in samples) peak=Mathf.Max(peak,Mathf.Abs(sample));
                Assert.That(peak,Is.InRange(.02f,.22f));
            }
            Assert.That(Random.state,Is.EqualTo(previous));
            yield return null;
        }

        [UnityTest]
        public IEnumerator CometFlights_FollowExactDiagonalsAndReturnToSharedPool()
        {
            var effects=Create(out var pieces);
            var origin=new Vector3(2,3);
            var ends=new List<Vector3>{origin+new Vector3(1,1),origin+new Vector3(-2,2)};
            var randomBefore=Random.state;
            effects.PlayCometFlights(origin,ends,.5f);
            yield return null;
            foreach(var renderer in effects.GetComponentsInChildren<SpriteRenderer>())
            {
                if(!renderer.gameObject.activeSelf) continue;
                Assert.That(renderer.sprite,Is.SameAs(Resources.Load<Sprite>("Magic/frisbee-coral-v2")));
                Vector3 delta=renderer.transform.position-origin;
                Assert.That(Mathf.Abs(delta.x),Is.EqualTo(Mathf.Abs(delta.y)).Within(.001f));
            }
            Assert.That(Random.state,Is.EqualTo(randomBefore));
            yield return new WaitForSeconds(.4f);
            Assert.That(effects.ActiveCount,Is.Zero);
            int created=effects.CreatedCount;
            effects.PlayCometFlights(origin,ends,.5f);
            Assert.That(effects.CreatedCount,Is.EqualTo(created));
            effects.Clear();
            Assert.That(effects.ActiveCount,Is.Zero);
        }

        [UnityTest]
        public IEnumerator BallBounces_UseCopiedDestinationsAndReleaseOnReducedMotion()
        {
            var effects=Create(out var pieces);
            var destinations=new List<Vector3>{new Vector3(2,1),new Vector3(-1,2),new Vector3(2,1)};
            var randomBefore=Random.state;
            effects.PlayBallBounces(Vector3.zero,destinations,.5f);
            Assert.That(effects.ActiveCount,Is.EqualTo(2));
            Assert.That(Random.state,Is.EqualTo(randomBefore));
            destinations.Clear();
            yield return new WaitForSeconds(.21f);
            var positions=new HashSet<Vector3>();
            foreach(var renderer in effects.GetComponentsInChildren<SpriteRenderer>())
                if(renderer.gameObject.activeSelf) positions.Add(renderer.transform.position);
            Assert.That(positions.Contains(new Vector3(2,1)),Is.True);
            Assert.That(positions.Contains(new Vector3(-1,2)),Is.True);
            AccessibilitySettings.ReducedMotion=true;
            yield return null;
            Assert.That(effects.ActiveCount,Is.Zero);
            effects.PlayBallBounces(Vector3.zero,new List<Vector3>{Vector3.one,Vector3.right},.5f);
            Assert.That(effects.ActiveCount,Is.EqualTo(2));
            yield return new WaitForSeconds(.25f);
            Assert.That(effects.ActiveCount,Is.Zero);
        }

        [UnityTest]
        public IEnumerator WideCombos_UseAreaAnchorAndSkipLanesOutsideBoard()
        {
            var root = new GameObject("WideComboTest"); objects.Add(root);
            var effects = root.AddComponent<ParticleEffectController>();
            var pieceObject = new GameObject("AreaAnchor"); objects.Add(pieceObject);
            var area = pieceObject.AddComponent<PieceView>();
            const int columns = 8, rows = 8;
            const float spacing = .5f;
            foreach (bool horizontal in new[] { true, false })
            foreach (var cell in new[] { new Vector2Int(0,0), new Vector2Int(7,7), new Vector2Int(3,4) })
            {
                area.gridX = cell.x; area.gridY = cell.y;
                area.transform.position = new Vector3(10 + cell.x * spacing, 20 + cell.y * spacing);
                effects.PlaySpecialCombo(horizontal ? SpecialComboKind.WideRow : SpecialComboKind.WideColumn,
                    Vector3.zero, columns, rows, spacing, area);
                var lines = root.GetComponentsInChildren<LineRenderer>();
                int lane = horizontal ? cell.y : cell.x;
                Assert.That(lines.Length, Is.EqualTo((lane == 0 || lane == 7 ? 2 : 3) * 2));
                foreach (var line in lines)
                {
                    Vector3 start = line.GetPosition(0), end = line.GetPosition(1);
                    Assert.That(start.x, Is.InRange(9.999f,13.501f));
                    Assert.That(end.x, Is.InRange(9.999f,13.501f));
                    Assert.That(start.y, Is.InRange(19.999f,23.501f));
                    Assert.That(end.y, Is.InRange(19.999f,23.501f));
                    Assert.That(horizontal ? start.y : start.x, Is.EqualTo(horizontal ? end.y : end.x).Within(.001f));
                }
                effects.ClearMatchImpacts();
                yield return null;
            }
            AccessibilitySettings.ReducedMotion = true;
            effects.PlaySpecialCombo(SpecialComboKind.WideRow, area.transform.position, columns, rows, spacing, area);
            Assert.That(root.GetComponentsInChildren<LineRenderer>().Length,Is.Zero);
        }
        [UnityTest] public IEnumerator OverlappingCascades_AreBoundedAndReuseTheirPool()
        {
            var effects = Create(out var pieces);
            for (int i=0;i<20;i++) effects.Play(pieces,7,i, .55f,false);
            Assert.That(effects.ActiveCount,Is.LessThanOrEqualTo(40));
            int created = effects.CreatedCount;
            yield return new WaitForSeconds(.85f);
            Assert.That(effects.ActiveCount,Is.Zero);
            effects.Play(pieces,5,1,.55f,false);
            Assert.That(effects.CreatedCount,Is.EqualTo(created));
            effects.Clear();
            Assert.That(effects.ActiveCount,Is.Zero);
        }
        [UnityTest] public IEnumerator ReducedMotion_DoesNotEmitTravellingStarsAndCancelsExistingMotion()
        {
            var effects = Create(out var pieces);
            effects.Play(pieces,7,3,.55f,false);
            yield return null;
            AccessibilitySettings.ReducedMotion=true;
            yield return null;
            yield return null;
            Assert.That(effects.ActiveCount,Is.Zero);
            effects.Play(pieces,7,3,.55f,false);
            Assert.That(effects.ActiveCount,Is.EqualTo(3));
            yield return new WaitForSeconds(.25f);
            Assert.That(effects.ActiveCount,Is.Zero);
        }
        [UnityTest] public IEnumerator DisableAndReenable_ReleasesImpactsWithoutGrowingPool()
        {
            var effects = Create(out var pieces);
            effects.Play(pieces,7,2,.55f,false);
            int created=effects.CreatedCount;
            effects.gameObject.SetActive(false);
            Assert.That(effects.ActiveCount,Is.Zero);
            effects.gameObject.SetActive(true);
            effects.Play(pieces,7,2,.55f,false);
            Assert.That(effects.CreatedCount,Is.EqualTo(created));
            yield return null;
        }

        [UnityTest] public IEnumerator MatchBurst_ActuallyEmitsParticlesInsteadOfIgnoringAnUnconfiguredBurst()
        {
            var root = new GameObject("RealParticleBurstTest"); objects.Add(root);
            var effects = root.AddComponent<ParticleEffectController>();
            effects.PlayMatchBurst(Vector3.zero,Color.cyan,9);
            yield return null;
            yield return null;
            var ps = root.GetComponentInChildren<ParticleSystem>();
            Assert.That(ps,Is.Not.Null);
            Assert.That(ps.emission.burstCount,Is.EqualTo(1));
            Assert.That(ps.emission.rateOverTime.constant,Is.Zero);
            Assert.That(ps.particleCount,Is.GreaterThan(0));
        }

        [UnityTest] public IEnumerator ClearEffects_CancelsActiveAndDelayedWavesAndReusesParticlesOnce()
        {
            var root=new GameObject("EffectsResetTest");objects.Add(root);
            var effects=root.AddComponent<ParticleEffectController>();
            effects.PlayMegaBlast(Vector3.zero,9,10,.55f);
            effects.PlayCombinationAccent(Vector3.zero,7);
            effects.PlayMatchBurst(Vector3.zero,Color.cyan,9);
            var original=root.GetComponentInChildren<ParticleSystem>();
            effects.ClearMatchImpacts();
            Assert.That(root.GetComponentsInChildren<LineRenderer>().Length,Is.Zero);
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Length,Is.Zero);
            Assert.That(original.particleCount,Is.Zero);
            yield return new WaitForSeconds(.3f);
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Length,Is.Zero,"Delayed shockwaves must not reappear.");
            effects.PlayMatchBurst(Vector3.zero,Color.cyan,9);
            Assert.That(root.GetComponentInChildren<ParticleSystem>(),Is.SameAs(original));
            yield return new WaitForSeconds(1.1f);
            effects.PlayMatchBurst(Vector3.zero,Color.cyan,9);
            effects.PlayMatchBurst(Vector3.one,Color.yellow,9);
            Assert.That(root.GetComponentsInChildren<ParticleSystem>().Length,Is.EqualTo(2),"Each simultaneous burst owns a distinct system.");
            Assert.That(root.GetComponentsInChildren<ParticleSystem>(true).Length,Is.EqualTo(2));
        }

        [UnityTest] public IEnumerator DisableEffects_ClearsOldBeamsAndAllowsFreshFeedback()
        {
            var root=new GameObject("EffectsDisableTest");objects.Add(root);
            var effects=root.AddComponent<ParticleEffectController>();
            effects.PlayMegaBlast(Vector3.zero,9,10,.55f);
            effects.PlayMatchBurst(Vector3.zero,Color.cyan,9);
            root.SetActive(false);root.SetActive(true);
            yield return null;
            Assert.That(root.GetComponentsInChildren<LineRenderer>().Length,Is.Zero);
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Length,Is.Zero);
            effects.PlayCombinationAccent(Vector3.zero,7);
            effects.PlayMatchBurst(Vector3.zero,Color.cyan,9);
            yield return null;
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Length,Is.GreaterThan(0));
            Assert.That(root.GetComponentsInChildren<ParticleSystem>().Length,Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator ClearEffects_RetainsOnlyTheBoundedParticlePool()
        {
            var root=new GameObject("ParticlePoolResetTest");objects.Add(root);
            var effects=root.AddComponent<ParticleEffectController>();
            for(int i=0;i<40;i++) effects.PlayMatchBurst(Vector3.zero,Color.cyan,3);
            effects.ClearMatchImpacts();yield return null;
            Assert.That(root.GetComponentsInChildren<ParticleSystem>(true).Length,Is.EqualTo(28));
            for(int i=0;i<28;i++) effects.PlayMatchBurst(Vector3.zero,Color.cyan,3);
            Assert.That(root.GetComponentsInChildren<ParticleSystem>().Length,Is.EqualTo(28));
            Assert.That(root.GetComponentsInChildren<ParticleSystem>(true).Length,Is.EqualTo(28));
        }

        [UnityTest] public IEnumerator LineSpecials_StayWithinActualBoardEndpointsAtEveryEdge()
        {
            foreach(var cell in new[]{new Vector2Int(0,3),new Vector2Int(8,3),new Vector2Int(4,0),new Vector2Int(4,9)})
            {
                var root=new GameObject("BoundedLineTest");objects.Add(root);
                var effects=root.AddComponent<ParticleEffectController>();
                var pieceObject=new GameObject("EdgeSpecial");objects.Add(pieceObject);
                var piece=pieceObject.AddComponent<PieceView>();piece.gridX=cell.x;piece.gridY=cell.y;
                bool row=cell.y==3;
                piece.transform.position=new Vector3((cell.x-4)*.55f,(cell.y-4)*.55f);
                piece.SetSpecial(row ? PieceSpecialType.RowBlast : PieceSpecialType.ColumnBlast);
                effects.PlaySpecialActivation(piece,9,10,.55f);
                Vector3 start=row ? new Vector3(-2.2f,piece.transform.position.y) : new Vector3(0,-2.2f);
                Vector3 end=row ? new Vector3(2.2f,piece.transform.position.y) : new Vector3(0,2.75f);
                var lines=root.GetComponentsInChildren<LineRenderer>();
                Assert.That(lines.Length,Is.EqualTo(2));
                foreach(var line in lines)
                {
                    Assert.That(Vector3.Distance(line.GetPosition(0),start),Is.LessThan(.001f));
                    Assert.That(Vector3.Distance(line.GetPosition(1),end),Is.LessThan(.001f));
                }
                // One travelling head at an edge, plus one arrival per real cell.
                Assert.That(root.GetComponent<MatchImpactController>().ActiveCount,Is.EqualTo(1 + (row ? 9 : 10)));
                yield return new WaitForSeconds(.5f);
                Assert.That(root.GetComponent<MatchImpactController>().ActiveCount,Is.Zero);
                Assert.That(root.GetComponentsInChildren<LineRenderer>().Length,Is.Zero);
            }
        }

        [UnityTest] public IEnumerator LineSpecials_ReducedMotionStopsBeamsAndTravellers()
        {
            var root=new GameObject("LineAccessibilityTest");objects.Add(root);
            var effects=root.AddComponent<ParticleEffectController>();
            var pieceObject=new GameObject("LineSpecial");objects.Add(pieceObject);
            var piece=pieceObject.AddComponent<PieceView>();piece.gridX=4;piece.gridY=4;
            piece.SetSpecial(PieceSpecialType.RowBlast);
            effects.PlaySpecialActivation(piece,9,10,.55f);
            yield return null;
            AccessibilitySettings.ReducedMotion=true;
            yield return null;yield return null;
            Assert.That(root.GetComponentsInChildren<LineRenderer>().Length,Is.Zero);
            Assert.That(root.GetComponent<MatchImpactController>().ActiveCount,Is.Zero);
            effects.PlaySpecialActivation(piece,9,10,.55f);
            Assert.That(root.GetComponentsInChildren<LineRenderer>().Length,Is.Zero);
            Assert.That(root.GetComponent<MatchImpactController>().ActiveCount,Is.Zero);
        }

        [UnityTest] public IEnumerator ColorSweep_CopiesTargetsBeforePiecesAreRecycled()
        {
            var effects = Create(out var pieces);
            var target = pieces[6];
            Vector3 destination = target.transform.position;
            effects.PlayColorSweep(new Vector3(-2f, -1f), new[] { target, target }, .55f);
            Assert.That(effects.ActiveCount, Is.EqualTo(2), "Duplicate targets must not create duplicate flights.");
            target.transform.position = new Vector3(100f, 100f);
            yield return new WaitForSeconds(.31f);
            foreach (var renderer in effects.GetComponentsInChildren<SpriteRenderer>())
                Assert.That(Vector3.Distance(renderer.transform.position, destination), Is.LessThan(.4f));
            yield return new WaitForSeconds(.3f);
            Assert.That(effects.ActiveCount, Is.Zero);
        }

        [UnityTest] public IEnumerator ColorSweep_RespectsPoolAndReducedMotionDuringFlight()
        {
            var effects = Create(out var pieces);
            for(int i=0;i<20;i++) effects.PlayColorSweep(Vector3.down, pieces, .55f);
            Assert.That(effects.ActiveCount, Is.LessThanOrEqualTo(40));
            int created = effects.CreatedCount;
            yield return null;
            AccessibilitySettings.ReducedMotion = true;
            yield return null;
            Assert.That(effects.ActiveCount, Is.Zero);
            effects.PlayColorSweep(Vector3.down, pieces, .55f);
            Assert.That(effects.ActiveCount, Is.EqualTo(3));
            var renderers = effects.GetComponentsInChildren<SpriteRenderer>();
            var positions = new List<Vector3>();
            foreach(var renderer in renderers) positions.Add(renderer.transform.position);
            yield return new WaitForSeconds(.08f);
            for(int i=0;i<renderers.Length;i++) Assert.That(renderers[i].transform.position, Is.EqualTo(positions[i]));
            effects.gameObject.SetActive(false);
            Assert.That(effects.ActiveCount, Is.Zero);
            effects.gameObject.SetActive(true);
            Assert.That(effects.CreatedCount, Is.EqualTo(created));
        }

        [UnityTest] public IEnumerator ColorSweep_RenderFlightAndArrivalForVisualReview()
        {
            var cameraObject = new GameObject("SweepReviewCamera"); objects.Add(cameraObject);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 2.4f;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.cullingMask = 1<<30;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.035f, .07f, .14f);
            var effects = Create(out var pieces);
            var frame = new RenderTexture(640,640,24);
            var pixels = new Texture2D(640,640,TextureFormat.RGB24,false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = frame;
                for(int i=0;i<pieces.Count;i++)
                {
                    float angle = i * Mathf.PI * 2f / pieces.Count;
                    pieces[i].transform.position = new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)*1.7f;
                    pieces[i].Initialize(PieceType.Ball,i,0,Resources.Load<Sprite>("Pieces/piece-ball-v2"),Color.white);
                    foreach(var child in pieces[i].GetComponentsInChildren<Transform>(true)) child.gameObject.layer=30;
                }
                effects.PlayColorSweep(Vector3.zero, pieces, .55f);
                foreach(var child in effects.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=30;
                for(int shot=0;shot<3;shot++)
                {
                    yield return new WaitForSeconds(shot==0 ? .10f : .12f);
                    camera.Render(); RenderTexture.active=frame;
                    pixels.ReadPixels(new Rect(0,0,640,640),0,0);pixels.Apply();
                    string directory=System.IO.Path.Combine(Application.dataPath,"../Builds/visual-qa");
                    System.IO.Directory.CreateDirectory(directory);
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory,"color-sweep-"+shot+".png"),pixels.EncodeToPNG());
                    Assert.That(effects.ActiveCount,Is.GreaterThan(0));
                }
            }
            finally
            {
                RenderTexture.active=previous;camera.targetTexture=null;
                frame.Release();Object.Destroy(frame);Object.Destroy(pixels);
            }
        }

        [UnityTest] public IEnumerator RenderMatchTiers_ForVisualReview()
        {
            var cameraObject = new GameObject("ImpactReviewCamera"); objects.Add(cameraObject);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic=true; camera.orthographicSize=1.4f;
            camera.transform.position=new Vector3(0,0,-10);
            camera.cullingMask=1<<30;
            camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.04f,.07f,.16f);
            var frame=new RenderTexture(640,400,24);
            var pixels=new Texture2D(640,400,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;
            try
            {
                camera.targetTexture=frame;
                foreach (int tier in new[]{3,4,5,7})
                {
                    var effects=Create(out var allPieces);
                    var pieces=allPieces.GetRange(0,tier);
                    for(int i=0;i<allPieces.Count;i++)
                    {
                        var piece=allPieces[i];
                        piece.gameObject.SetActive(i<tier);
                        if(i>=tier) continue;
                        piece.transform.position=new Vector3((i-(tier-1)*.5f)*.55f,0,0);
                        piece.Initialize(PieceType.Ball,i,0,Resources.Load<Sprite>("Pieces/piece-ball-v2"),Color.white);
                    }
                    effects.Play(pieces,tier,tier==7 ? 2 : 0,.55f,false);
                    foreach(var child in effects.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=30;
                    foreach(var piece in pieces)
                    {
                        foreach(var child in piece.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=30;
                        piece.AnimateDespawn(.05f,null);
                    }
                    yield return new WaitForSeconds(.17f);
                    camera.Render(); RenderTexture.active=frame;
                    pixels.ReadPixels(new Rect(0,0,640,400),0,0);pixels.Apply();
                    string directory=System.IO.Path.Combine(Application.dataPath,"../Builds/visual-qa");
                    System.IO.Directory.CreateDirectory(directory);
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory,"match-impact-"+tier+".png"),pixels.EncodeToPNG());
                    Assert.That(effects.ActiveCount,Is.GreaterThan(0));
                    effects.gameObject.SetActive(false);
                    foreach(var piece in allPieces) piece.gameObject.SetActive(false);
                }
            }
            finally
            {
                RenderTexture.active=previous;camera.targetTexture=null;
                frame.Release();Object.Destroy(frame);Object.Destroy(pixels);
            }
        }
    }
}
