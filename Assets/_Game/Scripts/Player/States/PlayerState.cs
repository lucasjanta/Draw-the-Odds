// Base abstrata de todos os estados do player.
// "abstract" = não dá para fazer new PlayerState(); só das classes filhas.
public abstract class PlayerState : IState
{
    // protected = visível aqui e nas classes filhas. readonly = só atribuído no construtor.
    protected readonly PlayerController player;

    // Construtor: roda no "new". As filhas repassam o player com ": base(player)".
    protected PlayerState(PlayerController player)
    {
        this.player = player;
    }

    // virtual = implementação padrão que as filhas PODEM sobrescrever (override).
    public virtual void Enter() { }
    public virtual void Tick() { }
    public virtual void FixedTick() => player.ApplyHorizontalMovement(); // todos andam por padrão
    public virtual void Exit() { }
}