using System;
using System.Collections.Generic;
using UnityEngine;

namespace JoinDog.App
{
    public enum BoosterKind
    {
        Paw,
        Bone,
        Food,
        MagicBone
    }

    [Serializable]
    public sealed class LevelProgressRecord
    {
        public int level;
        public int stars;
        public int bestScore;
        public bool completed;
        public bool rewardClaimed;
        public bool chestClaimed;
    }

    [Serializable]
    public sealed class PlayerProgressData
    {
        public int version = 5;
        public int unlockedLevel = 1;
        public int currentLevel = 1;
        public int treats;
        public int pawBoosters;
        public int boneBoosters;
        public int foodBoosters;
        public int magicBoneBoosters;
        public float mapScrollPosition;
        public int pawPrints;
        public int deepestCascade;
        public int totalCascades;
        public int totalSpecialsCreated;
        public int totalMatches;
        public string dailyDateKey;
        public int dailyCascadeProgress;
        public int dailySpecialProgress;
        public int dailyMatchProgress;
        public bool dailyRewardClaimed;
        // El compañero sustituye a las vidas: cada derrota le cansa una huella
        // y recupera energía con el tiempo real, incluso con el juego cerrado.
        public int dogEnergy = 5;
        public long dogEnergyUpdatedUtcTicks;
        public int dailyStreak;
        public string lastDailyClaimDateKey;
        public bool returnGiftClaimed;
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

        public int UnlockedLevel => CampaignCatalog.UnlockAllLevelsForTesting
            ? CampaignCatalog.MaxLevel
            : Mathf.Clamp(data.unlockedLevel, 1, CampaignCatalog.MaxLevel);
        public int CurrentLevel => Mathf.Clamp(data.currentLevel, 1, CampaignCatalog.MaxLevel);
        public int Treats => Mathf.Max(0, data.treats);
        public int PawPrints => Mathf.Max(0, data.pawPrints);
        public int DeepestCascade => Mathf.Max(0, data.deepestCascade);
        public int TotalCascades => Mathf.Max(0, data.totalCascades);
        public int TotalSpecialsCreated => Mathf.Max(0, data.totalSpecialsCreated);
        public int TotalMatches => Mathf.Max(0, data.totalMatches);
        public const int MaxDogEnergy = 5;
        public const int DogEnergyRecoveryMinutes = 20;
        public int DogEnergy
        {
            get
            {
                RefreshDogEnergy();
                return Mathf.Clamp(data.dogEnergy, 0, MaxDogEnergy);
            }
        }

        public int SecondsUntilDogEnergyRecovery
        {
            get
            {
                RefreshDogEnergy();
                if (data.dogEnergy >= MaxDogEnergy) return 0;
                DateTime updated = TicksToUtc(data.dogEnergyUpdatedUtcTicks);
                double seconds = (updated.AddMinutes(DogEnergyRecoveryMinutes) - DateTime.UtcNow).TotalSeconds;
                return Mathf.Max(0, Mathf.CeilToInt((float)seconds));
            }
        }

        public int DailyStreak => Mathf.Max(0, data.dailyStreak);

        public const int DailyCascadeTarget = 5;
        public const int DailySpecialTarget = 3;
        public const int DailyMatchTarget = 12;

        public int GetBoosterCount(BoosterKind kind)
        {
            switch (kind)
            {
                case BoosterKind.Paw: return Mathf.Max(0, data.pawBoosters);
                case BoosterKind.Bone: return Mathf.Max(0, data.boneBoosters);
                case BoosterKind.Food: return Mathf.Max(0, data.foodBoosters);
                case BoosterKind.MagicBone: return Mathf.Max(0, data.magicBoneBoosters);
                default: return 0;
            }
        }

        public bool TryPurchaseBooster(BoosterKind kind, int cost, int amount = 1)
        {
            cost = Mathf.Max(0, cost);
            amount = Mathf.Max(1, amount);
            if (data.treats < cost) return false;

            data.treats -= cost;
            AddBooster(kind, amount);
            Save();
            return true;
        }

