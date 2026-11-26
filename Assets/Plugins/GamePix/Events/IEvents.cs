namespace GamePix.Events
{
    public interface IEvents
    {
        /// <summary>
        /// Call this function every time there's a score update inside the Game.
        /// </summary>
        /// <param name="score">Score</param>
        void UpdateScore(int score); 

        /// <summary>
        /// Call this function on a positive moment: high score, achievement, boss defeat, big win.
        /// </summary>
        void HappyMoment();

        /// <summary>
        /// Call this function on a negative moment: death, failure, timeout.
        /// </summary>
        void SadMoment();
    }
}