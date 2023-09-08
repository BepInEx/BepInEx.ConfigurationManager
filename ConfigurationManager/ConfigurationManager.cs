// Made by MarC0 / ManlyMarco
// Copyright 2018 GNU General Public License v3.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using BepInEx.Configuration;
using ConfigurationManager.Utilities;

namespace ConfigurationManager
{
    /// <summary>
    /// An easy way to let user configure how a plugin behaves without the need to make your own GUI. The user can change any of the settings you expose, even keyboard shortcuts.
    /// https://github.com/ManlyMarco/BepInEx.ConfigurationManager
    /// </summary>
    [BepInPlugin(GUID, "Configuration Manager", Version)]
    public class ConfigurationManager : BaseUnityPlugin
    {
        /// <summary>
        /// GUID of this plugin
        /// </summary>
        public const string GUID = "com.bepis.bepinex.configurationmanager";

        /// <summary>
        /// Version constant
        /// </summary>
        public const string Version = "18.0.1";

        internal static new ManualLogSource Logger;
        private static SettingFieldDrawer _fieldDrawer;

        private static readonly Color _advancedSettingColor = new Color(1f, 0.95f, 0.67f, 1f);
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
        private bool _obsoleteCursor;

        private string _modsWithoutSettings;

        private List<SettingEntryBase> _allSettings;
        private List<PluginSettingsData> _filteredSetings = new List<PluginSettingsData>();

        internal Rect SettingWindowRect { get; private set; }
        private bool _windowWasMoved;

        private bool _tipsPluginHeaderWasClicked, _tipsWindowWasMoved;

        internal Rect DefaultWindowRect { get; private set; }
        private Rect currentWindowRect;
        private Rect _screenRect;
        private Vector2 _settingWindowScrollPos;
        private int _tipsHeight;

        private PropertyInfo _curLockState;
        private PropertyInfo _curVisible;
        private int _previousCursorLockState;
        private bool _previousCursorVisible;

        internal static Texture2D TooltipBg { get; private set; }
        internal static Texture2D WindowBackground { get; private set; }
        internal static Texture2D EntryBackground { get; private set; }
        internal static Texture2D WidgetBackground { get; private set; }

        internal int LeftColumnWidth { get; private set; }
        internal int RightColumnWidth { get; private set; }

        private readonly ConfigEntry<bool> _showAdvanced;
        private readonly ConfigEntry<bool> _showKeybinds;
        private readonly ConfigEntry<bool> _showSettings;
        private readonly ConfigEntry<KeyboardShortcut> _keybind;
        private readonly ConfigEntry<bool> _hideSingleSection;
        private readonly ConfigEntry<bool> _pluginConfigCollapsedDefault;
        private bool _showDebug;
        
        /* Custom configurations*/
        public static ConfigEntry<Vector2> _windowPosition;
        public static ConfigEntry<Vector2> _windowSize;
        public static ConfigEntry<int> _textSize;
        public static ConfigEntry<Color> _windowBackgroundColor;
        public static ConfigEntry<Color> _entryBackgroundColor;
        public static ConfigEntry<Color> _fontColor;
        public static ConfigEntry<Color> _widgetBackgroundColor;
        
        public static GUIStyle windowStyle;
        public static GUIStyle headerStyle;
        public static GUIStyle entryStyle;
        public static GUIStyle labelStyle;
        public static GUIStyle textStyle;
        public static GUIStyle toggleStyle;
        public static GUIStyle buttonStyle;
        public static GUIStyle boxStyle;
        public static GUIStyle sliderStyle;
        public static GUIStyle thumbStyle;
        public static GUIStyle categoryHeaderSkin;
        public static GUIStyle pluginHeaderSkin;
        public static int fontSize = 14;

