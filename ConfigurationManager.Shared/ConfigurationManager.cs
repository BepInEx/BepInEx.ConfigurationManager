// Made by MarC0 / ManlyMarco
// Copyright 2018 GNU General Public License v3.0

using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using ConfigurationManager.Utilities;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

#if IL2CPP
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using BaseUnityPlugin = BepInEx.Unity.IL2CPP.BasePlugin;
#endif

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
        public const string Version = Constants.Version;

        internal static ManualLogSource Logger;
        private static SettingFieldDrawer _fieldDrawer;

        private static readonly Color _advancedSettingColor = new Color(1f, 0.95f, 0.67f, 1f);
        private const int WindowId = -68;

        private const string SearchBoxName = "searchBox";
        private bool _focusSearchBox;
        private string _searchString = string.Empty;
        private string _selectedPluginName = null;

        private enum Tab
        {
            Plugins,
            OtherFiles
        }

        private Tab _selectedTab = Tab.Plugins;

// Store recognized files
        private List<string> _recognizedFiles = new List<string>();
        private string _selectedOtherFile;


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

        /// <summary>
        /// Window is visible and is blocking the whole screen. This is true until the user moves the window, which lets it run while user interacts with the game.
        /// </summary>
        public bool IsWindowFullscreen => DisplayingWindow && !_windowWasMoved;

        private bool _tipsPluginHeaderWasClicked, _tipsWindowWasMoved;

        private Rect _screenRect;
        private Vector2 _pluginWindowScrollPos;
        private Vector2 _settingWindowScrollPos;
        private int _tipsHeight;

        private PropertyInfo _curLockState;
        private PropertyInfo _curVisible;
        private int _previousCursorLockState;
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

        /// <inheritdoc />
        public ConfigurationManager()
        {
#if IL2CPP
            Logger = Log;
#else
            Logger = base.Logger;
#endif
            _fieldDrawer = new SettingFieldDrawer(this);

            _showAdvanced = Config.Bind("Filtering", "Show advanced", false);
            _showKeybinds = Config.Bind("Filtering", "Show keybinds", true);
            _showSettings = Config.Bind("Filtering", "Show settings", true);
            _keybind = Config.Bind("General", "Show config manager", new KeyboardShortcut(KeyCode.F1), new ConfigDescription("The shortcut used to toggle the config manager window on and off.\nThe key can be overridden by a game-specific plugin if necessary, in that case this setting is ignored."));
            _hideSingleSection = Config.Bind("General", "Hide single sections", false, new ConfigDescription("Show section title for plugins with only one section"));
            _pluginConfigCollapsedDefault = Config.Bind("General", "Plugin collapsed default", true, new ConfigDescription("If set to true plugins will be collapsed when opening the configuration manager window"));
        }

#if IL2CPP
        /// <inheritdoc/>
        public override void Load()
        {
            ConfigurationManagerBehaviour.Plugin = this;
            AddComponent<ConfigurationManagerBehaviour>();
        }
        private class ConfigurationManagerBehaviour : MonoBehaviour
        {
            internal static ConfigurationManager Plugin;
            private void Start() => Plugin.Start();
            private void Update() => Plugin.Update();
            private void LateUpdate() => Plugin.LateUpdate();
            private void OnGUI() => Plugin.OnGUI();
        }
