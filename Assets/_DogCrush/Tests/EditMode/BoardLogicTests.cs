using NUnit.Framework;
using System.IO;
using DogCrush.Board;
using DogCrush.Gameplay;
using DogCrush.Core;
using DogCrush.Presentation;
using JoinDog.App;
using UnityEngine;
using UnityEngine.UI;

namespace DogCrush.Tests.EditMode
{
    public class BoardLogicTests
    {
        [Test]
        public void AdjacencyCheck_ReturnsTrueOnlyForOrthogonalNeighbors()
        {
            Assert.IsTrue(BoardController.AreAdjacent(0, 0, 0, 1), "Orthogonal vertical adjacent");
            Assert.IsTrue(BoardController.AreAdjacent(0, 0, 1, 0), "Orthogonal horizontal adjacent");
            Assert.IsTrue(BoardController.AreAdjacent(1, 1, 1, 0), "Orthogonal downward adjacent");
            Assert.IsTrue(BoardController.AreAdjacent(1, 1, 0, 1), "Orthogonal left adjacent");
            Assert.IsFalse(BoardController.AreAdjacent(0, 0, 1, 1), "Diagonal movement is forbidden");
        }

        [Test]
        public void AdjacencyCheck_ReturnsFalseForNonAdjacentOrSameCell()
        {
            Assert.IsFalse(BoardController.AreAdjacent(0, 0, 0, 0), "Same cell is not adjacent");
            Assert.IsFalse(BoardController.AreAdjacent(0, 0, 0, 2), "Distant cell is not adjacent");
            Assert.IsFalse(BoardController.AreAdjacent(0, 0, 2, 2), "Distant diagonal is not adjacent");
        }

        [Test]
        public void SpecialRules_ClassifyFourFiveSixAndCrossMatchesIndependently()
        {
            Assert.That(BoardController.ClassifySpecialForRuns(4, 1), Is.EqualTo(PieceSpecialType.RowBlast));
            Assert.That(BoardController.ClassifySpecialForRuns(1, 4), Is.EqualTo(PieceSpecialType.ColumnBlast));
            Assert.That(BoardController.ClassifySpecialForRuns(3, 3), Is.EqualTo(PieceSpecialType.AreaBlast));
            Assert.That(BoardController.ClassifySpecialForRuns(5, 1), Is.EqualTo(PieceSpecialType.ColorBurst));
            Assert.That(BoardController.ClassifySpecialForRuns(1, 5), Is.EqualTo(PieceSpecialType.ColorBurst));
            Assert.That(BoardController.ClassifySpecialForRuns(6, 1), Is.EqualTo(PieceSpecialType.MegaBurst));
            Assert.That(BoardController.ClassifySpecialForRuns(1, 6), Is.EqualTo(PieceSpecialType.MegaBurst));
        }

        [Test]
        public void SpecialPairs_HaveDistinctCombinationRules()
        {
            Assert.That(BoardController.ClassifySpecialPair(PieceSpecialType.RowBlast, PieceSpecialType.RowBlast),
                Is.EqualTo(SpecialComboKind.DoubleRow));
            Assert.That(BoardController.ClassifySpecialPair(PieceSpecialType.RowBlast, PieceSpecialType.ColumnBlast),
                Is.EqualTo(SpecialComboKind.CrossBlast));
            Assert.That(BoardController.ClassifySpecialPair(PieceSpecialType.AreaBlast, PieceSpecialType.RowBlast),
                Is.EqualTo(SpecialComboKind.WideRow));
            Assert.That(BoardController.ClassifySpecialPair(PieceSpecialType.AreaBlast, PieceSpecialType.ColumnBlast),
                Is.EqualTo(SpecialComboKind.WideColumn));
            Assert.That(BoardController.ClassifySpecialPair(PieceSpecialType.AreaBlast, PieceSpecialType.AreaBlast),
                Is.EqualTo(SpecialComboKind.DoubleArea));
        }