        /// <inheritdoc />
        public ConfigurationManager()
        {
            Logger = base.Logger;
            _fieldDrawer = new SettingFieldDrawer(this);

            _showAdvanced = Config.Bind("Filtering", "Show advanced", false);
            _showKeybinds = Config.Bind("Filtering", "Show keybinds", true);
            _showSettings = Config.Bind("Filtering", "Show settings", true);
            _keybind = Config.Bind("General", "Show config manager", new KeyboardShortcut(KeyCode.F1),
                new ConfigDescription("The shortcut used to toggle the config manager window on and off.\n" +
                                      "The key can be overridden by a game-specific plugin if necessary, in that case this setting is ignored."));
            _hideSingleSection = Config.Bind("General", "Hide single sections", false, new ConfigDescription("Show section title for plugins with only one section"));
            _pluginConfigCollapsedDefault = Config.Bind("General", "Plugin collapsed default", true, new ConfigDescription("If set to true plugins will be collapsed when opening the configuration manager window"));
            
            /* Custom configurations*/
            _windowPosition = Config.Bind("General", "WindowPosition", new Vector2(55, 35), "Window position");
            _windowSize = Config.Bind("General", "WindowSize", DefaultWindowRect.size, "Window size");
            _textSize = Config.Bind("General", "FontSize", 14, "Font Size");
            _windowBackgroundColor = Config.Bind("Colors", "WindowBackgroundColor", new Color(0.27f, 0.26f, 0.26f, 1), "Window background color");
            _entryBackgroundColor = Config.Bind("Colors", "EntryBackgroundColor", new Color(0.435f, 0.415f, 0.396f, 1f), "Entry background color");
            _fontColor = Config.Bind("Colors", "FontColor", new Color(1f, 1f, 1f, 1), "Font color");
            _widgetBackgroundColor = Config.Bind("Colors", "WidgetColor", new Color(0.2f, 0.44f, 0.314f, 1f), "Widget color");
            
            currentWindowRect = new Rect(_windowPosition.Value, _windowSize.Value);
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

                    // Do through reflection for unity 4 compat
                    if (_curLockState != null)
                    {
                        _previousCursorLockState = _obsoleteCursor ? Convert.ToInt32((bool)_curLockState.GetValue(null, null)) : (int)_curLockState.GetValue(null, null);
                        _previousCursorVisible = (bool)_curVisible.GetValue(null, null);
                    }
                }
                else
                {
                    if (!_previousCursorVisible || _previousCursorLockState != 0) // 0 = CursorLockMode.None
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
            return x.SettingType == typeof(KeyboardShortcut);
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
            var offsetX = Mathf.RoundToInt((Screen.width - width) / 2f);
            var offsetY = Mathf.RoundToInt((Screen.height - height) / 2f);
            SettingWindowRect = new Rect(offsetX, offsetY, width, height);

            _screenRect = new Rect(0, 0, Screen.width, Screen.height);

            LeftColumnWidth = Mathf.RoundToInt(SettingWindowRect.width / 2.5f);
            RightColumnWidth = (int)SettingWindowRect.width - LeftColumnWidth - 115;

            _windowWasMoved = false;
        }

        private void OnGUI()
        {
            if (DisplayingWindow)
            {
                if (Event.current.type == EventType.KeyUp && Event.current.keyCode == _keybind.Value.MainKey)
                {
                    DisplayingWindow = false;
                    return;
                }

                if(_textSize.Value > 9 && _textSize.Value < 100)
                    fontSize = Mathf.Clamp(_textSize.Value, 10, 30);
                
                CreateBackgrounds();
                CreateStyles();
                SetUnlockCursor(0, true);
                GUI.Box(currentWindowRect, GUIContent.none, new GUIStyle { normal = new GUIStyleState { background = WindowBackground } });
                GUI.backgroundColor = _windowBackgroundColor.Value;

                // If the window hasn't been moved by the user yet, block the whole screen and use a solid background to make the window easier to see
                //if (!_windowWasMoved)
                //{
                //    if (GUI.Button(_screenRect, string.Empty, GUI.skin.box) &&
                //        !SettingWindowRect.Contains(UnityInput.Current.mousePosition))
                //        DisplayingWindow = false;

                //    GUI.Box(SettingWindowRect, GUIContent.none, new GUIStyle { normal = new GUIStyleState { background = WindowBackground } });
                //}
                if (_windowSize.Value.x > 200 && _windowSize.Value.x < Screen.width && _windowSize.Value.y > 200 && _windowSize.Value.y < Screen.height)
                    currentWindowRect.size = _windowSize.Value;

                RightColumnWidth = Mathf.RoundToInt(currentWindowRect.width / 2.5f * fontSize / 12f);
                LeftColumnWidth = Mathf.RoundToInt(currentWindowRect.width - RightColumnWidth - 115);

                currentWindowRect = GUILayout.Window(WindowId, SettingWindowRect, SettingsWindow, "Plugin / mod settings");

                if (currentWindowRect != SettingWindowRect)
                {
                    _windowWasMoved = true;
                    SettingWindowRect = currentWindowRect;

                    _tipsWindowWasMoved = true;
                }

                if (!SettingFieldDrawer.SettingKeyboardShortcut && (!_windowWasMoved || SettingWindowRect.Contains(UnityInput.Current.mousePosition)))
                    UnityInput.Current.ResetInputAxes();
                if (!Input.GetKey(KeyCode.Mouse0) && (currentWindowRect.x != _windowPosition.Value.x || currentWindowRect.y != _windowPosition.Value.y))
                {
                    _windowPosition.Value = currentWindowRect.position;
                    Config.Save();
                }
            }
        }

