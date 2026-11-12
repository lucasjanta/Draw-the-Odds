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
    private PowerType lastPower;
    private int samePowerStreak;

    // O tutorial liga/desliga os spawns automáticos.
    public bool PowersEnabled { get; set; } = true;
    public bool CardsEnabled { get; set; } = true;
    private readonly List<CardPickup> cardPickups = new List<CardPickup>();
    private readonly List<PowerPickup> powers = new List<PowerPickup>();

    public void Init(GameManager gm, LevelBuilder level)
    {
        this.gm = gm;
        this.level = level;
    }

    public void Begin()
    {
        running = true;
        cardTimer = -1f;
        powerTimer = Random.Range(gm.Config.PowerSpawnInterval.x, gm.Config.PowerSpawnInterval.y) * 0.5f;
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

        // Pacote de cartas: só quando a mão está baixa e não tem outro no mapa.
        if (CardsEnabled && gm.Hand.Count <= cfg.LowHandThreshold && cardPickups.Count == 0)
        {
            if (cardTimer < 0f) cardTimer = gm.Hand.Count == 0 ? 0.6f : Random.Range(1.2f, 2.8f);
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
            powerTimer = Random.Range(cfg.PowerSpawnInterval.x, cfg.PowerSpawnInterval.y);
            if (powers.Count < cfg.MaxPowersOnMap) SpawnPower(NextPowerType());
        }
    }

    // Sorteia o tipo do poder: × é raro (chance por chefe) e o mesmo tipo
    // nunca aparece mais de 2 vezes seguidas.
    private PowerType NextPowerType()
    {
        PowerType type = Random.value < gm.CurrentBossData.MultiplyChance
            ? PowerType.Multiply
            : (Random.value < 0.5f ? PowerType.Add : PowerType.Subtract);

        if (type == lastPower && samePowerStreak >= 2)
        {
            if (type == PowerType.Add) type = PowerType.Subtract;
            else if (type == PowerType.Subtract) type = PowerType.Add;
            else type = Random.value < 0.5f ? PowerType.Add : PowerType.Subtract;
        }

        samePowerStreak = type == lastPower ? samePowerStreak + 1 : 1;
        lastPower = type;
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
        Vector2 pos = level.RandomSpawnPoint(gm.Player.transform.position, 7f);
        var life = gm.Config.PowerLifetime;
        powers.Add(PowerPickup.Spawn(type, pos, Random.Range(life.x, life.y)));
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
