using UnityEngine;

namespace ItchDeploy
{
    public static class ItchDeployVersion
    {
        public const string Version = "1.0.0";
        public const string Name = "ItchDeploy";
        public const string Author = "Andrii Sviatenko";

        public static void Log()
        {
            Debug.Log($"{Name} v{Version} - Unity -> itch.io deployment tool");
        }
    }
}