        private static void DrawTooltip(Rect area)
        {
            if (!string.IsNullOrEmpty(GUI.tooltip))
            {
                var currentEvent = Event.current;

                var style = new GUIStyle(boxStyle)
                {
                    wordWrap = true,
                    alignment = TextAnchor.MiddleCenter
                };
                var color = GUI.backgroundColor;
                GUI.backgroundColor = _entryBackgroundColor.Value;
                const int width = 400;
                var height = style.CalcHeight(new GUIContent(GUI.tooltip), 400) + 10;

                var x = currentEvent.mousePosition.x + width > area.width
                    ? area.width - width
                    : currentEvent.mousePosition.x;

                var y = currentEvent.mousePosition.y + 25 + height > area.height
                    ? currentEvent.mousePosition.y - height
                    : currentEvent.mousePosition.y + 25;

                GUI.Box(new Rect(x, y, width, height), GUI.tooltip, style);
                GUI.backgroundColor = color;
            }
        }

        private void SettingsWindow(int id)
        {
            GUI.DragWindow(new Rect(0,0, currentWindowRect.width, 20));
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
                        catch (ArgumentException)
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
                        catch (ArgumentException)
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
        }

        private void DrawTips()
        {
            var tip = !_tipsPluginHeaderWasClicked ? "Tip: Click plugin names to expand. Click setting and group names to see their descriptions." :
                !_tipsWindowWasMoved ? "Tip: You can drag this window to move it. It will stay open while you interact with the game." : null;

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
            GUI.backgroundColor = _entryBackgroundColor.Value;
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
                
                Color color = GUI.backgroundColor;
                GUI.backgroundColor = _widgetBackgroundColor.Value;
                if (GUILayout.Button("Clear", GUILayout.ExpandWidth(false)))
                    SearchString = string.Empty;
                GUI.backgroundColor = color;
                
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
            var style = new GUIStyle(GUI.skin.box);
            style.normal.textColor = _fontColor.Value;
            style.normal.background = EntryBackground;
            style.fontSize = fontSize;
            GUI.backgroundColor = _entryBackgroundColor.Value;
            
            GUILayout.BeginVertical(style);

            var categoryHeader = _showDebug ?
                new GUIContent($"{plugin.Info.Name.TrimStart('!')} {plugin.Info.Version}", "GUID: " + plugin.Info.GUID) :
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
                    if (GUILayout.Button(new GUIContent("URL", plugin.Website), GUI.skin.label, GUILayout.ExpandWidth(false)))
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

                GUILayout.BeginHorizontal();
                var color = GUI.backgroundColor;
                GUI.backgroundColor = _widgetBackgroundColor.Value;
                if (GUILayout.Button("Reload", buttonStyle, GUILayout.ExpandWidth(true)))
                {
                    foreach (var category in plugin.Categories)
                    {
                        foreach (var setting in category.Settings)
                        {
                            setting.PluginInstance.Config.Reload();
                            break;
                        }

                        break;
                    }

                    BuildFilteredSettingList();
                }

                if (GUILayout.Button("Reset", buttonStyle, GUILayout.ExpandWidth(true)))
                {
                    foreach (var category in plugin.Categories)
                    {
                        foreach (var setting in category.Settings)
                        {
                            setting.Set(setting.DefaultValue);
                        }
                    }

                    BuildFilteredSettingList();
                }

                GUI.backgroundColor = color;
                GUILayout.EndHorizontal();
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
                    Logger.Log(LogLevel.Error, $"Failed to draw setting {setting.DispName} - {ex}");
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

            GUILayout.Label(new GUIContent(setting.DispName.TrimStart('!'), setting.Description),
                GUILayout.Width(LeftColumnWidth), GUILayout.MaxWidth(LeftColumnWidth));

            GUI.color = origColor;
        }

        private static void DrawDefaultButton(SettingEntryBase setting)
        {
            if (setting.HideDefaultButton) return;
            GUI.backgroundColor = _widgetBackgroundColor.Value;

            bool DefaultButton()
            {
                GUILayout.Space(5);
                return GUILayout.Button("Reset", GUILayout.ExpandWidth(false));
            }

            if (setting.DefaultValue != null)
            {
                if (DefaultButton())
                    setting.Set(setting.DefaultValue);
            }
            else if (setting.SettingType.IsClass)
            {
                if (DefaultButton())
                    setting.Set(null);
            }
        }

