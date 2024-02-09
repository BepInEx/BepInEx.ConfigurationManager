// Made by MarC0 / ManlyMarco
// Copyright 2018 GNU General Public License v3.0

using BepInEx;
using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

#if IL2CPP
using BaseUnityPlugin = BepInEx.PluginInfo;
#endif

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
            if (color.a < 1f)
            {
                // SetPixel ignores alpha, so we need to lerp manually
                for (var x = 0; x < tex.width; x++)
                {
                    for (var y = 0; y < tex.height; y++)
                    {
                        var origColor = tex.GetPixel(x, y);
                        var lerpedColor = Color.Lerp(origColor, color, color.a);
                        // Not accurate, but good enough for our purposes
                        lerpedColor.a = Mathf.Max(origColor.a, color.a);
                        tex.SetPixel(x, y, lerpedColor);
                    }
                }
            }
            else
            {
                for (var x = 0; x < tex.width; x++)
                    for (var y = 0; y < tex.height; y++)
                        tex.SetPixel(x, y, color);
            }

            tex.Apply(false);
        }

        public static void FillTextureCheckerboard(this Texture2D tex)
        {
            for (var x = 0; x < tex.width; x++)
                for (var y = 0; y < tex.height; y++)
                    tex.SetPixel(x, y, (x / 10 + y / 10) % 2 == 1 ? Color.black : Color.white);

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

            // Available since 2018.3
            var prop = typeof(Application).GetProperty("consoleLogPath", BindingFlags.Static | BindingFlags.Public);
            if (prop != null)
            {
                var path = prop.GetValue(null, null) as string;
                candidates.Add(path);
            }

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

        public static string GetWebsite(BaseUnityPlugin bepInPlugin)
        {
            if (bepInPlugin == null) return null;
            try
            {
#if IL2CPP
                var fileName = bepInPlugin.Location;//.Instance.GetType().Module.FullyQualifiedName;
#else
                var fileName = bepInPlugin.Info.Location; //.GetType().Assembly.Location;
#endif
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
#if IL2CPP
                ConfigurationManager.Logger.LogWarning($"Failed to get URI for {bepInPlugin.Metadata?.Name} - {e.Message}");
#else
                ConfigurationManager.Logger.LogWarning($"Failed to get URI for {bepInPlugin.Info?.Metadata?.Name} - {e.Message}");
#endif
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
                ConfigurationManager.Logger.Log(LogLevel.Message | LogLevel.Warning, $"Failed to open URL {url}\nCause: {ex.Message}");
            }
        }

        public static GUIStyle CreateCopy(this GUIStyle original)
        {
#if IL2CPP
            // Copy constructor is sometimes stripped out in IL2CPP
            var guiStyle = new GUIStyle();
            guiStyle.m_Ptr = GUIStyle.Internal_Copy(guiStyle, original);
            return guiStyle;
#else
            return new GUIStyle(original);
#endif
        }
    }
}
