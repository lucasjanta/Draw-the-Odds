// Base para estados no chão (Idle e Run): as transições comuns ficam num lugar só.
public abstract class PlayerGroundedState : PlayerState
{
    protected PlayerGroundedState(PlayerController player) : base(player) { }

    // Retorna true se já trocou de estado (aí a filha para de processar).
    protected bool CheckCommonTransitions()
    {
        if (player.TryDropThrough()) { player.ChangeState(player.FallState); return true; }
        if (player.JumpBuffered) { player.ChangeState(player.JumpState); return true; }
        if (!player.IsGrounded) { player.ChangeState(player.FallState); return true; }
        return false;
    }
}