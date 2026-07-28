using System.Collections.Generic;
using DogCrush.Board;
using DogCrush.Core;
using DogCrush.InputSystem;
using UnityEngine;

namespace DogCrush.Gameplay
{
    public class ChainSelectionController : MonoBehaviour
    {
        public BoardController boardController;
        public ChainInputHandler inputHandler;
        public Presentation.ChainLineView lineView;
        public GameStateController stateController;

        public System.Action<List<PieceView>> OnChainCompleted;
        public System.Action OnChainCancelled;
        public System.Action<int, PieceType> OnChainUpdated;
        public System.Action<List<PieceView>> OnMoveCompleted;
        [Header("Input mode")]
        public bool adjacentSwapMode = true;

        private readonly List<PieceView> selectedChain = new List<PieceView>();
        private PieceView swapOrigin;
        private PieceView swapTarget;
        private bool swapAnimating;
        public List<PieceView> SelectedChain => selectedChain;

        public bool IsSelecting { get; private set; }
        public PieceType ActiveChainType { get; private set; } = PieceType.None;

        private void OnEnable()
        {
            if (inputHandler != null)
            {
                inputHandler.OnPointerDownEvent += HandlePointerDown;
                inputHandler.OnPointerDragEvent += HandlePointerDrag;
                inputHandler.OnPointerUpEvent += HandlePointerUp;
            }
        }

        private void Awake()
        {
            if (stateController == null)
                stateController = FindAnyObjectByType<GameStateController>();
        }

        private void OnDisable()
        {
            if (inputHandler != null)
            {
                inputHandler.OnPointerDownEvent -= HandlePointerDown;
                inputHandler.OnPointerDragEvent -= HandlePointerDrag;
                inputHandler.OnPointerUpEvent -= HandlePointerUp;
            }
        }

        private void HandlePointerDown(Vector2 worldPos)
        {
            if (stateController != null && !stateController.CanSelectPieces()) return;
            PieceView piece = GetPieceAtPosition(worldPos);
            if (!IsCurrentBoardPiece(piece)) return;

            if (adjacentSwapMode)
            {
                if (swapAnimating) return;
                swapOrigin = piece;
                swapTarget = null;
                return;
            }

            IsSelecting = true;
            selectedChain.Clear();
            ActiveChainType = piece.type;
            if (lineView != null)
            {
                lineView.SetChainType(ActiveChainType);
            }

            AddPieceToChain(piece);
        }

        private void HandlePointerDrag(Vector2 worldPos)
        {
            if (adjacentSwapMode)
            {
                if (swapOrigin == null) return;
                PieceView candidate = GetPieceAtPosition(worldPos);
                if (candidate != null && IsCurrentBoardPiece(candidate) && BoardController.AreAdjacent(swapOrigin.gridX, swapOrigin.gridY, candidate.gridX, candidate.gridY))
                {
                    if (candidate != swapTarget)
                    {
                        if (swapTarget != null) boardController?.RestorePreviewSwap(swapOrigin, swapTarget);
                        swapTarget = candidate;
                        boardController?.PreviewSwap(swapOrigin, swapTarget);
                    }
                }
                else if (swapTarget != null)
                {
                    boardController?.RestorePreviewSwap(swapOrigin, swapTarget);
                    swapTarget = null;
                }
                return;
            }
            if (!IsSelecting) return;
            if (stateController != null && !stateController.CanSelectPieces())
            {
                CancelCurrentSelection();
                return;
            }

            PieceView piece = GetPieceAtPosition(worldPos);
            if (piece == null)
            {
                UpdateLineView(worldPos);
                return;
            }

            // Ignore pooled/hidden views that may still receive a collider hit
            // for one frame while the board is being rebuilt.
            if (!IsCurrentBoardPiece(piece))
            {
                UpdateLineView(worldPos);
                return;
            }

            if (selectedChain.Count == 0)
            {
                AddPieceToChain(piece);
                UpdateLineView(worldPos);
                return;
            }

            // Check if player dragged back to the second-to-last piece (Backtrack / Undo)
            if (selectedChain.Count >= 2 && piece == selectedChain[selectedChain.Count - 2])
            {
                RemoveLastPieceFromChain();
                UpdateLineView(worldPos);
                return;
            }

            // Check validity: must match active type, must be adjacent to last, must not already be in chain
            PieceView lastPiece = selectedChain[selectedChain.Count - 1];
            if (piece.type == ActiveChainType &&
                !selectedChain.Contains(piece) &&
                BoardController.AreAdjacent(lastPiece.gridX, lastPiece.gridY, piece.gridX, piece.gridY))
            {
                AddPieceToChain(piece);
            }
            else if (piece.type == ActiveChainType && !selectedChain.Contains(piece))
            {
                // A quick finger movement can skip over one or more cells
                // between input frames. Fill a straight orthogonal path so
                // fast swipes still select the intended chain.
                AddSkippedOrthogonalPath(lastPiece, piece);
            }

            UpdateLineView(worldPos);
        }

