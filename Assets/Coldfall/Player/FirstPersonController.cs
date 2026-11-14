using Coldfall.Player.Input;
using UnityEngine;

namespace Coldfall.Player
{
    /// <summary>
    /// The M0 first-person character controller: walk, crouch, sprint, stairs and vaulting.
    /// <para>
    /// Movement runs on the fixed physics step, as docs/09-technical-architecture.md, 9.1
    /// requires. That creates the classic first-person problem: a camera parented to a body that
    /// moves at 50 Hz visibly stutters when the game renders faster. The fix here is that the
    /// camera is <b>not</b> a child of this transform. It is a separate rig whose position is
    /// interpolated every frame between the last two physics positions, which is why exit
    /// criterion 1 of M0 ("without any camera artefact") is satisfiable at all.
    /// </para>
    /// <para>
    /// No stamina, no encumbrance and no temperature here. Those arrive in M1 and will modulate
    /// the speeds through <see cref="SpeedMultiplier"/> rather than by editing this class.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [Header("Rig")]
        [Tooltip("The camera rig. Must NOT be a child of this object, or interpolation cannot work.")]
        [SerializeField] private Transform cameraRig;

        [Header("Speeds, metres per second")]
        [Tooltip("Deliberately close to a Sluggish zombie's 1.6 m/s: walking cannot outrun the dead.")]
        [SerializeField] private float walkSpeed = 1.6f;
        [SerializeField] private float crouchSpeed = 0.85f;
        [Tooltip("Below a Warm zombie's 5.4 m/s sprint, so a warm pursuer gains ground.")]
        [SerializeField] private float sprintSpeed = 4.6f;

        [Header("Acceleration")]
        [SerializeField] private float groundAcceleration = 14f;
        [SerializeField] private float airAcceleration = 2f;
        [SerializeField] private float gravity = -18f;

        [Header("Stance, metres")]
        [SerializeField] private float standingHeight = 1.8f;
        [SerializeField] private float crouchingHeight = 1.2f;
        [SerializeField] private float standingEyeHeight = 1.66f;
        [SerializeField] private float crouchingEyeHeight = 1.06f;
        [SerializeField] private float stanceChangeSpeed = 6f;

        [Header("Look")]
        [SerializeField] private float minPitch = -85f;
        [SerializeField] private float maxPitch = 85f;

        [Header("Vault")]
        [SerializeField] private float vaultReach = 0.9f;
        [SerializeField] private float vaultMinObstacleHeight = 0.5f;
        [SerializeField] private float vaultMaxObstacleHeight = 1.35f;
        [SerializeField] private float vaultDuration = 0.55f;
        [SerializeField] private float vaultArcHeight = 0.35f;
        [SerializeField] private LayerMask obstacleLayers = ~0;

        private CharacterController _controller;
        private IPlayerInput _input;

        private float _yaw;
        private float _pitch;
        private float _currentHeight;
        private float _currentEyeHeight;
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;
        private bool _vaultQueued;

        private Vector3 _previousPhysicsPosition;
        private Vector3 _currentPhysicsPosition;
        private float _lastFixedTime;

        private Vector2 _viewPunch;
        private Vector2 _viewPunchVelocity;

        private bool _isVaulting;
        private float _vaultElapsed;
        private Vector3 _vaultStart;
        private Vector3 _vaultEnd;

        /// <summary>
        /// Scales every ground speed. M1 systems (encumbrance, clothing bulk, leg wounds,
        /// hypothermia) apply their penalties here instead of changing the speed fields.
        /// </summary>
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>
        /// Kicks the view by a pitch and yaw offset in degrees, which then springs back to zero.
        /// <para>
        /// This is the only impact feedback the greybox has: with no audio and no animation yet,
        /// the difference between a clean swing and one that clipped a doorframe has to be felt
        /// through the camera or it cannot be felt at all (M0 exit criterion 2).
        /// </para>
        /// <para>
        /// The punch is an offset on top of the look angles, never a change to them, so a kick
        /// can never steal the player's aim: when the spring settles, the view is exactly where
        /// the player pointed it.
        /// </para>
        /// </summary>
        public void AddViewPunch(float pitchDegrees, float yawDegrees)
        {
            _viewPunchVelocity += new Vector2(pitchDegrees, yawDegrees);
        }

        /// <summary>True while a vault is playing. Movement input is ignored during it.</summary>
        public bool IsVaulting => _isVaulting;

        /// <summary>True while the character is in the crouched stance.</summary>
        public bool IsCrouching { get; private set; }

        /// <summary>Current horizontal speed in metres per second, for debug readouts and audio.</summary>
        public float HorizontalSpeed => _horizontalVelocity.magnitude;

