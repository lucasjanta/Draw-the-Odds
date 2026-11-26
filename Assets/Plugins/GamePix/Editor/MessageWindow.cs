using System;
using UnityEditor;
using UnityEngine;

namespace GamePix.Editor
{
    public enum MessageStatus
    {
        Success,
        Error,
        Info,
    }

    public class MessageWindow : EditorWindow
    {
        private const float WindowWidth = 380;
        private const int Padding = 20;
        private const float IconSize = 28;
        private const int IconTextureSize = 64;
        private const float IconStroke = 0.09f;

        private static readonly Color Background = new Color32(0x3C, 0x3C, 0x3C, 0xFF);
        private static readonly Color HeadingColor = Color.white;
        private static readonly Color MessageColor = new Color32(0xB4, 0xB4, 0xB4, 0xFF);
        private static readonly Color SuccessColor = new Color32(0x22, 0xC5, 0x5E, 0xFF);
        private static readonly Color ErrorColor = new Color32(0xE5, 0x48, 0x4D, 0xFF);

        private static Texture2D successIcon;
        private static Texture2D errorIcon;
        private static Texture2D infoIcon;

        // SAFETY: fields are intentionally not serialized. After a domain reload Unity restores the window
        // with stale data from the previous code version, and the onOk callback is lost anyway,
        // so a restored window has no heading and closes itself.
        private string titleText;
        private MessageStatus status;
        private string heading;
        private string message;
        private string detail;
        private string okText;
        private string cancelText;
        private Action onOk;

        private GUIStyle contentStyle;
        private GUIStyle headingStyle;
        private GUIStyle messageStyle;
        private GUIStyle detailStyle;

        /// <param name="detail">Secondary line under the message, hidden when null or empty</param>
        /// <param name="cancelText">When null, only the OK button is shown</param>
        public static void Open(string title, MessageStatus status, string heading, string message,
                                string detail = null, string okText = "Close", Action onOk = null,
                                string cancelText = null)
        {
            // SAFETY: an Editor window can't be shown in batch mode; callers log the message to the console.
            if (Application.isBatchMode)
            {
                return;
            }
            MessageWindow window = GetWindow<MessageWindow>(true, "GamePix", true);
            window.titleText = title;
            window.status = status;
            window.heading = heading;
            window.message = message;
            window.detail = detail;
            window.okText = okText;
            window.cancelText = cancelText;
            window.onOk = onOk;
            window.minSize = new Vector2(WindowWidth, 200);
            window.maxSize = window.minSize;
        }

        private void OnGUI()
        {
            if (heading == null)
            {
                Close();
                return;
            }
            CreateStyles();
            HandleKeyboard();
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), Background);

            Rect windowRect = EditorGUILayout.BeginVertical();
            GamepixEditorStyles.DrawLineHeader(position.width, titleText);
            GUILayout.BeginVertical(contentStyle);
            DrawMessage();
            GUILayout.Space(20);
            DrawButtons();
            GUILayout.EndVertical();
            EditorGUILayout.EndVertical();

