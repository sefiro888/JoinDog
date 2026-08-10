using NUnit.Framework;
using DogCrush.Board;
using DogCrush.Gameplay;
using DogCrush.Core;
using DogCrush.Presentation;
using JoinDog.App;
using UnityEngine;

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
        public void Campaign_HasFiftyProgressiveLevelsAndFiveDistinctWorlds()
        {
            CampaignCatalog catalog = CampaignCatalog.LoadOrCreateRuntime();
            Assert.That(catalog.levels.Count, Is.EqualTo(50));
            Assert.That(catalog.zones.Count, Is.EqualTo(5));

            CampaignLevelEntry level10 = catalog.GetLevel(10);
            CampaignLevelEntry level20 = catalog.GetLevel(20);
            CampaignLevelEntry level30 = catalog.GetLevel(30);
            CampaignLevelEntry level40 = catalog.GetLevel(40);
            CampaignLevelEntry level50 = catalog.GetLevel(50);
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
            Assert.That(CampaignCatalog.BalancedTargetScore(level50),
                Is.GreaterThan(CampaignCatalog.BalancedTargetScore(level10)));
        }

        [Test]
        public void Campaign_NewWorldsIntroduceCascadeGoalsAndDurableObstacles()
        {
            CampaignCatalog catalog = CampaignCatalog.LoadOrCreateRuntime();
            CampaignLevelEntry level32 = catalog.GetLevel(32);
            CampaignLevelEntry level41 = catalog.GetLevel(41);

            Assert.That(level32.objectiveKind, Is.EqualTo(CampaignObjectiveKind.Cascades));
            Assert.That(level32.obstacleType, Is.EqualTo(CampaignObstacleKind.Sand));
            Assert.That(level41.obstacleType, Is.EqualTo(CampaignObstacleKind.Ice));
            Assert.That(level41.obstacleDurability, Is.EqualTo(3));
            Assert.That(catalog.GetZoneForLevel(35).id, Is.EqualTo("costa_dorada"));
            Assert.That(catalog.GetZoneForLevel(45).id, Is.EqualTo("cumbres_nevadas"));
        }
    }
}
