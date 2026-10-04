using System.Collections;
using DogCrush.Board;
using DogCrush.Core;
using DogCrush.Gameplay;
using DogCrush.InputSystem;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DogCrush.Tests.PlayMode
{
    public class SwapExperienceTests
    {
        private GameObject root;
        private BoardConfig config;
        private BoardController board;
        private ChainSelectionController selection;
        private ChainInputHandler input;
        private GameStateController state;
        private int completed;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            root = new GameObject("SwapExperienceFixture");
            state = root.AddComponent<GameStateController>();
            board = root.AddComponent<BoardController>();
            config = ScriptableObject.CreateInstance<BoardConfig>();
            config.columns = 5;
            config.rows = 5;
            board.config = config;
            board.spawner = root.AddComponent<PieceSpawner>();
            var prefab = new GameObject("TestPiecePrefab");
            prefab.transform.SetParent(root.transform);
            prefab.transform.position = Vector3.one * 100f;
            board.spawner.piecePrefab = prefab.AddComponent<PieceView>();
            board.InitializeBoard();
            foreach (var piece in board.Grid)
            {
                piece.SetSpecial(PieceSpecialType.None);
                piece.type = (PieceType)((piece.gridX + piece.gridY) % 5);
            }
            // One deliberate vertical exchange makes a horizontal triplet.
            board.GetPieceAt(0, 0).type = PieceType.Dog;
            board.GetPieceAt(1, 0).type = PieceType.Dog;
            board.GetPieceAt(2, 0).type = PieceType.Ball;
            board.GetPieceAt(2, 1).type = PieceType.Dog;
            var controls = new GameObject("Controls");
            controls.transform.SetParent(root.transform);
            controls.SetActive(false);
            input = controls.AddComponent<ChainInputHandler>();
            input.enabled = false; // Tests inject actual pointer events without hardware input.
            selection = controls.AddComponent<ChainSelectionController>();
            selection.boardController = board;
            selection.stateController = state;
            selection.inputHandler = input;
            completed = 0;
            selection.OnMoveCompleted += _ => completed++;
            state.ChangeState(GameState.Playing);
            controls.SetActive(true);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            Object.Destroy(root);
            Object.Destroy(config);
            yield return null;
        }

        private void Tap(int x, int y)
        {
            Vector2 position = board.GridToWorldPosition(x, y);
            input.OnPointerDownEvent?.Invoke(position);
            input.OnPointerUpEvent?.Invoke(position);
        }

        [UnityTest]
        public IEnumerator SpecialSelection_PreviewsDirectRangeWithoutSpendingMoveAndClearsOnCancel()
        {
            var special=board.GetPieceAt(2,2);
            special.SetSpecial(PieceSpecialType.RowBlast);
            var view=board.GetComponent<AdaptiveBoardView>();
            var cell=view.transform.Find("[AdaptiveBoardVisual]/Cell_0_2").GetComponent<SpriteRenderer>();
            var other=view.transform.Find("[AdaptiveBoardVisual]/Cell_0_0").GetComponent<SpriteRenderer>();
            Color original=cell.color, untouched=other.color;
            var randomBefore=Random.state;
            Tap(2,2);
            Assert.That(cell.color,Is.Not.EqualTo(original));
            Assert.That(other.color,Is.EqualTo(untouched));
            Assert.That(completed,Is.Zero);
            Assert.That(board.GetPieceAt(2,2),Is.SameAs(special));
            Assert.That(Random.state,Is.EqualTo(randomBefore));
            selection.CancelInteraction();
            Assert.That(cell.color,Is.EqualTo(original));
            special.SetSpecial(PieceSpecialType.BallBounce);
            Assert.That(board.GetDirectSpecialPreview(special),Is.Empty);
            special.SetSpecial(PieceSpecialType.Comet);
            Assert.That(board.GetDirectSpecialPreview(special).Count,Is.EqualTo(9));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TwoTaps_ResolveExactlyOneValidMoveAndClearSelection()
        {
            var origin = board.GetPieceAt(2, 1);
            Tap(2, 1);
            Assert.That(origin.IsSelected, Is.True);
            Assert.That(selection.AttentionTarget, Is.SameAs(origin));
            Tap(2, 0);
            yield return new WaitForSeconds(.2f);
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(board.GetPieceAt(2, 0), Is.SameAs(origin));
            Assert.That(origin.IsSelected, Is.False);
            Assert.That(selection.HasPendingSwap, Is.False);
            Assert.That(selection.AttentionTarget, Is.Null);
        }

        [UnityTest]
        public IEnumerator InvalidTapSwap_RestoresBoardWithoutSpendingAMove()
        {
            var first = board.GetPieceAt(4, 4);
            var second = board.GetPieceAt(4, 3);
            Tap(4, 4);
            Tap(4, 3);
            yield return new WaitForSeconds(.2f);
            Assert.That(board.GetPieceAt(4, 4), Is.SameAs(first));
            Assert.That(board.GetPieceAt(4, 3), Is.SameAs(second));
            Assert.That(completed, Is.Zero);
            Assert.That(first.IsSelected, Is.False);
        }

        [UnityTest]
        public IEnumerator SameTap_CancelsAndDistantTapChangesSelection()
        {
            var first = board.GetPieceAt(0, 0);
            Tap(0, 0);
            Tap(0, 0);
            Assert.That(first.IsSelected, Is.False);
            Assert.That(selection.HasPendingSwap, Is.False);
            Tap(0, 0);
            Tap(4, 4);
            Assert.That(first.IsSelected, Is.False);
            Assert.That(board.GetPieceAt(4, 4).IsSelected, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MenuBlock_CancelsSelectionAndIgnoresBoardTouches()
        {
            Tap(2, 1);
            selection.InteractionBlocked = true;
            yield return null;
            Tap(2, 0);
            Assert.That(selection.HasPendingSwap, Is.False);
            Assert.That(board.GetPieceAt(2, 1).IsSelected, Is.False);
            Assert.That(completed, Is.Zero);
        }

        [UnityTest]
        public IEnumerator Restart_CancelsDeferredMoveCallback()
        {
            Tap(2, 1);
            Tap(2, 0);
            selection.CancelInteraction(true);
            yield return new WaitForSeconds(.2f);
            Assert.That(completed, Is.Zero);
            Assert.That(selection.HasPendingSwap, Is.False);
        }

        [UnityTest]
        public IEnumerator Drag_StillWorksWithNoIntermediatePointerFrame()
        {
            input.OnPointerDownEvent?.Invoke(board.GridToWorldPosition(2, 1));
            input.OnPointerUpEvent?.Invoke(board.GridToWorldPosition(2, 0));
            yield return new WaitForSeconds(.2f);
            Assert.That(completed, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Hints_ReturnPlayableSwapsAndPreserveGridCoordinates()
        {
            var snapshot = (PieceView[,])board.Grid.Clone();
            for (int seed = 0; seed < 30; seed++)
            {
                Random.InitState(seed);
                Assert.That(board.TryFindHintMoveForObjective(PieceType.Dog, true, out var a, out var b), Is.True);
                foreach (var piece in board.Grid)
                    Assert.That(snapshot[piece.gridX, piece.gridY], Is.SameAs(piece));
                int ax = a.gridX, ay = a.gridY, bx = b.gridX, by = b.gridY;
                Assert.That(board.TrySwapAndFindMatches(a, b, out _), Is.True);
                board.Grid[ax, ay] = a;
                board.Grid[bx, by] = b;
                a.SetGridPosition(ax, ay);
                b.SetGridPosition(bx, by);
            }
            yield return null;
        }
    }
}
