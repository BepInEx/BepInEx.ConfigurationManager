using System;
using System.Collections.Generic;
using UnityEngine;

namespace ConfigurationManager.Utilities
{
    internal static class ImguiUtils
    {
#if IL2CPP
        internal static Texture2D WindowBackground { get; private set; }
        internal static Texture2D CategorySectionBackground { get; private set; }
        internal static Texture2D WidgetBackground { get; private set; }

        public static GUIStyle windowStyle;
        public static GUIStyle headerStyle;
        public static GUIStyle entryStyle;
        public static GUIStyle labelStyle;
        public static GUIStyle textStyle;
        public static GUIStyle toggleStyle;
        public static GUIStyle buttonStyle;
        public static GUIStyle redbuttonStyle;
        public static GUIStyle boxStyle;
        public static GUIStyle sliderStyle;
        public static GUIStyle thumbStyle;
        public static GUIStyle categoryHeaderSkin;
        public static GUIStyle pluginHeaderSkin;
        public static int fontSize = 14;

        private static Dictionary<ulong, Texture2D> _texCache = new Dictionary<ulong, Texture2D>();

        public static void DrawWindowBackground(Rect position, Color? color = null)
        {
            DrawBackground(position, color, 7, 4, 3, 2, 1, 1, 1);
        }

        public static void DrawControlBackground(Rect position, Color? color = null)
        {
            DrawBackground(position, color, 5, 3, 2, 1, 1);
        }

        private static void DrawBackground(Rect position, Color? color, params int[] corner)
        {
            int width = (int)position.width, height = (int)position.height;
            if (width <= 0 || height <= 0)
                return;

            var cacheKey = (ulong)width << 32 | (uint)height;
            _texCache.TryGetValue(cacheKey, out var texture);
            if (!texture)
            {
                Color32[] colors = new Color32[width * height];
                colors.Fill(color ?? Color.gray);
                Color32 clear = Color.clear;
                int cornerHeight = Math.Min(corner.Length, height);
                for (int i = 0, j = -1; i <= cornerHeight; j = ++i)
                {
                    int start = j >= 0 ? Math.Min(corner[j], width) : 0;
                    int length = i < cornerHeight ? Math.Min(corner[i], width) : 0;
                    length += start;
                    start = i * width - start;
                    colors.Fill(start, length, clear);
                    start = colors.Length - start - length;
                    colors.Fill(start, length, clear);
                }

                texture = TexturePool.GetTexture2D(width, height, TextureFormat.RGBA32, false);
                texture.SetPixels32(colors);
                texture.Apply();

                _texCache[cacheKey] = texture;
            }

            GUI.DrawTexture(position, texture, ScaleMode.StretchToFill, true);
        }