        public bool ConsumeBooster(BoosterKind kind)
        {
            if (GetBoosterCount(kind) <= 0) return false;
            AddBooster(kind, -1);
            Save();
            return true;
        }

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

        public bool IsMapChestClaimed(int level)
        {
            LevelProgressRecord record = Find(level);
            return record != null && record.chestClaimed;
        }

        public bool CanClaimMapChest(int level)
        {
            CampaignLevelEntry entry = CampaignCatalog.LoadOrCreateRuntime().GetLevel(level);
            LevelProgressRecord record = Find(level);
            return entry != null && entry.nodeKind == MapNodeKind.Reward && record != null &&
                record.completed && !record.chestClaimed;
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

        public void RegisterMatch(int pieces, int specialsCreated, int cascadeDepth)
        {
            EnsureDailyMissions();
            data.totalMatches++;
            data.pawPrints += Mathf.Max(1, pieces / 3) + Mathf.Max(0, cascadeDepth - 1);
            data.totalSpecialsCreated += Mathf.Max(0, specialsCreated);
            if (cascadeDepth > 0)
            {
                data.totalCascades++;
                data.deepestCascade = Mathf.Max(data.deepestCascade, cascadeDepth);
            }
            data.dailyMatchProgress = Mathf.Min(DailyMatchTarget, data.dailyMatchProgress + 1);
            data.dailySpecialProgress = Mathf.Min(DailySpecialTarget,
                data.dailySpecialProgress + Mathf.Max(0, specialsCreated));
            if (cascadeDepth > 1)
                data.dailyCascadeProgress = Mathf.Min(DailyCascadeTarget, data.dailyCascadeProgress + 1);
            Save();
        }

        public string GetDailyMissionSummary()
        {
            EnsureDailyMissions();
            return $"HOY  {data.dailyCascadeProgress}/{DailyCascadeTarget} CASCADAS · " +
                $"{data.dailySpecialProgress}/{DailySpecialTarget} ESPECIALES · " +
                $"{data.dailyMatchProgress}/{DailyMatchTarget} JUGADAS";
        }

        public bool IsDailyComplete()
        {
            EnsureDailyMissions();
            return data.dailyCascadeProgress >= DailyCascadeTarget &&
                data.dailySpecialProgress >= DailySpecialTarget &&
                data.dailyMatchProgress >= DailyMatchTarget;
        }

        public int ClaimDailyReward()
        {
            EnsureDailyMissions();
            if (!IsDailyComplete() || data.dailyRewardClaimed) return 0;
            const int reward = 45;
            data.dailyRewardClaimed = true;
            data.treats += reward;
            AddBooster(BoosterKind.MagicBone, 1);
            UpdateDailyStreak();
            Save();
            return reward;
        }

        public bool SpendDogEnergy()
        {
            RefreshDogEnergy();
            if (data.dogEnergy <= 0) return false;
            data.dogEnergy--;
            data.dogEnergyUpdatedUtcTicks = DateTime.UtcNow.Ticks;
            Save();
            return true;
        }

        public bool ClaimReturnGift()
        {
            EnsureDailyMissions();
            if (data.returnGiftClaimed) return false;
            data.returnGiftClaimed = true;
            data.treats += 18 + Mathf.Min(42, DailyStreak * 3);
            AddBooster(BoosterKind.MagicBone, 1);
            Save();
            return true;
        }

        public int ClaimMapChest(int level)
        {
            LevelProgressRecord record = Find(level);
            CampaignLevelEntry entry = CampaignCatalog.LoadOrCreateRuntime().GetLevel(level);
            if (record == null || !record.completed || record.chestClaimed ||
                entry == null || entry.nodeKind != MapNodeKind.Reward) return 0;

            const int treats = 35;
            record.chestClaimed = true;
            data.treats += treats;
            AddBooster(BoosterKind.Paw, 1);
            // El Hueso Mágico no se vende: es un regalo exclusivo del cofre.
            AddBooster(BoosterKind.MagicBone, 1);
            Save();
            return treats;
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

            data.version = 5;
            data.treats = Mathf.Max(0, data.treats);
            data.pawBoosters = Mathf.Max(0, data.pawBoosters);
            data.boneBoosters = Mathf.Max(0, data.boneBoosters);
            data.foodBoosters = Mathf.Max(0, data.foodBoosters);
            if (data.dogEnergyUpdatedUtcTicks <= 0)
            {
                data.dogEnergy = MaxDogEnergy;
                data.dogEnergyUpdatedUtcTicks = DateTime.UtcNow.Ticks;
            }
            RefreshDogEnergy();
            data.unlockedLevel = Mathf.Clamp(data.unlockedLevel, 1, CampaignCatalog.MaxLevel);
            data.currentLevel = Mathf.Clamp(data.currentLevel, 1, data.unlockedLevel);
            if (data.levels == null) data.levels = new List<LevelProgressRecord>();
            EnsureDailyMissions();
            Save();
        }

        private void EnsureDailyMissions()
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            if (data.dailyDateKey == today) return;
            data.dailyDateKey = today;
            data.dailyCascadeProgress = 0;
            data.dailySpecialProgress = 0;
            data.dailyMatchProgress = 0;
            data.dailyRewardClaimed = false;
            data.returnGiftClaimed = false;
        }

