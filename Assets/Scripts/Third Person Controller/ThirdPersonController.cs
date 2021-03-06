using UnityEngine;
#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
using UnityEngine.InputSystem;
#endif

// Note: animations are called via the controller for both the character and capsule using animator null checks

namespace StarterAssets
{
	[RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
	[RequireComponent(typeof(PlayerInput))]
#endif
	public class ThirdPersonController : MonoBehaviour
	{
		[Header("Player")]
		[Tooltip("Move speed of the character in m/s")]
		[SerializeField] private float moveSpeed = 5.335f;

		[Tooltip("How fast the character turns to face movement direction")]
		[Range(0.0f, 0.3f)]
		[SerializeField] private float rotationSmoothTime = 0.12f;

		[Tooltip("Acceleration and deceleration")]
		[SerializeField] private float speedChangeRate = 10.0f;

		[Space(10)]
		[Tooltip("The height the player can jump")]
		[SerializeField] private float jumpHeight = 1.2f;

		[Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
		[SerializeField] private float gravity = -15.0f;

		[Space(10)]
		[Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
		[SerializeField] private float jumpTimeout = 0.50f;

		[Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
		[SerializeField] private float fallTimeout = 0.15f;

		[Header("Player Grounded")]
		[Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
		[SerializeField] private bool grounded = true;

		[Tooltip("Useful for rough ground")]
		[SerializeField] private float groundedOffset = -0.14f;

		[Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
		[SerializeField] private float groundedRadius = 0.28f;

		[Tooltip("What layers the character uses as ground")]
		[SerializeField] private LayerMask groundLayers;

		[Header("Cinemachine")]
		[Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
		[SerializeField] private GameObject cinemachineCameraTarget;

		[Tooltip("How far in degrees can you move the camera up")]
		[SerializeField] private float topClamp = 70.0f;

		[Tooltip("How far in degrees can you move the camera down")]
		[SerializeField] private float bottomClamp = -30.0f;

		[Tooltip("Additional degress to override the camera. Useful for fine tuning camera position when locked")]
		[SerializeField] private float cameraAngleOverride = 0.0f;

		[Tooltip("For locking the camera position on all axis")]
		[SerializeField] private bool lockCameraPosition = false;

		// cinemachine
		private float _cinemachineTargetYaw;
		private float _cinemachineTargetPitch;

		// player
		private float _speed;
		private float _animationBlend;
		private float _targetRotation = 0.0f;
		private float _rotationVelocity;
		private float _verticalVelocity;
		private float _terminalVelocity = 53.0f;

		// timeout deltatime
		private float _jumpTimeoutDelta;
		private float _fallTimeoutDelta;

		// animation IDs
		private int _animIDSpeed;
		private int _animIDGrounded;
		private int _animIDJump;
		private int _animIDFreeFall;
		private int _animIDMotionSpeed;

		private Animator _animator;
		private CharacterController _controller;
		private PlayerInputs _input;
		private Transform _mainCamera;

		private const float _threshold = 0.01f;

		private bool _hasAnimator;

		// colors for ground check sphere gizmo
		private Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
		private Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

		private void Awake()
		{
			// get a reference to our main camera
			_mainCamera = Camera.main.transform;
		}

		private void Start()
		{
			_hasAnimator = TryGetComponent(out _animator);
			_controller = GetComponent<CharacterController>();
			_input = GetComponent<PlayerInputs>();

			AssignAnimationIDs();

			// reset our timeouts on start
			_jumpTimeoutDelta = jumpTimeout;
			_fallTimeoutDelta = fallTimeout;
		}

		private void Update()
		{
			GroundedCheck();
			JumpAndGravity();
			Move();
		}

		private void LateUpdate()
		{
			CameraRotation();
		}

		private void OnDrawGizmosSelected()
		{
			if (grounded) Gizmos.color = transparentGreen;
			else Gizmos.color = transparentRed;

			// when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
			Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - groundedOffset, transform.position.z), groundedRadius);
		}

		private void AssignAnimationIDs()
		{
			_animIDSpeed = Animator.StringToHash("Speed");
			_animIDGrounded = Animator.StringToHash("Grounded");
			_animIDJump = Animator.StringToHash("Jump");
			_animIDFreeFall = Animator.StringToHash("FreeFall");
			_animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
		}

		private void GroundedCheck()
		{
			// set sphere position, with offset
			Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - groundedOffset, transform.position.z);
			grounded = Physics.CheckSphere(spherePosition, groundedRadius, groundLayers, QueryTriggerInteraction.Ignore);

			// update animator if using character
			if (_hasAnimator)
				_animator.SetBool(_animIDGrounded, grounded);
		}

		private void CameraRotation()
		{
			Vector2 look = _input.GetLook();

			// if there is an input and camera position is not fixed
			if (look.sqrMagnitude >= _threshold && !lockCameraPosition)
			{
				_cinemachineTargetYaw += look.x * Time.deltaTime;
				_cinemachineTargetPitch += look.y * Time.deltaTime;
			}

			// clamp our rotations so our values are limited 360 degrees
			_cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
			_cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, bottomClamp, topClamp);

			// Cinemachine will follow this target
			cinemachineCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch + cameraAngleOverride, _cinemachineTargetYaw, 0.0f);
		}

		private void Move()
		{
			Vector2 move = _input.GetMove();

			// set target speed based on move speed, sprint speed and if sprint is pressed
			float targetSpeed = moveSpeed;

			// a simplistic acceleration and deceleration designed to be easy to remove, replace, or iterate upon

			// note: Vector2's == operator uses approximation so is not floating point error prone, and is cheaper than magnitude
			// if there is no input, set the target speed to 0
			if (move == Vector2.zero) targetSpeed = 0.0f;

			// a reference to the players current horizontal velocity
			float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

			float speedOffset = 0.1f;
			float inputMagnitude = _input.IsAnalog() ? move.magnitude : 1f;

			// accelerate or decelerate to target speed
			if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
			{
				// creates curved result rather than a linear one giving a more organic speed change
				// note T in Lerp is clamped, so we don't need to clamp our speed
				_speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * speedChangeRate);

				// round speed to 3 decimal places
				_speed = Mathf.Round(_speed * 1000f) / 1000f;
			}
			else
				_speed = targetSpeed;

			_animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * speedChangeRate);

			// normalise input direction
			Vector3 inputDirection = new Vector3(move.x, 0.0f, move.y).normalized;

			// note: Vector2's != operator uses approximation so is not floating point error prone, and is cheaper than magnitude
			// if there is a move input rotate player when the player is moving
			if (move != Vector2.zero)
			{
				_targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + _mainCamera.eulerAngles.y;
				float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity, rotationSmoothTime);

				// rotate to face input direction relative to camera position
				transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
			}


			Vector3 targetDirection = Quaternion.Euler(0.0f, _targetRotation, 0.0f) * Vector3.forward;

			// move the player
			_controller.Move(targetDirection.normalized * (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);

			// update animator if using character
			if (_hasAnimator)
			{
				_animator.SetFloat(_animIDSpeed, _animationBlend);
				_animator.SetFloat(_animIDMotionSpeed, inputMagnitude);
			}
		}

		private void JumpAndGravity()
		{
			if (grounded)
			{
				// reset the fall timeout timer
				_fallTimeoutDelta = fallTimeout;

				// update animator if using character
				if (_hasAnimator)
				{
					_animator.SetBool(_animIDJump, false);
					_animator.SetBool(_animIDFreeFall, false);
				}

				// stop our velocity dropping infinitely when grounded
				if (_verticalVelocity < 0.0f)
					_verticalVelocity = -2f;

				// Jump
				if (_input.IsJumping() && _jumpTimeoutDelta <= 0.0f)
				{
					// the square root of H * -2 * G = how much velocity needed to reach desired height
					_verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

					// update animator if using character
					if (_hasAnimator)
						_animator.SetBool(_animIDJump, true);
				}

				// jump timeout
				if (_jumpTimeoutDelta >= 0.0f)
					_jumpTimeoutDelta -= Time.deltaTime;
			}
			else
			{
				// reset the jump timeout timer
				_jumpTimeoutDelta = jumpTimeout;

				// fall timeout
				if (_fallTimeoutDelta >= 0.0f)
					_fallTimeoutDelta -= Time.deltaTime;
				else
				{
					// update animator if using character
					if (_hasAnimator)
						_animator.SetBool(_animIDFreeFall, true);
				}
			}

			// apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
			if (_verticalVelocity < _terminalVelocity)
				_verticalVelocity += gravity * Time.deltaTime;
		}

		private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
		{
			if (lfAngle < -360f) lfAngle += 360f;
			if (lfAngle > 360f) lfAngle -= 360f;

			return Mathf.Clamp(lfAngle, lfMin, lfMax);
		}
	}
}