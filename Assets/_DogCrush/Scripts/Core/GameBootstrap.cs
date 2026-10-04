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
        private bool IsMoveLimitedLevel => CurrentLevelDefinition != null && CurrentLevelDefinition.moveLimit > 0;
        private int shuffleBoosterCount;
        private int boneBoosterCount;
        private int foodBoosterCount;
        private int levelPawBoosters;
        private int levelBoneBoosters;
        private int levelFoodBoosters;
        private int objectiveProgress;
        private int movesRemaining;
        private int longestChain;
        private int cascadeDepth;
        private bool runtimeLevelDefinitionsReady;
        private bool victoryPending;
        private int matchPresentationVersion;
        private bool finalSpecialActivationQueued;
        private int finalBonusWave;
        private bool finalBonusCaptured;
        private readonly HashSet<PieceView> finalBonusSpecials = new HashSet<PieceView>();
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
        private int obstaclesClearedThisTurn;
        private bool vineShelterThisTurn;

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
                    if (uiController != null)
                    {
                        uiController.UpdateScore(current);
                        uiController.SetSecondaryScoreGoal(current, CurrentLevelDefinition.secondaryTargetScore);
                    }
                };
                scoreController.OnHighScoreChanged += (high) =>
                {
                    if (uiController != null) uiController.UpdateHighScore(high);
                };
                scoreController.OnComboTriggered += (mult, text) =>
                {
                    if (feedbackController != null)
                    {
                        if (!AccessibilitySettings.ReducedMotion)
                            feedbackController.TriggerCameraShake(0.15f, 0.25f);
                    }
                    // Match/cascade feedback below owns the single celebration label.
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
                    if (!IsMoveLimitedLevel && uiController != null)
                        uiController.UpdateTimer(remaining, gameTimer.Progress01);
                    RefreshFoodBoosterAvailability();
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
                uiController.OnMusicToggleRequested += HandleMusicToggleRequested;
                uiController.OnHapticsToggleRequested += HandleHapticsToggleRequested;
                uiController.OnReducedMotionToggleRequested += HandleReducedMotionToggleRequested;
                uiController.OnObstacleContrastToggleRequested += HandleObstacleContrastToggleRequested;
                uiController.OnSettingsVisibilityChanged += HandleSettingsVisibilityChanged;
                uiController.OnObjectiveBriefDismissRequested += () => dismissObjectiveIntro = true;
                uiController.OnMissionReviewRequested += () => uiController.ShowMissionReview(
                    presentationCampaign?.GetZoneForLevel(currentLevel)?.displayName,
                    BuildObjectiveIntroText(CurrentLevelDefinition), BuildObjectiveCoachingText(CurrentLevelDefinition),
                    BuildBoardCoachingText(CurrentLevelDefinition));
                uiController.OnMainMenuStartRequested += HandleMainMenuStartRequested;
                uiController.OnMainMenuLevelRequested += HandleMainMenuLevelRequested;
                uiController.OnMainMenuSettingsRequested += HandleMainMenuSettingsRequested;
                uiController.OnMainMenuTutorialRequested += HandleMainMenuTutorialRequested;
                uiController.OnReturnToMapRequested += HandleReturnToMapRequested;
                uiController.OnExitToMainMenuRequested += HandleExitToMainMenuRequested;
                UpdateSettingsUI();
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
            if (levelIntroRoutine != null) StopCoroutine(levelIntroRoutine);
            levelIntroRoutine = null;
            uiController?.HideObjectiveBrief();
            matchPresentationVersion++;
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
            finalBonusCaptured = false;
            finalBonusSpecials.Clear();
            cascadeDepth = 0;
            companionCharge = 0;
            uiController?.ResetCompanionPresentation();
            usedBoosterThisMatch = false;
            earnedSkillStar = false;
            obstaclesClearedThisTurn = 0;
            vineShelterThisTurn = false;
            // Invalidate any delayed gravity/refill callbacks from the
            // previous match before replacing its board.
            gravityController?.CancelResolution();
            selectionController?.CancelInteraction(true);
            if (selectionController != null) selectionController.InteractionBlocked = false;
            particleController?.ClearMatchImpacts();
            ClearHint();
            feedbackController?.ClearTransientFeedback();
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
            var atmosphere = GetComponent<GameplayWorldAtmosphere>();
            if (atmosphere == null) atmosphere = gameObject.AddComponent<GameplayWorldAtmosphere>();
            atmosphere.ApplyLevel(currentLevel);
            audioController?.PlayWorldTheme(CurrentLevelDefinition.boardTheme);
            particleController?.ApplyWorldTheme(CurrentLevelDefinition.boardTheme);

            if (uiController != null)
            {
                uiController.HideGameOver();
                uiController.UpdateChainInfo(0, "");
                objectiveProgress = 0;
                longestChain = 0;
                uiController.ApplyWorldTheme(CurrentLevelDefinition.boardTheme);
                movesRemaining = Mathf.Max(0, CurrentLevelDefinition.moveLimit);
                if (movesRemaining > 0)
                    uiController.SetMoveMode(movesRemaining, CurrentLevelDefinition.moveLimit);
                ApplyCurrentObjectiveToUI();
                RefreshSkillStarChallengeUI();
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
                // The selected companion now lives in the help card, not below the board.
                PrepareMagicBoneReward();
                RefreshSecondaryHazardUI();
            }

            if (gameTimer != null)
            {
                float duration = boardController != null && boardController.config != null
                    ? boardController.config.gameDurationSeconds
                    : gameTimer.durationSeconds;
                gameTimer.StartTimer(duration);
            }

            stateController.ChangeState(GameState.Playing);
            levelIntroRoutine = StartCoroutine(ShowLevelIntro());
            if (currentLevel == 1 && PlayerPrefs.GetInt("JoinDog_SwapTutorialSeen", 0) == 0)
                StartCoroutine(ShowFirstMoveTutorial());
        }

        private Coroutine levelIntroRoutine;
        private bool dismissObjectiveIntro;
        private CampaignCatalog presentationCampaign;

        private IEnumerator ShowLevelIntro()
        {
            gameTimer?.SetPaused(true, TimerPauseReason.Intro);
            yield return new WaitForSecondsRealtime(0.16f);
            if (currentLevel == 60 || currentLevel == 70 || currentLevel == 80 || currentLevel == 90 || currentLevel == 100)
            {
                string title = currentLevel == 100 ? "GRAN FINAL · SANTUARIO DORADO" :
                    currentLevel == 90 ? "FINAL DE ZONA · CAÑON DE RUBIES" :
                    currentLevel == 80 ? "FINAL DE ZONA · JARDINES CELESTES" :
                    currentLevel == 70 ? "FINAL DE ZONA · CUMBRE LUMINOSA" : "FINAL DE ZONA · VALLE AURORA";
                Color color = currentLevel == 100 ? new Color(1f, 0.82f, 0.22f) :
                    currentLevel == 90 ? new Color(1f, 0.32f, 0.20f) :
                    currentLevel == 80 ? new Color(0.50f, 1f, 0.86f) :
                    currentLevel == 70 ? new Color(1f, 0.78f, 0.20f) : new Color(1f, 0.38f, 0.78f);
                uiController?.ShowComboBanner(title, color);
                yield return new WaitForSecondsRealtime(0.90f);
            }
            if (currentLevel == 11)
            {
                uiController?.ShowComboBanner("¡NUEVA FICHA: PATITO!", new Color(1f, 0.85f, 0.15f));
                yield return new WaitForSecondsRealtime(1.5f);
            }
            else if (currentLevel == 31)
            {
                uiController?.ShowComboBanner("¡NUEVA FICHA: FRISBEE!", new Color(1f, .38f, .48f));
                yield return new WaitForSecondsRealtime(1.35f);
            }
            else if (currentLevel == 21)
            {
                uiController?.ShowComboBanner("¡NUEVA FICHA: CUERDA!", new Color(.16f, .88f, .86f));
                yield return new WaitForSecondsRealtime(1.35f);
            }
            else if (currentLevel == 41)
            {
                uiController?.ShowComboBanner("¡NUEVA FICHA: PINGÜINO!", new Color(.76f, .52f, 1f));
                yield return new WaitForSecondsRealtime(1.35f);
            }
            string world = presentationCampaign?.GetZoneForLevel(currentLevel)?.displayName;
            var definition = CurrentLevelDefinition;
            string hazardKey = definition.obstacleType != CellObstacleType.None &&
                boardController != null && boardController.RemainingObstacleCount > 0
                ? $"JoinDog_HazardHint_{definition.obstacleType}" : null;
            bool teachHazard = hazardKey != null && PlayerPrefs.GetInt(hazardKey, 0) == 0;
            dismissObjectiveIntro = false;
            bool shown = uiController != null && uiController.ShowObjectiveBrief(world,
                BuildObjectiveIntroText(definition), teachHazard ? BuildBoardCoachingText(definition) :
                BuildObjectiveCoachingText(definition), teachHazard);
            bool teachOpening=!string.IsNullOrEmpty(definition.openingStrategyTip) || definition.HasCollectionPhases;
            float deadline = Time.unscaledTime + ((teachHazard || teachOpening) && shown ? 2.6f : 1.05f);
            while (!dismissObjectiveIntro && Time.unscaledTime < deadline) yield return null;
            // Mark the existing hint key only after the lesson reached the
            // screen and completed or the player deliberately skipped it.
            if (teachHazard && shown && uiController != null && uiController.IsObjectiveBriefVisible)
            {
                PlayerPrefs.SetInt(hazardKey, 1);
                PlayerPrefs.Save();
            }
            uiController?.HideObjectiveBrief();
            gameTimer?.SetPaused(false, TimerPauseReason.Intro);
            levelIntroRoutine = null;
        }

        public static string BuildObjectiveIntroText(LevelDefinition definition)
        {
            if (definition == null) return "REVISA TU OBJETIVO";
            if (definition.HasCollectionPhases)
                return $"1: {definition.firstCollectionPhaseAmount} {PieceObjectiveLabel(definition.targetPieceType)}" +
                    $" · 2: {definition.targetAmount-definition.firstCollectionPhaseAmount} {PieceObjectiveLabel(definition.secondaryTargetPieceType)}";
            switch (definition.objectiveType)
            {
                case LevelObjectiveType.CollectPieces:
                    return $"REÚNE {definition.targetAmount} {PieceObjectiveLabel(definition.targetPieceType)}";
                case LevelObjectiveType.CollectTwoTypes:
                    return $"REÚNE {definition.targetAmount} ENTRE " +
                        $"{PieceObjectiveLabel(definition.targetPieceType)} Y {PieceObjectiveLabel(definition.secondaryTargetPieceType)}";
                case LevelObjectiveType.RescuePuppies:
                    return $"RESCATA {definition.targetAmount} CACHORROS";
                case LevelObjectiveType.DeliverToy:
                    // Compatibility for old saved level assets. New campaign
                    // data no longer creates delivery exits.
                    return $"REÚNE {definition.targetAmount} FICHAS";
                case LevelObjectiveType.LongChain:
                    return $"CREA {definition.targetAmount} ESPECIALES";
                case LevelObjectiveType.ClearObstacles:
                    return $"ROMPE {definition.targetAmount} OBSTÁCULOS";
                case LevelObjectiveType.Cascades:
                    return $"CONSIGUE {definition.targetAmount} CASCADAS";
                default:
                    return $"ALCANZA {definition.targetScore:N0} PUNTOS";
            }
        }

        public static string BuildObjectiveCoachingText(LevelDefinition definition)
        {
            if (definition == null) return "Consulta tu misión en la tarjeta superior.";
            if (definition.HasCollectionPhases)
                return "Dos fases: recoge primero la figura indicada. La segunda empieza en la siguiente combinación; sus fichas anteriores no se guardan. Las cascadas también cuentan para la fase activa.";
            if(!string.IsNullOrEmpty(definition.openingStrategyTip)) return definition.openingStrategyTip;
            string tip;
            switch (definition.objectiveType)
            {
                case LevelObjectiveType.LongChain:
                    tip = "Combina 4 o más para crear especiales."; break;
                case LevelObjectiveType.CollectTwoTypes:
                    tip = "Ambos tipos suman al mismo contador."; break;
                case LevelObjectiveType.CollectPieces:
                    tip = "Busca combinaciones con la ficha de tu misión."; break;
                case LevelObjectiveType.Cascades:
                    tip = "Cada nueva combinación tras una caída cuenta."; break;
                case LevelObjectiveType.RescuePuppies:
                    tip = "Rompe las jaulas para liberar a los cachorros."; break;
                case LevelObjectiveType.ClearObstacles:
                    tip = definition.obstacleType == CellObstacleType.Lantern ? "Los especiales rompen más capas de faroles." :
                        definition.obstacleType == CellObstacleType.Sand ? "Combina junto a la arena para limpiarla." :
                        "Combina sobre los obstáculos para romper sus capas."; break;
                default:
                    tip = "Combina fichas y aprovecha los especiales."; break;
            }
            return definition.secondaryTargetScore > 0
                ? tip + $" También necesitas {definition.secondaryTargetScore:N0} puntos." : tip;
        }

        public static string BuildBoardCoachingText(LevelDefinition definition)
        {
            if (definition == null) return "Combina 4 o más para crear especiales.";
            // Teach the rule actually present in this level, rather than
            // assuming every board in a chapter contains the same obstacle.
            if (definition.obstacleCount > 0)
            {
                switch (definition.obstacleType)
                {
                    case CellObstacleType.Vine:
                        return "ENREDADERAS · Combina sobre ellas. Si un turno no rompe ninguna, pueden crecer.";
                    case CellObstacleType.Sand:
                        return "ARENA · Combina encima o en una casilla vecina, sin diagonales, para limpiarla.";
                    case CellObstacleType.Ice:
                        return "HIELO · Los especiales quitan dos capas y alcanzan las casillas vecinas, sin diagonales.";
                    case CellObstacleType.Lantern:
                        return "FAROLES · Los especiales quitan dos capas y alcanzan las casillas vecinas, sin diagonales.";
                    case CellObstacleType.PuppyCage:
                        return "JAULAS · Combina sobre ellas hasta romper todas sus capas y liberar al cachorro.";
                }
            }
            return "ESPECIALES · Combina 4 o más para crearlos. Al seleccionarlos en intercambio, verás su alcance directo.";
        }

        private static string PieceObjectiveLabel(PieceType type)
        {
            switch (type)
            {
                case PieceType.Dog: return "PERRITOS";
                case PieceType.Bone: return "HUESOS";
                case PieceType.Ball: return "PELOTAS";
                case PieceType.Food: return "COMIDAS";
                case PieceType.Collar: return "COLLARES";
                case PieceType.Duck: return "PATITOS";
                case PieceType.Frisbee: return "FRISBEES";
                case PieceType.Penguin: return "PINGÜINOS";
                case PieceType.Rope: return "CUERDAS";
                default: return "FICHAS";
            }
        }

        private void EnsureCompanionOnBoard()
        {
            if (boardController == null) return;
            if (companionOnBoard == null)
            {
                GameObject companion = new GameObject("CompanionOnBoard_Runtime");
                companionOnBoard = companion.AddComponent<CompanionOnBoardController>();
            }
            // Reserve the companion a visible, central position below the board
            // instead of anchoring it to the left edge (which made it disappear
            // behind the mobile viewport). Prefer the bottom-centre cell so the
            // companion reads as part of the HUD without covering a match.
            int centerColumn = Mathf.Clamp(boardController.Columns / 2, 0, boardController.Columns - 1);
            PieceView anchor = boardController.GetPieceAt(centerColumn, 0)
                ?? boardController.GetPieceAt(Mathf.Max(0, centerColumn - 1), 0)
                ?? boardController.GetRandomPiece();
            companionOnBoard.Setup(MapCharacterSelection.LoadSelectedSprite(), anchor);
        }

        private IEnumerator ShowFirstMoveTutorial()
        {
            yield return new WaitForSeconds(1.75f);
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
            PieceView attention = stateController != null && stateController.CanSelectPieces() &&
                selectionController != null && !selectionController.InteractionBlocked
                ? selectionController.AttentionTarget : null;
            uiController?.SetCompanionAttention(attention != null
                ? (Vector3?)attention.transform.position : null);
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
                (selectionController != null && selectionController.HasPendingSwap) ||
                gameTimer == null || !gameTimer.IsRunning || gameTimer.IsPaused)
            {
                ClearHint();
                return;
            }

            if (hintPieceA != null) return;

            idleSeconds += Time.deltaTime;
            if (idleSeconds < hintDelaySeconds) return;

            LevelDefinition definition = CurrentLevelDefinition;
            PieceType preferredType = definition.objectiveType == LevelObjectiveType.CollectPieces
                ? definition.targetPieceType
                : PieceType.None;
            bool prioritizeObstacles = definition.objectiveType == LevelObjectiveType.ClearObstacles ||
                boardController.RemainingObstacleCount > 0;
            if (boardController.TryFindHintMoveForObjective(
                preferredType, prioritizeObstacles, out PieceView first, out PieceView second))
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
            boardController.config.initialPieceRows = definition.initialPieceRows;
            boardController.config.companionGardenCells = definition.companionGardenCells;
            boardController.config.vineShelterCells = definition.vineShelterCells;
            boardController.config.festivalBellCells = definition.festivalBellCells;
            boardController.config.coastTideCells = definition.coastTideCells;
            boardController.config.mountainWarmCells = definition.mountainWarmCells;
            boardController.config.auroraPrismCells = definition.auroraPrismCells;
            boardController.config.summitCrystalCells = definition.summitCrystalCells;
            boardController.config.celestialSproutCells = definition.celestialSproutCells;
            boardController.config.rubyGeyserCells = definition.rubyGeyserCells;
            boardController.config.sanctuarySealCells = definition.sanctuarySealCells;
            boardController.config.gameDurationSeconds = CurrentLevelDuration;
            // Duck is the sixth illustrated piece, introduced in the forest.
            boardController.config.typeCount = Mathf.Clamp(definition.typeCount, 1, 9);
            boardController.config.activePieceTypes = definition.activePieceTypes;
            boardController.config.minChainLength = Mathf.Clamp(definition.minChainLength, 3, 5);
            boardController.config.boardShape = definition.boardShape;
            boardController.config.boardTheme = definition.boardTheme;
            boardController.config.obstacleType = definition.obstacleType;
            boardController.config.obstacleCount = Mathf.Max(0, definition.obstacleCount);
            boardController.config.obstacleDurability = Mathf.Clamp(definition.obstacleDurability, 1, 3);
            boardController.config.obstacleCells = definition.obstacleCells;
            boardController.config.converterCells = definition.converterCells;
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
                                : theme == BoardTheme.Aurora
                                    ? new Color(0.58f, 0.72f, 0.90f, 1f)
                                    : theme == BoardTheme.LuminousSummit
                                        ? new Color(0.74f, 0.70f, 0.92f, 1f)
                                        : theme == BoardTheme.CelestialGarden
                                            ? new Color(0.50f, 0.92f, 0.92f, 1f)
                                            : theme == BoardTheme.RubyCanyon
                                                ? new Color(0.88f, 0.42f, 0.38f, 1f)
                                                : theme == BoardTheme.GoldenSanctuary
                                                    ? new Color(0.86f, 0.70f, 0.42f, 1f)
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
                                : theme == BoardTheme.Aurora
                                    ? new Color(0.055f, 0.06f, 0.20f)
                                    : theme == BoardTheme.LuminousSummit
                                        ? new Color(0.08f, 0.06f, 0.22f)
                                        : theme == BoardTheme.CelestialGarden
                                            ? new Color(0.03f, 0.24f, 0.34f)
                                            : theme == BoardTheme.RubyCanyon
                                                ? new Color(0.22f, 0.025f, 0.04f)
                                                : theme == BoardTheme.GoldenSanctuary
                                                    ? new Color(0.13f, 0.07f, 0.24f)
                                                    : new Color(0.12f, 0.22f, 0.30f);
            }
        }

        private void ApplyCurrentObjectiveToUI()
        {
            if (uiController == null) return;
            LevelDefinition definition = CurrentLevelDefinition;
            if (definition.HasCollectionPhases)
            {
                bool second = objectiveProgress >= definition.firstCollectionPhaseAmount;
                int offset = second ? definition.firstCollectionPhaseAmount : 0;
                int target = second ? definition.targetAmount-offset : definition.firstCollectionPhaseAmount;
                PieceType active = second ? definition.secondaryTargetPieceType : definition.targetPieceType;
                uiController.SetCustomObjective(currentLevel, $"{(second ? 2 : 1)}/2 · {PieceObjectiveLabel(active)}",
                    target, Mathf.Clamp(objectiveProgress-offset,0,target));
                uiController.SetObjectivePieceIcons(new LevelDefinition { objectiveType=LevelObjectiveType.CollectPieces, targetPieceType=active });
                uiController.SetSecondaryScoreGoal(scoreController != null ? scoreController.CurrentScore : 0, definition.secondaryTargetScore);
                return;
            }
            switch (definition.objectiveType)
            {
                case LevelObjectiveType.CollectPieces:
                    uiController.SetCustomObjective(
                        currentLevel,
                        $"{definition.targetPieceType} x",
                        definition.targetAmount,
                        objectiveProgress);
                    break;
                case LevelObjectiveType.CollectTwoTypes:
                    uiController.SetCustomObjective(
                        currentLevel,
                        $"{definition.targetPieceType} + {definition.secondaryTargetPieceType}",
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
                        definition.obstacleType == CellObstacleType.Sand ? "ARENA" :
                        definition.obstacleType == CellObstacleType.PuppyCage ? "CACHORROS" : "HIELO";
                    uiController.SetCustomObjective(
                        currentLevel,
                        obstacleLabel,
                        definition.targetAmount,
                        objectiveProgress);
                    break;
                case LevelObjectiveType.RescuePuppies:
                    uiController.SetCustomObjective(currentLevel, "CACHORROS RESCATADOS",
                        definition.targetAmount, objectiveProgress);
                    break;
                case LevelObjectiveType.DeliverToy:
                    uiController.SetCustomObjective(currentLevel,
                        $"{definition.targetPieceType} · SALIDA", definition.targetAmount, objectiveProgress);
                    break;
                case LevelObjectiveType.Cascades:
                    uiController.SetCustomObjective(currentLevel, "CASCADAS",
                        definition.targetAmount, objectiveProgress);
                    break;
                default:
                    uiController.SetLevelObjective(currentLevel, definition.targetScore);
                    break;
            }
            uiController.SetObjectivePieceIcons(definition);
            uiController.SetSecondaryScoreGoal(
                scoreController != null ? scoreController.CurrentScore : 0,
                definition.secondaryTargetScore);
        }

        private void RefreshSecondaryHazardUI()
        {
            if (uiController == null || boardController == null || boardController.config == null)
                return;

            CellObstacleType type = boardController.config.obstacleType;
            int remaining = boardController.RemainingObstacleCount;
            if (type == CellObstacleType.None || remaining <= 0 ||
                (CurrentLevelDefinition.objectiveType == LevelObjectiveType.ClearObstacles ||
                 CurrentLevelDefinition.objectiveType == LevelObjectiveType.RescuePuppies))
            {
                uiController.SetSecondaryHazard(null, 0);
                return;
            }

            string label = type == CellObstacleType.Vine ? "ENREDADERAS" :
                type == CellObstacleType.Lantern ? "FAROLES" :
                type == CellObstacleType.Sand ? "ARENA" : "HIELO";
            uiController.SetSecondaryHazard(label, remaining);

        }

        private void UpdateObjectiveProgress(List<PieceView> removedPieces, int specialsCreated, int obstaclesCleared = 0)
        {
            LevelDefinition definition = CurrentLevelDefinition;
            if (definition.HasCollectionPhases)
            {
                // Fix the active phase for this whole resolution. A simultaneous
                // clear cannot advance both phases or bank future mission pieces.
                bool second = objectiveProgress >= definition.firstCollectionPhaseAmount;
                PieceType active = second ? definition.secondaryTargetPieceType : definition.targetPieceType;
                int cap = second ? definition.targetAmount : definition.firstCollectionPhaseAmount;
                if (removedPieces != null)
                    foreach (PieceView piece in removedPieces)
                        if (piece != null && piece.type == active) objectiveProgress = Mathf.Min(cap,objectiveProgress+1);
                ApplyCurrentObjectiveToUI();
                if (!second && objectiveProgress == definition.firstCollectionPhaseAmount)
                    uiController?.ShowCompanionReaction(CompanionReaction($"FASE 2/2: {PieceObjectiveLabel(definition.secondaryTargetPieceType)}"));
                return;
            }
            if ((definition.objectiveType == LevelObjectiveType.CollectPieces ||
                definition.objectiveType == LevelObjectiveType.CollectTwoTypes) && removedPieces != null)
            {
                foreach (PieceView piece in removedPieces)
                {
                    if (piece != null && (piece.type == definition.targetPieceType ||
                        definition.objectiveType == LevelObjectiveType.CollectTwoTypes &&
                        piece.type == definition.secondaryTargetPieceType))
                        objectiveProgress++;
                }
            }
            else if (definition.objectiveType == LevelObjectiveType.DeliverToy && removedPieces != null)
            {
                foreach (PieceView piece in removedPieces)
                {
                    if (piece != null && piece.type == definition.targetPieceType &&
                        boardController != null && boardController.IsConverterCell(piece.gridX, piece.gridY))
                        objectiveProgress++;
                }
            }
            else if (definition.objectiveType == LevelObjectiveType.LongChain)
            {
                objectiveProgress += Mathf.Max(0, specialsCreated);
            }
            else if (definition.objectiveType == LevelObjectiveType.ClearObstacles ||
                definition.objectiveType == LevelObjectiveType.RescuePuppies && definition.obstacleType == CellObstacleType.PuppyCage)
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
            bool secondaryComplete = definition.secondaryTargetScore <= 0 ||
                (scoreController != null && scoreController.CurrentScore >= definition.secondaryTargetScore);
            if (definition.objectiveType == LevelObjectiveType.Score)
            {
                return scoreController != null && scoreController.CurrentScore >= definition.targetScore && secondaryComplete;
            }
            return objectiveProgress >= definition.targetAmount && secondaryComplete;
        }

        private static PieceType[] ThematicPiecePool(int level)
        {
            if (level <= 10) return new[] { PieceType.Dog, PieceType.Bone, PieceType.Ball, PieceType.Food, PieceType.Collar };
            if (level <= 20) return new[] { PieceType.Dog, PieceType.Bone, PieceType.Food, PieceType.Collar, PieceType.Duck, PieceType.Ball };
            if (level <= 30) return new[] { PieceType.Dog, PieceType.Ball, PieceType.Food, PieceType.Duck, PieceType.Rope, PieceType.Collar, PieceType.Bone };
            if (level <= 40) return new[] { PieceType.Dog, PieceType.Bone, PieceType.Ball, PieceType.Food, PieceType.Collar, PieceType.Rope, PieceType.Frisbee, PieceType.Duck };
            return new[] { PieceType.Dog, PieceType.Bone, PieceType.Ball, PieceType.Food, PieceType.Collar, PieceType.Duck, PieceType.Rope, PieceType.Frisbee, PieceType.Penguin };
        }

        private void EnsureLevelDefinitions()
        {
            if (runtimeLevelDefinitionsReady && levelDefinitions != null &&
                levelDefinitions.Count == MaxPlayableLevel) return;
            levelDefinitions = new List<LevelDefinition>();
            CampaignCatalog campaign = CampaignCatalog.LoadOrCreateRuntime();
            presentationCampaign = campaign;
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
                    typeCount = level >= 41 ? 9 : level >= 31 ? 8 : level >= 21 ? 7 : level >= 11 ? 6 : 5,
                    activePieceTypes = ThematicPiecePool(level),
                    minChainLength = 3,
                    objectiveType = entry.objectiveKind == CampaignObjectiveKind.Collect
                        ? LevelObjectiveType.CollectPieces
                        : entry.objectiveKind == CampaignObjectiveKind.CollectTwoTypes
                            ? LevelObjectiveType.CollectTwoTypes
                        : entry.objectiveKind == CampaignObjectiveKind.LongMatch
                            ? LevelObjectiveType.LongChain
                        : entry.objectiveKind == CampaignObjectiveKind.ClearObstacles
                            ? LevelObjectiveType.ClearObstacles
                        : entry.objectiveKind == CampaignObjectiveKind.RescuePuppies
                            ? LevelObjectiveType.RescuePuppies
                            : entry.objectiveKind == CampaignObjectiveKind.DeliverToy
                                ? LevelObjectiveType.CollectTwoTypes
                            : entry.objectiveKind == CampaignObjectiveKind.Cascades
                                    ? LevelObjectiveType.Cascades
                                    : LevelObjectiveType.Score,
                    targetPieceType = (PieceType)Mathf.Clamp((int)entry.targetPiece, 0, 8),
                    secondaryTargetPieceType = (PieceType)Mathf.Clamp((int)entry.secondaryTargetPiece, 0, 8),
                    targetAmount = CampaignCatalog.BalancedTargetAmount(entry),
                    moveLimit = entry.moveLimit,
                    boardShape = entry.diamondBoard
                        ? BoardShape.Diamond
                        : entry.roundedBoard
                            ? BoardShape.Rounded
                            : BoardShape.Full,
                    boardTheme = level <= 10 ? BoardTheme.Meadow :
                        level <= 20 ? BoardTheme.Forest :
                        level <= 30 ? BoardTheme.Festival :
                        level <= 40 ? BoardTheme.Coast :
                        level <= 50 ? BoardTheme.Mountain :
                        level <= 60 ? BoardTheme.Aurora :
                        level <= 70 ? BoardTheme.LuminousSummit :
                        level <= 80 ? BoardTheme.CelestialGarden :
                        level <= 90 ? BoardTheme.RubyCanyon : BoardTheme.GoldenSanctuary,
                    obstacleType = entry.obstacleType == CampaignObstacleKind.Vine
                        ? CellObstacleType.Vine
                        : entry.obstacleType == CampaignObstacleKind.Lantern
                            ? CellObstacleType.Lantern
                                : entry.obstacleType == CampaignObstacleKind.Sand
                                    ? CellObstacleType.Sand
                                    : entry.obstacleType == CampaignObstacleKind.Ice
                                        ? CellObstacleType.Ice
                                        : entry.obstacleType == CampaignObstacleKind.PuppyCage
                                            ? CellObstacleType.PuppyCage
                                    : CellObstacleType.None,
                    obstacleCount = entry.obstacleCount,
                    obstacleDurability = entry.obstacleDurability,
                    pawBoosterCount = entry.pawBoosters,
                    boneBoosterCount = entry.boneBoosters,
                    foodBoosterCount = entry.foodBoosters
                };

                if (level >= 51)
                {
                    definition.obstacleCells = BuildLateCampaignObstaclePattern(
                        level, definition.columns, definition.rows);
                    definition.layoutRows = BuildLateCampaignLayout(level, definition.columns, definition.rows);
                    definition.converterCells = BuildConverterCells(level, definition.columns, definition.rows);
                }
                if (level >= 31 && definition.objectiveType != LevelObjectiveType.Score)
                    definition.secondaryTargetScore = Mathf.RoundToInt(definition.targetScore * 0.45f);

                // A hand-authored asset overrides only the selected level.
                // Missing assets keep the established campaign generator as a
                // safe fallback while the catalogue is migrated incrementally.
                LevelDesignAsset manual = Resources.Load<LevelDesignAsset>(
                    $"Campaign/Levels/level_{level:000}");
                if (manual != null && manual.level == level)
                    manual.ApplyTo(definition);
                // Migrate any old serialized delivery definition to the new
                // clear collection mission, so no exit marker or converter
                // cell survives on a legacy asset.
                if (definition.objectiveType == LevelObjectiveType.DeliverToy)
                {
                    definition.objectiveType = LevelObjectiveType.CollectTwoTypes;
                    definition.converterCells = System.Array.Empty<string>();
                }
                EnsureObjectivePiecesInPool(definition);
                // Legacy authored rescue levels used ice/vines and their clears
                // never advanced the rescue counter. A rescue requires real cages,
                // with enough puppies to meet its existing quota.
                if (definition.objectiveType == LevelObjectiveType.RescuePuppies)
                {
                    definition.obstacleType = CellObstacleType.PuppyCage;
                    definition.obstacleCount = Mathf.Max(definition.obstacleCount,definition.targetAmount);
                }
                if(level==30 && definition.boardTheme==BoardTheme.Festival &&
                    definition.objectiveType==LevelObjectiveType.ClearObstacles && definition.obstacleType==CellObstacleType.Lantern)
                {
                    definition.initialPieceRows = new[]{
                        "....5....", "...244...", "..56436..", ".0240166.", "423113144",
                        "664030054", ".3620144.", "..36014..", "...324...", "....0...."
                    };
                    definition.obstacleCells = new[]{
                        "3,2","3,3","3,4","3,5","3,6","3,7",
                        "4,1","4,2","4,3","4,4","4,5","4,6","4,7","4,8",
                        "5,2","5,3","5,4","5,5","5,6","5,7","6,4","6,5"
                    };
                    definition.openingStrategyTip = "FINAL DEL FESTIVAL: combina en L para crear una explosión de área y prepara un rayo vertical junto a ella. Intercámbialos para barrer tres columnas; los faroles también reciben el impacto vecino. Otras especiales pueden abrirlos.";
                }
                if(level==40 && definition.boardTheme==BoardTheme.Coast &&
                    definition.objectiveType==LevelObjectiveType.ClearObstacles && definition.obstacleType==CellObstacleType.Sand)
                {
                    definition.initialPieceRows = new[]{
                        "..46300..", ".5756575.", "247214134", "073641712", "647117061",
                        "574361060", "663140301", "173776011", ".1021544.", "..04740.."
                    };
                    definition.obstacleCells = new[]{
                        "0,3","1,3","2,3","3,3","4,3","5,3","6,3","7,3",
                        "4,4","5,4","6,4","7,4","8,4",
                        "3,5","4,5","5,5","6,5","7,5","8,5",
                        "4,6","5,6","6,6","7,6","8,6"
                    };
                    definition.coastTideCells = new[]{"7,3"};
                    definition.openingStrategyTip = "FINAL DE LA COSTA: una L junto a la ola prepara una cascada de seis huesos y una megaespecial. Combina cuatro frisbees en vertical para dejar un cometa a su lado. Su pareja barre el tablero y suma puntos; la arena tiene dos capas. También puedes limpiarla con otras combinaciones.";
                }
                if(level==50 && definition.boardTheme==BoardTheme.Mountain &&
                    definition.objectiveType==LevelObjectiveType.ClearObstacles && definition.obstacleType==CellObstacleType.Ice)
                {
                    definition.initialPieceRows = new[]{
                        "....7....", "...115...", "..63747..", ".4261157.", "515633110",
                        "051030060", ".6470161.", "..36013..", "...440...", "....5...."
                    };
                    definition.obstacleCells = new[]{
                        "3,1","4,1","5,1","2,2","3,2","4,2","5,2","6,2",
                        "1,3","2,3","3,3","4,3","5,3","6,3","7,3",
                        "1,4","2,4","3,4","4,4","5,4","6,4","7,4",
                        "3,5","4,5","5,5","6,5"
                    };
                    definition.mountainWarmCells = new[]{"4,2","4,3","5,4","6,4","5,2","5,3","4,4"};
                    definition.openingStrategyTip = "FINAL DE LA MONTAÑA: prepara dos explosiones de área en L sobre el calor y combínalas cuando queden juntas. El hielo tiene tres capas: el calor debilita vecinos y la pareja rompe dos. Termina los hielos restantes con especiales y reúne los puntos de la misión.";
                }
                if(level==60 && definition.boardTheme==BoardTheme.Aurora &&
                    definition.objectiveType==LevelObjectiveType.ClearObstacles && definition.obstacleType==CellObstacleType.Lantern)
                {
                    definition.initialPieceRows = new[]{
                        "....1....", "...503...", "..51024..", ".5603051.", "562103456",
                        "241311236", ".4613035.", "..25331..", "...042...", "....6...."
                    };
                    definition.obstacleCells = new[]{
                        "3,1","4,1","5,1","2,2","3,2","4,2","5,2","6,2",
                        "1,3","2,3","3,3","4,3","5,3","6,3","7,3",
                        "1,4","2,4","3,4","4,4","5,4","6,4","7,4",
                        "2,5","3,5","4,5","5,5","6,5","3,6","4,6","5,6"
                    };
                    definition.auroraPrismCells = new[]{"2,4","4,6"};
                    definition.openingStrategyTip = "FINAL DE AURORA: prepara un rayo de fila y otro de columna con grupos de cuatro. Retira las fichas sobre los prismas para debilitar los faroles de sus columnas. Junta los rayos y cruza su luz; después termina los faroles restantes y reúne los puntos de la misión.";
                }
                if(level==93 && definition.boardTheme==BoardTheme.GoldenSanctuary && definition.obstacleType==CellObstacleType.Lantern)
                {
                    definition.sanctuarySealCells = new[]{"4,3","4,6"};
                    definition.openingStrategyTip = "SELLOS DORADOS: crea una especial sobre un sello. Quita una capa al farol adicional más cercano, sin retirar su ficha. Solo alcanza un farol; cada sello se usa una vez. Si no hay faroles adicionales, se conserva.";
                }
                if(level==83 && definition.boardTheme==BoardTheme.RubyCanyon && definition.obstacleType==CellObstacleType.Sand)
                {
                    definition.rubyGeyserCells = new[]{"4,3","4,6"};
                    definition.openingStrategyTip = "GÉISERES RUBÍ: activa una especial sobre un géiser. Quita una capa de arena dos casillas arriba y abajo, sin retirar sus fichas ni duplicar golpes. Cada géiser se usa una vez; si no alcanza arena adicional, se conserva.";
                }
                if(level==73 && definition.boardTheme==BoardTheme.CelestialGarden && definition.obstacleType==CellObstacleType.Vine)
                {
                    definition.celestialSproutCells = new[]{"4,3","4,6"};
                    definition.openingStrategyTip = "BROTES CELESTES: crea una especial sobre un brote. Poda una capa de las enredaderas vecinas en cruz, sin retirar fichas. Cada brote se usa una vez; si no hay enredaderas adicionales, se conserva. Romper una enredadera impide su crecimiento ese turno.";
                }
                if(level==63 && definition.boardTheme==BoardTheme.LuminousSummit && definition.obstacleType==CellObstacleType.Ice)
                {
                    definition.summitCrystalCells = new[]{"4,3","4,6"};
                    definition.openingStrategyTip = "CRISTALES DE CUMBRE: crea una especial sobre un cristal. Quita una capa al hielo de sus cuatro diagonales cercanas, sin retirar fichas. Cada cristal se usa una vez; si no hay hielo adicional, se conserva.";
                }
                if(level==53 && definition.boardTheme==BoardTheme.Aurora && definition.obstacleType==CellObstacleType.Lantern)
                {
                    definition.auroraPrismCells = new[]{"4,3","6,6"};
                    definition.openingStrategyTip = "PRISMAS DE AURORA: combina retirando la ficha sobre un prisma. Su luz quita una capa a las linternas de esa columna, sin retirar sus fichas. Cada prisma se usa una vez; si no hay linternas adicionales, se conserva.";
                }
                if(level==33 && definition.boardTheme==BoardTheme.Coast && definition.obstacleType==CellObstacleType.Sand)
                {
                    definition.coastTideCells = new[]{"2,3","6,6"};
                    definition.openingStrategyTip = "OLAS DE COSTA: combina retirando la ficha sobre una ola. Quita una capa de arena de esa fila, sin retirar otras fichas. Una vez por ola; se conserva si no alcanza arena extra.";
                }
                if(level==43 && definition.boardTheme==BoardTheme.Mountain && definition.obstacleType==CellObstacleType.Ice)
                {
                    definition.mountainWarmCells = new[]{"4,3","4,6"};
                    definition.openingStrategyTip = "PIEDRAS CÁLIDAS: combina retirando la ficha sobre una llama. Quita una capa del hielo vecino arriba, abajo, izquierda y derecha. Una vez por piedra; se conserva si no alcanza hielo extra. Las demás fichas permanecen.";
                }
                levelDefinitions.Add(definition);
            }
            runtimeLevelDefinitionsReady = levelDefinitions.Count == MaxPlayableLevel;
        }

        private static void EnsureObjectivePiecesInPool(LevelDefinition definition)
        {
            if (definition.activePieceTypes == null ||
                (definition.objectiveType != LevelObjectiveType.CollectPieces &&
                 definition.objectiveType != LevelObjectiveType.CollectTwoTypes)) return;
            // Manual levels can use fewer types than the world's full pool.
            // Keep every required figure inside that active prefix without
            // increasing variety or changing the authored difficulty.
            var ordered = new List<PieceType>();
            ordered.Add(definition.targetPieceType);
            if (definition.objectiveType == LevelObjectiveType.CollectTwoTypes &&
                definition.secondaryTargetPieceType != definition.targetPieceType)
                ordered.Add(definition.secondaryTargetPieceType);
            PieceType introduced = definition.level == 11 ? PieceType.Duck :
                definition.level == 21 ? PieceType.Rope :
                definition.level == 31 ? PieceType.Frisbee :
                definition.level == 41 ? PieceType.Penguin : PieceType.None;
            if (introduced != PieceType.None && !ordered.Contains(introduced)) ordered.Add(introduced);
            foreach (PieceType type in definition.activePieceTypes)
                if (!ordered.Contains(type)) ordered.Add(type);
            definition.activePieceTypes = ordered.ToArray();
        }

        public static string[] BuildLateCampaignLayout(int level, int columns, int rows)
        {
            if (level < 51 || columns < 5 || rows < 5) return null;
            char[][] mask = new char[rows][];
            for (int row = 0; row < rows; row++)
            {
                mask[row] = new string('.', columns).ToCharArray();
            }

            int variant = (level - 51) % 4;
            for (int x = 0; x < columns; x++)
            {
                int leftDistance = x;
                int rightDistance = columns - 1 - x;
                int topInset = 0;
                int bottomInset = 0;
                if (variant == 0)
                {
                    topInset = leftDistance == 0 ? 2 : leftDistance == 1 ? 1 : 0;
                    bottomInset = rightDistance == 0 ? 2 : rightDistance == 1 ? 1 : 0;
                }
                else if (variant == 1)
                {
                    topInset = rightDistance == 0 ? 2 : rightDistance == 1 ? 1 : 0;
                    bottomInset = leftDistance == 0 ? 2 : leftDistance == 1 ? 1 : 0;
                }
                else if (variant == 2)
                {
                    topInset = x % 3 == 0 ? 1 : 0;
                    bottomInset = x % 3 == 2 ? 1 : 0;
                }
                else
                {
                    topInset = (x == 0 || x == columns - 2) ? 2 : x == 1 ? 1 : 0;
                    bottomInset = (x == 1 || x == columns - 1) ? 2 : x == columns - 2 ? 1 : 0;
                }

                for (int i = 0; i < topInset; i++) mask[i][x] = '#';
                for (int i = 0; i < bottomInset; i++) mask[rows - 1 - i][x] = '#';
            }
            string[] result = new string[rows];
            for (int row = 0; row < rows; row++) result[row] = new string(mask[row]);
            return result;
        }

        public static string[] BuildConverterCells(int level, int columns, int rows)
        {
            if (level < 51 || columns < 5 || rows < 5 || level % 2 == 0) return null;
            int centerX = columns / 2;
            int centerY = rows / 2;
            return level <= 60
                ? new[] { $"{Mathf.Max(1, centerX - 2)},{centerY}", $"{Mathf.Min(columns - 2, centerX + 2)},{centerY}" }
                : new[] { $"{centerX},{Mathf.Max(1, centerY - 2)}", $"{centerX},{Mathf.Min(rows - 2, centerY + 2)}" };
        }

        public static string[] BuildLateCampaignObstaclePattern(int level, int columns, int rows)
        {
            if (level < 51 || columns < 2 || rows < 2) return null;
            List<string> cells = new List<string>();
            HashSet<string> unique = new HashSet<string>();
            void Add(int x, int y)
            {
                if (x < 0 || x >= columns || y < 0 || y >= rows) return;
                string value = $"{x},{y}";
                if (unique.Add(value)) cells.Add(value);
            }

            int centerX = columns / 2;
            int centerY = rows / 2;
            int variant = (level - 51) % 3;
            if (level <= 60)
            {
                // Aurora lanterns form readable constellations: cross, twin
                // diagonals or a pair of illuminated gates.
                if (variant == 0)
                {
                    for (int x = 1; x < columns - 1; x++) Add(x, centerY);
                    for (int y = 1; y < rows - 1; y++) Add(centerX, y);
                }
                else if (variant == 1)
                {
                    int diagonal = Mathf.Min(columns, rows);
                    for (int i = 1; i < diagonal - 1; i++)
                    {
                        Add(i, i);
                        Add(columns - 1 - i, i);
                    }
                }
                else
                {
                    for (int y = 1; y < rows - 1; y += 2)
                    {
                        Add(1, y);
                        Add(columns - 2, y);
                    }
                    for (int x = 2; x < columns - 2; x++) Add(x, centerY);
                }
            }
            else if (level <= 70)
            {
                // Summit ice arrives as a rim, a crystal diamond or layered
                // shelves, making the last ten boards recognisably different.
                if (variant == 0)
                {
                    for (int x = 0; x < columns; x += 2)
                    {
                        Add(x, 0);
                        Add(x, rows - 1);
                    }
                    for (int y = 1; y < rows - 1; y += 2)
                    {
                        Add(0, y);
                        Add(columns - 1, y);
                    }
                }
                else if (variant == 1)
                {
                    for (int y = 0; y < rows; y++)
                        for (int x = 0; x < columns; x++)
                        {
                            int distance = Mathf.Abs(x - centerX) + Mathf.Abs(y - centerY);
                            if (distance == 2 || distance == 3) Add(x, y);
                        }
                }
                else
                {
                    int[] bands = { 1, centerY, rows - 2 };
                    for (int band = 0; band < bands.Length; band++)
                        for (int x = band % 2; x < columns; x += 2)
                            Add(x, bands[band]);
                }
            }
            else if (level <= 80)
            {
                // Celestial gardens use airy gates and floating bridge lanes.
                for (int x = 1; x < columns - 1; x += 2) Add(x, centerY);
                for (int y = 1; y < rows - 1; y += 3)
                {
                    Add(1, y);
                    Add(columns - 2, y);
                }
            }
            else if (level <= 90)
            {
                // Ruby canyon creates alternating stone shelves, leaving
                // several readable routes through each board.
                int[] shelves = { 1, centerY, rows - 2 };
                for (int shelf = 0; shelf < shelves.Length; shelf++)
                    for (int x = shelf % 2; x < columns; x += 2) Add(x, shelves[shelf]);
            }
            else
            {
                // The sanctuary surrounds the centre with a ceremonial ring.
                for (int y = 1; y < rows - 1; y++)
                    for (int x = 1; x < columns - 1; x++)
                    {
                        int distance = Mathf.Abs(x - centerX) + Mathf.Abs(y - centerY);
                        if (distance == 3 || (level == 100 && distance == 2)) Add(x, y);
                    }
            }
            return cells.ToArray();
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
            definition.typeCount = Mathf.Clamp(definition.typeCount, 1, 9);
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

        private string CompanionReaction(string message)
        {
            string id = MapCharacterSelection.SelectedId;
            if (id == "pitbull")
            {
                if (message.Contains("CASCADA")) return "¡PITBULL AL ATAQUE! OTRA CASCADA";
                if (message.Contains("FUSIÓN")) return "¡PITBULL AL ATAQUE! FUSIÓN BRUTAL";
                if (message.Contains("CACHORRO")) return message.Replace("¡", "¡PITBULL: ");
                if (message.Contains("AYUDA")) return "¡PITBULL ENTRA EN ACCIÓN! LIMPIO UNA FILA";
                if (message.Contains("ESPECIAL")) return "¡PITBULL LO HA VISTO! ESPECIAL CONSEGUIDO";
                if (message.Contains("MOVIMIENTOS")) return "¡PITBULL TE AVISA! " + message.Trim('¡', '¡');
            }
            else if (id == "local-photo")
            {
                if (message.Contains("CASCADA")) return "¡TU MASCOTA SALTA! OTRA CASCADA";
                if (message.Contains("FUSIÓN")) return "¡TU MASCOTA CELEBRA LA FUSIÓN!";
                if (message.Contains("CACHORRO")) return "¡TU MASCOTA RESCATA!";
                if (message.Contains("AYUDA")) return "¡TU MASCOTA AYUDA! LIMPIO UNA FILA";
                if (message.Contains("ESPECIAL")) return "¡TU MASCOTA LO CELEBRA! ESPECIAL CONSEGUIDO";
            }
            else
            {
                if (message.Contains("CASCADA")) return "¡YORKSHIRE SALTA! OTRA CASCADA";
                if (message.Contains("FUSIÓN")) return "¡YORKSHIRE BRILLA! FUSIÓN INCREÍBLE";
                if (message.Contains("CACHORRO")) return "¡YORKSHIRE RESCATA!";
                if (message.Contains("AYUDA")) return "¡YORKSHIRE AYUDA! LIMPIO UNA FILA";
                if (message.Contains("ESPECIAL")) return "¡YORKSHIRE LADRA! ESPECIAL CONSEGUIDO";
            }
            return message;
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
            uiController?.ShowCompanionReaction(CompanionReaction(createdSpecial
                ? "¡ESPECIAL CONSEGUIDO!"
                : "¡CASCADA! SIGUE ASÍ"));
            if (companionCharge < CompanionChargeTarget || boardController == null) return;

            companionCharge = 0;
            PieceView target = boardController.GetRandomPiece();
            if (target == null) return;
            foreach (PieceView piece in boardController.GetRowPieces(target.gridY))
            {
                // This assistance runs after special activation was resolved. Preserve
                // prepared powers rather than removing them without their effect.
                if (piece != null && !piece.IsSpecial && !piecesToRemove.Contains(piece))
                    piecesToRemove.Add(piece);
            }
            uiController?.CelebrateCompanion();
            uiController?.UpdateCompanionCharge(companionCharge, CompanionChargeTarget);
            uiController?.ShowCompanionReaction(CompanionReaction("¡AYUDA LISTA! LIMPIO UNA FILA"), true);
            particleController?.PlayCompanionRow(
                target,
                boardController.Columns,
                boardController.ActivePieceSpacing);
            hapticController?.PulseMatch(8);
        }

        private void HandleChainCompleted(List<PieceView> chain)
        {
            if (!stateController.CanSelectPieces()) return;

            if (cascadeDepth >= 4 && !earnedSkillStar)
            {
                earnedSkillStar = true;
                RefreshSkillStarChallengeUI();
            }

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
            bool gardenReward = resolution != null && resolution.CreatedSpecial != null &&
                boardController.TryHarvestCompanionGarden(resolution.CreatedSpecial);
            if (gardenReward) companionCharge = Mathf.Min(CompanionChargeTarget, companionCharge + 1);
            TryActivateCompanionAssist(piecesToRemove, resolution != null && resolution.CreatedSpecial != null);
            if (gardenReward && companionCharge > 0)
                uiController?.ShowCompanionReaction(CompanionReaction("¡FLOR ACTIVADA! +1 AYUDA EXTRA"));
            if (resolution != null && resolution.ComboKind != SpecialComboKind.None)
                uiController?.ShowCompanionReaction(CompanionReaction("¡FUSIÓN INCREÍBLE!"));
            else if (cascadeDepth > 0)
                uiController?.ShowCompanionReaction(CompanionReaction("¡OTRA CASCADA!"));
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
            bool festivalBellReward = TryGrantFestivalBellTime(resolution) > 0f;
            var tideHits = !hasSpecialImpact && boardController!=null
                ? boardController.TryCollectCoastTide(chain,piecesToRemove) : null;
            var warmthHits = !hasSpecialImpact && boardController!=null
                ? boardController.TryCollectMountainWarmth(chain,piecesToRemove) : null;
            var prismHits = !hasSpecialImpact && boardController!=null
                ? boardController.TryCollectAuroraPrism(chain,piecesToRemove) : null;
            var crystalHits = !hasSpecialImpact && boardController!=null
                ? boardController.TryCollectSummitCrystal(resolution?.CreatedSpecial,piecesToRemove) : null;
            var sproutHits = !hasSpecialImpact && boardController!=null
                ? boardController.TryCollectCelestialSprout(resolution?.CreatedSpecial,piecesToRemove) : null;
            var geyserHits = hasSpecialImpact && boardController!=null
                ? boardController.TryCollectRubyGeyser(resolution.ActivatedSpecials,piecesToRemove) : null;
            var sealHits = !hasSpecialImpact && boardController!=null
                ? boardController.TryCollectSanctuarySeal(resolution?.CreatedSpecial,piecesToRemove) : null;
            int clearedObstacles = boardController != null
                ? boardController.DamageObstacles(piecesToRemove, hasSpecialImpact,
                    tideHits!=null && tideHits.Count>0 ? tideHits :
                    warmthHits!=null && warmthHits.Count>0 ? warmthHits :
                    prismHits!=null && prismHits.Count>0 ? prismHits :
                    crystalHits!=null && crystalHits.Count>0 ? crystalHits :
                    sproutHits!=null && sproutHits.Count>0 ? sproutHits :
                    geyserHits!=null && geyserHits.Count>0 ? geyserHits : sealHits)
                : 0;
            if(tideHits!=null && tideHits.Count>0)
                foreach(var cell in tideHits)
                    particleController?.PlayMatchBurst(boardController.GridToWorldPosition(cell.x,cell.y),
                        new Color(.25f,.85f,1f),JoinDog.App.AccessibilitySettings.ReducedMotion ? 1 : 3);
            if(warmthHits!=null && warmthHits.Count>0)
                foreach(var cell in warmthHits)
                    particleController?.PlayMatchBurst(boardController.GridToWorldPosition(cell.x,cell.y),
                        new Color(1f,.64f,.27f),JoinDog.App.AccessibilitySettings.ReducedMotion ? 1 : 3);
            obstaclesClearedThisTurn += clearedObstacles;
            if(sealHits!=null && sealHits.Count>0)
                foreach(var cell in sealHits)
                    particleController?.PlayMatchBurst(boardController.GridToWorldPosition(cell.x,cell.y),
                        new Color(1f,.85f,.36f),JoinDog.App.AccessibilitySettings.ReducedMotion ? 1 : 3);
            if(geyserHits!=null && geyserHits.Count>0)
                foreach(var cell in geyserHits)
                    particleController?.PlayMatchBurst(boardController.GridToWorldPosition(cell.x,cell.y),
                        new Color(1f,.42f,.3f),JoinDog.App.AccessibilitySettings.ReducedMotion ? 1 : 3);
            if(sproutHits!=null && sproutHits.Count>0)
                foreach(var cell in sproutHits)
                    particleController?.PlayMatchBurst(boardController.GridToWorldPosition(cell.x,cell.y),
                        new Color(.52f,1f,.69f),JoinDog.App.AccessibilitySettings.ReducedMotion ? 1 : 3);
            if(crystalHits!=null && crystalHits.Count>0)
                foreach(var cell in crystalHits)
                    particleController?.PlayMatchBurst(boardController.GridToWorldPosition(cell.x,cell.y),
                        new Color(.55f,.92f,1f),JoinDog.App.AccessibilitySettings.ReducedMotion ? 1 : 3);
            if(prismHits!=null && prismHits.Count>0)
            {
                foreach(var cell in prismHits)
                    particleController?.PlayMatchBurst(boardController.GridToWorldPosition(cell.x,cell.y),
                        new Color(.65f,.42f,1f),JoinDog.App.AccessibilitySettings.ReducedMotion ? 1 : 3);
            }
            bool shelterCollectedNow = false;
            if (!vineShelterThisTurn && obstaclesClearedThisTurn == 0 && !hasSpecialImpact &&
                boardController != null && boardController.TryCollectVineShelter(chain,piecesToRemove))
            {
                vineShelterThisTurn = true;
                shelterCollectedNow = true;
            }
            if (clearedObstacles > 0)
            {
                RefreshSecondaryHazardUI();
                if (CurrentLevelDefinition.objectiveType == LevelObjectiveType.RescuePuppies)
                {
                    uiController?.ShowCompanionReaction(CompanionReaction(clearedObstacles > 1
                        ? $"¡{clearedObstacles} CACHORROS LIBERADOS!"
                        : "¡CACHORRO LIBERADO!"));
                    uiController?.ShowComboBanner("¡RESCATE CONSEGUIDO!",
                        new Color(1f, 0.72f, 0.20f));
                }
            }
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
                int matchedCount = resolution != null ? resolution.OriginalMatchCount : chain.Count;
                bool focusedCreation = resolution != null && resolution.CreatedSpecial != null &&
                    (matchedCount == 4 || resolution.CreatedSpecialType == PieceSpecialType.ColorBurst ||
                     resolution.CreatedSpecialType == PieceSpecialType.MegaBurst);
                focusedCreation |= resolution != null && resolution.CreatedSpecialType == PieceSpecialType.BallBounce;
                bool focusedColorActivation = resolution != null && resolution.ComboKind == SpecialComboKind.None &&
                    resolution.SpecialsActivated > 0 && (ResolutionContainsSpecial(resolution, PieceSpecialType.ColorBurst) ||
                    ResolutionContainsSpecial(resolution, PieceSpecialType.MegaBurst) ||
                    ResolutionContainsSpecial(resolution, PieceSpecialType.BallBounce));
                if (focusedCreation)
                    uiController?.ShowCompanionReaction(GetCreatedSpecialTitle(resolution.CreatedSpecialType) +
                        (gardenReward ? " · +1 FLOR" : shelterCollectedNow ? " · REFUGIO" :
                         tideHits!=null && tideHits.Count>0 ? " · OLA" :
                         warmthHits!=null && warmthHits.Count>0 ? " · CALOR" :
                         prismHits!=null && prismHits.Count>0 ? " · PRISMA" :
                         crystalHits!=null && crystalHits.Count>0 ? " · CRISTAL" :
                         sproutHits!=null && sproutHits.Count>0 ? " · PODA" :
                         sealHits!=null && sealHits.Count>0 ? " · SELLO" : ""));
                else if (focusedColorActivation)
                    uiController?.ShowCompanionReaction(GetActivatedSpecialTitle(resolution) +
                        (festivalBellReward ? " · +2s CAMPANA" : geyserHits!=null && geyserHits.Count>0 ? " · GÉISER" : ""));
                else if (festivalBellReward)
                    uiController?.ShowCompanionReaction(CompanionReaction("¡CAMPANA FESTIVA! +2s"));
                else if (shelterCollectedNow)
                    uiController?.ShowCompanionReaction(CompanionReaction("¡HOJA REFUGIO! CRECIMIENTO EN PAUSA"));
                else if(tideHits!=null && tideHits.Count>0)
                    uiController?.ShowCompanionReaction(CompanionReaction("¡OLA! UNA CAPA MENOS DE ARENA EN LA FILA"));
                else if(warmthHits!=null && warmthHits.Count>0)
                    uiController?.ShowCompanionReaction(CompanionReaction("¡CALOR! UNA CAPA MENOS DE HIELO VECINO"));
                else if(prismHits!=null && prismHits.Count>0)
                    uiController?.ShowCompanionReaction(CompanionReaction("¡PRISMA! UNA CAPA MENOS EN SU COLUMNA"));
                else if(geyserHits!=null && geyserHits.Count>0)
                    uiController?.ShowCompanionReaction(CompanionReaction("¡GÉISER! ARENA DOS CASILLAS ARRIBA Y ABAJO"));
                if (!focusedCreation && (matchedCount >= 4 || cascadeDepth > 0))
                    uiController?.ShowMatchReward(centerPos);
                string celebration = FeedbackController.CelebrationTitle(matchedCount, cascadeDepth);
                if (!focusedCreation && !string.IsNullOrEmpty(celebration))
                    uiController?.ShowComboBanner(celebration, cascadeDepth > 0 ? new Color(.25f, .92f, 1f) :
                        matchedCount >= 6 ? new Color(1f, .32f, .78f) : new Color(1f, .78f, .15f));
                if (cascadeDepth == 0) particleController?.PlayCombinationAccent(centerPos, matchedCount);

                if (feedbackController != null)
                {
                    bool namedSpecialEvent = resolution != null &&
                        (resolution.MegaCombo || resolution.SpecialsActivated > 0 || resolution.CreatedSpecial != null);
                    Vector3 scorePosition = namedSpecialEvent ? centerPos + Vector3.down * 0.34f : centerPos;
                    feedbackController.SpawnFloatingText(scorePosition, $"+{pointsGained:N0}", Color.yellow, 34f);
                    if (resolution != null && resolution.ComboKind != SpecialComboKind.None)
                    {
                        feedbackController.SpawnFloatingText(
                            centerPos + Vector3.up * 0.42f,
                            GetSpecialComboTitle(resolution.ComboKind),
                            GetSpecialComboColor(resolution.ComboKind),
                            resolution.MegaCombo ? 50f : 43f);
                        feedbackController.SpawnFloatingText(
                            centerPos + Vector3.up * 0.16f,
                            GetSpecialComboHint(resolution.ComboKind),
                            new Color(1f, .94f, .72f), 22f);
                    }
                    else if (resolution != null && resolution.SpecialsActivated > 0 && !focusedColorActivation)
                        feedbackController.SpawnFloatingText(
                            centerPos + Vector3.up * 0.42f,
                            ResolutionContainsSpecial(resolution, PieceSpecialType.MegaBurst)
                                ? "¡SUPERNOVA!"
                                : GetActivatedSpecialTitle(resolution),
                            ResolutionContainsSpecial(resolution, PieceSpecialType.MegaBurst)
                                ? new Color(1f, 0.24f, 0.86f)
                                : new Color(1f, 0.55f, 0.08f),
                            ResolutionContainsSpecial(resolution, PieceSpecialType.MegaBurst) ? 48f : 40f);
                    else if (resolution != null && resolution.CreatedSpecial != null && !focusedCreation)
                        feedbackController.SpawnFloatingText(
                            resolution.CreatedSpecial.transform.position + Vector3.up * 0.42f,
                            GetCreatedSpecialTitle(resolution.CreatedSpecialType),
                            resolution.CreatedSpecialType == PieceSpecialType.MegaBurst
                                ? new Color(1f, 0.24f, 0.86f)
                                : new Color(1f, 0.82f, 0.12f),
                            resolution.CreatedSpecialType == PieceSpecialType.MegaBurst ? 48f : 38f);
                    if ((cascadeDepth == 0 && matchedCount >= 5) || namedSpecialEvent)
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
                    particleController.PlayMatchImpact(piecesToRemove, matchedCount, cascadeDepth,
                        boardController.ActivePieceSpacing, specialImpact);
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
                        particleController.PlayLineFormation(resolution.CreatedSpecial, piecesToRemove,
                            boardController.ActivePieceSpacing);
                        particleController.PlayColorFormation(resolution.CreatedSpecial, piecesToRemove,
                            boardController.ActivePieceSpacing);
                        particleController.PlayNovaFormation(resolution.CreatedSpecial,piecesToRemove,
                            boardController.ActivePieceSpacing);
                        particleController.PlayBounceFormation(resolution.CreatedSpecial,boardController.ActivePieceSpacing);
                    }

                    // Keep the burst readable without creating dozens of
                    // particles at once on small mobile/WebGL screens.
                    bool compactScreen = Screen.width <= 600 || Screen.height <= 900;
                    int burstCount = compactScreen
                        ? (specialImpact ? 4 : Mathf.Clamp(3 + piecesToRemove.Count, 5, 9))
                        : (specialImpact ? 7 : Mathf.Clamp(6 + piecesToRemove.Count, 9, 18));
                    int maxBurstLocations = specialImpact ? (compactScreen ? 7 : 12) :
                        (compactScreen ? 12 : 20);
                    if (cascadeDepth > 0 && !specialImpact)
                    {
                        burstCount = 3;
                        maxBurstLocations = 6;
                    }
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
                else if (specialAudio)
                {
                    if (!resolution.MegaCombo && resolution.ActivatedSpecials.Count > 0)
                        audioController.PlaySpecialSound(resolution.ActivatedSpecials[0].SpecialType);
                    else audioController.PlaySpecialSound(resolution.MegaCombo);
                }
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
                    resolution != null && !AccessibilitySettings.ReducedMotion &&
                    (ResolutionContainsSpecial(resolution, PieceSpecialType.ColorBurst) ||
                    ResolutionContainsSpecial(resolution, PieceSpecialType.MegaBurst)) && !resolution.MegaCombo ? 0.38f :
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
                if (special.SpecialType == PieceSpecialType.MegaBurst)
                {
                    particleController.PlaySpecialActivation(special,boardController.Columns,boardController.Rows,
                        boardController.ActivePieceSpacing);
                    var colorTargets = new List<PieceView>();
                    foreach(var target in resolution.PiecesToRemove)
                        if(target != null && target.type == special.type &&
                            target.gridX != special.gridX && target.gridY != special.gridY) colorTargets.Add(target);
                    particleController.PlayColorSweep(special.transform.position,colorTargets,boardController.ActivePieceSpacing);
                    continue;
                }
                special.PlaySpecialChargeAnimation(0.18f);
                if (++shown >= maximum) break;
            }
        }

        private IEnumerator PlaySpecialImpactAfterCharge(MatchResolution resolution, Vector3 centerPos, float delay)
        {
            int version = matchPresentationVersion;
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (version != matchPresentationVersion) yield break;
            if (particleController == null || resolution == null || boardController == null) yield break;

            // Individual activations already trace the real rows, columns and
            // diagonals. Avoid adding decorative lanes that imply extra damage.
            if(resolution.ComboKind==SpecialComboKind.DoubleArea)
                particleController.PlayDoubleAreaFootprints(resolution.ActivatedSpecials,boardController);
            bool individualRoutes = resolution.ComboKind == SpecialComboKind.DoubleArea ||
                resolution.ComboKind == SpecialComboKind.DoubleRow ||
                resolution.ComboKind == SpecialComboKind.DoubleColumn ||
                resolution.ComboKind == SpecialComboKind.CrossBlast ||
                resolution.ComboKind == SpecialComboKind.CombinedPowers;
            if (!individualRoutes && resolution.ComboKind != SpecialComboKind.None && resolution.ComboKind != SpecialComboKind.ColorSweep)
            {
                particleController.PlaySpecialCombo(
                    resolution.ComboKind,
                    centerPos,
                    boardController.Columns,
                    boardController.Rows,
                    boardController.ActivePieceSpacing,
                    resolution.ComboAnchor);
                if (resolution.MegaCombo) yield break;
            }

            int expandedAreasShown=0;
            foreach (PieceView special in resolution.ActivatedSpecials)
            {
                if (special == null) continue;
                if(resolution.ComboKind==SpecialComboKind.DoubleArea && special.SpecialType==PieceSpecialType.AreaBlast && expandedAreasShown++<2)
                    continue;
                if (special.SpecialType == PieceSpecialType.ColorBurst)
                {
                    particleController.PlayColorSweep(special.transform.position,
                        resolution.PiecesToRemove, boardController.ActivePieceSpacing);
                    continue;
                }
                if (special.SpecialType == PieceSpecialType.BallBounce &&
                    resolution.BallBounceDestinations.TryGetValue(special, out var destinations))
                {
                    particleController.PlayBallBounces(special.transform.position,
                        destinations, boardController.ActivePieceSpacing);
                    continue;
                }
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
            int version = matchPresentationVersion;
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (version != matchPresentationVersion) yield break;
            // Views are pooled: remove eligibility before recycling so a newly
            // spawned special cannot inherit the old view's bonus membership.
            foreach (PieceView piece in piecesToRemove) finalBonusSpecials.Remove(piece);
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
                // The resolving match displays the cascade label, avoiding duplicate restarts.
                GrantCascadeTimeBonus(cascades[cascades.Count / 2]);
                stateController.ChangeState(GameState.Playing);
                HandleMatch3Move(cascades);
                return;
            }

            if (victoryPending)
            {
                QueueNextFinalSpecial();
            }
            else if (!IsMoveLimitedLevel && gameTimer != null && gameTimer.RemainingTime <= 0f)
            {
                EndMatch(false);
            }
            else if (IsMoveLimitedLevel && movesRemaining <= 0)
            {
                EndMatch(false);
            }
            else
            {
                if (TrySpreadUnprotectedVines())
                {
                    RefreshSecondaryHazardUI();
                    uiController?.ShowComboBanner("¡LAS ENREDADERAS CRECEN!", new Color(0.42f, 0.94f, 0.28f));
                }
                stateController.ChangeState(GameState.Playing);
            }
        }

        private float TryGrantFestivalBellTime(MatchResolution resolution)
        {
            // Preserve a bell until its full benefit can be granted; no use in untimed/final bonus play.
            if(victoryPending || IsMoveLimitedLevel || gameTimer==null || !gameTimer.IsRunning ||
                gameTimer.RemainingTime > gameTimer.durationSeconds-2f || resolution==null ||
                !(resolution.MegaCombo || resolution.ColorBurstCombo || resolution.SpecialsActivated>0) ||
                boardController==null || !boardController.TryGetUnrungFestivalBell(resolution.PiecesToRemove,out var cell))
                return 0f;
            float granted=gameTimer.AddTime(2f);
            if(granted>0f) boardController.RingFestivalBell(cell);
            return granted;
        }

        private bool TrySpreadUnprotectedVines() => obstaclesClearedThisTurn == 0 && !vineShelterThisTurn &&
            boardController != null && boardController.TrySpreadVines();

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
            if (!finalBonusCaptured)
            {
                finalBonusCaptured = true;
                finalBonusSpecials.Clear();
                if (boardController != null)
                    foreach (PieceView special in boardController.GetSpecialPieces())
                        if (special != null) finalBonusSpecials.Add(special);
            }
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
            // Celebrate the specials present when the settled winning board
            // enters its bonus. Cascades still resolve normally, but specials
            // created during this celebration do not extend its queue forever.
            specials?.RemoveAll(piece => piece == null || !finalBonusSpecials.Contains(piece));
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
                PieceSpecialType.MegaBurst => "SUPERNOVA: COLOR + CRUZ",
                PieceSpecialType.BallBounce => "REBOTE: SALTOS + ÁREAS",
                PieceSpecialType.Whistle => "SILBATO MAGICO!",
                PieceSpecialType.Comet => "¡FRISBEE COMETA!",
                _ => "FICHA ESPECIAL!"
            };
        }

        private static string GetActivatedSpecialTitle(MatchResolution resolution)
        {
            if (ResolutionContainsSpecial(resolution, PieceSpecialType.MegaBurst)) return "SUPERNOVA: COLOR + CRUZ";
            if (ResolutionContainsSpecial(resolution, PieceSpecialType.ColorBurst)) return "BARRIDO DE COLOR!";
            if (ResolutionContainsSpecial(resolution, PieceSpecialType.BallBounce)) return "PELOTA REBOTE!";
            if (ResolutionContainsSpecial(resolution, PieceSpecialType.Whistle)) return "SILBATO MAGICO!";
            if (ResolutionContainsSpecial(resolution, PieceSpecialType.Comet)) return "¡DOBLE DIAGONAL!";
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

        private static string GetSpecialComboHint(SpecialComboKind kind)
        {
            return kind switch
            {
                SpecialComboKind.DoubleRow => "ACTIVA AMBOS RAYOS DE FILA",
                SpecialComboKind.DoubleColumn => "ACTIVA AMBOS RAYOS DE COLUMNA",
                SpecialComboKind.CrossBlast => "CRUZA FILA Y COLUMNA",
                SpecialComboKind.WideRow => "BARRIDO HORIZONTAL AMPLIADO",
                SpecialComboKind.WideColumn => "BARRIDO VERTICAL AMPLIADO",
                SpecialComboKind.DoubleArea => "DOS EXPLOSIONES EN CADENA",
                SpecialComboKind.ColorSweep => "EL COLOR ELEGIDO DESAPARECE",
                SpecialComboKind.BoardNova => "TODO EL TABLERO TIEMBLA",
                SpecialComboKind.CombinedPowers => "CADA ESPECIAL CONSERVA SU ALCANCE",
                _ => "DOS ESPECIALES UNIDOS"
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
            obstaclesClearedThisTurn = 0;
            vineShelterThisTurn = false;
            if (IsMoveLimitedLevel)
            {
                movesRemaining = Mathf.Max(0, movesRemaining - 1);
                uiController?.SetMoveMode(movesRemaining, CurrentLevelDefinition.moveLimit);
                uiController?.ShowCompanionReaction(CompanionReaction(movesRemaining <= 5
                    ? $"¡QUEDAN {movesRemaining} MOVIMIENTOS!"
                    : "¡BUEN MOVIMIENTO!"));
            }
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
                PieceType.Duck => new Color(1f, 0.85f, 0.15f),
                PieceType.Frisbee => new Color(.30f, .88f, 1f),
                PieceType.Penguin => new Color(.68f, .48f, 1f),
                PieceType.Rope => new Color(.16f, .88f, .86f),
                _ => new Color(1f, 0.85f, 0.2f)
            };
        }

        private void HandleTimerExpired()
        {
            if (IsMoveLimitedLevel) return;
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
            bool isFirstVictory = victory && highScore <= 0;

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
                    victory, finalScore, isNewRecord, stars, lives, currentLevel, earnedReward, isFirstVictory,
                    victory ? BuildStarResultTip(stars) : null);
            }
        }

        private string BuildStarResultTip(int stars)
        {
            string budget = IsMoveLimitedLevel ? "tus movimientos" : "tu tiempo";
            if (stars >= 3)
                return $"¡Tres estrellas! Conservaste al menos el 60 % de {budget}.";
            if (stars == 1)
                return $"Para dos estrellas, conserva al menos el 30 % de {budget}.";
            return $"Para tres, conserva el 60 % de {budget}. " +
                (usedBoosterThisMatch && !earnedSkillStar
                    ? "Con ayudas, consigue también una cascada de 4."
                    : "Sin ayudas, o con una cascada de 4 si las usas.");
        }

        private int CalculateStars()
        {
            if (IsMoveLimitedLevel)
            {
                float ratio = CurrentLevelDefinition.moveLimit <= 0 ? 0f :
                    (float)movesRemaining / CurrentLevelDefinition.moveLimit;
                if (ratio >= 0.60f && (!usedBoosterThisMatch || earnedSkillStar)) return 3;
                if (ratio >= 0.30f) return 2;
                return 1;
            }
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
            if (selectionController != null) selectionController.InteractionBlocked = visible;
            if (visible) selectionController?.CancelInteraction();
            gameTimer?.SetPaused(visible);
        }

        private void UseShuffleBooster()
        {
            if (shuffleBoosterCount <= 0 || stateController == null || !stateController.CanSelectPieces() || boardController == null) return;
            if (!ConsumeBooster(BoosterKind.Paw)) return;
            usedBoosterThisMatch = true;
            RefreshSkillStarChallengeUI();
            // The paw booster creates a completely fresh board.
            boardController.InitializeBoard();
            boardController.EnsureHasValidMoves();
            RefreshBoosterCounts();
            audioController?.PlayUISound();
            hapticController?.PulseSelection();
        }

        private void UseFoodBooster()
        {
            if (foodBoosterCount <= 0 || stateController == null || !stateController.CanSelectPieces() ||
                !CanGrantFoodBoosterTime) return;
            if (!ConsumeBooster(BoosterKind.Food)) return;
            usedBoosterThisMatch = true;
            RefreshSkillStarChallengeUI();
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
            RefreshSkillStarChallengeUI();
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

        private bool CanGrantFoodBoosterTime => !IsMoveLimitedLevel && gameTimer != null &&
            gameTimer.IsRunning && !gameTimer.IsPaused && gameTimer.RemainingTime > 0f &&
            gameTimer.durationSeconds - gameTimer.RemainingTime > .01f;

        private void RefreshFoodBoosterAvailability()
        {
            uiController?.SetFoodBoosterAvailability(foodBoosterCount > 0 && CanGrantFoodBoosterTime,
                IsMoveLimitedLevel);
        }

        private void RefreshBoosterCounts()
        {
            PlayerProgressService progress = AppServices.Instance != null ? AppServices.Instance.Progress : null;
            shuffleBoosterCount = levelPawBoosters + (progress != null ? progress.GetBoosterCount(BoosterKind.Paw) : 0);
            boneBoosterCount = levelBoneBoosters + (progress != null ? progress.GetBoosterCount(BoosterKind.Bone) : 0);
            foodBoosterCount = levelFoodBoosters + (progress != null ? progress.GetBoosterCount(BoosterKind.Food) : 0);
            uiController?.SetBoosterAvailability(shuffleBoosterCount > 0, boneBoosterCount > 0, foodBoosterCount > 0);
            uiController?.SetBoosterCounts(shuffleBoosterCount, boneBoosterCount, foodBoosterCount);
            RefreshFoodBoosterAvailability();
        }

        private void RefreshSkillStarChallengeUI()
        {
            uiController?.SetSkillStarChallenge(currentLevel >= 21, usedBoosterThisMatch, earnedSkillStar);
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

            audioController.CycleSfxVolume();
            UpdateSettingsUI();
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
            UpdateSettingsUI();
        }

        private void HandleMusicToggleRequested()
        {
            if (audioController == null) return;
            audioController.CycleMusicVolume();
            audioController.PlayUISound();
            UpdateSettingsUI();
        }

        private void HandleReducedMotionToggleRequested()
        {
            AccessibilitySettings.ReducedMotion = !AccessibilitySettings.ReducedMotion;
            audioController?.PlayUISound();
            UpdateSettingsUI();
        }

        private void HandleObstacleContrastToggleRequested()
        {
            AccessibilitySettings.HighContrastObstacles = !AccessibilitySettings.HighContrastObstacles;
            boardController?.RefreshObstacleContrast();
            audioController?.PlayUISound();
            UpdateSettingsUI();
        }

        private void UpdateSettingsUI()
        {
            uiController?.UpdateSettingsState(
                audioController != null ? audioController.SfxVolume : 0f,
                audioController != null ? audioController.MusicVolume : 0f,
                hapticController == null || hapticController.HapticsEnabled,
                AccessibilitySettings.ReducedMotion,
                AccessibilitySettings.HighContrastObstacles);
        }

        private void HandleSettingsVisibilityChanged(bool visible)
        {
            if (selectionController != null) selectionController.InteractionBlocked = visible;
            if (visible) selectionController?.CancelInteraction();
            gameTimer?.SetPaused(visible);
            if (visible)
            {
                audioController?.PlayUISound();
                UpdateSettingsUI();
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
