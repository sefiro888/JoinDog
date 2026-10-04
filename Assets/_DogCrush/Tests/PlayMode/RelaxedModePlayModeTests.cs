using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DogCrush.Board;
using DogCrush.Core;
using JoinDog.App;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace DogCrush.Tests.PlayMode
{
    public class RelaxedModePlayModeTests
    {
        private const string SaveKey = "JoinDog_PlayerProgress_v1";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private PlayerProgressService originalProgress;
        private string originalSave;
        private bool hadSave;
        private Random.State originalRandom;
        private bool originalMotion;
        private int originalPendingAdvance;
        private readonly Dictionary<string, int?> intPrefs = new Dictionary<string, int?>();

        [SetUp]
        public void SetUp()
        {
            originalRandom = Random.state;
            originalMotion = AccessibilitySettings.ReducedMotion;
            hadSave = PlayerPrefs.HasKey(SaveKey);
            originalSave = PlayerPrefs.GetString(SaveKey);
            foreach (string key in new[] {"DogCrush_HighScore", "DogCrush_UnlockedLevel", "DogCrush_LevelStars_1", "JoinDog_SwapTutorialSeen", "JoinDog_HazardHint_Vine"})
                intPrefs[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : (int?)null;
            if (AppServices.Instance == null) new GameObject("RelaxedTestServices").AddComponent<AppServices>();
            originalProgress = AppServices.Instance.Progress;
            originalPendingAdvance = AppServices.Instance.PendingMapAdvanceFromLevel;
            SetServiceProperty("PendingMapAdvanceFromLevel", 0);
            // This disposable save is restored in TearDown and by the external registry wrapper.
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(new PlayerProgressData {
                currentLevel = 7, unlockedLevel = 18, treats = 73, pawPrints = 91,
                pawBoosters = 4, boneBoosters = 3, foodBoosters = 2, magicBoneBoosters = 2,
                dogEnergy = 0, dogEnergyUpdatedUtcTicks = DateTime.UtcNow.Ticks,
                dailyDateKey = DateTime.UtcNow.ToString("yyyy-MM-dd")
            }));
            SetServiceProperty("Progress", new PlayerProgressService());
            PlayerPrefs.SetInt("DogCrush_HighScore", 0);
            PlayerPrefs.SetInt("JoinDog_SwapTutorialSeen", 1);
            PlayerPrefs.SetInt("JoinDog_HazardHint_Vine", 1);
            Random.InitState(1501);
        }

        [TearDown]
        public void TearDown()
        {
            AppServices.Instance.GoToMainMenu();
            SetServiceProperty("Progress", originalProgress);
            SetServiceProperty("PendingMapAdvanceFromLevel", originalPendingAdvance);
            if (hadSave) PlayerPrefs.SetString(SaveKey, originalSave); else PlayerPrefs.DeleteKey(SaveKey);
            foreach (var pair in intPrefs)
                if (pair.Value.HasValue) PlayerPrefs.SetInt(pair.Key, pair.Value.Value); else PlayerPrefs.DeleteKey(pair.Key);
            PlayerPrefs.Save();
            AccessibilitySettings.ReducedMotion = originalMotion;
            Random.state = originalRandom;
            intPrefs.Clear();
        }

        private static void SetServiceProperty(string name, object value) =>
            typeof(AppServices).GetProperty(name).SetValue(AppServices.Instance, value);

        private static PlayerProgressData Data => (PlayerProgressData)typeof(PlayerProgressService)
            .GetField("data", Private).GetValue(AppServices.Instance.Progress);

        private static void Invoke(GameBootstrap game, string method) =>
            typeof(GameBootstrap).GetMethod(method, Private).Invoke(game, null);

        private static TMP_Text Text(string name) => Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .ArrayFind(label => label.name == name);

        private static Button Button(string name) => Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .ArrayFind(button => button.name == name);

        private static IEnumerator OpenPreview(int level)
        {
            AppServices.Instance.GoToWorldMap();
            yield return null; yield return null;
            yield return new WaitForSecondsRealtime(.4f);
            var map = Object.FindAnyObjectByType<WorldMapScreenController>();
            Assert.That(map, Is.Not.Null);
            typeof(WorldMapScreenController).GetMethod("ShowLevelPreview", Private).Invoke(map, new object[] {level});
            yield return null;
        }

        private static IEnumerator FinishWithLegalSwaps(GameBootstrap game)
        {
            var move = typeof(GameBootstrap).GetMethod("HandlePlayerMatch3Move", Private);
            var state = Object.FindAnyObjectByType<GameStateController>();
            float deadline = Time.unscaledTime + 90f;
            int moves = 0;
            while (state.CurrentState != GameState.GameOver && Time.unscaledTime < deadline && moves < 60)
            {
                if (!state.CanSelectPieces()) { yield return null; continue; }
                Assert.That(game.boardController.TryFindHintMoveForObjective(PieceType.None, false, out var a, out var b), Is.True);
                Assert.That(game.boardController.TrySwapAndFindMatches(a, b, out var matches), Is.True);
                move.Invoke(game, new object[] {matches});
                moves++; yield return null;
            }
            Assert.That(state.CurrentState, Is.EqualTo(GameState.GameOver), $"Real completion after {moves} swaps.");
            Assert.That(game.scoreController.CurrentScore, Is.GreaterThanOrEqualTo(((LevelDefinition)typeof(GameBootstrap).GetProperty("CurrentLevelDefinition", Private).GetValue(game)).targetScore));
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator RelaxedEntry_RealCompletionRestartAndCampaignReturn_KeepRewardsSeparate()
        {
            foreach (bool reduced in new[] {false, true})
            {
                AccessibilitySettings.ReducedMotion = reduced;
                Data.relaxedCompletedLevels.Clear();
                yield return OpenPreview(1);
                Assert.That(Button("RelaxLevel").GetComponentInChildren<TextMeshProUGUI>().text, Does.StartWith("SIN RELOJ"));
                if (!reduced) BoardPlayModeTests.CaptureGameplayState("relaxed-preview.png");
                Button("RelaxLevel").onClick.Invoke();
                yield return null; yield return null;
                yield return new WaitForSecondsRealtime(1.6f);
                var game = Object.FindAnyObjectByType<GameBootstrap>();
                var state = Object.FindAnyObjectByType<GameStateController>();
                string baseline = JsonUtility.ToJson(Data);
                int legacyUnlocked=PlayerPrefs.GetInt("DogCrush_UnlockedLevel");
                int legacyStars=PlayerPrefs.GetInt("DogCrush_LevelStars_1");
                Assert.That(game.IsRelaxedMode, Is.True);
                Assert.That(state.CanSelectPieces(), Is.True, "Energy zero must permit a relaxed walk.");
                Assert.That(game.gameTimer.IsRunning, Is.False);
                Assert.That(Text("TimerText_RT").text, Is.EqualTo("SIN RELOJ"));
                Assert.That(Text("ObjectiveStarHint_RT").text, Is.EqualTo("PASEO · SIN ESTRELLAS"));
                Assert.That(Data.currentLevel, Is.EqualTo(7));
                Assert.That(game.scoreController.PersistHighScore, Is.False);
                if (!reduced) BoardPlayModeTests.CaptureGameplayState("relaxed-hud.png");
                // Expiry callback and a settled board with zero remaining time cannot end a walk.
                typeof(GameTimer).GetProperty("RemainingTime").SetValue(game.gameTimer, 0f);
                game.gameTimer.OnTimerExpired.Invoke();
                Assert.That(state.CanSelectPieces(),Is.True,"Expiry callback is ignored in a walk.");
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(state.CanSelectPieces(), Is.True);
                Assert.That(game.gameTimer.AddTime(10f), Is.Zero);
                Assert.That(game.gameTimer.RemainingTime, Is.Zero);
                // Keep zero time throughout real swaps: their settled callbacks must still allow play.
                Invoke(game, "UseShuffleBooster");
                Assert.That((int)typeof(GameBootstrap).GetField("levelPawBoosters", Private).GetValue(game), Is.Zero);
                var boardPiece = game.boardController.GetPieceAt(0, 0);
                Invoke(game, "UseShuffleBooster");
                Assert.That(game.boardController.GetPieceAt(0, 0), Is.SameAs(boardPiece), "Exhausted local help cannot spend stored paws.");
                Invoke(game, "UseFoodBooster");
                Assert.That(JsonUtility.ToJson(Data), Is.EqualTo(baseline));
                yield return FinishWithLegalSwaps(game);
                yield return new WaitForSecondsRealtime(.8f);
                Assert.That(AppServices.Instance.Progress.IsRelaxedCompleted(1), Is.True);
                Assert.That(AppServices.Instance.Progress.GetStars(1), Is.Zero);
                Assert.That(AppServices.Instance.Progress.GetBestScore(1), Is.Zero);
                Assert.That(PlayerPrefs.GetInt("DogCrush_HighScore"), Is.Zero);
                Assert.That(PlayerPrefs.GetInt("DogCrush_UnlockedLevel"), Is.EqualTo(legacyUnlocked));
                Assert.That(PlayerPrefs.GetInt("DogCrush_LevelStars_1"), Is.EqualTo(legacyStars));
                Assert.That(AppServices.Instance.PendingMapAdvanceFromLevel, Is.Zero);
                var saved = JsonUtility.FromJson<PlayerProgressData>(PlayerPrefs.GetString(SaveKey));
                Assert.That(saved.relaxedCompletedLevels, Is.EquivalentTo(new[] {1}));
                saved.relaxedCompletedLevels.Clear();
                Assert.That(JsonUtility.ToJson(saved), Is.EqualTo(baseline), "Only the relaxed mark may change in the campaign save.");
                Assert.That(Text("GOTitle").text, Is.EqualTo("¡PASEO COMPLETADO!"));
                BoardPlayModeTests.CaptureGameplayState("relaxed-result" + (reduced ? "-reduced" : "") + ".png");
                Button("PlayAgainBtn_RT").onClick.Invoke();
                yield return new WaitForSecondsRealtime(1.6f);
                Assert.That(state.CanSelectPieces(), Is.True);
                Assert.That(game.gameTimer.IsRunning, Is.False);
                Assert.That(Data.dogEnergy, Is.Zero);
                SetServiceProperty("Progress", new PlayerProgressService());
                Assert.That(AppServices.Instance.Progress.IsRelaxedCompleted(1), Is.True, "Mark survives service reload.");
                yield return OpenPreview(1);
                Assert.That(AppServices.Instance.SelectedRelaxedMode, Is.False);
                Assert.That(Button("RelaxLevel").GetComponentInChildren<TextMeshProUGUI>().text, Does.StartWith("PASEO HECHO"));
                if (reduced) BoardPlayModeTests.CaptureGameplayState("relaxed-preview-completed.png");
            }
            Data.dogEnergy = 5;
            Button("PlayLevel").onClick.Invoke();
            yield return null; yield return null;
            yield return new WaitForSecondsRealtime(1.6f);
            var normal = Object.FindAnyObjectByType<GameBootstrap>();
            Assert.That(normal.IsRelaxedMode, Is.False);
            Assert.That(normal.gameTimer.IsRunning, Is.True);
            Assert.That(normal.scoreController.PersistHighScore, Is.True);
            float time = normal.gameTimer.RemainingTime;
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(normal.gameTimer.RemainingTime, Is.LessThan(time));
            yield return FinishWithLegalSwaps(normal);
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(AppServices.Instance.Progress.GetStars(1), Is.GreaterThan(0));
            Assert.That(AppServices.Instance.Progress.GetBestScore(1), Is.GreaterThan(0));
            Assert.That(Data.treats, Is.GreaterThan(73));
            Assert.That(Data.totalMatches, Is.GreaterThan(0));
            Assert.That(PlayerPrefs.GetInt("DogCrush_HighScore"), Is.GreaterThan(0));
            Assert.That(AppServices.Instance.Progress.IsRelaxedCompleted(1), Is.True);
            BoardPlayModeTests.CaptureGameplayState("relaxed-return-campaign-result.png");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator RelaxedMoveLimitedLevel_DoesNotSpendMovesOrEnergyOnExit()
        {
            yield return OpenPreview(18);
            BoardPlayModeTests.CaptureGameplayState("relaxed-forest-preview.png");
            Button("RelaxLevel").onClick.Invoke();
            yield return null; yield return null;
            yield return new WaitForSecondsRealtime(4.5f);
            var game = Object.FindAnyObjectByType<GameBootstrap>();
            Assert.That(((LevelDefinition)typeof(GameBootstrap).GetProperty("CurrentLevelDefinition", Private).GetValue(game)).moveLimit, Is.GreaterThan(0));
            Assert.That((bool)typeof(GameBootstrap).GetProperty("IsMoveLimitedLevel", Private).GetValue(game), Is.False);
            Assert.That(game.IsRelaxedMode,Is.True);
            BoardPlayModeTests.CaptureGameplayState("relaxed-forest-hud.png");
            int moves = (int)typeof(GameBootstrap).GetField("movesRemaining", Private).GetValue(game);
            string baseline = JsonUtility.ToJson(Data);
            Assert.That(game.boardController.TryFindHintMoveForObjective(PieceType.None, true, out var a, out var b), Is.True);
            Assert.That(game.boardController.TrySwapAndFindMatches(a, b, out var matches), Is.True);
            typeof(GameBootstrap).GetMethod("HandlePlayerMatch3Move", Private).Invoke(game, new object[] {matches});
            var state = Object.FindAnyObjectByType<GameStateController>();
            float deadline = Time.unscaledTime + 40f;
            yield return null;
            while (!state.CanSelectPieces() && state.CurrentState != GameState.GameOver && Time.unscaledTime < deadline) yield return null;
            Assert.That(state.CurrentState, Is.EqualTo(GameState.Playing).Or.EqualTo(GameState.GameOver), $"Relaxed move settled: {state.CurrentState}.");
            if(state.CurrentState == GameState.GameOver) Assert.That(AppServices.Instance.Progress.IsRelaxedCompleted(18),Is.True);
            Assert.That((int)typeof(GameBootstrap).GetField("movesRemaining", Private).GetValue(game), Is.EqualTo(moves));
            Assert.That(game.gameTimer.IsRunning, Is.False);
            AppServices.Instance.GoToWorldMap();
            yield return null; yield return null;
            var afterExit=JsonUtility.FromJson<PlayerProgressData>(JsonUtility.ToJson(Data));
            afterExit.relaxedCompletedLevels.Clear();
            Assert.That(JsonUtility.ToJson(afterExit), Is.EqualTo(baseline));
            Assert.That(AppServices.Instance.SelectedRelaxedMode, Is.False);
            Assert.That(Data.dogEnergy, Is.Zero);
        }
    }

    internal static class RelaxedTestArraySearch
    {
        internal static T ArrayFind<T>(this T[] array, Predicate<T> predicate) => Array.Find(array, predicate);
    }
}
