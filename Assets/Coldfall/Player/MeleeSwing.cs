using Coldfall.Player.Input;
using Coldfall.World;
using UnityEngine;

namespace Coldfall.Player
{
    /// <summary>
    /// The M0 placeholder melee: charge and release, with the blade swept through its arc and
    /// traced against the world.
    /// <para>
    /// This exists to answer M0 exit criterion 2 — a full-charge swing must visibly clip a
    /// doorframe and cancel, and the difference between a clipped and a clean swing must be
    /// legible without a debug readout. Two design commitments follow from that, and both
    /// outlive the placeholder:
    /// </para>
    /// <list type="number">
    /// <item>
    /// <b>The arc is traced, not hitscanned.</b> The blade is swept through its path in
    /// sub-steps and tested where it actually is, so a cramped room really does take the
    /// player's best option away (docs/04-zombies-and-combat.md, 4.7). A hitscan from the
    /// camera would make geometry irrelevant and quietly delete that design.
    /// </item>
    /// <item>
    /// <b>Clipping is a punishment, not a miss.</b> It cancels the swing, kicks the view hard
    /// and costs a longer recovery, because the whole point is that committing to a heavy
    /// swing in the wrong place is a decision the player regrets.
    /// </item>
    /// </list>
    /// <para>
    /// No stamina, no durability, no damage model: M1 and M2 own those. Nothing here should be
    /// read as final tuning.
    /// </para>
    /// </summary>
    public sealed class MeleeSwing : MonoBehaviour
    {
        /// <summary>What the swing is doing right now.</summary>
        public enum SwingState
        {
            /// <summary>Weapon at rest, ready to charge.</summary>
            Ready,

            /// <summary>Attack held, winding up. Releasing commits the swing.</summary>
            Charging,

            /// <summary>Blade travelling through its arc and being traced.</summary>
            Swinging,

            /// <summary>Returning to rest. Input is ignored until this ends.</summary>
            Recovering,
        }

        [Header("Rig")]
        [Tooltip("The weapon transform. Must be a child of the camera rig so it follows the view.")]
        [SerializeField] private Transform weapon;

        [SerializeField] private FirstPersonController controller;

        [Header("Blade, local space of the weapon")]
        [Tooltip("Where the blade starts. Roughly the hand.")]
        [SerializeField] private Vector3 bladeHilt = new Vector3(0f, 0f, -0.15f);

        [Tooltip("Where the blade ends. The distance between the two is the reach that matters.")]
        [SerializeField] private Vector3 bladeTip = new Vector3(0f, 0f, 0.55f);

        [SerializeField] private float bladeRadius = 0.06f;

        [Header("Charge")]
        [Tooltip("Seconds of holding to reach full charge.")]
        [SerializeField] private float fullChargeTime = 0.8f;

        [Tooltip("Below this charge the swing is a weak flick rather than a committed blow.")]
        [SerializeField] private float minimumCharge = 0.15f;

        [Header("Swing timing, seconds")]
        [SerializeField] private float swingTimeAtMinCharge = 0.20f;
        [SerializeField] private float swingTimeAtFullCharge = 0.42f;
        [SerializeField] private float recoveryAfterHit = 0.25f;
        [SerializeField] private float recoveryAfterMiss = 0.18f;

        [Tooltip("Deliberately the longest recovery in the class: clipping the world must hurt.")]
        [SerializeField] private float recoveryAfterClip = 0.55f;

        [Header("Poses, local to the camera rig")]
        [SerializeField] private Vector3 restPosition = new Vector3(0.28f, -0.26f, 0.42f);
        [SerializeField] private Vector3 restEuler = new Vector3(12f, -16f, 6f);
        [SerializeField] private Vector3 chargedPosition = new Vector3(0.42f, -0.12f, 0.20f);
        [SerializeField] private Vector3 chargedEuler = new Vector3(-28f, 38f, 24f);
        [SerializeField] private Vector3 followThroughPosition = new Vector3(-0.30f, -0.34f, 0.40f);
        [SerializeField] private Vector3 followThroughEuler = new Vector3(26f, -52f, -34f);

