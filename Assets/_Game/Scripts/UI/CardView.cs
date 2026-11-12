using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Uma carta na UI: sprite ui_<naipe> (58×80, já tem os naipes nos cantos) + número por cima,
// em cima do naipe pequeno (canto superior esquerdo) e de cabeça para baixo no canto inferior direito.
// Anima suavemente até targetPosition/targetRotation/targetScale (tempo real).
public class CardView : MonoBehaviour
{
    public const float Width = 58f, Height = 80f; // = tamanho do sprite em pixels (escala 1 = pixel perfeito)
    private static readonly Color SelectedOutline = new Color32(0x3A, 0x8B, 0xFF, 0xFF);

    public RectTransform Rect { get; private set; }
    public Vector2 targetPosition;
    public float targetRotation;
    public float targetScale = 1f;
    public float targetAlpha = 1f;

    private GameConfig config;
    private CanvasGroup group;
    private Image outline, face;
    private TextMeshProUGUI number, cornerNumber, mystery, tagLabel;
    private Image tagBack;
    private bool animate;
    private float punch;
    private float scaleNow = 1f;

    public static CardView Create(Transform parent, GameConfig config, bool animate)
    {
        var rt = UIFactory.Rect("Card", parent);
        rt.sizeDelta = new Vector2(Width, Height);
        var v = rt.gameObject.AddComponent<CardView>();
        v.Rect = rt;
        v.config = config;
        v.animate = animate;
        v.group = rt.gameObject.AddComponent<CanvasGroup>();

        v.outline = UIFactory.Image("Outline", rt, SelectedOutline);
        v.outline.rectTransform.Stretch(-3f);
        v.outline.preserveAspect = false;
        v.outline.enabled = false;

        v.face = UIFactory.Image("Face", rt, Color.white);
        v.face.rectTransform.Stretch();

        // O naipe pequeno do sprite fica em x 3–7, y 18–23 (a partir do topo): o número vai logo acima dele.
        // Peso Regular: no Bold os números da Silkscreen ficavam embolados.
        v.number = UIFactory.Text("Number", rt, "", 16f, Color.black, TextAlignmentOptions.TopLeft);
        v.number.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(2f, -1f), new Vector2(30f, 17f));

        // Mesmo número no canto oposto, girado 180° (pivô no centro da caixa para girar no lugar).
        v.cornerNumber = UIFactory.Text("Corner", rt, "", 16f, Color.black, TextAlignmentOptions.TopLeft);
        v.cornerNumber.rectTransform.Place(new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-2f - 15f, 1f + 8.5f), new Vector2(30f, 17f));
        v.cornerNumber.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);

        v.mystery = UIFactory.Text("Mystery", rt, "", 15f, Color.white, TextAlignmentOptions.Bottom);
        v.mystery.fontStyle = FontStyles.Bold;
        v.mystery.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(Width, 14f));
        v.mystery.gameObject.SetActive(false);

        v.tagBack = UIFactory.Image("TagBack", rt, Color.white);
        v.tagBack.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 3f), new Vector2(16f, 14f));
        v.tagLabel = UIFactory.Text("Tag", v.tagBack.transform, "", 10f, Color.black, TextAlignmentOptions.Center);
        v.tagLabel.fontStyle = FontStyles.Bold;
        v.tagLabel.rectTransform.Stretch();
        v.tagBack.gameObject.SetActive(false);
        return v;
    }

    public void SetCard(Card card)
    {
        face.sprite = config.Blank(card.suit);
        face.color = Color.white;
        outline.sprite = face.sprite;
        Color c = SuitInfo.Ink(card.suit); // preto ♠♣ / vermelho ♥♦, igual à arte
        number.text = card.Label;
        number.color = c;
        cornerNumber.text = card.Label;
        cornerNumber.color = c;
        cornerNumber.gameObject.SetActive(true);
        mystery.gameObject.SetActive(false);
    }

    // Resultado com naipe sorteado: verso da carta + "♠/♥".
    public void SetMystery(int value, Suit a, Suit b)
    {
        face.sprite = config.CardBack;
        face.color = new Color(0.3f, 0.3f, 0.38f);   // verso escurecido para o texto aparecer
        outline.sprite = face.sprite;
        number.text = value.ToString();
        number.color = Color.white;
        cornerNumber.gameObject.SetActive(false);
        mystery.gameObject.SetActive(true);
        mystery.text = $"{SuitInfo.Colored(SuitInfo.Symbol(a), a)}<color=#FFFFFF>?</color>{SuitInfo.Colored(SuitInfo.Symbol(b), b)}";
    }

    public void SetSelected(bool selected) => outline.enabled = selected;

    public void SetTag(string text, Color color)
    {
        bool show = !string.IsNullOrEmpty(text);
        tagBack.gameObject.SetActive(show);
        if (!show) return;
        tagBack.color = color;
        tagLabel.text = text;
    }

    public void SetDimmed(bool dimmed) => face.color = dimmed ? new Color(0.55f, 0.55f, 0.6f) : Color.white;

    public void Snap()
    {
        Rect.anchoredPosition = targetPosition;
        Rect.localRotation = Quaternion.Euler(0f, 0f, targetRotation);
        scaleNow = targetScale;
        Rect.localScale = Vector3.one * targetScale;
        group.alpha = targetAlpha;
    }

    public void Punch() => punch = 0.3f;

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        punch = Mathf.MoveTowards(punch, 0f, dt * 1.5f);
        if (!animate)
        {
            Rect.localScale = Vector3.one * (targetScale + punch);
            return;
        }
        float k = 1f - Mathf.Exp(-16f * dt); // lerp independente de FPS
        Rect.anchoredPosition = Vector2.Lerp(Rect.anchoredPosition, targetPosition, k);
        float rot = Mathf.LerpAngle(Rect.localEulerAngles.z, targetRotation, k);
        Rect.localRotation = Quaternion.Euler(0f, 0f, rot);
        scaleNow = Mathf.Lerp(scaleNow, targetScale, k);
        Rect.localScale = Vector3.one * (scaleNow + punch);
        group.alpha = Mathf.Lerp(group.alpha, targetAlpha, k);
    }

    // Saída da mão (arremesso/descarte): sobe e some.
    public void FlyAwayAndDestroy(Vector2 velocity)
    {
        animate = false;
        StartCoroutine(FlyAway(velocity));
    }

    private IEnumerator FlyAway(Vector2 velocity)
    {
        for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
        {
            Rect.anchoredPosition += velocity * Time.unscaledDeltaTime;
            group.alpha = 1f - t / 0.25f;
            yield return null;
        }
        Destroy(gameObject);
    }
}
