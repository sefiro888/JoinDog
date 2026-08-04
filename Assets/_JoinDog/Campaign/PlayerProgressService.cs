using System;
using System.Collections.Generic;
using UnityEngine;

namespace JoinDog.App
{
    [Serializable]
    public sealed class LevelProgressRecord
    {
        public int level;
        public int stars;
        public int bestScore;
        public bool completed;
        public bool rewardClaimed;
    }

    [Serializable]
    public sealed class PlayerProgressData
    {
        public int version = 2;
        public int unlockedLevel = 1;
        public int currentLevel = 1;
        public int treats;
        public float mapScrollPosition;
        public List<LevelProgressRecord> levels = new List<LevelProgressRecord>();
    }

    /// <summary>
    /// Versioned progress model stored as one JSON document. Legacy PlayerPrefs
    /// values are imported once and mirrored while Gameplay is being migrated.
    /// </summary>
    public sealed class PlayerProgressService
    {
        private const string SaveKey = "JoinDog_PlayerProgress_v1";
        private const string LegacyUnlockedKey = "DogCrush_UnlockedLevel";
        private const string LegacyStarsPrefix = "DogCrush_LevelStars_";
        private PlayerProgressData data;

        public int UnlockedLevel => Mathf.Clamp(data.unlockedLevel, 1, CampaignCatalog.MaxLevel);
        public int CurrentLevel => Mathf.Clamp(data.currentLevel, 1, CampaignCatalog.MaxLevel);
        public int Treats => Mathf.Max(0, data.treats);

        public PlayerProgressService()
        {
            Load();
        }

        public bool IsUnlocked(int level) => level >= 1 && level <= UnlockedLevel;

        public int GetStars(int level)
        {
            LevelProgressRecord record = Find(level);
            return record != null ? Mathf.Clamp(record.stars, 0, 3) : 0;
        }

        public int GetBestScore(int level)
        {
            LevelProgressRecord record = Find(level);
            return record != null ? Mathf.Max(0, record.bestScore) : 0;
        }

        public int TotalStars()
        {
            int total = 0;
            foreach (LevelProgressRecord record in data.levels)
                if (record != null) total += Mathf.Clamp(record.stars, 0, 3);
            return total;
        }

        public int CompletedLevels()
        {
            int total = 0;
            foreach (LevelProgressRecord record in data.levels)
                if (record != null && record.completed) total++;
            return total;
        }

        public void SetCurrentLevel(int level)
        {
            data.currentLevel = Mathf.Clamp(level, 1, CampaignCatalog.MaxLevel);
            Save();
        }

        public void SetMapScroll(float normalized)
        {
            data.mapScrollPosition = Mathf.Clamp01(normalized);
            Save();
        }

        public float GetMapScroll() => Mathf.Clamp01(data.mapScrollPosition);

        public int RecordResult(int level, bool victory, int stars, int score, int rewardTreats)
        {
            level = Mathf.Clamp(level, 1, CampaignCatalog.MaxLevel);
            LevelProgressRecord record = Find(level);
            if (record == null)
            {
                record = new LevelProgressRecord { level = level };
                data.levels.Add(record);
            }

            record.bestScore = Mathf.Max(record.bestScore, score);
            int earnedReward = 0;
            if (victory)
            {
                record.completed = true;
                record.stars = Mathf.Max(record.stars, Mathf.Clamp(stars, 1, 3));
                if (!record.rewardClaimed)
                {
                    earnedReward = Mathf.Max(0, rewardTreats);
                    data.treats += earnedReward;
                    record.rewardClaimed = true;
                }
                data.unlockedLevel = Mathf.Max(data.unlockedLevel,
                    Mathf.Min(CampaignCatalog.MaxLevel, level + 1));
                data.currentLevel = Mathf.Min(CampaignCatalog.MaxLevel, level + 1);
            }

            PlayerPrefs.SetInt(LegacyUnlockedKey, data.unlockedLevel);
            PlayerPrefs.SetInt(LegacyStarsPrefix + level, record.stars);
            Save();
            return earnedReward;
        }

        private void Load()
        {
            string json = PlayerPrefs.GetString(SaveKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                try { data = JsonUtility.FromJson<PlayerProgressData>(json); }
                catch { data = null; }
            }

            if (data == null)
            {
                data = new PlayerProgressData();
                ImportLegacyProgress();
            }

            data.version = 2;
            data.treats = Mathf.Max(0, data.treats);
            data.unlockedLevel = Mathf.Clamp(data.unlockedLevel, 1, CampaignCatalog.MaxLevel);
            data.currentLevel = Mathf.Clamp(data.currentLevel, 1, data.unlockedLevel);
            if (data.levels == null) data.levels = new List<LevelProgressRecord>();
            Save();
        }

        private void ImportLegacyProgress()
        {
            data.unlockedLevel = Mathf.Clamp(
                PlayerPrefs.GetInt(LegacyUnlockedKey, 1), 1, CampaignCatalog.MaxLevel);
            data.currentLevel = data.unlockedLevel;
            for (int level = 1; level <= data.unlockedLevel; level++)
            {
                int stars = Mathf.Clamp(PlayerPrefs.GetInt(LegacyStarsPrefix + level, 0), 0, 3);
                if (stars <= 0) continue;
                data.levels.Add(new LevelProgressRecord
                {
                    level = level,
                    stars = stars,
                    completed = true
                });
            }
        }

        private LevelProgressRecord Find(int level)
        {
            return data.levels.Find(record => record != null && record.level == level);
        }

        private void Save()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }
    }
}
