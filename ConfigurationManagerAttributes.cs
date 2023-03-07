/// <summary>
/// Class that specifies how a setting should be displayed inside the ConfigurationManager settings window.
/// 
/// Usage:
/// This class template has to be copied inside the plugin's project and referenced by its code directly.
/// make a new instance, assign any fields that you want to override, and pass it as a tag for your setting.
/// 
/// If a field is null (default), it will be ignored and won't change how the setting is displayed.
/// If a field is non-null (you assigned a value to it), it will override default behavior.
/// </summary>
/// 
/// <example> 
/// Here's an example of overriding order of settings and marking one of the settings as advanced:
/// <code>
/// // Override IsAdvanced and Order
/// Config.Bind("X", "1", 1, new ConfigDescription("", null, new ConfigurationManagerAttributes { IsAdvanced = true, Order = 3 }));
/// // Override only Order, IsAdvanced stays as the default value assigned by ConfigManager
/// Config.Bind("X", "2", 2, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 1 }));
/// Config.Bind("X", "3", 3, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 2 }));
/// </code>
/// </example>
/// 
/// <remarks> 
/// You can read more and see examples in the readme at https://github.com/BepInEx/BepInEx.ConfigurationManager
/// You can optionally remove fields that you won't use from this class, it's the same as leaving them null.
/// </remarks>
#pragma warning disable 0169, 0414, 0649
internal sealed class ConfigurationManagerAttributes
{
    /// <summary>
    /// Should the setting be shown as a percentage (only use with value range settings).
    /// </summary>
    public bool? ShowRangeAsPercent;

    /// <summary>
    /// Custom setting editor (OnGUI code that replaces the default editor provided by ConfigurationManager).
    /// See below for a deeper explanation. Using a custom drawer will cause many of the other fields to do nothing.
    /// </summary>
    public System.Action<BepInEx.Configuration.ConfigEntryBase> CustomDrawer;

    /// <summary>
    /// Custom setting editor that allows polling keyboard input with the Input (or UnityInput) class.
    /// Use either CustomDrawer or CustomHotkeyDrawer, using both at the same time leads to undefined behaviour.
    /// </summary>
    public CustomHotkeyDrawerFunc CustomHotkeyDrawer;

    /// <summary>
    /// Custom setting draw action that allows polling keyboard input with the Input class.
    /// Note: Make sure to focus on your UI control when you are accepting input so user doesn't type in the search box or in another setting (best to do this on every frame).
    /// If you don't draw any selectable UI controls You can use `GUIUtility.keyboardControl = -1;` on every frame to make sure that nothing is selected.
    /// </summary>
    /// <example>
    /// CustomHotkeyDrawer = (ConfigEntryBase setting, ref bool isEditing) =>
    /// {
    ///     if (isEditing)
    ///     {
    ///         // Make sure nothing else is selected since we aren't focusing on a text box with GUI.FocusControl.
    ///         GUIUtility.keyboardControl = -1;
    ///                     
    ///         // Use Input.GetKeyDown and others here, remember to set isEditing to false after you're done!
    ///         // It's best to check Input.anyKeyDown and set isEditing to false immediately if it's true,
    ///         // so that the input doesn't have a chance to propagate to the game itself.
    /// 
    ///         if (GUILayout.Button("Stop"))
    ///             isEditing = false;
    ///     }
    ///     else
    ///     {
    ///         if (GUILayout.Button("Start"))
    ///             isEditing = true;
    ///     }
    /// 
    ///     // This will only be true when isEditing is true and you hold any key
    ///     GUILayout.Label("Any key pressed: " + Input.anyKey);
    /// }
    /// </example>
    /// <param name="setting">
    /// Setting currently being set (if available).
    /// </param>
    /// <param name="isCurrentlyAcceptingInput">
    /// Set this ref parameter to true when you want the current setting drawer to receive Input events.
    /// The value will persist after being set, use it to see if the current instance is being edited.
    /// Remember to set it to false after you are done!
    /// </param>
    public delegate void CustomHotkeyDrawerFunc(BepInEx.Configuration.ConfigEntryBase setting, ref bool isCurrentlyAcceptingInput);

    /// <summary>
    /// Show this setting in the settings screen at all? If false, don't show.
    /// </summary>
    public bool? Browsable;

    /// <summary>
    /// Category the setting is under. Null to be directly under the plugin.
    /// </summary>
    public string Category;

    /// <summary>
    /// If set, a "Default" button will be shown next to the setting to allow resetting to default.
    /// </summary>
    public object DefaultValue;

    /// <summary>
    /// Force the "Reset" button to not be displayed, even if a valid DefaultValue is available. 
    /// </summary>
    public bool? HideDefaultButton;

    /// <summary>
    /// Force the setting name to not be displayed. Should only be used with a <see cref="CustomDrawer"/> to get more space.
    /// Can be used together with <see cref="HideDefaultButton"/> to gain even more space.
    /// </summary>
    public bool? HideSettingName;

    /// <summary>
    /// Optional description shown when hovering over the setting.
    /// Not recommended, provide the description when creating the setting instead.
    /// </summary>
    public string Description;

    /// <summary>
    /// Name of the setting.
    /// </summary>
    public string DispName;

    /// <summary>
    /// Order of the setting on the settings list relative to other settings in a category.
    /// 0 by default, higher number is higher on the list.
    /// </summary>
    public int? Order;

    /// <summary>
    /// Only show the value, don't allow editing it.
    /// </summary>
    public bool? ReadOnly;

    /// <summary>
    /// If true, don't show the setting by default. User has to turn on showing advanced settings or search for it.
    /// </summary>
    public bool? IsAdvanced;

    /// <summary>
    /// Custom converter from setting type to string for the built-in editor textboxes.
    /// </summary>
    public System.Func<object, string> ObjToStr;

    /// <summary>
    /// Custom converter from string to setting type for the built-in editor textboxes.
    /// </summary>
    public System.Func<string, object> StrToObj;
}
