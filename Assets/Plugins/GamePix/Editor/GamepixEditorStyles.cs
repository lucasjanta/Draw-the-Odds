using UnityEditor;
using UnityEngine;

namespace GamePix.Editor
{
    /// <summary>
    /// GamePix brand colors (sampled from the GamePix logo) and shared Editor window styles.
    /// </summary>
    public static class GamepixEditorStyles
    {
        public static readonly Color Navy = new Color(0.000f, 0.224f, 0.369f);
        public static readonly Color NavyLight = new Color(0.000f, 0.286f, 0.471f);
        public static readonly Color Green = new Color(0.000f, 0.882f, 0.467f);
        public static readonly Color Blue = new Color(0.000f, 0.447f, 0.937f);
        public static readonly Color BlueHover = new Color(0.102f, 0.518f, 1.000f);
        public static readonly Color BlueActive = new Color(0.000f, 0.373f, 0.784f);

        private const float HeaderHeight = 64;
        private const float LineHeaderHeight = 44;

        private static Texture2D headerTexture;
        private static Texture2D stripeTexture;
        private static Texture2D navyTexture;
        private static Texture2D blueTexture;
        private static Texture2D blueHoverTexture;
        private static Texture2D blueActiveTexture;
        private static Texture2D greyTexture;
        private static Texture2D greyHoverTexture;
        private static Texture2D greyActiveTexture;
        private static GUIStyle titleStyle;
        private static GUIStyle subtitleStyle;
        private static GUIStyle lineTitleStyle;
        private static GUIStyle lineSubtitleStyle;
        private static GUIStyle contentStyle;
        private static GUIStyle primaryButtonStyle;
        private static GUIStyle secondaryButtonStyle;

        public static Texture2D NavyTexture => GetTexture(ref navyTexture, Navy, Navy, true);

        public static GUIStyle Content => contentStyle ?? (contentStyle = new GUIStyle { padding = new RectOffset(24, 24, 20, 20) });

        public static GUIStyle PrimaryButton
        {
            get
            {
                if (primaryButtonStyle == null || primaryButtonStyle.normal.background == null)
                {
                    primaryButtonStyle = new GUIStyle(GUI.skin.button)
                    {
                        fontSize = 13,
                        fontStyle = FontStyle.Bold,
                        border = new RectOffset(),
                    };
                    primaryButtonStyle.normal.background = GetTexture(ref blueTexture, Blue, Blue, true);
                    primaryButtonStyle.hover.background = GetTexture(ref blueHoverTexture, BlueHover, BlueHover, true);
                    primaryButtonStyle.active.background = GetTexture(ref blueActiveTexture, BlueActive, BlueActive, true);
                    primaryButtonStyle.normal.textColor = Color.white;
                    primaryButtonStyle.hover.textColor = Color.white;
                    primaryButtonStyle.active.textColor = Color.white;
                }
                return primaryButtonStyle;
            }
        }

        public static GUIStyle SecondaryButton
        {
            get
            {
                if (secondaryButtonStyle == null || secondaryButtonStyle.normal.background == null)
                {
                    Color grey = EditorGUIUtility.isProSkin ? new Color(0.30f, 0.30f, 0.30f) : new Color(0.84f, 0.84f, 0.84f);
                    Color greyHover = EditorGUIUtility.isProSkin ? new Color(0.36f, 0.36f, 0.36f) : new Color(0.90f, 0.90f, 0.90f);
                    Color greyActive = EditorGUIUtility.isProSkin ? new Color(0.24f, 0.24f, 0.24f) : new Color(0.76f, 0.76f, 0.76f);
                    secondaryButtonStyle = new GUIStyle(GUI.skin.button) { fontSize = 13, border = new RectOffset() };
                    secondaryButtonStyle.normal.background = GetTexture(ref greyTexture, grey, grey, true);
                    secondaryButtonStyle.hover.background = GetTexture(ref greyHoverTexture, greyHover, greyHover, true);
                    secondaryButtonStyle.active.background = GetTexture(ref greyActiveTexture, greyActive, greyActive, true);
                    Color textColor = EditorStyles.label.normal.textColor;
                    secondaryButtonStyle.normal.textColor = textColor;
                    secondaryButtonStyle.hover.textColor = textColor;
                    secondaryButtonStyle.active.textColor = textColor;
                }
                return secondaryButtonStyle;
            }
        }

        public static void DrawHeader(float width, string title, string subtitle)
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 22, normal = { textColor = Color.white } };
            }
            if (subtitleStyle == null)
            {
                subtitleStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 12,
                    normal = { textColor = new Color(1f, 1f, 1f, 0.85f) },
                };
            }

            Rect headerRect = DrawHeaderBackground(width, HeaderHeight);
            GUI.Label(new Rect(16, 7, headerRect.width - 32, 28), title, titleStyle);
            GUI.Label(new Rect(16, 35, headerRect.width - 32, 20), subtitle, subtitleStyle);
        }

        /// <summary>
        /// One-line header: "GamePix › subtitle".
        /// </summary>
        public static void DrawLineHeader(float width, string subtitle)
        {
            if (lineTitleStyle == null)
            {
                lineTitleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 16,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = Color.white },
                };
            }
            if (lineSubtitleStyle == null)
            {
                lineSubtitleStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = new Color(1f, 1f, 1f, 0.7f) },
                };
            }

            Rect headerRect = DrawHeaderBackground(width, LineHeaderHeight);
            Rect textRect = new Rect(20, headerRect.y, headerRect.width - 40, headerRect.height - 3);
            GUIContent title = new GUIContent("GamePix");
            float titleWidth = lineTitleStyle.CalcSize(title).x;
            GUI.Label(new Rect(textRect.x, textRect.y, titleWidth, textRect.height), title, lineTitleStyle);
            GUI.Label(new Rect(textRect.x + titleWidth + 4, textRect.y, textRect.width - titleWidth - 4, textRect.height),
                      "\u203A  " + subtitle, lineSubtitleStyle);
        }

        private static Rect DrawHeaderBackground(float width, float height)
        {
            Rect headerRect = GUILayoutUtility.GetRect(width, height);
            GUI.DrawTexture(headerRect, GetTexture(ref headerTexture, NavyLight, Navy, false));
            GUI.DrawTexture(new Rect(headerRect.x, headerRect.yMax - 3, headerRect.width, 3),
                            GetTexture(ref stripeTexture, Green, Blue, true));
            return headerRect;
        }

        private static Texture2D GetTexture(ref Texture2D texture, Color from, Color to, bool isHorizontal)
        {
            if (texture != null)
            {
                return texture;
            }

            const int size = 64;
            texture = new Texture2D(isHorizontal ? size : 1, isHorizontal ? 1 : size)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
            };
            for (int index = 0; index < size; index++)
            {
                Color color = Color.Lerp(from, to, index / (size - 1f));
                texture.SetPixel(isHorizontal ? index : 0, isHorizontal ? 0 : size - 1 - index, color);
            }
            texture.Apply();
            return texture;
        }
    }
}