        private void HandlePointerUp()
        {
            if (adjacentSwapMode)
            {
                if (swapOrigin != null && swapTarget != null && boardController != null)
                {
                    if (boardController.TrySwapAndFindMatches(swapOrigin, swapTarget, out List<PieceView> matches))
                    {
                        swapAnimating = true;
                        StartCoroutine(CompleteSwapAfterAnimation(matches));
                    }
                    else
                    {
                        boardController.RestorePreviewSwap(swapOrigin, swapTarget);
                    }
                }
                swapOrigin = null;
                swapTarget = null;
                return;
            }
            if (!IsSelecting) return;
            IsSelecting = false;

            if (stateController != null && !stateController.CanSelectPieces())
            {
                CancelCurrentSelection();
                return;
            }

            int minLength = boardController != null && boardController.config != null ? boardController.config.minChainLength : 3;

            if (selectedChain.Count >= minLength)
            {
                List<PieceView> completed = new List<PieceView>(selectedChain);
                ClearSelectionVisuals();
                OnChainCompleted?.Invoke(completed);
            }
            else
            {
                ClearSelectionVisuals();
                OnChainCancelled?.Invoke();
            }

            selectedChain.Clear();
            ActiveChainType = PieceType.None;
        }

        private System.Collections.IEnumerator CompleteSwapAfterAnimation(List<PieceView> matches)
        {
            // Match resolution starts after both pieces have visibly crossed
            // into their new cells, avoiding an instant teleport effect.
            yield return new WaitForSeconds(0.28f);
            swapAnimating = false;
            OnMoveCompleted?.Invoke(matches);
        }

        private void AddPieceToChain(PieceView piece)
        {
            selectedChain.Add(piece);
            piece.SetSelected(true, selectedChain.Count - 1);

            UpdateLineView();
            OnChainUpdated?.Invoke(selectedChain.Count, ActiveChainType);
        }

        private void RemoveLastPieceFromChain()
        {
            if (selectedChain.Count == 0) return;
            int lastIdx = selectedChain.Count - 1;
            PieceView lastPiece = selectedChain[lastIdx];
            lastPiece.SetSelected(false);
            selectedChain.RemoveAt(lastIdx);

            UpdateLineView();
            OnChainUpdated?.Invoke(selectedChain.Count, ActiveChainType);
        }

        private void AddSkippedOrthogonalPath(PieceView from, PieceView to)
        {
            if (from == null || to == null || boardController == null) return;
            bool sameColumn = from.gridX == to.gridX;
            bool sameRow = from.gridY == to.gridY;
            if (!sameColumn && !sameRow) return;

            int stepX = sameColumn ? 0 : (to.gridX > from.gridX ? 1 : -1);
            int stepY = sameRow ? 0 : (to.gridY > from.gridY ? 1 : -1);
            int x = from.gridX + stepX;
            int y = from.gridY + stepY;
            int checkX = x;
            int checkY = y;
            while (checkX != to.gridX || checkY != to.gridY)
            {
                PieceView intermediate = boardController.GetPieceAt(checkX, checkY);
                if (intermediate == null || intermediate.type != ActiveChainType ||
                    selectedChain.Contains(intermediate)) return;
                checkX += stepX;
                checkY += stepY;
            }

            while (x != to.gridX || y != to.gridY)
            {
                PieceView intermediate = boardController.GetPieceAt(x, y);
                AddPieceToChain(intermediate);
                x += stepX;
                y += stepY;
            }
            AddPieceToChain(to);
        }

        private void ClearSelectionVisuals()
        {
            foreach (var piece in selectedChain)
            {
                if (piece != null)
                    piece.SetSelected(false);
            }
            if (lineView != null)
            {
                lineView.ClearLine();
            }
        }

        private void CancelCurrentSelection()
        {
            IsSelecting = false;
            ClearSelectionVisuals();
            selectedChain.Clear();
            ActiveChainType = PieceType.None;
            OnChainCancelled?.Invoke();
        }

        private void UpdateLineView()
        {
            if (lineView != null)
            {
                lineView.UpdateLine(selectedChain);
            }
        }

        private void UpdateLineView(Vector2 pointerWorldPos)
        {
            if (lineView != null)
            {
                lineView.UpdateLine(selectedChain, pointerWorldPos);
            }
        }

        private PieceView GetPieceAtPosition(Vector2 worldPos)
        {
            // Prefer the logical grid hitbox. It gives each cell a forgiving
            // finger-sized target without overlapping neighbouring colliders.
            PieceView gridPiece = boardController != null
                ? boardController.GetPieceAtWorldPosition(worldPos)
                : null;
            if (gridPiece != null) return gridPiece;

            Collider2D hit = Physics2D.OverlapPoint(worldPos);
            if (hit != null)
            {
                return hit.GetComponent<PieceView>();
            }

            // Raycast fallback if collider is on child or offset
            RaycastHit2D hit3D = Physics2D.Raycast(worldPos, Vector2.zero);
            if (hit3D.collider != null)
            {
                return hit3D.collider.GetComponent<PieceView>();
            }

            return null;
        }

        private bool IsCurrentBoardPiece(PieceView piece)
        {
            if (piece == null || piece.type == PieceType.None || boardController == null)
                return false;
            return boardController.GetPieceAt(piece.gridX, piece.gridY) == piece;
        }
    }
}
