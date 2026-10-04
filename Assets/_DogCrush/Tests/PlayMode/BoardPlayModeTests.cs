using System.Collections;
using DogCrush.Board;
using DogCrush.Core;
using DogCrush.Gameplay;
using DogCrush.InputSystem;
using DogCrush.Presentation;
using DogCrush.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DogCrush.Tests.PlayMode
{
    public class BoardPlayModeTests
    {
        private static IEnumerator LoadGameplayScene()
        {
            // PlayMode tests must not inherit the developer's campaign save.
            PlayerPrefs.SetInt("DogCrush_UnlockedLevel", 1);
            SceneManager.LoadScene("Gameplay", LoadSceneMode.Single);
            yield return null;
            yield return null;
            Object.FindAnyObjectByType<GameBootstrap>()?.StartNewMatch();
            // Every level now presents a short, timer-safe objective card.
            yield return new WaitForSecondsRealtime(1.30f);
        }

        private static void RefreshOrdinaryFixtureArtwork(BoardController board)
        {
            // Fixtures author logical types directly; screenshots must show those same types.
            for (int x = 0; x < board.Columns; x++)
                for (int y = 0; y < board.Rows; y++)
                {
                    var piece = board.GetPieceAt(x, y);
                    if (piece == null || piece.IsSpecial) continue;
                    piece.Initialize(piece.type, x, y, board.spawner.GetSpriteForType(piece.type),
                        board.spawner.GetColorForType(piece.type));
                }
        }

        private static int CountPlayableCells(BoardController board)
        {
            int count = 0;
            for (int x = 0; x < board.Columns; x++)
                for (int y = 0; y < board.Rows; y++)
                    if (board.IsPlayableCell(x, y)) count++;
            return count;
        }

        private static void InvokeBoosterMethod(GameBootstrap bootstrap, string name)
        {
            typeof(GameBootstrap).GetMethod(name, System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).Invoke(bootstrap, null);
        }

        private static int RemainingLevelFood(GameBootstrap bootstrap) => (int)typeof(GameBootstrap)
            .GetField("levelFoodBoosters", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).GetValue(bootstrap);

        private static void GiveLevelFood(GameBootstrap bootstrap)
        {
            typeof(GameBootstrap).GetField("levelFoodBoosters", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).SetValue(bootstrap, 2);
            InvokeBoosterMethod(bootstrap, "RefreshBoosterCounts");
        }

        public static void CaptureGameplayState(string filename)
        {
            var camera=Camera.main;
            var frame=new RenderTexture(390,844,24);
            var pixels=new Texture2D(390,844,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;
            var canvases=Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            var modes=new System.Collections.Generic.List<RenderMode>();
            var cameras=new System.Collections.Generic.List<Camera>();
            foreach(var canvas in canvases)
            {
                modes.Add(canvas.renderMode);cameras.Add(canvas.worldCamera);
                if(canvas.renderMode==RenderMode.ScreenSpaceOverlay)
                {
                    canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1f;
                }
            }
            try
            {
                camera.targetTexture=frame;Canvas.ForceUpdateCanvases();camera.Render();
                RenderTexture.active=frame;pixels.ReadPixels(new Rect(0,0,390,844),0,0);pixels.Apply();
                string directory=System.IO.Path.Combine(Application.dataPath,"../Builds/visual-qa");
                System.IO.Directory.CreateDirectory(directory);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory,filename),pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active=previous;camera.targetTexture=null;
                for(int i=0;i<canvases.Length;i++) { canvases[i].renderMode=modes[i];canvases[i].worldCamera=cameras[i]; }
                frame.Release();Object.Destroy(frame);Object.Destroy(pixels);
            }
        }

        [UnityTest]
        public IEnumerator CascadeFeedback_RenderLocalAccentsAtDifferentDepths()
        {
            yield return LoadGameplayScene();
            var bootstrap=Object.FindAnyObjectByType<GameBootstrap>();
            bootstrap.uiController.SetMainMenuVisible(false);
            var board=bootstrap.boardController;
            var pieces=new System.Collections.Generic.List<PieceView>();
            for(int x=1;x<=6;x++) pieces.Add(board.GetPieceAt(x,3));
            foreach(int depth in new[]{1,5})
            {
                bootstrap.particleController.PlayMatchImpact(pieces,6,depth,board.ActivePieceSpacing,false);
                yield return new WaitForSeconds(.09f);
                CaptureGameplayState($"cascade-depth-{depth}-gameplay.png");
                bootstrap.particleController.ClearMatchImpacts();
            }
        }

        [UnityTest]
        public IEnumerator CometFlight_RenderCoralTravellersAndArrival()
        {
            yield return LoadGameplayScene();
            var bootstrap=Object.FindAnyObjectByType<GameBootstrap>();
            bootstrap.uiController.SetMainMenuVisible(false);
            var board=bootstrap.boardController;
            var comet=board.GetPieceAt(3,3);
            comet.SetSpecial(PieceSpecialType.Comet);
            bootstrap.particleController.PlaySpecialActivation(comet,board.Columns,board.Rows,board.ActivePieceSpacing);
            yield return new WaitForSeconds(.08f);
            CaptureGameplayState("comet-flight-gameplay.png");
            yield return new WaitForSeconds(.13f);
            yield return null;yield return null;
            CaptureGameplayState("comet-arrival-gameplay.png");
        }

        [UnityTest]
        public IEnumerator BallBounceResolution_RecordsRealTargetsAndRendersFlight()
        {
            yield return LoadGameplayScene();
            var bootstrap=Object.FindAnyObjectByType<GameBootstrap>();
            bootstrap.uiController.SetMainMenuVisible(false);
            var board=bootstrap.boardController;
            var ball=board.GetPieceAt(3,3);
            ball.SetSpecial(PieceSpecialType.BallBounce);
            var resolution=board.BuildMatchResolution(new System.Collections.Generic.List<PieceView>{ball});
            var destinations=resolution.BallBounceDestinations[ball];
            Assert.That(destinations.Count,Is.InRange(5,8));
            foreach(var destination in destinations)
                Assert.That(resolution.PiecesToRemove.Exists(p=>p.transform.position==destination),Is.True);
            var randomBefore=Random.state;
            bootstrap.particleController.PlayBallBounces(ball.transform.position,destinations,board.ActivePieceSpacing);
            Assert.That(Random.state,Is.EqualTo(randomBefore));
            yield return new WaitForSeconds(.08f);
            CaptureGameplayState("ball-bounce-flight-gameplay.png");
            yield return new WaitForSeconds(.13f);
            yield return null;
            yield return null;
            CaptureGameplayState("ball-bounce-arrival-gameplay.png");
        }

        [UnityTest]
        public IEnumerator SpecialRangePreview_RenderAndCancelInGameplay()
        {
            yield return LoadGameplayScene();
            var bootstrap=Object.FindAnyObjectByType<GameBootstrap>();
            bootstrap.uiController.SetMainMenuVisible(false);
            var board=bootstrap.boardController;
            var special=board.GetPieceAt(3,3);
            special.SetSpecial(PieceSpecialType.Comet);
            special.SetSelected(true);
            board.ShowSpecialPreview(special);
            yield return null;
            CaptureGameplayState("special-preview-comet-gameplay.png");
            special.SetSpecial(PieceSpecialType.AreaBlast);
            board.ShowSpecialPreview(special);
            yield return null;
            CaptureGameplayState("special-preview-area-gameplay.png");
            bootstrap.StartNewMatch();
            Assert.That(board.GetComponent<AdaptiveBoardView>().GetComponentsInChildren<SpriteRenderer>().Length,Is.GreaterThan(0));
            yield return null;
        }

        [UnityTest]
        public IEnumerator WideComboAtEdge_UsesSwappedAreaAndRendersActualLanes()
        {
            foreach (bool horizontal in new[] {true,false})
            {
                yield return LoadGameplayScene();
                var bootstrap=Object.FindAnyObjectByType<GameBootstrap>();
                bootstrap.uiController.SetMainMenuVisible(false);
                var board=bootstrap.boardController;
                var area=board.GetPieceAt(0,0);
                var ray=board.GetPieceAt(horizontal ? 1 : 0,horizontal ? 0 : 1);
                area.SetSpecial(PieceSpecialType.AreaBlast);
                ray.SetSpecial(horizontal ? PieceSpecialType.RowBlast : PieceSpecialType.ColumnBlast);
                Assert.That(board.TrySwapAndFindMatches(area,ray,out var matches),Is.True);
                var resolution=board.BuildMatchResolution(matches);
                Assert.That(resolution.ComboKind,Is.EqualTo(horizontal ? SpecialComboKind.WideRow : SpecialComboKind.WideColumn));
                Assert.That(resolution.ComboAnchor,Is.SameAs(area));
                yield return new WaitForSeconds(.2f);
                bootstrap.particleController.PlaySpecialCombo(resolution.ComboKind,
                    (area.transform.position+ray.transform.position)/2f,
                    board.Columns,board.Rows,board.ActivePieceSpacing,resolution.ComboAnchor);
                yield return new WaitForSeconds(.12f);
                Assert.That(bootstrap.particleController.GetComponentsInChildren<LineRenderer>().Length,Is.EqualTo(4));
                CaptureGameplayState(horizontal ? "wide-row-edge-gameplay.png" : "wide-column-edge-gameplay.png");
            }
        }

        [UnityTest]
        public IEnumerator CometAndRowPair_CombinesActualRoutesWithoutInventingACross()
        {
            yield return LoadGameplayScene();
            var bootstrap=Object.FindAnyObjectByType<GameBootstrap>();
            bootstrap.uiController.SetMainMenuVisible(false);
            var board=bootstrap.boardController;
            for(int x=0;x<board.Columns;x++) for(int y=0;y<board.Rows;y++)
                board.GetPieceAt(x,y)?.SetSpecial(PieceSpecialType.None);
            var comet=board.GetPieceAt(3,3);var ray=board.GetPieceAt(4,3);
            comet.SetSpecial(PieceSpecialType.Comet);ray.SetSpecial(PieceSpecialType.RowBlast);
            Assert.That(board.TrySwapAndFindMatches(comet,ray,out var matches),Is.True);
            var resolution=board.BuildMatchResolution(matches);
            Assert.That(resolution.ComboKind,Is.EqualTo(SpecialComboKind.CombinedPowers));
            Assert.That(resolution.SpecialsActivated,Is.EqualTo(2));
            for(int x=0;x<board.Columns;x++) for(int y=0;y<board.Rows;y++)
            {
                bool expected=y==ray.gridY || Mathf.Abs(x-comet.gridX)==Mathf.Abs(y-comet.gridY);
                Assert.That(resolution.PiecesToRemove.Contains(board.GetPieceAt(x,y)),Is.EqualTo(expected),
                    "Only the real row and diagonals may be cleared at "+x+","+y);
            }
            yield return new WaitForSeconds(.2f);
            var routine=(IEnumerator)typeof(GameBootstrap).GetMethod("PlaySpecialImpactAfterCharge",
                System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)
                .Invoke(bootstrap,new object[]{resolution,(comet.transform.position+ray.transform.position)/2f,0f});
            bootstrap.StartCoroutine(routine);
            yield return new WaitForSeconds(.12f);
            CaptureGameplayState("pair-comet-row-gameplay.png");
            bootstrap.StartNewMatch();
        }

        [UnityTest]
        public IEnumerator RayPairs_ClearTheirSwappedAxesIncludingSharedLanes()
        {
            yield return LoadGameplayScene();
            var board=Object.FindAnyObjectByType<GameBootstrap>().boardController;
            for(int scenario=0;scenario<5;scenario++)
            {
                for(int x=0;x<board.Columns;x++) for(int y=0;y<board.Rows;y++)
                    board.GetPieceAt(x,y)?.SetSpecial(PieceSpecialType.None);
                var first=board.GetPieceAt(3,3);
                var second=board.GetPieceAt(scenario%2==0?4:3,scenario%2==0?3:4);
                first.SetSpecial(scenario<2?PieceSpecialType.RowBlast:PieceSpecialType.ColumnBlast);
                second.SetSpecial(scenario==4?PieceSpecialType.RowBlast:first.SpecialType);
                Assert.That(board.TrySwapAndFindMatches(first,second,out var matches),Is.True);
                var resolution=board.BuildMatchResolution(matches);
                Assert.That(resolution.SpecialsActivated,Is.EqualTo(2));
                Assert.That(resolution.ComboKind,Is.EqualTo(scenario<2?SpecialComboKind.DoubleRow:
                    scenario<4?SpecialComboKind.DoubleColumn:SpecialComboKind.CrossBlast));
                for(int x=0;x<board.Columns;x++) for(int y=0;y<board.Rows;y++)
                {
                    bool expected=(first.SpecialType==PieceSpecialType.RowBlast?y==first.gridY:x==first.gridX) ||
                        (second.SpecialType==PieceSpecialType.RowBlast?y==second.gridY:x==second.gridX);
                    Assert.That(resolution.PiecesToRemove.Contains(board.GetPieceAt(x,y)),Is.EqualTo(expected));
                }
            }
            Object.FindAnyObjectByType<GameBootstrap>().StartNewMatch();
        }

        [UnityTest]
        public IEnumerator TacticalOpenings_OfferTwoPreparationsAndARealSecondColourChoice()
        {
            var previousRandom=Random.state;
            try
            {
                yield return LoadGameplayScene();
                var bootstrap=Object.FindAnyObjectByType<GameBootstrap>();
                foreach(int seed in new[]{128,7,31,99,2026})
                foreach(int level in new[]{4,8})
                {
                    Random.InitState(seed+level);
                    bootstrap.currentLevel=level;bootstrap.StartNewMatch();bootstrap.uiController.SetMainMenuVisible(false);
                    yield return new WaitForSecondsRealtime(.3f);
                    CaptureGameplayState("tactical-opening-intro-"+level+".png");
                    yield return new WaitForSecondsRealtime(4.5f);
                    var board=bootstrap.boardController;
                    Assert.That(board.FindMatches().Count,Is.Zero,"Opening must wait for the player.");
                    foreach(int row in new[]{2,5})
                    {
                        Assert.That(board.TryGetSpecialCreationPreview(board.GetPieceAt(3,row-1),board.GetPieceAt(3,row),
                            out var cell,out var kind),Is.True);
                        Assert.That(kind,Is.EqualTo(PieceSpecialType.ColorBurst));
                        Assert.That(cell,Is.EqualTo(new Vector2Int(3,row)));
                    }
                    CaptureGameplayState("tactical-opening-board-"+level+".png");
                    var moved=board.GetPieceAt(3,4);
                    Assert.That(board.TrySwapAndFindMatches(moved,board.GetPieceAt(3,5),out var matches),Is.True);
                    typeof(GameBootstrap).GetMethod("HandleChainCompleted",
                        System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)
                        .Invoke(bootstrap,new object[]{matches});
                    float deadline=Time.unscaledTime+15f;
                    var state=Object.FindAnyObjectByType<GameStateController>();
                    yield return null;
                    while(!state.CanSelectPieces() && Time.unscaledTime<deadline) yield return null;
                    Assert.That(state.CanSelectPieces(),Is.True,"Preparation must settle into a playable second step.");
                    Assert.That(moved.SpecialType,Is.EqualTo(PieceSpecialType.ColorBurst));
                    Assert.That(board.GetPieceAt(moved.gridX,moved.gridY),Is.SameAs(moved));
                    var target=board.GetPieceAt(moved.gridX,moved.gridY+1);
                    Assert.That(target,Is.Not.Null);Assert.That(target.IsSpecial,Is.False);
                    var chosenType=target.type;
                    var authored=Resources.Load<LevelDesignAsset>("Campaign/Levels/level_"+level.ToString("000"));
                    Assert.That(chosenType,Is.EqualTo(authored.targetPieceType));
                    CaptureGameplayState("tactical-opening-prepared-"+level+".png");
                    Assert.That(board.TrySwapAndFindMatches(moved,target,out var nextMatches),Is.True);
                    var next=board.BuildMatchResolution(nextMatches);
                    Assert.That(next.ColorBurstCombo,Is.True);
                    for(int x=0;x<board.Columns;x++) for(int y=0;y<board.Rows;y++)
                    {
                        var piece=board.GetPieceAt(x,y);
                        if(piece!=null && piece.type==chosenType) Assert.That(next.PiecesToRemove,Does.Contain(piece));
                    }
                }
                bootstrap.currentLevel=1;bootstrap.StartNewMatch();
                Assert.That(bootstrap.boardController.config.initialPieceRows,Is.Null.Or.Empty,
                    "An authored opening must not leak into other levels.");
            }
            finally {Random.state=previousRandom;}
        }

        [UnityTest]
        public IEnumerator StrategicOpening_OffersCollectionLanternsAndPreparationWithoutFreeRewards()
        {
            var previousRandom = Random.state;
            bool previousMotion = JoinDog.App.AccessibilitySettings.ReducedMotion;
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            try
            {
                yield return LoadGameplayScene();
                var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
                foreach (int seed in new[]{16,1216})
                foreach (bool reduced in new[]{false,true})
                for (int route = 0; route < 3; route++)
                {
                    Random.InitState(seed);
                    JoinDog.App.AccessibilitySettings.ReducedMotion = reduced;
                    bootstrap.currentLevel = 16; bootstrap.StartNewMatch();
                    bootstrap.uiController.SetMainMenuVisible(false);
                    yield return new WaitForSecondsRealtime(.3f);
                    if (seed == 16 && !reduced && route == 0)
                        CaptureGameplayState("strategic-opening-intro-16.png");
                    yield return new WaitForSecondsRealtime(4.5f);
                    var board = bootstrap.boardController;
                    Assert.That(board.FindMatches().Count, Is.Zero, "No automatic opening rewards.");
                    Assert.That(board.RemainingObstacleCount, Is.EqualTo(12));
                    Assert.That((int)typeof(GameBootstrap).GetField("objectiveProgress",flags).GetValue(bootstrap), Is.Zero);
                    var definition = (LevelDefinition)typeof(GameBootstrap).GetProperty("CurrentLevelDefinition",flags).GetValue(bootstrap);
                    Assert.That(definition.targetAmount, Is.EqualTo(26));
                    Assert.That(definition.durationSeconds, Is.EqualTo(72.4f).Within(.01f));
                    int x = route == 0 ? 6 : route == 1 ? 1 : 6;
                    int fromY = route == 0 ? 2 : route == 1 ? 3 : 4;
                    int toY = fromY + 1;
                    var moved = board.GetPieceAt(x,fromY);
                    var destination = board.GetPieceAt(x,toY);
                    bool preview = board.TryGetSpecialCreationPreview(moved,destination,out var cell,out var kind);
                    Assert.That(preview, Is.EqualTo(route == 2));
                    if (route == 2)
                    {
                        Assert.That(cell, Is.EqualTo(new Vector2Int(6,5)));
                        Assert.That(kind, Is.EqualTo(PieceSpecialType.RowBlast));
                    }
                    if (seed == 16 && !reduced && route == 0)
                        CaptureGameplayState("strategic-opening-board-16.png");
                    Assert.That(board.TrySwapAndFindMatches(moved,destination,out var matches), Is.True);
                    Assert.That(matches.Count, Is.EqualTo(route == 2 ? 4 : 3));
                    foreach (var piece in matches)
                        Assert.That(piece.type, Is.EqualTo(route == 0 ? PieceType.Ball : route == 1 ? PieceType.Dog : PieceType.Bone));
                    var health = (int[,])typeof(BoardController).GetField("obstacleHealth",flags).GetValue(board);
                    int expectedCleared = 0;
                    foreach (var piece in matches)
                        if (!(route == 2 && piece == moved) && health[piece.gridX,piece.gridY] == 1) expectedCleared++;
                    if (route == 1) Assert.That(expectedCleared, Is.GreaterThanOrEqualTo(2));
                    typeof(GameBootstrap).GetMethod("HandlePlayerMatch3Move",flags).Invoke(bootstrap,new object[]{matches});
                    Assert.That((int)typeof(GameBootstrap).GetField("objectiveProgress",flags).GetValue(bootstrap),
                        Is.EqualTo(route == 0 ? 3 : 0), "Only actual collected balls advance the mission.");
                    Assert.That(board.RemainingObstacleCount, Is.EqualTo(12 - expectedCleared),
                        "Only lanterns under removed pieces are cleared; a retained special gives no free hit.");
                    Assert.That(moved.SpecialType, Is.EqualTo(route == 2 ? PieceSpecialType.RowBlast : PieceSpecialType.None));
                    if (route == 2) Assert.That(board.GetPieceAt(6,5), Is.SameAs(moved));
                    yield return new WaitForSecondsRealtime(.18f);
                    if (seed == 16)
                        CaptureGameplayState("strategic-opening-route-"+route+(reduced ? "-reduced" : "")+".png");
                    var state = Object.FindAnyObjectByType<GameStateController>();
                    float deadline = Time.unscaledTime + 15f;
                    while (!state.CanSelectPieces() && Time.unscaledTime < deadline) yield return null;
                    Assert.That(state.CanSelectPieces(), Is.True, "Each route must return control after its cascades.");
                    if (route == 1)
                    {
                        // Clearing the left trio brings authored balls down. This path
                        // has a real second-step collection benefit, beyond its lantern counter.
                        Assert.That(board.GetPieceAt(0,4).type, Is.EqualTo(PieceType.Ball));
                        Assert.That(board.GetPieceAt(2,4).type, Is.EqualTo(PieceType.Ball));
                        var nextBall = board.GetPieceAt(1,5);
                        Assert.That(nextBall.type, Is.EqualTo(PieceType.Ball));
                        if (seed == 16 && !reduced) CaptureGameplayState("strategic-opening-left-prepared.png");
                        Assert.That(board.TrySwapAndFindMatches(nextBall,board.GetPieceAt(1,4),out var followup), Is.True);
                        Assert.That(followup.Count, Is.EqualTo(3));
                        foreach (var piece in followup) Assert.That(piece.type, Is.EqualTo(PieceType.Ball));
                        typeof(GameBootstrap).GetMethod("HandlePlayerMatch3Move",flags).Invoke(bootstrap,new object[]{followup});
                        Assert.That((int)typeof(GameBootstrap).GetField("objectiveProgress",flags).GetValue(bootstrap), Is.EqualTo(3));
                        yield return new WaitForSecondsRealtime(.18f);
                        if (seed == 16 && !reduced) CaptureGameplayState("strategic-opening-left-followup.png");
                        deadline = Time.unscaledTime + 15f;
                        while (!state.CanSelectPieces() && Time.unscaledTime < deadline) yield return null;
                        Assert.That(state.CanSelectPieces(), Is.True);
                    }
                    if (route == 2)
                    {
                        Assert.That(moved.SpecialType, Is.EqualTo(PieceSpecialType.RowBlast));
                        Assert.That(board.GetPieceAt(moved.gridX,moved.gridY), Is.SameAs(moved));
                    }
                }
                bootstrap.currentLevel = 17; bootstrap.StartNewMatch();
                Assert.That(bootstrap.boardController.config.initialPieceRows, Is.Null.Or.Empty);
            }
            finally
            {
                Random.state = previousRandom;
                JoinDog.App.AccessibilitySettings.ReducedMotion = previousMotion;
            }
        }

        [UnityTest]
        public IEnumerator CollectionPhases_CountOnlyActiveTypeAndResetWithoutBankingFuturePieces()
        {
            var previousRandom = Random.state;
            bool previousMotion = JoinDog.App.AccessibilitySettings.ReducedMotion;
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            try
            {
                yield return LoadGameplayScene();
                var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
                var update = typeof(GameBootstrap).GetMethod("UpdateObjectiveProgress",flags);
                var progress = typeof(GameBootstrap).GetField("objectiveProgress",flags);
                var complete = typeof(GameBootstrap).GetMethod("IsCurrentObjectiveComplete",flags);
                foreach (bool reduced in new[]{false,true})
                {
                    Random.InitState(1813);
                    JoinDog.App.AccessibilitySettings.ReducedMotion = reduced;
                    bootstrap.currentLevel = 18; bootstrap.StartNewMatch();
                    bootstrap.uiController.SetMainMenuVisible(false);
                    yield return new WaitForSecondsRealtime(.3f);
                    if (!reduced) CaptureGameplayState("collection-phases-intro-18.png");
                    yield return new WaitForSecondsRealtime(4.5f);
                    var definition = (LevelDefinition)typeof(GameBootstrap).GetProperty("CurrentLevelDefinition",flags).GetValue(bootstrap);
                    Assert.That(definition.HasCollectionPhases, Is.True);
                    Assert.That(definition.targetAmount, Is.EqualTo(6));
                    Assert.That(definition.moveLimit, Is.GreaterThan(0));
                    Assert.That(bootstrap.boardController.RemainingObstacleCount, Is.EqualTo(13));
                    var board = bootstrap.boardController;
                    var pieces = new System.Collections.Generic.List<PieceView>();
                    for (int x=1;x<=7;x++) pieces.Add(board.GetPieceAt(x,3));
                    // Feed real board pieces into the mission boundary independently
                    // of fall randomness, including simultaneous clears of both types.
                    foreach (var piece in pieces) piece.type=PieceType.Dog;
                    update.Invoke(bootstrap,new object[]{pieces,0,0});
                    Assert.That((int)progress.GetValue(bootstrap), Is.Zero, "Future dogs are not banked.");
                    for (int i=0;i<pieces.Count;i++) pieces[i].type=i<4 ? PieceType.Bone : PieceType.Dog;
                    RefreshOrdinaryFixtureArtwork(board);
                    update.Invoke(bootstrap,new object[]{pieces,0,0});
                    Assert.That((int)progress.GetValue(bootstrap), Is.EqualTo(3), "Overflow bones and dogs in the same resolution cannot skip phase two.");
                    Assert.That((bool)complete.Invoke(bootstrap,null), Is.False);
                    yield return null;
                    CaptureGameplayState("collection-phases-second"+(reduced ? "-reduced" : "")+".png");
                    var label=(string)typeof(GameplayUIController).GetField("objectiveLabel",flags).GetValue(bootstrap.uiController);
                    Assert.That(label, Does.Contain("2/2")); Assert.That(label, Does.Contain("PERRITOS"));
                    foreach (var piece in pieces) piece.type=PieceType.Bone;
                    update.Invoke(bootstrap,new object[]{pieces,0,0});
                    Assert.That((int)progress.GetValue(bootstrap), Is.EqualTo(3), "Completed phase pieces no longer advance.");
                    pieces[0].type=PieceType.Dog; pieces[1].type=PieceType.Dog;
                    update.Invoke(bootstrap,new object[]{pieces,0,0});
                    Assert.That((int)progress.GetValue(bootstrap), Is.EqualTo(5));
                    Assert.That((bool)complete.Invoke(bootstrap,null), Is.False);
                    update.Invoke(bootstrap,new object[]{pieces,0,0});
                    Assert.That((int)progress.GetValue(bootstrap), Is.EqualTo(6));
                    Assert.That((bool)complete.Invoke(bootstrap,null), Is.True);
                    bootstrap.StartNewMatch();
                    Assert.That((int)progress.GetValue(bootstrap), Is.Zero);
                    label=(string)typeof(GameplayUIController).GetField("objectiveLabel",flags).GetValue(bootstrap.uiController);
                    Assert.That(label, Does.Contain("1/2")); Assert.That(label, Does.Contain("HUESOS"));
                    foreach (int invalid in new[]{-1,0,6,7})
                    {
                        definition.firstCollectionPhaseAmount=invalid;
                        Assert.That(definition.HasCollectionPhases, Is.False);
                    }
                    definition.firstCollectionPhaseAmount=3;
                    var secondary=definition.secondaryTargetPieceType;
                    definition.secondaryTargetPieceType=definition.targetPieceType;
                    Assert.That(definition.HasCollectionPhases, Is.False);
                    definition.secondaryTargetPieceType=secondary;
                    definition.objectiveType=LevelObjectiveType.Score;
                    Assert.That(definition.HasCollectionPhases, Is.False);
                    definition.objectiveType=LevelObjectiveType.CollectTwoTypes;
                }
                bootstrap.currentLevel=16; bootstrap.StartNewMatch();
                var other=(LevelDefinition)typeof(GameBootstrap).GetProperty("CurrentLevelDefinition",flags).GetValue(bootstrap);
                Assert.That(other.HasCollectionPhases, Is.False);
            }
            finally { Random.state=previousRandom; JoinDog.App.AccessibilitySettings.ReducedMotion=previousMotion; }
        }

        [UnityTest]
        public IEnumerator CollectionPhases_RealSwapsAdvanceBothStagesBeforeVictory()
        {
            var previousRandom = Random.state;
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            try
            {
                yield return LoadGameplayScene();
                Random.InitState(18130);
                var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
                bootstrap.currentLevel=18; bootstrap.StartNewMatch(); bootstrap.uiController.SetMainMenuVisible(false);
                yield return new WaitForSecondsRealtime(4.5f);
                var board=bootstrap.boardController;
                var progress=typeof(GameBootstrap).GetField("objectiveProgress",flags);
                var moves=typeof(GameBootstrap).GetField("movesRemaining",flags);
                int initialMoveBudget=(int)moves.GetValue(bootstrap);
                var state=Object.FindAnyObjectByType<GameStateController>();
                var pool=new[]{PieceType.Dog,PieceType.Bone,PieceType.Food,PieceType.Collar,PieceType.Duck};
                for(int stage=0;stage<2;stage++)
                {
                    for(int x=0;x<board.Columns;x++) for(int y=0;y<board.Rows;y++)
                    {
                        var piece=board.GetPieceAt(x,y);
                        if(piece==null) continue;
                        piece.type=pool[(x+2*y)%pool.Length]; piece.SetSpecial(PieceSpecialType.None);
                    }
                    PieceType active=stage==0 ? PieceType.Bone : PieceType.Dog;
                    board.GetPieceAt(1,3).type=PieceType.Food; board.GetPieceAt(5,3).type=PieceType.Food;
                    board.GetPieceAt(2,3).type=active; board.GetPieceAt(4,3).type=active;
                    board.GetPieceAt(3,3).type=PieceType.Collar; board.GetPieceAt(3,2).type=active;
                    RefreshOrdinaryFixtureArtwork(board);
                    Assert.That(board.FindMatches().Count,Is.Zero);
                    Assert.That(board.TrySwapAndFindMatches(board.GetPieceAt(3,2),board.GetPieceAt(3,3),out var matches),Is.True);
                    Assert.That(matches.Count,Is.EqualTo(3));
                    typeof(GameBootstrap).GetMethod("HandlePlayerMatch3Move",flags).Invoke(bootstrap,new object[]{matches});
                    Assert.That((int)progress.GetValue(bootstrap),Is.EqualTo(stage==0 ? 3 : 6));
                    Assert.That((int)moves.GetValue(bootstrap),Is.EqualTo(initialMoveBudget-1-stage));
                    Assert.That((bool)typeof(GameBootstrap).GetMethod("IsCurrentObjectiveComplete",flags).Invoke(bootstrap,null),Is.EqualTo(stage==1));
                    yield return new WaitForSecondsRealtime(.08f);
                    CaptureGameplayState("collection-phases-real-stage-"+(stage+1)+".png");
                    if(stage==0)
                    {
                        float deadline=Time.unscaledTime+15f;
                        while(!state.CanSelectPieces() && Time.unscaledTime<deadline) yield return null;
                        Assert.That(state.CanSelectPieces(),Is.True);
                        Assert.That((int)progress.GetValue(bootstrap),Is.EqualTo(3));
                    }
                }
                // Restart before the final presentation can award campaign rewards.
                bootstrap.StartNewMatch();
                Assert.That((int)progress.GetValue(bootstrap),Is.Zero);
            }
            finally {Random.state=previousRandom;}
        }

        [UnityTest]
        public IEnumerator RescueMissions_UseEnoughRealCagesAndCountOnlyReleasedPuppies()
        {
            var previousRandom=Random.state;
            var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
            try
            {
                yield return LoadGameplayScene();
                var bootstrap=Object.FindAnyObjectByType<GameBootstrap>();
                var get=typeof(GameBootstrap).GetMethod("GetLevelDefinition",flags);
                for(int level=1;level<=100;level++)
                {
                    var definition=(LevelDefinition)get.Invoke(bootstrap,new object[]{level});
                    if(definition.objectiveType!=LevelObjectiveType.RescuePuppies) continue;
                    Assert.That(definition.obstacleType,Is.EqualTo(CellObstacleType.PuppyCage),"Rescue level "+level);
                    Assert.That(definition.obstacleCount,Is.GreaterThanOrEqualTo(definition.targetAmount));
                }
                foreach(int level in new[]{10,15,20})
                {
                    bootstrap.currentLevel=level;bootstrap.StartNewMatch();bootstrap.uiController.SetMainMenuVisible(false);
                    yield return new WaitForSecondsRealtime(.3f);
                    CaptureGameplayState("rescue-repair-intro-"+level+".png");
                    yield return new WaitForSecondsRealtime(4.5f);
                    var board=bootstrap.boardController;
                    var definition=(LevelDefinition)get.Invoke(bootstrap,new object[]{level});
                    Assert.That(board.RemainingObstacleCount,Is.GreaterThanOrEqualTo(definition.targetAmount));
                    if(level!=10) continue;
                    // Two durable cages verify partial damage does not count as a rescue.
                    board.config.obstacleCount=2;board.config.obstacleCells=new[]{"2,4","3,4"};
                    board.config.obstacleDurability=2;board.InitializeBoard();yield return null;
                    var affected=new System.Collections.Generic.List<PieceView>{board.GetPieceAt(2,4),board.GetPieceAt(3,4)};
                    var update=typeof(GameBootstrap).GetMethod("UpdateObjectiveProgress",flags);
                    var progress=typeof(GameBootstrap).GetField("objectiveProgress",flags);
                    int cleared=board.DamageObstacles(affected);
                    Assert.That(cleared,Is.Zero);
                    update.Invoke(bootstrap,new object[]{affected,0,cleared});
                    Assert.That((int)progress.GetValue(bootstrap),Is.Zero);
                    cleared=board.DamageObstacles(affected);
                    Assert.That(cleared,Is.EqualTo(2));
                    update.Invoke(bootstrap,new object[]{affected,0,cleared});
                    Assert.That((int)progress.GetValue(bootstrap),Is.EqualTo(2));
                    Assert.That((bool)typeof(GameBootstrap).GetMethod("IsCurrentObjectiveComplete",flags).Invoke(bootstrap,null),Is.False);
                    cleared=board.DamageObstacles(affected);
                    Assert.That(cleared,Is.Zero);
                    update.Invoke(bootstrap,new object[]{affected,0,cleared});
                    Assert.That((int)progress.GetValue(bootstrap),Is.EqualTo(2),"An already opened cage cannot award another rescue.");
                    CaptureGameplayState("rescue-repair-progress-10.png");
                    bootstrap.StartNewMatch();Assert.That((int)progress.GetValue(bootstrap),Is.Zero);
                }
            }
            finally {Random.state=previousRandom;}
        }

        [UnityTest]
        public IEnumerator MeadowFinale_RescuesThroughFlowerPreparationAndRealCometActivation()
        {
            var previousRandom=Random.state;
            bool previousMotion=JoinDog.App.AccessibilitySettings.ReducedMotion;
            var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
            try
            {
                yield return LoadGameplayScene();
                var bootstrap=Object.FindAnyObjectByType<GameBootstrap>();
                var progress=typeof(GameBootstrap).GetField("objectiveProgress",flags);
                foreach(int seed in new[]{10,14,1014})
                foreach(bool reduced in new[]{false,true})
                {
                    Random.InitState(seed);JoinDog.App.AccessibilitySettings.ReducedMotion=reduced;
                    bootstrap.currentLevel=10;bootstrap.StartNewMatch();bootstrap.uiController.SetMainMenuVisible(false);
                    yield return new WaitForSecondsRealtime(.3f);
                    if(seed==10 && !reduced) CaptureGameplayState("meadow-finale-intro.png");
                    if(seed==14 && !reduced) CaptureGameplayState("meadow-finale-strategy-intro.png");
                    yield return new WaitForSecondsRealtime(4.5f);
                    var board=bootstrap.boardController;
                    Assert.That(board.FindMatches().Count,Is.Zero);
                    Assert.That(board.RemainingObstacleCount,Is.EqualTo(12));
                    Assert.That(board.config.obstacleType,Is.EqualTo(CellObstacleType.PuppyCage));
                    var created=board.GetPieceAt(4,3);
                    Assert.That(board.TryGetSpecialCreationPreview(created,board.GetPieceAt(4,4),out var cell,out var special),Is.True);
                    Assert.That(cell,Is.EqualTo(new Vector2Int(4,4)));Assert.That(special,Is.EqualTo(PieceSpecialType.ColorBurst));
                    Assert.That(board.TrySwapAndFindMatches(created,board.GetPieceAt(4,4),out var matches),Is.True);
                    Assert.That(matches.Count,Is.EqualTo(5));
                    typeof(GameBootstrap).GetMethod("HandlePlayerMatch3Move",flags).Invoke(bootstrap,new object[]{matches});
                    Assert.That((int)progress.GetValue(bootstrap),Is.EqualTo(4));
                    Assert.That(board.RemainingObstacleCount,Is.EqualTo(8));
                    Assert.That(created.SpecialType,Is.EqualTo(PieceSpecialType.ColorBurst));
                    Assert.That(board.IsCompanionGardenHarvested(4,4),Is.True);
                    Assert.That((int)typeof(GameBootstrap).GetField("companionCharge",flags).GetValue(bootstrap),Is.EqualTo(2));
                    yield return new WaitForSecondsRealtime(.18f);
                    if(seed==10) CaptureGameplayState("meadow-finale-create"+(reduced ? "-reduced" : "")+".png");
                    var state=Object.FindAnyObjectByType<GameStateController>();
                    float deadline=Time.unscaledTime+15f;
                    while(!state.CanSelectPieces() && Time.unscaledTime<deadline) yield return null;
                    Assert.That(state.CanSelectPieces(),Is.True,
                        $"Finale must leave room for the planned activation: seed={seed}, reduced={reduced}, state={state.CurrentState}, rescued={progress.GetValue(bootstrap)}, cages={board.RemainingObstacleCount}, matches={board.FindMatches().Count}.");
                    Assert.That((int)progress.GetValue(bootstrap),Is.EqualTo(4));
                    Assert.That(board.GetPieceAt(4,4),Is.SameAs(created));
                    var target=board.GetPieceAt(4,5);
                    Assert.That(target,Is.Not.Null);Assert.That(target.IsSpecial,Is.False);
                    if(seed==10 && !reduced) CaptureGameplayState("meadow-finale-prepared.png");
                    Assert.That(board.TrySwapAndFindMatches(created,target,out var activation),Is.True);
                    typeof(GameBootstrap).GetMethod("HandlePlayerMatch3Move",flags).Invoke(bootstrap,new object[]{activation});
                    Assert.That((int)progress.GetValue(bootstrap),Is.GreaterThanOrEqualTo(5));
                    Assert.That((bool)typeof(GameBootstrap).GetMethod("IsCurrentObjectiveComplete",flags).Invoke(bootstrap,null),Is.True);
                    yield return new WaitForSecondsRealtime(.08f);
                    if(seed==10) CaptureGameplayState("meadow-finale-activate"+(reduced ? "-reduced" : "")+".png");
                    // Cancel final awards before starting the next restore-safe fixture.
                    bootstrap.StartNewMatch();
                    Assert.That((int)progress.GetValue(bootstrap),Is.Zero);
                }
            }
            finally {Random.state=previousRandom;JoinDog.App.AccessibilitySettings.ReducedMotion=previousMotion;}
        }

        [UnityTest]
        public IEnumerator ForestFinale_PreparesTwoCometsAndOpensDoubleLayerCagesWithTheirPair()
        {
            var previousRandom=Random.state;
            bool previousMotion=JoinDog.App.AccessibilitySettings.ReducedMotion;
            var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
            try
            {
                yield return LoadGameplayScene();
                var bootstrap=Object.FindAnyObjectByType<GameBootstrap>();
                var progress=typeof(GameBootstrap).GetField("objectiveProgress",flags);
                foreach(int seed in new[]{20,24,2024})
                foreach(bool reduced in new[]{false,true})
                {
                    Random.InitState(seed);JoinDog.App.AccessibilitySettings.ReducedMotion=reduced;
                    bootstrap.currentLevel=20;bootstrap.StartNewMatch();bootstrap.uiController.SetMainMenuVisible(false);
                    yield return new WaitForSecondsRealtime(.3f);
                    if(seed==24 && !reduced) CaptureGameplayState("forest-finale-intro.png");
                    yield return new WaitForSecondsRealtime(4.5f);
                    var board=bootstrap.boardController;
                    var state=Object.FindAnyObjectByType<GameStateController>();
                    Assert.That(board.FindMatches().Count,Is.Zero);
                    Assert.That(board.RemainingObstacleCount,Is.EqualTo(16));
                    Assert.That(board.config.obstacleType,Is.EqualTo(CellObstacleType.PuppyCage));
                    Assert.That(board.config.obstacleDurability,Is.EqualTo(2));
                    var first=board.GetPieceAt(5,3);
                    Assert.That(board.TryGetSpecialCreationPreview(first,board.GetPieceAt(4,3),out var cell,out var special),Is.True);
                    Assert.That(cell,Is.EqualTo(new Vector2Int(4,3)));Assert.That(special,Is.EqualTo(PieceSpecialType.ColorBurst));
                    Assert.That(board.TrySwapAndFindMatches(first,board.GetPieceAt(4,3),out var matches),Is.True);
                    Assert.That(matches.Count,Is.EqualTo(5));
                    typeof(GameBootstrap).GetMethod("HandlePlayerMatch3Move",flags).Invoke(bootstrap,new object[]{matches});
                    Assert.That((int)progress.GetValue(bootstrap),Is.Zero,"First impacts soften the double locks.");
                    float deadline=Time.unscaledTime+15f;
                    yield return null;
                    while(!state.CanSelectPieces() && Time.unscaledTime<deadline) yield return null;
                    Assert.That(state.CanSelectPieces(),Is.True,$"First preparation: seed={seed}, reduced={reduced}, state={state.CurrentState}, rescued={progress.GetValue(bootstrap)}.");
                    Assert.That(board.GetPieceAt(4,1),Is.SameAs(first));
                    Assert.That(first.SpecialType,Is.EqualTo(PieceSpecialType.ColorBurst));
                    var second=board.GetPieceAt(4,3);
                    Assert.That(board.TryGetSpecialCreationPreview(second,board.GetPieceAt(4,2),out cell,out special),Is.True);
                    Assert.That(cell,Is.EqualTo(new Vector2Int(4,2)));Assert.That(special,Is.EqualTo(PieceSpecialType.ColorBurst));
                    Assert.That(board.TrySwapAndFindMatches(second,board.GetPieceAt(4,2),out matches),Is.True);
                    Assert.That(matches.Count,Is.EqualTo(5));
                    typeof(GameBootstrap).GetMethod("HandlePlayerMatch3Move",flags).Invoke(bootstrap,new object[]{matches});
                    deadline=Time.unscaledTime+15f;
                    yield return null;
                    while(!state.CanSelectPieces() && Time.unscaledTime<deadline) yield return null;
                    Assert.That(state.CanSelectPieces(),Is.True,$"Second preparation: seed={seed}, reduced={reduced}, state={state.CurrentState}, rescued={progress.GetValue(bootstrap)}.");
                    Assert.That(board.GetPieceAt(4,1),Is.SameAs(first));
                    Assert.That(board.GetPieceAt(4,2),Is.SameAs(second));
                    Assert.That(second.SpecialType,Is.EqualTo(PieceSpecialType.ColorBurst));
                    Assert.That((int)progress.GetValue(bootstrap),Is.LessThan(7));
                    if(seed==20) CaptureGameplayState("forest-finale-prepared"+(reduced?"-reduced":"")+".png");
                    Assert.That(board.TrySwapAndFindMatches(first,second,out var pair),Is.True);
                    typeof(GameBootstrap).GetMethod("HandlePlayerMatch3Move",flags).Invoke(bootstrap,new object[]{pair});
                    Assert.That((int)progress.GetValue(bootstrap),Is.GreaterThanOrEqualTo(7));
                    Assert.That((bool)typeof(GameBootstrap).GetMethod("IsCurrentObjectiveComplete",flags).Invoke(bootstrap,null),Is.True);
                    yield return new WaitForSecondsRealtime(.08f);
                    if(seed==20) CaptureGameplayState("forest-finale-pair"+(reduced?"-reduced":"")+".png");
                    bootstrap.StartNewMatch();Assert.That((int)progress.GetValue(bootstrap),Is.Zero);
                }
            }
            finally {Random.state=previousRandom;JoinDog.App.AccessibilitySettings.ReducedMotion=previousMotion;}
        }

        [UnityTest]
        public IEnumerator FestivalFinale_PreparesAreaAndColumnPairToClearItsLanternCorridor()
        {
            var previousRandom=Random.state;
            bool previousMotion=JoinDog.App.AccessibilitySettings.ReducedMotion;
            var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
            try
            {
                yield return LoadGameplayScene();
                var bootstrap=Object.FindAnyObjectByType<GameBootstrap>();
                var progress=typeof(GameBootstrap).GetField("objectiveProgress",flags);
                foreach(int seed in new[]{30,34,3034})
                foreach(bool reduced in new[]{false,true})
                {
                    Random.InitState(seed);JoinDog.App.AccessibilitySettings.ReducedMotion=reduced;
                    bootstrap.currentLevel=30;bootstrap.StartNewMatch();bootstrap.uiController.SetMainMenuVisible(false);
                    yield return new WaitForSecondsRealtime(.3f);
                    if(seed==34 && !reduced) CaptureGameplayState("festival-finale-intro.png");
                    yield return new WaitForSecondsRealtime(4.5f);
                    var board=bootstrap.boardController;
                    var state=Object.FindAnyObjectByType<GameStateController>();
                    Assert.That(board.FindMatches().Count,Is.Zero);
                    Assert.That(board.RemainingObstacleCount,Is.EqualTo(22));
                    Assert.That(board.config.obstacleType,Is.EqualTo(CellObstacleType.Lantern));
                    Assert.That(board.config.obstacleDurability,Is.EqualTo(2));
                    Assert.That(board.config.boardShape,Is.EqualTo(BoardShape.Diamond));
                    var area=board.GetPieceAt(3,4);
                    Assert.That(board.TryGetSpecialCreationPreview(area,board.GetPieceAt(4,4),out var cell,out var special),Is.True);
                    Assert.That(cell,Is.EqualTo(new Vector2Int(4,4)));Assert.That(special,Is.EqualTo(PieceSpecialType.AreaBlast));
                    Assert.That(board.TrySwapAndFindMatches(area,board.GetPieceAt(4,4),out var matches),Is.True);
                    Assert.That(matches.Count,Is.EqualTo(5));
                    typeof(GameBootstrap).GetMethod("HandlePlayerMatch3Move",flags).Invoke(bootstrap,new object[]{matches});
                    float deadline=Time.unscaledTime+15f;
                    yield return null;
                    while(!state.CanSelectPieces() && Time.unscaledTime<deadline) yield return null;
                    Assert.That(state.CanSelectPieces(),Is.True,$"Area preparation: seed={seed}, reduced={reduced}, state={state.CurrentState}, cleared={progress.GetValue(bootstrap)}.");
                    Assert.That(board.GetPieceAt(4,2),Is.SameAs(area));
                    Assert.That(area.SpecialType,Is.EqualTo(PieceSpecialType.AreaBlast));
                    var ray=board.GetPieceAt(6,4);
                    Assert.That(board.TryGetSpecialCreationPreview(ray,board.GetPieceAt(5,4),out cell,out special),Is.True);
                    Assert.That(cell,Is.EqualTo(new Vector2Int(5,4)));Assert.That(special,Is.EqualTo(PieceSpecialType.ColumnBlast));
                    Assert.That(board.TrySwapAndFindMatches(ray,board.GetPieceAt(5,4),out matches),Is.True);
                    Assert.That(matches.Count,Is.EqualTo(4));
                    typeof(GameBootstrap).GetMethod("HandlePlayerMatch3Move",flags).Invoke(bootstrap,new object[]{matches});
                    deadline=Time.unscaledTime+15f;
                    yield return null;
                    while(!state.CanSelectPieces() && Time.unscaledTime<deadline) yield return null;
                    Assert.That(state.CanSelectPieces(),Is.True,$"Ray preparation: seed={seed}, reduced={reduced}, state={state.CurrentState}, cleared={progress.GetValue(bootstrap)}.");
                    Assert.That(board.GetPieceAt(4,2),Is.SameAs(area));
                    Assert.That(board.GetPieceAt(5,2),Is.SameAs(ray));
                    Assert.That(ray.SpecialType,Is.EqualTo(PieceSpecialType.ColumnBlast));
                    Assert.That((int)progress.GetValue(bootstrap),Is.LessThan(22));
                    if(seed==30) CaptureGameplayState("festival-finale-prepared"+(reduced?"-reduced":"")+".png");
                    Assert.That(BoardController.ClassifySpecialPair(area.SpecialType,ray.SpecialType),Is.EqualTo(SpecialComboKind.WideColumn));
                    Assert.That(board.TrySwapAndFindMatches(area,ray,out var pair),Is.True);
                    typeof(GameBootstrap).GetMethod("HandlePlayerMatch3Move",flags).Invoke(bootstrap,new object[]{pair});
                    Assert.That(board.RemainingObstacleCount,Is.Zero);
                    Assert.That((int)progress.GetValue(bootstrap),Is.GreaterThanOrEqualTo(22));
                    Assert.That((bool)typeof(GameBootstrap).GetMethod("IsCurrentObjectiveComplete",flags).Invoke(bootstrap,null),Is.True);
                    yield return new WaitForSecondsRealtime(.08f);
                    if(seed==30) CaptureGameplayState("festival-finale-pair"+(reduced?"-reduced":"")+".png");
                    bootstrap.StartNewMatch();Assert.That((int)progress.GetValue(bootstrap),Is.Zero);
                }
            }
            finally {Random.state=previousRandom;JoinDog.App.AccessibilitySettings.ReducedMotion=previousMotion;}
        }

        [UnityTest]
        public IEnumerator CoastFinale_PreparesCascadeMegaAndFrisbeePairForSandAndScore()
        {
            var previousRandom=Random.state;
            bool previousMotion=JoinDog.App.AccessibilitySettings.ReducedMotion;
            var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
            try
            {
                yield return LoadGameplayScene();
                var bootstrap=Object.FindAnyObjectByType<GameBootstrap>();
                var progress=typeof(GameBootstrap).GetField("objectiveProgress",flags);
                foreach(int seed in new[]{40,44,4044})
                foreach(bool reduced in new[]{false,true})
                {
                    Random.InitState(seed);JoinDog.App.AccessibilitySettings.ReducedMotion=reduced;
                    bootstrap.currentLevel=40;bootstrap.StartNewMatch();bootstrap.uiController.SetMainMenuVisible(false);
                    yield return new WaitForSecondsRealtime(.3f);
                    if(seed==44 && !reduced) CaptureGameplayState("coast-finale-intro.png");
                    yield return new WaitForSecondsRealtime(4.5f);
                    var board=bootstrap.boardController;
                    var state=Object.FindAnyObjectByType<GameStateController>();
                    Assert.That(board.FindMatches().Count,Is.Zero);
                    Assert.That(board.RemainingObstacleCount,Is.EqualTo(24));
                    Assert.That(board.config.obstacleType,Is.EqualTo(CellObstacleType.Sand));
                    Assert.That(board.config.obstacleDurability,Is.EqualTo(2));
                    Assert.That(board.config.boardShape,Is.EqualTo(BoardShape.Rounded));
                    var area=board.GetPieceAt(6,2);
                    Assert.That(board.TryGetSpecialCreationPreview(area,board.GetPieceAt(6,3),out var cell,out var special),Is.True);
                    Assert.That(cell,Is.EqualTo(new Vector2Int(6,3)));Assert.That(special,Is.EqualTo(PieceSpecialType.AreaBlast));
                    Assert.That(board.TrySwapAndFindMatches(area,board.GetPieceAt(6,3),out var matches),Is.True);
                    typeof(GameBootstrap).GetMethod("HandlePlayerMatch3Move",flags).Invoke(bootstrap,new object[]{matches});
                    float deadline=Time.unscaledTime+20f;
                    yield return null;
                    while(!state.CanSelectPieces() && Time.unscaledTime<deadline) yield return null;
                    Assert.That(state.CanSelectPieces(),Is.True,$"Cascade: seed={seed}, reduced={reduced}, state={state.CurrentState}.");
                    var mega=board.GetPieceAt(3,5);
                    Assert.That(mega.SpecialType,Is.EqualTo(PieceSpecialType.MegaBurst),$"seed={seed}, reduced={reduced}");
                    Assert.That(board.IsCoastTideUsed(7,3),Is.True);
                    Assert.That((int)progress.GetValue(bootstrap),Is.LessThan(24));
                    var comet=board.GetPieceAt(1,6);
                    Assert.That(comet.type,Is.EqualTo(PieceType.Frisbee));
                    Assert.That(board.TryGetSpecialCreationPreview(comet,board.GetPieceAt(2,6),out cell,out special),Is.True);
                    Assert.That(special,Is.EqualTo(PieceSpecialType.Comet));
                    Assert.That(board.TrySwapAndFindMatches(comet,board.GetPieceAt(2,6),out matches),Is.True);
                    typeof(GameBootstrap).GetMethod("HandlePlayerMatch3Move",flags).Invoke(bootstrap,new object[]{matches});
                    deadline=Time.unscaledTime+20f;
                    yield return null;
                    while(!state.CanSelectPieces() && Time.unscaledTime<deadline) yield return null;
                    Assert.That(state.CanSelectPieces(),Is.True,$"Comet: seed={seed}, reduced={reduced}, state={state.CurrentState}.");
                    Assert.That(comet.SpecialType,Is.EqualTo(PieceSpecialType.Comet));
                    Assert.That(mega.SpecialType,Is.EqualTo(PieceSpecialType.MegaBurst));
                    Assert.That(Mathf.Abs(mega.gridX-comet.gridX)+Mathf.Abs(mega.gridY-comet.gridY),Is.EqualTo(1));
                    Assert.That((bool)typeof(GameBootstrap).GetMethod("IsCurrentObjectiveComplete",flags).Invoke(bootstrap,null),Is.False);
                    if(seed==40) CaptureGameplayState("coast-finale-prepared"+(reduced?"-reduced":"")+".png");
                    Assert.That(board.TrySwapAndFindMatches(mega,comet,out var pair),Is.True);
                    typeof(GameBootstrap).GetMethod("HandlePlayerMatch3Move",flags).Invoke(bootstrap,new object[]{pair});
                    Assert.That(board.RemainingObstacleCount,Is.Zero);
                    Assert.That((int)progress.GetValue(bootstrap),Is.GreaterThanOrEqualTo(24));
                    Assert.That((bool)typeof(GameBootstrap).GetMethod("IsCurrentObjectiveComplete",flags).Invoke(bootstrap,null),Is.True,"Actual score must also satisfy the secondary goal.");
                    yield return new WaitForSecondsRealtime(.08f);
                    if(seed==40) CaptureGameplayState("coast-finale-pair"+(reduced?"-reduced":"")+".png");
                    bootstrap.StartNewMatch();Assert.That((int)progress.GetValue(bootstrap),Is.Zero);
                }
            }
            finally {Random.state=previousRandom;JoinDog.App.AccessibilitySettings.ReducedMotion=previousMotion;}
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator MountainFinale_WarmsDoubleAreaOpeningAndCompletesRealTimedMission()
        {
            var previousRandom=Random.state;
            bool previousMotion=JoinDog.App.AccessibilitySettings.ReducedMotion;
            var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
            try
            {
                yield return LoadGameplayScene();
                var bootstrap=Object.FindAnyObjectByType<GameBootstrap>();
                var completed=typeof(GameBootstrap).GetMethod("IsCurrentObjectiveComplete",flags);
                var move=typeof(GameBootstrap).GetMethod("HandlePlayerMatch3Move",flags);
                foreach(int seed in new[]{50,54,5054})
                foreach(bool reduced in new[]{false,true})
                {
                    Random.InitState(seed);JoinDog.App.AccessibilitySettings.ReducedMotion=reduced;
                    bootstrap.currentLevel=50;bootstrap.StartNewMatch();bootstrap.uiController.SetMainMenuVisible(false);
                    yield return new WaitForSecondsRealtime(.3f);
                    if(seed==54 && !reduced) CaptureGameplayState("mountain-finale-intro.png");
                    yield return new WaitForSecondsRealtime(4.5f);
                    var board=bootstrap.boardController;
                    var state=Object.FindAnyObjectByType<GameStateController>();
                    var timer=bootstrap.gameTimer;
                    Assert.That(board.FindMatches().Count,Is.Zero);
                    Assert.That(board.RemainingObstacleCount,Is.EqualTo(26));
                    Assert.That(board.config.obstacleType,Is.EqualTo(CellObstacleType.Ice));
                    Assert.That(board.config.obstacleDurability,Is.EqualTo(3));
                    Assert.That(board.config.boardShape,Is.EqualTo(BoardShape.Diamond));
                    var first=board.GetPieceAt(3,4);
                    Assert.That(board.TrySwapAndFindMatches(first,board.GetPieceAt(4,4),out var matches),Is.True);
                    move.Invoke(bootstrap,new object[]{matches});
                    float deadline=Time.unscaledTime+20f;yield return null;
                    while(!state.CanSelectPieces() && Time.unscaledTime<deadline) yield return null;
                    Assert.That(state.CanSelectPieces(),Is.True,$"First L seed={seed}, reduced={reduced}.");
                    Assert.That(first.SpecialType,Is.EqualTo(PieceSpecialType.AreaBlast));
                    Assert.That(board.IsMountainWarmthUsed(5,4),Is.True,
                        "Only the first useful warmth cell is collected in one resolution.");
                    var second=board.GetPieceAt(5,5);
                    Assert.That(board.TryGetSpecialCreationPreview(second,board.GetPieceAt(5,4),out var cell,out var special),Is.True);
                    Assert.That(special,Is.EqualTo(PieceSpecialType.AreaBlast));
                    Assert.That(board.TrySwapAndFindMatches(second,board.GetPieceAt(5,4),out matches),Is.True);
                    move.Invoke(bootstrap,new object[]{matches});
                    deadline=Time.unscaledTime+20f;yield return null;
                    while(!state.CanSelectPieces() && Time.unscaledTime<deadline) yield return null;
                    Assert.That(state.CanSelectPieces(),Is.True,$"Second L seed={seed}, reduced={reduced}.");
                    Assert.That(second.SpecialType,Is.EqualTo(PieceSpecialType.AreaBlast));
                    Assert.That(Mathf.Abs(first.gridX-second.gridX)+Mathf.Abs(first.gridY-second.gridY),Is.EqualTo(1));
                    if(seed==50) CaptureGameplayState("mountain-finale-prepared"+(reduced?"-reduced":"")+".png");
                    Assert.That(board.TrySwapAndFindMatches(first,second,out matches),Is.True);
                    move.Invoke(bootstrap,new object[]{matches});
                    yield return new WaitForSecondsRealtime(.08f);
                    if(seed==50) CaptureGameplayState("mountain-finale-double-area"+(reduced?"-reduced":"")+".png");
                    // Follow actual available moves on the random refill, with the
                    // existing obstacle-aware hint policy. Keep the real clock running.
                    int turns=0;deadline=Time.unscaledTime+180f;
                    while(!(bool)completed.Invoke(bootstrap,null) && Time.unscaledTime<deadline && turns<100)
                    {
                        if(!state.CanSelectPieces()) {yield return null;continue;}
                        // Allow actual decision time rather than selecting the next
                        // move before the timer gets an Update after each cascade.
                        float beforeDecision=timer.RemainingTime;
                        yield return new WaitForSecondsRealtime(1f);
                        if(turns==0) Assert.That(timer.RemainingTime,Is.LessThan(beforeDecision),
                            "The real gameplay clock must tick while choosing a move.");
                        Assert.That(state.CanSelectPieces(),Is.True,$"Decision seed={seed}, reduced={reduced}, state={state.CurrentState}");
                        Assert.That(timer.RemainingTime,Is.GreaterThan(0f));
                        Assert.That(board.TryFindHintMoveForObjective(PieceType.None,board.RemainingObstacleCount>0,out var a,out var b),Is.True);
                        Assert.That(board.TrySwapAndFindMatches(a,b,out matches),Is.True);
                        move.Invoke(bootstrap,new object[]{matches});turns++;yield return null;
                    }
                    Assert.That((bool)completed.Invoke(bootstrap,null),Is.True,$"Timed mission: seed={seed}, reduced={reduced}, turns={turns}, remainingIce={board.RemainingObstacleCount}, score={bootstrap.scoreController.CurrentScore}, time={timer.RemainingTime}, state={state.CurrentState}.");
                    Assert.That(board.RemainingObstacleCount,Is.Zero);
                    Debug.Log($"Mountain finale seed={seed}, reduced={reduced}, followupMoves={turns}, score={bootstrap.scoreController.CurrentScore}, time={timer.RemainingTime:F1}");
                    bootstrap.StartNewMatch();
                }
            }
            finally {Random.state=previousRandom;JoinDog.App.AccessibilitySettings.ReducedMotion=previousMotion;}
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator AuroraFinale_PreparesPrismCrossAndCompletesRealTimedMission()
        {
            var previousRandom=Random.state;
            bool previousMotion=JoinDog.App.AccessibilitySettings.ReducedMotion;
            var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
            try
            {
                yield return LoadGameplayScene();
                var bootstrap=Object.FindAnyObjectByType<GameBootstrap>();
                var completed=typeof(GameBootstrap).GetMethod("IsCurrentObjectiveComplete",flags);
                var move=typeof(GameBootstrap).GetMethod("HandlePlayerMatch3Move",flags);
                foreach(int seed in new[]{60,64,6064})
                foreach(bool reduced in new[]{false,true})
                {
                    Random.InitState(seed);JoinDog.App.AccessibilitySettings.ReducedMotion=reduced;
                    bootstrap.currentLevel=60;bootstrap.StartNewMatch();bootstrap.uiController.SetMainMenuVisible(false);
                    yield return new WaitForSecondsRealtime(.3f);
                    // The zone banner precedes the objective card by .9 seconds.
                    // Capture the actual advice card without extending the wait.
                    yield return new WaitForSecondsRealtime(1.3f);
                    if(seed==64 && !reduced)
                    {
                        Assert.That(bootstrap.uiController.IsObjectiveBriefVisible,Is.True);
                        CaptureGameplayState("aurora-finale-intro.png");
                    }
                    yield return new WaitForSecondsRealtime(3.2f);
                    var board=bootstrap.boardController;
                    var state=Object.FindAnyObjectByType<GameStateController>();
                    var timer=bootstrap.gameTimer;
                    Assert.That(board.FindMatches().Count,Is.Zero);
                    Assert.That(board.RemainingObstacleCount,Is.EqualTo(30));
                    Assert.That(board.config.obstacleType,Is.EqualTo(CellObstacleType.Lantern));
                    Assert.That(board.config.obstacleDurability,Is.EqualTo(3));
                    Assert.That(board.config.boardShape,Is.EqualTo(BoardShape.Diamond));
                    var first=board.GetPieceAt(3,3);
                    CaptureGameplayState("aurora-finale-opening-audit.png");
                    Assert.That(first.IsSpecial,Is.False,"The opening must start with ordinary pieces.");
                    Assert.That(board.TryGetSpecialCreationPreview(first,board.GetPieceAt(3,4),out var firstCell,out var firstKind),Is.True);
                    Assert.That(firstKind,Is.EqualTo(PieceSpecialType.RowBlast),"The authored first swap must create a four-piece ray.");
                    Assert.That(board.TrySwapAndFindMatches(first,board.GetPieceAt(3,4),out var matches),Is.True);
                    move.Invoke(bootstrap,new object[]{matches});
                    Assert.That(first.SpecialType,Is.EqualTo(PieceSpecialType.RowBlast),"The first ray must be created before the refill.");
                    float deadline=Time.unscaledTime+20f;yield return null;
                    while(!state.CanSelectPieces() && Time.unscaledTime<deadline) yield return null;
                    Assert.That(state.CanSelectPieces(),Is.True,$"First ray seed={seed}, reduced={reduced}.");
                    Assert.That(first.SpecialType,Is.EqualTo(PieceSpecialType.RowBlast));
                    Assert.That(board.IsAuroraPrismUsed(2,4),Is.True,
                        "The first four-piece match lights the prism in column two.");
                    var second=board.GetPieceAt(5,5);
                    Assert.That(board.TryGetSpecialCreationPreview(second,board.GetPieceAt(4,5),out var cell,out var special),Is.True);
                    Assert.That(special,Is.EqualTo(PieceSpecialType.ColumnBlast));
                    Assert.That(board.TrySwapAndFindMatches(second,board.GetPieceAt(4,5),out matches),Is.True);
                    move.Invoke(bootstrap,new object[]{matches});
                    deadline=Time.unscaledTime+20f;yield return null;
                    while(!state.CanSelectPieces() && Time.unscaledTime<deadline) yield return null;
                    Assert.That(state.CanSelectPieces(),Is.True,$"Second ray seed={seed}, reduced={reduced}.");
                    Assert.That(second.SpecialType,Is.EqualTo(PieceSpecialType.ColumnBlast));
                    Assert.That(board.IsAuroraPrismUsed(4,6),Is.True);
                    Assert.That(BoardController.ClassifySpecialPair(first.SpecialType,second.SpecialType),
                        Is.EqualTo(SpecialComboKind.CrossBlast));
                    Assert.That((bool)completed.Invoke(bootstrap,null),Is.False,
                        "Preparing the two rays does not complete thirty triple-layer lanterns and the score goal.");
                    Assert.That(Mathf.Abs(first.gridX-second.gridX)+Mathf.Abs(first.gridY-second.gridY),Is.EqualTo(1));
                    if(seed==60) CaptureGameplayState("aurora-finale-prepared"+(reduced?"-reduced":"")+".png");
                    Assert.That(board.TrySwapAndFindMatches(first,second,out matches),Is.True);
                    move.Invoke(bootstrap,new object[]{matches});
                    yield return new WaitForSecondsRealtime(.08f);
                    if(seed==60) CaptureGameplayState("aurora-finale-cross-lines"+(reduced?"-reduced":"")+".png");
                    // Follow actual available moves on the random refill, with the
                    // existing obstacle-aware hint policy. Keep the real clock running.
                    int turns=0;deadline=Time.unscaledTime+180f;
                    while(!(bool)completed.Invoke(bootstrap,null) && Time.unscaledTime<deadline && turns<100)
                    {
                        if(!state.CanSelectPieces()) {yield return null;continue;}
                        // Allow actual decision time rather than selecting the next
                        // move before the timer gets an Update after each cascade.
                        float beforeDecision=timer.RemainingTime;
                        yield return new WaitForSecondsRealtime(1f);
                        if(turns==0) Assert.That(timer.RemainingTime,Is.LessThan(beforeDecision),
                            "The real gameplay clock must tick while choosing a move.");
                        Assert.That(state.CanSelectPieces(),Is.True,$"Decision seed={seed}, reduced={reduced}, state={state.CurrentState}");
                        Assert.That(timer.RemainingTime,Is.GreaterThan(0f));
                        Assert.That(board.TryFindHintMoveForObjective(PieceType.None,board.RemainingObstacleCount>0,out var a,out var b),Is.True);
                        Assert.That(board.TrySwapAndFindMatches(a,b,out matches),Is.True);
                        move.Invoke(bootstrap,new object[]{matches});turns++;yield return null;
                    }
                    Assert.That((bool)completed.Invoke(bootstrap,null),Is.True,$"Timed mission: seed={seed}, reduced={reduced}, turns={turns}, remainingLanterns={board.RemainingObstacleCount}, score={bootstrap.scoreController.CurrentScore}, time={timer.RemainingTime}, state={state.CurrentState}.");
                    Assert.That(board.RemainingObstacleCount,Is.Zero);
                    Assert.That(bootstrap.scoreController.CurrentScore,Is.GreaterThanOrEqualTo(126054));
                    Debug.Log($"Aurora finale seed={seed}, reduced={reduced}, followupMoves={turns}, score={bootstrap.scoreController.CurrentScore}, time={timer.RemainingTime:F1}");
                    bootstrap.StartNewMatch();
                }
            }
            finally {Random.state=previousRandom;JoinDog.App.AccessibilitySettings.ReducedMotion=previousMotion;}
        }

        [UnityTest]
        public IEnumerator CompanionAssist_PreservesPreparedSpecialsAndExistingResolution()
        {
            var previousRandom = Random.state;
            bool previousMotion = JoinDog.App.AccessibilitySettings.ReducedMotion;
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            try
            {
                SceneManager.LoadScene("Gameplay", LoadSceneMode.Single);
                yield return null; yield return null;
                var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
                foreach (bool reduced in new[]{false,true})
                {
                    JoinDog.App.AccessibilitySettings.ReducedMotion = reduced;
                    bootstrap.currentLevel = 1; bootstrap.StartNewMatch();
                    bootstrap.uiController.SetMainMenuVisible(false);
                    yield return new WaitForSecondsRealtime(4.5f);
                    var board = bootstrap.boardController;
                    Random.InitState(4018);
                    var selectionState = Random.state;
                    var target = board.GetRandomPiece();
                    Random.state = selectionState;
                    var row = board.GetRowPieces(target.gridY);
                    Assert.That(row.Count,Is.GreaterThanOrEqualTo(4));
                    var prepared = row[0]; prepared.SetSpecial(PieceSpecialType.MegaBurst);
                    var fresh = row[1]; fresh.SetSpecial(PieceSpecialType.Comet);
                    var activated = row[2]; activated.SetSpecial(PieceSpecialType.RowBlast);
                    var removal = new System.Collections.Generic.List<PieceView>{activated};
                    typeof(GameBootstrap).GetField("cascadeDepth",flags).SetValue(bootstrap,0);
                    typeof(GameBootstrap).GetField("companionCharge",flags).SetValue(bootstrap,3);
                    typeof(GameBootstrap).GetMethod("TryActivateCompanionAssist",flags)
                        .Invoke(bootstrap,new object[]{removal,true});
                    Assert.That(removal.Contains(prepared),Is.False,"Help must preserve a prepared power for the next move.");
                    Assert.That(removal.Contains(fresh),Is.False,"A newly created special must survive the same resolution's help.");
                    Assert.That(removal,Does.Contain(activated),"Already activated powers remain in the original resolution.");
                    foreach(var piece in row)
                        if(!piece.IsSpecial) Assert.That(removal,Does.Contain(piece));
                    Assert.That(removal.Count,Is.EqualTo(row.Count-2));
                    Assert.That((int)typeof(GameBootstrap).GetField("companionCharge",flags).GetValue(bootstrap),Is.Zero);
                    Assert.That(board.GetPieceAt(prepared.gridX,prepared.gridY),Is.SameAs(prepared));
                    yield return new WaitForSecondsRealtime(.08f);
                    CaptureGameplayState("companion-preserves-specials"+(reduced?"-reduced":"")+".png");
                    bootstrap.StartNewMatch();
                }
            }
            finally {Random.state=previousRandom;JoinDog.App.AccessibilitySettings.ReducedMotion=previousMotion;}
        }

        [UnityTest]
        public IEnumerator CompanionGarden_RewardsOnlyCreationOncePerPlayableFlower()
        {
            var previousRandom = Random.state;
            bool previousMotion = JoinDog.App.AccessibilitySettings.ReducedMotion;
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            GameBootstrap bootstrap = null;
            try
            {
                // This fixture does not overwrite saved campaign progress.
                SceneManager.LoadScene("Gameplay", LoadSceneMode.Single);
                yield return null; yield return null;
                bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
                foreach (bool inside in new[]{false,true})
                {
                    Random.InitState(136);
                    JoinDog.App.AccessibilitySettings.ReducedMotion = inside;
                    bootstrap.currentLevel = 8; bootstrap.StartNewMatch();
                    bootstrap.uiController.SetMainMenuVisible(false);
                    yield return new WaitForSecondsRealtime(.3f);
                    if (inside) CaptureGameplayState("garden-intro-8.png");
                    yield return new WaitForSecondsRealtime(4.5f);
                    var board = bootstrap.boardController;
                    // Duplicates, malformed coordinates and blocked cells must not grant bonuses.
                    board.config.companionGardenCells = new[]{"3,5","3,5","0,0","bad","999,999"};
                    board.GetComponent<AdaptiveBoardView>().Rebuild(board);
                    yield return null; // Rebuild retires the previous visual root at end of frame.
                    if (inside) CaptureGameplayState("garden-board-8.png");
                    Assert.That(board.IsCompanionGardenCell(0,0),Is.False);
                    Assert.That(board.IsCompanionGardenCell(999,999),Is.False);
                    int row = inside ? 5 : 2;
                    var created = board.GetPieceAt(3,row-1);
                    Assert.That(board.TryHarvestCompanionGarden(created),Is.False,"Ordinary pieces cannot harvest.");
                    Assert.That(board.TrySwapAndFindMatches(created,board.GetPieceAt(3,row),out var matches),Is.True);
                    typeof(GameBootstrap).GetMethod("HandleChainCompleted",flags).Invoke(bootstrap,new object[]{matches});
                    Assert.That(created.SpecialType,Is.EqualTo(PieceSpecialType.ColorBurst));
                    Assert.That((int)typeof(GameBootstrap).GetField("companionCharge",flags).GetValue(bootstrap),
                        Is.EqualTo(inside ? 2 : 1),"Flower grants exactly one extra unit, independently of movement setting.");
                    Assert.That(board.IsCompanionGardenHarvested(3,row),Is.EqualTo(inside));
                    Assert.That(board.TryHarvestCompanionGarden(created),Is.False,"No repeated reward or outside reward.");
                    var marker = board.transform.Find("[AdaptiveBoardVisual]/Cell_3_5/GardenFlower_3_5");
                    Assert.That(marker,Is.Not.Null);
                    Assert.That(marker.GetComponent<Collider2D>(),Is.Null);
                    Assert.That(marker.GetComponent<SpriteRenderer>().color.a,Is.EqualTo(inside ? .65f : 1f).Within(.01f));
                    if (inside)
                    {
                        yield return new WaitForSecondsRealtime(.18f);
                        CaptureGameplayState("garden-harvest-8.png");
                    }
                }
                bootstrap.StartNewMatch();
                Assert.That(bootstrap.boardController.IsCompanionGardenHarvested(3,5),Is.False);
                bootstrap.currentLevel = 1; bootstrap.StartNewMatch();
                Assert.That(bootstrap.boardController.config.companionGardenCells,Is.Null.Or.Empty);
            }
            finally
            {
                Random.state = previousRandom;
                JoinDog.App.AccessibilitySettings.ReducedMotion = previousMotion;
                if (bootstrap != null) {bootstrap.currentLevel=1;bootstrap.StartNewMatch();}
            }
        }

        [UnityTest]
        public IEnumerator VineShelter_PreventsOneTurnOfGrowthWithoutSpendingOtherLeaves()
        {
            var previousRandom=Random.state;
            bool previousMotion=JoinDog.App.AccessibilitySettings.ReducedMotion;
            GameBootstrap bootstrap=null;
            var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
            try
            {
                SceneManager.LoadScene("Gameplay",LoadSceneMode.Single);
                yield return null;yield return null;
                bootstrap=Object.FindAnyObjectByType<GameBootstrap>();
                Random.InitState(2026);
                JoinDog.App.AccessibilitySettings.ReducedMotion=true;
                bootstrap.currentLevel=12;bootstrap.StartNewMatch();bootstrap.uiController.SetMainMenuVisible(false);
                yield return new WaitForSecondsRealtime(.3f);
                CaptureGameplayState("shelter-intro-12.png");
                yield return new WaitForSecondsRealtime(4.5f);
                var board=bootstrap.boardController;
                board.config.vineShelterCells=new[]{"1,1","2,1","1,1","0,0","bad"};
                board.GetComponent<AdaptiveBoardView>().Rebuild(board);yield return null;
                Assert.That(board.IsVineShelterCell(0,0),Is.False);
                var matches=new System.Collections.Generic.List<PieceView>();
                // Four in a row, anchor at the far end: both leaf pieces are really removed.
                for(int x=4;x>=1;x--) {var piece=board.GetPieceAt(x,1);piece.type=PieceType.Bone;matches.Add(piece);}
                RefreshOrdinaryFixtureArtwork(board);
                CaptureGameplayState("shelter-board-12.png");
                int obstacles=board.RemainingObstacleCount;
                typeof(GameBootstrap).GetMethod("HandlePlayerMatch3Move",flags).Invoke(bootstrap,new object[]{matches});
                Assert.That((bool)typeof(GameBootstrap).GetField("vineShelterThisTurn",flags).GetValue(bootstrap),Is.True);
                Assert.That(board.IsVineShelterCollected(2,1),Is.True);
                Assert.That(board.IsVineShelterCollected(1,1),Is.False,"One turn only spends one leaf.");
                Assert.That((bool)typeof(GameBootstrap).GetMethod("TrySpreadUnprotectedVines",flags).Invoke(bootstrap,null),Is.False);
                Assert.That(board.RemainingObstacleCount,Is.EqualTo(obstacles));
                var marker=board.transform.Find("[AdaptiveBoardVisual]/Cell_2_1/ShelterLeaf_2_1");
                Assert.That(marker.GetComponent<SpriteRenderer>().color.a,Is.EqualTo(.55f).Within(.01f));
                Assert.That(marker.GetComponent<Collider2D>(),Is.Null);
                yield return new WaitForSecondsRealtime(.15f);CaptureGameplayState("shelter-protected-12.png");
                float deadline=Time.unscaledTime+15f;
                while(!bootstrap.stateController.CanSelectPieces() && Time.unscaledTime<deadline) yield return null;
                Assert.That(bootstrap.stateController.CanSelectPieces(),Is.True);
                Assert.That((bool)typeof(GameBootstrap).GetField("vineShelterThisTurn",flags).GetValue(bootstrap),Is.True,
                    "Protection lasts through gravity and every cascade of the same turn.");
                Assert.That(board.RemainingObstacleCount,Is.LessThanOrEqualTo(obstacles));
                bootstrap.StartNewMatch();
                Assert.That(board.IsVineShelterCollected(2,1),Is.False);
                Assert.That((bool)typeof(GameBootstrap).GetField("vineShelterThisTurn",flags).GetValue(bootstrap),Is.False);
                obstacles=board.RemainingObstacleCount;
                Assert.That((bool)typeof(GameBootstrap).GetMethod("TrySpreadUnprotectedVines",flags).Invoke(bootstrap,null),Is.True);
                Assert.That(board.RemainingObstacleCount,Is.EqualTo(obstacles+1),"Unprotected vines retain their normal growth.");
                var leaf=board.GetPieceAt(1,1);
                Assert.That(board.TryCollectVineShelter(new[]{leaf},new System.Collections.Generic.List<PieceView>()),Is.False,
                    "A retained or merely visited leaf does not get spent.");
                yield return new WaitForSecondsRealtime(4.5f);
                board.config.vineShelterCells=new[]{"2,4"};
                matches.Clear();
                for(int x=1;x<=4;x++) {var piece=board.GetPieceAt(x,4);piece.type=PieceType.Bone;matches.Add(piece);}
                typeof(GameBootstrap).GetMethod("HandlePlayerMatch3Move",flags).Invoke(bootstrap,new object[]{matches});
                Assert.That((int)typeof(GameBootstrap).GetField("obstaclesClearedThisTurn",flags).GetValue(bootstrap),Is.GreaterThan(0));
                Assert.That(board.IsVineShelterCollected(2,4),Is.False,"Breaking a vine already prevents growth: save the leaf.");
                bootstrap.currentLevel=1;bootstrap.StartNewMatch();
                Assert.That(board.config.vineShelterCells,Is.Null.Or.Empty);
            }
            finally
            {
                Random.state=previousRandom;JoinDog.App.AccessibilitySettings.ReducedMotion=previousMotion;
                if(bootstrap!=null) {bootstrap.currentLevel=1;bootstrap.StartNewMatch();}
            }
        }

        [UnityTest]
        public IEnumerator FestivalBells_GrantBoundedTimeOnlyForActivatedSpecialsWithRoom()
        {
            var previousRandom=Random.state;
            bool previousMotion=JoinDog.App.AccessibilitySettings.ReducedMotion;
            GameBootstrap bootstrap=null;
            var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
            try
            {
                SceneManager.LoadScene("Gameplay",LoadSceneMode.Single);yield return null;yield return null;
                bootstrap=Object.FindAnyObjectByType<GameBootstrap>();
                bootstrap.currentLevel=24;bootstrap.StartNewMatch();bootstrap.uiController.SetMainMenuVisible(false);
                yield return new WaitForSecondsRealtime(.3f);CaptureGameplayState("festival-bell-intro-24.png");
                yield return new WaitForSecondsRealtime(4.5f);
                var board=bootstrap.boardController;var timer=bootstrap.gameTimer;
                var timeField=typeof(GameTimer).GetField("<RemainingTime>k__BackingField",flags);
                var grant=typeof(GameBootstrap).GetMethod("TryGrantFestivalBellTime",flags);
                foreach(bool reduced in new[]{false,true})
                {
                    JoinDog.App.AccessibilitySettings.ReducedMotion=reduced;
                    board.config.festivalBellCells=new[]{"3,3","6,3","3,3","0,9","999,999","invalid"};
                    board.InitializeBoard();yield return null;
                    timer.StartTimer(60f);timer.SetPaused(true);
                    Assert.That(board.IsFestivalBellCell(0,9),Is.False);
                    Assert.That(board.IsFestivalBellCell(999,999),Is.False);
                    var ordinary=new MatchResolution();ordinary.PiecesToRemove.Add(board.GetPieceAt(3,3));
                    timeField.SetValue(timer,40f);
                    Assert.That((float)grant.Invoke(bootstrap,new object[]{ordinary}),Is.Zero);
                    Assert.That(board.IsFestivalBellRung(3,3),Is.False);
                    var special=board.GetPieceAt(4,3);special.SetSpecial(PieceSpecialType.RowBlast);
                    var resolution=board.BuildMatchResolution(new System.Collections.Generic.List<PieceView>{special});
                    Assert.That(resolution.SpecialsActivated,Is.GreaterThan(0));
                    timeField.SetValue(timer,59f);
                    Assert.That((float)grant.Invoke(bootstrap,new object[]{resolution}),Is.Zero,"Do not spend a bell for a partial grant.");
                    Assert.That(board.IsFestivalBellRung(3,3),Is.False);
                    timer.StopTimer();
                    Assert.That((float)grant.Invoke(bootstrap,new object[]{resolution}),Is.Zero,"Stopped clocks cannot spend bells.");
                    timer.StartTimer(60f);timer.SetPaused(true);timeField.SetValue(timer,40f);
                    typeof(GameBootstrap).GetField("victoryPending",flags).SetValue(bootstrap,true);
                    Assert.That((float)grant.Invoke(bootstrap,new object[]{resolution}),Is.Zero,"No rewards during final bonus play.");
                    typeof(GameBootstrap).GetField("victoryPending",flags).SetValue(bootstrap,false);
                    timeField.SetValue(timer,40f);
                    Assert.That((float)grant.Invoke(bootstrap,new object[]{resolution}),Is.EqualTo(2f));
                    Assert.That(timer.RemainingTime,Is.EqualTo(42f));
                    int spent=(board.IsFestivalBellRung(3,3)?1:0)+(board.IsFestivalBellRung(6,3)?1:0);
                    Assert.That(spent,Is.EqualTo(1),"Only one bell per activated resolution.");
                    Assert.That((float)grant.Invoke(bootstrap,new object[]{resolution}),Is.EqualTo(2f));
                    Assert.That((float)grant.Invoke(bootstrap,new object[]{resolution}),Is.Zero);
                    Assert.That(timer.RemainingTime,Is.EqualTo(44f),"Two authored bells cap this match's reward at four seconds.");
                    var marker=board.transform.Find("[AdaptiveBoardVisual]/Cell_3_3/FestivalBell_3_3");
                    Assert.That(marker.GetComponent<Collider2D>(),Is.Null);
                    Assert.That(marker.GetComponent<SpriteRenderer>().color.a,Is.EqualTo(.55f).Within(.01f));
                    CaptureGameplayState("festival-bell-used-24.png");
                    board.InitializeBoard();yield return null;
                    special=board.GetPieceAt(4,3);special.SetSpecial(PieceSpecialType.RowBlast);
                    timer.StartTimer(60f);timer.SetPaused(true);timeField.SetValue(timer,40f);
                    typeof(GameBootstrap).GetMethod("HandleChainCompleted",flags).Invoke(bootstrap,
                        new object[]{new System.Collections.Generic.List<PieceView>{special}});
                    Assert.That(timer.RemainingTime,Is.EqualTo(42f),"Real resolution awards the bell before gravity.");
                    yield return new WaitForSecondsRealtime(.15f);CaptureGameplayState("festival-bell-active-24.png");
                    bootstrap.StartNewMatch();yield return new WaitForSecondsRealtime(4.5f);
                }
                bootstrap.currentLevel=1;bootstrap.StartNewMatch();
                Assert.That(board.config.festivalBellCells,Is.Null.Or.Empty);
            }
            finally
            {
                Random.state=previousRandom;JoinDog.App.AccessibilitySettings.ReducedMotion=previousMotion;
                if(bootstrap!=null) {bootstrap.currentLevel=1;bootstrap.StartNewMatch();}
            }
        }

        [UnityTest]
        public IEnumerator CoastTides_DamageOnlyAdditionalSandInTheirRowOnce()
        {
            var previousRandom=Random.state;
            bool previousMotion=JoinDog.App.AccessibilitySettings.ReducedMotion;
            GameBootstrap bootstrap=null;
            var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
            try
            {
                SceneManager.LoadScene("Gameplay",LoadSceneMode.Single);yield return null;yield return null;
                bootstrap=Object.FindAnyObjectByType<GameBootstrap>();
                bootstrap.currentLevel=33;bootstrap.StartNewMatch();bootstrap.uiController.SetMainMenuVisible(false);
                yield return new WaitForSecondsRealtime(.3f);CaptureGameplayState("coast-tide-intro-33.png");
                yield return new WaitForSecondsRealtime(4.5f);
                var board=bootstrap.boardController;
                board.config.obstacleType=CellObstacleType.Sand;board.config.obstacleCount=5;board.config.obstacleDurability=3;
                board.config.obstacleCells=new[]{"2,3","5,3","7,3","7,4","6,6"};
                board.config.coastTideCells=new[]{"2,3","6,6","2,3","0,0","bad"};
                foreach(bool reduced in new[]{false,true})
                {
                    JoinDog.App.AccessibilitySettings.ReducedMotion=reduced;
                    board.InitializeBoard();yield return null;
                    typeof(GameBootstrap).GetMethod("RefreshSecondaryHazardUI",flags).Invoke(bootstrap,null);
                    Assert.That(board.IsCoastTideCell(0,0),Is.False);
                    for(int x=0;x<board.Columns;x++) for(int y=0;y<board.Rows;y++)
                    {
                        var piece=board.GetPieceAt(x,y);if(piece!=null) {piece.type=(PieceType)((x+y)%5);piece.SetSpecial(PieceSpecialType.None);}
                    }
                    var matches=new System.Collections.Generic.List<PieceView>();
                    for(int x=1;x<=3;x++) {var piece=board.GetPieceAt(x,3);piece.type=PieceType.Bone;matches.Add(piece);}
                    board.GetPieceAt(0,3).type=PieceType.Food;board.GetPieceAt(4,3).type=PieceType.Food;
                    board.GetPieceAt(2,2).type=PieceType.Food;board.GetPieceAt(2,4).type=PieceType.Food;
                    var untouched=board.GetPieceAt(5,3);
                    var health=(int[,])typeof(BoardController).GetField("obstacleHealth",flags).GetValue(board);
                    RefreshOrdinaryFixtureArtwork(board);
                    CaptureGameplayState("coast-tide-board-33.png");
                    typeof(GameBootstrap).GetMethod("HandlePlayerMatch3Move",flags).Invoke(bootstrap,new object[]{matches});
                    Assert.That(board.IsCoastTideUsed(2,3),Is.True);
                    Assert.That(board.IsCoastTideUsed(6,6),Is.False);
                    Assert.That(health[2,3],Is.EqualTo(2),"Normal hit is not doubled by the wave.");
                    Assert.That(health[5,3],Is.EqualTo(2));Assert.That(health[7,3],Is.EqualTo(2));
                    Assert.That(health[7,4],Is.EqualTo(3),"Wave does not extend to adjacent rows.");
                    Assert.That(health[6,6],Is.EqualTo(3));
                    Assert.That(board.GetPieceAt(5,3),Is.SameAs(untouched),"The wave damages sand, not ordinary pieces.");
                    Assert.That(board.TryCollectCoastTide(matches,matches),Is.Empty,"A used wave cannot fire again.");
                    var marker=board.transform.Find("[AdaptiveBoardVisual]/Cell_2_3/CoastTide_2_3");
                    Assert.That(marker.GetComponent<SpriteRenderer>().color.a,Is.EqualTo(.55f).Within(.01f));
                    Assert.That(marker.GetComponent<Collider2D>(),Is.Null);
                    yield return new WaitForSecondsRealtime(.15f);CaptureGameplayState("coast-tide-active-33.png");
                    bootstrap.StartNewMatch();yield return new WaitForSecondsRealtime(4.5f);
                    board.config.obstacleType=CellObstacleType.Sand;board.config.obstacleCount=5;board.config.obstacleDurability=3;
                    board.config.obstacleCells=new[]{"2,3","5,3","7,3","7,4","6,6"};
                    board.config.coastTideCells=new[]{"2,3","6,6"};
                }
                board.InitializeBoard();yield return null;
                var tide=board.GetPieceAt(2,3);
                Assert.That(board.TryCollectCoastTide(new[]{tide},new System.Collections.Generic.List<PieceView>()),Is.Empty);
                Assert.That(board.IsCoastTideUsed(2,3),Is.False,"A retained piece cannot collect its wave.");
                var allInRow=board.GetRowPieces(3);
                Assert.That(board.TryCollectCoastTide(new[]{tide},allInRow),Is.Empty);
                Assert.That(board.IsCoastTideUsed(2,3),Is.False,"No additional reachable sand means save the wave.");
                bootstrap.currentLevel=1;bootstrap.StartNewMatch();
                Assert.That(board.config.coastTideCells,Is.Null.Or.Empty);
            }
            finally
            {
                Random.state=previousRandom;JoinDog.App.AccessibilitySettings.ReducedMotion=previousMotion;
                if(bootstrap!=null) {bootstrap.currentLevel=1;bootstrap.StartNewMatch();}
            }
        }

        [UnityTest]
        public IEnumerator RubyGeysers_ActivateOnlyWithSpecialsAndNeverDoubleSandDamage()
        {
            var previousRandom=Random.state;
            bool previousMotion=JoinDog.App.AccessibilitySettings.ReducedMotion;
            GameBootstrap bootstrap=null;
            var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
            try
            {
                SceneManager.LoadScene("Gameplay",LoadSceneMode.Single);yield return null;yield return null;
                bootstrap=Object.FindAnyObjectByType<GameBootstrap>();bootstrap.currentLevel=83;
                bootstrap.StartNewMatch();bootstrap.uiController.SetMainMenuVisible(false);
                yield return new WaitForSecondsRealtime(.3f);CaptureGameplayState("ruby-geyser-intro-83.png");
                yield return new WaitForSecondsRealtime(4.5f);
                var board=bootstrap.boardController;Assert.That(board.config.rubyGeyserCells,Is.Not.Null.And.Not.Empty);
                foreach(bool reduced in new[]{false,true})
                {
                    JoinDog.App.AccessibilitySettings.ReducedMotion=reduced;
                    board.config.obstacleType=CellObstacleType.Sand;board.config.obstacleCount=6;board.config.obstacleDurability=3;
                    board.config.obstacleCells=new[]{"4,3","4,4","4,1","4,5","5,5","4,6"};
                    board.config.rubyGeyserCells=new[]{"4,3","4,6","4,3","999,999","bad"};
                    board.InitializeBoard();yield return null;
                    typeof(GameBootstrap).GetMethod("RefreshSecondaryHazardUI",flags).Invoke(bootstrap,null);
                    Assert.That(board.IsRubyGeyserCell(999,999),Is.False);
                    for(int x=0;x<board.Columns;x++) for(int y=0;y<board.Rows;y++)
                    {
                        var piece=board.GetPieceAt(x,y);if(piece!=null) {piece.type=(PieceType)((x+y)%5);piece.SetSpecial(PieceSpecialType.None);}
                    }
                    RefreshOrdinaryFixtureArtwork(board);
                    var special=board.GetPieceAt(4,3);
                    Assert.That(board.TryCollectRubyGeyser(new[]{special},new System.Collections.Generic.List<PieceView>{special}),Is.Empty,
                        "Ordinary removal cannot fire geyser.");
                    special.SetSpecial(PieceSpecialType.RowBlast);
                    var health=(int[,])typeof(BoardController).GetField("obstacleHealth",flags).GetValue(board);
                    var untouched=board.GetPieceAt(4,5);
                    CaptureGameplayState("ruby-geyser-board-83.png");
                    typeof(GameBootstrap).GetMethod("HandleChainCompleted",flags).Invoke(bootstrap,
                        new object[]{new System.Collections.Generic.List<PieceView>{special}});
                    Assert.That(board.IsRubyGeyserUsed(4,3),Is.True);Assert.That(board.IsRubyGeyserUsed(4,6),Is.False);
                    Assert.That(health[4,3],Is.EqualTo(2));Assert.That(health[4,4],Is.EqualTo(2),"Normal adjacent hit is not doubled.");
                    Assert.That(health[4,1],Is.EqualTo(2));Assert.That(health[4,5],Is.EqualTo(2));
                    Assert.That(health[5,5],Is.EqualTo(3),"No adjacent column reach.");Assert.That(health[4,6],Is.EqualTo(3),"No more than two cells distance.");
                    Assert.That(board.GetPieceAt(4,5),Is.SameAs(untouched));
                    Assert.That(board.TryCollectRubyGeyser(new[]{special},new System.Collections.Generic.List<PieceView>{special}),Is.Empty);
                    yield return null;
                    SpriteRenderer marker=null;
                    foreach(var candidate in board.GetComponentsInChildren<SpriteRenderer>())
                        if(candidate.name=="RubyGeyser_4_3") {marker=candidate;break;}
                    Assert.That(marker,Is.Not.Null);Assert.That(marker.color.a,Is.EqualTo(.55f).Within(.01f));
                    Assert.That(marker.GetComponent<Collider2D>(),Is.Null);
                    yield return new WaitForSecondsRealtime(.15f);CaptureGameplayState("ruby-geyser-active-83.png");
                    bootstrap.StartNewMatch();yield return new WaitForSecondsRealtime(4.5f);
                    Assert.That(board.IsRubyGeyserUsed(4,3),Is.False);
                }
                board.config.obstacleCount=1;board.config.obstacleCells=new[]{"4,5"};board.InitializeBoard();yield return null;
                var vent=board.GetPieceAt(4,3);vent.SetSpecial(PieceSpecialType.RowBlast);
                Assert.That(board.TryCollectRubyGeyser(new[]{vent},new System.Collections.Generic.List<PieceView>()),Is.Empty,"Retained special cannot fire.");
                Assert.That(board.TryCollectRubyGeyser(new[]{vent},new System.Collections.Generic.List<PieceView>{vent,board.GetPieceAt(4,4)}),Is.Empty,
                    "Normal sand neighbour hit already covers target: save geyser.");
                Assert.That(board.IsRubyGeyserUsed(4,3),Is.False);
                board.config.obstacleCells=new[]{"4,3"};board.InitializeBoard();yield return null;
                vent=board.GetPieceAt(4,3);vent.SetSpecial(PieceSpecialType.RowBlast);
                Assert.That(board.TryCollectRubyGeyser(new[]{vent},new System.Collections.Generic.List<PieceView>{vent}),Is.Empty);
                Assert.That(board.IsRubyGeyserUsed(4,3),Is.False);
                bootstrap.currentLevel=1;bootstrap.StartNewMatch();Assert.That(board.config.rubyGeyserCells,Is.Null.Or.Empty);
            }
            finally
            {
                Random.state=previousRandom;JoinDog.App.AccessibilitySettings.ReducedMotion=previousMotion;
                if(bootstrap!=null) {bootstrap.currentLevel=1;bootstrap.StartNewMatch();}
            }
        }

        [UnityTest]
        public IEnumerator CelestialSprouts_PruneOnlyAdditionalNeighboursAndRespectVineGrowth()
        {
            var previousRandom=Random.state;
            bool previousMotion=JoinDog.App.AccessibilitySettings.ReducedMotion;
            GameBootstrap bootstrap=null;
            var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
            try
            {
                SceneManager.LoadScene("Gameplay",LoadSceneMode.Single);yield return null;yield return null;
                bootstrap=Object.FindAnyObjectByType<GameBootstrap>();
                bootstrap.currentLevel=73;bootstrap.StartNewMatch();bootstrap.uiController.SetMainMenuVisible(false);
                yield return new WaitForSecondsRealtime(.3f);CaptureGameplayState("celestial-sprout-intro-73.png");
                yield return new WaitForSecondsRealtime(4.5f);
                var board=bootstrap.boardController;
                Assert.That(board.config.celestialSproutCells,Is.Not.Null.And.Not.Empty);
                foreach(bool reduced in new[]{false,true})
                {
                    JoinDog.App.AccessibilitySettings.ReducedMotion=reduced;
                    board.config.obstacleType=CellObstacleType.Vine;board.config.obstacleCount=7;board.config.obstacleDurability=3;
                    board.config.obstacleCells=new[]{"4,2","4,4","5,3","3,3","5,4","6,4","6,6"};
                    board.config.celestialSproutCells=new[]{"4,3","4,6","4,3","999,999","bad"};
                    board.InitializeBoard();yield return null;
                    typeof(GameBootstrap).GetMethod("RefreshSecondaryHazardUI",flags).Invoke(bootstrap,null);
                    Assert.That(board.IsCelestialSproutCell(999,999),Is.False);
                    for(int x=0;x<board.Columns;x++) for(int y=0;y<board.Rows;y++)
                    {
                        var piece=board.GetPieceAt(x,y);if(piece!=null) {piece.type=(PieceType)((x+y)%5);piece.SetSpecial(PieceSpecialType.None);}
                    }
                    var matches=new System.Collections.Generic.List<PieceView>();
                    for(int x=4;x>=1;x--) {var piece=board.GetPieceAt(x,3);piece.type=PieceType.Bone;matches.Add(piece);}
                    if(board.GetPieceAt(0,3)!=null) board.GetPieceAt(0,3).type=PieceType.Food;board.GetPieceAt(5,3).type=PieceType.Food;
                    board.GetPieceAt(4,2).type=PieceType.Food;board.GetPieceAt(4,4).type=PieceType.Food;
                    RefreshOrdinaryFixtureArtwork(board);
                    var untouched=board.GetPieceAt(5,4);
                    var health=(int[,])typeof(BoardController).GetField("obstacleHealth",flags).GetValue(board);
                    Assert.That(board.TryCollectCelestialSprout(board.GetPieceAt(4,3),matches),Is.Empty,"Ordinary removal cannot use sprout.");
                    CaptureGameplayState("celestial-sprout-board-73.png");
                    typeof(GameBootstrap).GetMethod("HandlePlayerMatch3Move",flags).Invoke(bootstrap,new object[]{matches});
                    Assert.That(board.GetPieceAt(4,3).IsSpecial,Is.True);
                    Assert.That(board.IsCelestialSproutUsed(4,3),Is.True);Assert.That(board.IsCelestialSproutUsed(4,6),Is.False);
                    foreach(var cell in new[]{new Vector2Int(4,2),new Vector2Int(4,4),new Vector2Int(5,3)})
                        Assert.That(health[cell.x,cell.y],Is.EqualTo(2));
                    Assert.That(health[3,3],Is.EqualTo(2),"Normal hit is not doubled.");
                    Assert.That(health[5,4],Is.EqualTo(3),"No neighbour reach.");Assert.That(health[6,4],Is.EqualTo(3),"No distant reach.");
                    Assert.That(board.GetPieceAt(5,4),Is.SameAs(untouched));
                    Assert.That(board.TryCollectCelestialSprout(board.GetPieceAt(4,3),matches),Is.Empty);
                    // Rendering a portrait fixture can rebuild the frame; retire its old cells first.
                    yield return null;
                    Transform marker=null;
                    foreach(var candidate in board.GetComponentsInChildren<SpriteRenderer>())
                        if(candidate.name=="CelestialSprout_4_3") {marker=candidate.transform;break;}
                    Assert.That(marker,Is.Not.Null);
                    Assert.That(marker.GetComponent<SpriteRenderer>().color.a,Is.EqualTo(.55f).Within(.01f));
                    Assert.That(marker.GetComponent<Collider2D>(),Is.Null);
                    yield return new WaitForSecondsRealtime(.15f);CaptureGameplayState("celestial-sprout-active-73.png");
                    bootstrap.StartNewMatch();yield return new WaitForSecondsRealtime(4.5f);
                    Assert.That(board.IsCelestialSproutUsed(4,3),Is.False);
                }
                board.config.obstacleCount=1;board.config.obstacleCells=new[]{"4,3"};board.InitializeBoard();yield return null;
                var special=board.GetPieceAt(4,3);special.SetSpecial(PieceSpecialType.RowBlast);
                Assert.That(board.TryCollectCelestialSprout(special,new System.Collections.Generic.List<PieceView>()),Is.Empty);
                Assert.That(board.IsCelestialSproutUsed(4,3),Is.False,"No extra vines means save sprout.");
                board.config.obstacleCount=1;board.config.obstacleCells=new[]{"4,4"};board.InitializeBoard();yield return null;
                special=board.GetPieceAt(4,3);special.SetSpecial(PieceSpecialType.RowBlast);
                Assert.That(board.TryCollectCelestialSprout(special,new System.Collections.Generic.List<PieceView>{board.GetPieceAt(4,4)}),Is.Empty,
                    "Already hit neighbour cannot spend sprout for duplicate damage.");
                typeof(GameBootstrap).GetMethod("HandleChainCompleted",flags).Invoke(bootstrap,
                    new object[]{new System.Collections.Generic.List<PieceView>{special}});
                Assert.That(board.IsCelestialSproutUsed(4,3),Is.False,"Activation cannot also use creation-only sprout.");
                bootstrap.StartNewMatch();yield return new WaitForSecondsRealtime(4.5f);
                board.config.obstacleCount=1;board.config.obstacleDurability=1;board.config.obstacleCells=new[]{"4,4"};
                board.InitializeBoard();yield return null;
                var finalMatch=new System.Collections.Generic.List<PieceView>();
                for(int x=4;x>=1;x--) {var piece=board.GetPieceAt(x,3);piece.type=PieceType.Bone;piece.SetSpecial(PieceSpecialType.None);finalMatch.Add(piece);}
                board.GetPieceAt(5,3).type=PieceType.Food;board.GetPieceAt(4,2).type=PieceType.Food;board.GetPieceAt(4,4).type=PieceType.Food;
                typeof(GameBootstrap).GetMethod("HandlePlayerMatch3Move",flags).Invoke(bootstrap,new object[]{finalMatch});
                Assert.That(board.RemainingObstacleCount,Is.Zero);
                Assert.That((int)typeof(GameBootstrap).GetField("obstaclesClearedThisTurn",flags).GetValue(bootstrap),Is.EqualTo(1));
                Assert.That((bool)typeof(GameBootstrap).GetMethod("TrySpreadUnprotectedVines",flags).Invoke(bootstrap,null),Is.False,
                    "Pruning a final layer prevents growth through the established rule.");
                bootstrap.currentLevel=1;bootstrap.StartNewMatch();Assert.That(board.config.celestialSproutCells,Is.Null.Or.Empty);
            }
            finally
            {
                Random.state=previousRandom;JoinDog.App.AccessibilitySettings.ReducedMotion=previousMotion;
                if(bootstrap!=null) {bootstrap.currentLevel=1;bootstrap.StartNewMatch();}
            }
        }

        [UnityTest]
        public IEnumerator SanctuarySeals_SelectExactlyOneNearestAdditionalLanternDeterministically()
        {
            var previousRandom=Random.state;
            bool previousMotion=JoinDog.App.AccessibilitySettings.ReducedMotion;
            GameBootstrap bootstrap=null;
            var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
            try
            {
                SceneManager.LoadScene("Gameplay",LoadSceneMode.Single);yield return null;yield return null;
                bootstrap=Object.FindAnyObjectByType<GameBootstrap>();
                bootstrap.currentLevel=93;bootstrap.StartNewMatch();bootstrap.uiController.SetMainMenuVisible(false);
                yield return new WaitForSecondsRealtime(.3f);CaptureGameplayState("sanctuary-seal-intro-93.png");
                yield return new WaitForSecondsRealtime(4.5f);
                var board=bootstrap.boardController;
                Assert.That(board.config.sanctuarySealCells,Is.Not.Null.And.Not.Empty);
                foreach(bool reduced in new[]{false,true})
                {
                    JoinDog.App.AccessibilitySettings.ReducedMotion=reduced;
                    board.config.obstacleType=CellObstacleType.Lantern;board.config.obstacleCount=4;board.config.obstacleDurability=3;
                    board.config.obstacleCells=new[]{"3,3","4,4","5,3","6,5"};
                    board.config.sanctuarySealCells=new[]{"4,3","4,6","4,3","999,999","bad"};
                    board.InitializeBoard();yield return null;
                    typeof(GameBootstrap).GetMethod("RefreshSecondaryHazardUI",flags).Invoke(bootstrap,null);
                    Assert.That(board.IsSanctuarySealCell(999,999),Is.False);
                    for(int x=0;x<board.Columns;x++) for(int y=0;y<board.Rows;y++)
                    {
                        var piece=board.GetPieceAt(x,y);if(piece!=null) {piece.type=(PieceType)((x+y)%5);piece.SetSpecial(PieceSpecialType.None);}
                    }
                    var matches=new System.Collections.Generic.List<PieceView>();
                    for(int x=4;x>=1;x--) {var piece=board.GetPieceAt(x,3);piece.type=PieceType.Bone;matches.Add(piece);}
                    if(board.GetPieceAt(0,3)!=null) board.GetPieceAt(0,3).type=PieceType.Food;board.GetPieceAt(5,3).type=PieceType.Food;
                    board.GetPieceAt(4,2).type=PieceType.Food;board.GetPieceAt(4,4).type=PieceType.Food;
                    RefreshOrdinaryFixtureArtwork(board);
                    var untouched=board.GetPieceAt(4,4);
                    var health=(int[,])typeof(BoardController).GetField("obstacleHealth",flags).GetValue(board);
                    Assert.That(board.TryCollectSanctuarySeal(board.GetPieceAt(4,3),matches),Is.Empty,"Ordinary removal cannot use crystal.");
                    CaptureGameplayState("sanctuary-seal-board-93.png");
                    typeof(GameBootstrap).GetMethod("HandlePlayerMatch3Move",flags).Invoke(bootstrap,new object[]{matches});
                    Assert.That(board.GetPieceAt(4,3).IsSpecial,Is.True);
                    Assert.That(board.IsSanctuarySealUsed(4,3),Is.True);Assert.That(board.IsSanctuarySealUsed(4,6),Is.False);
                    Assert.That(health[4,4],Is.EqualTo(2),"Nearest additional lantern receives exactly one layer.");
                    Assert.That(health[5,3],Is.EqualTo(3),"Equal-distance tie picks stable x/y order, never two targets.");
                    Assert.That(health[6,5],Is.EqualTo(3),"More distant lantern remains intact.");
                    Assert.That(health[3,3],Is.EqualTo(2),"Already hit lantern is excluded from seal selection.");
                    Assert.That(board.GetPieceAt(4,4),Is.SameAs(untouched));
                    Assert.That(board.TryCollectSanctuarySeal(board.GetPieceAt(4,3),matches),Is.Empty);
                    // Rendering a portrait fixture can rebuild the frame; retire its old cells first.
                    yield return null;
                    Transform marker=null;
                    foreach(var candidate in board.GetComponentsInChildren<SpriteRenderer>())
                        if(candidate.name=="SanctuarySeal_4_3") {marker=candidate.transform;break;}
                    Assert.That(marker,Is.Not.Null);
                    Assert.That(marker.GetComponent<SpriteRenderer>().color.a,Is.EqualTo(.55f).Within(.01f));
                    Assert.That(marker.GetComponent<Collider2D>(),Is.Null);
                    yield return new WaitForSecondsRealtime(.15f);CaptureGameplayState("sanctuary-seal-active-93.png");
                    bootstrap.StartNewMatch();yield return new WaitForSecondsRealtime(4.5f);
                    Assert.That(board.IsSanctuarySealUsed(4,3),Is.False);
                }
                board.config.obstacleCount=0;board.config.obstacleCells=System.Array.Empty<string>();board.InitializeBoard();yield return null;
                var special=board.GetPieceAt(4,3);special.SetSpecial(PieceSpecialType.RowBlast);
                Assert.That(board.TryCollectSanctuarySeal(special,new System.Collections.Generic.List<PieceView>()),Is.Empty);
                Assert.That(board.IsSanctuarySealUsed(4,3),Is.False,"No lantern means save seal.");
                board.config.obstacleCount=1;board.config.obstacleCells=new[]{"4,4"};board.InitializeBoard();yield return null;
                special=board.GetPieceAt(4,3);special.SetSpecial(PieceSpecialType.RowBlast);
                Assert.That(board.TryCollectSanctuarySeal(special,new System.Collections.Generic.List<PieceView>{board.GetPieceAt(4,4)}),Is.Empty,
                    "Already hit lantern cannot spend seal for duplicate damage.");
                typeof(GameBootstrap).GetMethod("HandleChainCompleted",flags).Invoke(bootstrap,
                    new object[]{new System.Collections.Generic.List<PieceView>{special}});
                Assert.That(board.IsSanctuarySealUsed(4,3),Is.False,"Activation cannot also use creation-only seal.");
                bootstrap.StartNewMatch();yield return new WaitForSecondsRealtime(4.5f);
                board.config.obstacleCount=2;board.config.obstacleCells=new[]{"4,4","5,3"};board.InitializeBoard();yield return null;
                special=board.GetPieceAt(4,3);special.SetSpecial(PieceSpecialType.RowBlast);
                var rngBefore=Random.state;
                var target=board.TryCollectSanctuarySeal(special,new System.Collections.Generic.List<PieceView>());
                Assert.That(target,Is.EqualTo(new[]{new Vector2Int(4,4)}));
                Assert.That(JsonUtility.ToJson(Random.state),Is.EqualTo(JsonUtility.ToJson(rngBefore)),"Seal selection never consumes gameplay randomness.");

                bootstrap.currentLevel=1;bootstrap.StartNewMatch();Assert.That(board.config.sanctuarySealCells,Is.Null.Or.Empty);
            }
            finally
            {
                Random.state=previousRandom;JoinDog.App.AccessibilitySettings.ReducedMotion=previousMotion;
                if(bootstrap!=null) {bootstrap.currentLevel=1;bootstrap.StartNewMatch();}
            }
        }

        [UnityTest]
        public IEnumerator SummitCrystals_RewardCreationWithOnlyAdditionalDiagonalIce()
        {
            var previousRandom=Random.state;
            bool previousMotion=JoinDog.App.AccessibilitySettings.ReducedMotion;
            GameBootstrap bootstrap=null;
            var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
            try
            {
                SceneManager.LoadScene("Gameplay",LoadSceneMode.Single);yield return null;yield return null;
                bootstrap=Object.FindAnyObjectByType<GameBootstrap>();
                bootstrap.currentLevel=63;bootstrap.StartNewMatch();bootstrap.uiController.SetMainMenuVisible(false);
                yield return new WaitForSecondsRealtime(.3f);CaptureGameplayState("summit-crystal-intro-63.png");
                yield return new WaitForSecondsRealtime(4.5f);
                var board=bootstrap.boardController;
                Assert.That(board.config.summitCrystalCells,Is.Not.Null.And.Not.Empty);
                foreach(bool reduced in new[]{false,true})
                {
                    JoinDog.App.AccessibilitySettings.ReducedMotion=reduced;
                    board.config.obstacleType=CellObstacleType.Ice;board.config.obstacleCount=7;board.config.obstacleDurability=3;
                    board.config.obstacleCells=new[]{"3,2","3,4","5,2","5,4","4,2","6,4","3,3"};
                    board.config.summitCrystalCells=new[]{"4,3","4,6","4,3","999,999","bad"};
                    board.InitializeBoard();yield return null;
                    typeof(GameBootstrap).GetMethod("RefreshSecondaryHazardUI",flags).Invoke(bootstrap,null);
                    Assert.That(board.IsSummitCrystalCell(999,999),Is.False);
                    for(int x=0;x<board.Columns;x++) for(int y=0;y<board.Rows;y++)
                    {
                        var piece=board.GetPieceAt(x,y);if(piece!=null) {piece.type=(PieceType)((x+y)%5);piece.SetSpecial(PieceSpecialType.None);}
                    }
                    var matches=new System.Collections.Generic.List<PieceView>();
                    for(int x=4;x>=1;x--) {var piece=board.GetPieceAt(x,3);piece.type=PieceType.Bone;matches.Add(piece);}
                    board.GetPieceAt(0,3).type=PieceType.Food;board.GetPieceAt(5,3).type=PieceType.Food;
                    board.GetPieceAt(4,2).type=PieceType.Food;board.GetPieceAt(4,4).type=PieceType.Food;
                    RefreshOrdinaryFixtureArtwork(board);
                    var untouched=board.GetPieceAt(5,4);
                    var health=(int[,])typeof(BoardController).GetField("obstacleHealth",flags).GetValue(board);
                    Assert.That(board.TryCollectSummitCrystal(board.GetPieceAt(4,3),matches),Is.Empty,"Ordinary removal cannot use crystal.");
                    CaptureGameplayState("summit-crystal-board-63.png");
                    typeof(GameBootstrap).GetMethod("HandlePlayerMatch3Move",flags).Invoke(bootstrap,new object[]{matches});
                    Assert.That(board.GetPieceAt(4,3).IsSpecial,Is.True);
                    Assert.That(board.IsSummitCrystalUsed(4,3),Is.True);Assert.That(board.IsSummitCrystalUsed(4,6),Is.False);
                    foreach(var cell in new[]{new Vector2Int(3,2),new Vector2Int(3,4),new Vector2Int(5,2),new Vector2Int(5,4)})
                        Assert.That(health[cell.x,cell.y],Is.EqualTo(2));
                    Assert.That(health[3,3],Is.EqualTo(2),"Normal hit is not doubled.");
                    Assert.That(health[4,2],Is.EqualTo(3),"No orthogonal reach.");Assert.That(health[6,4],Is.EqualTo(3),"No distant reach.");
                    Assert.That(board.GetPieceAt(5,4),Is.SameAs(untouched));
                    Assert.That(board.TryCollectSummitCrystal(board.GetPieceAt(4,3),matches),Is.Empty);
                    // Rendering a portrait fixture can rebuild the frame; retire its old cells first.
                    yield return null;
                    Transform marker=null;
                    foreach(var candidate in board.GetComponentsInChildren<SpriteRenderer>())
                        if(candidate.name=="SummitCrystal_4_3") {marker=candidate.transform;break;}
                    Assert.That(marker,Is.Not.Null);
                    Assert.That(marker.GetComponent<SpriteRenderer>().color.a,Is.EqualTo(.55f).Within(.01f));
                    Assert.That(marker.GetComponent<Collider2D>(),Is.Null);
                    yield return new WaitForSecondsRealtime(.15f);CaptureGameplayState("summit-crystal-active-63.png");
                    bootstrap.StartNewMatch();yield return new WaitForSecondsRealtime(4.5f);
                    Assert.That(board.IsSummitCrystalUsed(4,3),Is.False);
                }
                board.config.obstacleCount=1;board.config.obstacleCells=new[]{"4,3"};board.InitializeBoard();yield return null;
                var special=board.GetPieceAt(4,3);special.SetSpecial(PieceSpecialType.RowBlast);
                Assert.That(board.TryCollectSummitCrystal(special,new System.Collections.Generic.List<PieceView>()),Is.Empty);
                Assert.That(board.IsSummitCrystalUsed(4,3),Is.False,"No extra ice means save crystal.");
                board.config.obstacleCount=1;board.config.obstacleCells=new[]{"5,4"};board.InitializeBoard();yield return null;
                special=board.GetPieceAt(4,3);special.SetSpecial(PieceSpecialType.RowBlast);
                Assert.That(board.TryCollectSummitCrystal(special,new System.Collections.Generic.List<PieceView>{board.GetPieceAt(5,4)}),Is.Empty,
                    "Already hit diagonal cannot spend crystal for duplicate damage.");
                typeof(GameBootstrap).GetMethod("HandleChainCompleted",flags).Invoke(bootstrap,
                    new object[]{new System.Collections.Generic.List<PieceView>{special}});
                Assert.That(board.IsSummitCrystalUsed(4,3),Is.False,"Activation cannot also use creation-only crystal.");
                bootstrap.currentLevel=1;bootstrap.StartNewMatch();Assert.That(board.config.summitCrystalCells,Is.Null.Or.Empty);
            }
            finally
            {
                Random.state=previousRandom;JoinDog.App.AccessibilitySettings.ReducedMotion=previousMotion;
                if(bootstrap!=null) {bootstrap.currentLevel=1;bootstrap.StartNewMatch();}
            }
        }

        [UnityTest]
        public IEnumerator AuroraPrisms_LightOnlyAdditionalLanternsInTheirColumnOnce()
        {
            var previousRandom=Random.state;
            bool previousMotion=JoinDog.App.AccessibilitySettings.ReducedMotion;
            GameBootstrap bootstrap=null;
            var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
            try
            {
                SceneManager.LoadScene("Gameplay",LoadSceneMode.Single);yield return null;yield return null;
                bootstrap=Object.FindAnyObjectByType<GameBootstrap>();
                bootstrap.currentLevel=53;bootstrap.StartNewMatch();bootstrap.uiController.SetMainMenuVisible(false);
                yield return new WaitForSecondsRealtime(.3f);CaptureGameplayState("aurora-prism-intro-53.png");
                yield return new WaitForSecondsRealtime(4.5f);
                var board=bootstrap.boardController;
                Assert.That(board.config.auroraPrismCells,Is.Not.Null.And.Not.Empty);
                foreach(bool reduced in new[]{false,true})
                {
                    JoinDog.App.AccessibilitySettings.ReducedMotion=reduced;
                    board.config.obstacleType=CellObstacleType.Lantern;board.config.obstacleCount=5;board.config.obstacleDurability=3;
                    board.config.obstacleCells=new[]{"4,3","4,1","4,7","5,7","6,6"};
                    board.config.auroraPrismCells=new[]{"4,3","6,6","4,3","999,999","bad"};
                    board.InitializeBoard();yield return null;
                    typeof(GameBootstrap).GetMethod("RefreshSecondaryHazardUI",flags).Invoke(bootstrap,null);
                    Assert.That(board.IsAuroraPrismCell(999,999),Is.False);
                    for(int x=0;x<board.Columns;x++) for(int y=0;y<board.Rows;y++)
                    {
                        var piece=board.GetPieceAt(x,y);if(piece!=null) {piece.type=(PieceType)((x+y)%5);piece.SetSpecial(PieceSpecialType.None);}
                    }
                    var matches=new System.Collections.Generic.List<PieceView>();
                    for(int x=3;x<=5;x++) {var piece=board.GetPieceAt(x,3);piece.type=PieceType.Bone;matches.Add(piece);}
                    board.GetPieceAt(2,3).type=PieceType.Food;board.GetPieceAt(6,3).type=PieceType.Food;
                    board.GetPieceAt(4,2).type=PieceType.Food;board.GetPieceAt(4,4).type=PieceType.Food;
                    RefreshOrdinaryFixtureArtwork(board);
                    var untouched=board.GetPieceAt(4,7);
                    var health=(int[,])typeof(BoardController).GetField("obstacleHealth",flags).GetValue(board);
                    CaptureGameplayState("aurora-prism-board-53.png");
                    typeof(GameBootstrap).GetMethod("HandlePlayerMatch3Move",flags).Invoke(bootstrap,new object[]{matches});
                    Assert.That(board.IsAuroraPrismUsed(4,3),Is.True);Assert.That(board.IsAuroraPrismUsed(6,6),Is.False);
                    Assert.That(health[4,3],Is.EqualTo(2),"No double damage on the removed cell.");
                    Assert.That(health[4,1],Is.EqualTo(2));Assert.That(health[4,7],Is.EqualTo(2));
                    Assert.That(health[5,7],Is.EqualTo(3),"No adjacent column impact.");Assert.That(health[6,6],Is.EqualTo(3));
                    Assert.That(board.GetPieceAt(4,7),Is.SameAs(untouched));
                    Assert.That(board.TryCollectAuroraPrism(matches,matches),Is.Empty);
                    var marker=board.transform.Find("[AdaptiveBoardVisual]/Cell_4_3/AuroraPrism_4_3");
                    Assert.That(marker.GetComponent<SpriteRenderer>().color.a,Is.EqualTo(.55f).Within(.01f));
                    Assert.That(marker.GetComponent<Collider2D>(),Is.Null);
                    yield return new WaitForSecondsRealtime(.15f);CaptureGameplayState("aurora-prism-active-53.png");
                    bootstrap.StartNewMatch();yield return new WaitForSecondsRealtime(4.5f);
                    Assert.That(board.IsAuroraPrismUsed(4,3),Is.False);
                }
                var prism=board.GetPieceAt(4,3);
                Assert.That(board.TryCollectAuroraPrism(new[]{prism},new System.Collections.Generic.List<PieceView>()),Is.Empty);
                board.config.obstacleCount=2;board.config.obstacleCells=new[]{"4,3","4,7"};board.InitializeBoard();yield return null;
                prism=board.GetPieceAt(4,3);prism.SetSpecial(PieceSpecialType.RowBlast);
                typeof(GameBootstrap).GetMethod("HandleChainCompleted",flags).Invoke(bootstrap,
                    new object[]{new System.Collections.Generic.List<PieceView>{prism}});
                var specialHealth=(int[,])typeof(BoardController).GetField("obstacleHealth",flags).GetValue(board);
                Assert.That(specialHealth[4,3],Is.EqualTo(1),"Activated specials keep their two-layer damage.");
                Assert.That(specialHealth[4,7],Is.EqualTo(3),"Activation must not also launch the environmental column.");
                Assert.That(board.IsAuroraPrismUsed(4,3),Is.False,"Save prism during special activation.");
                bootstrap.StartNewMatch();yield return new WaitForSecondsRealtime(4.5f);
                board.config.obstacleCount=1;board.config.obstacleCells=new[]{"4,3"};board.InitializeBoard();yield return null;
                prism=board.GetPieceAt(4,3);
                Assert.That(board.TryCollectAuroraPrism(new[]{prism},new System.Collections.Generic.List<PieceView>{prism}),Is.Empty);
                Assert.That(board.IsAuroraPrismUsed(4,3),Is.False,"Save prism when there are no additional lanterns.");
                bootstrap.currentLevel=1;bootstrap.StartNewMatch();Assert.That(board.config.auroraPrismCells,Is.Null.Or.Empty);
            }
            finally
            {
                Random.state=previousRandom;JoinDog.App.AccessibilitySettings.ReducedMotion=previousMotion;
                if(bootstrap!=null) {bootstrap.currentLevel=1;bootstrap.StartNewMatch();}
            }
        }

        [UnityTest]
        public IEnumerator MountainWarmth_ThawsOnlyOrthogonalIceWithoutDoublingNormalHits()
        {
            var previousRandom=Random.state;
            bool previousMotion=JoinDog.App.AccessibilitySettings.ReducedMotion;
            GameBootstrap bootstrap=null;
            var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
            try
            {
                SceneManager.LoadScene("Gameplay",LoadSceneMode.Single);yield return null;yield return null;
                bootstrap=Object.FindAnyObjectByType<GameBootstrap>();
                bootstrap.currentLevel=43;bootstrap.StartNewMatch();bootstrap.uiController.SetMainMenuVisible(false);
                yield return new WaitForSecondsRealtime(.3f);CaptureGameplayState("mountain-warmth-intro-43.png");
                yield return new WaitForSecondsRealtime(4.5f);
                var board=bootstrap.boardController;
                foreach(bool reduced in new[]{false,true})
                {
                    JoinDog.App.AccessibilitySettings.ReducedMotion=reduced;
                    board.config.obstacleType=CellObstacleType.Ice;board.config.obstacleCount=7;board.config.obstacleDurability=3;
                    board.config.obstacleCells=new[]{"3,3","4,3","5,3","4,2","4,4","5,4","6,3"};
                    board.config.mountainWarmCells=new[]{"4,3","4,6","4,3","999,999","invalid"};
                    board.InitializeBoard();yield return null;
                    typeof(GameBootstrap).GetMethod("RefreshSecondaryHazardUI",flags).Invoke(bootstrap,null);
                    Assert.That(board.IsMountainWarmCell(999,999),Is.False);
                    for(int x=0;x<board.Columns;x++) for(int y=0;y<board.Rows;y++)
                    {
                        var piece=board.GetPieceAt(x,y);if(piece!=null) {piece.type=(PieceType)((x+y)%5);piece.SetSpecial(PieceSpecialType.None);}
                    }
                    var matches=new System.Collections.Generic.List<PieceView>();
                    for(int x=3;x<=5;x++) {var piece=board.GetPieceAt(x,3);piece.type=PieceType.Bone;matches.Add(piece);}
                    board.GetPieceAt(2,3).type=PieceType.Food;board.GetPieceAt(6,3).type=PieceType.Food;
                    board.GetPieceAt(4,2).type=PieceType.Food;board.GetPieceAt(4,4).type=PieceType.Food;
                    var above=board.GetPieceAt(4,4);
                    var health=(int[,])typeof(BoardController).GetField("obstacleHealth",flags).GetValue(board);
                    RefreshOrdinaryFixtureArtwork(board);
                    CaptureGameplayState("mountain-warmth-board-43.png");
                    typeof(GameBootstrap).GetMethod("HandlePlayerMatch3Move",flags).Invoke(bootstrap,new object[]{matches});
                    Assert.That(board.IsMountainWarmthUsed(4,3),Is.True);Assert.That(board.IsMountainWarmthUsed(4,6),Is.False);
                    Assert.That(health[3,3],Is.EqualTo(2));Assert.That(health[4,3],Is.EqualTo(2));Assert.That(health[5,3],Is.EqualTo(2));
                    Assert.That(health[4,2],Is.EqualTo(2));Assert.That(health[4,4],Is.EqualTo(2));
                    Assert.That(health[5,4],Is.EqualTo(3),"No diagonal warmth.");
                    Assert.That(health[6,3],Is.EqualTo(3),"No distant row impact.");
                    Assert.That(board.GetPieceAt(4,4),Is.SameAs(above),"Warmth thaws ice but leaves the neighbouring figure.");
                    Assert.That(board.TryCollectMountainWarmth(matches,matches),Is.Empty);
                    var marker=board.transform.Find("[AdaptiveBoardVisual]/Cell_4_3/MountainWarmth_4_3");
                    Assert.That(marker.GetComponent<SpriteRenderer>().color.a,Is.EqualTo(.55f).Within(.01f));
                    Assert.That(marker.GetComponent<Collider2D>(),Is.Null);
                    yield return new WaitForSecondsRealtime(.15f);CaptureGameplayState("mountain-warmth-active-43.png");
                    bootstrap.StartNewMatch();yield return new WaitForSecondsRealtime(4.5f);
                }
                var warm=board.GetPieceAt(4,3);
                Assert.That(board.TryCollectMountainWarmth(new[]{warm},new System.Collections.Generic.List<PieceView>()),Is.Empty);
                board.config.obstacleCount=1;board.config.obstacleCells=new[]{"4,3"};board.InitializeBoard();yield return null;
                warm=board.GetPieceAt(4,3);
                Assert.That(board.TryCollectMountainWarmth(new[]{warm},new System.Collections.Generic.List<PieceView>{warm}),Is.Empty);
                Assert.That(board.IsMountainWarmthUsed(4,3),Is.False,"Save warmth when no additional neighbouring ice can benefit.");
                bootstrap.currentLevel=1;bootstrap.StartNewMatch();
                Assert.That(board.config.mountainWarmCells,Is.Null.Or.Empty);
            }
            finally
            {
                Random.state=previousRandom;JoinDog.App.AccessibilitySettings.ReducedMotion=previousMotion;
                if(bootstrap!=null) {bootstrap.currentLevel=1;bootstrap.StartNewMatch();}
            }
        }

        [UnityTest]
        public IEnumerator ObstacleLayers_ReflectRealDeduplicatedDamageAndReducedMotion()
        {
            bool previous=JoinDog.App.AccessibilitySettings.ReducedMotion;
            try
            {
                yield return LoadGameplayScene();
                var bootstrap=Object.FindAnyObjectByType<GameBootstrap>();bootstrap.uiController.SetMainMenuVisible(false);
                var board=bootstrap.boardController;
                foreach(var type in new[]{CellObstacleType.Vine,CellObstacleType.Lantern,CellObstacleType.Sand,
                    CellObstacleType.Ice,CellObstacleType.PuppyCage})
                {
                    board.config.obstacleType=type;board.config.obstacleDurability=3;board.config.obstacleCount=3;
                    board.config.obstacleCells=new[]{"3,3","4,3","4,4"};
                    board.InitializeBoard();yield return null;
                    var health=(int[,])typeof(BoardController).GetField("obstacleHealth",
                        System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(board);
                    var obstacle=GameObject.Find("Obstacle_"+type+"_3_3").transform;
                    var meter=obstacle.Find("LayerMeter");Assert.That(meter,Is.Not.Null);
                    Assert.That(meter.GetComponentsInChildren<Collider2D>().Length,Is.Zero);
                    for(int i=0;i<3;i++) Assert.That(meter.Find("Layer_"+i).GetComponent<SpriteRenderer>().color,Is.EqualTo(Color.white));
                    CaptureGameplayState("obstacle-layers-"+type+".png");
                    var piece=board.GetPieceAt(3,3);
                    JoinDog.App.AccessibilitySettings.ReducedMotion=true;
                    var scale=obstacle.localScale;
                    board.DamageObstacles(new[]{piece,piece});yield return null;
                    Assert.That(health[3,3],Is.EqualTo(2),"Duplicate pieces must not double damage.");
                    Assert.That(health[4,3],Is.EqualTo(type==CellObstacleType.Sand?2:3));
                    Assert.That(health[4,4],Is.EqualTo(3),"Diagonal neighbours are excluded.");
                    Assert.That(obstacle.localScale,Is.EqualTo(scale));
                    Assert.That(obstacle.localRotation,Is.EqualTo(Quaternion.identity));
                    Assert.That(meter.Find("Layer_2").GetComponent<SpriteRenderer>().color,Is.Not.EqualTo(Color.white));
                    board.DamageObstacles(new[]{piece,piece},true);yield return null;
                    bool strong=type==CellObstacleType.Ice || type==CellObstacleType.Lantern;
                    Assert.That(health[3,3],Is.EqualTo(strong?0:1));
                    Assert.That(health[4,3],Is.EqualTo(strong?1:type==CellObstacleType.Sand?1:3));
                    Assert.That(health[4,4],Is.EqualTo(3));
                    if(!strong)
                    {
                        Assert.That(meter.Find("Layer_0").GetComponent<SpriteRenderer>().color,Is.EqualTo(Color.white));
                        Assert.That(meter.Find("Layer_1").GetComponent<SpriteRenderer>().color,Is.Not.EqualTo(Color.white));
                        board.DamageObstacles(new[]{piece});yield return null;
                    }
                    Assert.That(GameObject.Find("Obstacle_"+type+"_3_3"),Is.Null);
                }
                bootstrap.StartNewMatch();
            }
            finally {JoinDog.App.AccessibilitySettings.ReducedMotion=previous;}
        }

        [UnityTest]
        public IEnumerator DoubleAreaPair_ClipsBothFootprintsAtEdgesAndRespectsReducedMotion()
        {
            bool previous=JoinDog.App.AccessibilitySettings.ReducedMotion;
            try
            {
                yield return LoadGameplayScene();
                var bootstrap=Object.FindAnyObjectByType<GameBootstrap>();
                bootstrap.uiController.SetMainMenuVisible(false);
                var board=bootstrap.boardController;
                foreach(bool edge in new[]{false,true})
                {
                    bootstrap.particleController.ClearMatchImpacts();yield return null;
                    for(int x=0;x<board.Columns;x++) for(int y=0;y<board.Rows;y++)
                        board.GetPieceAt(x,y)?.SetSpecial(PieceSpecialType.None);
                    int origin=edge?0:3;
                    var first=board.GetPieceAt(origin,origin);var second=board.GetPieceAt(origin+1,origin);
                    first.SetSpecial(PieceSpecialType.AreaBlast);second.SetSpecial(PieceSpecialType.AreaBlast);
                    Assert.That(board.TrySwapAndFindMatches(first,second,out var matches),Is.True);
                    var result=board.BuildMatchResolution(matches);
                    Assert.That(result.ComboKind,Is.EqualTo(SpecialComboKind.DoubleArea));
                    Assert.That(result.SpecialsActivated,Is.EqualTo(2));
                    for(int x=0;x<board.Columns;x++) for(int y=0;y<board.Rows;y++)
                    {
                        bool expected=(Mathf.Abs(x-first.gridX)<=2 && Mathf.Abs(y-first.gridY)<=2) ||
                            (Mathf.Abs(x-second.gridX)<=2 && Mathf.Abs(y-second.gridY)<=2);
                        Assert.That(result.PiecesToRemove.Contains(board.GetPieceAt(x,y)),Is.EqualTo(expected));
                    }
                    yield return new WaitForSeconds(.2f);
                    JoinDog.App.AccessibilitySettings.ReducedMotion=false;
                    var routine=(IEnumerator)typeof(GameBootstrap).GetMethod("PlaySpecialImpactAfterCharge",
                        System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)
                        .Invoke(bootstrap,new object[]{result,(first.transform.position+second.transform.position)/2f,0f});
                    bootstrap.StartCoroutine(routine);yield return new WaitForSeconds(.12f);
                    var outlines=bootstrap.particleController.GetComponentsInChildren<LineRenderer>();
                    Assert.That(outlines.Length,Is.EqualTo(16),"Each of eight edges has a glow and core.");
                    var lower=board.GridToWorldPosition(0,0)-Vector3.one*board.ActivePieceSpacing*.5f;
                    var upper=board.GridToWorldPosition(board.Columns-1,board.Rows-1)+Vector3.one*board.ActivePieceSpacing*.5f;
                    foreach(var outline in outlines)
                        for(int i=0;i<outline.positionCount;i++)
                        {
                            Assert.That(outline.GetPosition(i).x,Is.InRange(lower.x,upper.x));
                            Assert.That(outline.GetPosition(i).y,Is.InRange(lower.y,upper.y));
                            Assert.That(outline.startWidth,Is.LessThan(board.ActivePieceSpacing*.25f));
                        }
                    CaptureGameplayState(edge?"pair-area-edge-gameplay.png":"pair-area-center-gameplay.png");
                    bootstrap.particleController.ClearMatchImpacts();yield return null;
                    JoinDog.App.AccessibilitySettings.ReducedMotion=true;
                    bootstrap.particleController.PlayDoubleAreaFootprints(result.ActivatedSpecials,board);
                    yield return null;
                    Assert.That(bootstrap.particleController.GetComponentsInChildren<LineRenderer>().Length,Is.Zero);
                }
                bootstrap.StartNewMatch();
            }
            finally {JoinDog.App.AccessibilitySettings.ReducedMotion=previous;}
        }

        [UnityTest]
        public IEnumerator RestartAfterSpecialCharge_RemovesBeamsBeforeReplacingTheBoard()
        {
            yield return LoadGameplayScene();
            var bootstrap=Object.FindAnyObjectByType<GameBootstrap>();
            bootstrap.uiController.SetMainMenuVisible(false);
            var board=bootstrap.boardController;
            var special=board.GetPieceAt(0,board.Rows/2);
            special.SetSpecial(PieceSpecialType.RowBlast);
            typeof(GameBootstrap).GetMethod("HandleChainCompleted",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(bootstrap,new object[]{new System.Collections.Generic.List<PieceView>{special}});
            yield return new WaitForSeconds(.14f);
            Assert.That(bootstrap.particleController.GetComponentsInChildren<LineRenderer>().Length,Is.GreaterThan(0));
            bootstrap.feedbackController.SpawnFloatingText(special.transform.position,"ANTIGUO",Color.white);
            bootstrap.StartNewMatch();bootstrap.uiController.SetMainMenuVisible(false);
            var replacement=(PieceView[,])board.Grid.Clone();
            Assert.That(bootstrap.particleController.GetComponentsInChildren<LineRenderer>().Length,Is.Zero);
            foreach(var text in Object.FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None))
                Assert.That(text.text,Does.Not.Contain("ANTIGUO"));
            yield return new WaitForSeconds(.6f);
            Assert.That(bootstrap.particleController.GetComponentsInChildren<LineRenderer>().Length,Is.Zero);
            for(int x=0;x<board.Columns;x++) for(int y=0;y<board.Rows;y++)
                Assert.That(board.GetPieceAt(x,y),Is.SameAs(replacement[x,y]));
            CaptureGameplayState("feedback-restart-clean-board.png");
        }

        [UnityTest]
        public IEnumerator LineSpecials_RenderAtBoardEdgesAndRefillCleanly()
        {
            foreach(bool row in new[]{true,false})
            {
                yield return LoadGameplayScene();
                var bootstrap=Object.FindAnyObjectByType<GameBootstrap>();
                bootstrap.uiController.SetMainMenuVisible(false);
                var board=bootstrap.boardController;
                var special=board.GetPieceAt(row ? 0 : board.Columns/2,row ? board.Rows/2 : 0);
                special.SetSpecial(row ? PieceSpecialType.RowBlast : PieceSpecialType.ColumnBlast);
                typeof(GameBootstrap).GetMethod("HandleChainCompleted",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(bootstrap,new object[]{new System.Collections.Generic.List<PieceView>{special}});
                yield return new WaitForSeconds(.20f);
                var lines=bootstrap.particleController.GetComponentsInChildren<LineRenderer>();
                Assert.That(lines.Length,Is.GreaterThanOrEqualTo(2));
                CaptureGameplayState(row ? "line-sweep-row-gameplay.png" : "line-sweep-column-gameplay.png");
                yield return new WaitForSeconds(.20f);
                CaptureGameplayState(row ? "line-sweep-row-arrival.png" : "line-sweep-column-arrival.png");
                yield return new WaitForSeconds(.5f);
                float timeout=Time.realtimeSinceStartup+12f;
                while(bootstrap.gravityController.IsResolving && Time.realtimeSinceStartup<timeout) yield return null;
                Assert.That(bootstrap.gravityController.IsResolving,Is.False);
                for(int x=0;x<board.Columns;x++) for(int y=0;y<board.Rows;y++)
                    if(board.IsPlayableCell(x,y)) Assert.That(board.GetPieceAt(x,y),Is.Not.Null);
            }
        }

        [UnityTest]
        public IEnumerator FoodBooster_FullPausedAndStoppedTimersDoNotConsumeInventory()
        {
            yield return LoadGameplayScene();
            var bootstrap=Object.FindAnyObjectByType<GameBootstrap>();
            bootstrap.uiController.SetMainMenuVisible(false);
            var timer=bootstrap.gameTimer;
            GiveLevelFood(bootstrap);
            timer.enabled=false;timer.StartTimer(20f);
            var button=GameObject.Find("FoodButton_RT").GetComponent<UnityEngine.UI.Button>();
            Assert.That(button.interactable,Is.False);
            InvokeBoosterMethod(bootstrap,"UseFoodBooster");
            Assert.That(RemainingLevelFood(bootstrap),Is.EqualTo(2));
            CaptureGameplayState("food-booster-clock-full.png");
            timer.enabled=true;
            yield return new WaitForSeconds(.12f);
            timer.enabled=false;
            Assert.That(button.interactable,Is.True,"Clock deficit enables the bag on the next tick.");
            timer.SetPaused(true);
            InvokeBoosterMethod(bootstrap,"UseFoodBooster");
            Assert.That(RemainingLevelFood(bootstrap),Is.EqualTo(2));
            timer.SetPaused(false);timer.StopTimer();
            InvokeBoosterMethod(bootstrap,"UseFoodBooster");
            Assert.That(RemainingLevelFood(bootstrap),Is.EqualTo(2));
            timer.enabled=true;
        }

        [UnityTest]
        public IEnumerator FoodBooster_PartialBenefitConsumesOnceAndFullClockDisablesAgain()
        {
            yield return LoadGameplayScene();
            var bootstrap=Object.FindAnyObjectByType<GameBootstrap>();
            GiveLevelFood(bootstrap);
            var timer=bootstrap.gameTimer;
            timer.StartTimer(20f);
            yield return new WaitForSeconds(.12f);
            timer.enabled=false;
            float deficit=timer.durationSeconds-timer.RemainingTime;
            float granted=0f;timer.OnTimeGranted+=amount=>granted+=amount;
            InvokeBoosterMethod(bootstrap,"UseFoodBooster");
            Assert.That(granted,Is.EqualTo(deficit).Within(.001f));
            Assert.That(RemainingLevelFood(bootstrap),Is.EqualTo(1));
            Assert.That(timer.RemainingTime,Is.EqualTo(20f));
            Assert.That(GameObject.Find("FoodButton_RT").GetComponent<UnityEngine.UI.Button>().interactable,Is.False);
            InvokeBoosterMethod(bootstrap,"UseFoodBooster");
            Assert.That(RemainingLevelFood(bootstrap),Is.EqualTo(1));
            timer.enabled=true;
        }

        [UnityTest]
        public IEnumerator FoodBooster_MoveLimitedLevelExplainsDisabledBagAndPreservesInventory()
        {
            yield return LoadGameplayScene();
            var bootstrap=Object.FindAnyObjectByType<GameBootstrap>();
            bootstrap.currentLevel=18;bootstrap.StartNewMatch();
            yield return new WaitForSecondsRealtime(1.3f);
            bootstrap.uiController.SetMainMenuVisible(false);
            GiveLevelFood(bootstrap);
            var button=GameObject.Find("FoodButton_RT").GetComponent<UnityEngine.UI.Button>();
            Assert.That(button.interactable,Is.False);
            var label=GameObject.Find("BoosterCaption2").GetComponent<TMPro.TextMeshProUGUI>();
            Assert.That(label.text,Is.EqualTo("SIN RELOJ"));
            InvokeBoosterMethod(bootstrap,"UseFoodBooster");
            Assert.That(RemainingLevelFood(bootstrap),Is.EqualTo(2));
            label.ForceMeshUpdate();Assert.That(label.isTextOverflowing,Is.False);
            CaptureGameplayState("food-booster-move-level.png");
        }

        [UnityTest]
        public IEnumerator ColorBurst_RestartDuringChargeCancelsDelayedEffectsAndRemoval()
        {
            yield return LoadGameplayScene();
            var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            var board = bootstrap.boardController;
            var special = board.GetPieceAt(board.Columns / 2, board.Rows / 2);
            special.SetSpecial(PieceSpecialType.ColorBurst);
            typeof(GameBootstrap).GetMethod("HandleChainCompleted",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(bootstrap, new object[] { new System.Collections.Generic.List<PieceView> { special } });
            yield return new WaitForSeconds(.04f);
            bootstrap.StartNewMatch();
            var replacement = new PieceView[board.Columns,board.Rows];
            for(int x=0;x<board.Columns;x++) for(int y=0;y<board.Rows;y++) replacement[x,y]=board.GetPieceAt(x,y);
            yield return new WaitForSeconds(.8f);
            Assert.That(bootstrap.gravityController.IsResolving, Is.False);
            var impacts = bootstrap.particleController.GetComponent<MatchImpactController>();
            Assert.That(impacts.ActiveCount, Is.Zero);
            for(int x=0;x<board.Columns;x++) for(int y=0;y<board.Rows;y++)
                Assert.That(board.GetPieceAt(x,y), Is.SameAs(replacement[x,y]));
        }

        [UnityTest]
        public IEnumerator ColorBurst_TravelsInGameplayAndRefillsEveryPlayableCell()
        {
            bool previousMotion = JoinDog.App.AccessibilitySettings.ReducedMotion;
            JoinDog.App.AccessibilitySettings.ReducedMotion = false;
            yield return LoadGameplayScene();
            var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            bootstrap.uiController.SetMainMenuVisible(false);
            var board = bootstrap.boardController;
            var special = board.GetPieceAt(board.Columns / 2, board.Rows / 2);
            special.SetSpecial(PieceSpecialType.ColorBurst);
            var method = typeof(GameBootstrap).GetMethod("HandleChainCompleted",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            method.Invoke(bootstrap, new object[] { new System.Collections.Generic.List<PieceView> { special } });
            yield return new WaitForSeconds(.22f);
            var impacts = bootstrap.particleController.GetComponent<MatchImpactController>();
            Assert.That(impacts, Is.Not.Null);
            Assert.That(impacts.ActiveCount, Is.GreaterThan(0));
            var camera = Camera.main;
            var frame = new RenderTexture(451,871,24);
            var pixels = new Texture2D(451,871,TextureFormat.RGB24,false);
            var previous = RenderTexture.active;
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            var modes = new System.Collections.Generic.List<RenderMode>();
            var cameras = new System.Collections.Generic.List<Camera>();
            foreach(var canvas in canvases)
            {
                modes.Add(canvas.renderMode); cameras.Add(canvas.worldCamera);
                if(canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    canvas.renderMode=RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera=camera;canvas.planeDistance=1f;
                }
            }
            try
            {
                camera.targetTexture=frame;Canvas.ForceUpdateCanvases();camera.Render();
                RenderTexture.active=frame;pixels.ReadPixels(new Rect(0,0,451,871),0,0);pixels.Apply();
                string directory=System.IO.Path.Combine(Application.dataPath,"../Builds/visual-qa");
                System.IO.Directory.CreateDirectory(directory);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory,"color-sweep-gameplay.png"),pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active=previous;camera.targetTexture=null;
                for(int i=0;i<canvases.Length;i++) { canvases[i].renderMode=modes[i];canvases[i].worldCamera=cameras[i]; }
                frame.Release();Object.Destroy(frame);Object.Destroy(pixels);
                JoinDog.App.AccessibilitySettings.ReducedMotion=previousMotion;
            }
            // The flight precedes removal: wait beyond its impact delay before checking gravity.
            yield return new WaitForSeconds(.5f);
            float timeout=Time.realtimeSinceStartup+12f;
            while(bootstrap.gravityController.IsResolving && Time.realtimeSinceStartup<timeout) yield return null;
            Assert.That(bootstrap.gravityController.IsResolving, Is.False);
            for(int x=0;x<board.Columns;x++) for(int y=0;y<board.Rows;y++)
                if(board.IsPlayableCell(x,y)) Assert.That(board.GetPieceAt(x,y), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator GameplayScene_FillsConfiguredBoardWithInteractivePieces()
        {
            yield return LoadGameplayScene();

            BoardController board = Object.FindAnyObjectByType<BoardController>();
            Assert.That(board, Is.Not.Null, "Gameplay scene must contain a BoardController.");
            Assert.That(board.Grid, Is.Not.Null, "BoardController must initialize its grid.");
            Assert.That(board.Columns, Is.GreaterThanOrEqualTo(7));
            Assert.That(board.Rows, Is.GreaterThanOrEqualTo(8));
            Assert.That(board.HasAnyValidMove(), Is.True,
                "The generated board must contain an orthogonal three-piece move.");

            AdaptiveBoardView adaptiveView = board.GetComponent<AdaptiveBoardView>();
            Assert.That(adaptiveView, Is.Not.Null,
                "The board must use the adaptive visual presenter.");
            Assert.That(adaptiveView.VisualSize.x, Is.GreaterThan(0f));
            Assert.That(adaptiveView.VisualSize.y, Is.GreaterThan(0f));
            Assert.That(GameObject.Find("BoardFrame"), Is.Null,
                "The rigid legacy board image must not remain active.");

            GameObject topHud = GameObject.Find("AdventureHeader_RT");
            GameObject bottomHud = GameObject.Find("AdventureBoosters_RT");
            Assert.That(topHud, Is.Not.Null, "The adaptive top HUD must be generated.");
            Assert.That(bottomHud, Is.Not.Null, "The adaptive bottom HUD must be generated.");
            Assert.That(GameObject.Find("TopBarOuter_RT"), Is.Null,
                "The fixed top-panel implementation must no longer be active.");
            Assert.That(GameObject.Find("BottomPill_RT"), Is.Null,
                "The fixed bottom-panel implementation must no longer be active.");

            Assert.That(GameObject.Find("GoalCaption"), Is.Not.Null);
            Assert.That(GameObject.Find("ScoreText_RT"), Is.Not.Null);
            Assert.That(GameObject.Find("LivesText_RT"), Is.Not.Null);
            Assert.That(GameObject.Find("TimerBarFill_RT"), Is.Not.Null);

            int activePieces = 0;
            PieceView[] pieces = Object.FindObjectsByType<PieceView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (PieceView piece in pieces)
            {
                if (piece != null && piece.gameObject.activeInHierarchy)
                {
                    activePieces++;
                    Assert.That(piece.GetComponent<Collider2D>(), Is.Not.Null,
                        "Every board piece must remain interactive.");
                    SpriteRenderer renderer = piece.GetComponent<SpriteRenderer>();
                    Assert.That(renderer, Is.Not.Null);
                    Assert.That(renderer.sprite, Is.Not.Null, "Every board piece must have a sprite.");
                    Assert.That(renderer.sprite.bounds.size.x, Is.GreaterThan(0.5f),
                        "Piece sprite geometry must be large enough to be visible on the board.");
                }
            }

            Assert.That(activePieces, Is.EqualTo(CountPlayableCells(board)),
                "The initial board must fill every playable cell.");
        }

        [UnityTest]
        public IEnumerator RestartingMatch_RecyclesPreviousPieces()
        {
            yield return LoadGameplayScene();

            GameBootstrap bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            Assert.That(bootstrap, Is.Not.Null);

            bootstrap.RestartGame();
            yield return null;
            yield return null;

            PieceView[] pieces = Object.FindObjectsByType<PieceView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            int activePieces = 0;
            foreach (PieceView piece in pieces)
            {
                if (piece != null && piece.gameObject.activeInHierarchy)
                {
                    activePieces++;
                }
            }

            Assert.That(activePieces, Is.EqualTo(CountPlayableCells(
                Object.FindAnyObjectByType<BoardController>())),
                "Restarting a match must leave exactly one active set of pieces.");
        }

        [UnityTest]
        public IEnumerator SettingsPanel_ControlsSoundHapticsAndPausesTimer()
        {
            PlayerPrefs.DeleteKey("DogCrush_SfxVolume");
            PlayerPrefs.DeleteKey("DogCrush_HapticsEnabled");
            yield return LoadGameplayScene();

            GameplayUIController ui = Object.FindAnyObjectByType<GameplayUIController>();
            AudioPlaceholderController audio = Object.FindAnyObjectByType<AudioPlaceholderController>();
            HapticFeedbackController haptics = Object.FindAnyObjectByType<HapticFeedbackController>();
            GameTimer timer = Object.FindAnyObjectByType<GameTimer>();

            Assert.That(ui, Is.Not.Null);
            Assert.That(audio, Is.Not.Null);
            Assert.That(haptics, Is.Not.Null);
            Assert.That(timer, Is.Not.Null);
            Assert.That(ui.settingsButton, Is.Not.Null);
            Assert.That(ui.settingsPanel, Is.Not.Null);
            Assert.That(ui.settingsPanel.activeSelf, Is.False);

            ui.settingsButton.onClick.Invoke();
            yield return null;
            Assert.That(ui.settingsPanel.activeSelf, Is.True);
            Assert.That(timer.IsPaused, Is.True);

            ui.soundToggleButton.onClick.Invoke();
            yield return null;
            Assert.That(audio.SfxVolume, Is.EqualTo(0.6f).Within(0.001f));
            StringAssert.Contains("60%", ui.soundToggleText.text);

            ui.hapticsToggleButton.onClick.Invoke();
            yield return null;
            Assert.That(haptics.HapticsEnabled, Is.False);
            StringAssert.Contains("NO", ui.hapticsToggleText.text);

            ui.settingsCloseButton.onClick.Invoke();
            yield return null;
            Assert.That(ui.settingsPanel.activeSelf, Is.False);
            Assert.That(timer.IsPaused, Is.False);

            PlayerPrefs.DeleteKey("DogCrush_SfxVolume");
            PlayerPrefs.DeleteKey("DogCrush_HapticsEnabled");
        }

        [UnityTest]
        public IEnumerator PuzzleRefill_PrefixResetsAndFallsBackToActivePoolWithoutAffectingOrdinaryBoards()
        {
            yield return LoadGameplayScene();
            var board = Object.FindAnyObjectByType<BoardController>();
            var original = board.config;
            var fixture = Object.Instantiate(original);
            try
            {
                fixture.typeCount = 3;
                fixture.activePieceTypes = new[] {PieceType.Dog, PieceType.Bone, PieceType.Food};
                fixture.layoutRows = null; fixture.initialPieceRows = null;
                fixture.boardShape = BoardShape.Full;
                fixture.openingRefillPieces = new[] {PieceType.Dog, PieceType.None, PieceType.Ball, PieceType.Bone};
                board.config = fixture; board.InitializeBoard();
                void Remove(int x, int y)
                {
                    board.spawner.RecyclePiece(board.GetPieceAt(x, y));
                    board.SetPieceAt(x, y, null);
                }
                for (int y = 0; y < 4; y++) Remove(0, y);
                board.FillMissingCells();
                Assert.That(board.GetPieceAt(0, 0).type, Is.EqualTo(PieceType.Dog));
                Assert.That(board.GetPieceAt(0, 3).type, Is.EqualTo(PieceType.Bone));
                foreach (int y in new[] {1, 2})
                    Assert.That(fixture.GetActivePieceTypes(), Does.Contain(board.GetPieceAt(0, y).type), "Invalid/out-of-pool entries fall back.");
                for (int y = 0; y < board.Rows; y++) Remove(1, y);
                board.FillMissingCells();
                for (int y = 0; y < board.Rows; y++)
                    Assert.That(fixture.GetActivePieceTypes(), Does.Contain(board.GetPieceAt(1, y).type), "Exhausted prefix uses the normal pool.");
                board.InitializeBoard(); Remove(0, 0); board.FillMissingCells();
                Assert.That(board.GetPieceAt(0, 0).type, Is.EqualTo(PieceType.Dog), "Restart resets prefix.");
                fixture.openingRefillPieces = null;
                board.InitializeBoard();
                for (int y = 0; y < board.Rows; y++) Remove(1, y);
                board.FillMissingCells();
                for (int y = 0; y < board.Rows; y++)
                    Assert.That(fixture.GetActivePieceTypes(), Does.Contain(board.GetPieceAt(1, y).type), "Ordinary boards retain random refill.");
            }
            finally { board.config = original; Object.Destroy(fixture); }
        }

        [UnityTest]
        public IEnumerator ChangingLevelDimensions_RebuildsAdaptiveBoard()
        {
            yield return LoadGameplayScene();

            BoardController board = Object.FindAnyObjectByType<BoardController>();
            Assert.That(board, Is.Not.Null);

            BoardConfig originalConfig = board.config;
            BoardConfig levelConfig = Object.Instantiate(originalConfig);
            levelConfig.columns = 7;
            levelConfig.rows = 9;

            board.config = levelConfig;
            board.InitializeBoard();
            yield return null;

            Assert.That(board.Columns, Is.EqualTo(7));
            Assert.That(board.Rows, Is.EqualTo(9));
            Assert.That(board.Grid.GetLength(0), Is.EqualTo(7));
            Assert.That(board.Grid.GetLength(1), Is.EqualTo(9));

            AdaptiveBoardView adaptiveView = board.GetComponent<AdaptiveBoardView>();
            Assert.That(adaptiveView, Is.Not.Null);
            Assert.That(adaptiveView.VisualSize.y, Is.GreaterThan(adaptiveView.VisualSize.x),
                "A 7x9 level must produce a naturally taller board without stretching its cells.");

            int activePieces = 0;
            PieceView[] pieces = Object.FindObjectsByType<PieceView>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            foreach (PieceView piece in pieces)
            {
                if (piece.gameObject.activeInHierarchy) activePieces++;
            }
            Assert.That(activePieces, Is.EqualTo(63));

            board.config = originalConfig;
            Object.Destroy(levelConfig);
        }

        [UnityTest]
        public IEnumerator DraggingDiagonally_DoesNotExtendSelection()
        {
            yield return LoadGameplayScene();

            BoardController board = Object.FindAnyObjectByType<BoardController>();
            ChainSelectionController selection = Object.FindAnyObjectByType<ChainSelectionController>();
            ChainInputHandler input = Object.FindAnyObjectByType<ChainInputHandler>();
            Assert.That(board, Is.Not.Null);
            Assert.That(selection, Is.Not.Null);
            Assert.That(input, Is.Not.Null);

            PieceView first = board.GetPieceAt(0, 0);
            PieceView diagonal = board.GetPieceAt(1, 1);
            diagonal.Initialize(
                first.type,
                1,
                1,
                board.spawner.GetSpriteForType(first.type),
                board.spawner.GetColorForType(first.type));

            Physics2D.SyncTransforms();
            input.OnPointerDownEvent?.Invoke(first.transform.position);
            yield return null;
            input.OnPointerDragEvent?.Invoke(diagonal.transform.position);
            yield return null;

            Assert.That(selection.SelectedChain.Count, Is.EqualTo(0),
                "Swap mode must ignore a diagonal destination.");

            input.OnPointerUpEvent?.Invoke(Vector2.zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DraggingAdjacentHintMove_PreviewsAndCompletesSwap()
        {
            yield return LoadGameplayScene();

            BoardController board = Object.FindAnyObjectByType<BoardController>();
            ChainInputHandler input = Object.FindAnyObjectByType<ChainInputHandler>();
            ScoreController score = Object.FindAnyObjectByType<ScoreController>();
            BoardGravityController gravity = Object.FindAnyObjectByType<BoardGravityController>();

            Assert.That(board, Is.Not.Null);
            Assert.That(input, Is.Not.Null);
            Assert.That(score, Is.Not.Null);
            Assert.That(gravity, Is.Not.Null);
            Assert.That(board.TryFindHintMove(out PieceView first, out PieceView second), Is.True);
            Vector3 firstStart = first.transform.position;
            Vector3 secondStart = second.transform.position;

            Physics2D.SyncTransforms();
            input.OnPointerDownEvent?.Invoke(first.transform.position);
            yield return null;
            input.OnPointerDragEvent?.Invoke(second.transform.position);
            yield return new WaitForSeconds(0.18f);

            Assert.That(Vector3.Distance(first.transform.position, secondStart), Is.LessThan(0.02f));
            Assert.That(Vector3.Distance(second.transform.position, firstStart), Is.LessThan(0.02f));

            input.OnPointerUpEvent?.Invoke(secondStart);
            float timeout = Time.realtimeSinceStartup + 4f;
            while ((score.CurrentScore == 0 || gravity.IsResolving) && Time.realtimeSinceStartup < timeout)
                yield return null;
            Assert.That(score.CurrentScore, Is.GreaterThan(0));
            Assert.That(gravity.IsResolving, Is.False);
        }

        [UnityTest]
        public IEnumerator DraggingThreeMatchingPieces_ScoresFallsAndRefills()
        {
            yield return LoadGameplayScene();

            BoardController board = Object.FindAnyObjectByType<BoardController>();
            ChainSelectionController selection = Object.FindAnyObjectByType<ChainSelectionController>();
            ChainInputHandler input = Object.FindAnyObjectByType<ChainInputHandler>();
            ScoreController score = Object.FindAnyObjectByType<ScoreController>();
            BoardGravityController gravity = Object.FindAnyObjectByType<BoardGravityController>();

            Assert.That(board, Is.Not.Null);
            Assert.That(selection, Is.Not.Null);
            Assert.That(input, Is.Not.Null);
            Assert.That(score, Is.Not.Null);
            Assert.That(gravity, Is.Not.Null);
            // Keep one regression test for the optional legacy chain input;
            // the player-facing mode is covered by the adjacent-swap test above.
            selection.adjacentSwapMode = false;

            PieceView first = null;
            PieceView middle = null;
            PieceView last = null;

            for (int x = 0; x < board.Columns && middle == null; x++)
            {
                for (int y = 0; y < board.Rows && middle == null; y++)
                {
                    PieceView candidate = board.GetPieceAt(x, y);
                    if (candidate == null) continue;

                    PieceView[] matchingNeighbors = new PieceView[8];
                    int neighborCount = 0;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            if (!BoardController.AreAdjacent(x, y, x + dx, y + dy)) continue;
                            PieceView neighbor = board.GetPieceAt(x + dx, y + dy);
                            if (neighbor != null && neighbor.type == candidate.type)
                            {
                                matchingNeighbors[neighborCount++] = neighbor;
                            }
                        }
                    }

                    if (neighborCount >= 2)
                    {
                        first = matchingNeighbors[0];
                        middle = candidate;
                        last = matchingNeighbors[1];
                    }
                }
            }

            Assert.That(middle, Is.Not.Null,
                "The initialized board must expose at least one valid three-piece chain.");

            Physics2D.SyncTransforms();
            foreach (PieceView piece in new[] { first, middle, last })
            {
                Collider2D hit = Physics2D.OverlapPoint(piece.transform.position);
                Assert.That(hit, Is.Not.Null,
                    "Each normalized piece must keep a finger-sized collider at its visual center.");
                Assert.That(hit.GetComponent<PieceView>(), Is.EqualTo(piece));
            }

            input.OnPointerDownEvent?.Invoke(first.transform.position);
            yield return null;
            input.OnPointerDragEvent?.Invoke(middle.transform.position);
            yield return null;
            input.OnPointerDragEvent?.Invoke(last.transform.position);
            yield return null;

            Assert.That(selection.SelectedChain.Count, Is.EqualTo(3),
                "The live selection must contain the three dragged pieces.");

            input.OnPointerUpEvent?.Invoke(Vector2.zero);

            float timeoutAt = Time.realtimeSinceStartup + 4f;
            while ((gravity.IsResolving || score.CurrentScore == 0) &&
                   Time.realtimeSinceStartup < timeoutAt)
            {
                yield return null;
            }

            Assert.That(score.CurrentScore, Is.GreaterThan(0),
                "Completing a valid chain must award points.");
            Assert.That(gravity.IsResolving, Is.False,
                "Removal, fall and refill must finish.");

            int activePieces = 0;
            for (int x = 0; x < board.Columns; x++)
            {
                for (int y = 0; y < board.Rows; y++)
                {
                    PieceView piece = board.GetPieceAt(x, y);
                    Assert.That(piece, Is.Not.Null,
                        $"Grid position ({x}, {y}) must be refilled.");
                    if (piece.gameObject.activeInHierarchy) activePieces++;
                }
            }

            Assert.That(activePieces, Is.EqualTo(CountPlayableCells(board)),
                "A completed move must refill every playable board cell.");
        }
    }
}
