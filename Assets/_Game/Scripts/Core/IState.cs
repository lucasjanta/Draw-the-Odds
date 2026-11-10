// Interface = contrato. Todo estado PROMETE ter estes 4 métodos.
// Quem chama (StateMachine) não precisa saber qual estado concreto é.

public interface IState
{
    void Enter();      // 1x ao entrar no estado
    void Tick();       // todo frame (Update) — input, transições
    void FixedTick();  // todo passo de física (FixedUpdate) — mexer no Rigidbody
    void Exit();       // 1x ao sair do estado

}