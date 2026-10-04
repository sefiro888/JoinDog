using System.Collections;
using DogCrush.Core;
using DogCrush.Presentation;
using DogCrush.UI;
using JoinDog.App;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DogCrush.Tests.PlayMode
{
    public class VisualExperienceTests
    {
        [UnityTest]
        public IEnumerator CreationPreview_CoversLongLinesCrossFrisbeeAndExistingSpecials()
        {
            SceneManager.LoadScene("Gameplay");yield return null;yield return null;
            var bootstrap=Object.FindAnyObjectByType<GameBootstrap>();bootstrap.currentLevel=1;
            bootstrap.StartNewMatch();yield return new WaitForSecondsRealtime(4.5f);
            var board=bootstrap.boardController;
            foreach(int scenario in new[]{0,1,2,3,4,5,6,7})
            {
                for(int x=0;x<board.Columns;x++) for(int y=0;y<board.Rows;y++)
                {
                    var piece=board.GetPieceAt(x,y);
                    piece.Initialize((x+y)%2==0?DogCrush.Board.PieceType.Ball:DogCrush.Board.PieceType.Bone,
                        x,y,null,Color.white);
                    piece.SetSpecial(DogCrush.Board.PieceSpecialType.None);
                }
                var type=scenario==4?DogCrush.Board.PieceType.Frisbee:DogCrush.Board.PieceType.Dog;
                int length=scenario<3?scenario+5:scenario==6?3:4;
                if(scenario==3) length=3;
                for(int x=0;x<length;x++) if(x!=3)
                    board.GetPieceAt(x,3).Initialize(type,x,3,null,Color.white);
                if(scenario==3)
                {
                    board.GetPieceAt(3,3).Initialize(type,3,3,null,Color.white);
                    board.GetPieceAt(3,4).Initialize(type,3,4,null,Color.white);
                    board.GetPieceAt(3,5).Initialize(type,3,5,null,Color.white);
                }
                // Cross uses the dragged piece at (2,3); lines use (3,3).
                int targetX=scenario==3?2:scenario==6?2:3;
                if(scenario==3)
                {
                    board.GetPieceAt(2,4).Initialize(type,2,4,null,Color.white);
                    board.GetPieceAt(2,5).Initialize(type,2,5,null,Color.white);
                }
                var first=board.GetPieceAt(targetX,2);var second=board.GetPieceAt(targetX,3);
                first.Initialize(type,targetX,2,null,Color.white);
                // Remove the destination so the run only forms after exchanging.
                second.Initialize(DogCrush.Board.PieceType.Bone,targetX,3,null,Color.white);
                if(scenario==5) board.GetPieceAt(0,3).SetSpecial(DogCrush.Board.PieceSpecialType.RowBlast);
                if(scenario==7) first.SetSpecial(DogCrush.Board.PieceSpecialType.ColorBurst);
                var random=Random.state;
                bool predicted=board.TryGetSpecialCreationPreview(first,second,out var cell,out var kind);
                Assert.That(Random.state,Is.EqualTo(random));
                Assert.That(board.GetPieceAt(targetX,2),Is.SameAs(first));
                if(scenario>=5)
                {
                    Assert.That(predicted,Is.False,"No creation for triple or activation: "+scenario);
                    continue;
                }
                Assert.That(predicted,Is.True,"Scenario "+scenario);
                var expected=new[]{DogCrush.Board.PieceSpecialType.ColorBurst,
                    DogCrush.Board.PieceSpecialType.MegaBurst,DogCrush.Board.PieceSpecialType.BallBounce,
                    DogCrush.Board.PieceSpecialType.AreaBlast,DogCrush.Board.PieceSpecialType.Comet}[scenario];
                Assert.That(kind,Is.EqualTo(expected));
                Assert.That(board.TrySwapAndFindMatches(first,second,out var matches),Is.True);
                var actual=board.BuildMatchResolution(matches);
                Assert.That(actual.CreatedSpecial,Is.SameAs(first));
                Assert.That(actual.CreatedSpecialType,Is.EqualTo(kind));
                Assert.That(cell,Is.EqualTo(new Vector2Int(first.gridX,first.gridY)));
            }
            bootstrap.StartNewMatch();
        }

        [UnityTest]
        public IEnumerator CreationPreview_PredictsActualSurvivorWithoutChangingBoardAndCancelsCleanly()
        {
            SceneManager.LoadScene("Gameplay");yield return null;yield return null;
            var bootstrap=Object.FindAnyObjectByType<GameBootstrap>();bootstrap.currentLevel=1;
            bootstrap.StartNewMatch();bootstrap.uiController.SetMainMenuVisible(false);
            yield return new WaitForSecondsRealtime(4.5f);
            var board=bootstrap.boardController;
            for(int x=0;x<board.Columns;x++) for(int y=0;y<board.Rows;y++)
            {
                var piece=board.GetPieceAt(x,y);
                var type=(x+y)%2==0?DogCrush.Board.PieceType.Ball:DogCrush.Board.PieceType.Bone;
                piece.Initialize(type,x,y,Resources.Load<Sprite>((x+y)%2==0?
                    "Pieces/piece-ball-v2":"Pieces/piece-bone-v2"),Color.white);
                piece.SetSpecial(DogCrush.Board.PieceSpecialType.None);
            }
            for(int x=0;x<3;x++) board.GetPieceAt(x,3).Initialize(DogCrush.Board.PieceType.Dog,x,3,
                Resources.Load<Sprite>("Pieces/piece-dog-v1"),Color.white);
            var first=board.GetPieceAt(3,2);var second=board.GetPieceAt(3,3);
            first.Initialize(DogCrush.Board.PieceType.Dog,3,2,Resources.Load<Sprite>("Pieces/piece-dog-v1"),Color.white);
            var random=Random.state;
            Assert.That(board.TryGetSpecialCreationPreview(first,second,out var cell,out var kind),Is.True);
            Assert.That(cell,Is.EqualTo(new Vector2Int(3,3)));
            Assert.That(kind,Is.EqualTo(DogCrush.Board.PieceSpecialType.RowBlast));
            Assert.That(Random.state,Is.EqualTo(random));
            Assert.That(board.GetPieceAt(3,2),Is.SameAs(first));
            Assert.That(first.gridY,Is.EqualTo(2));
            board.PreviewSwap(first,second);yield return new WaitForSecondsRealtime(.12f);
            BoardPlayModeTests.CaptureGameplayState("creation-preview-gameplay.png");
            board.RestorePreviewSwap(first,second);
            var view=board.GetComponent<DogCrush.Board.AdaptiveBoardView>();
            var colors=(System.Collections.IDictionary)typeof(DogCrush.Board.AdaptiveBoardView).GetField("previewColors",
                System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(view);
            Assert.That(colors.Count,Is.Zero);
            Assert.That(board.TrySwapAndFindMatches(first,second,out var matches),Is.True);
            var resolution=board.BuildMatchResolution(matches);
            Assert.That(resolution.CreatedSpecial,Is.SameAs(first));
            Assert.That(new Vector2Int(first.gridX,first.gridY),Is.EqualTo(cell));
            Assert.That(resolution.CreatedSpecialType,Is.EqualTo(kind));
            bootstrap.StartNewMatch();
        }

        [UnityTest]
        public IEnumerator WorldImpacts_AreIntegratedInForestCoastAndMountainGameplay()
        {
            bool reduced=AccessibilitySettings.ReducedMotion;
            try
            {
                AccessibilitySettings.ReducedMotion=false;
                SceneManager.LoadScene("Gameplay");yield return null;yield return null;
                var bootstrap=Object.FindAnyObjectByType<GameBootstrap>();
                foreach(int level in new[]{11,31,41})
                {
                    bootstrap.currentLevel=level;bootstrap.StartNewMatch();bootstrap.uiController.SetMainMenuVisible(false);
                    yield return new WaitForSecondsRealtime(4.5f);
                    var board=bootstrap.boardController;
                    for(int x=0;x<board.Columns;x++) for(int y=0;y<board.Rows;y++)
                    {
                        var piece=board.GetPieceAt(x,y);if(piece==null) continue;
                        var type=(x+y)%2==0?DogCrush.Board.PieceType.Ball:DogCrush.Board.PieceType.Bone;
                        piece.Initialize(type,x,y,Resources.Load<Sprite>((x+y)%2==0?
                            "Pieces/piece-ball-v2":"Pieces/piece-bone-v2"),Color.white);
                        piece.SetSpecial(DogCrush.Board.PieceSpecialType.None);
                    }
                    var matches=new System.Collections.Generic.List<DogCrush.Board.PieceView>();
                    for(int y=board.Rows/2;y<board.Rows && matches.Count==0;y++)
                        for(int x=0;x<board.Columns-2 && matches.Count==0;x++)
                            if(board.GetPieceAt(x,y)!=null && board.GetPieceAt(x+1,y)!=null && board.GetPieceAt(x+2,y)!=null)
                                for(int i=0;i<3;i++) matches.Add(board.GetPieceAt(x+i,y));
                    Assert.That(matches.Count,Is.EqualTo(3));
                    foreach(var piece in matches) piece.Initialize(DogCrush.Board.PieceType.Dog,piece.gridX,piece.gridY,
                        Resources.Load<Sprite>("Pieces/piece-dog-v1"),Color.white);
                    typeof(GameBootstrap).GetMethod("HandleChainCompleted",System.Reflection.BindingFlags.NonPublic|
                        System.Reflection.BindingFlags.Instance).Invoke(bootstrap,new object[]{matches});
                    yield return new WaitForSecondsRealtime(.12f);
                    int motifs=0;
                    foreach(var renderer in bootstrap.particleController.GetComponentsInChildren<SpriteRenderer>())
                        if(renderer.sprite!=null && renderer.sprite.texture.name=="WorldImpact_"+board.config.boardTheme) motifs++;
                    Assert.That(motifs,Is.EqualTo(3));
                    BoardPlayModeTests.CaptureGameplayState("world-impact-gameplay-"+level+".png");
                }
                bootstrap.StartNewMatch();AccessibilitySettings.ReducedMotion=true;
                var effects=bootstrap.particleController.GetComponent<MatchImpactController>();
                effects.Play(new System.Collections.Generic.List<DogCrush.Board.PieceView>{
                    bootstrap.boardController.GetPieceAt(3,3)},3,0,bootstrap.boardController.ActivePieceSpacing,true);
                yield return new WaitForSecondsRealtime(.04f);
                BoardPlayModeTests.CaptureGameplayState("world-impact-gameplay-reduced.png");
            }
            finally {AccessibilitySettings.ReducedMotion=reduced;}
        }

        [UnityTest]
        public IEnumerator FiveMatch_CreatesColorBurstAndFlightsFollowActualColorTargets()
            => MatchFormation(5);

        [UnityTest]
        public IEnumerator SixMatch_CreatesNovaWithColorAndCrossIdentity()
            => MatchFormation(6);

        [UnityTest]
        public IEnumerator SevenMatch_CreatesBounceWithDistinctLocalFormation()
            => MatchFormation(7);

        private IEnumerator MatchFormation(int count)
        {
            string prefix=count==5?"five":count==6?"six":"seven";
            var kind=count==5?DogCrush.Board.PieceSpecialType.ColorBurst:
                count==6?DogCrush.Board.PieceSpecialType.MegaBurst:DogCrush.Board.PieceSpecialType.BallBounce;
            bool reduced = AccessibilitySettings.ReducedMotion;
            try
            {
                AccessibilitySettings.ReducedMotion = false;
                SceneManager.LoadScene("Gameplay");
                yield return null; yield return null;
                var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
                bootstrap.currentLevel = 1;
                bootstrap.StartNewMatch();
                bootstrap.uiController.SetMainMenuVisible(false);
                yield return new WaitForSecondsRealtime(4.5f);
                var board = bootstrap.boardController;
                for(int x=0;x<board.Columns;x++) for(int y=0;y<board.Rows;y++)
                {
                    var piece=board.GetPieceAt(x,y);
                    var type=(x+y)%2==0?DogCrush.Board.PieceType.Ball:DogCrush.Board.PieceType.Bone;
                    piece.Initialize(type,x,y,Resources.Load<Sprite>((x+y)%2==0?
                        "Pieces/piece-ball-v2":"Pieces/piece-bone-v2"),Color.white);
                    piece.SetSpecial(DogCrush.Board.PieceSpecialType.None);
                }
                var matches=new System.Collections.Generic.List<DogCrush.Board.PieceView>();
                for(int x=1;x<1+count;x++)
                {
                    var piece=board.GetPieceAt(x,3);
                    piece.Initialize(DogCrush.Board.PieceType.Dog,x,3,Resources.Load<Sprite>("Pieces/piece-dog-v1"),Color.white);
                    matches.Add(piece);
                }
                var handler=typeof(GameBootstrap).GetMethod("HandleChainCompleted",System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
                handler.Invoke(bootstrap,new object[]{matches});
                int specials=0;
                foreach(var piece in matches) if(piece.IsSpecial)
                { specials++; Assert.That(piece.SpecialType,Is.EqualTo(kind)); }
                Assert.That(specials,Is.EqualTo(1));
                Assert.That((int)typeof(GameBootstrap).GetField("companionCharge",System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance).GetValue(bootstrap),Is.EqualTo(1),
                    "Preparing a special contributes to companion help.");
                Assert.That(bootstrap.uiController.comboBannerText.gameObject.activeSelf,Is.False);
                yield return new WaitForSecondsRealtime(.16f);
                BoardPlayModeTests.CaptureGameplayState(prefix+"-formation-gameplay.png");
                bootstrap.StartNewMatch();
                yield return new WaitForSecondsRealtime(4.5f);
                foreach(var special in board.GetSpecialPieces()) special.SetSpecial(DogCrush.Board.PieceSpecialType.None);
                var burst=board.GetPieceAt(3,3);
                burst.Initialize(DogCrush.Board.PieceType.Dog,3,3,Resources.Load<Sprite>("Pieces/piece-dog-v1"),Color.white);
                burst.SetSpecial(kind);
                handler.Invoke(bootstrap,new object[]{new System.Collections.Generic.List<DogCrush.Board.PieceView>{burst}});
                yield return new WaitForSecondsRealtime(count==5?.22f:.16f);
                BoardPlayModeTests.CaptureGameplayState(prefix+(count==5?"-flight-gameplay.png":"-activation-gameplay.png"));
                bootstrap.StartNewMatch();
            }
            finally { AccessibilitySettings.ReducedMotion=reduced; }
        }

        [UnityTest]
        public IEnumerator FourMatch_CreatesSurvivingDirectionalSpecialWithFormationFeedback()
        {
            bool reduced = AccessibilitySettings.ReducedMotion;
            try
            {
                AccessibilitySettings.ReducedMotion = false;
                SceneManager.LoadScene("Gameplay");
                yield return null;
                yield return null;
                var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
                bootstrap.currentLevel = 1;
                bootstrap.StartNewMatch();
                bootstrap.uiController.SetMainMenuVisible(false);
                yield return new WaitForSecondsRealtime(4.5f);
                var board = bootstrap.boardController;
                for (int x = 0; x < board.Columns; x++) for (int y = 0; y < board.Rows; y++)
                {
                    var piece = board.GetPieceAt(x,y);
                    var type = (x+y)%2 == 0 ? DogCrush.Board.PieceType.Ball : DogCrush.Board.PieceType.Bone;
                    piece.Initialize(type,x,y,Resources.Load<Sprite>((x+y)%2 == 0 ?
                        "Pieces/piece-ball-v2" : "Pieces/piece-bone-v2"),Color.white);
                    piece.SetSpecial(DogCrush.Board.PieceSpecialType.None);
                }
                var matches = new System.Collections.Generic.List<DogCrush.Board.PieceView>();
                for (int x = 2; x < 6; x++)
                {
                    var piece = board.GetPieceAt(x,3);
                    piece.Initialize(DogCrush.Board.PieceType.Dog,x,3,Resources.Load<Sprite>("Pieces/piece-dog-v1"),Color.white);
                    matches.Add(piece);
                }
                typeof(GameBootstrap).GetMethod("HandleMatch3Move",System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance).Invoke(bootstrap,new object[] {matches});
                int surviving = 0;
                foreach (var piece in matches) if (piece.IsSpecial)
                {
                    surviving++;
                    Assert.That(piece.SpecialType, Is.EqualTo(DogCrush.Board.PieceSpecialType.RowBlast));
                }
                Assert.That(surviving, Is.EqualTo(1));
                Assert.That((int)typeof(GameBootstrap).GetField("companionCharge",System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance).GetValue(bootstrap),Is.EqualTo(1));
                Assert.That(bootstrap.uiController.comboBannerText.gameObject.activeSelf, Is.False,
                    "Four creation keeps its title in the companion tray, away from the board.");
                yield return new WaitForSecondsRealtime(.12f);
                Assert.That(bootstrap.particleController.GetComponent<MatchImpactController>().ActiveCount, Is.GreaterThan(0));
                BoardPlayModeTests.CaptureGameplayState("four-formation-gameplay.png");
                bootstrap.StartNewMatch();
                yield return new WaitForSecondsRealtime(4.5f);
                foreach (var special in board.GetSpecialPieces()) special.SetSpecial(DogCrush.Board.PieceSpecialType.None);
                var ray = board.GetPieceAt(3,3);
                ray.SetSpecial(DogCrush.Board.PieceSpecialType.RowBlast);
                typeof(GameBootstrap).GetMethod("HandleChainCompleted",System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance).Invoke(bootstrap,new object[] {
                        new System.Collections.Generic.List<DogCrush.Board.PieceView> {ray} });
                yield return new WaitForSecondsRealtime(.12f);
                BoardPlayModeTests.CaptureGameplayState("four-lane-activation.png");
                bootstrap.StartNewMatch();
            }
            finally { AccessibilitySettings.ReducedMotion = reduced; }
        }

        [UnityTest]
        public IEnumerator TripleImpact_InGameplayRemovesOnlyMatchedCellsAndLeavesInputTimingUnchanged()
        {
            bool motion = AccessibilitySettings.ReducedMotion;
            try
            {
                AccessibilitySettings.ReducedMotion = false;
                SceneManager.LoadScene("Gameplay");
                yield return null;
                yield return null;
                var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
                bootstrap.StartNewMatch();
                bootstrap.uiController.SetMainMenuVisible(false);
                yield return new WaitForSecondsRealtime(4.5f);
                var board = bootstrap.boardController;
                var pieces = new System.Collections.Generic.List<DogCrush.Board.PieceView>();
                var originals = new DogCrush.Board.PieceView[board.Columns, board.Rows];
                for (int x = 0; x < board.Columns; x++) for (int y = 0; y < board.Rows; y++)
                {
                    var piece = board.GetPieceAt(x,y);
                    var type = (x+y)%2 == 0 ? DogCrush.Board.PieceType.Ball : DogCrush.Board.PieceType.Bone;
                    piece.Initialize(type,x,y,Resources.Load<Sprite>((x+y)%2 == 0 ?
                        "Pieces/piece-ball-v2" : "Pieces/piece-bone-v2"),Color.white);
                    piece.SetSpecial(DogCrush.Board.PieceSpecialType.None);
                    originals[x,y] = piece;
                }
                for (int x = 0; x < 3; x++)
                {
                    var piece = board.GetPieceAt(x, board.Rows / 2);
                    piece.Initialize(DogCrush.Board.PieceType.Dog, x, board.Rows / 2,
                        Resources.Load<Sprite>("Pieces/piece-dog-v1"), Color.white);
                    pieces.Add(piece);
                }
                typeof(GameBootstrap).GetMethod("HandleMatch3Move",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Invoke(bootstrap, new object[] { pieces });
                yield return new WaitForSecondsRealtime(.065f);
                var effects = bootstrap.particleController.GetComponent<MatchImpactController>();
                Assert.That(effects, Is.Not.Null, "A playable triple invokes gameplay feedback.");
                int branches = 0;
                foreach (var renderer in effects.GetComponentsInChildren<SpriteRenderer>())
                    if (renderer.sprite.texture.name == "TripleSpokeTexture") branches++;
                Assert.That(branches, Is.EqualTo(3));
                Assert.That((int)typeof(GameBootstrap).GetField("companionCharge",System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance).GetValue(bootstrap),Is.Zero,
                    "An ordinary triple stays local; cascades and specials earn companion help.");
                for (int x = 0; x < board.Columns; x++) for (int y = 0; y < board.Rows; y++)
                {
                    if (y == board.Rows / 2 && x < 3) Assert.That(board.GetPieceAt(x,y), Is.Null);
                    else Assert.That(board.GetPieceAt(x,y), Is.SameAs(originals[x,y]), "Decorative branches do not remove neighbours.");
                }
                BoardPlayModeTests.CaptureGameplayState("triple-impact-gameplay.png");
                bootstrap.StartNewMatch();
                Assert.That(effects.ActiveCount, Is.Zero, "Restart cleans all branches.");
                AccessibilitySettings.ReducedMotion = true;
                effects.Play(new System.Collections.Generic.List<DogCrush.Board.PieceView> {
                    board.GetPieceAt(0,0), board.GetPieceAt(1,0), board.GetPieceAt(2,0) }, 3, 0, board.ActivePieceSpacing, false);
                yield return new WaitForSecondsRealtime(.04f);
                BoardPlayModeTests.CaptureGameplayState("triple-impact-reduced.png");
            }
            finally { AccessibilitySettings.ReducedMotion = motion; }
        }
        [UnityTest]
        public IEnumerator FinalBonus_OnlyQueuesWinningBoardSpecialsAndResetsPoolEligibility()
        {
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
            var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            var ui = bootstrap.uiController;
            ui.SetMainMenuVisible(false);
            yield return new WaitForSecondsRealtime(4.5f);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var queue = typeof(GameBootstrap).GetMethod("QueueNextFinalSpecial", flags);
            var pending = typeof(GameBootstrap).GetField("victoryPending", flags);
            var cohortField = typeof(GameBootstrap).GetField("finalBonusSpecials", flags);
            var capturedField = typeof(GameBootstrap).GetField("finalBonusCaptured", flags);
            var waveField = typeof(GameBootstrap).GetField("finalBonusWave", flags);
            try
            {
                // Do not record a synthetic campaign result or spend energy.
                bootstrap.uiController = null;
                bootstrap.gameTimer.StopTimer();
                foreach (var piece in bootstrap.boardController.GetSpecialPieces())
                    piece.SetSpecial(DogCrush.Board.PieceSpecialType.None);
                var original = bootstrap.boardController.GetPieceAt(0, 0);
                original.SetSpecial(DogCrush.Board.PieceSpecialType.RowBlast);
                pending.SetValue(bootstrap, true);
                queue.Invoke(bootstrap, null);
                var cohort = (System.Collections.Generic.HashSet<DogCrush.Board.PieceView>)cohortField.GetValue(bootstrap);
                Assert.That(cohort.Count, Is.EqualTo(1));
                Assert.That(cohort.Contains(original), Is.True);
                yield return new WaitForSecondsRealtime(.35f);
                BoardPlayModeTests.CaptureGameplayState("final-bonus-winning-special.png");
                float deadline = Time.unscaledTime + 12f;
                while (bootstrap.stateController.CurrentState != GameState.GameOver && Time.unscaledTime < deadline)
                    yield return null;
                Assert.That(bootstrap.stateController.CurrentState, Is.EqualTo(GameState.GameOver));
                Assert.That((int)waveField.GetValue(bootstrap), Is.EqualTo(1), "New cascade specials do not add automatic waves.");
                Assert.That(cohort.Count, Is.Zero, "Pooled views lose bonus eligibility before recycling.");
                Assert.That(bootstrap.scoreController.CurrentScore, Is.GreaterThan(0));
                bootstrap.uiController = ui;
                bootstrap.StartNewMatch();
                Assert.That((bool)capturedField.GetValue(bootstrap), Is.False);
                Assert.That(cohort.Count, Is.Zero);
                yield return new WaitForSecondsRealtime(4.5f);
                bootstrap.uiController = null;
                bootstrap.gameTimer.StopTimer();
                foreach (var piece in bootstrap.boardController.GetSpecialPieces())
                    piece.SetSpecial(DogCrush.Board.PieceSpecialType.None);
                pending.SetValue(bootstrap, true);
                queue.Invoke(bootstrap, null);
                var later = bootstrap.boardController.GetPieceAt(0, 0);
                later.SetSpecial(DogCrush.Board.PieceSpecialType.ColumnBlast);
                yield return new WaitForSecondsRealtime(.4f);
                Assert.That(bootstrap.stateController.CurrentState, Is.EqualTo(GameState.GameOver));
                Assert.That((int)waveField.GetValue(bootstrap), Is.Zero);
                Assert.That(later.SpecialType, Is.EqualTo(DogCrush.Board.PieceSpecialType.ColumnBlast));
            }
            finally { bootstrap.uiController = ui; }
        }
        [UnityTest]
        public IEnumerator ResultPresentation_ResetsStarsCountsWhilePausedAndHonorsReducedMotion()
        {
            bool motion = AccessibilitySettings.ReducedMotion;
            float scale = Time.timeScale;
            try
            {
                SceneManager.LoadScene("Gameplay");
                yield return null;
                yield return null;
                var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
                var ui = bootstrap.uiController;
                ui.SetMainMenuVisible(false);
                bootstrap.gameTimer.StopTimer();
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var tip = typeof(GameBootstrap).GetMethod("BuildStarResultTip", flags);
                bootstrap.currentLevel = 37;
                string advice = (string)tip.Invoke(bootstrap, new object[] { 2 });
                StringAssert.Contains("60 %", advice);
                AccessibilitySettings.ReducedMotion = false;
                Time.timeScale = 0f;
                ui.ShowLevelResult(true, 900, false, 3, 5, 37, 0, false, advice);
                yield return new WaitForSecondsRealtime(1.1f);
                Assert.That(ui.finalScoreText.text, Is.EqualTo($"{900:N0}"), "Counting must finish even when timeScale is zero.");
                ui.ShowLevelResult(true, 120, false, 1, 5, 37, 0, false,
                    (string)tip.Invoke(bootstrap, new object[] { 1 }));
                var last = GameObject.Find("ResultStar_3_RT").GetComponent<UnityEngine.UI.Image>();
                Assert.That(last.color.a, Is.LessThan(.5f), "Reset previous gold stars immediately.");
                Assert.That(ui.playAgainButton.interactable, Is.True, "Never gate return behind animation.");
                yield return new WaitForSecondsRealtime(.75f);
                Assert.That(ui.finalScoreText.text, Is.EqualTo($"{120:N0}"));
                BoardPlayModeTests.CaptureGameplayState("result-one-star.png");
                AccessibilitySettings.ReducedMotion = true;
                ui.ShowLevelResult(true, 12345, true, 3, 5, 100, 80, false,
                    (string)tip.Invoke(bootstrap, new object[] { 3 }));
                Assert.That(ui.finalScoreText.text, Is.EqualTo($"{12345:N0}"));
                yield return null;
                Canvas.ForceUpdateCanvases();
                for (int i = 1; i <= 3; i++)
                    Assert.That(GameObject.Find($"ResultStar_{i}_RT").transform.localScale, Is.EqualTo(Vector3.one));
                foreach (string name in new[] { "GOTitle", "FinalLabel", "ResultTip_RT" })
                {
                    var text = GameObject.Find(name).GetComponent<TextMeshProUGUI>();
                    text.ForceMeshUpdate();
                    Assert.That(text.isTextOverflowing, Is.False, name);
                }
                BoardPlayModeTests.CaptureGameplayState("result-finale-reduced.png");
                AccessibilitySettings.ReducedMotion = false;
                ui.ShowLevelResult(true, 9876, false, 3, 5, 10);
                yield return new WaitForSecondsRealtime(.05f);
                ui.HideGameOver();
                ui.ShowLevelResult(true, 77, false, 2, 5, 10, 0, false, advice);
                yield return new WaitForSecondsRealtime(1.1f);
                Assert.That(ui.finalScoreText.text, Is.EqualTo($"{77:N0}"), "Cancelled count cannot overwrite next result.");
                BoardPlayModeTests.CaptureGameplayState("result-world-finale.png");
                ui.HideGameOver();
            }
            finally
            {
                AccessibilitySettings.ReducedMotion = motion;
                Time.timeScale = scale;
            }
        }
        [UnityTest]
        public IEnumerator WorldDiscovery_IsShortSkippableAndDoesNotConsumeInterruptedOrLockedArrival()
        {
            var catalog = CampaignCatalog.LoadOrCreateRuntime();
            var saved = new System.Collections.Generic.Dictionary<string, int?>();
            bool motion = AccessibilitySettings.ReducedMotion;
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            WorldMapScreenController map = null;
            try
            {
                foreach (var zone in catalog.zones)
                {
                    string key = "JoinDog.ZoneSeen." + zone.id;
                    saved[key] = PlayerPrefs.HasKey(key) ? (int?)PlayerPrefs.GetInt(key) : null;
                    PlayerPrefs.SetInt(key, 1);
                }
                SceneManager.LoadScene("WorldMap");
                yield return null;
                yield return null;
                map = Object.FindAnyObjectByType<WorldMapScreenController>();
                Assert.That(map, Is.Not.Null);
                map.enabled = false;
                yield return new WaitForSecondsRealtime(2.2f);
                var show = typeof(WorldMapScreenController).GetMethod("ShowZoneDiscovery", flags);
                var cancel = typeof(WorldMapScreenController).GetMethod("CancelZoneDiscovery", flags);
                var canvas = GameObject.Find("WorldMapCanvas");
                Assert.That(canvas, Is.Not.Null);
                foreach (var zone in catalog.zones)
                {
                    string key = "JoinDog.ZoneSeen." + zone.id;
                    PlayerPrefs.DeleteKey(key);
                    AccessibilitySettings.ReducedMotion = false;
                    map.StartCoroutine((IEnumerator)show.Invoke(map, new object[] { zone }));
                    yield return new WaitForSecondsRealtime(.3f);
                    var overlay = GameObject.Find("ZoneDiscovery_" + zone.id);
                    Assert.That(overlay, Is.Not.Null);
                    Assert.That(PlayerPrefs.HasKey(key), Is.False, "Do not record before presentation finishes.");
                    var title = GameObject.Find("DiscoveryWorld").GetComponent<TextMeshProUGUI>();
                    Assert.That(title.text, Is.EqualTo(zone.displayName.ToUpperInvariant()));
                    title.ForceMeshUpdate();
                    Assert.That(title.isTextOverflowing, Is.False);
                    Assert.That(GameObject.Find("DiscoveryPanorama").GetComponent<UnityEngine.UI.Image>().sprite,
                        Is.EqualTo(WorldMapArtLibrary.LoadBackground(zone.id)));
                    if (zone.firstLevel == 31 || zone.firstLevel == 91)
                        BoardPlayModeTests.CaptureGameplayState($"world-arrival-{zone.firstLevel:000}.png");
                    GameObject.Find("DiscoveryContinue").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                    yield return null;
                    yield return null;
                    Assert.That(GameObject.Find("ZoneDiscovery_" + zone.id), Is.Null);
                    Assert.That(PlayerPrefs.GetInt(key), Is.EqualTo(1));
                    Assert.That(canvas, Is.Not.Null, "Closing only destroys the overlay, preserving the map.");
                }
                var coast = catalog.GetZoneForLevel(31);
                string coastKey = "JoinDog.ZoneSeen." + coast.id;
                PlayerPrefs.DeleteKey(coastKey);
                AccessibilitySettings.ReducedMotion = true;
                map.StartCoroutine((IEnumerator)show.Invoke(map, new object[] { coast }));
                yield return null;
                var still = GameObject.Find("DiscoveryPanorama").transform;
                Assert.That(still.localScale, Is.EqualTo(Vector3.one));
                Assert.That(GameObject.Find("ZoneDiscovery_" + coast.id).GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
                yield return new WaitForSecondsRealtime(.4f);
                Assert.That(still.localScale, Is.EqualTo(Vector3.one));
                BoardPlayModeTests.CaptureGameplayState("world-arrival-reduced.png");
                cancel.Invoke(map, null);
                yield return null;
                Assert.That(PlayerPrefs.HasKey(coastKey), Is.False, "Interrupted arrival remains available.");
                // Exercise the public lifecycle cleanup rather than just a manual cancel.
                map.enabled = true;
                map.StartCoroutine((IEnumerator)show.Invoke(map, new object[] { coast }));
                yield return null;
                map.enabled = false;
                yield return null;
                Assert.That(GameObject.Find("ZoneDiscovery_" + coast.id), Is.Null);
                Assert.That(PlayerPrefs.HasKey(coastKey), Is.False);
                map.StartCoroutine((IEnumerator)show.Invoke(map, new object[] { coast }));
                yield return new WaitForSecondsRealtime(2.6f);
                Assert.That(GameObject.Find("ZoneDiscovery_" + coast.id), Is.Null);
                Assert.That(PlayerPrefs.GetInt(coastKey), Is.EqualTo(1), "Auto completion is bounded to 2.4 seconds.");
                var locked = catalog.GetZoneForLevel(91);
                if (AppServices.Instance.Progress.EarnedUnlockedLevel < locked.firstLevel)
                {
                    PlayerPrefs.DeleteKey("JoinDog.ZoneSeen." + locked.id);
                    typeof(WorldMapScreenController).GetField("visibleZoneId", flags).SetValue(map, string.Empty);
                    typeof(WorldMapScreenController).GetMethod("ApplyVisibleZoneTitle", flags)
                        .Invoke(map, new object[] { locked });
                    yield return null;
                    Assert.That(GameObject.Find("ZoneDiscovery_" + locked.id), Is.Null);
                    Assert.That(PlayerPrefs.HasKey("JoinDog.ZoneSeen." + locked.id), Is.False);
                }
            }
            finally
            {
                if (map != null) typeof(WorldMapScreenController).GetMethod("CancelZoneDiscovery", flags).Invoke(map, null);
                AccessibilitySettings.ReducedMotion = motion;
                foreach (var entry in saved)
                {
                    if (entry.Value.HasValue) PlayerPrefs.SetInt(entry.Key, entry.Value.Value);
                    else PlayerPrefs.DeleteKey(entry.Key);
                }
                PlayerPrefs.Save();
            }
        }
        [UnityTest]
        public IEnumerator ChapterCollections_UseUnlockedToysAndKeepMapAdviceConsistent()
        {
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
            var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            var catalog = CampaignCatalog.LoadOrCreateRuntime();
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            for (int number = 1; number <= 100; number++)
            {
                var definition = (LevelDefinition)typeof(GameBootstrap).GetMethod("GetLevelDefinition", flags)
                    .Invoke(bootstrap, new object[] { number });
                if (definition.objectiveType != LevelObjectiveType.CollectPieces &&
                    definition.objectiveType != LevelObjectiveType.CollectTwoTypes) continue;
                var pool = new System.Collections.Generic.List<DogCrush.Board.PieceType>(definition.activePieceTypes);
                Assert.That(pool.IndexOf(definition.targetPieceType), Is.InRange(0, definition.typeCount - 1), $"Primary {number}");
                if (definition.objectiveType == LevelObjectiveType.CollectTwoTypes)
                {
                    Assert.That(pool.IndexOf(definition.secondaryTargetPieceType), Is.InRange(0, definition.typeCount - 1), $"Secondary {number}");
                    Assert.That(definition.targetPieceType, Is.Not.EqualTo(definition.secondaryTargetPieceType));
                }
                int unlock = definition.targetPieceType == DogCrush.Board.PieceType.Duck ? 11 :
                    definition.targetPieceType == DogCrush.Board.PieceType.Rope ? 21 :
                    definition.targetPieceType == DogCrush.Board.PieceType.Frisbee ? 31 :
                    definition.targetPieceType == DogCrush.Board.PieceType.Penguin ? 41 : 1;
                Assert.That(number, Is.GreaterThanOrEqualTo(unlock));
                if (number >= 31)
                {
                    Assert.That((int)catalog.GetLevel(number).targetPiece, Is.EqualTo((int)definition.targetPieceType));
                    StringAssert.Contains(CampaignCatalog.PieceLabel(catalog.GetLevel(number).targetPiece),
                        GameBootstrap.BuildObjectiveIntroText(definition));
                }
            }
            int[] levels = { 12, 21, 37, 47 };
            DogCrush.Board.PieceType[] toys = { DogCrush.Board.PieceType.Duck, DogCrush.Board.PieceType.Rope,
                DogCrush.Board.PieceType.Frisbee, DogCrush.Board.PieceType.Penguin };
            bootstrap.uiController.SetMainMenuVisible(false);
            for (int i = 0; i < levels.Length; i++)
            {
                bootstrap.currentLevel = levels[i];
                bootstrap.StartNewMatch();
                yield return new WaitForSecondsRealtime(4.5f);
                var definition = (LevelDefinition)typeof(GameBootstrap).GetMethod("GetLevelDefinition", flags)
                    .Invoke(bootstrap, new object[] { levels[i] });
                Assert.That(definition.targetPieceType, Is.EqualTo(toys[i]));
                var authored = Resources.Load<LevelDesignAsset>($"Campaign/Levels/level_{levels[i]:000}");
                if (authored != null)
                    Assert.That(authored.BuildObjectivePreview(catalog.GetLevel(levels[i])),
                        Is.EqualTo(GameBootstrap.BuildObjectiveIntroText(definition)));
                var icon = GameObject.Find("AdventureGoalIcon").GetComponent<UnityEngine.UI.Image>();
                Assert.That(icon.sprite, Is.Not.Null);
                BoardPlayModeTests.CaptureGameplayState($"chapter-toy-{levels[i]:000}.png");
            }
        }

        [UnityTest]
        public IEnumerator HazardLesson_FollowsNewToyAndOnlyRecordsPresentedLesson()
        {
            const string key = "JoinDog_HazardHint_Vine";
            bool hadKey = PlayerPrefs.HasKey(key);
            int previous = PlayerPrefs.GetInt(key);
            bool previousMotion = AccessibilitySettings.ReducedMotion;
            try
            {
                PlayerPrefs.SetInt(key, 0);
                AccessibilitySettings.ReducedMotion = true;
                SceneManager.LoadScene("Gameplay");
                yield return null;
                yield return null;
                var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
                bootstrap.currentLevel = 11;
                bootstrap.StartNewMatch();
                bootstrap.uiController.SetMainMenuVisible(false);
                yield return new WaitForSecondsRealtime(.4f);
                Assert.That(PlayerPrefs.GetInt(key), Is.Zero, "A toy announcement must not consume the lesson.");
                Assert.That(GameObject.Find("ObjectiveBriefPlay_RT"), Is.Null);
                yield return new WaitForSecondsRealtime(1.4f);
                Assert.That(bootstrap.gameTimer.IsPaused, Is.True);
                StringAssert.Contains("ENREDADERAS", GameObject.Find("BriefCoaching").GetComponent<TextMeshProUGUI>().text);
                Assert.That(PlayerPrefs.GetInt(key), Is.Zero);
                foreach (var label in GameObject.Find("ObjectiveBrief_RT").GetComponentsInChildren<TextMeshProUGUI>())
                {
                    label.ForceMeshUpdate();
                    Assert.That(label.isTextOverflowing, Is.False, label.name);
                }
                BoardPlayModeTests.CaptureGameplayState("hazard-lesson-vine.png");
                GameObject.Find("ObjectiveBriefPlay_RT").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                yield return null;
                yield return null;
                Assert.That(GameObject.Find("ObjectiveBrief_RT"), Is.Null);
                Assert.That(PlayerPrefs.GetInt(key), Is.EqualTo(1));
                Assert.That(bootstrap.gameTimer.IsPaused, Is.False);
                bootstrap.StartNewMatch();
                yield return new WaitForSecondsRealtime(1.8f);
                Assert.That(GameObject.Find("ObjectiveBriefPlay_RT"), Is.Null, "An acknowledged lesson must not repeat.");
                PlayerPrefs.SetInt(key, 0);
                bootstrap.StartNewMatch();
                yield return new WaitForSecondsRealtime(1.8f);
                Assert.That(GameObject.Find("ObjectiveBriefPlay_RT"), Is.Not.Null);
                bootstrap.currentLevel = 1;
                bootstrap.StartNewMatch();
                yield return new WaitForSecondsRealtime(1.5f);
                Assert.That(PlayerPrefs.GetInt(key), Is.Zero, "An interrupted lesson must remain available.");
                bootstrap.currentLevel = 11;
                bootstrap.StartNewMatch();
                yield return new WaitForSecondsRealtime(4.5f);
                Assert.That(GameObject.Find("ObjectiveBrief_RT"), Is.Null);
                Assert.That(PlayerPrefs.GetInt(key), Is.EqualTo(1), "The lesson also completes automatically.");
                Assert.That(bootstrap.gameTimer.IsPaused, Is.False);
            }
            finally
            {
                if (hadKey) PlayerPrefs.SetInt(key, previous); else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
                AccessibilitySettings.ReducedMotion = previousMotion;
            }
        }

        [UnityTest]
        public IEnumerator SharedCollection_ShowsBothTargetsAndRestoresSingleIcon()
        {
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
            var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            bootstrap.currentLevel = 37;
            bootstrap.StartNewMatch();
            bootstrap.uiController.SetMainMenuVisible(false);
            yield return new WaitForSecondsRealtime(1.5f);
            bootstrap.gameTimer.SetPaused(true);
            var goal = GameObject.Find("AdventureGoal_RT").transform;
            var primary = goal.Find("AdventureGoalIcon").GetComponent<UnityEngine.UI.Image>();
            var secondary = goal.Find("AdventureGoalSecondaryIcon").GetComponent<UnityEngine.UI.Image>();
            Assert.That(primary.sprite, Is.EqualTo(Resources.Load<Sprite>("Magic/frisbee-coral-v2")));
            Assert.That(secondary.gameObject.activeSelf, Is.True);
            Assert.That(secondary.sprite, Is.EqualTo(Resources.Load<Sprite>("Pieces/piece-bone-v2")));
            BoardPlayModeTests.CaptureGameplayState("shared-targets-level-037.png");
            var ui = bootstrap.uiController;
            var definition = new LevelDefinition {
                objectiveType = LevelObjectiveType.CollectTwoTypes,
                targetPieceType = DogCrush.Board.PieceType.Ball,
                secondaryTargetPieceType = DogCrush.Board.PieceType.Frisbee,
                targetAmount = 20
            };
            ui.SetCustomObjective(37, "BALL + FRISBEE", 20);
            ui.SetObjectivePieceIcons(definition);
            ui.UpdateObjectiveProgress(4);
            Assert.That(primary.sprite, Is.EqualTo(Resources.Load<Sprite>("Pieces/piece-ball-v2")));
            Assert.That(secondary.sprite, Is.EqualTo(Resources.Load<Sprite>("Magic/frisbee-coral-v2")));
            StringAssert.Contains("4/20", ui.scoreText.text);
            yield return null;
            Assert.That(primary.rectTransform.anchorMax.x, Is.LessThan(secondary.rectTransform.anchorMin.x),
                "The two target icons must not overlap within their shared parent.");
            BoardPlayModeTests.CaptureGameplayState("shared-targets-ball-frisbee.png");
            ui.SetLevelObjective(1, 14400);
            Assert.That(secondary.gameObject.activeSelf, Is.False);
            Assert.That(primary.rectTransform.anchorMin.x, Is.EqualTo(.04f));
            Assert.That(primary.sprite, Is.EqualTo(Resources.Load<Sprite>("UI/icon-score-star")));
        }

        [UnityTest]
        public IEnumerator MissionCard_OpensGuideDirectlyAndKeepsPauseAfterIntroEnds()
        {
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
            var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            bootstrap.currentLevel = 1;
            bootstrap.StartNewMatch();
            bootstrap.uiController.SetMainMenuVisible(false);
            yield return new WaitForSecondsRealtime(.35f);
            var goal = GameObject.Find("AdventureGoal_RT");
            var canvas = goal.GetComponentInParent<Canvas>();
            var goalRect = goal.GetComponent<RectTransform>();
            var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,
                    goalRect.TransformPoint(goalRect.rect.center))
            };
            var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            Assert.That(hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>(),
                Is.EqualTo(goal.GetComponent<UnityEngine.UI.Button>()), "Mission card must receive the actual touch.");
            goal.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            float heldTime = bootstrap.gameTimer.RemainingTime;
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.That(GameObject.Find("ObjectiveBrief_RT"), Is.Null);
            Assert.That(GameObject.Find("MissionReview_RT"), Is.Not.Null);
            Assert.That(bootstrap.gameTimer.IsPaused, Is.True);
            Assert.That(bootstrap.gameTimer.RemainingTime, Is.EqualTo(heldTime));
            Assert.That(bootstrap.selectionController.InteractionBlocked, Is.True);
            var back = GameObject.Find("MissionReviewBack_RT").GetComponent<UnityEngine.UI.Button>();
            StringAssert.Contains("PARTIDA", back.GetComponentInChildren<TextMeshProUGUI>().text);
            BoardPlayModeTests.CaptureGameplayState("mission-direct-guide.png");
            back.onClick.Invoke();
            Assert.That(GameObject.Find("MissionReview_RT"), Is.Null);
            Assert.That(bootstrap.gameTimer.IsPaused, Is.False);
            Assert.That(bootstrap.selectionController.InteractionBlocked, Is.False);
            var caption = GameObject.Find("GoalCaption").GetComponent<TextMeshProUGUI>();
            caption.ForceMeshUpdate();
            Assert.That(caption.isTextOverflowing, Is.False);
            BoardPlayModeTests.CaptureGameplayState("mission-direct-board.png");
        }

        [Test]
        public void BoardCoaching_OnlyTeachesPresentObstaclesAndAccurateReach()
        {
            var definition = new LevelDefinition { obstacleCount = 8 };
            definition.obstacleType = DogCrush.Board.CellObstacleType.Vine;
            StringAssert.Contains("Si un turno no rompe ninguna", GameBootstrap.BuildBoardCoachingText(definition));
            definition.obstacleType = DogCrush.Board.CellObstacleType.Sand;
            StringAssert.Contains("sin diagonales", GameBootstrap.BuildBoardCoachingText(definition));
            foreach (var type in new[] { DogCrush.Board.CellObstacleType.Ice, DogCrush.Board.CellObstacleType.Lantern })
            {
                definition.obstacleType = type;
                StringAssert.Contains("dos capas", GameBootstrap.BuildBoardCoachingText(definition));
                StringAssert.Contains("sin diagonales", GameBootstrap.BuildBoardCoachingText(definition));
            }
            definition.obstacleType = DogCrush.Board.CellObstacleType.PuppyCage;
            StringAssert.Contains("liberar al cachorro", GameBootstrap.BuildBoardCoachingText(definition));
            definition.obstacleCount = 0;
            StringAssert.StartsWith("ESPECIALES", GameBootstrap.BuildBoardCoachingText(definition));
        }

        [UnityTest]
        public IEnumerator BoardCoaching_RendersActualChapterStrategiesWithoutOverflow()
        {
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
            var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            bootstrap.uiController.SetMainMenuVisible(false);
            for (int chapter = 0; chapter < 10; chapter++)
            {
                bootstrap.currentLevel = chapter * 10 + 1;
                bootstrap.StartNewMatch();
                yield return new WaitForSecondsRealtime(3f);
                bootstrap.uiController.SetSettingsVisible(true);
                GameObject.Find("MissionReviewOpen_RT").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                yield return null;
                var card = GameObject.Find("MissionReview_RT");
                foreach (var label in card.GetComponentsInChildren<TextMeshProUGUI>())
                {
                    label.ForceMeshUpdate();
                    Assert.That(label.isTextOverflowing, Is.False, label.name);
                }
                Assert.That(bootstrap.gameTimer.IsPaused, Is.True);
                BoardPlayModeTests.CaptureGameplayState($"board-coaching-{bootstrap.currentLevel:000}.png");
                bootstrap.uiController.SetSettingsVisible(false);
            }
        }

        [UnityTest]
        public IEnumerator MissionReview_StaysPausedAndUsesCurrentLevelAdvice()
        {
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
            var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            bootstrap.currentLevel = 41;
            bootstrap.StartNewMatch();
            var ui = bootstrap.uiController;
            ui.SetMainMenuVisible(false);
            yield return new WaitForSecondsRealtime(3f);
            ui.SetSettingsVisible(true);
            GameObject.Find("MissionReviewOpen_RT").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;
            var review = GameObject.Find("MissionReview_RT");
            Assert.That(review, Is.Not.Null);
            Assert.That(review.GetComponent<Canvas>().sortingOrder, Is.EqualTo(200));
            Assert.That(review.GetComponent<UnityEngine.UI.GraphicRaycaster>(), Is.Not.Null);
            Assert.That(review.GetComponent<UnityEngine.UI.Image>().raycastTarget, Is.True,
                "Advice must block settings controls behind it.");
            Assert.That(bootstrap.gameTimer.IsPaused, Is.True);
            Assert.That(bootstrap.selectionController.InteractionBlocked, Is.True);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var definition = (LevelDefinition)typeof(GameBootstrap).GetMethod("GetLevelDefinition", flags)
                .Invoke(bootstrap, new object[] { 41 });
            Assert.That(review.transform.Find("MissionAdvice").GetComponent<TextMeshProUGUI>().text,
                Is.EqualTo(GameBootstrap.BuildObjectiveCoachingText(definition)));
            foreach (var label in review.GetComponentsInChildren<TextMeshProUGUI>())
            {
                label.ForceMeshUpdate();
                Assert.That(label.isTextOverflowing, Is.False, label.name);
            }
            BoardPlayModeTests.CaptureGameplayState("mission-review-gameplay.png");
            GameObject.Find("MissionReviewBack_RT").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(GameObject.Find("MissionReview_RT"), Is.Null);
            Assert.That(bootstrap.gameTimer.IsPaused, Is.True);
            ui.SetSettingsVisible(false);
            Assert.That(bootstrap.gameTimer.IsPaused, Is.False);
            Assert.That(bootstrap.selectionController.InteractionBlocked, Is.False);
            ui.SetSettingsVisible(true);
            GameObject.Find("MissionReviewOpen_RT").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            ui.SetSettingsVisible(false);
            Assert.That(GameObject.Find("MissionReview_RT"), Is.Null);
        }

        [UnityTest]
        public IEnumerator WorldBoards_RenderActualChapterEntriesWithTheirObstacles()
        {
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
            var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            bootstrap.uiController.SetMainMenuVisible(false);
            for (int chapter = 0; chapter < 10; chapter++)
            {
                bootstrap.currentLevel = chapter * 10 + 1;
                bootstrap.StartNewMatch();
                yield return new WaitForSecondsRealtime(3f);
                var board = bootstrap.boardController;
                var root = board.transform.Find("[AdaptiveBoardVisual]");
                Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Length, Is.LessThan(150));
                Assert.That(root.GetComponentsInChildren<Collider2D>().Length, Is.Zero);
                var piece = board.GetPieceAt(board.Columns / 2, board.Rows / 2);
                Assert.That(piece, Is.Not.Null);
                Assert.That(piece.GetComponent<Collider2D>().OverlapPoint(piece.transform.position), Is.True);
                bool hasHole = false;
                for (int x = 0; x < board.Columns; x++)
                    for (int y = 0; y < board.Rows; y++)
                        if (!board.IsPlayableCell(x, y))
                        {
                            hasHole = true;
                            Assert.That(root.Find($"Cell_{x}_{y}"), Is.Null, "Empty space must not look playable.");
                        }
                if (hasHole)
                {
                    var contour = root.Find("ContourFrame").GetComponent<SpriteRenderer>();
                    Assert.That(root.Find("OuterFrame"), Is.Null);
                    var oldSprite = contour.sprite;
                    var oldTexture = contour.sprite.texture;
                    var previousRandom = Random.state;
                    board.GetComponent<DogCrush.Board.AdaptiveBoardView>().Rebuild(board);
                    Assert.That(Random.state, Is.EqualTo(previousRandom));
                    yield return null;
                    Assert.That(oldSprite == null, Is.True, "Previous contour sprite must be released.");
                    Assert.That(oldTexture == null, Is.True, "Previous contour texture must be released.");
                    Assert.That(board.GetPieceAt(board.Columns / 2, board.Rows / 2), Is.SameAs(piece));
                    if (bootstrap.currentLevel == 41)
                    {
                        var texture = root.Find("ContourFrame").GetComponent<SpriteRenderer>().sprite.texture;
                        var target = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
                        var pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                        var previous = RenderTexture.active;
                        try
                        {
                            Graphics.Blit(texture, target);
                            RenderTexture.active = target;
                            pixels.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);
                            pixels.Apply();
                            Assert.That(pixels.GetPixel(4,4).a, Is.LessThan(.01f));
                            Assert.That(pixels.GetPixel(texture.width / 2, texture.height / 2).a, Is.GreaterThan(.99f));
                        }
                        finally
                        {
                            RenderTexture.active = previous;
                            RenderTexture.ReleaseTemporary(target);
                            Object.Destroy(pixels);
                        }
                    }
                }
                BoardPlayModeTests.CaptureGameplayState($"world-entry-{bootstrap.currentLevel:000}.png");
            }
        }

        [UnityTest]
        public IEnumerator ObjectiveBrief_ExplainsActualCountersAndResetsBetweenMatches()
        {
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
            var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            bootstrap.currentLevel = 1;
            bootstrap.StartNewMatch();
            bootstrap.uiController.SetMainMenuVisible(false);
            yield return new WaitForSecondsRealtime(.35f);
            Assert.That(GameObject.Find("ObjectiveBrief_RT"), Is.Not.Null);
            Assert.That(bootstrap.gameTimer.IsPaused, Is.True);
            bootstrap.StartNewMatch();
            yield return new WaitForSecondsRealtime(.95f);
            Assert.That(GameObject.Find("ObjectiveBrief_RT"), Is.Not.Null,
                "An old intro must not remove the new mission card.");
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(GameObject.Find("ObjectiveBrief_RT"), Is.Null);
            var ui = bootstrap.uiController;
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var definition = (LevelDefinition)typeof(GameBootstrap).GetMethod("GetLevelDefinition", flags)
                .Invoke(bootstrap, new object[] { 1 });
            var originalType = definition.objectiveType;
            var originalTarget = definition.targetPieceType;
            var originalSecondary = definition.secondaryTargetPieceType;
            var originalAmount = definition.targetAmount;
            try
            {
                definition.objectiveType = LevelObjectiveType.CollectTwoTypes;
                definition.targetPieceType = DogCrush.Board.PieceType.Dog;
                definition.secondaryTargetPieceType = DogCrush.Board.PieceType.Ball;
                definition.targetAmount = 19;
                typeof(GameBootstrap).GetField("objectiveProgress", flags).SetValue(bootstrap, 0);
                var removed = new System.Collections.Generic.List<DogCrush.Board.PieceView>();
                foreach (var piece in bootstrap.boardController.Grid)
                    if (piece != null && piece.type == DogCrush.Board.PieceType.Dog && removed.Count < 4) removed.Add(piece);
                typeof(GameBootstrap).GetMethod("UpdateObjectiveProgress", flags)
                    .Invoke(bootstrap, new object[] { removed, 0, 0 });
                Assert.That((int)typeof(GameBootstrap).GetField("objectiveProgress", flags).GetValue(bootstrap),
                    Is.EqualTo(removed.Count), "One type contributes freely to the shared total.");
                foreach (var kind in new[] { LevelObjectiveType.CollectTwoTypes, LevelObjectiveType.LongChain })
                {
                    definition.objectiveType = kind;
                    definition.targetAmount = kind == LevelObjectiveType.LongChain ? 6 : 19;
                    ui.ShowObjectiveBrief(kind == LevelObjectiveType.LongChain ? "CUMBRES NEVADAS" : "COSTA DORADA",
                        GameBootstrap.BuildObjectiveIntroText(definition), GameBootstrap.BuildObjectiveCoachingText(definition));
                    yield return null;
                    BoardPlayModeTests.CaptureGameplayState($"objective-brief-{kind}.png");
                    foreach (var label in GameObject.Find("ObjectiveBrief_RT").GetComponentsInChildren<TextMeshProUGUI>())
                    {
                        label.ForceMeshUpdate();
                        Assert.That(label.isTextOverflowing, Is.False, label.name);
                        Assert.That(label.raycastTarget, Is.False);
                    }
                }
                definition.objectiveType = LevelObjectiveType.LongChain;
                typeof(GameBootstrap).GetField("objectiveProgress", flags).SetValue(bootstrap, 0);
                typeof(GameBootstrap).GetMethod("UpdateObjectiveProgress", flags)
                    .Invoke(bootstrap, new object[] { removed, 2, 0 });
                Assert.That((int)typeof(GameBootstrap).GetField("objectiveProgress", flags).GetValue(bootstrap), Is.EqualTo(2));
                ui.SetSettingsVisible(true);
                Assert.That(GameObject.Find("ObjectiveBrief_RT"), Is.Null);
                ui.ShowObjectiveBrief("ZONA", "MISIÓN", "CONSEJO");
                Assert.That(GameObject.Find("ObjectiveBrief_RT"), Is.Null);
                ui.SetSettingsVisible(false);
                bootstrap.StartNewMatch();
                Assert.That(GameObject.Find("ObjectiveBrief_RT"), Is.Null);
            }
            finally
            {
                definition.objectiveType = originalType;
                definition.targetPieceType = originalTarget;
                definition.secondaryTargetPieceType = originalSecondary;
                definition.targetAmount = originalAmount;
                ui.HideObjectiveBrief();
            }
        }

        [UnityTest]
        public IEnumerator Companion_FollowsSelectionAndProtectsAutomaticHelpAnnouncement()
        {
            bool previousMotion = AccessibilitySettings.ReducedMotion;
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
            var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            bootstrap.StartNewMatch();
            bootstrap.uiController.SetMainMenuVisible(false);
            yield return new WaitForSecondsRealtime(1.4f);
            var ui = bootstrap.uiController;
            var portrait = GameObject.Find("CompanionPortrait").GetComponent<UnityEngine.UI.Image>();
            var hint = GameObject.Find("CompanionHelp").GetComponent<TextMeshProUGUI>();
            var selection = bootstrap.selectionController;
            var input = selection.inputHandler;
            bool previousInput = input.enabled;
            input.enabled = false;
            try
            {
                AccessibilitySettings.ReducedMotion = false;
                var board = bootstrap.boardController;
                var target = board.GetPieceAt(board.Columns - 1, 2);
                input.OnPointerDownEvent?.Invoke(target.transform.position);
                input.OnPointerUpEvent?.Invoke(target.transform.position);
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(selection.AttentionTarget, Is.SameAs(target));
                float lean = Mathf.DeltaAngle(0f, portrait.transform.localEulerAngles.z);
                Assert.That(lean, Is.InRange(-6.01f, -.2f));
                Assert.That(portrait.raycastTarget, Is.False);
                BoardPlayModeTests.CaptureGameplayState("companion-selection-gameplay.png");
                selection.CancelInteraction();
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(0f, portrait.transform.localEulerAngles.z)), Is.LessThan(.01f));

                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var charge = typeof(GameBootstrap).GetField("companionCharge", flags);
                var assist = typeof(GameBootstrap).GetMethod("TryActivateCompanionAssist", flags);
                charge.SetValue(bootstrap, 3);
                var removed = new System.Collections.Generic.List<DogCrush.Board.PieceView>();
                assist.Invoke(bootstrap, new object[] { removed, true });
                Assert.That((int)charge.GetValue(bootstrap), Is.EqualTo(0));
                Assert.That(removed.Count, Is.GreaterThan(0));
                int helpedRow = removed[0].gridY;
                foreach (var piece in removed) Assert.That(piece.gridY, Is.EqualTo(helpedRow));
                Assert.That(removed.Count, Is.EqualTo(board.GetRowPieces(helpedRow).Count));
                string helpAnnouncement = hint.text;
                Assert.That(helpAnnouncement, Does.Contain("FILA"));
                var chargeHeading = (TextMeshProUGUI)typeof(GameplayUIController)
                    .GetField("companionChargeText", flags).GetValue(ui);
                Assert.That(chargeHeading.text, Is.EqualTo("¡AYUDA! LIMPIO UNA FILA"));
                foreach (var line in bootstrap.particleController.GetComponentsInChildren<LineRenderer>())
                {
                    if (line.name != "Core") continue;
                    Assert.That(line.GetPosition(0).y, Is.EqualTo(board.GetPieceAt(removed[0].gridX, helpedRow).transform.position.y).Within(.001f));
                    Assert.That(line.GetPosition(1).y, Is.EqualTo(line.GetPosition(0).y).Within(.001f));
                }
                ui.ShowCompanionReaction("¡OTRA CASCADA!");
                Assert.That(hint.text, Is.EqualTo(helpAnnouncement), "Help must survive competing cascade reactions.");
                yield return new WaitForSecondsRealtime(.15f);
                Assert.That(portrait.transform.localScale.x, Is.InRange(1.01f, 1.141f));
                BoardPlayModeTests.CaptureGameplayState("companion-help-gameplay.png");
                AccessibilitySettings.ReducedMotion = true;
                yield return null;
                Assert.That(portrait.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(portrait.transform.localRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(hint.text, Is.EqualTo(helpAnnouncement));
                BoardPlayModeTests.CaptureGameplayState("companion-reduced-gameplay.png");
                bootstrap.StartNewMatch();
                Assert.That(hint.text, Is.EqualTo("CASCADAS + ESPECIALES = AYUDA"));
                Assert.That(chargeHeading.text, Is.EqualTo("COMPAÑERO  0/4"));
                ui.ShowCompanionReaction("NUEVA PARTIDA");
                Assert.That(hint.text, Is.EqualTo("NUEVA PARTIDA"));
                ui.enabled = false;
                Assert.That(hint.text, Is.EqualTo("CASCADAS + ESPECIALES = AYUDA"));
                ui.enabled = true;
            }
            finally
            {
                input.enabled = previousInput;
                AccessibilitySettings.ReducedMotion = previousMotion;
            }
        }

        [UnityTest]
        public IEnumerator PremiumBoard_RenderThemeVariantsWithSpecials()
        {
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
            var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            bootstrap.currentLevel = 1;
            bootstrap.StartNewMatch();
            bootstrap.uiController.SetMainMenuVisible(false);
            yield return new WaitForSecondsRealtime(1.4f);
            var board = bootstrap.boardController;
            var original = board.config.boardTheme;
            var view = board.GetComponent<DogCrush.Board.AdaptiveBoardView>();
            var piece = board.GetPieceAt(2, 3);
            piece.SetSpecial(DogCrush.Board.PieceSpecialType.RowBlast);
            board.GetPieceAt(4, 3).SetSpecial(DogCrush.Board.PieceSpecialType.ColorBurst);
            board.GetPieceAt(3, 4).SetSelected(true);
            try
            {
                var materialInstances = new System.Collections.Generic.HashSet<Sprite>();
                var frameInstances = new System.Collections.Generic.HashSet<Sprite>();
                foreach (BoardTheme theme in System.Enum.GetValues(typeof(BoardTheme)))
                {
                    board.config.boardTheme = theme;
                    bootstrap.uiController.ApplyWorldTheme(theme);
                    var randomBefore = Random.state;
                    view.Rebuild(board);
                    Assert.That(Random.state, Is.EqualTo(randomBefore));
                    yield return null;
                    Assert.That(board.GetPieceAt(2, 3), Is.SameAs(piece));
                    Assert.That(piece.GetComponent<Collider2D>().OverlapPoint(piece.transform.position), Is.True);
                    var root = board.transform.Find("[AdaptiveBoardVisual]");
                    var cell = root.Find("Cell_2_3").GetComponent<SpriteRenderer>();
                    var frame = root.Find("WoodBase").GetComponent<SpriteRenderer>();
                    Assert.That(materialInstances.Add(cell.sprite), Is.True, "Each world needs its own cell material.");
                    Assert.That(frameInstances.Add(frame.sprite), Is.True, "Each world needs its own frame material.");
                    Assert.That(root.GetComponentsInChildren<Collider2D>().Length, Is.Zero);
                    Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Length, Is.LessThan(150));
                    var cached = cell.sprite;
                    view.Rebuild(board);
                    yield return null;
                    Assert.That(root.Find("Cell_2_3").GetComponent<SpriteRenderer>().sprite, Is.SameAs(cached));
                    BoardPlayModeTests.CaptureGameplayState($"premium-board-{theme}.png");
                }
            }
            finally { board.config.boardTheme = original; }
        }

        [UnityTest]
        public IEnumerator Comet_IsCreatedFromFourFrisbeesAndKeepsItsOrigin()
        {
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
            var board = Object.FindAnyObjectByType<DogCrush.Board.BoardController>();
            foreach (var piece in board.Grid)
            {
                if (piece == null) continue;
                piece.SetSpecial(DogCrush.Board.PieceSpecialType.None);
                piece.type = (DogCrush.Board.PieceType)((piece.gridX+piece.gridY)%5);
            }
            for (int x=1;x<=4;x++) board.GetPieceAt(x,2).type = DogCrush.Board.PieceType.Frisbee;
            var result = board.BuildMatchResolution(board.FindMatches());
            Assert.That(result.CreatedSpecialType,Is.EqualTo(DogCrush.Board.PieceSpecialType.Comet));
            Assert.That(result.CreatedSpecial.type,Is.EqualTo(DogCrush.Board.PieceType.Frisbee));
            Assert.That(result.PiecesToRemove.Count,Is.EqualTo(3));
            Assert.That(result.PiecesToRemove.Contains(result.CreatedSpecial),Is.False);
        }

        [UnityTest]
        public IEnumerator Comet_RenderActualMarkerForVisualReview()
        {
            var piece = new GameObject("CometVisualReview").AddComponent<DogCrush.Board.PieceView>();
            var camera = new GameObject("CometReviewCamera").AddComponent<Camera>();
            var frame = new RenderTexture(384,384,24);
            var pixels = new Texture2D(384,384,TextureFormat.RGB24,false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                piece.Initialize(DogCrush.Board.PieceType.Frisbee,0,0,Resources.Load<Sprite>("Magic/frisbee-coral-v2"),Color.white);
                piece.SetSpecial(DogCrush.Board.PieceSpecialType.Comet);
                foreach (var child in piece.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=30;
                camera.cullingMask=1<<30;
                camera.orthographic=true;
                camera.orthographicSize=.5f;
                camera.transform.position=new Vector3(0,0,-10);
                camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=new Color(.035f,.07f,.14f);
                camera.targetTexture=frame;
                yield return new WaitForSecondsRealtime(.1f);
                RenderTexture.active=frame;
                pixels.ReadPixels(new Rect(0,0,384,384),0,0);
                pixels.Apply();
                string directory=System.IO.Path.Combine(Application.dataPath,"../Builds/visual-qa");
                System.IO.Directory.CreateDirectory(directory);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory,"comet-piece.png"),pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active=previous;
                camera.targetTexture=null;
                frame.Release();
                Object.Destroy(frame);
                Object.Destroy(pixels);
                Object.Destroy(camera.gameObject);
                Object.Destroy(piece.gameObject);
            }
        }
        [UnityTest]
        public IEnumerator Companion_ReducedMotionStopsAndPreventsHops()
        {
            bool previous = AccessibilitySettings.ReducedMotion;
            var pet = new GameObject("MotionTestPet").AddComponent<CompanionOnBoardController>();
            var target = new GameObject("MotionTestTarget").AddComponent<DogCrush.Board.PieceView>();
            try
            {
                pet.Setup(null,null);
                Vector3 home = pet.transform.position;
                target.transform.position = new Vector3(3,3,0);
                AccessibilitySettings.ReducedMotion = false;
                pet.Celebrate(target);
                yield return null;
                AccessibilitySettings.ReducedMotion = true;
                yield return null;
                Assert.That(pet.transform.position,Is.EqualTo(home));
                pet.Celebrate(target);
                yield return null;
                Assert.That(pet.transform.position,Is.EqualTo(home));
            }
            finally
            {
                AccessibilitySettings.ReducedMotion = previous;
                Object.Destroy(pet.gameObject);
                Object.Destroy(target.gameObject);
            }
        }
        [UnityTest]
        public IEnumerator Guide_AllPagesNavigateAndCloseWithoutStartingGame()
        {
            SceneManager.LoadScene("MainMenu");
            yield return null;
            yield return null;
            GameObject.Find("Help").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;
            for (int page=1;page<=6;page++)
            {
                Assert.That(GameObject.Find("GuidePage").GetComponent<TextMeshProUGUI>().text,Is.EqualTo($"{page} / 6"));
                Assert.That(GameObject.Find("GuidePrevious").GetComponent<UnityEngine.UI.Button>().interactable,Is.EqualTo(page>1));
                GameObject.Find("GuideNext").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                yield return null;
            }
            Assert.That(GameObject.Find("GuidePage"),Is.Null);
            Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("MainMenu"));
        }

        [UnityTest]
        public IEnumerator Comet_ClearsBothDiagonalsAndChainsSpecialsOnce()
        {
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
            var board = Object.FindAnyObjectByType<DogCrush.Board.BoardController>();
            var comet = board.GetPieceAt(3,3);
            comet.SetSpecial(DogCrush.Board.PieceSpecialType.Comet);
            var chained = board.GetPieceAt(5,5);
            chained.SetSpecial(DogCrush.Board.PieceSpecialType.RowBlast);
            var result = board.BuildMatchResolution(new System.Collections.Generic.List<DogCrush.Board.PieceView>{comet});
            Assert.That(result.SpecialsActivated,Is.EqualTo(2));
            Assert.That(result.PiecesToRemove,Does.Contain(board.GetPieceAt(0,0)));
            Assert.That(result.PiecesToRemove,Does.Contain(board.GetPieceAt(0,6)));
            Assert.That(result.PiecesToRemove,Does.Contain(board.GetPieceAt(7,7)));
            Assert.That(result.PiecesToRemove,Does.Contain(board.GetPieceAt(0,5)));
            Assert.That(result.PiecesToRemove.Contains(board.GetPieceAt(1,0)),Is.False);
            Assert.That(new System.Collections.Generic.HashSet<DogCrush.Board.PieceView>(result.PiecesToRemove).Count,
                Is.EqualTo(result.PiecesToRemove.Count));
            chained.SetSpecial(DogCrush.Board.PieceSpecialType.None);
            comet.SetSpecial(DogCrush.Board.PieceSpecialType.None);
            var corner = board.GetPieceAt(0,0);
            corner.SetSpecial(DogCrush.Board.PieceSpecialType.Comet);
            result = board.BuildMatchResolution(new System.Collections.Generic.List<DogCrush.Board.PieceView>{corner});
            Assert.That(result.PiecesToRemove.Count,Is.EqualTo(8));
            Assert.That(result.SpecialsActivated,Is.EqualTo(1));
            Assert.That(result.PiecesToRemove.Contains(board.GetPieceAt(1,0)),Is.False);
        }
        [UnityTest]
        public IEnumerator Collection_CanReturnToMenuWithoutClaimingOrStartingALevel()
        {
            bool previous = AccessibilitySettings.ReducedMotion;
            try
            {
                AccessibilitySettings.ReducedMotion = true;
                SceneManager.LoadScene("MainMenu");
                yield return null;
                yield return null;
                int treats = AppServices.Instance.Progress.Treats;
                GameObject.Find("FigureAlbum").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                yield return null;
                var close = GameObject.Find("DismissAlbum");
                Assert.That(close, Is.Not.Null, "Collection needs a separate, visible exit.");
                close.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                yield return null;
                Assert.That(GameObject.Find("AlbumCard"), Is.Null);
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
                Assert.That(AppServices.Instance.Progress.Treats, Is.EqualTo(treats));
                var pet = GameObject.Find("MenuDog").transform;
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(pet.localScale, Is.EqualTo(Vector3.one), "Reduced motion also applies to the menu pet.");
                Assert.That(pet.localRotation, Is.EqualTo(Quaternion.identity));
            }
            finally { AccessibilitySettings.ReducedMotion = previous; }
        }

        [UnityTest]
        public IEnumerator AllWorlds_KeepTheirArtworkAndBoundAmbientEffectsOnRestart()
        {
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
            var atmosphere = Object.FindAnyObjectByType<GameplayWorldAtmosphere>();
            Assert.That(atmosphere, Is.Not.Null);
            for (int level = 1; level <= 100; level += 10)
            {
                atmosphere.ApplyLevel(level);
                yield return null;
                var backdrop = GameObject.Find("GameplayWorldBackdrop").GetComponent<SpriteRenderer>();
                Assert.That(backdrop.sprite, Is.Not.Null, "Missing world art at level " + level);
                Assert.That(atmosphere.ZoneId, Is.EqualTo(CampaignCatalog.LoadOrCreateRuntime().GetZoneForLevel(level).id));
                if (level > 1) Assert.That(backdrop.sprite, Is.EqualTo(WorldMapArtLibrary.LoadBackground(atmosphere.ZoneId)));
                Assert.That(atmosphere.AmbientCount, Is.EqualTo(12));
                Assert.That(atmosphere.GetComponentsInChildren<Collider2D>().Length, Is.EqualTo(0),
                    "Atmosphere must never block touch input.");
            }
            bool previous = AccessibilitySettings.ReducedMotion;
            try
            {
                AccessibilitySettings.ReducedMotion = true;
                yield return null;
                for (int i = 0; i < 12; i++)
                    Assert.That(GameObject.Find("WorldMote_" + i).GetComponent<SpriteRenderer>().enabled, Is.False);
                var ui = Object.FindAnyObjectByType<GameplayUIController>();
                ui.UpdateTimer(9f, .1f);
                Color urgent = ui.timerText.color;
                ui.ShowMatchReward(Vector3.zero);
                yield return new WaitForSecondsRealtime(.3f);
                ui.UpdateTimer(9f, .1f);
                Assert.That(ui.timerText.color, Is.EqualTo(urgent), "Urgent timer must not flash.");
                Assert.That(ui.timerText.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(GameObject.Find("MatchReward"), Is.Null);
                ui.SetMoveMode(12, 20);
                Assert.That(GameObject.Find("TimerCaption").GetComponent<TextMeshProUGUI>().text, Is.EqualTo("MOVIMIENTOS"));
                ui.SetCustomObjective(1, "PUNTOS", 1000);
                ui.UpdateTimer(8f, .2f);
                Assert.That(GameObject.Find("ObjectiveStarHint_RT").GetComponent<TextMeshProUGUI>().text,
                    Is.EqualTo("RITMO: 1 ESTRELLA"), "Rating must follow the resources remaining, not objective completion.");
                AccessibilitySettings.ReducedMotion = false;
                for (int i = 0; i < 12; i++) ui.ShowMatchReward(Vector3.zero);
                int flying = 0;
                foreach (var image in Object.FindObjectsByType<UnityEngine.UI.Image>(FindObjectsInactive.Exclude))
                    if (image.name == "MatchReward")
                    {
                        flying++;
                        Assert.That(image.raycastTarget, Is.False);
                    }
                Assert.That(flying, Is.InRange(1, 3));
                yield return new WaitForSecondsRealtime(.8f);
                Assert.That(GameObject.Find("MatchReward"), Is.Null, "Reward animation must clean up after itself.");
            }
            finally { AccessibilitySettings.ReducedMotion = previous; }
        }
    }
}
