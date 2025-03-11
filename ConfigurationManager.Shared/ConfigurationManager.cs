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
using UnityEngine.UI;

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
    [BepInPlugin(GUID, "Configuration Manager", Constants.Version)]
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

        private const string FileEditorName = "fileEditor";
        private string _fileEditorString = string.Empty;
        private bool _focusFileEditor;

        private enum Tab
        {
            Plugins,
            OtherFiles
        }

        private Tab _selectedTab = Tab.Plugins;

        private string _selectedOtherFile;

        private static HashSet<string> _pinnedPlugins = new HashSet<string>();

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
        private List<PluginSettingsData> _filteredSettings = new List<PluginSettingsData>();

        internal Rect SettingWindowRect { get; private set; }
        private bool _windowWasMoved;

        private bool _tipsPluginHeaderWasClicked, _tipsWindowWasMoved;

        private Rect _screenRect;
        private Vector2 _pluginWindowScrollPos;
        private Vector2 _settingWindowScrollPos;

        private PropertyInfo _curLockState;
        private PropertyInfo _curVisible;
        private int _previousCursorLockState;
        private bool _previousCursorVisible;

        internal int LeftColumnWidth { get; private set; }
        internal int RightColumnWidth { get; set; }

        private GameObject _overlayCanvasObj;
        private Canvas _overlayCanvas;
        private CanvasScaler _overlayCanvasScaler;
        private GraphicRaycaster _overlayRaycaster;
        private Image _overlayBlocker;
        private readonly Dictionary<string, string> _otherFileContents = new Dictionary<string, string>();


        private readonly ConfigEntry<bool> _showAdvanced;
        private readonly ConfigEntry<bool> _showKeybinds;
        private readonly ConfigEntry<bool> _showSettings;
        private readonly ConfigEntry<bool> _showDefault;
        private readonly ConfigEntry<KeyboardShortcut> _keybind;
        private readonly ConfigEntry<bool> _hideSingleSection;
        private readonly ConfigEntry<string> _pinnedPluginsConfig;
        private bool _showDebug;


        /* Custom configurations*/
        public static ConfigEntry<Vector2> _windowSize;
        public static ConfigEntry<int> _textSize;
        public static ConfigEntry<Color> _fontColor;
        public static ConfigEntry<Color> _widgetBackgroundColor;
        public static ConfigEntry<Color> _settingDescriptionColor;


        // Red close button (#BF3030)
        public static ConfigEntry<Color> _closeButtonColor;

        // Darker red cancel button (#541B1B)
        public static ConfigEntry<Color> _cancelButtonColor;

        // Light green for setting text (#A7EDA7)
        public static ConfigEntry<Color> _lightGreenSettingTextColor;

        // Dark green for save button (#1C401B)
        public static ConfigEntry<Color> _saveButtonColor;

        // Dark grey for background of left panel (#262626)
        public static ConfigEntry<Color> _leftPanelColor;

        // Black background for entire panel (#0D0D0D)
        public static ConfigEntry<Color> _panelBackgroundColor;

        // Medium black background for category section (#1F1F1F)
        public static ConfigEntry<Color> _categorySectionColor;

        // Medium black background for category header (#121212)
        public static ConfigEntry<Color> _categoryHeaderColor;

        // Light grey for sliders (#4C4C4C)
        public static ConfigEntry<Color> _lightGreySlidersColor;

        // Medium grey for sliders (#404040)
        public static ConfigEntry<Color> _mediumGreySlidersColor;

        // Green for class/type name (#148B32)
        public static ConfigEntry<Color> _classTypeColor;

        // Yellow/tan for highlight (#989076)
        public static ConfigEntry<Color> _highlightColor;

        // Default Value label color ()
        public static ConfigEntry<Color> _defaultValueColor;

        // Range Value left color ()
        public static ConfigEntry<Color> _rangeValueColor;


        /// <inheritdoc />
        public ConfigurationManager()
        {
#if IL2CPP
            Logger = Log;
#else
            Logger = base.Logger;
#endif
            _fieldDrawer = new SettingFieldDrawer(this);

            /* Custom configurations*/
            _windowSize = Config.Bind("General", "Window Size", new Vector2(0.55f, 0.95f), "Window size. The x is the width and the y is the height. This is a percent of screen to take up. 0.5 in the x is 50% of the screen width.");
            _textSize = Config.Bind("General", "Font Size", 14, "Font Size");
            _fontColor = Config.Bind("Colors", "Font Color", new Color(1f, 1f, 1f, 1), "Font color");
            _widgetBackgroundColor = Config.Bind("Colors", "Widget Color", GUIHelper.DarkGreenSaveButton, "Widget color");
            _settingDescriptionColor = Config.Bind("Colors", "Description Color", GUIHelper.SettingDescription, "Description Color");
            _closeButtonColor = Config.Bind("Colors", "Close Button Color", GUIHelper.RedCloseButton, "Color for the close button (#BF3030).");
            _cancelButtonColor = Config.Bind("Colors", "Cancel Button Color", GUIHelper.DarkRedCancelButton, "Color for the cancel button (#541B1B).");
            _lightGreenSettingTextColor = Config.Bind("Colors", "Light Green Setting Text Color", GUIHelper.LightGreenSettingText, "Light green color for setting text (#A7EDA7).");
            _saveButtonColor = Config.Bind("Colors", "Save Button Color", GUIHelper.DarkGreenSaveButton, "Dark green color for save button (#1C401B).");
            _leftPanelColor = Config.Bind("Colors", "Left Panel Color", GUIHelper.DarkGreyLeftPanel, "Dark grey background for the left panel (#262626).");
            _panelBackgroundColor = Config.Bind("Colors", "Panel Background Color", GUIHelper.WhitePanelBackground, "Black background for the entire panel (#0D0D0D).");
            _categorySectionColor = Config.Bind("Colors", "Category Section Color", GUIHelper.MediumBlackCategorySection, "Medium black background for category sections (#1F1F1F).");
            _categoryHeaderColor = Config.Bind("Colors", "Category Header Color", GUIHelper.MediumBlackCategoryHeader, "Medium black background for category header (#121212).");
            _lightGreySlidersColor = Config.Bind("Colors", "Light Grey Sliders Color", GUIHelper.LightGreySliders, "Light grey for sliders (#4C4C4C).");
            _mediumGreySlidersColor = Config.Bind("Colors", "Medium Grey Sliders Color", GUIHelper.MediumGreySliders, "Medium grey for sliders (#404040).");
            _classTypeColor = Config.Bind("Colors", "Class/Type Name Color", GUIHelper.GreenClassTypeName, "Green for class/type name (#148B32).");
            _defaultValueColor = Config.Bind("Colors", "Default Value Label Color", GUIHelper.DefaultValueColor, "Default Value label color (#FFF4AC).");
            _rangeValueColor = Config.Bind("Colors", "Range Value Label Color", GUIHelper.RangeValueColor, "Range Value label color.");
            _highlightColor = Config.Bind("Colors", "Highlight Color", GUIHelper.YellowTanHighlight, "Yellow/tan highlight (#989076).");


            _showAdvanced = Config.Bind("Filtering", "Show advanced", false);
            _showKeybinds = Config.Bind("Filtering", "Show keybinds", true);
            _showSettings = Config.Bind("Filtering", "Show settings", true);
            _showDefault = Config.Bind("Filtering", "Show default settings as well as changed", true);
            _keybind = Config.Bind("General", "Show config manager", new KeyboardShortcut(KeyCode.F1), new ConfigDescription("The shortcut used to toggle the config manager window on and off.\nThe key can be overridden by a game-specific plugin if necessary, in that case this setting is ignored."));
            _hideSingleSection = Config.Bind("General", "Hide single sections", false, new ConfigDescription("Show section title for plugins with only one section"));

            _pinnedPluginsConfig = Config.Bind("Pins", "Pinned plugins", "", new ConfigDescription("Comma-separated list of plugin GUIDs to pin to the top of the list. Use the GUID of the plugin to pin it, or use the configuration manager UI to pin it."));
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
            private void OnDestroy() => Plugin.OnDestroy();
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


                ImguiUtils.CreateBackgrounds();

                if (_displayingWindow)
                {
                    CalculateWindowRect();

                    BuildSettingList();

                    _focusSearchBox = true;

                    ShowOverlayCanvas();

                    // Do through reflection for unity 4 compat
                    if (_curLockState != null)
                    {
                        _previousCursorLockState = _obsoleteCursor ? Convert.ToInt32((bool)_curLockState.GetValue(null, null)) : (int)_curLockState.GetValue(null, null);
                        _previousCursorVisible = (bool)_curVisible.GetValue(null, null);
                    }
                }
                else
                {
                    HideOverlayCanvas();

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
            // Release all old objects to the pool
            for (int index = 0; index < _filteredSettings.Count; ++index)
            {
                PluginSettingsData oldPlugin = _filteredSettings[index];
                PluginSettingsDataPool.Release(oldPlugin);
            }

            _filteredSettings.Clear();

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
                if (!_showDefault.Value)
                    results = results.Where(x => x.DefaultValue != null && x.Get() != null && !x.Get().Equals(x.DefaultValue));
            }

            const string shortcutsCatName = "Keyboard shortcuts";

            _filteredSettings = results
                .GroupBy(x => x.PluginInfo)
                .Select(pluginSettings =>
                {
                    var originalCategoryOrder = pluginSettings.Select(x => x.Category).Distinct().ToList();

                    var categories = pluginSettings
                        .GroupBy(x => x.Category)
                        .OrderBy(x => originalCategoryOrder.IndexOf(x.Key))
                        .ThenBy(x => x.Key)
                        .Select(x => new PluginSettingsData.PluginSettingsGroupData { Name = x.Key, Settings = x.OrderByDescending(set => set.Order).ThenBy(set => set.DispName).ToList() });

                    var website = Utils.GetWebsite(pluginSettings.First().PluginInstance);

                    // Instead of `new PluginSettingsData`
                    var data = PluginSettingsDataPool.Get(pluginSettings.Key, categories.ToList(), website);
                    return data;
                })
                .OrderBy(x => x.Info.Name)
                .ToList();

            // 2) Then reorder so pinned appear on top
            _filteredSettings = _filteredSettings
                .OrderByDescending(p => IsPinned(p.Info.GUID)) // pinned = true => sort earlier
                .ThenBy(p => p.Info.Name) // secondary sort by name if desired
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
            float widthFactor = _windowSize.Value.x; // 55% of the screen width by default
            float heightFactor = _windowSize.Value.y; // 95% of the screen height by default

            // Ensure the window size is within reasonable limits
            var width = Mathf.Clamp(Screen.width * widthFactor, Screen.width * 0.25f, Screen.width * widthFactor);
            var height = Mathf.Clamp(Screen.height * heightFactor, Screen.width * 0.20f, Screen.height * heightFactor);

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
            if (!DisplayingWindow) return;
            if (Event.current.type == EventType.KeyUp && Event.current.keyCode == _keybind.Value.MainKey)
            {
                DisplayingWindow = false;
            }
            else
            {
                if (Event.current.type == EventType.MouseDown)
                {
                    Vector2 mousePos = Event.current.mousePosition;
                    if (!SettingWindowRect.Contains(mousePos))
                    {
                        DisplayingWindow = false;
                    }
                }


                if (_textSize.Value > 9 && _textSize.Value < 100)
                    ImguiUtils.fontSize = Mathf.Clamp(_textSize.Value, 10, 30);

                ImguiUtils.CreateStyles();

                SetUnlockCursor(0, true);

#if IL2CPP
                Vector2 mousePosition = Input.mousePosition; //todo move to UnityInput whenever it is added
#else
                Vector2 mousePosition = UnityInput.Current.mousePosition;
#endif
                mousePosition.y = Screen.height - mousePosition.y;


                // If the window hasn't been moved by the user yet, block the whole screen and use a solid background to make the window easier to see
                if (!_windowWasMoved)
                {
                    if (GUI.Button(_screenRect, string.Empty, GUI.skin.box) && !SettingWindowRect.Contains(mousePosition))
                        DisplayingWindow = false;

                    ImguiUtils.DrawWindowBackground(SettingWindowRect);
                }

                var newRect = GUIHelper.CreateWindowWithColor(WindowId, SettingWindowRect, (GUI.WindowFunction)SettingsWindow, $"Configuration Manager {Version}", _panelBackgroundColor.Value);
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

        private void SettingsWindow(int id)
        {
            GUI.DragWindow(new Rect(0.0f, 0.0f, SettingWindowRect.width, 20f));
            DrawWindowHeader();

            // Define columns
            GUILayout.BeginHorizontal(GUILayout.MaxWidth(LeftColumnWidth + RightColumnWidth));
            {
                // Left Column: Plugin List
                GUILayout.BeginVertical(GUILayout.MaxWidth(LeftColumnWidth), GUILayout.ExpandWidth(false));
                {
                    GUILayout.BeginHorizontal(GUI.skin.box);
                    {
                        if (GUIHelper.CreateButtonWithColor("Plugins", default, ImguiUtils.buttonStyle, GUILayout.ExpandWidth(true)))
                        {
                            _selectedTab = Tab.Plugins;
                            _selectedPluginName = null;
                            _selectedOtherFile = null;
                        }

                        if (GUIHelper.CreateButtonWithColor("Other Config Files", default, ImguiUtils.buttonStyle, GUILayout.ExpandWidth(true)))
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
                        var currentHeight = 0;
                        for (int index = 0; index < _filteredSettings.Count; ++index)
                        {
                            PluginSettingsData plugin = _filteredSettings[index];
                            var visible = plugin.Height == 0 || currentHeight + plugin.Height >= _pluginWindowScrollPos.y && currentHeight <= _pluginWindowScrollPos.y + SettingWindowRect.height;
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
                                    if (plugin.Height > 0)
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

                            currentHeight += plugin.Height + 1;
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
                        GUIHelper.BeginColor(_fontColor.Value);
                        GUILayout.Label("Plugins with no options available: " + _modsWithoutSettings);
                        GUIHelper.EndColor();
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
                GUILayout.BeginVertical(GUILayout.MaxWidth(RightColumnWidth), GUILayout.ExpandWidth(true));
                {
                    // 1) Show instructions if nothing is selected
                    if (_selectedTab == Tab.Plugins && string.IsNullOrEmpty(_selectedPluginName))
                    {
                        GUIHelper.CreateLabelWithColor("Select a plugin from the left column to view settings.", _fontColor.Value, ImguiUtils.labelStyle);
                    }
                    else if (_selectedTab == Tab.OtherFiles && string.IsNullOrEmpty(_selectedOtherFile))
                    {
                        GUIHelper.CreateLabelWithColor("Select a file from the left column to edit.", _fontColor.Value, ImguiUtils.labelStyle);
                    }

                    // 2) Figure out the current item we're "Editing: ..."
                    PluginSettingsData selectedPlugin = _filteredSettings.FirstOrDefault(p => p.Info.Name == _selectedPluginName);

                    string fileName;
                    if (selectedPlugin != null && !string.IsNullOrEmpty(_selectedPluginName))
                    {
                        fileName = selectedPlugin.Info.Name;
                    }
                    else if (selectedPlugin == null && !string.IsNullOrEmpty(_selectedOtherFile))
                    {
                        fileName = Path.GetFileName(_selectedOtherFile);
                    }
                    else
                    {
                        fileName = "Select a plugin or file to edit.";
                    }

                    GUIHelper.CreateLabelWithColor($"Editing: {fileName}", _fontColor.Value, ImguiUtils.labelStyle, GUILayout.ExpandWidth(false));

                    // 3) If on Plugins tab, show 'Reset All Settings' button
                    if (_selectedTab == Tab.Plugins && selectedPlugin != null)
                    {
                        GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.ExpandWidth(false));
                        {
                            var buttonStyle = new GUIStyle(GUI.skin.button);
                            string resetLabel = $"Reset All Settings For {selectedPlugin.Info.Name}";

                            if (GUILayout.Button(resetLabel, buttonStyle, GUILayout.ExpandWidth(false)))
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

                            if (GUILayout.Button("Collapse All Settings", buttonStyle, GUILayout.ExpandWidth(false)))
                            {
                                foreach (var category in selectedPlugin.Categories)
                                {
                                    category.Collapsed = !category.Collapsed;
                                }
                            }
                        }
                        GUILayout.EndHorizontal();
                    }

                    // 4) If on OtherFiles tab with a valid file, show Save/Open/Delete
                    if (_selectedTab == Tab.OtherFiles && !string.IsNullOrEmpty(_selectedOtherFile))
                    {
                        GUILayout.BeginHorizontal();
                        {
                            if (GUIHelper.CreateButtonWithColor("Save", _saveButtonColor.Value, ImguiUtils.buttonStyle, GUILayout.ExpandWidth(false)))
                            {
                                File.WriteAllText(_selectedOtherFile, _otherFileContents[_selectedOtherFile]);
                                Logger.LogInfo($"File saved: {_selectedOtherFile}");
                            }

                            if (GUIHelper.CreateButtonWithColor("Open File Location", _saveButtonColor.Value, ImguiUtils.buttonStyle, GUILayout.ExpandWidth(false)))
                            {
                                Utils.OpenFileLocation(_selectedOtherFile);
                            }

                            GUILayout.FlexibleSpace();

                            if (GUIHelper.CreateButtonWithColor("Delete File", _closeButtonColor.Value, ImguiUtils.redbuttonStyle, GUILayout.ExpandWidth(false)))
                            {
                                File.Delete(_selectedOtherFile);
                                _otherFileContents.Remove(_selectedOtherFile);
                                Logger.LogInfo($"File deleted: {_selectedOtherFile}");
                                _selectedOtherFile = null;
                            }
                        }
                        GUILayout.EndHorizontal();
                    }

                    // 5) Put everything else in a scroll area
                    _settingWindowScrollPos = GUILayout.BeginScrollView(_settingWindowScrollPos, false, true, GUILayout.Width(RightColumnWidth));
                    {
                        // Display plugin settings if on the Plugins tab and one is selected
                        if (_selectedTab == Tab.Plugins && selectedPlugin != null && !string.IsNullOrEmpty(_selectedPluginName))
                        {
                            DrawPluginSettings(selectedPlugin);
                        }
                        // Display file editor if on OtherFiles tab
                        else if (_selectedTab == Tab.OtherFiles && !string.IsNullOrEmpty(_selectedOtherFile))
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
        }

        private void DrawOtherFileEditor(string filePath)
        {
            try
            {
                // Load the file content if not cached
                if (!_otherFileContents.TryGetValue(filePath, out var fileContent))
                {
                    fileContent = File.ReadAllText(filePath);
                    _otherFileContents[filePath] = fileContent; // Cache the content
                }

                GUI.SetNextControlName(FileEditorName);
                // Display the file content in an editable text area
                var updatedContent = GUILayout.TextArea(fileContent, GUILayout.ExpandHeight(true));
                if (updatedContent != _otherFileContents[filePath])
                {
                    _otherFileContents[filePath] = updatedContent; // Update the cached content
                }

                if (GUI.GetNameOfFocusedControl() == FileEditorName)
                {
                    GUI.FocusWindow(WindowId);
                    GUI.FocusControl(FileEditorName);
                }
            }
            catch (Exception ex)
            {
                GUIHelper.CreateLabelWithColor($"Failed to load or edit file: {ex.Message}", _fontColor.Value, ImguiUtils.labelStyle, GUILayout.ExpandWidth(false));
            }
        }


        private void DrawWindowHeader()
        {
            GUILayout.BeginHorizontal(GUI.skin.box);
            {
                GUI.enabled = SearchString == string.Empty;

                var newVal = GUIHelper.CreateToggleWithColor(_showSettings.Value, " Normal settings", style: ImguiUtils.toggleStyle);
                if (_showSettings.Value != newVal)
                {
                    _showSettings.Value = newVal;
                    BuildFilteredSettingList();
                }

                newVal = GUIHelper.CreateToggleWithColor(_showDefault.Value, " Show Default", style: ImguiUtils.toggleStyle);
                if (_showDefault.Value != newVal)
                {
                    _showDefault.Value = newVal;
                    BuildFilteredSettingList();
                }

                newVal = GUIHelper.CreateToggleWithColor(_showKeybinds.Value, " Keyboard shortcuts", style: ImguiUtils.toggleStyle);
                if (_showKeybinds.Value != newVal)
                {
                    _showKeybinds.Value = newVal;
                    BuildFilteredSettingList();
                }

                newVal = GUIHelper.CreateToggleWithColor(_showAdvanced.Value, " Advanced settings", _advancedSettingColor);
                if (_showAdvanced.Value != newVal)
                {
                    _showAdvanced.Value = newVal;
                    BuildFilteredSettingList();
                }

                GUI.enabled = true;

                GUILayout.Space(8);

                newVal = GUIHelper.CreateToggleWithColor(_showDebug, " Debug info", style: ImguiUtils.toggleStyle);
                if (_showDebug != newVal)
                {
                    _showDebug = newVal;
                    BuildSettingList();
                }

                if (GUIHelper.CreateButtonWithColor("Open BepInEx Log", style: ImguiUtils.buttonStyle))
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

                if (GUIHelper.CreateButtonWithColor("Open Player Log", style: ImguiUtils.buttonStyle))
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

                GUILayout.Space(8);
                if (GUIHelper.CreateButtonWithColor("Close", _closeButtonColor.Value, style: ImguiUtils.redbuttonStyle))
                {
                    DisplayingWindow = false;
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUI.skin.box);
            {
                GUIHelper.CreateLabelWithColor("Search: ", _fontColor.Value, ImguiUtils.labelStyle, GUILayout.ExpandWidth(false));


                GUI.SetNextControlName(SearchBoxName);
                SearchString = GUILayout.TextField(SearchString, GUILayout.ExpandWidth(true));

                if (_focusSearchBox)
                {
                    GUI.FocusWindow(WindowId);
                    GUI.FocusControl(SearchBoxName);
                    _focusSearchBox = false;
                }

                if (GUIHelper.CreateButtonWithColor("Clear", _widgetBackgroundColor.Value, ImguiUtils.redbuttonStyle, GUILayout.ExpandWidth(false)))
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
            GUIStyle style = GUI.skin.box.CreateCopy();
            style.hover.background = TexturePool.GetColorTexture(_highlightColor.Value);
            style.normal.background = TexturePool.GetColorTexture(_categorySectionColor.Value);
            style.fontSize = ImguiUtils.fontSize;


            GUILayout.BeginVertical(style);

            var categoryHeader = new GUIContent($"{plugin.Info.Name.TrimStart('!')} {plugin.Info.Version}\n<size=10><color=grey>GUID: {plugin.Info.GUID}</color></size>", null, "GUID: " + plugin.Info.GUID);


            {
                var hasWebsite = plugin.Website != null;
                if (hasWebsite)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(29); // Same as the URL button to keep the plugin name centered
                }

                if (SettingFieldDrawer.DrawPluginHeader(categoryHeader))
                {
                    _tipsPluginHeaderWasClicked = true;
                    _selectedPluginName = plugin.Info.Name;
                }

                if (hasWebsite)
                {
                    if (GUIHelper.CreateButtonWithColor(new GUIContent("URL", null, plugin.Website), ImguiUtils.buttonStyle, _settingDescriptionColor.Value, GUILayout.ExpandWidth(false)))
                        Utils.OpenWebsite(plugin.Website);
                    GUILayout.EndHorizontal();
                }

                GUILayout.BeginHorizontal();
                {
                    GUILayout.FlexibleSpace(); // push it to the right
                    if (IsPinned(plugin.Info.GUID))
                    {
                        // If pinned, show “Unpin” button
                        if (GUIHelper.CreateButtonWithColor("Unpin", _closeButtonColor.Value, ImguiUtils.redbuttonStyle, GUILayout.ExpandWidth(false)))
                        {
                            UnpinPlugin(plugin.Info.GUID);
                            BuildFilteredSettingList();
                        }
                    }
                    else
                    {
                        // If not pinned, show “Pin” button
                        if (GUIHelper.CreateButtonWithColor("Pin", _saveButtonColor.Value, ImguiUtils.buttonStyle, GUILayout.ExpandWidth(false)))
                        {
                            PinPlugin(plugin.Info.GUID);
                            BuildFilteredSettingList();
                        }
                    }
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
        }

        private void DrawPluginSettings(PluginSettingsData selectedPlugin)
        {
            // The top of the visible scroll area
            float scrollY = _settingWindowScrollPos.y;

            // How tall is the viewport (the portion of the scroll area we can see)
            float visibleAreaHeight = SettingWindowRect.height;

            // Tracks how far we've progressed vertically in our custom layout
            float currentY = 0f;

            // Loop over each category
            foreach (var category in selectedPlugin.Categories)
            {
                // Grab the last measured height of this category
                float catHeight = category.CalculatedHeight;

                // Check if this category is potentially visible:
                //   1) catHeight == 0 => we haven't measured it yet, so we should draw it at least once
                //   2) Or the bounding rect intersects with the visible scroll window
                bool isVisible = (catHeight == 0f) || ((currentY + catHeight) >= scrollY && currentY <= (scrollY + visibleAreaHeight));

                // If the category is definitely off-screen, skip drawing
                if (!isVisible)
                {
                    // Just reserve the same vertical space so subsequent elements line up
                    GUILayout.Space(catHeight);
                    // Advance currentY by the category's height
                    currentY += catHeight;
                    continue;
                }

                // If we reach here, the category is visible or unmeasured => we draw
                GUILayout.Space(1);
                // We'll measure how much space this category uses. 
                // We do that by capturing the layout rect "before" we draw, 
                // and again "after" we finish drawing. 
                float startY = 0f;
                if (Event.current.type == EventType.Repaint)
                {
                    // Get the bottom of the last drawn item
                    startY = GUILayoutUtility.GetLastRect().yMax;
                }

                GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandWidth(false), GUILayout.MaxWidth(RightColumnWidth));
                {
                    if (!string.IsNullOrEmpty(category.Name))
                    {
                        if (SettingFieldDrawer.DrawCollapsibleCategoryHeader(category.Name, category.Collapsed))
                        {
                            category.Collapsed = !category.Collapsed;
                        }
                        //SettingFieldDrawer.DrawCategoryHeader(category.Name);
                    }

                    if (!category.Collapsed)
                    {
                        foreach (var setting in category.Settings)
                        {
                            DrawSingleSetting(setting);
                            GUILayout.FlexibleSpace();
                        }
                    }
                }
                GUILayout.EndVertical();

                // Now measure the new layout
                if (Event.current.type == EventType.Repaint)
                {
                    float endY = GUILayoutUtility.GetLastRect().yMax;
                    catHeight = endY - startY;
                    category.CalculatedHeight = catHeight; // Store for next frame
                }

                // Advance our vertical offset
                currentY += catHeight;
            }
        }


        private void DrawOtherFile(string file)
        {
            string fileType = Path.GetExtension(file).ToUpperInvariant().TrimStart('.');
            var fileName = Path.GetFileNameWithoutExtension(file);
            var categoryHeader = _showDebug
                ? new GUIContent($"{fileName} ({fileType})", null, "File Type: " + fileType)
                : new GUIContent($"{fileName} ({fileType})");

            var isSearching = !string.IsNullOrEmpty(SearchString);

            var style = GUI.skin.box.CreateCopy();
            var pooledOtherFileTex = TexturePool.GetTexture2D(1, 1, TextureFormat.RGBA32, false);
            pooledOtherFileTex.SetPixel(0, 0, _highlightColor.Value);
            pooledOtherFileTex.Apply();
            var pooledOtherFileTexNormal = TexturePool.GetTexture2D(1, 1, TextureFormat.RGBA32, false);
            pooledOtherFileTexNormal.SetPixel(0, 0, _categorySectionColor.Value);
            pooledOtherFileTexNormal.Apply();
            style.hover.background = pooledOtherFileTex;
            style.normal.background = pooledOtherFileTexNormal;
            TexturePool.ReleaseTexture2D(pooledOtherFileTex);
            TexturePool.ReleaseTexture2D(pooledOtherFileTexNormal);

            GUILayout.BeginVertical(style);


            //if (SettingFieldDrawer.DrawPluginHeader(categoryHeader) && !isSearching)
            if (SettingFieldDrawer.DrawPluginHeader(categoryHeader) && !isSearching)
            {
                _tipsPluginHeaderWasClicked = true;
                _selectedOtherFile = file;
            }


            GUILayout.EndVertical();
        }


        private void DrawSingleSetting(SettingEntryBase setting)
        {
            var lighterBox = GUI.skin.box.CreateCopy();
            bool isDefaultValue = setting.DefaultValue != null && (setting.Get() != null && setting.Get().Equals(setting.DefaultValue));
            Color bgColor = isDefaultValue ? _categorySectionColor.Value : new Color(_categorySectionColor.Value.r / 2f, _categorySectionColor.Value.g / 2f, _categorySectionColor.Value.b / 2f, _categorySectionColor.Value.a);
            lighterBox.normal.background = TexturePool.GetColorTexture(bgColor);

            GUILayout.BeginHorizontal(lighterBox, GUILayout.ExpandWidth(false), GUILayout.MaxWidth(RightColumnWidth));
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
                    GUIHelper.CreateLabelWithColor("Failed to draw this field, check log for details.", _fontColor.Value, style: ImguiUtils.labelStyle);
                }
            }
            GUILayout.EndHorizontal();
        }

        private void DrawSettingName(SettingEntryBase setting)
        {
            if (!setting.HideSettingName)
            {
                var nameStyle = GUI.skin.label.CreateCopy();
                nameStyle.wordWrap = true;
                nameStyle.fontStyle = FontStyle.Bold;
                nameStyle.normal.textColor = _lightGreenSettingTextColor.Value;
                nameStyle.richText = true;

                var descriptionStyle = GUI.skin.label.CreateCopy();
                descriptionStyle.wordWrap = true;
                descriptionStyle.fontStyle = FontStyle.Italic;
                descriptionStyle.normal.textColor = _settingDescriptionColor.Value;

                var settingTypeStyle = GUI.skin.label.CreateCopy();
                settingTypeStyle.wordWrap = true;
                settingTypeStyle.fontStyle = FontStyle.Italic;
                settingTypeStyle.normal.textColor = _classTypeColor.Value;

                var settingRangeStyle = GUI.skin.label.CreateCopy();
                settingRangeStyle.wordWrap = true;
                settingRangeStyle.fontStyle = FontStyle.Italic;
                settingRangeStyle.normal.textColor = _rangeValueColor.Value;

                var defaultValueStyle = GUI.skin.label.CreateCopy();
                defaultValueStyle.wordWrap = true;
                defaultValueStyle.fontStyle = FontStyle.BoldAndItalic;
                defaultValueStyle.normal.textColor = _defaultValueColor.Value;

                GUILayout.BeginVertical();
                {
                    GUILayout.BeginHorizontal();
                    {
                        string displayName = setting.DispName.TrimStart('!');
                        bool isSynced = setting.Description.Contains("[Synced with Server]");
                        bool isReadOnly = setting.ReadOnly != null && setting.ReadOnly.Value;
                        string syncedIndicator = isSynced ? " <size=20><color=#FF0000>⇅</color></size>" : "";
                        string readOnlyIndicator = isReadOnly ? " <size=20><color=#FF0000>🔒</color></size>" : "";
                        displayName += syncedIndicator;
                        displayName += readOnlyIndicator;

                        GUILayout.Label(displayName, nameStyle, GUILayout.ExpandWidth(false));

                        // Render the type of the setting
                        GUILayout.Label($" ({setting.SettingType.Name})", settingTypeStyle, GUILayout.ExpandWidth(false));

                        // If a range is defined, render min and max
                        if (setting.AcceptableValueRange.Key != null)
                        {
                            var min = setting.AcceptableValueRange.Key;
                            var max = setting.AcceptableValueRange.Value;
                            GUILayout.Label($" [{min} - {max}]", settingRangeStyle, GUILayout.ExpandWidth(false));
                        }

                        // Render the default value if present.
                        if (setting.DefaultValue != null)
                        {
                            GUILayout.Label($" Default: {setting.DefaultValue}", defaultValueStyle, GUILayout.ExpandWidth(false));
                        }
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

            //GUIHelper.BeginColor(_widgetBackgroundColor.Value);

            object defaultValue = setting.DefaultValue;
            if (defaultValue != null || setting.SettingType.IsClass)
            {
                GUILayout.Space(5);
                if (GUIHelper.CreateButtonWithColor("Reset", default, ImguiUtils.buttonStyle, GUILayout.ExpandWidth(false)))
                    setting.Set(defaultValue);
            }

            //GUIHelper.EndColor();
        }

        public static void PinPlugin(string pluginName)
        {
            if (!_pinnedPlugins.Contains(pluginName))
                _pinnedPlugins.Add(pluginName);
            UpdatePluginConfig();
        }

        public static void UnpinPlugin(string pluginName)
        {
            _pinnedPlugins.Remove(pluginName);
            UpdatePluginConfig();
        }

        public static bool IsPinned(string pluginName)
        {
            return _pinnedPlugins.Contains(pluginName);
        }

        public static void UpdatePluginConfig()
        {
            var pinnedPluginsStr = string.Join(",", _pinnedPlugins.ToArray());
            SettingFieldDrawer._instance._pinnedPluginsConfig.Value = pinnedPluginsStr;
            SettingFieldDrawer._instance.Config.Save();
        }

        private void LoadPinnedPluginsFromConfig()
        {
            string pinnedFromConfig = _pinnedPluginsConfig.Value;
            if (!string.IsNullOrEmpty(pinnedFromConfig))
            {
                _pinnedPlugins = new HashSet<string>(pinnedFromConfig.Split(','));
            }
        }

        private void CreateOverlayCanvas()
        {
            if (_overlayCanvasObj != null) return;

            // 1) Create an empty GameObject for our overlay
            _overlayCanvasObj = new GameObject("ConfigurationManagerOverlayCanvas");
            GameObject.DontDestroyOnLoad(_overlayCanvasObj);

            // 2) Add components one by one
            _overlayCanvas = _overlayCanvasObj.AddComponent<Canvas>();
            _overlayCanvasScaler = _overlayCanvasObj.AddComponent<CanvasScaler>();
            _overlayRaycaster = _overlayCanvasObj.AddComponent<GraphicRaycaster>();

            // Make sure it's on top
            _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _overlayCanvas.sortingOrder = 9999;

            // 3) Add a child object with an Image that blocks clicks
            var blockerObj = new GameObject("ConfigurationManagerBlockerImage");
            blockerObj.transform.SetParent(_overlayCanvasObj.transform, false);

            _overlayBlocker = blockerObj.AddComponent<Image>();
            _overlayBlocker.color = new Color(0f, 0f, 0f, 0.5f); // Semi-transparent black
            _overlayBlocker.raycastTarget = true; // IMPORTANT: blocks clicks

            // Stretch the Image to cover the entire screen
            RectTransform rt = _overlayBlocker.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void ShowOverlayCanvas()
        {
            if (_overlayCanvasObj == null) CreateOverlayCanvas();
            _overlayCanvasObj.SetActive(true);
        }

        private void HideOverlayCanvas()
        {
            if (_overlayCanvasObj != null)
                _overlayCanvasObj.SetActive(false);
        }


        private void Start()
        {
            LoadPinnedPluginsFromConfig();

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
            if (DisplayingWindow)
            {
                /*ImguiUtils.CreateBackgrounds();
                ImguiUtils.CreateStyles();*/
                SetUnlockCursor(0, true);
                if (GUI.GetNameOfFocusedControl() == FileEditorName && (Event.current.type == EventType.KeyDown || Event.current.type == EventType.KeyUp) && Event.current.isKey)
                {
                    // Suppress all input to avoid triggering in-game actions
                    Input.ResetInputAxes();

                    // If the user presses escape, unfocus the text area
                    if (Input.GetKeyDown(KeyCode.Escape))
                    {
                        GUI.FocusControl(null);
                        Event.current.Use();
                    }

                    // If the user presses tab, we want to insert a tab character
                    if (Event.current.keyCode == KeyCode.Tab)
                    {
                        if (Event.current.type == EventType.KeyUp)
                        {
                            GUIUtility.keyboardControl = 0;
                            GUIUtility.hotControl = 0;
                            Event.current.Use();
                            GUIUtility.ExitGUI();
                        }
                    }

                    // If the user presses enter, we want to insert a newline character
                    if (Event.current.keyCode == KeyCode.Return)
                    {
                        if (Event.current.type == EventType.KeyUp)
                        {
                            GUIUtility.keyboardControl = 0;
                            GUIUtility.hotControl = 0;
                            Event.current.Use();
                            GUIUtility.ExitGUI();
                        }
                    }

                    return;
                }
            }

            if (OverrideHotkey) return;

            //if (_keybind.Value.IsDown()) DisplayingWindow = !DisplayingWindow;
            if (!DisplayingWindow && _keybind.Value.IsUp())
            {
                ImguiUtils.CreateBackgrounds();

                DisplayingWindow = true;
            }
        }

        private void LateUpdate()
        {
            if (DisplayingWindow) SetUnlockCursor(0, true);
        }

        private void OnDestroy()
        {
            TexturePool.ClearAll();
            TexturePool.ClearCache();
            PluginSettingsDataPool.ClearAll();
            GC.Collect(); // Force GC
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

        internal sealed class PluginSettingsData
        {
            public BepInPlugin Info;
            public List<PluginSettingsGroupData> Categories;
            public int Height;
            public string Website;

            public sealed class PluginSettingsGroupData
            {
                public string Name;
                public List<SettingEntryBase> Settings;
                public float CalculatedHeight;
                public bool Collapsed { get; set; } = true;
            }
        }
    }
}