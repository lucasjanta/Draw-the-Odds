using TMPro;
using UnityEngine;

// Traduz um texto de UI sozinho (UIFactory.Text coloca em todos).
// O código escreve em inglês; aqui guardamos esse original e mostramos Loc.T(original).
// Se o idioma mudar, o texto é retraduzido a partir do original.
[DisallowMultipleComponent]
public class LocText : MonoBehaviour
{
    private TextMeshProUGUI tmp;
    private string source, output;
    private int version = -1;

    private void Awake() => tmp = GetComponent<TextMeshProUGUI>();

    private void OnEnable() => Refresh();

    private void LateUpdate() => Refresh();

    private void Refresh()
    {
        if (tmp == null) return;
        // O código trocou o texto (está diferente do que nós colocamos): esse é o novo original.
        if (tmp.text != output) { source = tmp.text; version = -1; }
        if (version == Loc.Version) return;
        version = Loc.Version;
        output = Loc.T(source);
        if (tmp.text != output) tmp.text = output;
    }
}
