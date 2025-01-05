using System;
using System.Collections.Generic;
using UnityEngine;

namespace ConfigurationManager.Utilities
{
    internal static class ImguiUtils
    {
#if IL2CPP
        private static Dictionary<ulong, Texture2D> _texCache = new Dictionary<ulong, Texture2D>();

        public static void DrawWindowBackground(Rect position, Color? color = null)
        {
            DrawBackground(position, color, 7, 4, 3, 2, 1, 1, 1);
        }

        public static void DrawContolBackground(Rect position, Color? color = null)
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
                for (int i = 0, j = -1; i <= cornerHeight; j = i++)
                {
                    int start = j >= 0 ? Math.Min(corner[j], width) : 0;
                    int length = i < cornerHeight ? Math.Min(corner[i], width) : 0;
                    length += start;
                    start = i * width - start;
                    colors.Fill(start, length, clear);
                    start = colors.Length - start - length;
                    colors.Fill(start, length, clear);
                }
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.SetPixels32(colors);
                texture.Apply();

                _texCache[cacheKey] = texture;
            }

            GUI.DrawTexture(position, texture, ScaleMode.StretchToFill, true);
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
            for (int i = 0; i < array.Length; i++)
                array[i] = value;
        }

        public static void Fill<T>(this T[] array, int start, int length, T value)
        {
            for (int i = start; i < start + length; i++)
                array[i] = value;
        }
#endif

#else
        private static Texture2D _tooltipBg;
        private static Texture2D _windowBackground;

        public static void DrawWindowBackground(Rect position)
        {
            if (!_windowBackground)
            {
                var windowBackground = new Texture2D(1, 1, TextureFormat.ARGB32, false);
                windowBackground.SetPixel(0, 0, new Color(0.5f, 0.5f, 0.5f, 1));
                windowBackground.Apply();
                _windowBackground = windowBackground;
            }

            GUI.Box(position, GUIContent.none, new GUIStyle { normal = new GUIStyleState { background = _windowBackground } });
        }

        public static void DrawContolBackground(Rect position, Color color = default)
        {
            if (!_tooltipBg)
            {
                var background = new Texture2D(1, 1, TextureFormat.ARGB32, false);
                background.SetPixel(0, 0, Color.black);
                background.Apply();
                _tooltipBg = background;
            }

            GUI.Box(position, GUIContent.none, new GUIStyle { normal = new GUIStyleState { background = _tooltipBg } });
        }
#endif
    }
}
