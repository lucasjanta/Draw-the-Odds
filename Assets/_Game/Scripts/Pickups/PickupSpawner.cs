using System.Collections.Generic;
using UnityEngine;

// Faz surgir pacotes de carta (quando a mão está baixa) e orbes de poder (de tempos em tempos).
public class PickupSpawner : MonoBehaviour
{
    private GameManager gm;
    private LevelBuilder level;
    private bool running;
    private float powerTimer;
    private float cardTimer = -1f;

    // O tutorial liga/desliga os spawns automáticos.
    public bool PowersEnabled { get; set; } = true;
    public bool CardsEnabled { get; set; } = true;
    private readonly List<CardPickup> cardPickups = new List<CardPickup>();
    private readonly List<PowerPickup> powers = new List<PowerPickup>();

    public void Init(GameManager gm, LevelBuilder level)
    {
        this.gm = gm;
        this.level = level;
        nextIsAdd = Random.value < 0.5f; // começa com + ou − ao acaso
    }

    public void Begin()
    {
        running = true;
        cardTimer = -1f;
        powerTimer = NextPowerInterval() * 0.5f;
    }

    public void StopAndClear()
    {
        running = false;
        foreach (var c in cardPickups) if (c != null) Destroy(c.gameObject);
        foreach (var p in powers) if (p != null) Destroy(p.gameObject);
        cardPickups.Clear();
        powers.Clear();
    }

    private void Update()
    {
        if (!running || gm.State != GameManager.GameState.Playing) return;
        cardPickups.RemoveAll(c => c == null);
        powers.RemoveAll(p => p == null);
        var cfg = gm.Config;

        // Pacote de cartas: aparece sempre que a mão tem até 7 cartas (LowHandThreshold) e não tem
        // outro no mapa. Mão vazia = rápido; mão quase cheia = espera um pouco mais.
        if (CardsEnabled && gm.Hand.Count <= cfg.LowHandThreshold && cardPickups.Count == 0)
        {
            if (cardTimer < 0f)
                cardTimer = gm.Hand.Count == 0 ? 0.6f
                          : gm.Hand.Count <= 3 ? Random.Range(1.2f, 2.8f)
                          : Random.Range(2.5f, 4.5f);
            cardTimer -= Time.deltaTime;
            if (cardTimer <= 0f)
            {
                SpawnCardPack();
                cardTimer = -1f;
            }
        }

        if (!PowersEnabled) return;
        powerTimer -= Time.deltaTime;
        if (powerTimer <= 0f)
        {
            powerTimer = NextPowerInterval();
            if (powers.Count < cfg.MaxPowersOnMap + gm.CurrentDifficulty.ExtraPowersOnMap) SpawnPower(NextPowerType());
        }
    }

    // Intervalo até o próximo orbe (EASY: bem menor = mais orbes).
    private float NextPowerInterval()
    {
        var range = gm.Config.PowerSpawnInterval;
        return Random.Range(range.x, range.y) * gm.CurrentDifficulty.PowerIntervalMultiplier;
    }

    // Tipo do próximo poder: × é raro (chance por chefe, no GameConfig); fora isso + e − se
    // ALTERNAM (−, +, −, +...), então o player nunca espera muito pelo sinal que precisa.
    private bool nextIsAdd;

    private PowerType NextPowerType()
    {
        if (Random.value < gm.CurrentBossData.MultiplyChance) return PowerType.Multiply;
        PowerType type = nextIsAdd ? PowerType.Add : PowerType.Subtract;
        nextIsAdd = !nextIsAdd;
        return type;
    }

    public void SpawnCardPack()
    {
        var cfg = gm.Config;
        int count = Random.Range(cfg.MinPackSize, cfg.MaxPackSize + 1);
        bool specific = Random.value < cfg.SpecificSuitChance;
        Suit suit = cfg.RandomCardSuit();
        var cards = new List<Card>();
        for (int i = 0; i < count; i++) cards.Add(specific ? Card.RandomOfSuit(suit) : Card.Random());

        Vector2 pos = level.RandomSpawnPoint(gm.Player.transform.position, 6f);
        cardPickups.Add(CardPickup.Spawn(cfg, cards, specific, pos));
    }

    public void SpawnPower(PowerType type)
    {
        Vector2 pos = level.RandomSpawnPoint(gm.Player.transform.position, 7f, OccupiedPoints(), 3f);
        var life = gm.Config.PowerLifetime;
        powers.Add(PowerPickup.Spawn(type, pos, Random.Range(life.x, life.y)));
    }

    // Onde já tem orbe ou pacote (para o próximo não nascer em cima).
    private List<Vector2> OccupiedPoints()
    {
        var list = new List<Vector2>();
        foreach (var p in powers) if (p != null) list.Add(p.transform.position);
        foreach (var c in cardPickups) if (c != null) list.Add(c.transform.position);
        return list;
    }

    // lifetime 0 = o orbe não some sozinho (usado no tutorial).
    public void SpawnPowerAt(PowerType type, Vector2 position, float lifetime) => powers.Add(PowerPickup.Spawn(type, position, lifetime));

    // Remove só os orbes (o tutorial usa ao voltar uma dica).
    public void ClearPowers()
    {
        foreach (var p in powers) if (p != null) Destroy(p.gameObject);
        powers.Clear();
    }

    public bool HasPowerOnMap
    {
        get { powers.RemoveAll(p => p == null); return powers.Count > 0; }
    }
}