        [Header("Tracing")]
        [Tooltip("Sub-steps per physics step. Too few and a fast blade tunnels through a wall.")]
        [SerializeField] private int traceSubSteps = 5;

        [SerializeField] private LayerMask hittableLayers = ~0;

        [Header("Feedback")]
        [SerializeField] private float cleanHitPunch = 2.4f;
        [SerializeField] private float clipPunch = 6.5f;

        private IPlayerInput _input;
        private SwingState _state = SwingState.Ready;
        private float _charge;
        private float _swingElapsed;
        private float _swingDuration;
        private float _recoveryRemaining;
        private float _committedCharge;
        private Vector3 _previousHilt;
        private Vector3 _previousTip;
        private bool _hasPreviousBlade;

        private readonly Collider[] _overlapBuffer = new Collider[16];

        /// <summary>Current state, for debug overlays and later for animation.</summary>
        public SwingState State => _state;

        /// <summary>Charge built so far, 0 to 1. Zero unless charging.</summary>
        public float Charge => _state == SwingState.Charging ? _charge : 0f;

        /// <summary>True if the last completed swing was cancelled by hitting the environment.</summary>
        public bool LastSwingClipped { get; private set; }

        /// <summary>Replaces the input source. Mirrors <see cref="FirstPersonController.SetInput"/>.</summary>
        public void SetInput(IPlayerInput input)
        {
            _input = input;
        }

        private void Awake()
        {
            _input ??= new DeviceInput();
            controller ??= GetComponent<FirstPersonController>();

            if (weapon != null)
            {
                ApplyPose(0f);
            }
        }

        private void Update()
        {
            if (_input == null || weapon == null)
            {
                return;
            }

            switch (_state)
            {
                case SwingState.Ready:
                    if (_input.AttackHeld && !IsBlocked())
                    {
                        _state = SwingState.Charging;
                        _charge = 0f;
                    }

                    break;

                case SwingState.Charging:
                    _charge = Mathf.Clamp01(_charge + Time.deltaTime / Mathf.Max(0.01f, fullChargeTime));

                    if (IsBlocked())
                    {
                        _state = SwingState.Ready;
                        _charge = 0f;
                        break;
                    }

                    if (!_input.AttackHeld)
                    {
                        BeginSwing();
                    }

                    break;

                case SwingState.Recovering:
                    _recoveryRemaining -= Time.deltaTime;
                    if (_recoveryRemaining <= 0f)
                    {
                        _state = SwingState.Ready;
                    }

                    break;
            }

            UpdatePose();
        }

        private void FixedUpdate()
        {
            if (_state != SwingState.Swinging || weapon == null)
            {
                _hasPreviousBlade = false;
                return;
            }

            StepSwing(Time.fixedDeltaTime);
        }

        private bool IsBlocked()
        {
            return controller != null && controller.IsVaulting;
        }

        private void BeginSwing()
        {
            _committedCharge = Mathf.Max(minimumCharge, _charge);
            _swingDuration = Mathf.Lerp(swingTimeAtMinCharge, swingTimeAtFullCharge, _committedCharge);
            _swingElapsed = 0f;
            _charge = 0f;
            _hasPreviousBlade = false;
            LastSwingClipped = false;
            _state = SwingState.Swinging;
        }

        /// <summary>
        /// Advances the arc and traces the blade along it. The arc is sub-stepped: at full
        /// charge the tip covers more than a metre in a single physics step, which is wider
        /// than a doorframe, so testing only at the step boundaries would let the blade pass
        /// straight through the thing it is supposed to clip.
        /// </summary>
        private void StepSwing(float delta)
        {
            var steps = Mathf.Max(1, traceSubSteps);
            var startT = Mathf.Clamp01(_swingElapsed / _swingDuration);
            _swingElapsed += delta;
            var endT = Mathf.Clamp01(_swingElapsed / _swingDuration);

            for (var i = 1; i <= steps; i++)
            {
                var t = Mathf.Lerp(startT, endT, i / (float)steps);
                ApplySwingPose(t);

                var hilt = weapon.TransformPoint(bladeHilt);
                var tip = weapon.TransformPoint(bladeTip);

                if (_hasPreviousBlade && Trace(hilt, tip))
                {
                    return;
                }

                _previousHilt = hilt;
                _previousTip = tip;
                _hasPreviousBlade = true;
            }

            if (endT < 1f)
            {
                return;
            }

            EndSwing(recoveryAfterMiss);
        }

