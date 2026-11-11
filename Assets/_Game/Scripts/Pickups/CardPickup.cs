using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Pacote de cartas no mapa. Pode ter naipes aleatórios ou um naipe específico.
public class CardPickup : MonoBehaviour
{
    private List<Card> cards;
    private Vector3 basePos;
    private TextMeshPro label;
    private float age;

    public static CardPickup Spawn(GameConfig config, List<Card> cards, bool specificSuit, Vector2 position)
    {
        var go = new GameObject("CardPickup");
        go.transform.position = position;
        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(1.6f, 2f);

        var glow = Fx.MakeSprite("Glow", Fx.Circle, position, new Color(1f, 0.95f, 0.6f, 0.25f), 30, go.transform);
        glow.transform.localScale = Vector3.one * 2.2f;

        // Naipe específico → mostra a carta do naipe; aleatório → verso da carta.
        Sprite sprite = specificSuit ? config.Blank(cards[0].suit) : config.CardBack;
        var sr = Fx.MakeSprite("Card", sprite, position, Color.white, 31, go.transform);
        sr.transform.localScale = Vector3.one * 0.45f;
        // Um segundo "verso" atrás para parecer um pacote
        var back = Fx.MakeSprite("Card2", sprite, (Vector3)position + new Vector3(0.25f, 0.15f), new Color(0.75f, 0.75f, 0.75f), 30, go.transform);
        back.transform.localScale = Vector3.one * 0.45f;
        back.transform.localRotation = Quaternion.Euler(0f, 0f, -12f);

        var p = go.AddComponent<CardPickup>();
        p.cards = cards;
        p.basePos = position;
        p.label = Fx.WorldText("+" + cards.Count, position + Vector2.up * 1.6f, 5f,
            specificSuit ? SuitInfo.GetColor(cards[0].suit) : Color.white, 32, go.transform);
        Fx.Burst(position, Color.white, 8, 5f, 0.2f);
        return p;
    }

    private void Update()
    {
        age += Time.deltaTime;
        transform.position = basePos + Vector3.up * Mathf.Sin(age * 3f) * 0.2f;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController>() == null) return;
        var gm = GameManager.Instance;
        if (gm == null || !gm.CanAct) return;
        if (gm.Hand.IsFull)
        {
            Fx.Popup("HAND FULL", transform.position + Vector3.up * 2f, Color.white, 5f, 0.8f);
            return;
        }
        int added = gm.GiveCards(cards);
        Fx.Popup($"+{added} CARDS", transform.position + Vector3.up * 1.5f, new Color(1f, 0.95f, 0.6f), 6f, 1f);
        Fx.Burst(transform.position, new Color(1f, 0.95f, 0.6f), 12, 7f, 0.25f);
        Destroy(gameObject);
    }
}
