// Made by MarC0 / ManlyMarco
// Copyright 2018 GNU General Public License v3.0

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using BepInEx.Configuration;

namespace ConfigurationManager
{
    /// <summary>
    /// An easy way to let user configure how a plugin behaves without the need to make your own GUI. The user can change any of the settings you expose, even keyboard shortcuts.
    /// https://github.com/ManlyMarco/BepInEx.ConfigurationManager
    /// </summary>
    [BepInPlugin(GUID, "Configuration Manager", Version)]
    [Browsable(false)]
    public class ConfigurationManager : BaseUnityPlugin
    {
        /// <summary>
        /// GUID of this plugin
        /// </summary>
        public const string GUID = "com.bepis.bepinex.configurationmanager";

        /// <summary>
        /// Version constant
        /// </summary>
        public const string Version = "14.1";

        //todo use?
        private static readonly string KeyboarShortcutExplanation =
            "The first key is the main key, while the rest are modifiers.\n" +
            "The shortcut will only fire when you press \n" +
            "the main key while all modifiers are already pressed.";

        internal static new ManualLogSource Logger;
        private static SettingFieldDrawer _fieldDrawer;

        private const string SearchBoxName = "searchBox";
        private const int WindowId = -68;
        private bool _focusSearchBox;

        /// <summary>
        /// Event fired every time the manager window is shown or hidden.
        /// </summary>
        public event EventHandler<ValueChangedEventArgs<bool>> DisplayingWindowChanged;

        /// <summary>
        /// Disable the hotkey check used by config manager. If enabled you have to set DisplayingWindow to show the manager.
        /// </summary>
        public bool OverrideHotkey;

        private bool _displayingWindow;

        private string _modsWithoutSettings;

        private List<SettingEntryBase> _allSettings;
        private List<PluginSettingsData> _filteredSetings;

        internal Rect SettingWindowRect { get; private set; }
        private Rect _screenRect;
        private Vector2 _settingWindowScrollPos;
        private int _tipsHeight;

        private CursorLockMode _previousCursorLockState;
        private bool _previousCursorVisible;

        internal static Texture2D TooltipBg { get; private set; }
        internal static Texture2D WindowBackground { get; private set; }

        internal int LeftColumnWidth { get; private set; }
        internal int RightColumnWidth { get; private set; }

        private readonly ConfigEntry<bool> _showAdvanced;
        private readonly ConfigEntry<bool> _showKeybinds;
        private readonly ConfigEntry<bool> _showSettings;
        private readonly ConfigEntry<BepInEx.Configuration.KeyboardShortcut> _keybind;
        private readonly ConfigEntry<bool> _hideSingleSection;
        private readonly ConfigEntry<bool> _pluginConfigCollapsedDefault;
        private bool _showDebug;
        private string _searchString = string.Empty;

        /// <inheritdoc />
        public ConfigurationManager()
        {
            Logger = base.Logger;
            _fieldDrawer = new SettingFieldDrawer(this);

            _showAdvanced = Config.AddSetting("Filtering", "Show advanced", false);
            _showKeybinds = Config.AddSetting("Filtering", "Show keybinds", true);
            _showSettings = Config.AddSetting("Filtering", "Show settings", true);
            _keybind = Config.AddSetting("General", "Show config manager", new BepInEx.Configuration.KeyboardShortcut(KeyCode.F1),
                new ConfigDescription("The shortcut used to toggle the config manager window on and off.\n" +
                                      "The key can be overridden by a game-specific plugin if necessary, in that case this setting is ignored."));
            _hideSingleSection = Config.AddSetting("General", "Hide single sections", false, new ConfigDescription("Show section title for plugins with only one section"));
            _pluginConfigCollapsedDefault = Config.AddSetting("General", "Plugin collapsed default", true, new ConfigDescription("If set to true plugins will be collapsed when opening the configuration manager window"));
        }

