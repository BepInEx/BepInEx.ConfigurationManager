﻿// Made by MarC0 / ManlyMarco
// Copyright 2018 GNU General Public License v3.0

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using ConfigurationManager.Utilities;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using System.ComponentModel;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ConfigurationManager
{
    /// <summary>
    /// An easy way to let user configure how a plugin behaves without the need to make your own GUI. The user can change any of the settings you expose, even keyboard shortcuts.
    /// https://github.com/ManlyMarco/BepInEx.ConfigurationManager
    /// </summary>
    [BepInPlugin(GUID, "Configuration Manager", Version)]
    [Browsable(false)]
    public class ConfigurationManager : BasePlugin
    {
        private class Behaviour : MonoBehaviour
        {
            internal static ConfigurationManager Plugin;
            private void Start() => Plugin.Start();
            private void Update() => Plugin.Update();
            private void LateUpdate() => Plugin.LateUpdate();
            private void OnGUI() => Plugin.OnGUI();
        }

        /// <summary>
        /// GUID of this plugin
        /// </summary>
        public const string GUID = "com.bepis.bepinex.configurationmanager";

        /// <summary>
        /// Version constant
        /// </summary>
        public const string Version = "18.0";

        internal static ManualLogSource Logger;
        internal static ConfigurationManager Instance;
        private static SettingFieldDrawer _fieldDrawer;

        private static readonly Color _advancedSettingColor = new Color(1, 0.95f, 0.67f, 1);
        private const int WindowId = -68;

        private const string SearchBoxName = "searchBox";
        private bool _focusSearchBox;
        private string _searchString = string.Empty;

        /// <summary>
        /// Event fired every time the manager window is shown or hidden.
        /// </summary>
        public event EventHandler<ValueChangedEventArgs<bool>> DisplayingWindowChanged;

        /// <summary>
        /// Disable the hotkey check used by config manager. If enabled you have to set <see cref="DisplayingWindow"/> to show the manager.
        /// </summary>
        public bool OverrideHotkey;

        private bool _displayingWindow;

        private string _modsWithoutSettings;

        private List<SettingEntryBase> _allSettings;
        private List<PluginSettingsData> _filteredSetings = new List<PluginSettingsData>();

        internal Rect SettingWindowRect { get; private set; }
        private int _dragButton = 0;

        private bool _tipsPluginHeaderWasClicked;

        private Rect _screenRect;
        private Vector2 _settingWindowScrollPos;
        private int _tipsHeight;

        private CursorLockMode _previousCursorLockState;
        private bool _previousCursorVisible;

        internal int LeftColumnWidth { get; private set; }
        internal int RightColumnWidth { get; private set; }

        private readonly ConfigEntry<bool> _showAdvanced;
        private readonly ConfigEntry<bool> _showKeybinds;
        private readonly ConfigEntry<bool> _showSettings;
        private readonly ConfigEntry<KeyboardShortcut> _keybind;
        private readonly ConfigEntry<bool> _hideSingleSection;
        private readonly ConfigEntry<bool> _pluginConfigCollapsedDefault;
        private bool _showDebug;

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetKeyboardState(long[] lpKeyState);

        /// <inheritdoc />
        public ConfigurationManager()
        {
            Logger = Log;
            Instance = this;
            _fieldDrawer = new SettingFieldDrawer(this);

            _showAdvanced = Config.Bind("Filtering", "Show advanced", false);
            _showKeybinds = Config.Bind("Filtering", "Show keybinds", true);
            _showSettings = Config.Bind("Filtering", "Show settings", true);
            _keybind = Config.Bind("General", "Show config manager", new KeyboardShortcut(KeyCode.F1),
                new ConfigDescription("The shortcut used to toggle the config manager window on and off.\n" +
                                      "The key can be overridden by a game-specific plugin if necessary, in that case this setting is ignored."));
            _hideSingleSection = Config.Bind("General", "Hide single sections", false, new ConfigDescription("Show section title for plugins with only one section"));
            _pluginConfigCollapsedDefault = Config.Bind("General", "Plugin collapsed default", true, new ConfigDescription("If set to true plugins will be collapsed when opening the configuration manager window"));
        }

        /// <inheritdoc/>
        public override void Load()
        {
            try
            {
                Behaviour.Plugin = this;
                ClassInjector.RegisterTypeInIl2Cpp<Behaviour>();
                GameObject gameObject = new GameObject(nameof(ConfigurationManager));
                gameObject.hideFlags |= HideFlags.HideAndDontSave;
                UnityEngine.Object.DontDestroyOnLoad(gameObject);
                gameObject.AddComponent<Behaviour>();
            }
            catch
            {
                Log.LogError("FAILED to Register Il2Cpp Type: " + nameof(ConfigurationManager) + "!");
            }
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
                        SetUnlockCursor(_previousCursorLockState, _previousCursorVisible);
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

        /// <summary>
        /// Rebuild the setting list. Use to update the config manager window if config settings were removed or added while it was open.
        /// </summary>
        public void BuildSettingList()
        {
            SettingSearcher.CollectSettings(out var results, out var modsWithoutSettings, _showDebug);

            _modsWithoutSettings = string.Join(", ", modsWithoutSettings.Select(x => x.TrimStart('!')).OrderBy(x => x).ToArray());
            _allSettings = results.ToList();

            BuildFilteredSettingList();
        }

        private void BuildFilteredSettingList()
        {
            IEnumerable<SettingEntryBase> results = _allSettings;

            var searchStrings = SearchString.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (searchStrings.Length > 0)
            {
                results = results.Where(x => ContainsSearchString(x, searchStrings));
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

            var settingsAreCollapsed = _pluginConfigCollapsedDefault.Value;

            var nonDefaultCollpasingStateByPluginName = new HashSet<string>();
            foreach (var pluginSetting in _filteredSetings)
            {
                if (pluginSetting.Collapsed != settingsAreCollapsed)
                {
                    nonDefaultCollpasingStateByPluginName.Add(pluginSetting.Info.Name);
                }
            }

            _filteredSetings = results
                .GroupBy(x => x.PluginInfo)
                .Select(pluginSettings =>
                {
                    var categories = pluginSettings
                        .GroupBy(eb => eb.Category)
                        .OrderBy(x => string.Equals(x.Key, shortcutsCatName, StringComparison.Ordinal))
                        .ThenBy(x => x.Key)
                        .Select(x => new PluginSettingsData.PluginSettingsGroupData { Name = x.Key, Settings = x.OrderByDescending(set => set.Order).ThenBy(set => set.DispName).ToList() });

                    var website = Utils.GetWebsite(pluginSettings.First().PluginInstance);

                    return new PluginSettingsData
                    {
                        Info = pluginSettings.Key,
                        Categories = categories.ToList(),
                        Collapsed = nonDefaultCollpasingStateByPluginName.Contains(pluginSettings.Key.Name) ? !settingsAreCollapsed : settingsAreCollapsed,
                        Website = website
                    };
                })
                .OrderBy(x => x.Info.Name)
                .ToList();
        }

        private static bool IsKeyboardShortcut(SettingEntryBase x)
        {
            return x.SettingType == typeof(KeyboardShortcut) || x.SettingType == typeof(KeyCode);
        }

        private static bool ContainsSearchString(SettingEntryBase setting, string[] searchStrings)
        {
            var combinedSearchTarget = setting.PluginInfo.Name + "\n" +
                                       setting.PluginInfo.GUID + "\n" +
                                       setting.DispName + "\n" +
                                       setting.Category + "\n" +
                                       setting.Description + "\n" +
                                       setting.DefaultValue + "\n" +
                                       setting.Get();

            return searchStrings.All(s => combinedSearchTarget.IndexOf(s, StringComparison.InvariantCultureIgnoreCase) >= 0);
        }

        private void CalculateWindowRect()
        {
            var width = Mathf.Min(Screen.width, 650);
            var height = Screen.height < 560 ? Screen.height : Screen.height - 100;
            var offsetX = Mathf.RoundToInt((Screen.width - width) / 2);
            var offsetY = Mathf.RoundToInt((Screen.height - height) / 2);
            SettingWindowRect = new Rect(offsetX, offsetY, width, height);

            _screenRect = new Rect(0, 0, Screen.width, Screen.height);

            LeftColumnWidth = Mathf.RoundToInt(SettingWindowRect.width / 2.5f);
            RightColumnWidth = (int)SettingWindowRect.width - LeftColumnWidth - 115;

            _dragButton = 0;
        }

        private void OnGUI()
        {
            if (DisplayingWindow)
            {
                const int VK_LBUTTON = 0x01;
                const int VK_RBUTTON = 0x02;
                const int VK_MBUTTON = 0x04;
                const long VK_LBUTTON_PRESSED = 0x80L << (VK_LBUTTON * 8);
                const long VK_RBUTTON_PRESSED = 0x80L << (VK_RBUTTON * 8);
                const long VK_MBUTTON_PRESSED = 0x80L << (VK_MBUTTON * 8);
                const long VK_MOUSE_PRESSED = VK_LBUTTON_PRESSED | VK_RBUTTON_PRESSED | VK_MBUTTON_PRESSED;

                SetUnlockCursor(CursorLockMode.None, true);

                // If the window hasn't been moved by the user yet, block the whole screen and use a solid background to make the window easier to see
                if (_dragButton == 0)
                {
                    if (GUI.Button(_screenRect, string.Empty, GUI.skin.box) &&
                    !SettingWindowRect.Contains(Input.mousePosition))
                        DisplayingWindow = false;

                    Utils.DrawWindowBackground(SettingWindowRect);
                }

                var newRect = GUILayout.Window(WindowId, SettingWindowRect, (GUI.WindowFunction)SettingsWindow, "Plugin / mod settings");

                if (_dragButton == 0)
                {
                    long[] keys = new long[32];
                    if (!GetKeyboardState(keys) || (keys[0] & VK_MOUSE_PRESSED) == 0)
                    {
                        if (SettingFieldDrawer.SettingKeyboardShortcut || !SettingWindowRect.Contains(Input.mousePosition))
                            return;
                    }
                    else if (newRect != SettingWindowRect)
                    {
                        SettingWindowRect = newRect;
                        if ((keys[0] & VK_LBUTTON_PRESSED) != 0)
                            _dragButton = VK_LBUTTON;
                        else if ((keys[0] & VK_RBUTTON_PRESSED) != 0)
                            _dragButton = VK_RBUTTON;
                        else
                            _dragButton = VK_MBUTTON;
                    }
                }
                else
                {
                    if (Application.isFocused && GetKeyState(_dragButton) < 0)
                        SettingWindowRect = newRect;
                    else
                        _dragButton = 0;
                }
                Input.ResetInputAxes();
            }
        }

        private static void DrawTooltip(Rect area)
        {
            string tooltip = GUI.tooltip;
            if (!string.IsNullOrEmpty(tooltip))
            {
                var style = GUI.skin.box.Instantiate();
                style.wordWrap = true;
                style.alignment = TextAnchor.MiddleCenter;

                GUIContent content = new GUIContent(tooltip);

                const int width = 400;
                var height = style.CalcHeight(content, 400) + 10;

                var mousePosition = Event.current.mousePosition;

                var x = mousePosition.x + width > area.width
                    ? area.width - width
                    : mousePosition.x;

                var y = mousePosition.y + 25 + height > area.height
                    ? mousePosition.y - height
                    : mousePosition.y + 25;

                Rect position = new Rect(x, y, width, height);
                Utils.DrawContolBackground(position, Color.black);
                style.Draw(position, content, -1);
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
                        catch (Il2CppException)
                        {
                            // Needed to avoid GUILayout: Mismatched LayoutGroup.Repaint crashes on large lists
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
                        catch (Il2CppException)
                        {
                            // Needed to avoid GUILayout: Mismatched LayoutGroup.Repaint crashes on large lists
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

            GUI.DragWindow();
        }

        private void DrawTips()
        {
            var tip = !_tipsPluginHeaderWasClicked
                ? "Tip: Click plugin names to expand. Click setting and group names to see their descriptions."
                : "Tip: You can drag this window to move it. It will stay open while you interact with the game.";

            if (tip != null)
            {
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label(tip);
                }
                GUILayout.EndHorizontal();
            }
        }

        private void DrawWindowHeader()
        {
            GUILayout.BeginHorizontal(GUI.skin.box);
            {
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

                var origColor = GUI.color;
                GUI.color = _advancedSettingColor;
                newVal = GUILayout.Toggle(_showAdvanced.Value, "Advanced settings");
                if (_showAdvanced.Value != newVal)
                {
                    _showAdvanced.Value = newVal;
                    BuildFilteredSettingList();
                }
                GUI.color = origColor;

                GUI.enabled = true;

                GUILayout.Space(8);

                newVal = GUILayout.Toggle(_showDebug, "Debug info");
                if (_showDebug != newVal)
                {
                    _showDebug = newVal;
                    BuildSettingList();
                }

                if (GUILayout.Button("Open Log"))
                {
                    try { Utils.OpenLog(); }
                    catch (SystemException ex) { Logger.Log(LogLevel.Message | LogLevel.Error, ex.Message); }
                }

                GUILayout.Space(8);

                if (GUILayout.Button("Close"))
                {
                    DisplayingWindow = false;
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUI.skin.box);
            {
                GUILayout.Label("Search: ", GUILayout.ExpandWidth(false));

                GUI.SetNextControlName(SearchBoxName);
                SearchString = GUILayout.TextField(SearchString, GUILayout.ExpandWidth(true));

                if (_focusSearchBox)
                {
                    GUI.FocusWindow(WindowId);
                    GUI.FocusControl(SearchBoxName);
                    _focusSearchBox = false;
                }

                GUILayout.Space(8);

                if (GUILayout.Button(_pluginConfigCollapsedDefault.Value ? "Expand All" : "Collapse All", GUILayout.ExpandWidth(false)))
                {
                    var newValue = !_pluginConfigCollapsedDefault.Value;
                    _pluginConfigCollapsedDefault.Value = newValue;
                    foreach (var plugin in _filteredSetings)
                        plugin.Collapsed = newValue;

                    _tipsPluginHeaderWasClicked = true;
                }
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
                if (value == null)
                    value = string.Empty;

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
                new GUIContent($"{plugin.Info.Name.TrimStart('!')} {plugin.Info.Version}", null, "GUID: " + plugin.Info.GUID) :
                new GUIContent($"{plugin.Info.Name.TrimStart('!')} {plugin.Info.Version}");

            var isSearching = !string.IsNullOrEmpty(SearchString);

            {
                var hasWebsite = plugin.Website != null;
                if (hasWebsite)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(29); // Same as the URL button to keep the plugin name centered
                }

                if (SettingFieldDrawer.DrawPluginHeader(categoryHeader, plugin.Collapsed && !isSearching) && !isSearching)
                {
                    _tipsPluginHeaderWasClicked = true;
                    plugin.Collapsed = !plugin.Collapsed;
                }

                if (hasWebsite)
                {
                    var origColor = GUI.color;
                    GUI.color = Color.gray;
                    if (GUILayout.Button(new GUIContent("URL", null, plugin.Website), GUI.skin.label, GUILayout.ExpandWidth(false)))
                        Utils.OpenWebsite(plugin.Website);
                    GUI.color = origColor;
                    GUILayout.EndHorizontal();
                }
            }

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
                    Logger.Log(LogLevel.Error, "Failed to draw setting " + setting.DispName + " - " + ex);
                    GUILayout.Label("Failed to draw this field, check log for details.");
                }
            }
            GUILayout.EndHorizontal();
        }

        private void DrawSettingName(SettingEntryBase setting)
        {
            if (setting.HideSettingName) return;

            var origColor = GUI.color;
            if (setting.IsAdvanced == true)
                GUI.color = _advancedSettingColor;

            GUILayout.Label(new GUIContent(setting.DispName.TrimStart('!'), null, setting.Description), GUILayout.Width(LeftColumnWidth), GUILayout.MaxWidth(LeftColumnWidth));

            GUI.color = origColor;
        }

        private static void DrawDefaultButton(SettingEntryBase setting)
        {
            if (setting.HideDefaultButton) return;

            object defaultValue = setting.DefaultValue;
            if (defaultValue != null || setting.SettingType.IsClass)
            {
                GUILayout.Space(5);
                if (GUILayout.Button("Reset", GUILayout.ExpandWidth(false)))
                    setting.Set(defaultValue);
            }
        }

        private void Start()
        {
            // Check if user has permissions to write config files to disk
            try { Config.Save(); }
            catch (IOException ex) { Logger.Log(LogLevel.Message | LogLevel.Warning, "WARNING: Failed to write to config directory, expect issues!\nError message:" + ex.Message); }
            catch (UnauthorizedAccessException ex) { Logger.Log(LogLevel.Message | LogLevel.Warning, "WARNING: Permission denied to write to config directory, expect issues!\nError message:" + ex.Message); }
        }

        private void Update()
        {
            if (DisplayingWindow) SetUnlockCursor(CursorLockMode.None, true);

            if (OverrideHotkey) return;

            if (_keybind.Value.IsDown()) DisplayingWindow = !DisplayingWindow;
        }

        private void LateUpdate()
        {
            if (DisplayingWindow) SetUnlockCursor(CursorLockMode.None, true);
        }

        private void SetUnlockCursor(CursorLockMode lockState, bool cursorVisible)
        {
            Cursor.lockState = lockState;
            Cursor.visible = cursorVisible;
        }

        private sealed class PluginSettingsData
        {
            public BepInPlugin Info;
            public List<PluginSettingsGroupData> Categories;
            public int Height;
            public string Website;

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
        }
    }
}
