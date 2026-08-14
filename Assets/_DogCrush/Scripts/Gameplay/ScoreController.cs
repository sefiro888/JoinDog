using UnityEngine;
using DogCrush.Board;

namespace DogCrush.Gameplay
{
    public class ScoreController : MonoBehaviour
    {
        public int CurrentScore { get; private set; }
        public int HighScore { get; private set; }
        public int CurrentStreak { get; private set; }
        public float StreakTimer { get; private set; }

        public System.Action<int, int> OnScoreChanged; // currentScore, pointsGained
        public System.Action<int> OnHighScoreChanged;
        public System.Action<int, string> OnComboTriggered; // multiplier, comboText

        private float streakTimeout = 4.0f;

        private void Start()
        {
            HighScore = PlayerPrefs.GetInt("DogCrush_HighScore", 0);
            OnHighScoreChanged?.Invoke(HighScore);
        }

        private void Update()
        {
            if (StreakTimer > 0)
            {
                StreakTimer -= Time.deltaTime;
                if (StreakTimer <= 0)
                {
                    CurrentStreak = 0;
                    StreakTimer = 0;
                }
            }
        }

        public void ResetScore()
        {
            CurrentScore = 0;
            CurrentStreak = 0;
            StreakTimer = 0;
            OnScoreChanged?.Invoke(0, 0);
        }

        public int AddChainScore(int chainLength)
        {
            if (chainLength < 3) return 0;

            int baseScorePerPiece = 100;
            int points = chainLength * baseScorePerPiece;

            // Extra bonus for long chains
            if (chainLength == 4) points += 200;
            else if (chainLength == 5) points += 400;
            else if (chainLength == 6) points += 800;
            else if (chainLength >= 7) points += 1500 + (chainLength - 7) * 500;

            // Multipliers
            int multiplier = 1;
            string comboText = "";

            if (chainLength >= 9)
            {
                multiplier = 4;
                comboText = "SUPERCOMBO x4!";
            }
            else if (chainLength >= 7)
            {
                multiplier = 3;
                comboText = "COMBO x3!";
            }
            else if (chainLength >= 5)
            {
                multiplier = 2;
                comboText = "COMBO x2!";
            }

            // Streak multiplier
            CurrentStreak++;
            StreakTimer = streakTimeout;

            if (CurrentStreak >= 3 && multiplier == 1)
            {
                multiplier = 2;
                comboText = $"RACHA x{CurrentStreak}!";
            }

            int finalPoints = points * multiplier;
            CurrentScore += finalPoints;

            if (multiplier > 1)
            {
                OnComboTriggered?.Invoke(multiplier, comboText);
            }

            OnScoreChanged?.Invoke(CurrentScore, finalPoints);

            if (CurrentScore > HighScore)
            {
                HighScore = CurrentScore;
                PlayerPrefs.SetInt("DogCrush_HighScore", HighScore);
                PlayerPrefs.Save();
                OnHighScoreChanged?.Invoke(HighScore);
            }

            return finalPoints;
        }

        public int AddResolutionScore(
            int originalMatchCount,
            int removedCount,
            PieceSpecialType createdSpecial,
            int specialsActivated,
            bool megaCombo,
            SpecialComboKind comboKind)
        {
            bool directSpecialActivation = specialsActivated > 0 || megaCombo;
            if (removedCount < 1 || (originalMatchCount < 2 && !directSpecialActivation)) return 0;

            int matchedPieces = directSpecialActivation && originalMatchCount < 2
                ? 1
                : Mathf.Max(3, originalMatchCount);
            int points = matchedPieces * 100;
            points += Mathf.Max(0, removedCount - matchedPieces) * 125;

            if (createdSpecial == PieceSpecialType.RowBlast || createdSpecial == PieceSpecialType.ColumnBlast)
                points += 450;
            else if (createdSpecial == PieceSpecialType.AreaBlast)
                points += 900;
            else if (createdSpecial == PieceSpecialType.ColorBurst)
                points += 1600;
            else if (createdSpecial == PieceSpecialType.MegaBurst)
                points += 2800;
            else if (createdSpecial == PieceSpecialType.BallBounce)
                points += 3600;
            else if (createdSpecial == PieceSpecialType.Whistle)
                points += 2200;

            points += specialsActivated * 650;
            if (megaCombo) points += 3000;
            points += comboKind switch
            {
                SpecialComboKind.DoubleRow => 900,
                SpecialComboKind.DoubleColumn => 900,
                SpecialComboKind.CrossBlast => 1300,
                SpecialComboKind.WideRow => 1700,
                SpecialComboKind.WideColumn => 1700,
                SpecialComboKind.DoubleArea => 2400,
                SpecialComboKind.ColorSweep => 3000,
                SpecialComboKind.BoardNova => 5000,
                _ => 0
            };

            bool combinedSpecial = comboKind != SpecialComboKind.None;
            int multiplier = megaCombo ? 5 : combinedSpecial || specialsActivated >= 2 || originalMatchCount >= 6 ? 3 :
                specialsActivated == 1 || originalMatchCount >= 5 ? 2 : 1;
            string comboText = megaCombo ? "SUPERNOVA x5!" :
                combinedSpecial ? "FUSION ESPECIAL x3!" :
                specialsActivated >= 2 ? "REACCION x3!" :
                originalMatchCount >= 6 ? "SUPERNOVA x3!" :
                specialsActivated == 1 ? "EXPLOSION x2!" :
                originalMatchCount >= 5 ? "ESPECIAL x2!" : string.Empty;

            CurrentStreak++;
            StreakTimer = streakTimeout;
            if (CurrentStreak >= 3 && multiplier == 1)
            {
                multiplier = 2;
                comboText = $"RACHA x{CurrentStreak}!";
            }

            CurrentScore += points * multiplier;
            int finalPoints = points * multiplier;
            if (multiplier > 1)
                OnComboTriggered?.Invoke(multiplier, comboText);
            OnScoreChanged?.Invoke(CurrentScore, finalPoints);

            if (CurrentScore > HighScore)
            {
                HighScore = CurrentScore;
                PlayerPrefs.SetInt("DogCrush_HighScore", HighScore);
                PlayerPrefs.Save();
                OnHighScoreChanged?.Invoke(HighScore);
            }
            return finalPoints;
        }
    }
}
