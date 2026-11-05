using UnityEngine;

namespace AshenRealm.Input
{
    public enum PlayerButton
    {
        Attack,
        Block,
        Jump,
        Interact,
        Sprint,
        Ability1,
        Ability2,
        Potion
    }

    /// <summary>
    /// Device-agnostic gameplay input. Gameplay code reads only this interface, never touch UI or devices directly.
    /// "Consume" methods return a buffered press once and clear it.
    /// </summary>
    public interface IPlayerInput
    {
        /// <summary>Movement on the local XZ plane, magnitude 0..1.</summary>
        Vector2 Move { get; }
        bool SprintHeld { get; }
        bool BlockHeld { get; }
        bool AttackHeld { get; }

        /// <summary>Accumulated camera rotation in degrees (x = yaw, y = pitch up) since the last call.</summary>
        Vector2 ConsumeLook();
        bool ConsumeJump();
        bool ConsumeAttack();
        bool ConsumeInteract();
        /// <summary>Class ability press, slot 0 or 1.</summary>
        bool ConsumeAbility(int slot);
        bool ConsumePotion();

        /// <summary>Clears a latched sprint toggle (e.g. when stamina runs out).</summary>
        void CancelSprint();
    }
}
