#if !UNITY_EDITOR && UNITY_WEBGL
using System.Runtime.InteropServices;

namespace GamePix.Events
{
    public class GamepixEvents : IEvents
    {
        [DllImport("__Internal")]
        private static extern void gpxUpdateScore(int score); 

        public void UpdateScore(int score)
        {
           gpxUpdateScore(score);
        }

        [DllImport("__Internal")]
        private static extern void gpxHappyMoment();

        public void HappyMoment()
        {
           gpxHappyMoment();
        }

        [DllImport("__Internal")]
        private static extern void gpxSadMoment();

        public void SadMoment()
        {
           gpxSadMoment();
        }
    }
}
#endif