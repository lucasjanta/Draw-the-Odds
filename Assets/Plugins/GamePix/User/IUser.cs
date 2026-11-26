using System;

namespace GamePix.User
{
    public interface IUser
    {
        /// <summary>
        /// Returns the current GamePix user and Xsolla ID token without showing any UI.
        /// A guest gets code 401. Safe to call at startup.
        /// </summary>
        /// <param name="onResult">Called with the authentication result</param>
        void GetUser(Action<AuthResult> onResult);

        /// <summary>
        /// Opens the GamePix login window. Call it only from a click or tap handler,
        /// browsers block windows not opened by a user action.
        /// Code 200 when the player logged in, 401 when the login was cancelled.
        /// </summary>
        /// <param name="onResult">Called with the authentication result</param>
        void RequestLogin(Action<AuthResult> onResult);

        /// <summary>
        /// Opens the player's Xsolla Backpack. Call it from a player action.
        /// It does not show the login UI; a guest receives code 401.
        /// </summary>
        /// <param name="onResult">Called when the Backpack opens or cannot be opened</param>
        void ShowBackpack(Action<BackpackResult> onResult);

        /// <summary>
        /// Fires when the token was renewed or the player logged in/out outside of
        /// GetUser/RequestLogin calls.
        /// </summary>
        event Action<AuthResult> OnAuthChanged;
    }
}
