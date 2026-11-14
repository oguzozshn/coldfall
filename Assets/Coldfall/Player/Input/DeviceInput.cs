using UnityEngine;
using UnityEngine.InputSystem;

namespace Coldfall.Player.Input
{
    /// <summary>
    /// Reads keyboard and mouse directly through the Input System's device API.
    /// <para>
    /// This is deliberately the simplest thing that works, because M0 exists to answer whether
    /// moving feels good, not to ship a bindings UI. It polls devices instead of using an
    /// <c>InputActionAsset</c>, which means no rebinding and no gamepad; both arrive later behind
    /// <see cref="IPlayerInput"/> without the controller noticing.
    /// </para>
    /// </summary>
    public sealed class DeviceInput : IPlayerInput
    {
        private readonly float _mouseDegreesPerCount;

        /// <param name="mouseDegreesPerCount">
        /// Degrees of rotation per unit of mouse delta. Mouse delta is not a time-based quantity,
        /// so it is never multiplied by a frame delta: doing so makes sensitivity depend on frame
        /// rate, which is the most common first-person camera bug.
        /// </param>
        public DeviceInput(float mouseDegreesPerCount = 0.08f)
        {
            _mouseDegreesPerCount = mouseDegreesPerCount;
        }

        public Vector2 Move
        {
            get
            {
                var keyboard = Keyboard.current;
                if (keyboard == null)
                {
                    return Vector2.zero;
                }

                var move = new Vector2(
                    Axis(keyboard.dKey.isPressed, keyboard.aKey.isPressed),
                    Axis(keyboard.wKey.isPressed, keyboard.sKey.isPressed));

                // Clamp rather than normalize: a partially pressed diagonal should not become a
                // full-speed input, and holding one key must stay at exactly 1.
                return move.sqrMagnitude > 1f ? move.normalized : move;
            }
        }

        public Vector2 Look
        {
            get
            {
                var mouse = Mouse.current;
                if (mouse == null)
                {
                    return Vector2.zero;
                }

                return mouse.delta.ReadValue() * _mouseDegreesPerCount;
            }
        }

        public bool SprintHeld => Keyboard.current?.leftShiftKey.isPressed ?? false;

        public bool CrouchHeld => Keyboard.current?.leftCtrlKey.isPressed ?? false;

        public bool VaultPressed => Keyboard.current?.spaceKey.wasPressedThisFrame ?? false;

        public bool AttackHeld => Mouse.current?.leftButton.isPressed ?? false;

        public bool ShovePressed => Mouse.current?.rightButton.wasPressedThisFrame ?? false;

        public bool ReleaseCursorPressed => Keyboard.current?.escapeKey.wasPressedThisFrame ?? false;

        private static float Axis(bool positive, bool negative)
        {
            if (positive == negative)
            {
                return 0f;
            }

            return positive ? 1f : -1f;
        }
    }
}
