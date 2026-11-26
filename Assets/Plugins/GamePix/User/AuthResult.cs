using System;

namespace GamePix.User
{
    [Serializable]
    public class AuthResult
    {
        public bool success;
        /// <summary>
        /// 200 authenticated, 401 guest or login cancelled, 404 not inside the GamePix Player,
        /// 408 the Player did not answer in time.
        /// </summary>
        public int code;
        public UserInfo user;
        /// <summary>
        /// Xsolla ID JWT token. It is a credential: never log it or save it to Storage,
        /// call GetUser every time you need it.
        /// </summary>
        public string token;
        /// <summary>
        /// Token expiry in milliseconds since epoch.
        /// </summary>
        public double expiresAt;
        public string message;
    }

    [Serializable]
    public class UserInfo
    {
        /// <summary>
        /// Unique and stable user identifier.
        /// </summary>
        public string id;
        public string name;
        public string nickname;
        public string username;
        public string avatar;
        public bool guest;
    }
}
