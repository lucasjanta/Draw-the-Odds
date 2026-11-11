using UnityEngine;

// Dash: avança rápido na horizontal, sem gravidade, invencível, deixando um rastro.
public class PlayerDashState : PlayerState
{
    private float timer;
    private float afterImageTimer;

    public PlayerDashState(PlayerController player) : base(player) { }

    public override void Enter()
    {
        timer = 0f;
        afterImageTimer = 0f;
        player.BeginDash();
    }

    public override void Tick()
    {
        timer += Time.deltaTime;
        afterImageTimer -= Time.deltaTime;
        if (afterImageTimer <= 0f)
        {
            player.SpawnAfterImage();
            afterImageTimer = 0.03f;
        }

        if (timer >= player.DashTime)
        {
            if (!player.IsGrounded) player.ChangeState(player.FallState);
            else player.ChangeState(Mathf.Abs(player.MoveInput.x) > 0.01f ? player.RunState : player.IdleState);
        }
    }

    // Não chama ApplyHorizontalMovement: a velocidade do dash fica fixa.
    public override void FixedTick() { }

    public override void Exit() => player.EndDash();
}
