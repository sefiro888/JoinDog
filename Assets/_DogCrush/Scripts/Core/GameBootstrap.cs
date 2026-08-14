using System.Collections;
using System.Collections.Generic;
using DogCrush.Board;
using DogCrush.Gameplay;
using DogCrush.Presentation;
using DogCrush.UI;
using UnityEngine;
using JoinDog.App;

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
        private int cascadeDepth;
        private bool runtimeLevelDefinitionsReady;
        private bool victoryPending;
        private bool finalSpecialActivationQueued;
        private int finalBonusWave;
        private Coroutine finalBonusCoroutine;
        private bool climaxSlowMotionActive;
        private Coroutine climaxSlowMotionCoroutine;
        private const int MaxFinalBonusWaves = 96;
        private const string UnlockedLevelKey = "DogCrush_UnlockedLevel";
        private const string LevelStarsKeyPrefix = "DogCrush_LevelStars_";
        private const int MaxLives = PlayerProgressService.MaxDogEnergy;
        public const int MaxPlayableLevel = CampaignCatalog.MaxLevel;
        private int lives;
        private const int CompanionChargeTarget = 4;
        private int companionCharge;
        private CompanionOnBoardController companionOnBoard;
        private bool usedBoosterThisMatch;
        private bool earnedSkillStar;

        [Header("Assistance")]
        [Tooltip("Seconds of player inactivity before a valid move is highlighted.")]
        [Min(1f)] public float hintDelaySeconds = 4.5f;
        [Tooltip("Seconds granted for each cascade beyond the first.")]
        [Min(0f)] public float cascadeTimeBonusSeconds = 1.2f;
        [Tooltip("Cascade depth from which no further time is granted.")]
        [Min(1)] public int maxRewardedCascadeDepth = 6;
        private float idleSeconds;
        private PieceView hintPieceA;
        private PieceView hintPieceB;

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
            lives = AppServices.Instance != null ? AppServices.Instance.Progress.DogEnergy : MaxLives;
            if (stateController == null) stateController = GetComponent<GameStateController>();
            if (stateController != null)
            {
                stateController.OnStateChanged -= HandleStateChangedForClock;
                stateController.OnStateChanged += HandleStateChangedForClock;
            }
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
                selectionController.OnMoveCompleted += HandlePlayerMatch3Move;
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
                uiController.SetUnlockedLevel(CampaignCatalog.UnlockAllLevelsForTesting
                    ? MaxPlayableLevel
                    : Mathf.Clamp(PlayerPrefs.GetInt(UnlockedLevelKey, 1), 1, MaxPlayableLevel));
                uiController.OnSoundToggleRequested += HandleSoundToggleRequested;
                uiController.OnHapticsToggleRequested += HandleHapticsToggleRequested;
                uiController.OnSettingsVisibilityChanged += HandleSettingsVisibilityChanged;
                uiController.OnMainMenuStartRequested += HandleMainMenuStartRequested;
                uiController.OnMainMenuLevelRequested += HandleMainMenuLevelRequested;
                uiController.OnMainMenuSettingsRequested += HandleMainMenuSettingsRequested;
                uiController.OnMainMenuTutorialRequested += HandleMainMenuTutorialRequested;
                uiController.OnReturnToMapRequested += HandleReturnToMapRequested;
                uiController.OnExitToMainMenuRequested += HandleExitToMainMenuRequested;
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
            Time.timeScale = 1f;
            climaxSlowMotionActive = false;
            if (climaxSlowMotionCoroutine != null)
            {
                StopCoroutine(climaxSlowMotionCoroutine);
                climaxSlowMotionCoroutine = null;
            }
            if (finalBonusCoroutine != null)
            {
                StopCoroutine(finalBonusCoroutine);
                finalBonusCoroutine = null;
            }
            victoryPending = false;
            finalSpecialActivationQueued = false;
            finalBonusWave = 0;
            cascadeDepth = 0;
            companionCharge = 0;
            usedBoosterThisMatch = false;
            earnedSkillStar = false;
            // Invalidate any delayed gravity/refill callbacks from the
            // previous match before replacing its board.
            gravityController?.CancelResolution();
            ClearHint();
            feedbackController?.InvalidateCameraRestPosition();
            stateController.ChangeState(GameState.Initializing);

            // La energía del perro no se rellena al perder: se recupera con
            // el tiempo real mediante PlayerProgressService.
            lives = AppServices.Instance != null ? AppServices.Instance.Progress.DogEnergy : lives;
            if (lives <= 0)
            {
                uiController?.ShowLevelResult(false, 0, false, 0, 0, currentLevel, 0);
                return;
            }

            ConfigureCurrentLevel();

            if (uiController != null)
            {
                uiController.HideGameOver();
                uiController.UpdateChainInfo(0, "");
                objectiveProgress = 0;
                longestChain = 0;
                uiController.ApplyWorldTheme(CurrentLevelDefinition.boardTheme);
                ApplyCurrentObjectiveToUI();
                uiController.UpdateLives(lives, MaxLives);
                uiController.UpdateCompanionCharge(companionCharge, CompanionChargeTarget);
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
                EnsureCompanionOnBoard();
                PrepareMagicBoneReward();
                RefreshSecondaryHazardUI(true);
            }

            if (gameTimer != null)
            {
                float duration = boardController != null && boardController.config != null
                    ? boardController.config.gameDurationSeconds
                    : gameTimer.durationSeconds;
                gameTimer.StartTimer(duration);
            }

            stateController.ChangeState(GameState.Playing);
            if (currentLevel == 1 && PlayerPrefs.GetInt("JoinDog_SwapTutorialSeen", 0) == 0)
                StartCoroutine(ShowFirstMoveTutorial());
        }

        private void EnsureCompanionOnBoard()
        {
            if (boardController == null) return;
            if (companionOnBoard == null)
            {
                GameObject companion = new GameObject("CompanionOnBoard_Runtime");
                companionOnBoard = companion.AddComponent<CompanionOnBoardController>();
            }
            PieceView anchor = boardController.GetPieceAt(0, boardController.Rows / 2)
                ?? boardController.GetPieceAt(1, boardController.Rows / 2)
                ?? boardController.GetRandomPiece();
            companionOnBoard.Setup(MapCharacterSelection.LoadSelectedSprite(), anchor);
        }

        private IEnumerator ShowFirstMoveTutorial()
        {
            yield return new WaitForSeconds(0.65f);
            if (boardController != null && boardController.TryFindHintMove(out PieceView first, out PieceView second))
            {
                first.SetHintHighlight(true);
                second.SetHintHighlight(true);
                uiController?.ShowComboBanner("ARRASTRA UNA FICHA HACIA SU VECINA", new Color(1f, 0.82f, 0.18f));
                yield return new WaitForSeconds(3f);
                first?.SetHintHighlight(false);
                second?.SetHintHighlight(false);
            }
            PlayerPrefs.SetInt("JoinDog_SwapTutorialSeen", 1);
            PlayerPrefs.Save();
        }

        // Recompensa exclusiva de los cofres: deja un comodín ColorBurst listo
        // para combinar con cualquier ficha, sin añadir otro botón al HUD.
        private void PrepareMagicBoneReward()
        {
            PlayerProgressService progress = AppServices.Instance != null ? AppServices.Instance.Progress : null;
            if (progress == null || boardController == null ||
                progress.GetBoosterCount(BoosterKind.MagicBone) <= 0) return;

            PieceView target = null;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                PieceView candidate = boardController.GetRandomPiece();
                if (candidate != null && !candidate.IsSpecial)
                {
                    target = candidate;
                    break;
                }
            }
            if (target == null || !progress.ConsumeBooster(BoosterKind.MagicBone)) return;

            target.SetSpecial(PieceSpecialType.ColorBurst);
            target.PlaySpecialCreationAnimation();
            particleController?.PlaySpecialCreated(target);
            feedbackController?.SpawnFloatingText(target.transform.position,
                "HUESO MAGICO!", new Color(1f, 0.78f, 0.18f), 30f);
            uiController?.ShowComboBanner("HUESO MAGICO LISTO", new Color(1f, 0.78f, 0.18f));
        }

        private void Update()
        {
            UpdateIdleHint();
        }

        private void HandleStateChangedForClock(GameState previous, GameState current)
        {
            bool boardBusy = current != GameState.Playing && current != GameState.Selecting;
            gameTimer?.SetPaused(boardBusy, TimerPauseReason.Resolving);
            if (boardBusy) ClearHint();
            else idleSeconds = 0f;
        }

        private void UpdateIdleHint()
        {
            if (boardController == null || stateController == null) return;

            if (!stateController.CanSelectPieces() ||
                gameTimer == null || !gameTimer.IsRunning || gameTimer.IsPaused)
            {
                ClearHint();
                return;
            }

            if (hintPieceA != null) return;

            idleSeconds += Time.deltaTime;
            if (idleSeconds < hintDelaySeconds) return;

            if (boardController.TryFindHintMove(out PieceView first, out PieceView second))
            {
                hintPieceA = first;
                hintPieceB = second;
                first.SetHintHighlight(true);
                second.SetHintHighlight(true);
            }
            else
            {
                idleSeconds = 0f;
            }
        }

        private void ClearHint()
        {
            if (hintPieceA != null) hintPieceA.SetHintHighlight(false);
            if (hintPieceB != null) hintPieceB.SetHintHighlight(false);
            hintPieceA = null;
            hintPieceB = null;
            idleSeconds = 0f;
        }

        private void ConfigureCurrentLevel()
        {
            if (boardController == null || boardController.config == null) return;
            LevelDefinition definition = CurrentLevelDefinition;
            boardController.config.columns = CurrentBoardColumns;
            boardController.config.rows = CurrentBoardRows;
            boardController.config.layoutRows = definition.layoutRows;
            boardController.config.gameDurationSeconds = CurrentLevelDuration;
            // Only five real piece sprites exist. A sixth enum value is None
            // and would render as a blank cell on higher levels.
            boardController.config.typeCount = Mathf.Clamp(definition.typeCount, 1, 5);
            boardController.config.minChainLength = Mathf.Clamp(definition.minChainLength, 3, 5);
            boardController.config.boardShape = definition.boardShape;
            boardController.config.boardTheme = definition.boardTheme;
            boardController.config.obstacleType = definition.obstacleType;
            boardController.config.obstacleCount = Mathf.Max(0, definition.obstacleCount);
            boardController.config.obstacleDurability = Mathf.Clamp(definition.obstacleDurability, 1, 3);
            boardController.config.obstacleCells = definition.obstacleCells;
            ApplyGameplayWorldBackground(definition.boardTheme);
        }

        private static void ApplyGameplayWorldBackground(BoardTheme theme)
        {
            GameObject backgroundObject = GameObject.Find("DogParkBackground");
            SpriteRenderer background = backgroundObject != null
                ? backgroundObject.GetComponent<SpriteRenderer>()
                : null;
            if (background != null)
            {
                background.color = theme == BoardTheme.Forest
                    ? new Color(0.54f, 0.78f, 0.62f, 1f)
                    : theme == BoardTheme.Festival
                        ? new Color(0.55f, 0.48f, 0.78f, 1f)
                        : theme == BoardTheme.Coast
                            ? new Color(0.72f, 0.92f, 1f, 1f)
                            : theme == BoardTheme.Mountain
                                ? new Color(0.72f, 0.82f, 0.96f, 1f)
                                : Color.white;
            }

            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.backgroundColor = theme == BoardTheme.Forest
                    ? new Color(0.025f, 0.11f, 0.08f)
                    : theme == BoardTheme.Festival
                        ? new Color(0.055f, 0.025f, 0.12f)
                        : theme == BoardTheme.Coast
                            ? new Color(0.04f, 0.30f, 0.42f)
                            : theme == BoardTheme.Mountain
                                ? new Color(0.06f, 0.12f, 0.24f)
                                : new Color(0.12f, 0.22f, 0.30f);
            }
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
                        "ESPECIALES",
                        definition.targetAmount,
                        objectiveProgress);
                    break;
                case LevelObjectiveType.ClearObstacles:
                    string obstacleLabel = definition.obstacleType == CellObstacleType.Vine ? "ENREDADERAS" :
                        definition.obstacleType == CellObstacleType.Lantern ? "FAROLES" :
                        definition.obstacleType == CellObstacleType.Sand ? "ARENA" : "HIELO";
                    uiController.SetCustomObjective(
                        currentLevel,
                        obstacleLabel,
                        definition.targetAmount,
                        objectiveProgress);
                    break;
                case LevelObjectiveType.Cascades:
                    uiController.SetCustomObjective(currentLevel, "CASCADAS",
                        definition.targetAmount, objectiveProgress);
                    break;
                default:
                    uiController.SetLevelObjective(currentLevel, definition.targetScore);
                    break;
            }
        }

        private void RefreshSecondaryHazardUI(bool announceFirstEncounter)
        {
            if (uiController == null || boardController == null || boardController.config == null)
                return;

            CellObstacleType type = boardController.config.obstacleType;
            int remaining = boardController.RemainingObstacleCount;
            if (type == CellObstacleType.None || remaining <= 0 ||
                CurrentLevelDefinition.objectiveType == LevelObjectiveType.ClearObstacles)
            {
                uiController.SetSecondaryHazard(null, 0);
                return;
            }

            string label = type == CellObstacleType.Vine ? "ENREDADERAS" :
                type == CellObstacleType.Lantern ? "FAROLES" :
                type == CellObstacleType.Sand ? "ARENA" : "HIELO";
            uiController.SetSecondaryHazard(label, remaining);

            string tutorialKey = $"JoinDog_HazardHint_{type}";
            if (announceFirstEncounter && PlayerPrefs.GetInt(tutorialKey, 0) == 0)
            {
                string instruction = type == CellObstacleType.Vine
                    ? "ENREDADERAS: COMBINA SOBRE ELLAS"
                    : type == CellObstacleType.Lantern
                        ? "FAROLES: USA COMBINACIONES ESPECIALES"
                        : type == CellObstacleType.Sand
                            ? "ARENA: COMBINA AL LADO"
                            : "HIELO: ROMPE SUS CAPAS";
                uiController.ShowComboBanner(instruction, new Color(0.48f, 1f, 0.38f));
                PlayerPrefs.SetInt(tutorialKey, 1);
                PlayerPrefs.Save();
            }
        }

        private void UpdateObjectiveProgress(List<PieceView> removedPieces, int specialsCreated, int obstaclesCleared = 0)
        {
            LevelDefinition definition = CurrentLevelDefinition;
            if (definition.objectiveType == LevelObjectiveType.CollectPieces && removedPieces != null)
            {
                foreach (PieceView piece in removedPieces)
                {
                    if (piece != null && piece.type == definition.targetPieceType)
                        objectiveProgress++;
                }
            }
            else if (definition.objectiveType == LevelObjectiveType.LongChain)
            {
                objectiveProgress += Mathf.Max(0, specialsCreated);
            }
            else if (definition.objectiveType == LevelObjectiveType.ClearObstacles)
            {
                objectiveProgress += Mathf.Max(0, obstaclesCleared);
            }
            else if (definition.objectiveType == LevelObjectiveType.Cascades && cascadeDepth > 0)
            {
                objectiveProgress++;
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
            if (runtimeLevelDefinitionsReady && levelDefinitions != null &&
                levelDefinitions.Count == MaxPlayableLevel) return;
            levelDefinitions = new List<LevelDefinition>();
            CampaignCatalog campaign = CampaignCatalog.LoadOrCreateRuntime();
            for (int level = 1; level <= MaxPlayableLevel; level++)
            {
                CampaignLevelEntry entry = campaign.GetLevel(level);
                if (entry == null) continue;
                LevelDefinition definition = new LevelDefinition
                {
                    level = level,
                    rows = entry.rows,
                    columns = entry.columns,
                    durationSeconds = entry.durationSeconds,
                    targetScore = CampaignCatalog.BalancedTargetScore(entry),
                    typeCount = 5,
                    minChainLength = 3,
                    objectiveType = entry.objectiveKind == CampaignObjectiveKind.Collect
                        ? LevelObjectiveType.CollectPieces
                        : entry.objectiveKind == CampaignObjectiveKind.LongMatch
                            ? LevelObjectiveType.LongChain
                            : entry.objectiveKind == CampaignObjectiveKind.ClearObstacles
                                ? LevelObjectiveType.ClearObstacles
                                : entry.objectiveKind == CampaignObjectiveKind.Cascades
                                    ? LevelObjectiveType.Cascades
                                    : LevelObjectiveType.Score,
                    targetPieceType = (PieceType)Mathf.Clamp((int)entry.targetPiece, 0, 4),
                    targetAmount = CampaignCatalog.BalancedTargetAmount(entry),
                    boardShape = entry.diamondBoard
                        ? BoardShape.Diamond
                        : entry.roundedBoard
                            ? BoardShape.Rounded
                            : BoardShape.Full,
                    boardTheme = level <= 10 ? BoardTheme.Meadow :
                        level <= 20 ? BoardTheme.Forest :
                        level <= 30 ? BoardTheme.Festival :
                        level <= 40 ? BoardTheme.Coast : BoardTheme.Mountain,
                    obstacleType = entry.obstacleType == CampaignObstacleKind.Vine
                        ? CellObstacleType.Vine
                        : entry.obstacleType == CampaignObstacleKind.Lantern
                            ? CellObstacleType.Lantern
                            : entry.obstacleType == CampaignObstacleKind.Sand
                                ? CellObstacleType.Sand
                                : entry.obstacleType == CampaignObstacleKind.Ice
                                    ? CellObstacleType.Ice
                                    : CellObstacleType.None,
                    obstacleCount = entry.obstacleCount,
                    obstacleDurability = entry.obstacleDurability,
                    pawBoosterCount = entry.pawBoosters,
                    boneBoosterCount = entry.boneBoosters,
                    foodBoosterCount = entry.foodBoosters
                };

                // A hand-authored asset overrides only the selected level.
                // Missing assets keep the established campaign generator as a
                // safe fallback while the catalogue is migrated incrementally.
                LevelDesignAsset manual = Resources.Load<LevelDesignAsset>(
                    $"Campaign/Levels/level_{level:000}");
                if (manual != null && manual.level == level)
                    manual.ApplyTo(definition);
                levelDefinitions.Add(definition);
            }
            runtimeLevelDefinitionsReady = levelDefinitions.Count == MaxPlayableLevel;
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
            ClearHint();
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

        private void TryActivateCompanionAssist(List<PieceView> piecesToRemove, bool createdSpecial)
        {
            if (piecesToRemove == null) return;
            // Las cascadas y los especiales animan al perro. Al llenarse la
            // correa, ayuda limpiando una fila, antes de la gravedad.
            int gained = (cascadeDepth > 0 ? 1 : 0) + (createdSpecial ? 1 : 0);
            if (gained <= 0) return;
            companionCharge = Mathf.Min(CompanionChargeTarget, companionCharge + gained);
            uiController?.UpdateCompanionCharge(companionCharge, CompanionChargeTarget);
            if (companionCharge < CompanionChargeTarget || boardController == null) return;

            companionCharge = 0;
            PieceView target = boardController.GetRandomPiece();
            if (target == null) return;
            foreach (PieceView piece in boardController.GetRowPieces(target.gridY))
            {
                if (piece != null && !piecesToRemove.Contains(piece)) piecesToRemove.Add(piece);
            }
            companionOnBoard?.Celebrate(target);
            uiController?.UpdateCompanionCharge(companionCharge, CompanionChargeTarget);
            uiController?.ShowComboBanner("TU COMPANERO AYUDA!", new Color(1f, 0.82f, 0.20f));
            feedbackController?.SpawnFloatingText(target.transform.position + Vector3.up * 0.55f,
                "GUAU! + FILA", new Color(1f, 0.84f, 0.22f), 38f);
            particleController?.PlaySpecialActivation(
                target,
                boardController.Columns,
                boardController.Rows,
                boardController.ActivePieceSpacing);
            hapticController?.PulseMatch(8);
        }

        private void HandleChainCompleted(List<PieceView> chain)
        {
            if (!stateController.CanSelectPieces()) return;

            if (cascadeDepth >= 4) earnedSkillStar = true;

            stateController.ChangeState(GameState.Resolving);
            if (uiController != null)
            {
                uiController.UpdateChainInfo(0, "");
            }

            MatchResolution resolution = boardController != null
                ? boardController.BuildMatchResolution(chain)
                : null;
            List<PieceView> piecesToRemove = resolution != null
                ? resolution.PiecesToRemove
                : chain;
            TryActivateCompanionAssist(piecesToRemove, resolution != null && resolution.CreatedSpecial != null);
            int pointsGained = scoreController != null && resolution != null
                ? scoreController.AddResolutionScore(
                    resolution.OriginalMatchCount,
                    piecesToRemove.Count,
                    resolution.CreatedSpecialType,
                    resolution.SpecialsActivated,
                    resolution.MegaCombo,
                    resolution.ComboKind)
                : scoreController != null ? scoreController.AddChainScore(chain.Count) : 0;
            bool hasSpecialImpact = resolution != null &&
                (resolution.MegaCombo || resolution.ColorBurstCombo || resolution.SpecialsActivated > 0);
            int clearedObstacles = boardController != null
                ? boardController.DamageObstacles(piecesToRemove, hasSpecialImpact)
                : 0;
            if (clearedObstacles > 0)
                RefreshSecondaryHazardUI(false);
            UpdateObjectiveProgress(
                piecesToRemove,
                resolution != null && resolution.CreatedSpecial != null ? 1 : 0,
                clearedObstacles);
            AppServices.Instance?.Progress.RegisterMatch(
                piecesToRemove != null ? piecesToRemove.Count : 0,
                resolution != null && resolution.CreatedSpecial != null ? 1 : 0,
                cascadeDepth);
            MarkVictoryPendingIfReady();
            TryPlayClimaxSlowMotion();

            if (piecesToRemove != null && piecesToRemove.Count > 0)
            {
                Vector3 centerPos = piecesToRemove[piecesToRemove.Count / 2].transform.position;

                if (feedbackController != null)
                {
                    bool namedSpecialEvent = resolution != null &&
                        (resolution.MegaCombo || resolution.SpecialsActivated > 0 || resolution.CreatedSpecial != null);
                    Vector3 scorePosition = namedSpecialEvent ? centerPos + Vector3.down * 0.34f : centerPos;
                    feedbackController.SpawnFloatingText(scorePosition, $"+{pointsGained:N0}", Color.yellow, 34f);
                    if (resolution != null && resolution.ComboKind != SpecialComboKind.None)
                        feedbackController.SpawnFloatingText(
                            centerPos + Vector3.up * 0.42f,
                            GetSpecialComboTitle(resolution.ComboKind),
                            GetSpecialComboColor(resolution.ComboKind),
                            resolution.MegaCombo ? 50f : 43f);
                    else if (resolution != null && resolution.SpecialsActivated > 0)
                        feedbackController.SpawnFloatingText(
                            centerPos + Vector3.up * 0.42f,
                            ResolutionContainsSpecial(resolution, PieceSpecialType.MegaBurst)
                                ? "¡SUPERNOVA!"
                                : GetActivatedSpecialTitle(resolution),
                            ResolutionContainsSpecial(resolution, PieceSpecialType.MegaBurst)
                                ? new Color(1f, 0.24f, 0.86f)
                                : new Color(1f, 0.55f, 0.08f),
                            ResolutionContainsSpecial(resolution, PieceSpecialType.MegaBurst) ? 48f : 40f);
                    else if (resolution != null && resolution.CreatedSpecial != null)
                        feedbackController.SpawnFloatingText(
                            resolution.CreatedSpecial.transform.position + Vector3.up * 0.42f,
                            GetCreatedSpecialTitle(resolution.CreatedSpecialType),
                            resolution.CreatedSpecialType == PieceSpecialType.MegaBurst
                                ? new Color(1f, 0.24f, 0.86f)
                                : new Color(1f, 0.82f, 0.12f),
                            resolution.CreatedSpecialType == PieceSpecialType.MegaBurst ? 48f : 38f);
                    feedbackController.TriggerCameraShake(
                        resolution != null && resolution.ComboKind != SpecialComboKind.None
                            ? (resolution.MegaCombo ? 0.14f : 0.095f)
                            : Mathf.Clamp(0.025f + piecesToRemove.Count * 0.004f, 0.035f, 0.09f),
                        resolution != null && resolution.ComboKind != SpecialComboKind.None ? 0.28f : 0.16f);
                }

                if (particleController != null)
                {
                    bool specialImpact = resolution != null &&
                        (resolution.SpecialsActivated > 0 || resolution.MegaCombo);
                    if (resolution != null && resolution.MegaCombo)
                    {
                        ChargeActivatedSpecials(resolution, 4);
                        StartCoroutine(PlaySpecialImpactAfterCharge(resolution, centerPos, 0.10f));
                    }
                    else if (resolution != null && resolution.SpecialsActivated > 0)
                    {
                        ChargeActivatedSpecials(resolution, 6);
                        StartCoroutine(PlaySpecialImpactAfterCharge(resolution, centerPos, 0.10f));
                    }
                    else if (resolution != null && resolution.CreatedSpecial != null)
                    {
                        resolution.CreatedSpecial.PlaySpecialCreationAnimation();
                        particleController.PlaySpecialCreated(resolution.CreatedSpecial);
                    }

                    // Keep the burst readable without creating dozens of
                    // particles at once on small mobile/WebGL screens.
                    bool compactScreen = Screen.width <= 600 || Screen.height <= 900;
                    int burstCount = compactScreen
                        ? (specialImpact ? 4 : Mathf.Clamp(3 + piecesToRemove.Count, 5, 9))
                        : (specialImpact ? 7 : Mathf.Clamp(6 + piecesToRemove.Count, 9, 18));
                    int maxBurstLocations = specialImpact ? (compactScreen ? 7 : 12) :
                        (compactScreen ? 12 : 20);
                    int stride = Mathf.Max(1, Mathf.CeilToInt(piecesToRemove.Count / (float)maxBurstLocations));
                    for (int i = 0; i < piecesToRemove.Count; i += stride)
                    {
                        PieceView piece = piecesToRemove[i];
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
                bool specialAudio = resolution != null &&
                    (resolution.MegaCombo || resolution.ColorBurstCombo ||
                     resolution.SpecialsActivated > 0 || resolution.CreatedSpecial != null);
                if (resolution != null && resolution.ComboKind != SpecialComboKind.None)
                    audioController.PlaySpecialComboSound(resolution.ComboKind);
                else if (specialAudio) audioController.PlaySpecialSound(resolution.MegaCombo);
                else audioController.PlayMatchSound(piecesToRemove != null ? Mathf.Max(3, piecesToRemove.Count) : 3);
            }
            if (hapticController != null)
            {
                if (resolution != null && resolution.ComboKind != SpecialComboKind.None)
                    hapticController.PulseSpecialCombo(resolution.ComboKind);
                else if (resolution != null && (resolution.SpecialsActivated > 0 || resolution.CreatedSpecial != null))
                    hapticController.PulseSpecial(
                        resolution.CreatedSpecial != null ? resolution.CreatedSpecialType : PieceSpecialType.AreaBlast,
                        resolution.MegaCombo);
                else
                    hapticController.PulseMatch(piecesToRemove != null ? Mathf.Max(3, piecesToRemove.Count) : 3);
            }

            if (gravityController != null)
            {
                OrderSpecialRemovalWave(piecesToRemove, resolution);
                float impactDelay = resolution != null && resolution.MegaCombo ? 0.32f :
                    resolution != null && resolution.SpecialsActivated > 0 ? 0.22f : 0.06f;
                StartCoroutine(ResolvePiecesAfterImpact(piecesToRemove, impactDelay));
            }
        }

        private void ChargeActivatedSpecials(MatchResolution resolution, int maximum)
        {
            if (resolution == null) return;
            int shown = 0;
            foreach (PieceView special in resolution.ActivatedSpecials)
            {
                if (special == null) continue;
                special.PlaySpecialChargeAnimation(0.18f);
                if (++shown >= maximum) break;
            }
        }

        private IEnumerator PlaySpecialImpactAfterCharge(MatchResolution resolution, Vector3 centerPos, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (particleController == null || resolution == null || boardController == null) yield break;

            if (resolution.ComboKind != SpecialComboKind.None)
            {
                particleController.PlaySpecialCombo(
                    resolution.ComboKind,
                    centerPos,
                    boardController.Columns,
                    boardController.Rows,
                    boardController.ActivePieceSpacing);
                if (resolution.MegaCombo) yield break;
            }

            foreach (PieceView special in resolution.ActivatedSpecials)
            {
                if (special == null) continue;
                particleController.PlaySpecialActivation(
                    special,
                    boardController.Columns,
                    boardController.Rows,
                    boardController.ActivePieceSpacing);
            }
        }

        private static void OrderSpecialRemovalWave(List<PieceView> pieces, MatchResolution resolution)
        {
            if (pieces == null || pieces.Count < 2 || resolution == null ||
                (!resolution.MegaCombo && resolution.SpecialsActivated <= 0)) return;

            pieces.Sort((a, b) => SpecialWaveDistance(a, resolution).CompareTo(SpecialWaveDistance(b, resolution)));
        }

        private static int SpecialWaveDistance(PieceView piece, MatchResolution resolution)
        {
            if (piece == null) return int.MaxValue;
            int best = int.MaxValue / 2;
            foreach (PieceView special in resolution.ActivatedSpecials)
            {
                if (special == null) continue;
                int dx = Mathf.Abs(piece.gridX - special.gridX);
                int dy = Mathf.Abs(piece.gridY - special.gridY);
                int distance = special.SpecialType == PieceSpecialType.RowBlast && dy == 0 ? dx :
                    special.SpecialType == PieceSpecialType.ColumnBlast && dx == 0 ? dy :
                    special.SpecialType == PieceSpecialType.AreaBlast ? Mathf.Max(dx, dy) :
                    special.SpecialType == PieceSpecialType.MegaBurst ? Mathf.Min(dx, dy) :
                    dx + dy + 20;
                if (distance < best) best = distance;
            }
            return best;
        }

        private IEnumerator ResolvePiecesAfterImpact(List<PieceView> piecesToRemove, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            yield return StartCoroutine(gravityController.ProcessRemovalAndRefill(piecesToRemove, () =>
            {
                ContinueAfterBoardSettled();
            }));
        }

        private void MarkVictoryPendingIfReady()
        {
            if (victoryPending || !IsCurrentObjectiveComplete()) return;
            victoryPending = true;
            gameTimer?.StopTimer();
            uiController?.ShowComboBanner("OBJETIVO COMPLETADO!", new Color(0.30f, 1f, 0.48f));
        }

        private void TryPlayClimaxSlowMotion()
        {
            if (climaxSlowMotionActive || victoryPending || gameTimer == null ||
                gameTimer.RemainingTime > 5f || !IsNearCurrentObjective()) return;
            if (climaxSlowMotionCoroutine != null) StopCoroutine(climaxSlowMotionCoroutine);
            climaxSlowMotionCoroutine = StartCoroutine(ClimaxSlowMotionRoutine());
        }

        private bool IsNearCurrentObjective()
        {
            LevelDefinition definition = CurrentLevelDefinition;
            int target = definition.objectiveType == LevelObjectiveType.Score
                ? definition.targetScore : definition.targetAmount;
            if (target <= 0) return false;
            int current = definition.objectiveType == LevelObjectiveType.Score
                ? (scoreController != null ? scoreController.CurrentScore : 0) : objectiveProgress;
            return current >= Mathf.CeilToInt(target * 0.78f) && current < target;
        }

        private IEnumerator ClimaxSlowMotionRoutine()
        {
            climaxSlowMotionActive = true;
            float previous = Time.timeScale;
            Time.timeScale = Mathf.Min(previous, 0.68f);
            uiController?.ShowComboBanner("ULTIMO EMPUJON!", new Color(1f, 0.76f, 0.20f));
            yield return new WaitForSecondsRealtime(0.72f);
            Time.timeScale = previous;
            climaxSlowMotionActive = false;
            climaxSlowMotionCoroutine = null;
        }

        private void ContinueAfterBoardSettled()
        {
            MarkVictoryPendingIfReady();
            List<PieceView> cascades = boardController != null
                ? boardController.FindMatches()
                : null;
            if (cascades != null && cascades.Count >= 3)
            {
                // Cascades always finish, even after the objective has been
                // reached. This keeps every reaction and point visible.
                cascadeDepth++;
                audioController?.PlayCascadeSound(cascadeDepth);
                audioController?.PlayCascadeBark(cascadeDepth);
                uiController?.ShowComboBanner($"CASCADA x{cascadeDepth + 1}", new Color(0.35f, 0.92f, 1f));
                GrantCascadeTimeBonus(cascades[cascades.Count / 2]);
                stateController.ChangeState(GameState.Playing);
                HandleMatch3Move(cascades);
                return;
            }

            if (victoryPending)
            {
                QueueNextFinalSpecial();
            }
            else if (gameTimer != null && gameTimer.RemainingTime <= 0f)
            {
                EndMatch(false);
            }
            else
            {
                stateController.ChangeState(GameState.Playing);
            }
        }

        private void GrantCascadeTimeBonus(PieceView origin)
        {
            if (gameTimer == null || victoryPending) return;
            if (cascadeTimeBonusSeconds <= 0f || cascadeDepth > maxRewardedCascadeDepth) return;

            float granted = gameTimer.AddTime(cascadeTimeBonusSeconds);
            int wholeSeconds = Mathf.RoundToInt(granted);
            if (wholeSeconds < 1 || feedbackController == null || origin == null) return;

            feedbackController.SpawnFloatingText(
                origin.transform.position + Vector3.up * 0.62f,
                $"+{wholeSeconds}s",
                new Color(0.42f, 1f, 0.62f),
                34f);
        }

        private void QueueNextFinalSpecial()
        {
            if (finalSpecialActivationQueued || stateController.CurrentState == GameState.GameOver) return;
            finalSpecialActivationQueued = true;
            stateController.ChangeState(GameState.Resolving);
            finalBonusCoroutine = StartCoroutine(TriggerNextFinalSpecial());
        }

        private IEnumerator TriggerNextFinalSpecial()
        {
            yield return new WaitForSeconds(0.24f);
            finalSpecialActivationQueued = false;
            finalBonusCoroutine = null;
            if (stateController.CurrentState == GameState.GameOver) yield break;

            List<PieceView> specials = boardController != null
                ? boardController.GetSpecialPieces()
                : null;
            if (specials == null || specials.Count == 0)
            {
                EndMatch(true);
                yield break;
            }

            if (finalBonusWave >= MaxFinalBonusWaves)
            {
                // Defensive guard for an extremely unlikely endless sequence
                // of new specials generated by final cascades.
                EndMatch(true);
                yield break;
            }

            PieceView special = specials[0];
            if (special == null)
            {
                QueueNextFinalSpecial();
                yield break;
            }

            finalBonusWave++;
            string bonusLabel = special.SpecialType == PieceSpecialType.MegaBurst
                ? "SUPERNOVA FINAL!"
                : $"BONUS FINAL x{finalBonusWave}";
            uiController?.ShowComboBanner(bonusLabel, new Color(1f, 0.76f, 0.16f));
            stateController.ChangeState(GameState.Playing);
            HandleChainCompleted(new List<PieceView> { special });
        }

        private static bool ResolutionContainsSpecial(MatchResolution resolution, PieceSpecialType type)
        {
            if (resolution == null) return false;
            foreach (PieceView special in resolution.ActivatedSpecials)
            {
                if (special != null && special.SpecialType == type) return true;
            }
            return false;
        }

        private static string GetCreatedSpecialTitle(PieceSpecialType type)
        {
            return type switch
            {
                PieceSpecialType.RowBlast => "RAYO HORIZONTAL!",
                PieceSpecialType.ColumnBlast => "RAYO VERTICAL!",
                PieceSpecialType.AreaBlast => "BOMBA DE AREA!",
                PieceSpecialType.ColorBurst => "ESTALLIDO DE COLOR!",
                PieceSpecialType.MegaBurst => "SUPERNOVA x6!",
                PieceSpecialType.BallBounce => "PELOTA REBOTE!",
                PieceSpecialType.Whistle => "SILBATO MAGICO!",
                _ => "FICHA ESPECIAL!"
            };
        }

        private static string GetActivatedSpecialTitle(MatchResolution resolution)
        {
            if (ResolutionContainsSpecial(resolution, PieceSpecialType.ColorBurst)) return "BARRIDO DE COLOR!";
            if (ResolutionContainsSpecial(resolution, PieceSpecialType.BallBounce)) return "PELOTA REBOTE!";
            if (ResolutionContainsSpecial(resolution, PieceSpecialType.Whistle)) return "SILBATO MAGICO!";
            if (ResolutionContainsSpecial(resolution, PieceSpecialType.AreaBlast)) return "ONDA EXPLOSIVA!";
            if (ResolutionContainsSpecial(resolution, PieceSpecialType.RowBlast)) return "RAYO HORIZONTAL!";
            if (ResolutionContainsSpecial(resolution, PieceSpecialType.ColumnBlast)) return "RAYO VERTICAL!";
            return "EXPLOSION ESPECIAL!";
        }

        private static string GetSpecialComboTitle(SpecialComboKind kind)
        {
            return kind switch
            {
                SpecialComboKind.DoubleRow => "DOBLE RAYO HORIZONTAL!",
                SpecialComboKind.DoubleColumn => "DOBLE RAYO VERTICAL!",
                SpecialComboKind.CrossBlast => "CRUCE RELAMPAGO!",
                SpecialComboKind.WideRow => "TRIPLE ONDA HORIZONTAL!",
                SpecialComboKind.WideColumn => "TRIPLE ONDA VERTICAL!",
                SpecialComboKind.DoubleArea => "DOBLE DETONACION!",
                SpecialComboKind.ColorSweep => "BARRIDO DE COLOR!",
                SpecialComboKind.BoardNova => "SUPERNOVA TOTAL!",
                _ => "FUSION ESPECIAL!"
            };
        }

        private static Color GetSpecialComboColor(SpecialComboKind kind)
        {
            return kind switch
            {
                SpecialComboKind.DoubleRow => new Color(0.12f, 0.92f, 1f),
                SpecialComboKind.DoubleColumn => new Color(0.74f, 0.38f, 1f),
                SpecialComboKind.CrossBlast => new Color(0.30f, 0.88f, 1f),
                SpecialComboKind.WideRow => new Color(1f, 0.46f, 0.18f),
                SpecialComboKind.WideColumn => new Color(1f, 0.38f, 0.72f),
                SpecialComboKind.DoubleArea => new Color(1f, 0.24f, 0.62f),
                SpecialComboKind.ColorSweep => new Color(1f, 0.86f, 0.12f),
                SpecialComboKind.BoardNova => new Color(1f, 0.22f, 0.88f),
                _ => Color.yellow
            };
        }

        private void HandleMatch3Move(List<PieceView> matches)
        {
            if (matches == null || matches.Count < 2 || !stateController.CanSelectPieces()) return;
            HandleChainCompleted(matches);
        }

        private void HandlePlayerMatch3Move(List<PieceView> matches)
        {
            cascadeDepth = 0;
            HandleMatch3Move(matches);
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
            if (climaxSlowMotionCoroutine != null)
            {
                StopCoroutine(climaxSlowMotionCoroutine);
                climaxSlowMotionCoroutine = null;
            }
            Time.timeScale = 1f;
            climaxSlowMotionActive = false;
            stateController.ChangeState(GameState.GameOver);

            if (gameTimer != null)
            {
                gameTimer.StopTimer();
            }

            if (audioController != null)
            {
                if (victory)
                    audioController.PlayVictorySound();
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
                    if (AppServices.Instance != null)
                        AppServices.Instance.Progress.SpendDogEnergy();
                    lives = AppServices.Instance != null ? AppServices.Instance.Progress.DogEnergy : Mathf.Max(0, lives - 1);
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
            if (timeRatio >= 0.60f && (!usedBoosterThisMatch || earnedSkillStar)) return 3;
            if (timeRatio >= 0.30f) return 2;
            return 1;
        }

        public void RestartGame()
        {
            lives = AppServices.Instance != null ? AppServices.Instance.Progress.DogEnergy : lives;
            if (lives <= 0) return;
            StartNewMatch();
        }

        public void StartNextLevel()
        {
            currentLevel = Mathf.Min(MaxPlayableLevel, currentLevel + 1);
            StartNewMatch();
        }

        private void SelectLevel(int level)
        {
            int unlockedLevel = CampaignCatalog.UnlockAllLevelsForTesting
                ? MaxPlayableLevel
                : PlayerPrefs.GetInt(UnlockedLevelKey, 1);
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
            usedBoosterThisMatch = true;
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
            usedBoosterThisMatch = true;
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
            usedBoosterThisMatch = true;
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

        private void HandleExitToMainMenuRequested()
        {
            gameTimer?.StopTimer();
            AppServices.Instance?.GoToMainMenu();
        }
    }
}
