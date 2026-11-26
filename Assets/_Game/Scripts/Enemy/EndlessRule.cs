// "Regra da casa" do modo infinito: a partir da rodada 4, cada chefe vem com uma regra extra
// (sorteada, nunca igual à da rodada anterior). Aplicada em BossData.ForEndless.
public enum EndlessRule
{
    None,
    Shuffle,        // o número secreto troca sozinho a cada poucos segundos
    LoadedDice,     // dados perigosos e mais rápidos
    DoubleTrouble,  // muito mais ataques misturados/emendados
    WideOpen,       // número de 1 a 26, qualquer chefe
    ThickSkin,      // +30% de vida
}
