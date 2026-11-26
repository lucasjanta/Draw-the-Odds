using System;

namespace GamePix.User
{
    [Serializable]
    public class BackpackResult
    {
        public bool success;
        /// <summary>
        /// 200 when the Backpack opened, 401 for a guest, 404 when unavailable,
        /// or 408 when the Player did not answer in time.
        /// </summary>
        public int code;
        public string message;
    }
}
