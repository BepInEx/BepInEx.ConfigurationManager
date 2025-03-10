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
        private static readonly Dictionary<SettingEntryBase, FloatConfigCacheEntry> _floatConfigCache = new Dictionary<SettingEntryBase, SettingFieldDrawer.FloatConfigCacheEntry>();


        internal static ConfigurationManager _instance;

        private static SettingEntryBase _currentKeyboardShortcutToSet;
        public static bool SettingKeyboardShortcut => _currentKeyboardShortcutToSet != null;

        static SettingFieldDrawer()
        {
            SettingDrawHandlers = new Dictionary<Type, Action<SettingEntryBase>>
            {
                { typeof(bool), DrawBoolField },
                { typeof(float), DrawFloatField },
                { typeof(KeyboardShortcut), DrawKeyboardShortcut },
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
            {
                _instance.RightColumnWidth -= 50;
                GUILayout.BeginHorizontal(GUILayout.MaxWidth(_instance.RightColumnWidth - 50), GUILayout.ExpandWidth(false));
                setting.CustomDrawer(setting is ConfigSettingEntry newSetting ? newSetting.Entry : null);
                GUILayout.EndHorizontal();
                _instance.RightColumnWidth += 50;
            }
            else if (setting.CustomHotkeyDrawer != null)
            {
                var isBeingSet = _currentKeyboardShortcutToSet == setting;
                var isBeingSetOriginal = isBeingSet;

                setting.CustomHotkeyDrawer(setting is ConfigSettingEntry newSetting ? newSetting.Entry : null, ref isBeingSet);

                if (isBeingSet != isBeingSetOriginal)
                    _currentKeyboardShortcutToSet = isBeingSet ? setting : null;
            }
            else if (setting.ShowRangeAsPercent != null && setting.AcceptableValueRange.Key != null)
            {
                DrawRangeField(setting, _instance.RightColumnWidth);
            }
            else if (setting.AcceptableValues != null)
            {
                DrawListField(setting);
            }
            else if (DrawFieldBasedOnValueType(setting))
            {
                return;
            }
            else if (setting.SettingType.IsEnum)
            {
                DrawEnumField(setting);
            }
            else
            {
                DrawUnknownField(setting, _instance.RightColumnWidth);
            }
        }

        public static void ClearCache()
        {
            _comboBoxCache.Clear();

            foreach (var kvp in _colorCache)
            {
                var entry = kvp.Value;
                if (entry.Tex != null)
                {
                    // Return color texture to the pool instead of destroying
                    TexturePool.ReleaseTexture2D(entry.Tex);
                }
            }

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
            if (_categoryHeaderSkin == null || _categoryHeaderSkin != null && ConfigurationManager._categoryHeaderColor.Value != _categoryHeaderSkin.normal.background.GetPixel(0, 0))
            {
                _categoryHeaderSkin = GUI.skin.box.CreateCopy();
                _categoryHeaderSkin.alignment = TextAnchor.UpperCenter;
                _categoryHeaderSkin.wordWrap = true;
                _categoryHeaderSkin.stretchWidth = true;
                _categoryHeaderSkin.fontSize = 16;
                _categoryHeaderSkin.fontStyle = FontStyle.Bold;
                var pooledHeaderTex = TexturePool.GetTexture2D(1, 1, TextureFormat.RGBA32, false);
                pooledHeaderTex.SetPixel(0, 0, ConfigurationManager._categoryHeaderColor.Value);
                pooledHeaderTex.Apply();
                _categoryHeaderSkin.normal.background = pooledHeaderTex;
                TexturePool.ReleaseTexture2D(pooledHeaderTex);
            }

            GUIHelper.CreateLabelWithColor(text, style: _categoryHeaderSkin);
        }

        private static GUIStyle _pluginHeaderSkin;

        public static bool DrawPluginHeader(GUIContent content)
        {
            if (_pluginHeaderSkin == null)
            {
                _pluginHeaderSkin = GUI.skin.label.CreateCopy();
                _pluginHeaderSkin.alignment = TextAnchor.UpperCenter;
                _pluginHeaderSkin.wordWrap = true;
                _pluginHeaderSkin.stretchWidth = true;
                _pluginHeaderSkin.fontSize = 15;
            }

            return GUIHelper.CreateButtonWithColor(content, _pluginHeaderSkin, default, GUILayout.ExpandWidth(true));
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
                DrawComboboxField(setting, acceptableValues, _instance.SettingWindowRect.yMax, _instance.RightColumnWidth);
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
            var result = GUIHelper.CreateToggleWithColor(boolVal, boolVal ? "Enabled" : "Disabled", boolVal ? Color.green : Color.red, new GUIStyle(GUI.skin.button) { normal = { textColor = ConfigurationManager._fontColor.Value } }, GUILayout.ExpandWidth(false));
            if (result != boolVal)
                setting.Set(result);
        }

        public static void DrawFloatField(SettingEntryBase configEntry)
        {
            float num = (float)configEntry.Get();
            SettingFieldDrawer.FloatConfigCacheEntry configCacheEntry;
            if (!SettingFieldDrawer._floatConfigCache.TryGetValue(configEntry, out configCacheEntry))
            {
                configCacheEntry = new SettingFieldDrawer.FloatConfigCacheEntry()
                {
                    Value = num,
                    FieldColor = GUI.color
                };
                SettingFieldDrawer._floatConfigCache[configEntry] = configCacheEntry;
            }

            if (GUIHelper.HasChanged() || GUIHelper.IsEnterPressed() || (double)configCacheEntry.Value != (double)num)
            {
                configCacheEntry.Value = num;
                configCacheEntry.FieldText = num.ToString((IFormatProvider)NumberFormatInfo.InvariantInfo);
                configCacheEntry.FieldColor = GUI.color;
            }


            string str = GUIHelper.CreateTextFieldWithColor(configCacheEntry.FieldText, configCacheEntry.FieldColor, GUILayout.ExpandWidth(true));

            if (str == configCacheEntry.FieldText)
                return;
            configCacheEntry.FieldText = str;
            float result;
            if (SettingFieldDrawer.ShouldParse(str) && float.TryParse(str, NumberStyles.Float, (IFormatProvider)NumberFormatInfo.InvariantInfo, out result))
            {
                configEntry.Set((object)result);
                configCacheEntry.Value = (float)configEntry.Get();
                configCacheEntry.FieldText = configCacheEntry.Value.ToString((IFormatProvider)NumberFormatInfo.InvariantInfo);
                if (configCacheEntry.FieldText == str)
                {
                    configCacheEntry.FieldColor = GUI.color;
                }
                else
                {
                    configCacheEntry.FieldColor = Color.yellow;
                    configCacheEntry.FieldText = str;
                }
            }
            else
                configCacheEntry.FieldColor = Color.red;
        }

        private static bool ShouldParse(string text)
        {
            if (text == null || text.Length <= 0)
                return false;
            switch (text[text.Length - 1])
            {
                case '+':
                case ',':
                case '-':
                case '.':
                case 'E':
                case 'e':
                    return false;
                default:
                    return true;
            }
        }

        private static void DrawEnumField(SettingEntryBase setting)
        {
            if (setting.SettingType.GetCustomAttributes(typeof(FlagsAttribute), false).Any())
                DrawFlagsField(setting, Enum.GetValues(setting.SettingType), _instance.RightColumnWidth);
            else
                DrawComboboxField(setting, Enum.GetValues(setting.SettingType), _instance.SettingWindowRect.yMax, _instance.RightColumnWidth);
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
                        for (; index < allValues.Length; ++index)
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
                                var newVal = GUILayout.Toggle((currentValue & value.val) == value.val, value.name, GUILayout.ExpandWidth(false));
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

        private static void DrawComboboxField(SettingEntryBase setting, IList list, float windowYmax, int rightColumnWidth)
        {
            var buttonText = ObjectToGuiContent($"{setting.Get()}");
            var dispRect = GUILayoutUtility.GetRect(buttonText, GUI.skin.button, GUILayout.ExpandWidth(false), GUILayout.MaxWidth(_instance.RightColumnWidth / 4f));

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

        private static void DrawRangeField(SettingEntryBase setting, int rightColumnWidth)
        {
            var value = setting.Get();
            var converted = (float)Convert.ToDouble(value, CultureInfo.InvariantCulture);
            var leftValue = (float)Convert.ToDouble(setting.AcceptableValueRange.Key, CultureInfo.InvariantCulture);
            var rightValue = (float)Convert.ToDouble(setting.AcceptableValueRange.Value, CultureInfo.InvariantCulture);

            var result = GUILayout.HorizontalSlider(converted, leftValue, rightValue, GUILayout.ExpandWidth(false), GUILayout.MaxWidth(rightColumnWidth));
            if (Math.Abs(result - converted) > Mathf.Abs(rightValue - leftValue) / 1000)
            {
                var newValue = Convert.ChangeType(result, setting.SettingType, CultureInfo.InvariantCulture);
                setting.Set(newValue);
            }

            if (setting.ShowRangeAsPercent == true)
            {
                DrawCenteredLabel(Mathf.Round(100 * Mathf.Abs(result - leftValue) / Mathf.Abs(rightValue - leftValue)) + "%", GUILayout.Width(50));
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
                var result = GUILayout.TextField(text, GUILayout.MaxWidth(rightColumnWidth));

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
                    var result = GUILayout.TextField(value, GUILayout.MaxWidth(rightColumnWidth));
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
                if (GUIHelper.CreateButtonWithColor("Cancel", ConfigurationManager._cancelButtonColor.Value, ImguiUtils.buttonStyle, GUILayout.ExpandWidth(false)))
                    _currentKeyboardShortcutToSet = null;
            }
            else
            {
                var acceptableValues = setting.AcceptableValues?.Length > 1 ? setting.AcceptableValues : Enum.GetValues(setting.SettingType);
                DrawComboboxField(setting, acceptableValues, _instance.SettingWindowRect.yMax, _instance.RightColumnWidth);


                if (GUIHelper.CreateButtonWithColor(new GUIContent("Set...", null, "Set the key by pressing any key on your keyboard."), ImguiUtils.buttonStyle, default, GUILayout.ExpandWidth(false)))
                    _currentKeyboardShortcutToSet = setting;
            }
        }

        private static void DrawKeyboardShortcut(SettingEntryBase setting)
        {
            if (ReferenceEquals(_currentKeyboardShortcutToSet, setting))
            {
                GUIHelper.CreateLabelWithColor("Press any key combination", default, ImguiUtils.textStyle, GUILayout.ExpandWidth(true));
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

                if (GUIHelper.CreateButtonWithColor("Cancel", ConfigurationManager._cancelButtonColor.Value, ImguiUtils.buttonStyle, GUILayout.ExpandWidth(false)))
                    _currentKeyboardShortcutToSet = null;
            }
            else
            {
                if (GUILayout.Button($"   {setting.Get().ToString()}   ", GUILayout.ExpandWidth(false)))
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


        /// <summary>
        /// Draws a color setting with:
        ///  - Hex field
        ///  - Sliders for R/G/B/A
        ///  - Cached texture preview
        ///  - "Pick Color..." button to open popup
        /// </summary>
        private static void DrawColor(SettingEntryBase obj)
        {
            Color settingColor = (Color)obj.Get();
            GUILayout.BeginVertical(GUI.skin.box);
            {
                SettingFieldDrawer.ColorCacheEntry colorCacheEntry;
                GUILayout.BeginHorizontal();
                {
                    DrawHexField(ref settingColor);
                    GUILayout.Space(3f);
                    GUIHelper.BeginColor(settingColor);
                    GUILayout.Label(string.Empty, GUILayout.ExpandWidth(true));

                    if (!_colorCache.TryGetValue(obj, out colorCacheEntry))
                    {
                        colorCacheEntry = new ColorCacheEntry()
                        {
                            Tex = new Texture2D(40, 10, TextureFormat.ARGB32, false),
                            Last = settingColor
                        };
                        colorCacheEntry.Tex.FillTexture(settingColor);
                        _colorCache[obj] = colorCacheEntry;
                    }

                    if (Event.current.type == EventType.Repaint)
                        GUI.DrawTexture(GUILayoutUtility.GetLastRect(), (Texture)colorCacheEntry.Tex);
                    GUIHelper.EndColor();
                    GUILayout.Space(3f);
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(4f);
                /*GUILayout.BeginHorizontal();
                {
                    /*if (GUILayout.Button("Pick Color...", GUILayout.ExpandWidth(false)))
                    {
                        _tempPickedColor = settingColor;
                        _currentColorPickerSetting = obj;
                        _colorPickerWindowRect.position = new Vector2(Screen.width / 2f - _colorPickerWindowRect.width / 2f, Screen.height / 2f - _colorPickerWindowRect.height / 2f);
                    }#1#
                    GUILayout.Space(3f);
                    if (GUILayout.Button("Reset", GUILayout.ExpandWidth(false)))
                    {
                        settingColor = Color.white;
                        obj.Set((object)settingColor);
                        colorCacheEntry.Tex.FillTexture(settingColor);
                        colorCacheEntry.Last = settingColor;
                    }
                }
                GUILayout.EndHorizontal();*/
                GUILayout.BeginHorizontal();
                {
                    DrawColorField("Red", ref settingColor, ref settingColor.r);
                    GUILayout.Space(3f);
                    DrawColorField("Green", ref settingColor, ref settingColor.g);
                    GUILayout.Space(3f);
                    DrawColorField("Blue", ref settingColor, ref settingColor.b);
                    GUILayout.Space(3f);
                    DrawColorField("Alpha", ref settingColor, ref settingColor.a);
                    if (settingColor != colorCacheEntry.Last)
                    {
                        obj.Set((object)settingColor);
                        colorCacheEntry.Tex.FillTexture(settingColor);
                        colorCacheEntry.Last = settingColor;
                    }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }

        /// <summary>Ensure we have a 128×128 hue-sat texture allocated.</summary>
        private static void EnsureHueSatTexture()
        {
            if (_hueSatTex == null)
            {
                _hueSatTex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
                _hueSatTex.wrapMode = TextureWrapMode.Clamp;
            }
        }

        /// <summary>
        /// Fill _hueSatTex so horizontally is Hue, vertically is Sat (top = S=1, bottom = S=0),
        /// at a fixed brightness = V param. E.g. V=1 means top row goes from H=0..1 with S=1
        /// </summary>
        private static void RecolorHueSatTexture(float V)
        {
            if (_hueSatTex == null) return;
            var width = _hueSatTex.width;
            var height = _hueSatTex.height;
            Color32[] pixels = new Color32[width * height];

            // row 0 => S=1, row (height-1) => S=0
            for (int y = 0; y < height; ++y)
            {
                float s = 1f - (float)y / (height - 1);
                for (int x = 0; x < width; ++x)
                {
                    float h = (float)x / (width - 1);
                    // convert H,S,V to color
                    Color c = Color.HSVToRGB(h, s, V);
                    pixels[y * width + x] = c;
                }
            }

            _hueSatTex.SetPixels32(pixels);
            _hueSatTex.Apply(false);
        }


        private static Texture2D _hueSatTex;
        private static bool _isDraggingHSRect = false;
        private static Vector2 _hsDragPos; // Where user is dragging in the hue-sat rect
        


        /// <summary>
        /// Renders label + textfield + slider for the given color component (R/G/B/A).
        /// </summary>
        private static void DrawColorField(string fieldLabel, ref Color settingColor, ref float settingValue)
        {
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUIHelper.BeginColor(ConfigurationManager._fontColor.Value);
            GUILayout.Label(fieldLabel, GUILayout.ExpandWidth(true));
            GUIHelper.EndColor();
            GUILayout.TextField(settingValue.ToString((IFormatProvider)CultureInfo.CurrentCulture), GUILayout.MaxWidth(45f), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            GUILayout.Space(2f);
            GUIHelper.BeginColor(ConfigurationManager._lightGreySlidersColor.Value);
            switch (fieldLabel)
            {
                case "Red":
                    settingColor.r = GUILayout.HorizontalSlider(settingValue, 0.0f, 1f, GUILayout.ExpandWidth(true));
                    break;
                case "Green":
                    settingColor.g = GUILayout.HorizontalSlider(settingValue, 0.0f, 1f, GUILayout.ExpandWidth(true));
                    break;
                case "Blue":
                    settingColor.b = GUILayout.HorizontalSlider(settingValue, 0.0f, 1f, GUILayout.ExpandWidth(true));
                    break;
                case "Alpha":
                    settingColor.a = GUILayout.HorizontalSlider(settingValue, 0.0f, 1f, GUILayout.ExpandWidth(true));
                    break;
            }

            GUIHelper.EndColor();
            GUILayout.EndVertical();
        }

        /// <summary>
        /// Allows editing via hex, like #RRGGBB or #RRGGBBAA
        /// </summary>
        private static void DrawHexField(ref Color value)
        {
            string text = "#" + ColorUtility.ToHtmlStringRGBA(value);
            string htmlString = GUILayout.TextField(text, GUILayout.Width(90f), GUILayout.ExpandWidth(false));
            if (htmlString == text || !ColorUtility.TryParseHtmlString(htmlString, out Color color))
                return;
            value = color;
        }

        /// <summary>
        /// Fill the texture with the given color, showing a checkerboard if alpha < 1.
        /// (Port of Code2's FillTex logic)
        /// </summary>
        private static void FillTextureWithColor(Color color, Texture2D tex)
        {
            if (color.a < 1f) tex.FillTextureCheckerboard();
            tex.FillTexture(color);
        }

        private sealed class FloatConfigCacheEntry
        {
            public float Value = 0.0f;
            public string FieldText = string.Empty;
            public Color FieldColor = Color.clear;
        }

        private sealed class ColorCacheEntry
        {
            public Color Last;
            public Texture2D Tex;
        }
    }
}