using UnityEngine;
using UnityEngine.SceneManagement;

namespace JoinDog.App
{
    /// <summary>
    /// Small persistent application shell. It owns navigation and progress,
    /// never gameplay objects, so every screen remains independent.
    /// </summary>
    public sealed class AppServices : MonoBehaviour
    {
        public const string BootScene = "Boot";
        public const string MainMenuScene = "MainMenu";
        public const string WorldMapScene = "WorldMap";
        public const string GameplayScene = "Gameplay";

        public static AppServices Instance { get; private set; }

        public PlayerProgressService Progress { get; private set; }
        public int SelectedLevel { get; private set; } = 1;
        public bool HasSelectedLevel { get; private set; }
        public int PendingMapAdvanceFromLevel { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureExists()
        {
            if (Instance != null) return;
            GameObject root = new GameObject("JoinDog_AppServices");
            root.AddComponent<AppServices>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            // A stable 60 Hz cadence makes touch swaps feel predictable while
            // avoiding unconstrained WebGL rendering on high-refresh phones.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            Progress = new PlayerProgressService();
            SelectedLevel = Mathf.Clamp(Progress.CurrentLevel, 1, CampaignCatalog.MaxLevel);
        }

        public void GoToMainMenu()
        {
            HasSelectedLevel = false;
            Load(MainMenuScene);
        }

        public void GoToWorldMap()
        {
            HasSelectedLevel = false;
            Load(WorldMapScene);
        }

        public void StartLevel(int level)
        {
            if (!Progress.IsUnlocked(level)) return;
            SelectedLevel = Mathf.Clamp(level, 1, CampaignCatalog.MaxLevel);
            Progress.SetCurrentLevel(SelectedLevel);
            HasSelectedLevel = true;
            Load(GameplayScene);
        }

        public int RecordLevelResult(int level, bool victory, int stars, int score)
        {
            CampaignLevelEntry entry = CampaignCatalog.LoadOrCreateRuntime().GetLevel(level);
            int earnedReward = Progress.RecordResult(
                level, victory, stars, score, entry != null ? entry.rewardTreats : 0);
            if (victory && level < CampaignCatalog.MaxLevel)
                PendingMapAdvanceFromLevel = level;
            return earnedReward;
        }

        public int ConsumePendingMapAdvance()
        {
            int value = PendingMapAdvanceFromLevel;
            PendingMapAdvanceFromLevel = 0;
            return value;
        }

        private static void Load(string sceneName)
        {
            if (SceneManager.GetActiveScene().name == sceneName) return;
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
    }
}
