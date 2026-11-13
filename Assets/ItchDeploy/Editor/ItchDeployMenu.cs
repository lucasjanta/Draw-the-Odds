using UnityEditor;

namespace ItchDeploy.Editor
{
    public static class ItchDeployMenu
    {
        [MenuItem("Tools/ItchDeploy/Open ItchDeploy %#d", false, 100)]
        public static void OpenWindow()
        {
            ItchDeployWindow.Open();
        }
    }
}