            FitHeight(windowRect);
        }

        private void DrawMessage()
        {
            GUILayout.BeginHorizontal();
            Rect iconRect = GUILayoutUtility.GetRect(IconSize, IconSize, GUILayout.Width(IconSize),
                                                     GUILayout.Height(IconSize));
            GUI.DrawTexture(iconRect, StatusIcon(), ScaleMode.ScaleToFit);
            GUILayout.Space(14);
            GUILayout.BeginVertical();
            GUILayout.Label(heading, headingStyle);
            GUILayout.Space(4);
            GUILayout.Label(message, messageStyle);
            if (!string.IsNullOrEmpty(detail))
            {
                GUILayout.Label(detail, detailStyle);
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private Texture StatusIcon()
        {
            switch (status)
            {
                case MessageStatus.Success:
                    return GetIcon(ref successIcon, SuccessColor, point => SegmentDistance(point,
                        new Vector2(0.28f, 0.52f), new Vector2(0.44f, 0.36f), new Vector2(0.72f, 0.64f)));
                case MessageStatus.Error:
                    return GetIcon(ref errorIcon, ErrorColor, point => Mathf.Min(
                        SegmentDistance(point, new Vector2(0.35f, 0.35f), new Vector2(0.65f, 0.65f)),
                        SegmentDistance(point, new Vector2(0.35f, 0.65f), new Vector2(0.65f, 0.35f))));
                default:
                    return GetIcon(ref infoIcon, GamepixEditorStyles.Blue, point => Mathf.Min(
                        SegmentDistance(point, new Vector2(0.5f, 0.27f), new Vector2(0.5f, 0.52f)),
                        Vector2.Distance(point, new Vector2(0.5f, 0.69f)) - 0.015f));
            }
        }

        /// <summary>
        /// Draws a filled circle with a white glyph. Coordinates are 0..1 with Y up.
        /// </summary>
        /// <param name="glyphDistance">Distance from a point to the glyph center line</param>
        private static Texture2D GetIcon(ref Texture2D icon, Color color, Func<Vector2, float> glyphDistance)
        {
            if (icon != null)
            {
                return icon;
            }

            icon = new Texture2D(IconTextureSize, IconTextureSize, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            Color[] pixels = new Color[IconTextureSize * IconTextureSize];
            Vector2 center = new Vector2(0.5f, 0.5f);
            for (int y = 0; y < IconTextureSize; y++)
            {
                for (int x = 0; x < IconTextureSize; x++)
                {
                    Vector2 point = new Vector2(x + 0.5f, y + 0.5f) / IconTextureSize;
                    // PERF: distance-based coverage gives 1px anti-aliasing without supersampling.
                    float circle = Mathf.Clamp01((0.5f - Vector2.Distance(point, center)) * IconTextureSize + 0.5f);
                    float glyph = Mathf.Clamp01((IconStroke / 2 - glyphDistance(point)) * IconTextureSize + 0.5f);
                    Color pixel = Color.Lerp(color, Color.white, glyph);
                    pixel.a = circle;
                    pixels[y * IconTextureSize + x] = pixel;
                }
            }
            icon.SetPixels(pixels);
            icon.Apply();
            return icon;
        }

        private static float SegmentDistance(Vector2 point, params Vector2[] polyline)
        {
            float distance = float.MaxValue;
            for (int index = 1; index < polyline.Length; index++)
            {
                Vector2 start = polyline[index - 1];
                Vector2 segment = polyline[index] - start;
                float projection = Mathf.Clamp01(Vector2.Dot(point - start, segment) / segment.sqrMagnitude);
                distance = Mathf.Min(distance, Vector2.Distance(point, start + segment * projection));
            }
            return distance;
        }

        private void DrawButtons()
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (!string.IsNullOrEmpty(cancelText))
            {
                if (GUILayout.Button(cancelText, GamepixEditorStyles.SecondaryButton, GUILayout.Width(110),
                                     GUILayout.Height(32)))
                {
                    Close();
                }
                GUILayout.Space(8);
            }
            if (GUILayout.Button(okText, GamepixEditorStyles.PrimaryButton, GUILayout.Width(110), GUILayout.Height(32)))
            {
                AcceptAndClose();
            }
            GUILayout.EndHorizontal();
        }

        // IMGUI knows the content height only after layout, so the window is resized on the next repaint.
        private void FitHeight(Rect windowRect)
        {
            if (Event.current.type != EventType.Repaint || Mathf.Approximately(windowRect.height, position.height))
            {
                return;
            }
            minSize = new Vector2(WindowWidth, windowRect.height);
            maxSize = minSize;
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

        private void AcceptAndClose()
        {
            Action callback = onOk;
            Close();
            callback?.Invoke();
        }

        private void CreateStyles()
        {
            if (contentStyle != null)
            {
                return;
            }

            contentStyle = new GUIStyle { padding = new RectOffset(Padding, Padding, Padding, Padding) };
            headingStyle = LeftAligned(EditorStyles.boldLabel, 15, HeadingColor);
            messageStyle = LeftAligned(EditorStyles.wordWrappedLabel, 13, MessageColor);
            messageStyle.richText = true;
            detailStyle = LeftAligned(EditorStyles.miniLabel, 11, MessageColor);
        }

        private static GUIStyle LeftAligned(GUIStyle source, int fontSize, Color textColor)
        {
            return new GUIStyle(source)
            {
                fontSize = fontSize,
                wordWrap = true,
                margin = new RectOffset(0, 0, source.margin.top, source.margin.bottom),
                padding = new RectOffset(0, 0, source.padding.top, source.padding.bottom),
                normal = { textColor = textColor },
            };
        }
    }
}
