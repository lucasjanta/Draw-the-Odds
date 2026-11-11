using TMPro;
using UnityEngine;

// Faz um texto piscar suavemente (ex.: "CLICK TO START").
public class Blink : MonoBehaviour
{
    [SerializeField] private float speed = 3f;
    private TMP_Text text;

    private void Awake() => text = GetComponent<TMP_Text>();

    private void Update() => text.alpha = 0.35f + 0.65f * Mathf.PingPong(Time.unscaledTime * speed, 1f);
}