        [Test]
        public void ScoreController_BasePointsCalculation()
        {
            GameObject go = new GameObject();
            ScoreController score = go.AddComponent<ScoreController>();

            int points3 = score.AddChainScore(3);
            Assert.AreEqual(300, points3, "3-piece chain should yield 300 base points");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ScoreController_ComboMultiplierTrigger()
        {
            GameObject go = new GameObject();
            ScoreController score = go.AddComponent<ScoreController>();

            int points5 = score.AddChainScore(5);
            // 5 * 100 + 400 bonus = 900 base. Multiplier x2 = 1800 points.
            Assert.AreEqual(1800, points5, "5-piece chain with COMBO x2 should yield 1800 points");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ScoreController_FinalSpecialActivationStillAddsRecordPoints()
        {
            GameObject go = new GameObject();
            ScoreController score = go.AddComponent<ScoreController>();

            int points = score.AddResolutionScore(
                1,
                9,
                PieceSpecialType.None,
                1,
                false,
                SpecialComboKind.None);

            Assert.That(points, Is.GreaterThan(0),
                "A special detonated by the final bonus must keep increasing the score and record.");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ScoreController_SixMatchRewardsMoreThanFiveMatch()
        {
            GameObject fiveObject = new GameObject();
            ScoreController fiveScore = fiveObject.AddComponent<ScoreController>();
            int fivePoints = fiveScore.AddResolutionScore(
                5, 5, PieceSpecialType.ColorBurst, 0, false, SpecialComboKind.None);

            GameObject sixObject = new GameObject();
            ScoreController sixScore = sixObject.AddComponent<ScoreController>();
            int sixPoints = sixScore.AddResolutionScore(
                6, 6, PieceSpecialType.MegaBurst, 0, false, SpecialComboKind.None);

            Assert.That(sixPoints, Is.GreaterThan(fivePoints));
            Object.DestroyImmediate(fiveObject);
            Object.DestroyImmediate(sixObject);
        }

        [Test]
        public void SaveController_HighScorePersistence()
        {
            SaveController.ClearData();
            Assert.AreEqual(0, SaveController.GetHighScore());

            bool saved = SaveController.SaveHighScore(1500);
            Assert.IsTrue(saved);
            Assert.AreEqual(1500, SaveController.GetHighScore());

            bool lowerSaved = SaveController.SaveHighScore(1000);
            Assert.IsFalse(lowerSaved);
            Assert.AreEqual(1500, SaveController.GetHighScore());

            SaveController.ClearData();
        }

        [Test]
        public void AudioController_CreatesFallbackSoundsAndPersistsVolume()
        {
            PlayerPrefs.DeleteKey("DogCrush_SfxVolume");
            GameObject firstObject = new GameObject("AudioTest");
            AudioPlaceholderController audio = firstObject.AddComponent<AudioPlaceholderController>();
            audio.Initialize();

            Assert.That(audio.selectClip, Is.Not.Null);
            Assert.That(audio.matchClip, Is.Not.Null);
            Assert.That(audio.comboClip, Is.Not.Null);
            Assert.That(audio.specialClip, Is.Not.Null);
            Assert.That(audio.cascadeClip, Is.Not.Null);
            Assert.That(audio.timerWarningClip, Is.Not.Null);
            Assert.That(audio.gameOverClip, Is.Not.Null);

            audio.SetSfxVolume(0.6f);
            Assert.That(audio.SfxVolume, Is.EqualTo(0.6f).Within(0.001f));
            Assert.That(audio.sfxSource.volume, Is.EqualTo(0.6f).Within(0.001f));
            Object.DestroyImmediate(firstObject);

            GameObject restoredObject = new GameObject("RestoredAudioTest");
            AudioPlaceholderController restored = restoredObject.AddComponent<AudioPlaceholderController>();
            restored.Initialize();
            Assert.That(restored.SfxVolume, Is.EqualTo(0.6f).Within(0.001f));

            Object.DestroyImmediate(restoredObject);
            PlayerPrefs.DeleteKey("DogCrush_SfxVolume");
        }

        [Test]
        public void HapticsController_PersistsChoiceAndHonorsDisabledState()
        {
            PlayerPrefs.DeleteKey("DogCrush_HapticsEnabled");
            GameObject firstObject = new GameObject("HapticsTest");
            HapticFeedbackController haptics = firstObject.AddComponent<HapticFeedbackController>();
            haptics.Initialize();

            Assert.That(haptics.HapticsEnabled, Is.True);
            haptics.SetHapticsEnabled(false);
            haptics.PulseMatch(8);
            Assert.That(haptics.LastPulseDurationMs, Is.EqualTo(0));
            Object.DestroyImmediate(firstObject);

            GameObject restoredObject = new GameObject("RestoredHapticsTest");
            HapticFeedbackController restored = restoredObject.AddComponent<HapticFeedbackController>();
            restored.Initialize();
            Assert.That(restored.HapticsEnabled, Is.False);

            Object.DestroyImmediate(restoredObject);
            PlayerPrefs.DeleteKey("DogCrush_HapticsEnabled");
        }

        [Test]
        public void Campaign_HasSeventyProgressiveLevelsAndSevenDistinctWorlds()
        {
            CampaignCatalog catalog = CampaignCatalog.LoadOrCreateRuntime();
            Assert.That(catalog.levels.Count, Is.EqualTo(70));
            Assert.That(catalog.zones.Count, Is.EqualTo(7));

            CampaignLevelEntry level10 = catalog.GetLevel(10);
            CampaignLevelEntry level20 = catalog.GetLevel(20);
            CampaignLevelEntry level30 = catalog.GetLevel(30);
            CampaignLevelEntry level40 = catalog.GetLevel(40);
            CampaignLevelEntry level50 = catalog.GetLevel(50);
            CampaignLevelEntry level60 = catalog.GetLevel(60);
            CampaignLevelEntry level70 = catalog.GetLevel(70);
            Assert.That(level10.nodeKind, Is.EqualTo(MapNodeKind.Finale));
            Assert.That(level10.objectiveKind, Is.EqualTo(CampaignObjectiveKind.LongMatch));
            Assert.That(level20.objectiveKind, Is.EqualTo(CampaignObjectiveKind.ClearObstacles));
            Assert.That(level20.obstacleType, Is.EqualTo(CampaignObstacleKind.Vine));
            Assert.That(level30.objectiveKind, Is.EqualTo(CampaignObjectiveKind.ClearObstacles));
            Assert.That(level30.obstacleType, Is.EqualTo(CampaignObstacleKind.Lantern));
            Assert.That(level40.objectiveKind, Is.EqualTo(CampaignObjectiveKind.ClearObstacles));
            Assert.That(level40.obstacleType, Is.EqualTo(CampaignObstacleKind.Sand));
            Assert.That(level50.objectiveKind, Is.EqualTo(CampaignObjectiveKind.ClearObstacles));
            Assert.That(level50.obstacleType, Is.EqualTo(CampaignObstacleKind.Ice));
            Assert.That(level60.objectiveKind, Is.EqualTo(CampaignObjectiveKind.ClearObstacles));
            Assert.That(level60.obstacleType, Is.EqualTo(CampaignObstacleKind.Lantern));
            Assert.That(level70.objectiveKind, Is.EqualTo(CampaignObjectiveKind.ClearObstacles));
            Assert.That(level70.obstacleType, Is.EqualTo(CampaignObstacleKind.Ice));
            Assert.That(CampaignCatalog.BalancedTargetScore(level70),
                Is.GreaterThan(CampaignCatalog.BalancedTargetScore(level10)));
        }

        [Test]
        public void Campaign_NewWorldsIntroduceCascadeGoalsAndDurableObstacles()
        {
            CampaignCatalog catalog = CampaignCatalog.LoadOrCreateRuntime();
            CampaignLevelEntry level36 = catalog.GetLevel(36);
            CampaignLevelEntry level41 = catalog.GetLevel(41);
            CampaignLevelEntry level42 = catalog.GetLevel(42);

            Assert.That(level36.objectiveKind, Is.EqualTo(CampaignObjectiveKind.Cascades));
            Assert.That(level36.obstacleType, Is.EqualTo(CampaignObstacleKind.Sand));
            Assert.That(level41.obstacleType, Is.EqualTo(CampaignObstacleKind.Ice));
            Assert.That(level41.obstacleDurability, Is.EqualTo(1), "The first level after a finale is a relief stage.");
            Assert.That(level42.obstacleDurability, Is.EqualTo(3));
            Assert.That(catalog.GetZoneForLevel(35).id, Is.EqualTo("costa_dorada"));
            Assert.That(catalog.GetZoneForLevel(45).id, Is.EqualTo("cumbres_nevadas"));
            Assert.That(catalog.GetZoneForLevel(55).id, Is.EqualTo("valle_aurora"));
            Assert.That(catalog.GetZoneForLevel(65).id, Is.EqualTo("cumbre_luminosa"));
        }

        [Test]
        public void LateCampaign_UsesDistinctValidObstaclePatterns()
        {
            string[] lanternCross = GameBootstrap.BuildLateCampaignObstaclePattern(51, 9, 10);
            string[] lanternDiagonal = GameBootstrap.BuildLateCampaignObstaclePattern(52, 9, 10);
            string[] iceRim = GameBootstrap.BuildLateCampaignObstaclePattern(61, 9, 10);
            string[] iceDiamond = GameBootstrap.BuildLateCampaignObstaclePattern(62, 9, 10);

            Assert.That(lanternCross, Is.Not.Empty);
            Assert.That(lanternDiagonal, Is.Not.Empty);
            Assert.That(iceRim, Is.Not.Empty);
            Assert.That(iceDiamond, Is.Not.Empty);
            Assert.That(string.Join("|", lanternCross), Is.Not.EqualTo(string.Join("|", lanternDiagonal)));
            Assert.That(string.Join("|", iceRim), Is.Not.EqualTo(string.Join("|", iceDiamond)));

            foreach (string value in iceDiamond)
            {
                string[] parts = value.Split(',');
                Assert.That(parts.Length, Is.EqualTo(2));
                Assert.That(int.Parse(parts[0]), Is.InRange(0, 8));
                Assert.That(int.Parse(parts[1]), Is.InRange(0, 9));
            }
        }

        [Test]
        public void ZoneStarReward_IsGrantedOnceAfterTwentyStars()
        {
            const string saveKey = "JoinDog_PlayerProgress_v1";
            PlayerPrefs.DeleteKey(saveKey);
            try
            {
                PlayerProgressService progress = new PlayerProgressService();
                for (int level = 1; level <= 7; level++)
                    progress.RecordResult(level, true, 3, 1000 * level, 0);

                Assert.That(progress.GetZoneStars("pradera_feliz"), Is.EqualTo(21));
                Assert.That(progress.CanClaimZoneStarReward("pradera_feliz"), Is.True);
                int foodBefore = progress.GetBoosterCount(BoosterKind.Food);
                int reward = progress.ClaimZoneStarReward("pradera_feliz");
                Assert.That(reward, Is.GreaterThan(0));
                Assert.That(progress.GetBoosterCount(BoosterKind.Food), Is.EqualTo(foodBefore + 1));
                Assert.That(progress.ClaimZoneStarReward("pradera_feliz"), Is.EqualTo(0));
            }
            finally
            {
                PlayerPrefs.DeleteKey(saveKey);
            }
        }

        [Test]
        public void LateCampaign_LayoutsAreAsymmetricAndGravitySafe()
        {
            string[] first = GameBootstrap.BuildLateCampaignLayout(51, 9, 10);
            string[] second = GameBootstrap.BuildLateCampaignLayout(52, 9, 10);
            Assert.That(first, Has.Length.EqualTo(10));
            Assert.That(second, Has.Length.EqualTo(10));
            Assert.That(string.Join("|", first), Is.Not.EqualTo(string.Join("|", second)));

            for (int x = 0; x < 9; x++)
            {
                bool enteredPlayable = false;
                bool exitedPlayable = false;
                for (int row = 0; row < 10; row++)
                {
                    bool playable = first[row][x] == '.';
                    if (playable)
                    {
                        Assert.That(exitedPlayable, Is.False, "Playable cells must remain contiguous per column.");
                        enteredPlayable = true;
                    }
                    else if (enteredPlayable)
                    {
                        exitedPlayable = true;
                    }
                }
            }
        }

        [Test]
        public void LateCampaign_ConverterCellsAppearOnAlternatingAdvancedLevels()
        {
            string[] converters = GameBootstrap.BuildConverterCells(51, 9, 10);
            Assert.That(converters, Is.Not.Null.And.Not.Empty);
            Assert.That(GameBootstrap.BuildConverterCells(52, 9, 10), Is.Null);
            Assert.That(GameBootstrap.BuildConverterCells(61, 9, 10), Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void AccessibilitySettings_PersistBothPlayerChoices()
        {
            const string motionKey = "JoinDog_ReducedMotion";
            const string contrastKey = "JoinDog_HighContrastObstacles";
            try
            {
                AccessibilitySettings.ReducedMotion = true;
                AccessibilitySettings.HighContrastObstacles = true;
                Assert.That(AccessibilitySettings.ReducedMotion, Is.True);
                Assert.That(AccessibilitySettings.HighContrastObstacles, Is.True);
            }
            finally
            {
                PlayerPrefs.DeleteKey(motionKey);
                PlayerPrefs.DeleteKey(contrastKey);
            }
        }

        [Test]
        public void MinimumTouchTarget_IsAddedToSmallButtons()
        {
            GameObject root = new GameObject("TouchTest", typeof(RectTransform));
            GameObject child = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            child.transform.SetParent(root.transform, false);
            try
            {
                JoinDogUIFactory.EnsureMinimumTouchTargets(root.transform);
                RectTransform target = child.transform.Find("MinimumTouchTarget") as RectTransform;
                Assert.That(target, Is.Not.Null);
                Assert.That(target.sizeDelta.x, Is.GreaterThanOrEqualTo(56f));
                Assert.That(target.sizeDelta.y, Is.GreaterThanOrEqualTo(56f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void WebGLTemplate_ContainsPwaCacheAndConsoleAuditHooks()
        {
            string root = Path.Combine(Application.dataPath, "WebGLTemplates", "DogCrushTemplate");
            string worker = File.ReadAllText(Path.Combine(root, "service-worker.js"));
            string html = File.ReadAllText(Path.Combine(root, "index.html"));
            Assert.That(worker, Does.Contain("/*__RUNTIME_FILES__*/"));
            Assert.That(worker, Does.Contain("SHELL_FILES.concat(RUNTIME_FILES)"));
            Assert.That(html, Does.Contain("window.__joinDogConsoleErrors"));
            Assert.That(html, Does.Contain("webglConsoleStatus"));
        }

        [Test]
        public void ObjectiveIntro_ExplainsTheGoalBriefly()
        {
            LevelDefinition score = new LevelDefinition
            {
                objectiveType = LevelObjectiveType.Score,
                targetScore = 12500
            };
            Assert.That(GameBootstrap.BuildObjectiveIntroText(score), Does.Contain("12").And.Contain("500"));
            LevelDefinition obstacles = new LevelDefinition
            {
                objectiveType = LevelObjectiveType.ClearObstacles,
                targetAmount = 8
            };
            Assert.That(GameBootstrap.BuildObjectiveIntroText(obstacles), Is.EqualTo("ROMPE 8 OBSTÁCULOS"));
        }
    }
}
