using System.Collections;
using DogCrush.Board;
using DogCrush.Core;
using DogCrush.Gameplay;
using DogCrush.InputSystem;
using DogCrush.Presentation;
using DogCrush.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DogCrush.Tests.PlayMode
{
    public class BoardPlayModeTests
    {
        private static IEnumerator LoadGameplayScene()
        {
            // PlayMode tests must not inherit the developer's campaign save.
            PlayerPrefs.SetInt("DogCrush_UnlockedLevel", 1);
            SceneManager.LoadScene("Gameplay", LoadSceneMode.Single);
            yield return null;
            yield return null;
            Object.FindAnyObjectByType<GameBootstrap>()?.StartNewMatch();
            // Every level now presents a short, timer-safe objective card.
            yield return new WaitForSecondsRealtime(1.30f);
        }

        private static int CountPlayableCells(BoardController board)
        {
            int count = 0;
            for (int x = 0; x < board.Columns; x++)
                for (int y = 0; y < board.Rows; y++)
                    if (board.IsPlayableCell(x, y)) count++;
            return count;
        }

        [UnityTest]
        public IEnumerator GameplayScene_FillsConfiguredBoardWithInteractivePieces()
        {
            yield return LoadGameplayScene();

            BoardController board = Object.FindAnyObjectByType<BoardController>();
            Assert.That(board, Is.Not.Null, "Gameplay scene must contain a BoardController.");
            Assert.That(board.Grid, Is.Not.Null, "BoardController must initialize its grid.");
            Assert.That(board.Columns, Is.GreaterThanOrEqualTo(7));
            Assert.That(board.Rows, Is.GreaterThanOrEqualTo(8));
            Assert.That(board.HasAnyValidMove(), Is.True,
                "The generated board must contain an orthogonal three-piece move.");

            AdaptiveBoardView adaptiveView = board.GetComponent<AdaptiveBoardView>();
            Assert.That(adaptiveView, Is.Not.Null,
                "The board must use the adaptive visual presenter.");
            Assert.That(adaptiveView.VisualSize.x, Is.GreaterThan(0f));
            Assert.That(adaptiveView.VisualSize.y, Is.GreaterThan(0f));
            Assert.That(GameObject.Find("BoardFrame"), Is.Null,
                "The rigid legacy board image must not remain active.");

            GameObject topHud = GameObject.Find("TopHud_RT");
            GameObject bottomHud = GameObject.Find("BottomHud_RT");
            Assert.That(topHud, Is.Not.Null, "The adaptive top HUD must be generated.");
            Assert.That(bottomHud, Is.Not.Null, "The adaptive bottom HUD must be generated.");
            Assert.That(GameObject.Find("TopBarOuter_RT"), Is.Null,
                "The fixed top-panel implementation must no longer be active.");
            Assert.That(GameObject.Find("BottomPill_RT"), Is.Null,
                "The fixed bottom-panel implementation must no longer be active.");

            Assert.That(GameObject.Find("ScoreLabel_RT"), Is.Not.Null);
            Assert.That(GameObject.Find("ScoreText_RT"), Is.Not.Null);
            Assert.That(GameObject.Find("LivesText_RT"), Is.Not.Null);
            Assert.That(GameObject.Find("TimerBarFill_RT"), Is.Not.Null);

            int activePieces = 0;
            PieceView[] pieces = Object.FindObjectsByType<PieceView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (PieceView piece in pieces)
            {
                if (piece != null && piece.gameObject.activeInHierarchy)
                {
                    activePieces++;
                    Assert.That(piece.GetComponent<Collider2D>(), Is.Not.Null,
                        "Every board piece must remain interactive.");
                    SpriteRenderer renderer = piece.GetComponent<SpriteRenderer>();
                    Assert.That(renderer, Is.Not.Null);
                    Assert.That(renderer.sprite, Is.Not.Null, "Every board piece must have a sprite.");
                    Assert.That(renderer.sprite.bounds.size.x, Is.GreaterThan(0.5f),
                        "Piece sprite geometry must be large enough to be visible on the board.");
                }
            }

            Assert.That(activePieces, Is.EqualTo(CountPlayableCells(board)),
                "The initial board must fill every playable cell.");
        }

        [UnityTest]
        public IEnumerator RestartingMatch_RecyclesPreviousPieces()
        {
            yield return LoadGameplayScene();

            GameBootstrap bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            Assert.That(bootstrap, Is.Not.Null);

            bootstrap.RestartGame();
            yield return null;
            yield return null;

            PieceView[] pieces = Object.FindObjectsByType<PieceView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            int activePieces = 0;
            foreach (PieceView piece in pieces)
            {
                if (piece != null && piece.gameObject.activeInHierarchy)
                {
                    activePieces++;
                }
            }

            Assert.That(activePieces, Is.EqualTo(CountPlayableCells(
                Object.FindAnyObjectByType<BoardController>())),
                "Restarting a match must leave exactly one active set of pieces.");
        }

        [UnityTest]
        public IEnumerator SettingsPanel_ControlsSoundHapticsAndPausesTimer()
        {
            PlayerPrefs.DeleteKey("DogCrush_SfxVolume");
            PlayerPrefs.DeleteKey("DogCrush_HapticsEnabled");
            yield return LoadGameplayScene();

            GameplayUIController ui = Object.FindAnyObjectByType<GameplayUIController>();
            AudioPlaceholderController audio = Object.FindAnyObjectByType<AudioPlaceholderController>();
            HapticFeedbackController haptics = Object.FindAnyObjectByType<HapticFeedbackController>();
            GameTimer timer = Object.FindAnyObjectByType<GameTimer>();

            Assert.That(ui, Is.Not.Null);
            Assert.That(audio, Is.Not.Null);
            Assert.That(haptics, Is.Not.Null);
            Assert.That(timer, Is.Not.Null);
            Assert.That(ui.settingsButton, Is.Not.Null);
            Assert.That(ui.settingsPanel, Is.Not.Null);
            Assert.That(ui.settingsPanel.activeSelf, Is.False);

            ui.settingsButton.onClick.Invoke();
            yield return null;
            Assert.That(ui.settingsPanel.activeSelf, Is.True);
            Assert.That(timer.IsPaused, Is.True);

            ui.soundToggleButton.onClick.Invoke();
            yield return null;
            Assert.That(audio.SfxVolume, Is.EqualTo(0.6f).Within(0.001f));
            StringAssert.Contains("60%", ui.soundToggleText.text);

            ui.hapticsToggleButton.onClick.Invoke();
            yield return null;
            Assert.That(haptics.HapticsEnabled, Is.False);
            StringAssert.Contains("NO", ui.hapticsToggleText.text);

            ui.settingsCloseButton.onClick.Invoke();
            yield return null;
            Assert.That(ui.settingsPanel.activeSelf, Is.False);
            Assert.That(timer.IsPaused, Is.False);

            PlayerPrefs.DeleteKey("DogCrush_SfxVolume");
            PlayerPrefs.DeleteKey("DogCrush_HapticsEnabled");
        }

        [UnityTest]
        public IEnumerator ChangingLevelDimensions_RebuildsAdaptiveBoard()
        {
            yield return LoadGameplayScene();

            BoardController board = Object.FindAnyObjectByType<BoardController>();
            Assert.That(board, Is.Not.Null);

            BoardConfig originalConfig = board.config;
            BoardConfig levelConfig = Object.Instantiate(originalConfig);
            levelConfig.columns = 7;
            levelConfig.rows = 9;

            board.config = levelConfig;
            board.InitializeBoard();
            yield return null;

            Assert.That(board.Columns, Is.EqualTo(7));
            Assert.That(board.Rows, Is.EqualTo(9));
            Assert.That(board.Grid.GetLength(0), Is.EqualTo(7));
            Assert.That(board.Grid.GetLength(1), Is.EqualTo(9));

            AdaptiveBoardView adaptiveView = board.GetComponent<AdaptiveBoardView>();
            Assert.That(adaptiveView, Is.Not.Null);
            Assert.That(adaptiveView.VisualSize.y, Is.GreaterThan(adaptiveView.VisualSize.x),
                "A 7x9 level must produce a naturally taller board without stretching its cells.");

            int activePieces = 0;
            PieceView[] pieces = Object.FindObjectsByType<PieceView>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            foreach (PieceView piece in pieces)
            {
                if (piece.gameObject.activeInHierarchy) activePieces++;
            }
            Assert.That(activePieces, Is.EqualTo(63));

            board.config = originalConfig;
            Object.Destroy(levelConfig);
        }

        [UnityTest]
        public IEnumerator DraggingDiagonally_DoesNotExtendSelection()
        {
            yield return LoadGameplayScene();

            BoardController board = Object.FindAnyObjectByType<BoardController>();
            ChainSelectionController selection = Object.FindAnyObjectByType<ChainSelectionController>();
            ChainInputHandler input = Object.FindAnyObjectByType<ChainInputHandler>();
            Assert.That(board, Is.Not.Null);
            Assert.That(selection, Is.Not.Null);
            Assert.That(input, Is.Not.Null);

            PieceView first = board.GetPieceAt(0, 0);
            PieceView diagonal = board.GetPieceAt(1, 1);
            diagonal.Initialize(
                first.type,
                1,
                1,
                board.spawner.GetSpriteForType(first.type),
                board.spawner.GetColorForType(first.type));

            Physics2D.SyncTransforms();
            input.OnPointerDownEvent?.Invoke(first.transform.position);
            yield return null;
            input.OnPointerDragEvent?.Invoke(diagonal.transform.position);
            yield return null;

            Assert.That(selection.SelectedChain.Count, Is.EqualTo(0),
                "Swap mode must ignore a diagonal destination.");

            input.OnPointerUpEvent?.Invoke(Vector2.zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DraggingAdjacentHintMove_PreviewsAndCompletesSwap()
        {
            yield return LoadGameplayScene();

            BoardController board = Object.FindAnyObjectByType<BoardController>();
            ChainInputHandler input = Object.FindAnyObjectByType<ChainInputHandler>();
            ScoreController score = Object.FindAnyObjectByType<ScoreController>();
            BoardGravityController gravity = Object.FindAnyObjectByType<BoardGravityController>();

            Assert.That(board, Is.Not.Null);
            Assert.That(input, Is.Not.Null);
            Assert.That(score, Is.Not.Null);
            Assert.That(gravity, Is.Not.Null);
            Assert.That(board.TryFindHintMove(out PieceView first, out PieceView second), Is.True);
            Vector3 firstStart = first.transform.position;
            Vector3 secondStart = second.transform.position;

            Physics2D.SyncTransforms();
            input.OnPointerDownEvent?.Invoke(first.transform.position);
            yield return null;
            input.OnPointerDragEvent?.Invoke(second.transform.position);
            yield return new WaitForSeconds(0.18f);

            Assert.That(Vector3.Distance(first.transform.position, secondStart), Is.LessThan(0.02f));
            Assert.That(Vector3.Distance(second.transform.position, firstStart), Is.LessThan(0.02f));

            input.OnPointerUpEvent?.Invoke(secondStart);
            float timeout = Time.realtimeSinceStartup + 4f;
            while ((score.CurrentScore == 0 || gravity.IsResolving) && Time.realtimeSinceStartup < timeout)
                yield return null;
            Assert.That(score.CurrentScore, Is.GreaterThan(0));
            Assert.That(gravity.IsResolving, Is.False);
        }

        [UnityTest]
        public IEnumerator DraggingThreeMatchingPieces_ScoresFallsAndRefills()
        {
            yield return LoadGameplayScene();

            BoardController board = Object.FindAnyObjectByType<BoardController>();
            ChainSelectionController selection = Object.FindAnyObjectByType<ChainSelectionController>();
            ChainInputHandler input = Object.FindAnyObjectByType<ChainInputHandler>();
            ScoreController score = Object.FindAnyObjectByType<ScoreController>();
            BoardGravityController gravity = Object.FindAnyObjectByType<BoardGravityController>();

            Assert.That(board, Is.Not.Null);
            Assert.That(selection, Is.Not.Null);
            Assert.That(input, Is.Not.Null);
            Assert.That(score, Is.Not.Null);
            Assert.That(gravity, Is.Not.Null);
            // Keep one regression test for the optional legacy chain input;
            // the player-facing mode is covered by the adjacent-swap test above.
            selection.adjacentSwapMode = false;

            PieceView first = null;
            PieceView middle = null;
            PieceView last = null;

            for (int x = 0; x < board.Columns && middle == null; x++)
            {
                for (int y = 0; y < board.Rows && middle == null; y++)
                {
                    PieceView candidate = board.GetPieceAt(x, y);
                    if (candidate == null) continue;

                    PieceView[] matchingNeighbors = new PieceView[8];
                    int neighborCount = 0;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            if (!BoardController.AreAdjacent(x, y, x + dx, y + dy)) continue;
                            PieceView neighbor = board.GetPieceAt(x + dx, y + dy);
                            if (neighbor != null && neighbor.type == candidate.type)
                            {
                                matchingNeighbors[neighborCount++] = neighbor;
                            }
                        }
                    }

                    if (neighborCount >= 2)
                    {
                        first = matchingNeighbors[0];
                        middle = candidate;
                        last = matchingNeighbors[1];
                    }
                }
            }

            Assert.That(middle, Is.Not.Null,
                "The initialized board must expose at least one valid three-piece chain.");

            Physics2D.SyncTransforms();
            foreach (PieceView piece in new[] { first, middle, last })
            {
                Collider2D hit = Physics2D.OverlapPoint(piece.transform.position);
                Assert.That(hit, Is.Not.Null,
                    "Each normalized piece must keep a finger-sized collider at its visual center.");
                Assert.That(hit.GetComponent<PieceView>(), Is.EqualTo(piece));
            }

            input.OnPointerDownEvent?.Invoke(first.transform.position);
            yield return null;
            input.OnPointerDragEvent?.Invoke(middle.transform.position);
            yield return null;
            input.OnPointerDragEvent?.Invoke(last.transform.position);
            yield return null;

            Assert.That(selection.SelectedChain.Count, Is.EqualTo(3),
                "The live selection must contain the three dragged pieces.");

            input.OnPointerUpEvent?.Invoke(Vector2.zero);

            float timeoutAt = Time.realtimeSinceStartup + 4f;
            while ((gravity.IsResolving || score.CurrentScore == 0) &&
                   Time.realtimeSinceStartup < timeoutAt)
            {
                yield return null;
            }

            Assert.That(score.CurrentScore, Is.GreaterThan(0),
                "Completing a valid chain must award points.");
            Assert.That(gravity.IsResolving, Is.False,
                "Removal, fall and refill must finish.");

            int activePieces = 0;
            for (int x = 0; x < board.Columns; x++)
            {
                for (int y = 0; y < board.Rows; y++)
                {
                    PieceView piece = board.GetPieceAt(x, y);
                    Assert.That(piece, Is.Not.Null,
                        $"Grid position ({x}, {y}) must be refilled.");
                    if (piece.gameObject.activeInHierarchy) activePieces++;
                }
            }

            Assert.That(activePieces, Is.EqualTo(CountPlayableCells(board)),
                "A completed move must refill every playable board cell.");
        }
    }
}
