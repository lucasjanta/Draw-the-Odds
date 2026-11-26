using System;
using GamePix.User;
using UnityEditor;
using UnityEngine;

namespace GamePix.Editor
{
    public class LoginWindow : EditorWindow
    {
        private static readonly Vector2 WindowSize = new Vector2(360, 470);

        private static Action<bool> pendingResult;

        private GUIStyle avatarStyle;
        private GUIStyle displayNameStyle;

        [InitializeOnLoadMethod]
        private static void RegisterLoginPrompt()
        {
            UnityUser.LoginPrompt = Prompt;
        }

        private static void Prompt(Action<bool> onResult)
        {
            pendingResult += onResult;
            LoginWindow window = GetWindow<LoginWindow>(true, "GamePix login", true);
            window.minSize = WindowSize;
            window.maxSize = WindowSize;
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnDestroy()
        {
            Resolve(false);
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                Close();
            }
        }

        private void OnGUI()
        {
            CreateStyles();
            HandleKeyboard();
            GamepixEditorStyles.DrawHeader(position.width, "GamePix", "Log in to save your progress");

            GUILayout.BeginVertical(GamepixEditorStyles.Content);
            DrawAccount();
            GUILayout.FlexibleSpace();
            EditorGUILayout.HelpBox(
                "Editor simulation of the GamePix Player. The token is fake and expires in 30 minutes.",
                MessageType.Info);
            GUILayout.Space(12);
            DrawButtons();
            GUILayout.EndVertical();
        }

        private void DrawAccount()
        {
            UserInfo user = UnityUser.TestUser;

            GUILayout.BeginHorizontal(GUILayout.Height(56));
            string initial = string.IsNullOrEmpty(user.name) ? "?" : user.name.Substring(0, 1).ToUpperInvariant();
            GUILayout.Label(initial, avatarStyle, GUILayout.Width(56), GUILayout.Height(56));
            GUILayout.Space(12);
            GUILayout.BeginVertical(GUILayout.Height(56));
            GUILayout.FlexibleSpace();
            GUILayout.Label(string.IsNullOrEmpty(user.name) ? "No name" : user.name, displayNameStyle);
            GUILayout.Label(user.guest ? "Guest account" : "@" + user.username, EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            GUILayout.Label("Test account", EditorStyles.boldLabel);
            user.name = EditorGUILayout.TextField("Name", user.name);
            user.nickname = EditorGUILayout.TextField("Nickname", user.nickname);
            user.username = EditorGUILayout.TextField("Username", user.username);
            user.id = EditorGUILayout.TextField("User ID", user.id);
            user.guest = EditorGUILayout.Toggle("Guest account", user.guest);
        }

        private void DrawButtons()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Cancel", GamepixEditorStyles.SecondaryButton, GUILayout.Height(34)))
            {
                Close();
            }
            GUILayout.Space(8);
            if (GUILayout.Button("Log in", GamepixEditorStyles.PrimaryButton, GUILayout.Height(34)))
            {
                AcceptAndClose();
            }
            GUILayout.EndHorizontal();
        }

        private void HandleKeyboard()
        {
            Event current = Event.current;
            if (current.type != EventType.KeyDown)
            {
                return;
            }

            if (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter)
            {
                current.Use();
                AcceptAndClose();
            }
            else if (current.keyCode == KeyCode.Escape)
            {
                current.Use();
                Close();
            }
        }

        // SAFETY: Close() triggers OnDestroy, which resolves any still-pending callbacks with false.
        private void AcceptAndClose()
        {
            Resolve(true);
            Close();
        }

        private static void Resolve(bool isAccepted)
        {
            Action<bool> callbacks = pendingResult;
            pendingResult = null;
            callbacks?.Invoke(isAccepted);
        }

        private void CreateStyles()
        {
            if (avatarStyle != null && avatarStyle.normal.background != null)
            {
                return;
            }

            avatarStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 24,
                alignment = TextAnchor.MiddleCenter,
            };
            avatarStyle.normal.background = GamepixEditorStyles.NavyTexture;
            avatarStyle.normal.textColor = GamepixEditorStyles.Green;

            displayNameStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15 };
        }
    }
}
