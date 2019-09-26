## Plugin / mod configuration manager for BepInEx
An easy way to let user configure how a plugin behaves without the need to make your own GUI. The user can change any of the settings you expose, even keyboard shortcuts.

The configuration manager can be accessed in-game by pressing the hotkey (by default F1). Hover over the setting names to see their descriptions, if any.

![Configuration manager](Screenshot.PNG)

## How to make my mod compatible?
ConfigurationManager will automatically display all settings from your plugin's `Config`. All metadata (e.g. description, value range) will be used by ConfigurationManager to display the settings to the user.

In most cases there is no need to do anything special, just make sure to add as much metadata as possible (doing so will help all users, even if they use the config files directly). Always add descriptive section and key names, descriptions, and acceptable value lists or ranges (wherever applicable).

### How to make my setting into a slider?
Specify `AcceptableValueRange` when creating your setting. If the range is 0f - 1f or 0 - 100 the slider will be shown as %.
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

### How to make a custom editor for my setting?
If you are using a setting type that is not supported by ConfigurationManager, you can add a drawer for it by using `ConfigurationManager.RegisterCustomSettingDrawer(Type, Action<SettingEntryBase>)`. The Action will be executed inside OnGUI, use GUILayout to draw your setting as shown in the example below.
```c#
void Start()
{
    ConfigurationManager.RegisterCustomSettingDrawer(typeof(MyType), CustomDrawer);
}

static void CustomDrawer(SettingEntryBase entry)
{
    // Make sure to use GUILayout.ExpandWidth(true) to use all available space
    GUILayout.Label(entry.Get(), GUILayout.ExpandWidth(true));
}
```

You can also use a custom seting drawer for each individual setting. To do this, add the `Action<SettingEntryBase>` as a tag when creating your setting.
```c#
void Start()
{
    // Add the drawer as a tag to this setting. 
    // WARNING: Make sure that the type of the delegate is Action<SettingEntryBase> or it might not work!
    Config.AddSetting("Section", "Key", new ConfigDescription("Desc", null, new Action<SettingEntryBase>(CustomDrawer)));
}

static void CustomDrawer(SettingEntryBase entry)
...
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

### How to hide my settings from the manager window?
You can mark your entire plugin class with the `System.ComponentModel.BrowsableAttribute (false)` to hide all of your settings from the ConfigurationManager. To hide only some of the settings, pass a string `"Hidden"` as one of the tags of these settings.

### How to prevent user from changing a setting (read only)?
If you want to prevent user from editing a setting (so they can only see its value), pass a string `"ReadOnly"` as one of the tags.

### How to mark the setting as advanced to hide it by default?
If you want to mark a setting as advanced (it won't be shown in the list unless user turns on "Show advanced" or searches for it), pass a string `"Advanced"` as one of the tags.
