#if !UNITY_EDITOR && UNITY_WEBGL
using System;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;

namespace GamePix.User
{
    public class GamepixUser : IUser
    {
        [DllImport("__Internal")]
        private static extern void gpxGetUser(
            [MarshalAs(UnmanagedType.FunctionPtr)] Gpx.gpxCallbackString onResult);

        [DllImport("__Internal")]
        private static extern void gpxRequestLogin(
            [MarshalAs(UnmanagedType.FunctionPtr)] Gpx.gpxCallbackString onResult);

        [DllImport("__Internal")]
        private static extern void gpxShowBackpack(
            [MarshalAs(UnmanagedType.FunctionPtr)] Gpx.gpxCallbackString onResult);

        [DllImport("__Internal")]
        private static extern void gpxSetAuthChangedCallback(
            [MarshalAs(UnmanagedType.FunctionPtr)] Gpx.gpxCallbackString onAuthChanged);

        private static Action<AuthResult> getUserCallbacks;
        private static Action<AuthResult> requestLoginCallbacks;
        private static Action<BackpackResult> showBackpackCallbacks;
        private static Action<AuthResult> authChangedCallbacks;

        public event Action<AuthResult> OnAuthChanged
        {
            add
            {
                authChangedCallbacks += value;
                gpxSetAuthChangedCallback(AuthChanged);
            }
            remove
            {
                authChangedCallbacks -= value;
            }
        }

        public void GetUser(Action<AuthResult> onResult)
        {
            bool isPending = getUserCallbacks != null;
            getUserCallbacks += onResult;
            if (!isPending)
            {
                gpxGetUser(GetUserDone);
            }
        }

        public void RequestLogin(Action<AuthResult> onResult)
        {
            bool isPending = requestLoginCallbacks != null;
            requestLoginCallbacks += onResult;
            if (!isPending)
            {
                gpxRequestLogin(RequestLoginDone);
            }
        }

        public void ShowBackpack(Action<BackpackResult> onResult)
        {
            bool isPending = showBackpackCallbacks != null;
            showBackpackCallbacks += onResult;
            if (!isPending)
            {
                gpxShowBackpack(ShowBackpackDone);
            }
        }

        [MonoPInvokeCallback(typeof(Gpx.gpxCallbackString))]
        private static void GetUserDone(string authJson)
        {
            Action<AuthResult> callbacks = getUserCallbacks;
            getUserCallbacks = null;
            callbacks?.Invoke(JsonUtility.FromJson<AuthResult>(authJson));
        }

        [MonoPInvokeCallback(typeof(Gpx.gpxCallbackString))]
        private static void RequestLoginDone(string authJson)
        {
            Action<AuthResult> callbacks = requestLoginCallbacks;
            requestLoginCallbacks = null;
            callbacks?.Invoke(JsonUtility.FromJson<AuthResult>(authJson));
        }

        [MonoPInvokeCallback(typeof(Gpx.gpxCallbackString))]
        private static void ShowBackpackDone(string resultJson)
        {
            Action<BackpackResult> callbacks = showBackpackCallbacks;
            showBackpackCallbacks = null;
            callbacks?.Invoke(JsonUtility.FromJson<BackpackResult>(resultJson));
        }

        [MonoPInvokeCallback(typeof(Gpx.gpxCallbackString))]
        private static void AuthChanged(string authJson)
        {
            authChangedCallbacks?.Invoke(JsonUtility.FromJson<AuthResult>(authJson));
        }
    }
}

#endif