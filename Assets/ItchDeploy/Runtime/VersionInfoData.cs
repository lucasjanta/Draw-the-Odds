using UnityEngine;

namespace ItchDeploy
{
    public sealed class VersionInfoData : ScriptableObject
    {
        public string version;
        public string platform;
        public string channel;
        public string buildDate;
        public string productName;
        public string unityVersion;
    }
}