        /// <summary>
        /// Tests the blade where it is now. A target anywhere in the overlap wins over scenery,
        /// so a zombie standing in a doorway can be hit even though the doorframe is also in
        /// range: the player aimed at the zombie, and the frame is not what they meant.
        /// </summary>
        private bool Trace(Vector3 hilt, Vector3 tip)
        {
            var count = Physics.OverlapCapsuleNonAlloc(
                hilt, tip, bladeRadius, _overlapBuffer, hittableLayers, QueryTriggerInteraction.Ignore);

            if (count == 0)
            {
                return false;
            }

            Collider scenery = null;

            for (var i = 0; i < count; i++)
            {
                var collider = _overlapBuffer[i];
                if (collider == null || collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                var target = collider.GetComponentInParent<MeleeTarget>();
                if (target != null)
                {
                    var point = collider.ClosestPoint(tip);
                    target.ReceiveHit(_committedCharge, point);
                    controller?.AddViewPunch(cleanHitPunch * _committedCharge, -cleanHitPunch * 0.4f * _committedCharge);
                    EndSwing(recoveryAfterHit);
                    return true;
                }

                scenery ??= collider;
            }

            if (scenery == null)
            {
                return false;
            }

            // Clipped the world. Cancel hard: the swing is spent, the view is kicked the other
            // way, and the recovery is the longest in the class.
            LastSwingClipped = true;
            controller?.AddViewPunch(-clipPunch * _committedCharge, clipPunch * 0.6f * _committedCharge);
            EndSwing(recoveryAfterClip);
            return true;
        }

        private void EndSwing(float recovery)
        {
            _recoveryRemaining = recovery;
            _hasPreviousBlade = false;
            _state = SwingState.Recovering;
        }

        private void UpdatePose()
        {
            switch (_state)
            {
                case SwingState.Ready:
                    ApplyPose(0f);
                    break;

                case SwingState.Charging:
                    // Ease so the last of the wind-up is slow: the player can see the swing is
                    // nearly ready without a charge meter.
                    ApplyPose(Mathf.SmoothStep(0f, 1f, _charge));
                    break;

                case SwingState.Recovering:
                    var back = 1f - Mathf.Clamp01(_recoveryRemaining / Mathf.Max(0.01f, recoveryAfterClip));
                    weapon.localPosition = Vector3.Lerp(weapon.localPosition, restPosition, back * 0.35f);
                    weapon.localRotation = Quaternion.Slerp(
                        weapon.localRotation, Quaternion.Euler(restEuler), back * 0.35f);
                    break;
            }
        }

        /// <summary>Rest pose to fully charged, by <paramref name="t"/> from 0 to 1.</summary>
        private void ApplyPose(float t)
        {
            weapon.localPosition = Vector3.Lerp(restPosition, chargedPosition, t);
            weapon.localRotation = Quaternion.Slerp(
                Quaternion.Euler(restEuler), Quaternion.Euler(chargedEuler), t);
        }

        /// <summary>Charged pose through to follow-through, by <paramref name="t"/> from 0 to 1.</summary>
        private void ApplySwingPose(float t)
        {
            // Fast through the middle of the arc, slower at both ends: the blade should look
            // like it is being thrown rather than driven at a constant rate.
            var eased = t * t * (3f - 2f * t);
            weapon.localPosition = Vector3.Lerp(chargedPosition, followThroughPosition, eased);
            weapon.localRotation = Quaternion.Slerp(
                Quaternion.Euler(chargedEuler), Quaternion.Euler(followThroughEuler), eased);
        }

        private void OnDrawGizmosSelected()
        {
            if (weapon == null)
            {
                return;
            }

            Gizmos.color = _state == SwingState.Swinging ? Color.red : Color.yellow;
            var hilt = weapon.TransformPoint(bladeHilt);
            var tip = weapon.TransformPoint(bladeTip);
            Gizmos.DrawLine(hilt, tip);
            Gizmos.DrawWireSphere(tip, bladeRadius);
        }
    }
}
