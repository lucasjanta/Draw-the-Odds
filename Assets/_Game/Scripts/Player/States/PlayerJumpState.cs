public class PlayerJumpState : PlayerState
{
    private bool jumpCut; // já cortamos o pulo nesta subida?

    public PlayerJumpState(PlayerController player) : base(player) { }

    public override void Enter()
    {
        jumpCut = false;
        player.DoJump();
        player.PlayAnimation("jump");
    }

    public override void Tick()
    {
        // Soltou o botão ainda subindo → corta a velocidade (pulo variável)
        if (!jumpCut && !player.JumpHeld)
        {
            player.CutJump();
            jumpCut = true;
        }

        // Começou a descer → estado de queda
        if (player.Body.linearVelocity.y <= 0f)
            player.ChangeState(player.FallState);
    }
}