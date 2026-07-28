using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DogCrush.Board
{
    public class BoardGravityController : MonoBehaviour
    {
        public BoardController boardController;
        public PieceSpawner spawner;

        public bool IsResolving { get; private set; }
        private int resolutionVersion;

        /// <summary>
        /// Invalidates an in-flight gravity operation before a new match or
        /// board is created. This prevents delayed animation callbacks from
        /// writing into the replacement grid.
        /// </summary>
        public void CancelResolution()
        {
            resolutionVersion++;
            IsResolving = false;
        }

        public IEnumerator ProcessRemovalAndRefill(List<PieceView> removedPieces, System.Action onComplete)
        {
            int operationVersion = ++resolutionVersion;
            IsResolving = true;

            // 1. Despawn removed pieces
            int pendingDespawns = 0;
            int despawnIndex = 0;
            foreach (var piece in removedPieces)
            {
                if (piece == null) continue;
                pendingDespawns++;
                boardController.SetPieceAt(piece.gridX, piece.gridY, null);
                float despawnDelay = Mathf.Min(despawnIndex * 0.025f, 0.18f);
                despawnIndex++;
                piece.AnimateDespawn(despawnDelay, () =>
                {
                    // Do not recycle a pooled view that may already belong to
                    // the replacement board after a restart.
                    if (operationVersion == resolutionVersion)
                    {
                        spawner.RecyclePiece(piece);
                    }
                    pendingDespawns--;
                });
            }

            while (pendingDespawns > 0)
            {
                if (operationVersion != resolutionVersion) yield break;
                yield return null;
            }

            // 2. Compact columns downward
            int movingPiecesCount = 0;
            float fallSpeed = boardController.config != null ? boardController.config.fallSpeed : 12f;
            int availableTypeCount = boardController.config != null
                ? Mathf.Clamp(boardController.config.typeCount, 1, (int)PieceType.Collar + 1)
                : (int)PieceType.Collar + 1;

            for (int x = 0; x < boardController.Columns; x++)
            {
                List<int> playableRows = new List<int>();
                for (int row = 0; row < boardController.Rows; row++)
                {
                    if (boardController.IsPlayableCell(x, row)) playableRows.Add(row);
                }
                int emptySlotsBelow = 0;
                for (int rowIndex = 0; rowIndex < playableRows.Count; rowIndex++)
                {
                    int y = playableRows[rowIndex];
                    PieceView current = boardController.GetPieceAt(x, y);
                    if (current == null)
                    {
                        emptySlotsBelow++;
                    }
                    else if (emptySlotsBelow > 0)
                    {
                        int newY = playableRows[rowIndex - emptySlotsBelow];
                        boardController.SetPieceAt(x, y, null);
                        boardController.SetPieceAt(x, newY, current);

                        Vector3 targetWorldPos = boardController.GridToWorldPosition(x, newY);
                        movingPiecesCount++;
                        float moveDelay = x * 0.012f + newY * 0.004f;
                        current.MoveToWorldPosition(targetWorldPos, fallSpeed, moveDelay, () =>
                        {
                            movingPiecesCount--;
                        });
                    }
                }

                // 3. Fill empty top slots
                for (int fillIndex = 0; fillIndex < emptySlotsBelow; fillIndex++)
                {
                    int targetY = playableRows[playableRows.Count - emptySlotsBelow + fillIndex];
                    PieceType randomType = (PieceType)Random.Range(0, availableTypeCount);

                    Vector3 spawnWorldPos = boardController.GridToWorldPosition(
                        x,
                        playableRows[playableRows.Count - 1] + fillIndex + 1);
                    Vector3 targetWorldPos = boardController.GridToWorldPosition(x, targetY);

                    PieceView newPiece = spawner.SpawnPiece(randomType, x, targetY, spawnWorldPos);
                    boardController.SetPieceAt(x, targetY, newPiece);

                    movingPiecesCount++;
                    float refillDelay = x * 0.012f + fillIndex * 0.035f;
                    newPiece.MoveToWorldPosition(targetWorldPos, fallSpeed, refillDelay, () =>
                    {
                        movingPiecesCount--;
                    });
                }
            }

            while (movingPiecesCount > 0)
            {
                if (operationVersion != resolutionVersion) yield break;
                yield return null;
            }

            // A second touch or a cancelled animation must never leave a
            // playable cell empty. Fill any defensive gaps before unlocking input.
            boardController.FillMissingCells();

            // Ensure grid has valid moves after refill
            boardController.EnsureHasValidMoves();

            if (operationVersion != resolutionVersion) yield break;
            IsResolving = false;
            onComplete?.Invoke();
        }
    }
}
