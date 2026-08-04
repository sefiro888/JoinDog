using System.Collections.Generic;
using DogCrush.Board;
using DogCrush.Gameplay;
using DogCrush.Presentation;
using DogCrush.UI;
using JoinDog.App;
using UnityEngine;

namespace DogCrush.Core
{
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Controllers")]
        public GameStateController stateController;
        public BoardController boardController;
        public BoardGravityController gravityController;
        public ChainSelectionController selectionController;
        public ScoreController scoreController;
        public GameTimer gameTimer;

        [Header("Presentation & UI")]
        public GameplayUIController uiController;
        public FeedbackController feedbackController;
        public ParticleEffectController particleController;
        public AudioPlaceholderController audioController;
        public HapticFeedbackController hapticController;

        [Header("Level Progress")]
        [Min(1)] public int currentLevel = 1;
        [Min(100)] public int baseTargetScore = 5000;
        [Min(0)] public int targetIncreasePerLevel = 2000;
        [Tooltip("Optional per-level data. Empty lists receive the balanced defaults at runtime.")]
        public List<LevelDefinition> levelDefinitions = new List<LevelDefinition>();

        private LevelDefinition CurrentLevelDefinition => GetLevelDefinition(currentLevel);
        private int CurrentTargetScore => CurrentLevelDefinition.targetScore;
        private int CurrentBoardRows => CurrentLevelDefinition.rows;
        private int CurrentBoardColumns => CurrentLevelDefinition.columns;
        private float CurrentLevelDuration => CurrentLevelDefinition.durationSeconds;
        private int shuffleBoosterCount;
        private int boneBoosterCount;
        private int foodBoosterCount;
        private int levelPawBoosters;
        private int levelBoneBoosters;
        private int levelFoodBoosters;
        private int objectiveProgress;
        private int longestChain;
        private const string UnlockedLevelKey = "DogCrush_UnlockedLevel";
        private const string LevelStarsKeyPrefix = "DogCrush_LevelStars_";
        private const string LivesKey = "DogCrush_Lives";
        private const int MaxLives = 5;
        public const int MaxPlayableLevel = CampaignCatalog.MaxLevel;
        private int lives;

        private void Start()
        {
            InitializeGame();
        }

