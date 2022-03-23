using UnityEngine;
using UnityEngine.EventSystems;

namespace StarterAssets
{
    public class PlayerInputs : MonoBehaviour
	{
		[Header("Character Input Values")]
		[SerializeField] private Vector2 move;
		[SerializeField] private Vector2 look;
		[SerializeField] private bool jump;

		[Header("Movement Settings")]
		[SerializeField] private bool analogMovement;

		[Header("Touch Settings")]
		[SerializeField] private float touchSensitivity = 10.0f;
		private int lookTouchID = -1;

#if UNITY_EDITOR
		[Header("Editor Input Settings")]
		[SerializeField] private bool keyboardAndMouseControls = true;
		[SerializeField] private float mouseSensitivity = 200.0f;
		[SerializeField] private bool lockCursor = true;
#endif

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
			int touchCount = Input.touchCount;

			if (touchCount > 0)
			{
				//Find suitable touch
				for (int i = 0; i < touchCount; i++)
				{
					Touch currentTouch = Input.GetTouch(i);
					if (currentTouch.phase == TouchPhase.Began)
					{
						if (!EventSystem.current.IsPointerOverGameObject(i))
							lookTouchID = currentTouch.fingerId;
						else
							lookTouchID = -1;

						break;
					}
				}

				Touch currentLookTouch;
				if (GetTouch(lookTouchID, out currentLookTouch))
				{
					look.x = currentLookTouch.deltaPosition.x * touchSensitivity;
					look.y = -currentLookTouch.deltaPosition.y * touchSensitivity;
				}
			}
			else
				lookTouchID = -1;
		}

		private bool GetTouch(int fingerID, out Touch result)
		{
			for (int i = 0; i < Input.touchCount; i++)
			{
				Touch currentTouch = Input.GetTouch(i);

				if (currentTouch.fingerId == fingerID)
				{
					result = currentTouch;
					return true;
				}
			}

			result = new Touch();
			return false;
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