        internal static Texture2D MakeTexture(int width, int height, Color color)
        {
            var texture = new Texture2D(width, height);
            for (int y = 0; y < height; ++y)
            {
                for (int x = 0; x < width; ++x)
                {
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            return texture;
        }

        public static void CreateBackgrounds()
        {
            if (WindowBackground == null)
            {
                var background = new Texture2D(1, 1, TextureFormat.ARGB32, false);
                background.SetPixel(0, 0, ConfigurationManager._panelBackgroundColor.Value);
                background.Apply();
                WindowBackground = background;
            }

            if (CategorySectionBackground == null)
            {
                var entryBackground = new Texture2D(1, 1, TextureFormat.ARGB32, false);
                entryBackground.SetPixel(0, 0, ConfigurationManager._categorySectionColor.Value);
                entryBackground.Apply();
                CategorySectionBackground = entryBackground;
            }
        }

        public static void CreateStyles()
        {
            if (windowStyle == null)
            {
                windowStyle = GUI.skin.window.CreateCopy();
                windowStyle.normal.textColor = ConfigurationManager._fontColor.Value;
                windowStyle.fontSize = fontSize;
                windowStyle.active.textColor = ConfigurationManager._fontColor.Value;
            }

            if (labelStyle == null)
            {
                labelStyle = GUI.skin.label.CreateCopy();
                labelStyle.normal.textColor = ConfigurationManager._fontColor.Value;
                labelStyle.fontSize = fontSize;
            }

            if (textStyle == null)
            {
                textStyle = GUI.skin.textArea.CreateCopy();
                textStyle.normal.textColor = ConfigurationManager._fontColor.Value;
                textStyle.fontSize = fontSize;
            }

            if (buttonStyle == null)
            {
                buttonStyle = GUI.skin.button.CreateCopy();
                buttonStyle.normal.textColor = ConfigurationManager._fontColor.Value;
                var pooledPluginTex = TexturePool.GetTexture2D(1, 1, TextureFormat.RGBA32, false);
                pooledPluginTex.SetPixel(0, 0, ConfigurationManager._highlightColor.Value);
                pooledPluginTex.Apply();
                buttonStyle.hover.background = pooledPluginTex;
                buttonStyle.normal.background = pooledPluginTex;
                TexturePool.ReleaseTexture2D(pooledPluginTex);
                ///buttonStyle.normal.background = ConfigurationManager._widgetBackgroundColor.Value;
                buttonStyle.fontSize = fontSize;
            }
            
            if (redbuttonStyle == null)
            {
                redbuttonStyle = GUI.skin.button.CreateCopy();
                redbuttonStyle.normal.textColor = ConfigurationManager._fontColor.Value;
                var pooledPluginTex = TexturePool.GetTexture2D(2, 2, TextureFormat.RGBA32, false);
                pooledPluginTex.SetPixel(0, 0, ConfigurationManager._closeButtonColor.Value);
                pooledPluginTex.Apply();
                buttonStyle.hover.background = pooledPluginTex;
                //buttonStyle.normal.background = pooledPluginTex;
                TexturePool.ReleaseTexture2D(pooledPluginTex);
                buttonStyle.fontSize = fontSize;
            }


            if (categoryHeaderSkin == null)
            {
                categoryHeaderSkin = labelStyle.CreateCopy();
                categoryHeaderSkin.alignment = TextAnchor.UpperCenter;
                categoryHeaderSkin.wordWrap = true;
                categoryHeaderSkin.stretchWidth = true;
            }

            if (pluginHeaderSkin == null)
            {
                pluginHeaderSkin = categoryHeaderSkin.CreateCopy();
            }

            if (toggleStyle == null)
            {
                toggleStyle = GUI.skin.toggle.CreateCopy();
                toggleStyle.normal.textColor = ConfigurationManager._fontColor.Value;
                toggleStyle.fontSize = fontSize;
            }

            if (boxStyle == null)
            {
                boxStyle = GUI.skin.box.CreateCopy();
                boxStyle.normal.textColor = ConfigurationManager._fontColor.Value;
                boxStyle.fontSize = fontSize;
            }

            if (sliderStyle == null)
            {
                sliderStyle = GUI.skin.horizontalSlider.CreateCopy();
            }

            if (thumbStyle == null)
            {
                thumbStyle = GUI.skin.horizontalSliderThumb.CreateCopy();
            }
        }

#if NETSTANDARD || NETCOREAPP
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static void Fill<T>(this T[] array, T value) =>
            new Span<T>(array).Fill(value);

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static void Fill<T>(this T[] array, int start, int length, T value) =>
            new Span<T>(array, start, length).Fill(value);
#else
        public static void Fill<T>(this T[] array, T value)
        {
            for (int i = 0; i < array.Length; ++i)
                array[i] = value;
        }

        public static void Fill<T>(this T[] array, int start, int length, T value)
        {
            for (int i = start; i < start + length; ++i)
                array[i] = value;
        }
#endif

#else
        internal static Texture2D TooltipBg { get; private set; }
        internal static Texture2D WindowBackground { get; private set; }
        internal static Texture2D CategorySectionBackground { get; private set; }
        internal static Texture2D WidgetBackground { get; private set; }

        public static GUIStyle windowStyle;
        public static GUIStyle leftPanelStyle;
        public static GUIStyle headerStyle;
        public static GUIStyle entryStyle;
        public static GUIStyle labelStyle;
        public static GUIStyle textStyle;
        public static GUIStyle toggleStyle;
        public static GUIStyle buttonStyle;
        public static GUIStyle redbuttonStyle;
        public static GUIStyle boxStyle;
        public static GUIStyle sliderStyle;
        public static GUIStyle thumbStyle;
        public static GUIStyle categoryHeaderSkin;
        public static GUIStyle pluginHeaderSkin;
        public static int fontSize = 14;

        public static void DrawWindowBackground(Rect position)
        {
            if (!WindowBackground)
            {
                var pooledTex = TexturePool.GetTexture2D(1, 1, TextureFormat.ARGB32, false);
                pooledTex.SetPixel(0, 0, new Color(0.5f, 0.5f, 0.5f, 1));
                pooledTex.Apply();

                WindowBackground = pooledTex;
                TexturePool.ReleaseTexture2D(pooledTex);
            }

            GUI.Box(position, GUIContent.none, new GUIStyle { normal = new GUIStyleState { background = WindowBackground } });
        }

        public static void DrawControlBackground(Rect position, Color color = default)
        {
            if (!TooltipBg)
            {
                var background = TexturePool.GetTexture2D(1, 1, TextureFormat.ARGB32, false);
                background.SetPixel(0, 0, Color.black);
                background.Apply();
                TooltipBg = background;
                TexturePool.ReleaseTexture2D(background);
            }

            GUI.Box(position, GUIContent.none, new GUIStyle { normal = new GUIStyleState { background = TooltipBg } });
        }

        internal static Texture2D MakeTexture(int width, int height, Color color)
        {
            var texture = new Texture2D(width, height);
            for (int y = 0; y < height; ++y)
            {
                for (int x = 0; x < width; ++x)
                {
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            return texture;
        }

        public static void CreateBackgrounds()
        {
            if (WindowBackground == null)
            {
                var background = TexturePool.GetTexture2D(1, 1, TextureFormat.ARGB32, false);
                background.SetPixel(0, 0, ConfigurationManager._panelBackgroundColor.Value);
                background.Apply();
                WindowBackground = background;
                TexturePool.ReleaseTexture2D(background);
            }

            if (CategorySectionBackground == null)
            {
                var entryBackground = TexturePool.GetTexture2D(1, 1, TextureFormat.ARGB32, false);
                entryBackground.SetPixel(0, 0, ConfigurationManager._categorySectionColor.Value);
                entryBackground.Apply();
                CategorySectionBackground = entryBackground;
                TexturePool.ReleaseTexture2D(entryBackground);
            }
        }

        public static void CreateStyles()
        {
            if (windowStyle == null)
            {
                RecreateStyles();
            }
        }

        public static void RecreateStyles()
        {
            windowStyle = GUI.skin.window.CreateCopy();
            windowStyle.normal.textColor = ConfigurationManager._fontColor.Value;
            windowStyle.fontSize = fontSize;
            windowStyle.active.textColor = ConfigurationManager._fontColor.Value;

            leftPanelStyle = GUI.skin.box.CreateCopy();
            leftPanelStyle.normal.background = TexturePool.GetColorTexture(ConfigurationManager._leftPanelColor.Value);
            leftPanelStyle.active.background = TexturePool.GetColorTexture(ConfigurationManager._leftPanelColor.Value);

            labelStyle = GUI.skin.label.CreateCopy();
            labelStyle.normal.textColor = ConfigurationManager._fontColor.Value;
            labelStyle.fontSize = fontSize;

            textStyle = GUI.skin.textArea.CreateCopy();
            textStyle.normal.textColor = ConfigurationManager._fontColor.Value;
            textStyle.fontSize = fontSize;

            buttonStyle = GUI.skin.button.CreateCopy();
            buttonStyle.normal.textColor = ConfigurationManager._fontColor.Value;
            buttonStyle.hover.background = TexturePool.GetColorTexture(ConfigurationManager._highlightColor.Value);
            ///buttonStyle.normal.background = ConfigurationManager._widgetBackgroundColor.Value;
            buttonStyle.fontSize = fontSize;

            redbuttonStyle = GUI.skin.button.CreateCopy();
            redbuttonStyle.normal.textColor = ConfigurationManager._fontColor.Value;
            redbuttonStyle.hover.background = TexturePool.GetColorTexture(ConfigurationManager._closeButtonColor.Value);
            redbuttonStyle.normal.background = TexturePool.GetColorTexture(ConfigurationManager._closeButtonColor.Value * 1.25f);
            redbuttonStyle.fontSize = fontSize;

            categoryHeaderSkin = labelStyle.CreateCopy();
            categoryHeaderSkin.alignment = TextAnchor.UpperCenter;
            categoryHeaderSkin.wordWrap = true;
            categoryHeaderSkin.stretchWidth = true;

            pluginHeaderSkin = categoryHeaderSkin.CreateCopy();

            toggleStyle = GUI.skin.toggle.CreateCopy();
            toggleStyle.normal.textColor = ConfigurationManager._fontColor.Value;
            toggleStyle.fontSize = fontSize;

            boxStyle = GUI.skin.box.CreateCopy();
            boxStyle.normal.textColor = ConfigurationManager._fontColor.Value;
            boxStyle.fontSize = fontSize;

            sliderStyle = GUI.skin.horizontalSlider.CreateCopy();

            thumbStyle = GUI.skin.horizontalSliderThumb.CreateCopy();
        }

#endif
    }
}