#endif

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
            return x.SettingType == typeof(KeyboardShortcut) || x.SettingType == typeof(KeyCode);
        }

        private static bool ContainsSearchString(SettingEntryBase setting, string[] searchStrings)
        {
            var combinedSearchTarget = setting.PluginInfo.Name + Environment.NewLine +
                                       setting.PluginInfo.GUID + Environment.NewLine +
                                       setting.DispName + Environment.NewLine +
                                       setting.Category + Environment.NewLine +
                                       setting.Description + Environment.NewLine +
                                       setting.DefaultValue + Environment.NewLine +
                                       setting.Get();

            return searchStrings.All(s => combinedSearchTarget.IndexOf(s, StringComparison.InvariantCultureIgnoreCase) >= 0);
        }

        private void CalculateWindowRect()
        {
            float widthFactor = 0.50f; // 50% of the screen width
            float heightFactor = 0.95f; // 95% of the screen height

            // Ensure the window size is within reasonable limits
            var width = Mathf.Clamp(Screen.width * widthFactor, 400, Screen.width * widthFactor);
            var height = Mathf.Clamp(Screen.height * heightFactor, 300, Screen.height * heightFactor);

            // Center the window
            var offsetX = Mathf.RoundToInt((Screen.width - width) / 2f);
            var offsetY = Mathf.RoundToInt((Screen.height - height) / 2f);

            SettingWindowRect = new Rect(offsetX, offsetY, width, height);

            _screenRect = new Rect(0, 0, Screen.width, Screen.height);

            LeftColumnWidth = Mathf.RoundToInt(SettingWindowRect.width / 3.5f);
            RightColumnWidth = (int)SettingWindowRect.width - LeftColumnWidth;

            _windowWasMoved = false;
        }

        private void OnGUI()
        {
            if (DisplayingWindow)
            {
                SetUnlockCursor(0, true);

#if IL2CPP
                Vector2 mousePosition = Input.mousePosition; //todo move to UnityInput whenever it is added
#else
                Vector2 mousePosition = UnityInput.Current.mousePosition;
#endif
                mousePosition.y = Screen.height - mousePosition.y;


                if (Event.current.type == EventType.mouseDown && SettingWindowRect.Contains(Event.current.mousePosition))
                {
                    Debug.Log("MouseDown inside the window");
                    Event.current.Use(); // Prevent further propagation
                }

                if (Input.GetMouseButtonDown(0) && SettingWindowRect.Contains(Input.mousePosition))
                {
                    Debug.Log($"Mouse clicked at: {Input.mousePosition}");
                    Debug.Log("Current: " + Event.current);
                    Debug.Log("Current Type: " + Event.current.type);
                    Event.current.Use(); // Prevent further propagation
                }

                // If the window hasn't been moved by the user yet, block the whole screen and use a solid background to make the window easier to see
                if (!_windowWasMoved)
                {
                    if (GUI.Button(_screenRect, string.Empty, GUI.skin.box) && !SettingWindowRect.Contains(mousePosition))
                        DisplayingWindow = false;

                    ImguiUtils.DrawWindowBackground(SettingWindowRect);
                }

                var newRect = GUILayout.Window(WindowId, SettingWindowRect, (GUI.WindowFunction)SettingsWindow, $"Configuration Manager {Version}");
                GUI.FocusWindow(WindowId);

                if (newRect != SettingWindowRect)
                {
                    _windowWasMoved = true;
                    SettingWindowRect = newRect;

                    _tipsWindowWasMoved = true;
                }

                if (!SettingFieldDrawer.SettingKeyboardShortcut && (!_windowWasMoved || SettingWindowRect.Contains(mousePosition)))
                    Input.ResetInputAxes();
            }
        }

        private static void DrawTooltip(Rect area)
        {
            string tooltip = GUI.tooltip;
            if (!string.IsNullOrEmpty(tooltip))
            {
                var style = GUI.skin.box.CreateCopy();
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
                ImguiUtils.DrawControlBackground(position, Color.black);
                style.Draw(position, content, -1);
            }
        }

        /*private void SettingsWindow(int id)
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
#if IL2CPP
                        catch (Il2CppException)
#else
                        catch (ArgumentException)
#endif
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
#if IL2CPP
                        catch (Il2CppException)
#else
                        catch (ArgumentException)
#endif
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
        }*/
        private void SettingsWindow(int id)
        {
            DrawWindowHeader();

            // Define columns
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true), GUILayout.MaxWidth(LeftColumnWidth + RightColumnWidth));
            {
                // Left Column: Plugin List
                GUILayout.BeginVertical(GUILayout.MaxWidth(LeftColumnWidth), GUILayout.ExpandWidth(true));
                {
                    GUILayout.BeginHorizontal(GUI.skin.box);
                    {
                        if (GUILayout.Button("Plugins", GUILayout.ExpandWidth(true)))
                        {
                            _selectedTab = Tab.Plugins;
                            _selectedPluginName = null;
                            _selectedOtherFile = null;
                        }

                        if (GUILayout.Button("Other Config Files", GUILayout.ExpandWidth(true)))
                        {
                            _selectedTab = Tab.OtherFiles;
                            _selectedPluginName = null;
                            _selectedOtherFile = null;
                        }
                    }
                    GUILayout.EndHorizontal();

                    _pluginWindowScrollPos = GUILayout.BeginScrollView(_pluginWindowScrollPos);
                    if (_selectedTab == Tab.Plugins)
                    {
                        foreach (var plugin in _filteredSetings)
                        {
                            DrawSinglePlugin(plugin);
                        }
                    }
                    else if (_selectedTab == Tab.OtherFiles)
                    {
                        foreach (var file in SettingSearcher.OtherConfigFiles)
                        {
                            DrawOtherFile(file);
                        }
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

                    GUILayout.EndScrollView();
                }
                GUILayout.EndVertical();


                // Add a vertical separator between columns
                GUILayout.Box(GUIContent.none, GUILayout.Width(1), GUILayout.ExpandHeight(true));


                // Right Column: Plugin Settings
                GUILayout.BeginVertical(GUILayout.MaxWidth(RightColumnWidth), GUILayout.ExpandWidth(false));
                {
                    _settingWindowScrollPos = GUILayout.BeginScrollView(_settingWindowScrollPos, GUILayout.ExpandWidth(false));

                    if (_selectedTab == Tab.Plugins)
                    {
                        if (string.IsNullOrEmpty(_selectedPluginName))
                        {
                            GUILayout.Label("Select a plugin from the left column to view settings.");
                        }
                        else
                        {
                            var selectedPlugin = _filteredSetings.FirstOrDefault(p => p.Info.Name == _selectedPluginName);
                            if (selectedPlugin != null)
                            {
                                GUILayout.Label($"Editing: {selectedPlugin.Info.Name}", GUILayout.ExpandWidth(false));
                                GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.ExpandWidth(false));

                                var buttonStyle = new GUIStyle(GUI.skin.button);

                                if (GUILayout.Button($"Reset All Settings For {selectedPlugin.Info.Name}", buttonStyle, GUILayout.ExpandWidth(false)))
                                {
                                    foreach (var category in selectedPlugin.Categories)
                                    {
                                        foreach (var setting in category.Settings)
                                        {
                                            setting.Set(setting.DefaultValue);
                                        }
                                    }

                                    BuildFilteredSettingList();
                                }

                                GUILayout.EndHorizontal();
                                DrawPluginSettings(selectedPlugin);
                            }
                            else
                            {
                                GUILayout.Label("Plugin not found.", GUILayout.MaxWidth(RightColumnWidth));
                            }
                        }
                    }
                    else if (_selectedTab == Tab.OtherFiles)
                    {
                        if (string.IsNullOrEmpty(_selectedOtherFile))
                        {
                            GUILayout.Label("Select a file from the left column to edit.");
                        }
                        else
                        {
                            DrawOtherFileEditor(_selectedOtherFile);
                        }
                    }

                    GUILayout.EndScrollView();
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();

            if (!SettingFieldDrawer.DrawCurrentDropdown())
                DrawTooltip(SettingWindowRect);

            GUI.DragWindow();
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

        private void DrawOtherFileEditor(string filePath)
        {
            try
            {
                GUILayout.Label($"Editing: {Path.GetFileName(filePath)}", GUILayout.ExpandWidth(false));

                string content = File.ReadAllText(filePath);
                string updatedContent = content;

                // Place the Save button above the text editor
                GUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Save", GUILayout.ExpandWidth(false)))
                    {
                        File.WriteAllText(filePath, updatedContent);
                        Logger.LogInfo($"File saved: {filePath}");
                    }

                    if (GUILayout.Button("Delete File", GUILayout.ExpandWidth(false)))
                    {
                        File.Delete(filePath);
                        Logger.LogInfo($"File deleted: {filePath}");
                    }

                    GUILayout.Space(10);
                    if (GUILayout.Button("Open File Location", GUILayout.ExpandWidth(false)))
                    {
                        Utils.OpenFileLocation(filePath);
                    }
                }
                GUILayout.EndHorizontal();

                // Display content in a scrollable text area
                updatedContent = GUILayout.TextArea(content, GUILayout.ExpandHeight(true));
            }
            catch (Exception ex)
            {
                GUILayout.Label($"Failed to load or edit file: {ex.Message}", GUILayout.ExpandWidth(false));
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

                if (GUILayout.Button("Open BepInEx Log"))
                {
                    try
                    {
                        Utils.OpenBepInExLog();
                    }
                    catch (SystemException ex)
                    {
                        Logger.Log(LogLevel.Message | LogLevel.Error, ex.Message);
                    }
                }

                if (GUILayout.Button("Open Player Log"))
                {
                    try
                    {
                        Utils.OpenLog();
                    }
                    catch (SystemException ex)
                    {
                        Logger.Log(LogLevel.Message | LogLevel.Error, ex.Message);
                    }
                }

                //GUILayout.Space(8);

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

                if (GUILayout.Button("Clear", GUILayout.ExpandWidth(false)))
                    SearchString = string.Empty;

                /*GUILayout.Space(8);

                if (GUILayout.Button(_pluginConfigCollapsedDefault.Value ? "Expand All" : "Collapse All", GUILayout.ExpandWidth(false)))
                {
                    var newValue = !_pluginConfigCollapsedDefault.Value;
                    _pluginConfigCollapsedDefault.Value = newValue;
                    foreach (var plugin in _filteredSetings)
                        plugin.Collapsed = newValue;

                    _tipsPluginHeaderWasClicked = true;
                }*/
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
            GUIStyle style = new GUIStyle(GUI.skin.box)
            {
                normal = { background = GUI.skin.box.normal.background },
                active = { background = GUI.skin.box.normal.background },
                hover = { background = GUI.skin.button.hover.background }
            };
            GUILayout.BeginVertical(style);

            var categoryHeader = _showDebug ? new GUIContent($"{plugin.Info.Name.TrimStart('!')} {plugin.Info.Version}", null, "GUID: " + plugin.Info.GUID) : new GUIContent($"{plugin.Info.Name.TrimStart('!')} {plugin.Info.Version}");

            var isSearching = !string.IsNullOrEmpty(SearchString);

            {
                var hasWebsite = plugin.Website != null;
                if (hasWebsite)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(29); // Same as the URL button to keep the plugin name centered
                }

                if (SettingFieldDrawer.DrawPluginHeader(categoryHeader) && !isSearching)
                {
                    _tipsPluginHeaderWasClicked = true;
                    _selectedPluginName = plugin.Info.Name;
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

            GUILayout.EndVertical();
        }

        private void DrawPluginSettings(PluginSettingsData selectedPlugin)
        {
            foreach (var category in selectedPlugin.Categories)
            {
                GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandWidth(false));
                if (!string.IsNullOrEmpty(category.Name))
                {
                    SettingFieldDrawer.DrawCategoryHeader(category.Name);
                }

                foreach (var setting in category.Settings)
                {
                    DrawSingleSetting(setting);
                    GUILayout.Space(2);
                }

                GUILayout.EndVertical();
            }
        }

        private void DrawOtherFile(string file)
        {
            string fileType = Path.GetExtension(file).ToUpperInvariant().TrimStart('.');
            var categoryHeader = _showDebug
                ? new GUIContent($"{Path.GetFileName(file)} ({fileType})", null, "File Type: " + fileType)
                : new GUIContent($"{Path.GetFileName(file)} ({fileType})");

            var isSearching = !string.IsNullOrEmpty(SearchString);

            GUIStyle style = new GUIStyle(GUI.skin.box)
            {
                normal = { background = GUI.skin.box.normal.background },
                active = { background = GUI.skin.box.normal.background },
                hover = { background = GUI.skin.button.hover.background }
            };


            GUILayout.BeginVertical(style);


            if (SettingFieldDrawer.DrawPluginHeader(categoryHeader) && !isSearching)
            {
                _tipsPluginHeaderWasClicked = true;
                _selectedOtherFile = file;
            }


            GUILayout.EndVertical();
        }


        private void DrawSingleSetting(SettingEntryBase setting)
        {
            var ligherBox = GUI.skin.box.CreateCopy();
            var isDefaultValue = setting.DefaultValue != null && (setting.Get() != null && setting.Get().Equals(setting.DefaultValue));
            ligherBox.normal.background = ImguiUtils.MakeTexture(1, 1, isDefaultValue ? new Color(0.4f, 0.4f, 0.4f, 1f) : new Color(0.3f, 0.3f, 0.3f, 1f));
            GUILayout.BeginHorizontal(ligherBox, GUILayout.ExpandWidth(false));
            {
                try
                {
                    DrawSettingName(setting);
                    if (!isDefaultValue)
                    {
                        DrawDefaultButton(setting);
                    }
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
            if (!setting.HideSettingName)
            {
                var nameStyle = new GUIStyle(GUI.skin.label)
                {
                    wordWrap = true, // Ensure name doesn't wrap
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white }
                };

                var descriptionStyle = new GUIStyle(GUI.skin.label)
                {
                    wordWrap = true, // Enable word wrapping for the description
                    fontStyle = FontStyle.Italic,
                    normal = { textColor = Color.black }
                };

                var defaultValueStyle = new GUIStyle(GUI.skin.label)
                {
                    wordWrap = true, // Enable word wrapping for the description
                    fontStyle = FontStyle.BoldAndItalic,
                    normal = { textColor = _advancedSettingColor }
                };

                GUILayout.BeginVertical(GUILayout.ExpandWidth(false), GUILayout.MaxWidth(RightColumnWidth));
                {
                    GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
                    // Render the name
                    GUILayout.Label(setting.DispName.TrimStart('!'), nameStyle, GUILayout.ExpandWidth(false));

                    // Render the type of the setting
                    GUILayout.Label($" ({setting.SettingType.Name})", descriptionStyle, GUILayout.ExpandWidth(false));

                    // If the setting type is a range, render the min and max values like [min - max]
                    if (setting.AcceptableValueRange.Key != null)
                    {
                        var min = setting.AcceptableValueRange.Key;
                        var max = setting.AcceptableValueRange.Value;
                        GUILayout.Label($" [{min} - {max}]", descriptionStyle, GUILayout.ExpandWidth(false));
                    }

                    // Render the default value
                    if (setting.DefaultValue != null)
                    {
                        GUILayout.Label($" Default: {setting.DefaultValue}", defaultValueStyle, GUILayout.ExpandWidth(false));
                    }

                    GUILayout.EndHorizontal();
                    // Render the description below the name
                    if (!string.IsNullOrEmpty(setting.Description))
                    {
                        GUILayout.Label(setting.Description, descriptionStyle);
                    }
                }
                _fieldDrawer.DrawSettingValue(setting);
                GUILayout.EndVertical();
            }
            else
            {
                _fieldDrawer.DrawSettingValue(setting);
            }
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
            try
            {
                Config.Save();
            }
            catch (IOException ex)
            {
                Logger.Log(LogLevel.Message | LogLevel.Warning, "WARNING: Failed to write to config directory, expect issues!\nError message:" + ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Log(LogLevel.Message | LogLevel.Warning, "WARNING: Permission denied to write to config directory, expect issues!\nError message:" + ex.Message);
            }
        }

        private void Update()
        {
            if (DisplayingWindow) SetUnlockCursor(0, true);

            if (OverrideHotkey) return;

            if (_keybind.Value.IsDown()) DisplayingWindow = !DisplayingWindow;
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