        public void InitializeGame()
        {
            EnsureLevelDefinitions();
            bool launchedFromCampaign = AppServices.Instance != null && AppServices.Instance.HasSelectedLevel;
            currentLevel = launchedFromCampaign
                ? Mathf.Clamp(AppServices.Instance.SelectedLevel, 1, MaxPlayableLevel)
                : Mathf.Clamp(
                    Mathf.Max(currentLevel, PlayerPrefs.GetInt(UnlockedLevelKey, 1)),
                    1,
                    MaxPlayableLevel);
            lives = Mathf.Clamp(PlayerPrefs.GetInt(LivesKey, MaxLives), 0, MaxLives);
            if (stateController == null) stateController = GetComponent<GameStateController>();
            if (audioController == null) audioController = GetComponent<AudioPlaceholderController>();
            if (hapticController == null)
                hapticController = GetComponent<HapticFeedbackController>() ??
                    gameObject.AddComponent<HapticFeedbackController>();

            // Subscribe Events
            if (selectionController != null)
            {
                selectionController.OnChainCompleted += HandleChainCompleted;
                selectionController.OnChainCancelled += HandleChainCancelled;
                selectionController.OnChainUpdated += HandleChainUpdated;
                selectionController.OnMoveCompleted += HandleMatch3Move;
            }

            if (scoreController != null)
            {
                scoreController.OnScoreChanged += (current, added) =>
                {
                    if (uiController != null) uiController.UpdateScore(current);
                };
                scoreController.OnHighScoreChanged += (high) =>
                {
                    if (uiController != null) uiController.UpdateHighScore(high);
                };
                scoreController.OnComboTriggered += (mult, text) =>
                {
                    if (feedbackController != null)
                    {
                        feedbackController.TriggerCameraShake(0.15f, 0.25f);
                    }
                    if (uiController != null)
                    {
                        Color comboColor = mult >= 4 ? new Color(1f, 0.3f, 0.8f) : new Color(1f, 0.85f, 0.2f);
                        uiController.ShowComboBanner(text, comboColor);
                    }
                    if (audioController != null)
                    {
                        audioController.PlayComboSound();
                    }
                };
            }

            if (gameTimer != null)
            {
                gameTimer.OnTimerTick += (remaining) =>
                {
                    if (uiController != null) uiController.UpdateTimer(remaining, gameTimer.Progress01);
                };
                gameTimer.OnTenSecondsLeft += () =>
                {
                    if (audioController != null) audioController.PlayTimerWarningSound();
                };
                gameTimer.OnTimerExpired += HandleTimerExpired;
            }

            if (uiController != null)
            {
                uiController.OnRestartRequested += RestartGame;
                uiController.OnNextLevelRequested += StartNextLevel;
                uiController.OnShuffleBoosterRequested += UseShuffleBooster;
                uiController.OnBoneBoosterRequested += UseBoneBooster;
                uiController.OnFoodBoosterRequested += UseFoodBooster;
                uiController.OnLevelSelected += SelectLevel;
                uiController.OnLevelSelectVisibilityChanged += HandleLevelSelectVisibilityChanged;
                uiController.SetUnlockedLevel(Mathf.Clamp(
                    PlayerPrefs.GetInt(UnlockedLevelKey, 1), 1, MaxPlayableLevel));
                uiController.OnSoundToggleRequested += HandleSoundToggleRequested;
                uiController.OnHapticsToggleRequested += HandleHapticsToggleRequested;
                uiController.OnSettingsVisibilityChanged += HandleSettingsVisibilityChanged;
                uiController.OnMainMenuStartRequested += HandleMainMenuStartRequested;
                uiController.OnMainMenuLevelRequested += HandleMainMenuLevelRequested;
                uiController.OnMainMenuSettingsRequested += HandleMainMenuSettingsRequested;
                uiController.OnMainMenuTutorialRequested += HandleMainMenuTutorialRequested;
                uiController.OnReturnToMapRequested += HandleReturnToMapRequested;
                uiController.UpdateSettingsState(
                    audioController != null ? audioController.SfxVolume : 0f,
                    hapticController == null || hapticController.HapticsEnabled);
            }

            StartNewMatch();
            if (launchedFromCampaign)
            {
                uiController?.SetMainMenuVisible(false);
                gameTimer?.SetPaused(false);
            }
            else
            {
                // Legacy direct-scene play remains available for development.
                uiController?.SetMainMenuVisible(true);
                gameTimer?.SetPaused(true);
                stateController.ChangeState(GameState.Initializing);
            }
        }

        public void StartNewMatch()
        {
            // Invalidate any delayed gravity/refill callbacks from the
            // previous match before replacing its board.
            gravityController?.CancelResolution();
            stateController.ChangeState(GameState.Initializing);

            ConfigureCurrentLevel();

            if (uiController != null)
            {
                uiController.HideGameOver();
                uiController.UpdateChainInfo(0, "");
                objectiveProgress = 0;
                longestChain = 0;
                ApplyCurrentObjectiveToUI();
                uiController.UpdateLives(lives, MaxLives);
                LevelDefinition level = CurrentLevelDefinition;
                levelPawBoosters = Mathf.Max(0, level.pawBoosterCount);
                levelBoneBoosters = Mathf.Max(0, level.boneBoosterCount);
                levelFoodBoosters = Mathf.Max(0, level.foodBoosterCount);
                RefreshBoosterCounts();
                uiController.SetSettingsVisible(false);
            }

            if (scoreController != null)
            {
                scoreController.ResetScore();
            }

            if (boardController != null)
            {
                boardController.InitializeBoard();
            }

            if (gameTimer != null)
            {
                float duration = boardController != null && boardController.config != null
                    ? boardController.config.gameDurationSeconds
                    : gameTimer.durationSeconds;
                gameTimer.StartTimer(duration);
            }

            stateController.ChangeState(GameState.Playing);
        }

        private void ConfigureCurrentLevel()
        {
            if (boardController == null || boardController.config == null) return;
            LevelDefinition definition = CurrentLevelDefinition;
            boardController.config.columns = CurrentBoardColumns;
            boardController.config.rows = CurrentBoardRows;
            boardController.config.gameDurationSeconds = CurrentLevelDuration;
            // Only five real piece sprites exist. A sixth enum value is None
            // and would render as a blank cell on higher levels.
            boardController.config.typeCount = Mathf.Clamp(definition.typeCount, 1, 5);
            boardController.config.minChainLength = Mathf.Clamp(definition.minChainLength, 3, 5);
            boardController.config.boardShape = definition.boardShape;
        }