        /// <summary>
        /// Is the config manager main window displayed on screen
        /// </summary>
        public bool DisplayingWindow
        {
            get => _displayingWindow;
            set
            {
                if (_displayingWindow == value) return;
                _displayingWindow = value;

                SettingFieldDrawer.ClearCache();

                if (_displayingWindow)
                {
                    CalculateWindowRect();

                    BuildSettingList();

                    _focusSearchBox = true;

                    _previousCursorLockState = Cursor.lockState;
                    _previousCursorVisible = Cursor.visible;
                }
                else
                {
                    if (!_previousCursorVisible || _previousCursorLockState != CursorLockMode.None)
                    {
                        Cursor.lockState = _previousCursorLockState;
                        Cursor.visible = _previousCursorVisible;
                    }
                }

                DisplayingWindowChanged?.Invoke(this, new ValueChangedEventArgs<bool>(value));
            }
        }

        /// <summary>
        /// Register a custom setting drawer for a given type. The action is ran in OnGui in a single setting slot.
        /// Do not use any Begin / End layout methods, and avoid raising height from standard.
        /// </summary>
        public static void RegisterCustomSettingDrawer(Type settingType, Action<SettingEntryBase> onGuiDrawer)
        {
            if (settingType == null) throw new ArgumentNullException(nameof(settingType));
            if (onGuiDrawer == null) throw new ArgumentNullException(nameof(onGuiDrawer));

            if (SettingFieldDrawer.SettingDrawHandlers.ContainsKey(settingType))
                Logger.LogWarning("Tried to add a setting drawer for type " + settingType.FullName + " while one already exists.");
            else
                SettingFieldDrawer.SettingDrawHandlers[settingType] = onGuiDrawer;
        }

        private void BuildSettingList()
        {
            SettingSearcher.CollectSettings(out var results, out var modsWithoutSettings, _showDebug);

            //todo set collapsed state

            _modsWithoutSettings = string.Join(", ", modsWithoutSettings.Select(x => x.TrimStart('!')).OrderBy(x => x).ToArray());
            _allSettings = results.ToList();

            BuildFilteredSettingList();
        }

        private void BuildFilteredSettingList()
        {
            IEnumerable<SettingEntryBase> results = _allSettings;

            if (!string.IsNullOrEmpty(_searchString))
            {
                results = results.Where(x => ContainsSearchString(x, _searchString));
            }
            else
            {
                if (!_showAdvanced.Value)
                    results = results.Where(x => x.IsAdvanced != true);
                if (!_showKeybinds.Value)
                    results = results.Where(x => !IsKeyboardShortcut(x));
                if (!_showSettings.Value)
                    results = results.Where(x => x.IsAdvanced == true || IsKeyboardShortcut(x));
            }

            const string shortcutsCatName = "Keyboard shortcuts";
            string GetCategory(SettingEntryBase eb)
            {
                // Legacy behavior
                if (eb.SettingType == typeof(BepInEx.KeyboardShortcut)) return shortcutsCatName;
                return eb.Category;
            }

            var settingsAreCollapsed = _pluginConfigCollapsedDefault.Value;

            _filteredSetings = results
                .GroupBy(x => x.PluginInfo)
                .Select(pluginSettings =>
                {
                    var categories = pluginSettings
                        .GroupBy(GetCategory)
                        .OrderBy(x => string.Equals(x.Key, shortcutsCatName, StringComparison.Ordinal))
                        .ThenBy(x => x.Key)
                        .Select(x => new PluginSettingsData.PluginSettingsGroupData { Name = x.Key, Settings = x.OrderByDescending(set => set.Order).ThenBy(set => set.DispName).ToList() });

                    return new PluginSettingsData { Info = pluginSettings.Key, Categories = categories.ToList(), Collapsed = settingsAreCollapsed };
                })
                .OrderBy(x => x.Info.Name)
                .ToList();
        }

        private static bool IsKeyboardShortcut(SettingEntryBase x)
        {
            return x.SettingType == typeof(BepInEx.KeyboardShortcut) || x.SettingType == typeof(BepInEx.Configuration.KeyboardShortcut);
        }

