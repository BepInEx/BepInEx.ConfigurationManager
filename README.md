## Plugin / mod configuration manager for BepInEx 5
An easy way to let user configure how a plugin behaves without the need to make your own GUI. The user can change any of the settings you expose, even keyboard shortcuts.

The configuration manager can be accessed in-game by pressing the hotkey (by default F1). Hover over the setting names to see their descriptions, if any.

![Configuration manager](Screenshot.PNG)

## How to use
- Install a build of BepInEx 5 from at least 26/09/2019 (older won't work).
- Download latest release from the Releases tab above.
- Place the .dll inside your BepInEx\Plugins folder.
- Start the game and press F1.

Note: The .xml file is useful for plugin developers when referencing ConfigurationManager.dll in your plugin, it will provide descriptions for types and methods to your IDE. Users can ignore it.

## How to make my mod compatible?
ConfigurationManager will automatically display all settings from your plugin's `Config`. All metadata (e.g. description, value range) will be used by ConfigurationManager to display the settings to the user.

In most cases you don't have to reference ConfiguraitonManager.dll or do anything special with your settings. Simply make sure to add as much metadata as possible (doing so will help all users, even if they use the config files directly). Always add descriptive section and key names, descriptions, and acceptable value lists or ranges (wherever applicable).

### How to make my setting into a slider?
Specify `AcceptableValueRange` when creating your setting. If the range is 0f - 1f or 0 - 100 the slider will be shown as % (this can be overridden below).
```c#
CaptureWidth = Config.AddSetting("Section", "Key", 1, new ConfigDescription("Description", new AcceptableValueRange<int>(0, 100)));
```

### How to make my setting into a drop-down list?
Specify `AcceptableValueList` when creating your setting. If you use an enum you don't need to specify AcceptableValueList, all of the enum values will be shown. If you want to hide some values, you will have to use the attribute.

Note: You can add `System.ComponentModel.DescriptionAttribute` to your enum's items to override their displayed names. For example:
```c#
public enum MyEnum
{
    // Entry1 will be shown in the combo box as Entry1
    Entry1,
    [Description("Entry2 will be shown in the combo box as this string")]
    Entry2
}
```

### How to allow user to change my keyboard shorcuts / How to easily check for key presses?
Add a setting of type KeyboardShortcut. Use the value of this setting to check for inputs (recommend using IsDown) inside of your Update method.

The KeyboardShortcut class supports modifier keys - Shift, Control and Alt. They are properly handled, preventing common problems like K+Shift+Control triggering K+Shift when it shouldn't have.
```c#
private ConfigEntry<KeyboardShortcut> ShowCounter { get; set; }

public Constructor()
{
    ShowCounter = Config.AddSetting("Hotkeys", "Show FPS counter", new KeyboardShortcut(KeyCode.U, KeyCode.LeftShift));
}

private void Update()
{
    if (ShowCounter.Value.IsDown())
    {
        // Handle the key press
    }
}
```

## Overriding default Configuration Manager behavior
You can override most of the properties of a setting shown inside the configuration manager window by passing an instance of a specially named class as a tag of your setting. You simply have to place the code of this class anywhere in your code, it will work as long as its name remains unchanged. Here's an example of overriding order of settings and marking one of the settings as advanced:
```c#
// Place the class anywhere in your code, you can remove parts of it that you won't use
internal sealed class ConfigurationManagerAttributes
{
    public bool? IsAdvanced;
    public int? Order;
}

// When creating settings, add an instance of this class as a tag. Don't forget to set the overriden values.
// Override IsAdvanced and Order
Config.AddSetting("X", "1", 1, new ConfigDescription("", null, new ConfigurationManagerAttributes { IsAdvanced = true, Order = 3 }));
// Override only Order, IsAdvanced stays as the default value assigned by ConfigManager
Config.AddSetting("X", "2", 2, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 1 }));
Config.AddSetting("X", "3", 3, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 2 }));
```
Here's a full template of this special class with all available fields to override. Name of this class, as well as names, types and access modifiers of its fields cannot be changed. You can copy it to a new .cs file and add it to your project.
```c#
/// <summary>
/// Special class that controls how a setting is displayed inside ConfigurationManager.
/// To use, make a new instance, assign any fields that you want to override, and pass it as a setting tag.
/// 
/// If a field is null (default), it will be ignored and won't change how the setting is displayed.
/// If a field is non-null (you assigned a value to it), it will override default behavior.
/// You can optionally remove fields that you won't use from this class.
/// </summary>
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
    public Action<BepInEx.Configuration.ConfigEntryBase> CustomDrawer;

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
    /// 0 by default, lower is higher on the list.
    /// </summary>
    public int? Order;

    /// <summary>
    /// Only show the value, don't allow editing it.
    /// </summary>
    public bool? ReadOnly;

    /// <summary>
    /// Don't show the setting by default, user has to turn on showing advanced settings or search for it.
    /// </summary>
    public bool? IsAdvanced;

    /// <summary>
    /// Custom converter from setting type to string for the textbox.
    /// </summary>
    public Func<object, string> ObjToStr;

    /// <summary>
    /// Custom converter from string to setting type for the textbox.
    /// </summary>
    public Func<string, object> StrToObj;
}
```

### How to make a custom editor for my setting?
If you are using a setting type that is not supported by ConfigurationManager, you can add a drawer Action for it. The Action will be executed inside OnGUI, use GUILayout to draw your setting as shown in the example below.

To use a custom seting drawer for an individual setting, use the `CustomDrawer` field in the attribute class. See above for more info on the attribute class.
```c#
// Add the attribute override class
internal sealed class ConfigurationManagerAttributes
{
    public Action<BepInEx.Configuration.ConfigEntryBase> CustomDrawer;
}

void Start()
{
    // Add the drawer as a tag to this setting.
    Config.AddSetting("Section", "Key", "Some value" 
        new ConfigDescription("Desc", null, new ConfigurationManagerAttributes{ CustomDrawer = MyDrawer });
}

static void MyDrawer(BepInEx.Configuration.ConfigEntryBase entry)
{
    // Make sure to use GUILayout.ExpandWidth(true) to use all available space
    GUILayout.Label(entry.BoxedValue, GUILayout.ExpandWidth(true));
}
```
#### Add a custom editor globally
You can specify a drawer for all settings of a setting type. Do this by using `ConfigurationManager.RegisterCustomSettingDrawer(Type, Action<SettingEntryBase>)`.

**Warning:** This requires you to reference ConfigurationManager.dll in your project and is not recommended unless you are sure all users will have it installed. It's usually better to use the above method to add the custom drawer to each setting individually instead.
```c#
void Start()
{
    ConfigurationManager.RegisterCustomSettingDrawer(typeof(MyType), CustomDrawer);
}

static void CustomDrawer(SettingEntryBase entry)
{
    GUILayout.Label((MyType)entry.Get(), GUILayout.ExpandWidth(true));
}
```
