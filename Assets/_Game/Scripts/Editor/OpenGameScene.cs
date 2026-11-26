using UnityEditor;
using UnityEditor.SceneManagement;

// Menu "Draw the Odds/Open Game Scene": abre a cena principal (a ponte do Claude usa quando o Editor
// abre numa cena vazia "Untitled").
public static class OpenGameScene
{
    [MenuItem("Draw the Odds/Open Game Scene")]
    public static void Open()
    {
        if (EditorApplication.isPlaying) return;
        EditorSceneManager.OpenScene("Assets/_Game/Scenes/Game.unity");
    }
}