        private static bool ContainsSearchString(SettingEntryBase setting, string searchString)
        {
            foreach (var target in new[]
            {
                setting.PluginInfo.Name,
                setting.PluginInfo.GUID,
                setting.DispName,
                setting.Category,
                setting.Description ,
                setting.DefaultValue?.ToString(),
                setting.Get()?.ToString()
            })
            {
                if (target != null && target.IndexOf(searchString, StringComparison.InvariantCultureIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private void CalculateWindowRect()
        {
            var width = Mathf.Min(Screen.width, 650);
            var height = Screen.height < 560 ? Screen.height : Screen.height - 100;
            var offsetX = Mathf.RoundToInt((Screen.width - width) / 2f);
            var offsetY = Mathf.RoundToInt((Screen.height - height) / 2f);
            SettingWindowRect = new Rect(offsetX, offsetY, width, height);

            _screenRect = new Rect(0, 0, Screen.width, Screen.height);

            LeftColumnWidth = Mathf.RoundToInt(SettingWindowRect.width / 2.5f);
            RightColumnWidth = (int)SettingWindowRect.width - LeftColumnWidth - 115;
        }

        private void OnGUI()
        {
            if (DisplayingWindow)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                if (GUI.Button(_screenRect, string.Empty, GUI.skin.box) &&
                    !SettingWindowRect.Contains(Input.mousePosition))
                    DisplayingWindow = false;

                GUI.Box(SettingWindowRect, GUIContent.none, new GUIStyle { normal = new GUIStyleState { background = WindowBackground } });

                GUILayout.Window(WindowId, SettingWindowRect, SettingsWindow, "Plugin / mod settings");

                if (!SettingFieldDrawer.SettingKeyboardShortcut)
                    Input.ResetInputAxes();
            }
        }

        private static void DrawTooltip(Rect area)
        {
            if (!string.IsNullOrEmpty(GUI.tooltip))
            {
                var currentEvent = Event.current;

                var style = new GUIStyle
                {
                    normal = new GUIStyleState { textColor = Color.white, background = TooltipBg },
                    wordWrap = true,
                    alignment = TextAnchor.MiddleCenter
                };

                const int width = 400;
                var height = style.CalcHeight(new GUIContent(GUI.tooltip), 400) + 10;

                var x = currentEvent.mousePosition.x + width > area.width
                    ? area.width - width
                    : currentEvent.mousePosition.x;

                var y = currentEvent.mousePosition.y + 25 + height > area.height
                    ? currentEvent.mousePosition.y - height
                    : currentEvent.mousePosition.y + 25;

                GUI.Box(new Rect(x, y, width, height), GUI.tooltip, style);
            }
        }

        private void SettingsWindow(int id)
        {
            DrawWindowHeader();

            _settingWindowScrollPos = GUILayout.BeginScrollView(_settingWindowScrollPos, false, true);

            var scrollPosition = _settingWindowScrollPos.y;
            var scrollHeight = SettingWindowRect.height;

            GUILayout.BeginVertical();
            {
                if (string.IsNullOrEmpty(SearchString))
                {
                    DrawTips();

                    if (_tipsHeight == 0 && Event.current.type == EventType.Repaint)
                        _tipsHeight = (int)GUILayoutUtility.GetLastRect().height;
                }

                var currentHeight = _tipsHeight;

                foreach (var plugin in _filteredSetings)
                {
                    var visible = plugin.Height == 0 || currentHeight + plugin.Height >= scrollPosition && currentHeight <= scrollPosition + scrollHeight;

                    if (visible)
                    {
                        try
                        {
                            DrawSinglePlugin(plugin);
                        }
                        catch (ArgumentException ex)
                        {
                            // Needed to avoid GUILayout: Mismatched LayoutGroup.Repaint crashes on large lists
                            Logger.LogDebug(ex);
                        }

                        if (plugin.Height == 0 && Event.current.type == EventType.Repaint)
                            plugin.Height = (int)GUILayoutUtility.GetLastRect().height;
                    }
                    else
                    {
                        try
                        {
                            GUILayout.Space(plugin.Height);
                        }
                        catch (ArgumentException ex)
                        {
                            // Needed to avoid GUILayout: Mismatched LayoutGroup.Repaint crashes on large lists
                            Logger.LogDebug(ex);
                        }
                    }

                    currentHeight += plugin.Height;
                }

                if (_showDebug)
                {
                    GUILayout.Space(10);
                    GUILayout.Label("Plugins with no options available: " + _modsWithoutSettings);
                }
                else
                {
                    // Always leave some space in case there's a dropdown box at the very bottom of the list
                    GUILayout.Space(70);
                }
            }
            GUILayout.EndVertical();
            GUILayout.EndScrollView();

            if (!SettingFieldDrawer.DrawCurrentDropdown())
                DrawTooltip(SettingWindowRect);
        }

        private void DrawTips()
        {
            GUILayout.BeginHorizontal();
            {
                GUILayout.Label("Tip: Click plugin names to expand. Click setting and group names to see their descriptions.");

                GUILayout.FlexibleSpace();

                if (GUILayout.Button(_pluginConfigCollapsedDefault.Value ? "Expand" : "Collapse", GUILayout.ExpandWidth(false)))
                {
                    var newValue = !_pluginConfigCollapsedDefault.Value;
                    _pluginConfigCollapsedDefault.Value = newValue;
                    foreach (var plugin in _filteredSetings)
                        plugin.Collapsed = newValue;
                }
            }
            GUILayout.EndHorizontal();
        }

        private void DrawWindowHeader()
        {
            GUILayout.BeginHorizontal(GUI.skin.box);
            {
                GUILayout.Label("Show: ", GUILayout.ExpandWidth(false));

                GUI.enabled = SearchString == string.Empty;

                var newVal = GUILayout.Toggle(_showSettings.Value, "Normal settings");
                if (_showSettings.Value != newVal)
                {
                    _showSettings.Value = newVal;
                    BuildFilteredSettingList();
                }

                newVal = GUILayout.Toggle(_showKeybinds.Value, "Keyboard shortcuts");
                if (_showKeybinds.Value != newVal)
                {
                    _showKeybinds.Value = newVal;
                    BuildFilteredSettingList();
                }

                newVal = GUILayout.Toggle(_showAdvanced.Value, "Advanced settings");
                if (_showAdvanced.Value != newVal)
                {
                    _showAdvanced.Value = newVal;
                    BuildFilteredSettingList();
                }

                GUI.enabled = true;

                newVal = GUILayout.Toggle(_showDebug, "Debug mode");
                if (_showDebug != newVal)
                {
                    _showDebug = newVal;
                    BuildSettingList();
                }

                if (GUILayout.Button("Log", GUILayout.ExpandWidth(false)))
                {
                    try { Utilities.Utils.OpenLog(); }
                    catch (SystemException ex) { Logger.Log(LogLevel.Message | LogLevel.Error, ex.Message); }
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUI.skin.box);
            {
                GUILayout.Label("Search settings: ", GUILayout.ExpandWidth(false));

                GUI.SetNextControlName(SearchBoxName);
                SearchString = GUILayout.TextField(SearchString, GUILayout.ExpandWidth(true));

                if (_focusSearchBox)
                {
                    GUI.FocusWindow(WindowId);
                    GUI.FocusControl(SearchBoxName);
                    _focusSearchBox = false;
                }

                if (GUILayout.Button("Clear", GUILayout.ExpandWidth(false)))
                    SearchString = string.Empty;
            }
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// String currently entered into the search box
        /// </summary>
        public string SearchString
        {
            get => _searchString;
            private set
            {
                if (string.IsNullOrEmpty(_searchString))
                    _searchString = string.Empty;

                if (_searchString == value)
                    return;

                _searchString = value;

                BuildFilteredSettingList();
            }
        }

        private void DrawSinglePlugin(PluginSettingsData plugin)
        {
            GUILayout.BeginVertical(GUI.skin.box);

            var categoryHeader = _showDebug ?
                new GUIContent($"{plugin.Info.Name.TrimStart('!')} {plugin.Info.Version}", "GUID: " + plugin.Info.GUID) :
                new GUIContent($"{plugin.Info.Name.TrimStart('!')} {plugin.Info.Version}");

            var isSearching = !string.IsNullOrEmpty(SearchString);

            if (SettingFieldDrawer.DrawPluginHeader(categoryHeader, plugin.Collapsed && !isSearching) && !isSearching)
                plugin.Collapsed = !plugin.Collapsed;

            if (isSearching || !plugin.Collapsed)
            {
                foreach (var category in plugin.Categories)
                {
                    if (!string.IsNullOrEmpty(category.Name))
                    {
                        if (plugin.Categories.Count > 1 || !_hideSingleSection.Value)
                            SettingFieldDrawer.DrawCategoryHeader(category.Name);
                    }

                    foreach (var setting in category.Settings)
                    {
                        DrawSingleSetting(setting);
                        GUILayout.Space(2);
                    }
                }
            }

            GUILayout.EndVertical();
        }

        private void DrawSingleSetting(SettingEntryBase setting)
        {
            GUILayout.BeginHorizontal();
            {
                try
                {
                    DrawSettingName(setting);
                    _fieldDrawer.DrawSettingValue(setting);
                    DrawDefaultButton(setting);
                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Error, $"[ConfigManager] Failed to draw setting {setting.DispName} - {ex}");
                    GUILayout.Label("Failed to draw this field, check log for details.");
                }
            }
            GUILayout.EndHorizontal();
        }

        private void DrawSettingName(SettingEntryBase setting)
        {
            GUILayout.Label(new GUIContent(setting.DispName.TrimStart('!'), setting.Description),
                GUILayout.Width(LeftColumnWidth), GUILayout.MaxWidth(LeftColumnWidth));
        }

        private static void DrawDefaultButton(SettingEntryBase setting)
        {
            if (setting.HideDefaultButton) return;

            bool DrawDefaultButton()
            {
                GUILayout.Space(5);
                return GUILayout.Button("Reset", GUILayout.ExpandWidth(false));
            }

            if (setting.DefaultValue != null)
            {
                if (DrawDefaultButton())
                    setting.Set(setting.DefaultValue);
            }
            else if (setting is LegacySettingEntry legacySetting && legacySetting.Wrapper != null)
            {
                var method = legacySetting.Wrapper.GetType().GetMethod("Clear", BindingFlags.Instance | BindingFlags.Public);
                if (method != null && DrawDefaultButton())
                    method.Invoke(legacySetting.Wrapper, null);
            }
            else if (setting.SettingType.IsClass)
            {
                if (DrawDefaultButton())
                    setting.Set(null);
            }
        }

        private void Start()
        {
            var background = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            background.SetPixel(0, 0, Color.black);
            background.Apply();
            TooltipBg = background;

            var windowBackground = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            windowBackground.SetPixel(0, 0, new Color(0.5f, 0.5f, 0.5f, 1));
            windowBackground.Apply();
            WindowBackground = windowBackground;

            // Check if user has permissions to write config files to disk
            try { Config.Save(); }
            catch (IOException ex) { Logger.Log(LogLevel.Message | LogLevel.Warning, "WARNING: Failed to write to config directory, expect issues!\nError message:" + ex.Message); }
            catch (UnauthorizedAccessException ex) { Logger.Log(LogLevel.Message | LogLevel.Warning, "WARNING: Permission denied to write to config directory, expect issues!\nError message:" + ex.Message); }
        }

        private void Update()
        {
            if (DisplayingWindow)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (OverrideHotkey) return;

            if (_keybind.Value.IsDown())
            {
                DisplayingWindow = !DisplayingWindow;
            }
        }

        private void LateUpdate()
        {
            if (DisplayingWindow)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private sealed class PluginSettingsData
        {
            public BepInPlugin Info;
            public List<PluginSettingsGroupData> Categories;
            private bool _collapsed;

            public bool Collapsed
            {
                get => _collapsed;
                set
                {
                    _collapsed = value;
                    Height = 0;
                }
            }

            public sealed class PluginSettingsGroupData
            {
                public string Name;
                public List<SettingEntryBase> Settings;
            }

            public int Height { get; set; }
        }
    }
}