        private void RefreshDogEnergy()
        {
            if (data == null) return;
            data.dogEnergy = Mathf.Clamp(data.dogEnergy, 0, MaxDogEnergy);
            if (data.dogEnergy >= MaxDogEnergy) return;
            DateTime updated = TicksToUtc(data.dogEnergyUpdatedUtcTicks);
            double elapsed = (DateTime.UtcNow - updated).TotalMinutes;
            int recovered = Mathf.FloorToInt((float)(elapsed / DogEnergyRecoveryMinutes));
            if (recovered <= 0) return;
            data.dogEnergy = Mathf.Min(MaxDogEnergy, data.dogEnergy + recovered);
            data.dogEnergyUpdatedUtcTicks = updated.AddMinutes(recovered * DogEnergyRecoveryMinutes).Ticks;
            Save();
        }

        private void UpdateDailyStreak()
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            if (data.lastDailyClaimDateKey == today) return;
            DateTime yesterday = DateTime.Now.Date.AddDays(-1);
            if (DateTime.TryParse(data.lastDailyClaimDateKey, out DateTime last) && last.Date == yesterday)
                data.dailyStreak++;
            else if (string.IsNullOrEmpty(data.lastDailyClaimDateKey))
                data.dailyStreak = 1;
            else
                // Un día de margen: la racha no se siente como un castigo.
                data.dailyStreak = Mathf.Max(1, data.dailyStreak - 1);
            data.lastDailyClaimDateKey = today;
        }

        private static DateTime TicksToUtc(long ticks)
        {
            if (ticks <= 0) return DateTime.UtcNow;
            try { return new DateTime(ticks, DateTimeKind.Utc); }
            catch { return DateTime.UtcNow; }
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

        private void AddBooster(BoosterKind kind, int amount)
        {
            switch (kind)
            {
                case BoosterKind.Paw:
                    data.pawBoosters = Mathf.Max(0, data.pawBoosters + amount);
                    break;
                case BoosterKind.Bone:
                    data.boneBoosters = Mathf.Max(0, data.boneBoosters + amount);
                    break;
                case BoosterKind.Food:
                    data.foodBoosters = Mathf.Max(0, data.foodBoosters + amount);
                    break;
                case BoosterKind.MagicBone:
                    data.magicBoneBoosters = Mathf.Max(0, data.magicBoneBoosters + amount);
                    break;
            }
        }

        private void Save()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }
    }
}
