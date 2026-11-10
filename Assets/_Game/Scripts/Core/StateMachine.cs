// Classe C# "pura" (não herda de MonoBehaviour): não vai num GameObject,
// é criada com "new" por quem precisa dela. Serve para Player E chefes.

public class StateMachine
{
    // Propriedade: qualquer um lê, só esta classe altera.
    public IState Current { get; private set; }

    public void ChangeState(IState next)
    {
        if (next == null || next == Current) return;

        Current?.Exit();  // "?." = só chama se Current não for null (no 1º estado é null)
        Current = next;
        Current.Enter();
    }

    public void Tick() => Current?.Tick();  // "?." = só chama se Current não for null
    public void FixedTick() => Current?.FixedTick();  // "?." = só chama se Current não for null
}