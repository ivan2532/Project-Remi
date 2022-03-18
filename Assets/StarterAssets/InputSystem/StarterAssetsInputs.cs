using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace StarterAssets
{
	public class StarterAssetsInputs : MonoBehaviour
	{
		[Header("Character Input Values")]
		[SerializeField] private Vector2 move;
		[SerializeField] private Vector2 look;
		[SerializeField] private bool jump;

		[Header("Movement Settings")]
		[SerializeField] private bool analogMovement;

		[Header("Touch Settings")]
		[SerializeField] private float touchSensitivity = 10.0f;
		private int lookTouchID;

#if UNITY_EDITOR
		[Header("Editor Input Settings")]
		[SerializeField] private bool keyboardAndMouseControls = true;
		[SerializeField] private float mouseSensitivity = 200.0f;
		[SerializeField] private bool lockCursor = true;
#endif

		private void Awake()
		{
			EnhancedTouchSupport.Enable();
		}

		private void Update()
		{
			InputUpdate();
		}

		private void InputUpdate()
		{
#if UNITY_EDITOR
			if (keyboardAndMouseControls)
			{
				look = new Vector2(Input.GetAxis("Mouse X"), -Input.GetAxis("Mouse Y")) * mouseSensitivity;
				move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
				jump = Input.GetKey(KeyCode.Space);

				return;
			}
#endif

			look = Vector2.zero;

			if (Touch.activeTouches.Count > 0)
			{
				//Find suitable touch
				for (int i = 0; i < Touch.activeTouches.Count; i++)
				{
					if (Touch.activeTouches[i].phase == TouchPhase.Began && !EventSystem.current.IsPointerOverGameObject(Touch.activeTouches[i].touchId))
					{
						lookTouchID = Touch.activeTouches[i].touchId;
						break;
					}
				}

				Touch currentLookTouch;
				if (GetTouch(lookTouchID, out currentLookTouch))
				{
					look.x = currentLookTouch.delta.x * touchSensitivity;
					look.y = -currentLookTouch.delta.y * touchSensitivity;
				}
			}
		}

		private bool GetTouch(int touchID, out Touch result)
        {
			for (int i = 0; i < Touch.activeTouches.Count; i++)
            {
				var currentTouch = Touch.activeTouches[i];

				if (currentTouch.touchId == touchID)
				{
					result = currentTouch;
					return true;
				}
			}

			result = new Touch();
			return false;
		}

        public void OnMove(InputAction.CallbackContext value)
		{
			MoveInput(value.ReadValue<Vector2>());
		}

		public void OnJump(InputAction.CallbackContext value)
		{
			JumpInput(value.action.triggered);
		}

		public void MoveInput(Vector2 newMoveDirection)
		{
			move = newMoveDirection;
		} 

		public void JumpInput(bool newJumpState)
		{
			jump = newJumpState;
		}

		public Vector2 GetMove() => move;

		public Vector2 GetLook() => look;

		public bool IsJumping() => jump;

		public bool IsAnalog() => analogMovement;

#if UNITY_EDITOR

		private void OnApplicationFocus(bool hasFocus)
		{
			if(keyboardAndMouseControls)
				SetCursorState(lockCursor);
		}

		private void SetCursorState(bool newState)
		{
			Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
		}

#endif
	}
}