// Made by MarC0 / ManlyMarco
// Copyright 2018 GNU General Public License v3.0

using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using System.Diagnostics;
#if NETSTANDARD || NETCOREAPP
using System.Runtime.CompilerServices;
#endif
using UnityEngine;

namespace ConfigurationManager.Utilities
{
    internal static class Utils
    {
        public static string ToProperCase(this string str)
        {
            if (string.IsNullOrEmpty(str)) return string.Empty;
            if (str.Length < 2) return str;

            // Start with the first character.
            string result = str.Substring(0, 1).ToUpper();

            // Add the remaining characters.
            for (int i = 1; i < str.Length; i++)
            {
                if (char.IsUpper(str[i])) result += " ";
                result += str[i];
            }

            return result;
        }

        // Search for instances of BaseUnityPlugin to also find dynamically loaded plugins. Doing this makes checking Chainloader.PluginInfos redundant.
        // Have to use FindObjectsOfType(Type) instead of FindObjectsOfType<T> because the latter is not available in some older unity versions.
        public static IReadOnlyList<PluginInfo> FindPlugins() => IL2CPPChainloader.Instance.Plugins.Values.Where(x => x.Instance is BasePlugin).ToList();

        public static string AppendZero(this string s)
        {
            return !s.Contains(".") ? s + ".0" : s;
        }

        public static string AppendZeroIfFloat(this string s, Type type)
        {
            return type == typeof(float) || type == typeof(double) || type == typeof(decimal) ? s.AppendZero() : s;
        }

        public static void FillTexture(this Texture2D tex, Color color)
        {
            for (var x = 0; x < tex.width; x++)
                for (var y = 0; y < tex.height; y++)
                    tex.SetPixel(x, y, color);

            tex.Apply(false);
        }

        public static void OpenLog()
        {
            bool TryOpen(string path)
            {
                if (path == null) return false;
                try
                {
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            var candidates = new List<string>();

            // Redirected by preloader to game root
            var rootDir = Path.Combine(Application.dataPath, "..");
            candidates.Add(Path.Combine(rootDir, "output_log.txt"));

            // Generated in most versions unless disabled
            candidates.Add(Path.Combine(Application.dataPath, "output_log.txt"));

            if (Directory.Exists(Application.persistentDataPath))
            {
                var file = Directory.GetFiles(Application.persistentDataPath, "output_log.txt", SearchOption.AllDirectories).FirstOrDefault();
                candidates.Add(file);
            }

            var latestLog = candidates.Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if (TryOpen(latestLog)) return;

            candidates.Clear();
            // Fall back to more aggresive brute search
            // BepInEx 5.x log file, can be "LogOutput.log.1" or higher if multiple game instances run
            candidates.AddRange(Directory.GetFiles(rootDir, "LogOutput.log*", SearchOption.AllDirectories));
            candidates.AddRange(Directory.GetFiles(rootDir, "output_log.txt", SearchOption.AllDirectories));
            latestLog = candidates.Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if (TryOpen(latestLog)) return;

            throw new FileNotFoundException("No log files were found");
        }

        public static string GetWebsite(PluginInfo bepInPlugin)
        {
            if (bepInPlugin == null) return null;
            try
            {
                var fileName = bepInPlugin.Instance.GetType().Module.FullyQualifiedName;
                if (!File.Exists(fileName)) return null;
                var fi = FileVersionInfo.GetVersionInfo(fileName);
                return new[]
                {
                    fi.CompanyName,
                    fi.FileDescription,
                    fi.Comments,
                    fi.LegalCopyright,
                    fi.LegalTrademarks
                }.FirstOrDefault(x => Uri.IsWellFormedUriString(x, UriKind.Absolute));
            }
            catch (Exception e)
            {
                ConfigurationManager.Logger.LogWarning("Failed to get URI for " + bepInPlugin?.Metadata?.Name + " - " + e.Message);
                return null;
            }
        }

        public static void OpenWebsite(string url)
        {
            try
            {
                if (string.IsNullOrEmpty(url)) throw new Exception("Empty URL");
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ConfigurationManager.Logger.Log(LogLevel.Message | LogLevel.Warning, "Failed to open URL " + url + "\nCause: " + ex.Message);
            }
        }

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
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(colors);
            texture.Apply();
            GUI.DrawTexture(position, texture, ScaleMode.StretchToFill, true);
        }

#if NETSTANDARD || NETCOREAPP
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Fill<T>(this T[] array, T value) =>
            new Span<T>(array).Fill(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    }
}