        private void ApplyCurrentObjectiveToUI()
        {
            if (uiController == null) return;
            LevelDefinition definition = CurrentLevelDefinition;
            switch (definition.objectiveType)
            {
                case LevelObjectiveType.CollectPieces:
                    uiController.SetCustomObjective(
                        currentLevel,
                        $"{definition.targetPieceType} x",
                        definition.targetAmount,
                        objectiveProgress);
                    break;
                case LevelObjectiveType.LongChain:
                    uiController.SetCustomObjective(
                        currentLevel,
                        "CADENA",
                        definition.targetAmount,
                        longestChain);
                    break;
                default:
                    uiController.SetLevelObjective(currentLevel, definition.targetScore);
                    break;
            }
        }

        private void UpdateObjectiveProgress(List<PieceView> chain)
        {
            LevelDefinition definition = CurrentLevelDefinition;
            if (definition.objectiveType == LevelObjectiveType.CollectPieces && chain != null)
            {
                foreach (PieceView piece in chain)
                {
                    if (piece != null && piece.type == definition.targetPieceType)
                        objectiveProgress++;
                }
            }
            else if (definition.objectiveType == LevelObjectiveType.LongChain && chain != null)
            {
                longestChain = Mathf.Max(longestChain, chain.Count);
                objectiveProgress = longestChain;
            }

            if (definition.objectiveType == LevelObjectiveType.Score)
            {
                objectiveProgress = scoreController != null ? scoreController.CurrentScore : 0;
            }
            uiController?.UpdateObjectiveProgress(objectiveProgress);
        }

        private bool IsCurrentObjectiveComplete()
        {
            LevelDefinition definition = CurrentLevelDefinition;
            if (definition.objectiveType == LevelObjectiveType.Score)
            {
                return scoreController != null && scoreController.CurrentScore >= definition.targetScore;
            }
            return objectiveProgress >= definition.targetAmount;
        }

        private void EnsureLevelDefinitions()
        {
            if (levelDefinitions != null && levelDefinitions.Count == MaxPlayableLevel) return;
            levelDefinitions = new List<LevelDefinition>();
            CampaignCatalog campaign = CampaignCatalog.LoadOrCreateRuntime();
            for (int level = 1; level <= MaxPlayableLevel; level++)
            {
                CampaignLevelEntry entry = campaign.GetLevel(level);
                if (entry == null) continue;
                levelDefinitions.Add(new LevelDefinition
                {
                    level = level,
                    rows = entry.rows,
                    columns = entry.columns,
                    durationSeconds = entry.durationSeconds,
                    targetScore = entry.targetScore,
                    typeCount = 5,
                    minChainLength = 3,
                    objectiveType = entry.objectiveKind == CampaignObjectiveKind.Collect
                        ? LevelObjectiveType.CollectPieces
                        : entry.objectiveKind == CampaignObjectiveKind.LongMatch
                            ? LevelObjectiveType.LongChain
                            : LevelObjectiveType.Score,
                    targetPieceType = (PieceType)Mathf.Clamp((int)entry.targetPiece, 0, 4),
                    targetAmount = entry.targetAmount,
                    boardShape = entry.diamondBoard ? BoardShape.Diamond : BoardShape.Full,
                    pawBoosterCount = entry.pawBoosters,
                    boneBoosterCount = entry.boneBoosters,
                    foodBoosterCount = entry.foodBoosters
                });
            }
        }

        private LevelDefinition GetLevelDefinition(int level)
        {
            EnsureLevelDefinitions();
            int index = Mathf.Clamp(level - 1, 0, levelDefinitions.Count - 1);
            LevelDefinition definition = levelDefinitions[index];
            if (definition == null) definition = new LevelDefinition { level = level };
            definition.level = level;
            definition.rows = Mathf.Max(2, definition.rows);
            definition.columns = Mathf.Max(2, definition.columns);
            definition.durationSeconds = Mathf.Max(15f, definition.durationSeconds);
            definition.targetScore = Mathf.Max(100, definition.targetScore);
            definition.targetAmount = Mathf.Max(1, definition.targetAmount);
            definition.typeCount = Mathf.Clamp(definition.typeCount, 1, 5);
            definition.minChainLength = Mathf.Clamp(definition.minChainLength, 3, 5);
            return definition;
        }

