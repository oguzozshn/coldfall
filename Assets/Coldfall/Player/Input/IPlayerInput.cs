using UnityEngine;

namespace Coldfall.Player.Input
{
    /// <summary>
    /// Everything the character controller is allowed to know about the player's intent.
    /// <para>
    /// The controller reads this interface rather than any input API directly, for two reasons.
    /// It keeps the controller testable without a device, and it means the eventual move to a
    /// rebindable <c>InputActionAsset</c> changes exactly one file instead of the controller.
    /// </para>
    /// </summary>
    public interface IPlayerInput
    {
        /// <summary>Desired movement on the ground plane, x = strafe, y = forward. Length clamped to 1.</summary>
        Vector2 Move { get; }

        /// <summary>Look delta for this frame in degrees, x = yaw, y = pitch.</summary>
        Vector2 Look { get; }

        /// <summary>True while the sprint key is held.</summary>
        bool SprintHeld { get; }

        /// <summary>True while the crouch key is held. Hold-to-crouch, not a toggle.</summary>
        bool CrouchHeld { get; }

        /// <summary>True on the frame the vault key was pressed.</summary>
        bool VaultPressed { get; }

        /// <summary>
        /// True while the attack button is held. Melee is charge-and-release, so the hold is the
        /// input and the release is the commitment; there is no "attack pressed" event.
        /// </summary>
        bool AttackHeld { get; }

        /// <summary>True while the shove button is held. Shove is a tap, not a charge.</summary>
        bool ShovePressed { get; }

        /// <summary>
        /// True on the frame the player asked for the mouse cursor back. Not a game action: it
        /// exists so the editor stays usable while the view has the cursor captured.
        /// </summary>
        bool ReleaseCursorPressed { get; }
    }
}
