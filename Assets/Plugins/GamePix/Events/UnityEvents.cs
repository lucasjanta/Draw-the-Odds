#if UNITY_EDITOR || !UNITY_WEBGL
namespace GamePix.Events
{
    public class UnityEvents: IEvents
    {
        public void UpdateScore(int score)
        {
           Gpx.Log(string.Format("[Gpx] Update score: {0}", score));
        }

        public void HappyMoment()
        {
           Gpx.Log("[Gpx] Happy moment");
        }

        public void SadMoment()
        {
           Gpx.Log("[Gpx] Sad moment");
        }
    }
}
#endif