        private void HandleChainUpdated(int count, PieceType type)
        {
            if (!stateController.CanSelectPieces()) return;

            if (uiController != null)
            {
                List<PieceView> chain = selectionController != null
                    ? selectionController.SelectedChain
                    : null;
                Vector3 lastPiecePosition = chain != null && chain.Count > 0
                    ? chain[chain.Count - 1].transform.position
                    : Vector3.zero;
                uiController.UpdateChainInfo(count, type.ToString(), lastPiecePosition);
            }
            if (audioController != null && count > 1)
            {
                audioController.PlaySelectSound(count);
            }
            if (hapticController != null && count > 1)
            {
                hapticController.PulseSelection();
            }
        }

        private void HandleChainCancelled()
        {
            if (uiController != null)
            {
                uiController.UpdateChainInfo(0, "");
            }
        }

        private void HandleChainCompleted(List<PieceView> chain)
        {
            if (!stateController.CanSelectPieces()) return;

            stateController.ChangeState(GameState.Resolving);
            if (uiController != null)
            {
                uiController.UpdateChainInfo(0, "");
            }

            int pointsGained = scoreController != null ? scoreController.AddChainScore(chain.Count) : 0;
            UpdateObjectiveProgress(chain);

            if (chain != null && chain.Count > 0)
            {
                Vector3 centerPos = chain[chain.Count / 2].transform.position;

                if (feedbackController != null)
                {
                    feedbackController.SpawnFloatingText(centerPos, $"+{pointsGained:N0}", Color.yellow, 36f);
                    feedbackController.TriggerCameraShake(
                        Mathf.Clamp(0.025f + chain.Count * 0.004f, 0.035f, 0.075f),
                        0.14f);
                }

                if (particleController != null)
                {
                    // Keep the burst readable without creating dozens of
                    // particles at once on small mobile/WebGL screens.
                    bool compactScreen = Screen.width <= 600 || Screen.height <= 900;
                    int burstCount = compactScreen
                        ? Mathf.Clamp(3 + chain.Count, 5, 8)
                        : Mathf.Clamp(6 + chain.Count, 9, 16);
                    foreach (var piece in chain)
                    {
                        if (piece != null)
                        {
                            particleController.PlayMatchBurst(
                                piece.transform.position,
                                GetPieceAccentColor(piece.type),
                                burstCount);
                        }
                    }
                }
            }

            if (audioController != null)
            {
                audioController.PlayMatchSound(chain != null ? chain.Count : 3);
            }
            if (hapticController != null)
            {
                hapticController.PulseMatch(chain != null ? chain.Count : 3);
            }

            if (gravityController != null)
            {
                StartCoroutine(gravityController.ProcessRemovalAndRefill(chain, () =>
                {
                    if (IsCurrentObjectiveComplete())
                    {
                        EndMatch(true);
                    }
                    else if (boardController != null && boardController.FindMatches().Count >= 3)
                    {
                        // Resolve automatic cascades before unlocking input.
                        stateController.ChangeState(GameState.Playing);
                        HandleMatch3Move(boardController.FindMatches());
                    }
                    else if (gameTimer != null && gameTimer.RemainingTime <= 0)
                    {
                        EndMatch(false);
                    }
                    else
                    {
                        stateController.ChangeState(GameState.Playing);
                    }
                }));
            }
        }

        private void HandleMatch3Move(List<PieceView> matches)
        {
            if (matches == null || matches.Count < 3 || !stateController.CanSelectPieces()) return;
            HandleChainCompleted(matches);
        }

        private static Color GetPieceAccentColor(PieceType type)
        {
            return type switch
            {
                PieceType.Dog => new Color(1f, 0.66f, 0.18f),
                PieceType.Bone => new Color(1f, 0.95f, 0.72f),
                PieceType.Ball => new Color(0.24f, 0.78f, 1f),
                PieceType.Food => new Color(1f, 0.34f, 0.28f),
                PieceType.Collar => new Color(0.32f, 0.95f, 0.48f),
                _ => new Color(1f, 0.85f, 0.2f)
            };
        }

