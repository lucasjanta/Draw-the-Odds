#if UNITY_EDITOR || !UNITY_WEBGL
using System;
using UnityEngine;

namespace GamePix.User
{
    public class UnityUser : IUser
    {
        private const double TokenLifetimeMs = 30 * 60 * 1000;

        public static bool IsLoggedIn = false;

        public static UserInfo TestUser = new UserInfo
        {
            id = "editor-test-user",
            name = "Editor Test User",
            nickname = "editor",
            username = "editor_test_user",
            avatar = "",
            guest = false,
        };

        /// <summary>
        /// Shows the login UI and reports whether the player logged in.
        /// Set by the Editor; when null, RequestLogin logs in immediately.
        /// </summary>
        public static Action<Action<bool>> LoginPrompt;

        public event Action<AuthResult> OnAuthChanged;

        public void GetUser(Action<AuthResult> onResult)
        {
            Gpx.Log("[Gpx] GetUser");
            onResult(CurrentAuth());
        }

        public void RequestLogin(Action<AuthResult> onResult)
        {
            Gpx.Log("[Gpx] RequestLogin");
            if ((IsLoggedIn && !TestUser.guest) || LoginPrompt == null)
            {
                IsLoggedIn = true;
                onResult(CurrentAuth());
                return;
            }

            LoginPrompt(isAccepted =>
            {
                IsLoggedIn = isAccepted;
                onResult(CurrentAuth());
            });
        }

        public void ShowBackpack(Action<BackpackResult> onResult)
        {
            Gpx.Log("[Gpx] ShowBackpack");
            onResult(new BackpackResult { success = false, code = 404, message = "Backpack is not available outside the GamePix Player" });
        }

        public void SimulateLogin()
        {
            IsLoggedIn = true;
            OnAuthChanged?.Invoke(CurrentAuth());
        }

        public void SimulateLogout()
        {
            IsLoggedIn = false;
            OnAuthChanged?.Invoke(CurrentAuth());
        }

        public void SimulateTokenRenewal()
        {
            OnAuthChanged?.Invoke(CurrentAuth());
        }

        private static AuthResult CurrentAuth()
        {
            if (!IsLoggedIn)
            {
                return new AuthResult { success = false, code = 401, user = new UserInfo(), token = "", message = "" };
            }

            UserInfo user = JsonUtility.FromJson<UserInfo>(JsonUtility.ToJson(TestUser));
            if (user.guest)
            {
                return new AuthResult { success = false, code = 401, user = user, token = "", message = "" };
            }

            return new AuthResult
            {
                success = true,
                code = 200,
                user = user,
                token = "editor-fake-token-" + Guid.NewGuid().ToString("N"),
                expiresAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + TokenLifetimeMs,
                message = "",
            };
        }
    }
}

#endif