        private void Start()
        {
            /*var background = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            background.SetPixel(0, 0, Color.black);
            background.Apply();
            TooltipBg = background;

            var windowBackground = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            windowBackground.SetPixel(0, 0, new Color(0.5f, 0.5f, 0.5f, 1));
            windowBackground.Apply();
            WindowBackground = windowBackground;*/

            // Use reflection to keep compatibility with unity 4.x since it doesn't have Cursor
            var tCursor = typeof(Cursor);
            _curLockState = tCursor.GetProperty("lockState", BindingFlags.Static | BindingFlags.Public);
            _curVisible = tCursor.GetProperty("visible", BindingFlags.Static | BindingFlags.Public);

            if (_curLockState == null && _curVisible == null)
            {
                _obsoleteCursor = true;

                _curLockState = typeof(Screen).GetProperty("lockCursor", BindingFlags.Static | BindingFlags.Public);
                _curVisible = typeof(Screen).GetProperty("showCursor", BindingFlags.Static | BindingFlags.Public);
            }

            // Check if user has permissions to write config files to disk
            try { Config.Save(); }
            catch (IOException ex) { Logger.Log(LogLevel.Message | LogLevel.Warning, "WARNING: Failed to write to config directory, expect issues!\nError message:" + ex.Message); }
            catch (UnauthorizedAccessException ex) { Logger.Log(LogLevel.Message | LogLevel.Warning, "WARNING: Permission denied to write to config directory, expect issues!\nError message:" + ex.Message); }
        }

        private void Update()
        {
            if (DisplayingWindow) SetUnlockCursor(0, true);

            if (OverrideHotkey) return;

            if (!DisplayingWindow && _keybind.Value.IsUp())
            {
                CreateBackgrounds();
               
                DisplayingWindow = true;
            }
            //if (_keybind.Value.IsDown()) DisplayingWindow = !DisplayingWindow;
        }

        private void LateUpdate()
        {
            if (DisplayingWindow) SetUnlockCursor(0, true);
        }

        private void SetUnlockCursor(int lockState, bool cursorVisible)
        {
            if (_curLockState != null)
            {
                // Do through reflection for unity 4 compat
                //Cursor.lockState = CursorLockMode.None;
                //Cursor.visible = true;
                if (_obsoleteCursor)
                    _curLockState.SetValue(null, Convert.ToBoolean(lockState), null);
                else
                    _curLockState.SetValue(null, lockState, null);

                _curVisible.SetValue(null, cursorVisible, null);
            }
        }
        
        private void CreateBackgrounds()
        {
            var background = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            background.SetPixel(0, 0, _windowBackgroundColor.Value);
            background.Apply();
            WindowBackground = background;

            var entryBackground = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            entryBackground.SetPixel(0, 0, _entryBackgroundColor.Value);
            entryBackground.Apply();
            EntryBackground = entryBackground;
        }
        
        private void CreateStyles()
        {
            windowStyle = new GUIStyle(GUI.skin.window);
            windowStyle.normal.textColor = _fontColor.Value;
            //windowStyle.fontSize = fontSize;
            windowStyle.active.textColor = _fontColor.Value;

            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.normal.textColor = _fontColor.Value;
            labelStyle.fontSize = fontSize;

            textStyle = new GUIStyle(GUI.skin.textArea);
            textStyle.normal.textColor = _fontColor.Value;
            textStyle.fontSize = fontSize;

            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.normal.textColor = _fontColor.Value;
            buttonStyle.fontSize = fontSize;

            categoryHeaderSkin = new GUIStyle(labelStyle)
            {
                alignment = TextAnchor.UpperCenter,
                wordWrap = true,
                stretchWidth = true,
            };
            pluginHeaderSkin = new GUIStyle(categoryHeaderSkin);
            


            toggleStyle = new GUIStyle(GUI.skin.toggle);
            toggleStyle.normal.textColor = _fontColor.Value;
            toggleStyle.fontSize = fontSize;

            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.textColor = _fontColor.Value;
            boxStyle.fontSize = fontSize;

            sliderStyle = new GUIStyle(GUI.skin.horizontalSlider);

            thumbStyle = new GUIStyle(GUI.skin.horizontalSliderThumb);
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
