using UnityEngine;

public class PlayerIdleState : PlayerGroundedState
{
    public PlayerIdleState(PlayerController player) : base(player) { }

    public override void Enter() => player.PlayAnimation("idle");

    public override void Tick()
    {
        if (CheckCommonTransitions()) return;

        if (Mathf.Abs(player.MoveInput.x) > 0.01f)
            player.ChangeState(player.RunState);
    }
}