        private void HandleTimerExpired()
        {
            if (gravityController != null && gravityController.IsResolving)
            {
                return;
            }
            EndMatch(false);
        }

        private void EndMatch(bool victory)
        {
            if (stateController.CurrentState == GameState.GameOver) return;
            stateController.ChangeState(GameState.GameOver);

            if (gameTimer != null)
            {
                gameTimer.StopTimer();
            }

            if (audioController != null)
            {
                if (victory)
                    audioController.PlayComboSound();
                else
                    audioController.PlayGameOverSound();
            }
            if (hapticController != null)
            {
                if (victory)
                    hapticController.PulseMatch(8);
                else
                    hapticController.PulseGameOver();
            }

            int finalScore = scoreController != null ? scoreController.CurrentScore : 0;
            int highScore = scoreController != null ? scoreController.HighScore : 0;
            bool isNewRecord = finalScore > 0 && finalScore >= highScore;

            if (uiController != null)
            {
                int stars = CalculateStars();
                int earnedReward = AppServices.Instance != null
                    ? AppServices.Instance.RecordLevelResult(currentLevel, victory, stars, finalScore)
                    : 0;
                if (victory)
                {
                    string starsKey = LevelStarsKeyPrefix + currentLevel;
                    int previousStars = PlayerPrefs.GetInt(starsKey, 0);
                    PlayerPrefs.SetInt(starsKey, Mathf.Max(previousStars, stars));
                    PlayerPrefs.SetInt(UnlockedLevelKey, Mathf.Max(
                        PlayerPrefs.GetInt(UnlockedLevelKey, 1),
                        Mathf.Min(MaxPlayableLevel, currentLevel + 1)));
                    PlayerPrefs.Save();
                }
                else
                {
                    lives = Mathf.Max(0, lives - 1);
                    PlayerPrefs.SetInt(LivesKey, lives);
                    PlayerPrefs.Save();
                    uiController.UpdateLives(lives, MaxLives);
                }
                uiController.ShowLevelResult(
                    victory, finalScore, isNewRecord, stars, lives, currentLevel, earnedReward);
            }
        }

        private int CalculateStars()
        {
            if (gameTimer == null || gameTimer.durationSeconds <= 0f)
            {
                return 1;
            }

            float timeRatio = gameTimer.RemainingTime / gameTimer.durationSeconds;
            if (timeRatio >= 0.60f) return 3;
            if (timeRatio >= 0.30f) return 2;
            return 1;
        }

        public void RestartGame()
        {
            if (lives <= 0)
            {
                lives = MaxLives;
                PlayerPrefs.SetInt(LivesKey, lives);
                PlayerPrefs.Save();
            }
            StartNewMatch();
        }

        public void StartNextLevel()
        {
            currentLevel = Mathf.Min(MaxPlayableLevel, currentLevel + 1);
            StartNewMatch();
        }

        private void SelectLevel(int level)
        {
            int unlockedLevel = PlayerPrefs.GetInt(UnlockedLevelKey, 1);
            if (level < 1 || level > unlockedLevel || level > MaxPlayableLevel) return;
            currentLevel = Mathf.Clamp(level, 1, MaxPlayableLevel);
            StartNewMatch();
        }

        private void HandleLevelSelectVisibilityChanged(bool visible)
        {
            gameTimer?.SetPaused(visible);
        }

        private void UseShuffleBooster()
        {
            if (shuffleBoosterCount <= 0 || stateController == null || !stateController.CanSelectPieces() || boardController == null) return;
            if (!ConsumeBooster(BoosterKind.Paw)) return;
            // The paw booster creates a completely fresh board.
            boardController.InitializeBoard();
            boardController.EnsureHasValidMoves();
            RefreshBoosterCounts();
            audioController?.PlayUISound();
            hapticController?.PulseSelection();
        }

        private void UseFoodBooster()
        {
            if (foodBoosterCount <= 0 || stateController == null || !stateController.CanSelectPieces()) return;
            if (!ConsumeBooster(BoosterKind.Food)) return;
            // The food bag is the time-support booster: it grants ten seconds
            // instead of duplicating the paw's board refresh behaviour.
            gameTimer?.AddTime(10f);
            RefreshBoosterCounts();
            audioController?.PlayUISound();
            hapticController?.PulseSelection();
        }

