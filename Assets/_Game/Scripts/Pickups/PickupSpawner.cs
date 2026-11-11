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
        if (gm.Hand.Count <= cfg.LowHandThreshold && cardPickups.Count == 0)
        {
            if (cardTimer < 0f) cardTimer = gm.Hand.Count == 0 ? 0.6f : Random.Range(1.2f, 2.8f);
            cardTimer -= Time.deltaTime;
            if (cardTimer <= 0f)
            {
                SpawnCardPack();
                cardTimer = -1f;
            }
        }

        powerTimer -= Time.deltaTime;
        if (powerTimer <= 0f)
        {
            powerTimer = Random.Range(cfg.PowerSpawnInterval.x, cfg.PowerSpawnInterval.y);
            if (powers.Count < cfg.MaxPowersOnMap) SpawnPower((PowerType)Random.Range(0, 3));
        }
    }

    public void SpawnCardPack()
    {
        var cfg = gm.Config;
        int count = Random.Range(cfg.MinPackSize, cfg.MaxPackSize + 1);
        bool specific = Random.value < cfg.SpecificSuitChance;
        Suit suit = SuitInfo.Random();
        var cards = new List<Card>();
        for (int i = 0; i < count; i++) cards.Add(specific ? Card.RandomOfSuit(suit) : Card.Random());

        Vector2 pos = level.RandomSpawnPoint(gm.Player.transform.position, 6f);
        cardPickups.Add(CardPickup.Spawn(cfg, cards, specific, pos));
    }

    public void SpawnPower(PowerType type)
    {
        Vector2 pos = level.RandomSpawnPoint(gm.Player.transform.position, 7f);
        powers.Add(PowerPickup.Spawn(type, pos));
    }
}
