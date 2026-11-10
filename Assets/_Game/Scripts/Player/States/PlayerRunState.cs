using UnityEngine;

public class PlayerRunState : PlayerGroundedState
{
    public PlayerRunState(PlayerController player) : base(player) { }

    public override void Enter() => player.PlayAnimation("run");

    public override void Tick()
    {
        if (CheckCommonTransitions()) return;

        if (Mathf.Abs(player.MoveInput.x) <= 0.01f)
            player.ChangeState(player.IdleState);
    }
}