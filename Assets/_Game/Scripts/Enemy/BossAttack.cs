using System;
using UnityEngine;

public enum BossAttackType { Volley, Rain, Dash, Jokers }

// O que uma face do dado do chefe faz. Cada chefe tem 6 destes (faces 1..6), editáveis no
// Inspector do GameConfig: tipo do ataque, quantidade (dados, fichas, investidas ou coringas) e velocidade.
[Serializable]
public class BossAttack
{
    [SerializeField] private BossAttackType type = BossAttackType.Volley;
    [Tooltip("Volley = dados no leque · Rain = fichas caindo · Dash = investidas · Jokers = coringas (1 por andar, máx. 4)")]
    [SerializeField, Min(1)] private int count = 1;
    [Tooltip("Unidades por segundo (Dash: velocidade da investida).")]
    [SerializeField, Min(1f)] private float speed = 10f;

    public BossAttackType Type => type;
    public int Count => count;
    public float Speed => speed;

    public BossAttack() { }

    public BossAttack(BossAttackType type, int count, float speed)
    {
        this.type = type;
        this.count = count;
        this.speed = speed;
    }

    // Nome mostrado quando o dado para (ex.: "DICE x3").
    public string Label => type switch
    {
        BossAttackType.Volley => $"DICE x{count}",
        BossAttackType.Rain => $"CHIP RAIN x{count}",
        BossAttackType.Dash => count > 1 ? $"CHARGE x{count}" : "CHARGE",
        _ => $"JOKERS x{count}",
    };

    // Cor da antecipação (o chefe pisca com ela depois que o dado para).
    public Color Tint => type switch
    {
        BossAttackType.Volley => new Color(1f, 0.75f, 0.45f),
        BossAttackType.Rain => new Color(0.55f, 0.85f, 1f),
        BossAttackType.Jokers => new Color(0.8f, 0.55f, 1f),
        _ => new Color(1f, 0.45f, 0.45f),
    };
}