        private void UseBoneBooster()
        {
            if (boneBoosterCount <= 0 || stateController == null || !stateController.CanSelectPieces() || boardController == null || gravityController == null) return;
            bool clearsColumn = currentLevel % 2 == 0;
            List<PieceView> line = clearsColumn
                ? boardController.GetColumnPieces(boardController.Columns / 2)
                : boardController.GetRowPieces(boardController.Rows / 2);
            if (line.Count == 0) return;
            if (!ConsumeBooster(BoosterKind.Bone)) return;
            stateController.ChangeState(GameState.Resolving);
            RefreshBoosterCounts();
            StartCoroutine(gravityController.ProcessRemovalAndRefill(line, () =>
            {
                if (gameTimer != null && gameTimer.RemainingTime <= 0f) EndMatch(false);
                else stateController.ChangeState(GameState.Playing);
            }));
            audioController?.PlayMatchSound(line.Count);
            hapticController?.PulseMatch(line.Count);
        }

        private void RefreshBoosterCounts()
        {
            PlayerProgressService progress = AppServices.Instance != null ? AppServices.Instance.Progress : null;
            shuffleBoosterCount = levelPawBoosters + (progress != null ? progress.GetBoosterCount(BoosterKind.Paw) : 0);
            boneBoosterCount = levelBoneBoosters + (progress != null ? progress.GetBoosterCount(BoosterKind.Bone) : 0);
            foodBoosterCount = levelFoodBoosters + (progress != null ? progress.GetBoosterCount(BoosterKind.Food) : 0);
            uiController?.SetBoosterAvailability(shuffleBoosterCount > 0, boneBoosterCount > 0, foodBoosterCount > 0);
            uiController?.SetBoosterCounts(shuffleBoosterCount, boneBoosterCount, foodBoosterCount);
        }

        private bool ConsumeBooster(BoosterKind kind)
        {
            switch (kind)
            {
                case BoosterKind.Paw:
                    if (levelPawBoosters > 0) { levelPawBoosters--; return true; }
                    break;
                case BoosterKind.Bone:
                    if (levelBoneBoosters > 0) { levelBoneBoosters--; return true; }
                    break;
                case BoosterKind.Food:
                    if (levelFoodBoosters > 0) { levelFoodBoosters--; return true; }
                    break;
            }

            return AppServices.Instance != null && AppServices.Instance.Progress.ConsumeBooster(kind);
        }

        private void HandleSoundToggleRequested()
        {
            if (audioController == null)
            {
                return;
            }

            float volume = audioController.CycleSfxVolume();
            uiController?.UpdateSettingsState(
                volume,
                hapticController == null || hapticController.HapticsEnabled);
        }

        private void HandleHapticsToggleRequested()
        {
            if (hapticController == null)
            {
                return;
            }

            bool enabled = hapticController.ToggleHaptics();
            if (enabled)
            {
                hapticController.PulseSelection();
            }
            audioController?.PlayUISound();
            uiController?.UpdateSettingsState(
                audioController != null ? audioController.SfxVolume : 0f,
                enabled);
        }

        private void HandleSettingsVisibilityChanged(bool visible)
        {
            gameTimer?.SetPaused(visible);
            if (visible)
            {
                audioController?.PlayUISound();
                uiController?.UpdateSettingsState(
                    audioController != null ? audioController.SfxVolume : 0f,
                    hapticController == null || hapticController.HapticsEnabled);
            }
        }

        private void HandleMainMenuStartRequested()
        {
            StartNewMatch();
        }

        private void HandleMainMenuLevelRequested()
        {
            uiController?.SetLevelSelectVisible(true);
        }

        private void HandleMainMenuSettingsRequested()
        {
            uiController?.SetSettingsVisible(true);
        }

        private void HandleMainMenuTutorialRequested()
        {
            uiController?.SetTutorialVisible(true);
        }

        private void HandleReturnToMapRequested()
        {
            gameTimer?.StopTimer();
            AppServices.Instance?.GoToWorldMap();
        }
    }
}