        /// <summary>
        /// Replaces the input source. Called by tests and by whatever owns input routing; if
        /// nothing calls it, the controller falls back to keyboard and mouse.
        /// </summary>
        public void SetInput(IPlayerInput input)
        {
            _input = input;
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _input ??= new DeviceInput();

            // Stairs are handled by the step offset rather than by any bespoke code. Every
            // staircase in the game must therefore have risers below this value.
            _controller.stepOffset = 0.35f;
            _controller.slopeLimit = 50f;
            _controller.skinWidth = 0.02f;

            _currentHeight = standingHeight;
            _currentEyeHeight = standingEyeHeight;
            ApplyHeight(_currentHeight);

            _yaw = transform.eulerAngles.y;
            _previousPhysicsPosition = transform.position;
            _currentPhysicsPosition = transform.position;
            _lastFixedTime = Time.time;
        }

        private void OnEnable()
        {
            CaptureCursor(true);
        }

        private void OnDisable()
        {
            CaptureCursor(false);
        }

        /// <summary>
        /// Locks the cursor to the window and hides it, or releases it. Without this the mouse
        /// escapes the view and first-person look is untestable; with it, Escape has to give the
        /// cursor back or the editor becomes unusable.
        /// </summary>
        private static void CaptureCursor(bool captured)
        {
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
        }

        private void OnValidate()
        {
            crouchingHeight = Mathf.Min(crouchingHeight, standingHeight);
            standingEyeHeight = Mathf.Min(standingEyeHeight, standingHeight);
            crouchingEyeHeight = Mathf.Min(crouchingEyeHeight, crouchingHeight);
        }

        private void Update()
        {
            if (_input == null)
            {
                return;
            }

            if (_input.ReleaseCursorPressed)
            {
                CaptureCursor(Cursor.lockState != CursorLockMode.Locked);
            }

            // Looking only while captured, so clicking back into the window does not snap the
            // view by the distance the cursor travelled outside it.
            var look = Cursor.lockState == CursorLockMode.Locked ? _input.Look : Vector2.zero;
            _yaw += look.x;
            _pitch = Mathf.Clamp(_pitch - look.y, minPitch, maxPitch);

            // Sampled here because a press lasts exactly one frame, and movement runs on the
            // fixed step, which may not happen on this frame at all.
            if (_input.VaultPressed)
            {
                _vaultQueued = true;
            }

            UpdateCameraRig();
        }

        private void FixedUpdate()
        {
            if (_input == null)
            {
                return;
            }

            _previousPhysicsPosition = _currentPhysicsPosition;
            var delta = Time.fixedDeltaTime;

            if (_isVaulting)
            {
                StepVault(delta);
            }
            else
            {
                StepStance(delta);
                StepMovement(delta);
                TryStartVault();
            }

            _currentPhysicsPosition = transform.position;
            _lastFixedTime = Time.time;
        }

        private void StepStance(float delta)
        {
            var wantsCrouch = _input.CrouchHeld;

            // Standing up into a ceiling would push the capsule through geometry, so the stance
            // only returns to standing when there is room for the whole capsule.
            if (!wantsCrouch && IsCrouching && !HasHeadroomToStand())
            {
                wantsCrouch = true;
            }

            IsCrouching = wantsCrouch;

            var targetHeight = wantsCrouch ? crouchingHeight : standingHeight;
            var targetEye = wantsCrouch ? crouchingEyeHeight : standingEyeHeight;

            _currentHeight = Mathf.MoveTowards(_currentHeight, targetHeight, stanceChangeSpeed * delta);
            _currentEyeHeight = Mathf.MoveTowards(_currentEyeHeight, targetEye, stanceChangeSpeed * delta);
            ApplyHeight(_currentHeight);
        }

