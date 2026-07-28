using NUnit.Framework;
using DogCrush.Board;
using DogCrush.Gameplay;
using DogCrush.Core;
using DogCrush.Presentation;
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
    }
}
