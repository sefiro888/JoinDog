using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DogCrush.InputSystem
{
    public class ChainInputHandler : MonoBehaviour
    {
        public System.Action<Vector2> OnPointerDownEvent;
        public System.Action<Vector2> OnPointerDragEvent;
        public System.Action OnPointerUpEvent;

        public bool IsPointerPressed { get; private set; }
        private Camera mainCamera;

        private void Awake()
        {
            mainCamera = Camera.main;
        }

        private void Update()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;

#if ENABLE_INPUT_SYSTEM
            Pointer pointer = Pointer.current;
            if (pointer != null)
            {
                Vector2 screenPos = pointer.position.ReadValue();
                bool isPressed = pointer.press.isPressed;

                if (pointer.press.wasPressedThisFrame)
                {
                    IsPointerPressed = true;
                    OnPointerDownEvent?.Invoke(GetWorldPosition(screenPos));
                }
                else if (isPressed && IsPointerPressed)
                {
                    OnPointerDragEvent?.Invoke(GetWorldPosition(screenPos));
                }
                else if (pointer.press.wasReleasedThisFrame && IsPointerPressed)
                {
                    IsPointerPressed = false;
                    OnPointerUpEvent?.Invoke();
                }
                return;
            }
#endif

            // Fallback for Legacy Input
            TryLegacyInput();
        }

        private void TryLegacyInput()
        {
            try
            {
                if (Input.touchCount > 0)
                {
                    Touch touch = Input.GetTouch(0);
                    Vector2 worldPos = GetWorldPosition(touch.position);

                    if (touch.phase == UnityEngine.TouchPhase.Began)
                    {
                        IsPointerPressed = true;
                        OnPointerDownEvent?.Invoke(worldPos);
                    }
                    else if ((touch.phase == UnityEngine.TouchPhase.Moved || touch.phase == UnityEngine.TouchPhase.Stationary) && IsPointerPressed)
                    {
                        OnPointerDragEvent?.Invoke(worldPos);
                    }
                    else if (touch.phase == UnityEngine.TouchPhase.Ended || touch.phase == UnityEngine.TouchPhase.Canceled)
                    {
                        IsPointerPressed = false;
                        OnPointerUpEvent?.Invoke();
                    }
                    return;
                }

                if (Input.GetMouseButtonDown(0))
                {
                    IsPointerPressed = true;
                    OnPointerDownEvent?.Invoke(GetWorldPosition(Input.mousePosition));
                }
                else if (Input.GetMouseButton(0) && IsPointerPressed)
                {
                    OnPointerDragEvent?.Invoke(GetWorldPosition(Input.mousePosition));
                }
                else if (Input.GetMouseButtonUp(0) && IsPointerPressed)
                {
                    IsPointerPressed = false;
                    OnPointerUpEvent?.Invoke();
                }
            }
            catch (System.InvalidOperationException)
            {
                // Handle if activeInputHandler is strictly set to New Input System without legacy support
            }
        }

        public Vector2 GetWorldPosition(Vector3 screenPos)
        {
            if (mainCamera == null) return Vector2.zero;
            Vector3 world = mainCamera.ScreenToWorldPoint(screenPos);
            return new Vector2(world.x, world.y);
        }
    }
}
