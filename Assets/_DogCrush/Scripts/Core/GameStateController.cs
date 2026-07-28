using UnityEngine;

namespace DogCrush.Core
{
    public enum GameState
    {
        Initializing,
        Playing,
        Selecting,
        Resolving,
        GameOver
    }

    public class GameStateController : MonoBehaviour
    {
        public GameState CurrentState { get; private set; } = GameState.Initializing;

        public System.Action<GameState, GameState> OnStateChanged;

        public void ChangeState(GameState newState)
        {
            if (CurrentState == newState) return;
            GameState oldState = CurrentState;
            CurrentState = newState;
            OnStateChanged?.Invoke(oldState, newState);
        }

        public bool CanSelectPieces()
        {
            return CurrentState == GameState.Playing || CurrentState == GameState.Selecting;
        }
    }
}
