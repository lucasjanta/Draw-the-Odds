using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Input das cartas: scroll escolhe, clique esquerdo arremessa, R troca a mão,
// e durante a fusão: Q / E põem a carta nos slots, botão esquerdo confirma e o direito pula o poder.
[RequireComponent(typeof(PlayerController))]
public class PlayerCards : MonoBehaviour
{
    [SerializeField] private Vector2 throwOffset = new Vector2(0f, 1.1f);
    [SerializeField] private float scrollRepeatDelay = 0.06f;

    private PlayerController controller;
    private float nextThrowTime;
    private float scrollReadyTime;

    private void Awake() => controller = GetComponent<PlayerController>();

    private void Update()
    {
        var gm = GameManager.Instance;
        var mouse = Mouse.current;
        var keyboard = Keyboard.current;
        if (gm == null || mouse == null || keyboard == null || !gm.CanAct) return;
        if (gm.Fusion.Resolving) return; // animação do merge: nada de input

        HandleSelection(gm, mouse, keyboard);

        if (gm.Fusion.Active)
        {
            if (keyboard.qKey.wasPressedThisFrame) gm.Fusion.PutInSlot(true);
            if (keyboard.eKey.wasPressedThisFrame) gm.Fusion.PutInSlot(false);
            if (mouse.leftButton.wasPressedThisFrame) gm.Fusion.TryConfirm();
            else if (mouse.rightButton.wasPressedThisFrame) gm.Fusion.Skip();
            return; // sem arremessar durante a fusão
        }

        // Clique em cima de um botão da UI (ex.: seta do tutorial) não arremessa.
        bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (mouse.leftButton.wasPressedThisFrame && !overUi) TryThrow(gm, mouse);
        if (keyboard.rKey.wasPressedThisFrame) gm.Redraw();
    }

    private void HandleSelection(GameManager gm, Mouse mouse, Keyboard keyboard)
    {
        // Scroll: para baixo = próxima carta (direita), para cima = anterior.
        int before = gm.Hand.SelectedIndex;
        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f && Time.unscaledTime >= scrollReadyTime)
        {
            gm.Hand.MoveSelection(scroll < 0f ? 1 : -1);
            scrollReadyTime = Time.unscaledTime + scrollRepeatDelay;
        }

        // Atalho: teclas 1..9 e 0 escolhem direto.
        for (int i = 0; i < 10; i++)
        {
            Key key = i == 9 ? Key.Digit0 : Key.Digit1 + i;
            if (keyboard[key].wasPressedThisFrame && i < gm.Hand.Count) gm.Hand.Select(i);
        }
        if (gm.Hand.SelectedIndex != before) AudioManager.Ui(gm.Config.SfxCardSelect, 0.7f);
    }

    private void TryThrow(GameManager gm, Mouse mouse)
    {
        if (Time.time < nextThrowTime || gm.Hand.IsEmpty) return;
        nextThrowTime = Time.time + gm.Config.ThrowCooldown;

        Card card = gm.Hand.RemoveAt(gm.Hand.SelectedIndex);
        Vector2 origin = (Vector2)transform.position + throwOffset;
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(mouse.position.ReadValue());
        Vector2 dir = (Vector2)mouseWorld - origin;
        if (dir.sqrMagnitude < 0.01f) dir = Vector2.up;

        CardProjectile.Spawn(gm.Config, card, origin, dir, gm.Boss != null ? gm.Boss.transform : null);
        controller.FaceTowards(dir.x, 0.3f);
        controller.PlayOneShot("attack1", 0.25f);
        gm.Stats.cardsThrown++;
        AudioManager.Sfx(gm.Config.SfxThrow);
    }
}
