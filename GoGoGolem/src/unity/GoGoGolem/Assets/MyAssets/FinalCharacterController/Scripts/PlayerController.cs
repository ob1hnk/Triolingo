using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MyAssets.FinalCharacterController
{
    [DefaultExecutionOrder(-1)]
    public class PlayerController : MonoBehaviour
    {
        #region Class Variables
        [Header("Components")]
        [SerializeField] private CharacterController _characterController;

        [Header("Base Movement")]
        public float walkAcceleration = 25f;
        public float walkSpeed = 2f;
        public float runAcceleration = 35f;
        public float runSpeed = 4f;
        public float sprintAcceleration = 50f;
        public float sprintSpeed = 7f;
        public float inAirAcceleration = 25f;
        public float drag = 20f;
        public float gravity = 25f;
        public float movingThreshold = 0.01f;
        public float rotationSpeed = 10f;

        [Header("Ground Details")]
        [SerializeField] private LayerMask _groundLayers;

        private PlayerLocomotionInput _playerLocomotionInput;
        private PlayerState _playerState;
        
        private float _verticalVelocity = 0f;
        private float _antiBump;
        private float _stepOffset;

		private PlayerMovementState _lastMovementState = PlayerMovementState.Falling;

        #endregion

        #region Startup
        private void Awake()
        {
            _playerLocomotionInput = GetComponent<PlayerLocomotionInput>();
            _playerState = GetComponent<PlayerState>();

            _antiBump = sprintSpeed;
            _stepOffset = _characterController.stepOffset;
        }
        #endregion

        #region Update Logic
        private void Update()
        {
            UpdateMovementState();
            HandleLateralMovement();
            HandleVerticalMovement();
        }

        private void UpdateMovementState()
        {
			_lastMovementState = _playerState.CurrentPlayerMovementState;

            bool isMovementInput = _playerLocomotionInput.MovementInput != Vector2.zero;    //order
            bool isMovingLaterally = IsMovingLaterally();                                   //matter
            bool isSprinting = _playerLocomotionInput.SprintToggledOn && isMovingLaterally; //order matters //matters
            bool isGrounded = IsGrounded();


            PlayerMovementState lateralState = isSprinting ? PlayerMovementState.Sprinting :
                                               isMovingLaterally || isMovementInput ? PlayerMovementState.Running : PlayerMovementState.Idling;

            _playerState.SetPlayerMovementState(lateralState);

			// Control Airborn State
			if (!isGrounded && _characterController.velocity.y <= 0f)
			{
					_playerState.SetPlayerMovementState(PlayerMovementState.Falling);
					_characterController.stepOffset = 0f;
			}

			else if (!isGrounded && _characterController.velocity.y > 0f)
			{
				_characterController.stepOffset = 0f;
			}

			else
			{
					_characterController.stepOffset = _stepOffset;
			}

        }

        private void HandleVerticalMovement()
        {
            bool isGrounded = _playerState.InGroundedState();

            _verticalVelocity -= gravity * Time.deltaTime;

            if (isGrounded && _verticalVelocity < 0 && _lastMovementState != PlayerMovementState.Falling)
                _verticalVelocity = -_antiBump;
			if (_playerState.IsStateGroundedState(_lastMovementState) && !isGrounded)
            {
                _verticalVelocity += _antiBump;
            }

        }

        private void HandleLateralMovement()
        {
            // Create quick references for current state
            bool isSprinting = _playerState.CurrentPlayerMovementState == PlayerMovementState.Sprinting;
            bool isGrounded = _playerState.InGroundedState();
            bool isWalking = _playerState.CurrentPlayerMovementState == PlayerMovementState.Walking;

            // State dependent acceleration and speed
            float lateralAcceleration = !isGrounded ? inAirAcceleration :
                                        isWalking ? walkAcceleration :
                                        isSprinting ? sprintAcceleration : runAcceleration;

            float clampLateralMagnitude = !isGrounded ? sprintSpeed :
                                          isWalking ? walkSpeed :
                                          isSprinting ? sprintSpeed : runSpeed;
            
            // 입력 방향 계산
            Vector3 movementDirection = new Vector3(_playerLocomotionInput.MovementInput.x, 0f, _playerLocomotionInput.MovementInput.y).normalized;

        // 입력이 있을 때만 회전 처리
        if (movementDirection != Vector3.zero)
                        {
                                // 이동 방향을 바라보는 Quaternion 계산
                                Quaternion targetRotation = Quaternion.LookRotation(movementDirection);

                                // 현재 회전에서 목표 회전으로 부드럽게 회전
                                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                        }
            Vector3 movementDelta = movementDirection * lateralAcceleration * Time.deltaTime;
            Vector3 newVelocity = _characterController.velocity + movementDelta;

            // Add drag to player
            Vector3 currentDrag = newVelocity.normalized * drag * Time.deltaTime;
            newVelocity = (newVelocity.magnitude > drag * Time.deltaTime) ? newVelocity - currentDrag : Vector3.zero;
            newVelocity = Vector3.ClampMagnitude(newVelocity, clampLateralMagnitude);
            newVelocity.y += _verticalVelocity;
            newVelocity = !isGrounded ? HandleSteepWalls(newVelocity) : newVelocity;

            // Move character (Unity suggests only calling this once per tick)
            _characterController.Move(newVelocity * Time.deltaTime);
        }
        private Vector3 HandleSteepWalls(Vector3 velocity)
        {
            Vector3 normal = CharacterControllerUtils.GetNormalWithSphereCast(_characterController, _groundLayers);
            float angle = Vector3.Angle(normal, Vector3.up);
            bool validAngle = angle <= _characterController.slopeLimit;

            if (!validAngle && _verticalVelocity < 0f)
                velocity = Vector3.ProjectOnPlane(velocity, normal);

            return velocity;
        }

        #endregion

        #region State Checks
        private bool IsMovingLaterally()
        {
            Vector3 lateralVelocity = new Vector3(_characterController.velocity.x, 0f, _characterController.velocity.z);

            return lateralVelocity.magnitude > movingThreshold;
        }

        private bool IsGrounded()
        {
            bool grounded = _playerState.InGroundedState() ? IsGroundedWhileGrounded() : IsGroundedWhileAirborne();

            return grounded;
        }

        private bool IsGroundedWhileGrounded()
        {
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - _characterController.radius, transform.position.z);

            bool grounded = Physics.CheckSphere(spherePosition, _characterController.radius, _groundLayers, QueryTriggerInteraction.Ignore);

            return grounded;
        }

        private bool IsGroundedWhileAirborne()
        {
            Vector3 normal = CharacterControllerUtils.GetNormalWithSphereCast(_characterController, _groundLayers);
            float angle = Vector3.Angle(normal, Vector3.up);
            bool validAngle = angle <= _characterController.slopeLimit;

            return _characterController.isGrounded && validAngle;
        }
        #endregion
    }
}