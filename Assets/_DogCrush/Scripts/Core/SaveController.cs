using UnityEngine;

namespace DogCrush.Core
{
    public static class SaveController
    {
        private const string HIGH_SCORE_KEY = "DogCrush_HighScore";

        public static int GetHighScore()
        {
            return PlayerPrefs.GetInt(HIGH_SCORE_KEY, 0);
        }

        public static bool SaveHighScore(int newScore)
        {
            int current = GetHighScore();
            if (newScore > current)
            {
                PlayerPrefs.SetInt(HIGH_SCORE_KEY, newScore);
                PlayerPrefs.Save();
                return true;
            }
            return false;
        }

        public static void ClearData()
        {
            PlayerPrefs.DeleteKey(HIGH_SCORE_KEY);
            PlayerPrefs.Save();
        }
    }
}
