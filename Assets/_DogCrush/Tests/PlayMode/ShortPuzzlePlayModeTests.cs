using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DogCrush.Board;
using DogCrush.Core;
using JoinDog.App;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace DogCrush.Tests.PlayMode
{
    public class ShortPuzzlePlayModeTests
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        private static IEnumerator WaitForSettled(GameStateController state, string context)
        {
            float deadline = Time.unscaledTime + 25f;
            yield return null;
            while (!state.CanSelectPieces() && state.CurrentState != GameState.GameOver && Time.unscaledTime < deadline)
                yield return null;
            Assert.That(state.CurrentState, Is.EqualTo(GameState.Playing).Or.EqualTo(GameState.GameOver), context);
        }

        private static string DescribeBoard(BoardController board)
        {
            var rows = new List<string>();
            for (int y = board.Rows - 1; y >= 0; y--)
            {
                string row = "";
                for (int x = 0; x < board.Columns; x++)
                {
                    var piece = board.GetPieceAt(x, y);
                    row += piece == null ? "." : ((int)piece.type).ToString();
                }
                rows.Add(row);
            }
            return string.Join("/", rows);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator ForestPuzzle_ThreeLegalMovesPrepareAndCrossRaysAcrossBothCollectionPhases()
        {
            const string saveKey = "JoinDog_PlayerProgress_v1";
            bool hadSave = PlayerPrefs.HasKey(saveKey);
            string originalSave = PlayerPrefs.GetString(saveKey);
            var originalRandom = Random.state;
            bool originalMotion = AccessibilitySettings.ReducedMotion;
            var prefs = new Dictionary<string, int?>();
            foreach (string key in new[] {"DogCrush_HighScore", "DogCrush_UnlockedLevel", "DogCrush_LevelStars_18", "JoinDog_HazardHint_Lantern"})
                prefs[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : (int?)null;
            if (AppServices.Instance == null) new GameObject("PuzzleTestServices").AddComponent<AppServices>();
            var originalProgress = AppServices.Instance.Progress;
            int originalPending = AppServices.Instance.PendingMapAdvanceFromLevel;
            var progressProperty = typeof(AppServices).GetProperty("Progress");
            var pendingProperty = typeof(AppServices).GetProperty("PendingMapAdvanceFromLevel");
            try
            {
                PlayerPrefs.SetString(saveKey, JsonUtility.ToJson(new PlayerProgressData {
                    currentLevel = 18, unlockedLevel = 18, dogEnergy = 5,
                    dogEnergyUpdatedUtcTicks = DateTime.UtcNow.Ticks
                }));
                PlayerPrefs.SetInt("JoinDog_HazardHint_Lantern", 1);
                progressProperty.SetValue(AppServices.Instance, new PlayerProgressService());
                pendingProperty.SetValue(AppServices.Instance, 0);
                AppServices.Instance.GoToWorldMap();
                yield return null; yield return null;
                AppServices.Instance.StartLevel(18);
                yield return null; yield return null;
                var game = Object.FindAnyObjectByType<GameBootstrap>();
                var state = Object.FindAnyObjectByType<GameStateController>();
                var move = typeof(GameBootstrap).GetMethod("HandlePlayerMatch3Move", Private);
                var progress = typeof(GameBootstrap).GetField("objectiveProgress", Private);
                var moves = typeof(GameBootstrap).GetField("movesRemaining", Private);
                foreach (int seed in new[] {18, 1818, 1618})
                foreach (bool reduced in new[] {false, true})
                {
                    Random.InitState(seed);
                    AccessibilitySettings.ReducedMotion = reduced;
                    game.StartNewMatch();
                    yield return new WaitForSecondsRealtime(1.6f);
                    if (seed == 18 && !reduced)
                    {
                        var coaching = GameObject.Find("BriefCoaching")?.GetComponent<TMPro.TextMeshProUGUI>();
                        Assert.That(coaching, Is.Not.Null);
                        StringAssert.Contains("rayo de fila", coaching.text);
                        StringAssert.Contains("rayo de columna", coaching.text);
                        BoardPlayModeTests.CaptureGameplayState("short-puzzle-18-intro.png");
                    }
                    yield return new WaitForSecondsRealtime(1.5f);
                    var definition = (LevelDefinition)typeof(GameBootstrap).GetProperty("CurrentLevelDefinition", Private).GetValue(game);
                    Assert.That(game.IsRelaxedMode, Is.False);
                    Assert.That(definition.moveLimit, Is.EqualTo(8));
                    Assert.That(definition.openingRefillPieces, Is.EqualTo(new[] {PieceType.Dog, PieceType.Collar, PieceType.Bone, PieceType.Dog, PieceType.Bone, PieceType.Collar}), "Serialized puzzle prefix loads from the asset.");
                    Assert.That(definition.HasCollectionPhases, Is.True);
                    Assert.That(definition.firstCollectionPhaseAmount, Is.EqualTo(3));
                    Assert.That(definition.targetAmount, Is.EqualTo(6));
                    Assert.That(game.boardController.RemainingObstacleCount, Is.EqualTo(13));
                    Assert.That(game.boardController.FindMatches().Count, Is.Zero);
                    var board = game.boardController;
                    var row = board.GetPieceAt(4, 3);
                    Assert.That(row.type, Is.EqualTo(PieceType.Bone));
                    Assert.That(board.TryGetSpecialCreationPreview(row, board.GetPieceAt(4, 4), out _, out var kind), Is.True);
                    Assert.That(kind, Is.EqualTo(PieceSpecialType.RowBlast));
                    Assert.That(board.TrySwapAndFindMatches(row, board.GetPieceAt(4, 4), out var matches), Is.True);
                    move.Invoke(game, new object[] {matches});
                    yield return WaitForSettled(state, "First row preparation settles.");
                    Assert.That(state.CanSelectPieces(), Is.True, $"Row seed={seed}, reduced={reduced}.");
                    Assert.That(row.SpecialType, Is.EqualTo(PieceSpecialType.RowBlast));
                    Assert.That((int)progress.GetValue(game), Is.EqualTo(3), "Bones complete phase one; dogs still pending.");
                    Assert.That(row.gridX, Is.EqualTo(4)); Assert.That(row.gridY, Is.EqualTo(4));
                    var column = board.GetPieceAt(3, 7);
                    Assert.That(column.type, Is.EqualTo(PieceType.Food), $"Food preparation seed={seed}, reduced={reduced}.");
                    Assert.That(board.TryGetSpecialCreationPreview(column, board.GetPieceAt(4, 7), out _, out kind), Is.True);
                    Assert.That(kind, Is.EqualTo(PieceSpecialType.ColumnBlast), $"Column preview seed={seed}, reduced={reduced}; grid={DescribeBoard(board)}");
                    Assert.That(board.TrySwapAndFindMatches(column, board.GetPieceAt(4, 7), out matches), Is.True);
                    move.Invoke(game, new object[] {matches});
                    Debug.Log($"Column immediate progress={progress.GetValue(game)}, matches={matches.Count}, grid={DescribeBoard(board)}");
                    yield return WaitForSettled(state, "Column preparation settles.");
                    Assert.That(state.CanSelectPieces(), Is.True, $"Column seed={seed}, reduced={reduced}; state={state.CurrentState}, progress={progress.GetValue(game)}, grid={DescribeBoard(board)}.");
                    Assert.That((int)progress.GetValue(game), Is.EqualTo(3), "Food prepares a ray without skipping phase two.");
                    Assert.That(column.SpecialType, Is.EqualTo(PieceSpecialType.ColumnBlast));
                    Assert.That(column.gridX, Is.EqualTo(4)); Assert.That(column.gridY, Is.EqualTo(5));
                    Assert.That(BoardController.ClassifySpecialPair(row.SpecialType, column.SpecialType), Is.EqualTo(SpecialComboKind.CrossBlast));
                    Assert.That((int)moves.GetValue(game), Is.EqualTo(6));
                    if (seed == 18) BoardPlayModeTests.CaptureGameplayState("short-puzzle-18-prepared" + (reduced ? "-reduced" : "") + ".png");
                    Assert.That(board.TrySwapAndFindMatches(row, column, out matches), Is.True);
                    move.Invoke(game, new object[] {matches});
                    yield return new WaitForSecondsRealtime(.08f);
                    if (seed == 18) BoardPlayModeTests.CaptureGameplayState("short-puzzle-18-cross" + (reduced ? "-reduced" : "") + ".png");
                    yield return WaitForSettled(state, "Cross finishes its cascades and final bonus.");
                    Assert.That(state.CurrentState, Is.EqualTo(GameState.GameOver), $"Three-move victory seed={seed}, reduced={reduced}.");
                    Assert.That((int)progress.GetValue(game), Is.EqualTo(6));
                    Assert.That((int)moves.GetValue(game), Is.EqualTo(5), "Cascades and final celebration do not spend extra moves.");
                    Assert.That(AppServices.Instance.Progress.GetStars(18), Is.EqualTo(3));
                    Debug.Log($"Short forest puzzle seed={seed}, reduced={reduced}: 3 legal moves, 5 moves left, score={game.scoreController.CurrentScore}, phases=6/6.");
                }
                // A separate disposable progress fixture verifies budget exhaustion.
                // Use legal food matches without either mission type, so a cascade
                // cannot accidentally satisfy the collection objective.
                PlayerPrefs.SetString(saveKey, JsonUtility.ToJson(new PlayerProgressData {
                    currentLevel = 18, unlockedLevel = 18, dogEnergy = 5,
                    dogEnergyUpdatedUtcTicks = DateTime.UtcNow.Ticks
                }));
                progressProperty.SetValue(AppServices.Instance, new PlayerProgressService());
                game.StartNewMatch();
                yield return new WaitForSecondsRealtime(3.1f);
                var failureBoard = game.boardController;
                var nonMissionPool = new[] {PieceType.Food, PieceType.Collar, PieceType.Duck};
                failureBoard.config.typeCount = 3;
                failureBoard.config.activePieceTypes = nonMissionPool;
                for (int turn = 0; turn < 8; turn++)
                {
                    for (int x = 0; x < failureBoard.Columns; x++)
                    for (int y = 0; y < failureBoard.Rows; y++)
                    {
                        var piece = failureBoard.GetPieceAt(x, y);
                        if (piece == null) continue;
                        piece.type = nonMissionPool[(x + 2*y) % nonMissionPool.Length];
                        piece.SetSpecial(PieceSpecialType.None);
                    }
                    failureBoard.GetPieceAt(1, 3).type = PieceType.Collar;
                    failureBoard.GetPieceAt(5, 3).type = PieceType.Collar;
                    failureBoard.GetPieceAt(2, 3).type = PieceType.Food;
                    failureBoard.GetPieceAt(4, 3).type = PieceType.Food;
                    failureBoard.GetPieceAt(3, 3).type = PieceType.Duck;
                    failureBoard.GetPieceAt(3, 2).type = PieceType.Food;
                    Assert.That(failureBoard.FindMatches().Count, Is.Zero, "Failure fixture starts settled.");
                    Assert.That(failureBoard.TrySwapAndFindMatches(failureBoard.GetPieceAt(3, 2), failureBoard.GetPieceAt(3, 3), out var failureMatches), Is.True);
                    move.Invoke(game, new object[] {failureMatches});
                    yield return WaitForSettled(state, "Budget exhaustion resolves real matches/cascades.");
                    Assert.That((int)moves.GetValue(game), Is.EqualTo(7-turn));
                    Assert.That((int)progress.GetValue(game), Is.Zero);
                    Assert.That(state.CurrentState, Is.EqualTo(turn < 7 ? GameState.Playing : GameState.GameOver));
                }
                Assert.That(AppServices.Instance.Progress.DogEnergy, Is.EqualTo(4), "One defeat spends one energy.");
                Assert.That(AppServices.Instance.Progress.GetStars(18), Is.Zero);
                Assert.That(AppServices.Instance.Progress.EarnedUnlockedLevel, Is.EqualTo(18));
                Assert.That(AppServices.Instance.Progress.Treats, Is.Zero);
                Assert.That(AppServices.Instance.Progress.PawPrints, Is.GreaterThan(0), "Normal campaign matches preserve their existing paw-print earnings.");
                game.RestartGame();
                Assert.That((int)progress.GetValue(game), Is.Zero);
                Assert.That((int)moves.GetValue(game), Is.EqualTo(8));
                Assert.That(game.boardController.GetPieceAt(4, 3).type, Is.EqualTo(PieceType.Bone));
                Assert.That(game.boardController.FindMatches().Count, Is.Zero);
            }
            finally
            {
                AppServices.Instance.GoToMainMenu();
                progressProperty.SetValue(AppServices.Instance, originalProgress);
                pendingProperty.SetValue(AppServices.Instance, originalPending);
                if (hadSave) PlayerPrefs.SetString(saveKey, originalSave); else PlayerPrefs.DeleteKey(saveKey);
                foreach (var pair in prefs)
                    if (pair.Value.HasValue) PlayerPrefs.SetInt(pair.Key, pair.Value.Value); else PlayerPrefs.DeleteKey(pair.Key);
                AccessibilitySettings.ReducedMotion = originalMotion;
                Random.state = originalRandom;
                PlayerPrefs.Save();
            }
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator CoastPuzzle_AreaAndRowClearAllTwoLayerSandInThreeLegalMoves()
        {
            const string saveKey = "JoinDog_PlayerProgress_v1";
            bool hadSave = PlayerPrefs.HasKey(saveKey);
            string originalSave = PlayerPrefs.GetString(saveKey);
            var originalRandom = Random.state;
            bool originalMotion = AccessibilitySettings.ReducedMotion;
            var prefs = new Dictionary<string, int?>();
            foreach (string key in new[] {"DogCrush_HighScore", "DogCrush_UnlockedLevel", "DogCrush_LevelStars_38", "JoinDog_HazardHint_Sand"})
                prefs[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : (int?)null;
            if (AppServices.Instance == null) new GameObject("PuzzleTestServices").AddComponent<AppServices>();
            var originalProgress = AppServices.Instance.Progress;
            int originalPending = AppServices.Instance.PendingMapAdvanceFromLevel;
            var progressProperty = typeof(AppServices).GetProperty("Progress");
            var pendingProperty = typeof(AppServices).GetProperty("PendingMapAdvanceFromLevel");
            try
            {
                PlayerPrefs.SetString(saveKey, JsonUtility.ToJson(new PlayerProgressData {
                    currentLevel = 38, unlockedLevel = 38, dogEnergy = 5,
                    dogEnergyUpdatedUtcTicks = DateTime.UtcNow.Ticks
                }));
                PlayerPrefs.SetInt("JoinDog_HazardHint_Sand", 1);
                progressProperty.SetValue(AppServices.Instance, new PlayerProgressService());
                pendingProperty.SetValue(AppServices.Instance, 0);
                AppServices.Instance.GoToWorldMap();
                yield return null; yield return null;
                AppServices.Instance.StartLevel(38);
                yield return null; yield return null;
                var game = Object.FindAnyObjectByType<GameBootstrap>();
                var state = Object.FindAnyObjectByType<GameStateController>();
                var move = typeof(GameBootstrap).GetMethod("HandlePlayerMatch3Move", Private);
                var progress = typeof(GameBootstrap).GetField("objectiveProgress", Private);
                var moves = typeof(GameBootstrap).GetField("movesRemaining", Private);
                foreach (int seed in new[] {38, 3838, 1638})
                foreach (bool reduced in new[] {false, true})
                {
                    Random.InitState(seed); AccessibilitySettings.ReducedMotion = reduced;
                    game.StartNewMatch();
                    yield return new WaitForSecondsRealtime(1.6f);
                    if (seed == 38 && !reduced)
                    {
                        var coaching = GameObject.Find("BriefCoaching")?.GetComponent<TMPro.TextMeshProUGUI>();
                        Assert.That(coaching, Is.Not.Null);
                        StringAssert.Contains("20 arenas", coaching.text);
                        BoardPlayModeTests.CaptureGameplayState("short-puzzle-38-intro.png");
                    }
                    yield return new WaitForSecondsRealtime(1.5f);
                    var definition = (LevelDefinition)typeof(GameBootstrap).GetProperty("CurrentLevelDefinition", Private).GetValue(game);
                    var board = game.boardController;
                    Assert.That(game.IsRelaxedMode, Is.False);
                    Assert.That(definition.moveLimit, Is.EqualTo(8));
                    Assert.That(definition.objectiveType, Is.EqualTo(LevelObjectiveType.ClearObstacles));
                    Assert.That(definition.secondaryTargetScore, Is.Zero);
                    Assert.That(definition.obstacleDurability, Is.EqualTo(2));
                    Assert.That(definition.boardShape, Is.EqualTo(BoardShape.Rounded));
                    Assert.That(board.RemainingObstacleCount, Is.EqualTo(20));
                    Assert.That(board.config.GetActivePieceTypes(), Does.Contain(PieceType.Frisbee));
                    Assert.That(board.FindMatches().Count, Is.Zero);
                    var area = board.GetPieceAt(4, 3);
                    Assert.That(area.type, Is.EqualTo(PieceType.Bone));
                    Assert.That(board.TryGetSpecialCreationPreview(area, board.GetPieceAt(4, 4), out _, out var kind), Is.True);
                    Assert.That(kind, Is.EqualTo(PieceSpecialType.AreaBlast));
                    Assert.That(board.TrySwapAndFindMatches(area, board.GetPieceAt(4, 4), out var matches), Is.True);
                    move.Invoke(game, new object[] {matches});
                    yield return WaitForSettled(state, "Area preparation settles.");
                    Assert.That(state.CanSelectPieces(), Is.True, $"Area seed={seed}, reduced={reduced}, grid={DescribeBoard(board)}");
                    Assert.That(area.SpecialType, Is.EqualTo(PieceSpecialType.AreaBlast));
                    Assert.That(board.IsCoastTideUsed(4, 6), Is.True, "Preparing the area on the wave weakens its row once.");
                    Assert.That(area.gridX, Is.EqualTo(4)); Assert.That(area.gridY, Is.EqualTo(4));
                    Assert.That(board.RemainingObstacleCount, Is.GreaterThan(0));
                    var row = board.GetPieceAt(4, 6);
                    Assert.That(row.type, Is.EqualTo(PieceType.Food));
                    Assert.That(board.TryGetSpecialCreationPreview(row, board.GetPieceAt(4, 5), out _, out kind), Is.True);
                    Assert.That(kind, Is.EqualTo(PieceSpecialType.RowBlast), $"Row seed={seed}, reduced={reduced}, grid={DescribeBoard(board)}");
                    Assert.That(board.TrySwapAndFindMatches(row, board.GetPieceAt(4, 5), out matches), Is.True);
                    move.Invoke(game, new object[] {matches});
                    yield return WaitForSettled(state, "Row preparation settles.");
                    Assert.That(state.CanSelectPieces(), Is.True);
                    Assert.That(board.RemainingObstacleCount, Is.GreaterThan(0), "Pair is needed to finish sand.");
                    Assert.That(row.SpecialType, Is.EqualTo(PieceSpecialType.RowBlast));
                    Assert.That(row.gridX, Is.EqualTo(4)); Assert.That(row.gridY, Is.EqualTo(5));
                    Assert.That(BoardController.ClassifySpecialPair(area.SpecialType, row.SpecialType), Is.EqualTo(SpecialComboKind.WideRow));
                    Assert.That((int)moves.GetValue(game), Is.EqualTo(6));
                    if (seed == 38) BoardPlayModeTests.CaptureGameplayState("short-puzzle-38-prepared" + (reduced ? "-reduced" : "") + ".png");
                    Assert.That(board.TrySwapAndFindMatches(area, row, out matches), Is.True);
                    move.Invoke(game, new object[] {matches});
                    yield return new WaitForSecondsRealtime(.08f);
                    if (seed == 38) BoardPlayModeTests.CaptureGameplayState("short-puzzle-38-pair" + (reduced ? "-reduced" : "") + ".png");
                    yield return WaitForSettled(state, "Wide row resolves and awards victory.");
                    Assert.That(board.RemainingObstacleCount, Is.Zero, $"All sand seed={seed}, reduced={reduced}.");
                    Assert.That((int)progress.GetValue(game), Is.EqualTo(20));
                    Assert.That(state.CurrentState, Is.EqualTo(GameState.GameOver));
                    Assert.That((int)moves.GetValue(game), Is.EqualTo(5));
                    Assert.That(AppServices.Instance.Progress.GetStars(38), Is.EqualTo(3));
                    Assert.That(AppServices.Instance.Progress.DogEnergy, Is.EqualTo(5));
                    Debug.Log($"Short coast puzzle seed={seed}, reduced={reduced}: 3 legal moves, 5 moves left, score={game.scoreController.CurrentScore}, sand=0/20.");
                }
                game.RestartGame();
                Assert.That((int)progress.GetValue(game), Is.Zero);
                Assert.That((int)moves.GetValue(game), Is.EqualTo(8));
                Assert.That(game.boardController.RemainingObstacleCount, Is.EqualTo(20));
                Assert.That(game.boardController.FindMatches().Count, Is.Zero);
                Assert.That(game.boardController.GetPieceAt(4, 3).type, Is.EqualTo(PieceType.Bone));
            }
            finally
            {
                AppServices.Instance.GoToMainMenu();
                progressProperty.SetValue(AppServices.Instance, originalProgress);
                pendingProperty.SetValue(AppServices.Instance, originalPending);
                if (hadSave) PlayerPrefs.SetString(saveKey, originalSave); else PlayerPrefs.DeleteKey(saveKey);
                foreach (var pair in prefs)
                    if (pair.Value.HasValue) PlayerPrefs.SetInt(pair.Key, pair.Value.Value); else PlayerPrefs.DeleteKey(pair.Key);
                AccessibilitySettings.ReducedMotion = originalMotion;
                Random.state = originalRandom;
                PlayerPrefs.Save();
            }
        }
        [UnityTest, Timeout(240000)]
        public IEnumerator AuroraPuzzle_TwoFivePieceSpecialsClearLanternsInThreeLegalMoves()
        {
            const string saveKey = "JoinDog_PlayerProgress_v1";
            bool hadSave = PlayerPrefs.HasKey(saveKey);
            string originalSave = PlayerPrefs.GetString(saveKey);
            var originalRandom = Random.state;
            bool originalMotion = AccessibilitySettings.ReducedMotion;
            var prefs = new Dictionary<string, int?>();
            foreach (string key in new[] {"DogCrush_HighScore", "DogCrush_UnlockedLevel", "DogCrush_LevelStars_58", "JoinDog_HazardHint_Lantern"})
                prefs[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : (int?)null;
            if (AppServices.Instance == null) new GameObject("PuzzleTestServices").AddComponent<AppServices>();
            var originalProgress = AppServices.Instance.Progress;
            int originalPending = AppServices.Instance.PendingMapAdvanceFromLevel;
            var progressProperty = typeof(AppServices).GetProperty("Progress");
            var pendingProperty = typeof(AppServices).GetProperty("PendingMapAdvanceFromLevel");
            try
            {
                PlayerPrefs.SetString(saveKey, JsonUtility.ToJson(new PlayerProgressData {
                    currentLevel = 58, unlockedLevel = 58, dogEnergy = 5,
                    dogEnergyUpdatedUtcTicks = DateTime.UtcNow.Ticks
                }));
                PlayerPrefs.SetInt("JoinDog_HazardHint_Lantern", 1);
                progressProperty.SetValue(AppServices.Instance, new PlayerProgressService());
                pendingProperty.SetValue(AppServices.Instance, 0);
                AppServices.Instance.GoToWorldMap();
                yield return null; yield return null;
                AppServices.Instance.StartLevel(58);
                yield return null; yield return null;
                var game = Object.FindAnyObjectByType<GameBootstrap>();
                var state = Object.FindAnyObjectByType<GameStateController>();
                var move = typeof(GameBootstrap).GetMethod("HandlePlayerMatch3Move", Private);
                var progress = typeof(GameBootstrap).GetField("objectiveProgress", Private);
                var moves = typeof(GameBootstrap).GetField("movesRemaining", Private);
                foreach (int seed in new[] {58, 5858, 1658})
                foreach (bool reduced in new[] {false, true})
                {
                    Random.InitState(seed); AccessibilitySettings.ReducedMotion = reduced;
                    game.StartNewMatch();
                    yield return new WaitForSecondsRealtime(1.6f);
                    if (seed == 58 && !reduced)
                    {
                        var coaching = GameObject.Find("BriefCoaching")?.GetComponent<TMPro.TextMeshProUGUI>();
                        Assert.That(coaching, Is.Not.Null);
                        StringAssert.Contains("24 faroles", coaching.text);
                        BoardPlayModeTests.CaptureGameplayState("short-puzzle-58-intro.png");
                    }
                    yield return new WaitForSecondsRealtime(1.5f);
                    var definition = (LevelDefinition)typeof(GameBootstrap).GetProperty("CurrentLevelDefinition", Private).GetValue(game);
                    var board = game.boardController;
                    Assert.That(game.IsRelaxedMode, Is.False);
                    Assert.That(definition.moveLimit, Is.EqualTo(8));
                    Assert.That(definition.objectiveType, Is.EqualTo(LevelObjectiveType.ClearObstacles));
                    Assert.That(definition.secondaryTargetScore, Is.Zero);
                    Assert.That(definition.obstacleDurability, Is.EqualTo(2));
                    Assert.That(definition.boardShape, Is.EqualTo(BoardShape.Full));
                    Assert.That(board.RemainingObstacleCount, Is.EqualTo(24));
                    Assert.That(board.config.GetActivePieceTypes(), Is.EquivalentTo(new[] {PieceType.Dog, PieceType.Bone, PieceType.Ball, PieceType.Food, PieceType.Collar, PieceType.Duck, PieceType.Rope}));
                    Assert.That(board.FindMatches().Count, Is.Zero);
                    var area = board.GetPieceAt(3, 4);
                    Assert.That(area.type, Is.EqualTo(PieceType.Bone));
                    Assert.That(board.TryGetSpecialCreationPreview(area, board.GetPieceAt(4, 4), out _, out var kind), Is.True);
                    Assert.That(kind, Is.EqualTo(PieceSpecialType.ColorBurst));
                    Assert.That(board.TrySwapAndFindMatches(area, board.GetPieceAt(4, 4), out var matches), Is.True);
                    move.Invoke(game, new object[] {matches});
                    yield return WaitForSettled(state, "First color preparation settles.");
                    Assert.That(state.CanSelectPieces(), Is.True, $"Area seed={seed}, reduced={reduced}, grid={DescribeBoard(board)}");
                    Assert.That(area.SpecialType, Is.EqualTo(PieceSpecialType.ColorBurst));
                    Assert.That(area.gridX, Is.EqualTo(4)); Assert.That(area.gridY, Is.EqualTo(2));
                    Assert.That(board.RemainingObstacleCount, Is.GreaterThan(0));
                    var row = board.GetPieceAt(4, 4);
                    Assert.That(row.type, Is.EqualTo(PieceType.Food));
                    Assert.That(board.TryGetSpecialCreationPreview(row, board.GetPieceAt(4, 3), out _, out kind), Is.True);
                    Assert.That(kind, Is.EqualTo(PieceSpecialType.ColorBurst), $"Row seed={seed}, reduced={reduced}, grid={DescribeBoard(board)}");
                    Assert.That(board.TrySwapAndFindMatches(row, board.GetPieceAt(4, 3), out matches), Is.True);
                    move.Invoke(game, new object[] {matches});
                    yield return WaitForSettled(state, "Second color preparation settles.");
                    Assert.That(state.CanSelectPieces(), Is.True);
                    Assert.That(board.RemainingObstacleCount, Is.GreaterThan(0), "Pair is needed to finish lanterns.");
                    Assert.That(row.SpecialType, Is.EqualTo(PieceSpecialType.ColorBurst));
                    Assert.That(row.gridX, Is.EqualTo(4)); Assert.That(row.gridY, Is.EqualTo(3));
                    Assert.That((int)moves.GetValue(game), Is.EqualTo(6));
                    if (seed == 58) BoardPlayModeTests.CaptureGameplayState("short-puzzle-58-prepared" + (reduced ? "-reduced" : "") + ".png");
                    Assert.That(board.TrySwapAndFindMatches(area, row, out matches), Is.True);
                    move.Invoke(game, new object[] {matches});
                    yield return new WaitForSecondsRealtime(.08f);
                    if (seed == 58) BoardPlayModeTests.CaptureGameplayState("short-puzzle-58-pair" + (reduced ? "-reduced" : "") + ".png");
                    yield return WaitForSettled(state, "Nova resolves and awards victory.");
                    Assert.That(board.RemainingObstacleCount, Is.Zero, $"All lanterns seed={seed}, reduced={reduced}.");
                    Assert.That((int)progress.GetValue(game), Is.EqualTo(24));
                    Assert.That(state.CurrentState, Is.EqualTo(GameState.GameOver));
                    Assert.That((int)moves.GetValue(game), Is.EqualTo(5));
                    Assert.That(AppServices.Instance.Progress.GetStars(58), Is.EqualTo(3));
                    Assert.That(AppServices.Instance.Progress.DogEnergy, Is.EqualTo(5));
                    Debug.Log($"Short lantern puzzle seed={seed}, reduced={reduced}: 3 legal moves, 5 moves left, score={game.scoreController.CurrentScore}, lanterns=0/24.");
                }
                game.RestartGame();
                Assert.That((int)progress.GetValue(game), Is.Zero);
                Assert.That((int)moves.GetValue(game), Is.EqualTo(8));
                Assert.That(game.boardController.RemainingObstacleCount, Is.EqualTo(24));
                Assert.That(game.boardController.FindMatches().Count, Is.Zero);
                Assert.That(game.boardController.GetPieceAt(3, 4).type, Is.EqualTo(PieceType.Bone));
            }
            finally
            {
                AppServices.Instance.GoToMainMenu();
                progressProperty.SetValue(AppServices.Instance, originalProgress);
                pendingProperty.SetValue(AppServices.Instance, originalPending);
                if (hadSave) PlayerPrefs.SetString(saveKey, originalSave); else PlayerPrefs.DeleteKey(saveKey);
                foreach (var pair in prefs)
                    if (pair.Value.HasValue) PlayerPrefs.SetInt(pair.Key, pair.Value.Value); else PlayerPrefs.DeleteKey(pair.Key);
                AccessibilitySettings.ReducedMotion = originalMotion;
                Random.state = originalRandom;
                PlayerPrefs.Save();
            }
        }
    }
}