        private void StepMovement(float delta)
        {
            var input = _input.Move;
            var forward = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
            var right = Quaternion.Euler(0f, _yaw, 0f) * Vector3.right;
            var wish = (forward * input.y + right * input.x);

            var speed = IsCrouching
                ? crouchSpeed
                : _input.SprintHeld ? sprintSpeed : walkSpeed;
            speed *= Mathf.Max(0f, SpeedMultiplier);

            var target = wish * speed;
            var acceleration = _controller.isGrounded ? groundAcceleration : airAcceleration;
            _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, target, acceleration * delta);

            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                // A small downward bias keeps isGrounded stable on slopes and stair edges
                // instead of flickering, which is what produces camera jitter on stairs.
                _verticalVelocity = -2f;
            }
            else
            {
                _verticalVelocity += gravity * delta;
            }

            var motion = _horizontalVelocity + Vector3.up * _verticalVelocity;
            _controller.Move(motion * delta);
        }

        private void TryStartVault()
        {
            if (!_vaultQueued)
            {
                return;
            }

            _vaultQueued = false;

            if (!_controller.isGrounded || IsCrouching)
            {
                return;
            }

            if (!FindVaultTarget(out var landing))
            {
                return;
            }

            _isVaulting = true;
            _vaultElapsed = 0f;
            _vaultStart = transform.position;
            _vaultEnd = landing;

            // The capsule is switched off for the duration so the body does not fight the
            // obstacle it is crossing. Everything in the path was already checked below.
            _controller.enabled = false;
        }

        /// <summary>
        /// Looks for a waist-high obstacle directly ahead with clear space above it and a
        /// standable surface beyond it, and reports where the character would land.
        /// </summary>
        private bool FindVaultTarget(out Vector3 landing)
        {
            landing = default;

            var origin = transform.position + Vector3.up * vaultMinObstacleHeight;
            var forward = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;

            if (!Physics.Raycast(origin, forward, out var frontHit, vaultReach, obstacleLayers, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            // Find the top of the obstacle by probing downwards from above its face.
            var above = frontHit.point + forward * 0.1f + Vector3.up * (vaultMaxObstacleHeight + 0.5f);
            if (!Physics.Raycast(above, Vector3.down, out var topHit, vaultMaxObstacleHeight + 1f, obstacleLayers, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            var obstacleHeight = topHit.point.y - transform.position.y;
            if (obstacleHeight < vaultMinObstacleHeight || obstacleHeight > vaultMaxObstacleHeight)
            {
                return false;
            }

            // Somewhere to come down on the far side.
            var beyond = topHit.point + forward * (_controller.radius * 2f + 0.25f) + Vector3.up * 0.2f;
            if (!Physics.Raycast(beyond, Vector3.down, out var landHit, obstacleHeight + 2f, obstacleLayers, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            // Room for the standing capsule where we intend to arrive.
            var footToCentre = Vector3.up * (standingHeight * 0.5f);
            var capsuleCentre = landHit.point + footToCentre;
            var half = Mathf.Max(0f, standingHeight * 0.5f - _controller.radius);
            if (Physics.CheckCapsule(
                    capsuleCentre - Vector3.up * half,
                    capsuleCentre + Vector3.up * half,
                    _controller.radius * 0.95f,
                    obstacleLayers,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            landing = landHit.point;
            return true;
        }

        private void StepVault(float delta)
        {
            _vaultElapsed += delta;
            var t = Mathf.Clamp01(_vaultElapsed / Mathf.Max(0.01f, vaultDuration));

            var flat = Vector3.Lerp(_vaultStart, _vaultEnd, t);
            // A single sine hump: zero at both ends, so the body never snaps vertically.
            flat.y += Mathf.Sin(t * Mathf.PI) * vaultArcHeight;
            transform.position = flat;

            if (t < 1f)
            {
                return;
            }

            transform.position = _vaultEnd;
            _isVaulting = false;
            _verticalVelocity = 0f;
            _horizontalVelocity = Vector3.zero;
            _controller.enabled = true;
        }

        /// <summary>
        /// A critically damped spring, so the kick returns to zero without oscillating. An
        /// overshooting camera reads as a bug rather than as an impact.
        /// </summary>
        private void StepViewPunch(float delta)
        {
            if (delta <= 0f)
            {
                return;
            }

            const float stiffness = 180f;
            const float damping = 22f;

            var acceleration = -_viewPunch * stiffness - _viewPunchVelocity * damping;
            _viewPunchVelocity += acceleration * delta;
            _viewPunch += _viewPunchVelocity * delta;

            if (_viewPunch.sqrMagnitude < 0.0001f && _viewPunchVelocity.sqrMagnitude < 0.0001f)
            {
                _viewPunch = Vector2.zero;
                _viewPunchVelocity = Vector2.zero;
            }
        }

        private bool HasHeadroomToStand()
        {
            var feet = transform.position;
            var radius = _controller.radius * 0.95f;
            var bottom = feet + Vector3.up * radius;
            var top = feet + Vector3.up * (standingHeight - radius);

            return !Physics.CheckCapsule(bottom, top, radius, obstacleLayers, QueryTriggerInteraction.Ignore);
        }

        private void ApplyHeight(float height)
        {
            _controller.height = height;
            // CharacterController's centre is relative to the transform, which sits at the feet.
            _controller.center = Vector3.up * (height * 0.5f);
        }

        private void UpdateCameraRig()
        {
            if (cameraRig == null)
            {
                return;
            }

            var fixedDelta = Time.fixedDeltaTime;
            var sinceFixed = Time.time - _lastFixedTime;
            var alpha = fixedDelta <= 0f ? 1f : Mathf.Clamp01(sinceFixed / fixedDelta);

            var body = Vector3.Lerp(_previousPhysicsPosition, _currentPhysicsPosition, alpha);

            StepViewPunch(Time.deltaTime);

            cameraRig.SetPositionAndRotation(
                body + Vector3.up * _currentEyeHeight,
                Quaternion.Euler(_pitch + _viewPunch.x, _yaw + _viewPunch.y, 0f));
        }
    }
}
