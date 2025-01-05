// Made by MarC0 / ManlyMarco
// Copyright 2018 GNU General Public License v3.0

using ConfigurationManager.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace ConfigurationManager
{
    internal class SettingFieldDrawer
    {
        private static IEnumerable<KeyCode> _keysToCheck;
        public static Dictionary<Type, Action<SettingEntryBase>> SettingDrawHandlers { get; }

        private static readonly Dictionary<SettingEntryBase, ComboBox> _comboBoxCache = new Dictionary<SettingEntryBase, ComboBox>();
        private static readonly Dictionary<SettingEntryBase, ColorCacheEntry> _colorCache = new Dictionary<SettingEntryBase, ColorCacheEntry>();

        private static ConfigurationManager _instance;

        private static SettingEntryBase _currentKeyboardShortcutToSet;
        public static bool SettingKeyboardShortcut => _currentKeyboardShortcutToSet != null;

        static SettingFieldDrawer()
        {
            SettingDrawHandlers = new Dictionary<Type, Action<SettingEntryBase>>
            {
                { typeof(bool), DrawBoolField },
                { typeof(KeyboardShortcut), DrawKeyboardShortcut},
                { typeof(KeyCode), DrawKeyCode },
                { typeof(Color), DrawColor },
                { typeof(Vector2), DrawVector2 },
                { typeof(Vector3), DrawVector3 },
                { typeof(Vector4), DrawVector4 },
                { typeof(Quaternion), DrawQuaternion },
            };
        }

        public SettingFieldDrawer(ConfigurationManager instance)
        {
            _instance = instance;
        }

        public void DrawSettingValue(SettingEntryBase setting)
        {
            if (setting.CustomDrawer != null)
                setting.CustomDrawer(setting is ConfigSettingEntry newSetting ? newSetting.Entry : null);
            else if (setting.CustomHotkeyDrawer != null)
            {
                var isBeingSet = _currentKeyboardShortcutToSet == setting;
                var isBeingSetOriginal = isBeingSet;

                setting.CustomHotkeyDrawer(setting is ConfigSettingEntry newSetting ? newSetting.Entry : null, ref isBeingSet);

                if (isBeingSet != isBeingSetOriginal)
                    _currentKeyboardShortcutToSet = isBeingSet ? setting : null;
            }
            else if (setting.ShowRangeAsPercent != null && setting.AcceptableValueRange.Key != null)
                DrawRangeField(setting);
            else if (setting.AcceptableValues != null)
                DrawListField(setting);
            else if (DrawFieldBasedOnValueType(setting))
                return;
            else if (setting.SettingType.IsEnum)
                DrawEnumField(setting);
            else
                DrawUnknownField(setting, _instance.RightColumnWidth);

        }

        public static void ClearCache()
        {
            _comboBoxCache.Clear();

            foreach (var tex in _colorCache)
                UnityEngine.Object.Destroy(tex.Value.Tex);
            _colorCache.Clear();
        }

        public static void DrawCenteredLabel(string text, params GUILayoutOption[] options)
        {
            GUILayout.BeginHorizontal(options);
            GUILayout.FlexibleSpace();
            GUILayout.Label(text);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private static GUIStyle _categoryHeaderSkin;
        public static void DrawCategoryHeader(string text)
        {
            if (_categoryHeaderSkin == null)
            {
                _categoryHeaderSkin = GUI.skin.label.CreateCopy();
                _categoryHeaderSkin.alignment = TextAnchor.UpperCenter;
                _categoryHeaderSkin.wordWrap = true;
                _categoryHeaderSkin.stretchWidth = true;
                _categoryHeaderSkin.fontSize = 14;
            }

            GUILayout.Label(text, _categoryHeaderSkin);
        }

        private static GUIStyle _pluginHeaderSkin;
        public static bool DrawPluginHeader(GUIContent content, bool isCollapsed)
        {
            if (_pluginHeaderSkin == null)
            {
                _pluginHeaderSkin = GUI.skin.label.CreateCopy();
                _pluginHeaderSkin.alignment = TextAnchor.UpperCenter;
                _pluginHeaderSkin.wordWrap = true;
                _pluginHeaderSkin.stretchWidth = true;
                _pluginHeaderSkin.fontSize = 15;
            }

            if (isCollapsed) content.text += "\n...";
            return GUILayout.Button(content, _pluginHeaderSkin, GUILayout.ExpandWidth(true));
        }

        public static bool DrawCurrentDropdown()
        {
            if (ComboBox.CurrentDropdownDrawer != null)
            {
                ComboBox.CurrentDropdownDrawer.Invoke();
                ComboBox.CurrentDropdownDrawer = null;
                return true;
            }
            return false;
        }

        private static void DrawListField(SettingEntryBase setting)
        {
            var acceptableValues = setting.AcceptableValues;
            if (acceptableValues.Length == 0)
                throw new ArgumentException("AcceptableValueListAttribute returned an empty list of acceptable values. You need to supply at least 1 option.");

            if (!setting.SettingType.IsInstanceOfType(acceptableValues.FirstOrDefault(x => x != null)))
                throw new ArgumentException("AcceptableValueListAttribute returned a list with items of type other than the settng type itself.");

            if (setting.SettingType == typeof(KeyCode))
                DrawKeyCode(setting);
            else
                DrawComboboxField(setting, acceptableValues, _instance.SettingWindowRect.yMax);
        }

        private static bool DrawFieldBasedOnValueType(SettingEntryBase setting)
        {
            if (SettingDrawHandlers.TryGetValue(setting.SettingType, out var drawMethod))
            {
                drawMethod(setting);
                return true;
            }
            return false;
        }

        private static void DrawBoolField(SettingEntryBase setting)
        {
            var boolVal = (bool)setting.Get();
            var result = GUILayout.Toggle(boolVal, boolVal ? "Enabled" : "Disabled", GUILayout.ExpandWidth(true));
            if (result != boolVal)
                setting.Set(result);
        }

        private static void DrawEnumField(SettingEntryBase setting)
        {
            if (setting.SettingType.GetCustomAttributes(typeof(FlagsAttribute), false).Any())
                DrawFlagsField(setting, Enum.GetValues(setting.SettingType), _instance.RightColumnWidth);
            else
                DrawComboboxField(setting, Enum.GetValues(setting.SettingType), _instance.SettingWindowRect.yMax);
        }

        private static void DrawFlagsField(SettingEntryBase setting, IList enumValues, int maxWidth)
        {
            var currentValue = Convert.ToInt64(setting.Get());
            var allValues = enumValues.Cast<Enum>().Select(x => new { name = x.ToString(), val = Convert.ToInt64(x) }).ToArray();

            // Vertically stack Horizontal groups of the options to deal with the options taking more width than is available in the window
            GUILayout.BeginVertical(GUILayout.MaxWidth(maxWidth));
            {
                for (var index = 0; index < allValues.Length;)
                {
                    GUILayout.BeginHorizontal();
                    {
                        var currentWidth = 0;
                        for (; index < allValues.Length; index++)
                        {
                            var value = allValues[index];

                            // Skip the 0 / none enum value, just uncheck everything to get 0
                            if (value.val != 0)
                            {
                                // Make sure this horizontal group doesn't extend over window width, if it does then start a new horiz group below
                                var textDimension = (int)GUI.skin.toggle.CalcSize(new GUIContent(value.name)).x;
                                currentWidth += textDimension;
                                if (currentWidth > maxWidth)
                                    break;

                                GUI.changed = false;
                                var newVal = GUILayout.Toggle((currentValue & value.val) == value.val, value.name,
                                    GUILayout.ExpandWidth(false));
                                if (GUI.changed)
                                {
                                    var newValue = newVal ? currentValue | value.val : currentValue & ~value.val;
                                    setting.Set(Enum.ToObject(setting.SettingType, newValue));
                                }
                            }
                        }
                    }
                    GUILayout.EndHorizontal();
                }

                GUI.changed = false;
            }
            GUILayout.EndVertical();
            // Make sure the reset button is properly spaced
            GUILayout.FlexibleSpace();
        }

        private static void DrawComboboxField(SettingEntryBase setting, IList list, float windowYmax)
        {
            var buttonText = ObjectToGuiContent(setting.Get());
            var dispRect = GUILayoutUtility.GetRect(buttonText, GUI.skin.button, GUILayout.ExpandWidth(true));

            if (!_comboBoxCache.TryGetValue(setting, out var box))
            {
                box = new ComboBox(dispRect, buttonText, list.Cast<object>().Select(ObjectToGuiContent).ToArray(), GUI.skin.button, windowYmax);
                _comboBoxCache[setting] = box;
            }
            else
            {
                box.Rect = dispRect;
                box.ButtonContent = buttonText;
            }

            box.Show(id =>
            {
                if (id >= 0 && id < list.Count)
                    setting.Set(list[id]);
            });
        }

        private static GUIContent ObjectToGuiContent(object x)
        {
            if (x is Enum)
            {
                var enumType = x.GetType();
                var enumMember = enumType.GetMember(x.ToString()).FirstOrDefault();
                var attr = enumMember?.GetCustomAttributes(typeof(DescriptionAttribute), false).Cast<DescriptionAttribute>().FirstOrDefault();
                if (attr != null)
                    return new GUIContent(attr.Description);
                return new GUIContent(x.ToString().ToProperCase());
            }
            return new GUIContent(x.ToString());
        }

        private static void DrawRangeField(SettingEntryBase setting)
        {
            var value = setting.Get();
            var converted = (float)Convert.ToDouble(value, CultureInfo.InvariantCulture);
            var leftValue = (float)Convert.ToDouble(setting.AcceptableValueRange.Key, CultureInfo.InvariantCulture);
            var rightValue = (float)Convert.ToDouble(setting.AcceptableValueRange.Value, CultureInfo.InvariantCulture);

            var result = GUILayout.HorizontalSlider(converted, leftValue, rightValue, GUILayout.ExpandWidth(true));
            if (Math.Abs(result - converted) > Mathf.Abs(rightValue - leftValue) / 1000)
            {
                var newValue = Convert.ChangeType(result, setting.SettingType, CultureInfo.InvariantCulture);
                setting.Set(newValue);
            }

            if (setting.ShowRangeAsPercent == true)
            {
                DrawCenteredLabel(
                    Mathf.Round(100 * Mathf.Abs(result - leftValue) / Mathf.Abs(rightValue - leftValue)) + "%",
                    GUILayout.Width(50));
            }
            else
            {
                var strVal = value.ToString().AppendZeroIfFloat(setting.SettingType);
                var strResult = GUILayout.TextField(strVal, GUILayout.Width(50));
                if (strResult != strVal)
                {
                    try
                    {
                        var resultVal = (float)Convert.ToDouble(strResult, CultureInfo.InvariantCulture);
                        var clampedResultVal = Mathf.Clamp(resultVal, leftValue, rightValue);
                        setting.Set(Convert.ChangeType(clampedResultVal, setting.SettingType, CultureInfo.InvariantCulture));
                    }
                    catch (FormatException)
                    {
                        // Ignore user typing in bad data
                    }
                }
            }
        }

        private void DrawUnknownField(SettingEntryBase setting, int rightColumnWidth)
        {
            // Try to use user-supplied converters
            if (setting.ObjToStr != null && setting.StrToObj != null)
            {
                var text = setting.ObjToStr(setting.Get()).AppendZeroIfFloat(setting.SettingType);
                var result = GUILayout.TextField(text, GUILayout.Width(rightColumnWidth), GUILayout.MaxWidth(rightColumnWidth));

                if (result != text)
                    setting.Set(setting.StrToObj(result));
            }
            else
            {
                // Fall back to slow/less reliable method
                var rawValue = setting.Get();
                var value = rawValue == null ? "NULL" : rawValue.ToString().AppendZeroIfFloat(setting.SettingType);
                if (CanCovert(value, setting.SettingType))
                {
                    var result = GUILayout.TextField(value, GUILayout.Width(rightColumnWidth), GUILayout.MaxWidth(rightColumnWidth));
                    if (result != value)
                        setting.Set(Convert.ChangeType(result, setting.SettingType, CultureInfo.InvariantCulture));
                }
                else
                {
                    GUILayout.TextArea(value, GUILayout.MaxWidth(rightColumnWidth));
                }
            }

            // When using MaxWidth the width will always be less than full window size, use this to fill this gap and push the Reset button to the right edge
            GUILayout.FlexibleSpace();
        }

        private readonly Dictionary<Type, bool> _canCovertCache = new Dictionary<Type, bool>();
        private bool CanCovert(string value, Type type)
        {
            if (_canCovertCache.ContainsKey(type))
                return _canCovertCache[type];

            try
            {
                var _ = Convert.ChangeType(value, type);
                _canCovertCache[type] = true;
                return true;
            }
            catch
            {
                _canCovertCache[type] = false;
                return false;
            }
        }

        private static void DrawKeyCode(SettingEntryBase setting)
        {
            if (ReferenceEquals(_currentKeyboardShortcutToSet, setting))
            {
                GUILayout.Label("Press any key", GUILayout.ExpandWidth(true));
                GUIUtility.keyboardControl = -1;

#if IL2CPP
                KeyCode key = KeyboardShortcut.ModifierBlockKeyCodes.FirstOrDefault(Input.GetKeyUp);
                if (key != KeyCode.None)
                {
                    setting.Set(key);
                    _currentKeyboardShortcutToSet = null;
                }
#else
                var input = UnityInput.Current;
                if (_keysToCheck == null) _keysToCheck = input.SupportedKeyCodes.Except(new[] { KeyCode.Mouse0, KeyCode.None }).ToArray();
                foreach (var key in _keysToCheck)
                {
                    if (input.GetKeyUp(key))
                    {
                        setting.Set(key);
                        _currentKeyboardShortcutToSet = null;
                        break;
                    }
                }
#endif
                if (GUILayout.Button("Cancel", GUILayout.ExpandWidth(false)))
                    _currentKeyboardShortcutToSet = null;
            }
            else
            {
                var acceptableValues = setting.AcceptableValues?.Length > 1 ? setting.AcceptableValues : Enum.GetValues(setting.SettingType);
                DrawComboboxField(setting, acceptableValues, _instance.SettingWindowRect.yMax);

                if (GUILayout.Button(new GUIContent("Set...", null, "Set the key by pressing any key on your keyboard."), GUILayout.ExpandWidth(false)))
                    _currentKeyboardShortcutToSet = setting;
            }
        }

        private static void DrawKeyboardShortcut(SettingEntryBase setting)
        {
            if (ReferenceEquals(_currentKeyboardShortcutToSet, setting))
            {
                GUILayout.Label("Press any key combination", GUILayout.ExpandWidth(true));
                GUIUtility.keyboardControl = -1;

#if IL2CPP
                KeyCode key = KeyboardShortcut.ModifierBlockKeyCodes.FirstOrDefault(Input.GetKeyUp);
                if (key != KeyCode.None)
                {
                    setting.Set(new KeyboardShortcut(key, KeyboardShortcut.ModifierBlockKeyCodes.Where(Input.GetKey).ToArray()));
                    _currentKeyboardShortcutToSet = null;
                }
#else
                var input = UnityInput.Current;
                if (_keysToCheck == null) _keysToCheck = input.SupportedKeyCodes.Except(new[] { KeyCode.Mouse0, KeyCode.None }).ToArray();
                foreach (var key in _keysToCheck)
                {
                    if (input.GetKeyUp(key))
                    {
                        setting.Set(new BepInEx.Configuration.KeyboardShortcut(key, _keysToCheck.Where(input.GetKey).ToArray()));
                        _currentKeyboardShortcutToSet = null;
                        break;
                    }
                }
#endif
                if (GUILayout.Button("Cancel", GUILayout.ExpandWidth(false)))
                    _currentKeyboardShortcutToSet = null;
            }
            else
            {
                if (GUILayout.Button(setting.Get().ToString(), GUILayout.ExpandWidth(true)))
                    _currentKeyboardShortcutToSet = setting;

                if (GUILayout.Button("Clear", GUILayout.ExpandWidth(false)))
                {
                    setting.Set(BepInEx.Configuration.KeyboardShortcut.Empty);
                    _currentKeyboardShortcutToSet = null;
                }
            }
        }

        private static void DrawVector2(SettingEntryBase obj)
        {
            var setting = (Vector2)obj.Get();
            var copy = setting;
            setting.x = DrawSingleVectorSlider(setting.x, "X");
            setting.y = DrawSingleVectorSlider(setting.y, "Y");
            if (setting != copy) obj.Set(setting);
        }

        private static void DrawVector3(SettingEntryBase obj)
        {
            var setting = (Vector3)obj.Get();
            var copy = setting;
            setting.x = DrawSingleVectorSlider(setting.x, "X");
            setting.y = DrawSingleVectorSlider(setting.y, "Y");
            setting.z = DrawSingleVectorSlider(setting.z, "Z");
            if (setting != copy) obj.Set(setting);
        }

        private static void DrawVector4(SettingEntryBase obj)
        {
            var setting = (Vector4)obj.Get();
            var copy = setting;
            setting.x = DrawSingleVectorSlider(setting.x, "X");
            setting.y = DrawSingleVectorSlider(setting.y, "Y");
            setting.z = DrawSingleVectorSlider(setting.z, "Z");
            setting.w = DrawSingleVectorSlider(setting.w, "W");
            if (setting != copy) obj.Set(setting);
        }

        private static void DrawQuaternion(SettingEntryBase obj)
        {
            var setting = (Quaternion)obj.Get();
            var copy = setting;
            setting.x = DrawSingleVectorSlider(setting.x, "X");
            setting.y = DrawSingleVectorSlider(setting.y, "Y");
            setting.z = DrawSingleVectorSlider(setting.z, "Z");
            setting.w = DrawSingleVectorSlider(setting.w, "W");
            if (setting != copy) obj.Set(setting);
        }

        private static float DrawSingleVectorSlider(float setting, string label)
        {
            GUILayout.Label(label, GUILayout.ExpandWidth(false));
            float.TryParse(GUILayout.TextField(setting.ToString("F", CultureInfo.InvariantCulture), GUILayout.ExpandWidth(true)), NumberStyles.Any, CultureInfo.InvariantCulture, out var x);
            return x;
        }

        private static bool _drawColorHex;
        private static void DrawColor(SettingEntryBase obj)
        {
            var colorValue = (Color)obj.Get();

            GUI.changed = false;

            if (!_colorCache.TryGetValue(obj, out var cacheEntry))
            {
                var tex = new Texture2D(100, 20, TextureFormat.ARGB32, false);
                cacheEntry = new ColorCacheEntry { Tex = tex, Last = colorValue };
                FillTex(colorValue, tex);
                _colorCache[obj] = cacheEntry;
            }

            GUILayout.BeginVertical();
            {
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label(cacheEntry.Tex, GUILayout.ExpandWidth(false));

                    var colorStr = _drawColorHex ? "#" + ColorUtility.ToHtmlStringRGBA(colorValue) : $"{colorValue.r:F2} {colorValue.g:F2} {colorValue.b:F2} {colorValue.a:F2}";
                    var newColorStr = GUILayout.TextField(colorStr, GUILayout.ExpandWidth(true));
                    if (GUI.changed && colorStr != newColorStr)
                    {
                        if (_drawColorHex)
                        {
                            if (ColorUtility.TryParseHtmlString(newColorStr, out var parsedColor))
                                colorValue = parsedColor;
                        }
                        else
                        {
                            var split = newColorStr.Split(' ');
                            if (split.Length == 4 && float.TryParse(split[0], out var r) && float.TryParse(split[1], out var g) && float.TryParse(split[2], out var b) && float.TryParse(split[3], out var a))
                                colorValue = new Color(r, g, b, a);
                        }
                    }

                    _drawColorHex = GUILayout.Toggle(_drawColorHex, "Hex", GUILayout.ExpandWidth(false));
                }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label("R", GUILayout.ExpandWidth(false));
                    colorValue.r = GUILayout.HorizontalSlider(colorValue.r, 0f, 1f, GUILayout.ExpandWidth(true));
                    GUILayout.Label("G", GUILayout.ExpandWidth(false));
                    colorValue.g = GUILayout.HorizontalSlider(colorValue.g, 0f, 1f, GUILayout.ExpandWidth(true));
                    GUILayout.Label("B", GUILayout.ExpandWidth(false));
                    colorValue.b = GUILayout.HorizontalSlider(colorValue.b, 0f, 1f, GUILayout.ExpandWidth(true));
                    GUILayout.Label("A", GUILayout.ExpandWidth(false));
                    colorValue.a = GUILayout.HorizontalSlider(colorValue.a, 0f, 1f, GUILayout.ExpandWidth(true));
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();

            if (colorValue != cacheEntry.Last)
            {
                obj.Set(colorValue);
                FillTex(colorValue, cacheEntry.Tex);
                cacheEntry.Last = colorValue;
            }

            void FillTex(Color color, Texture2D tex)
            {
                if (color.a < 1f) tex.FillTextureCheckerboard();
                tex.FillTexture(color);
            }
        }

        private sealed class ColorCacheEntry
        {
            public Color Last;
            public Texture2D Tex;
        }
    }
}