using UnityEngine;

public class PlayerFallState : PlayerState
{
    public PlayerFallState(PlayerController player) : base(player) { }

    public override void Enter() => player.PlayAnimation("fall");

    public override void Tick()
    {
        if (player.TryStartDash()) return;

        // Coyote time: saiu da borda há pouquinho e apertou pulo → ainda pode
        if (player.JumpBuffered && player.CanUseCoyote)
        {
            player.ChangeState(player.JumpState);
            return;
        }

        if (player.IsGrounded)
        {
            bool moving = Mathf.Abs(player.MoveInput.x) > 0.01f;
            player.ChangeState(moving ? player.RunState : player.IdleState);
        }
    }
}
