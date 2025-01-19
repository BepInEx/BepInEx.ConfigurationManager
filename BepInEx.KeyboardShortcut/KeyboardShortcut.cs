// This is copied from BepInEx 5 and modified to work with BepInEx 6 nightlies where this type is missing
// This is a temporary fix until this struct is added to BepInEx 6 proper

using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using System.Reflection;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace BepInEx.Configuration
{
    /// <summary>
    /// A keyboard shortcut that can be used in Update method to check if user presses a key combo. The shortcut is only
    /// triggered when the user presses the exact combination. For example, <c>F + LeftCtrl</c> will trigger only if user
    /// presses and holds only LeftCtrl, and then presses F. If any other keys are pressed, the shortcut will not trigger.
    ///
    /// Can be used as a value of a setting in <see cref="ConfigFile.Bind{T}(ConfigDefinition,T,ConfigDescription)"/>
    /// to allow user to change this shortcut and have the changes saved.
    ///
    /// How to use: Use <see cref="IsDown"/> in this class instead of <see cref="Input.GetKeyDown(KeyCode)"/> in the Update loop.
    /// </summary>
    public struct KeyboardShortcut
    {
        static KeyboardShortcut()
        {
            TomlTypeConverter.AddConverter(
                typeof(KeyboardShortcut),
                new TypeConverter
                {
                    ConvertToString = (o, type) => ((KeyboardShortcut)o).Serialize(),
                    ConvertToObject = (s, type) => Deserialize(s)
                });
        }

        /// <summary>
        /// Shortcut that never triggers.
        /// </summary>
        public static readonly KeyboardShortcut Empty = new KeyboardShortcut();

        /// <summary>
        /// All KeyCode values that can be used in a keyboard shortcut.
        /// </summary>
        public static readonly IEnumerable<KeyCode> AllKeyCodes = Enum.GetValues(typeof(KeyCode)) as KeyCode[];

        // Don't block hotkeys if mouse is being pressed, e.g. when shooting and trying to strafe
        public static readonly KeyCode[] ModifierBlockKeyCodes = AllKeyCodes.Except(new KeyCode[] {
            KeyCode.Mouse0, KeyCode.Mouse1, KeyCode.Mouse2, KeyCode.Mouse3,
            KeyCode.Mouse4, KeyCode.Mouse5, KeyCode.Mouse6, KeyCode.None }).ToArray();

        private readonly KeyCode[] _allKeys;

#if true
        private static MethodInfo logMethod = typeof(Logger).GetMethod(
            "Log", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null,
            new Type[] { typeof(LogLevel), typeof(object) }, null);

#endif
        /// <summary>
        /// Create a new keyboard shortcut.
        /// </summary>
        /// <param name="mainKey">Main key to press</param>
        /// <param name="modifiers">Keys that should be held down before main key is registered</param>
        public KeyboardShortcut(KeyCode mainKey, params KeyCode[] modifiers) : this(new[] { mainKey }.Concat(modifiers).ToArray())
        {
            if (mainKey == KeyCode.None && modifiers.Any())
                throw new ArgumentException($"Can't set {nameof(mainKey)} to KeyCode.None if there are any {nameof(modifiers)}");
        }

        private KeyboardShortcut(KeyCode[] keys)
        {
            _allKeys = SanitizeKeys(keys);
        }

        private static KeyCode[] SanitizeKeys(params KeyCode[] keys)
        {
            return keys != null && keys.Length != 0 && keys[0] != KeyCode.None
                ? new KeyCode[] { keys[0] }.Concat(keys.Skip(1).Distinct().Where(x => x != keys[0]).OrderBy(x => (int)x)).ToArray()
                : new KeyCode[] { KeyCode.None };
        }

        /// <summary>
        /// Main key of the key combination. It has to be pressed / let go last for the combination to be triggered.
        /// If the combination is empty, <see cref="KeyCode.None"/> is returned.
        /// </summary>
        public KeyCode MainKey => _allKeys != null && _allKeys.Length > 0 ? _allKeys[0] : KeyCode.None;

        /// <summary>
        /// Modifiers of the key combination, if any.
        /// </summary>
        public IEnumerable<KeyCode> Modifiers => _allKeys?.Skip(1) ?? Enumerable.Empty<KeyCode>();

        /// <summary>
        /// Attempt to deserialize key combination from the string.
        /// </summary>
        public static KeyboardShortcut Deserialize(string str)
        {
            try
            {
                KeyCode[] parts = str.Split(new[] { ' ', '+', ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(x => (KeyCode)Enum.Parse(typeof(KeyCode), x)).ToArray();
                return new KeyboardShortcut(parts);
            }
            catch (SystemException ex)
            {
#if false
                Logger.Log(LogLevel.Error, "Failed to read keybind from settings: " + ex.Message);
#else
                logMethod?.Invoke(null, new object[] { LogLevel.Error, "Failed to read keybind from settings: " + ex.Message });
#endif
            }
            return Empty;
        }

        /// <summary>
        /// Serialize the key combination into a user readable string.
        /// </summary>
        public string Serialize()
        {
            return _allKeys != null
                ? string.Join(" + ", _allKeys.Select(x => x.ToString()).ToArray())
                : string.Empty;
        }

        /// <summary>
        /// Check if the main key was just pressed (Input.GetKeyDown), and specified modifier keys are all pressed
        /// </summary>
        public bool IsDown()
        {
            KeyCode mainKey = MainKey;
            return mainKey != KeyCode.None && Input.GetKeyDown(mainKey) && ModifierKeyTest();
        }

        /// <summary>
        /// Check if the main key is currently held down (Input.GetKey), and specified modifier keys are all pressed
        /// </summary>
        public bool IsPressed()
        {
            KeyCode mainKey = MainKey;
            return mainKey != KeyCode.None && Input.GetKey(mainKey) && ModifierKeyTest();
        }

        /// <summary>
        /// Check if the main key was just lifted (Input.GetKeyUp), and specified modifier keys are all pressed.
        /// </summary>
        public bool IsUp()
        {
            KeyCode mainKey = MainKey;
            return mainKey != KeyCode.None && Input.GetKeyUp(mainKey) && ModifierKeyTest();
        }

        private bool ModifierKeyTest()
        {
            KeyCode mainKey = MainKey;
            return
                _allKeys.All(key => key == mainKey || Input.GetKey(key)) &&
                ModifierBlockKeyCodes.Except(_allKeys).All(key => !Input.GetKey(key));
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return MainKey != KeyCode.None
                ? string.Join(" + ", _allKeys.Select(key => key.ToString()).ToArray())
                : "Not set";
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is KeyboardShortcut shortcut && MainKey == shortcut.MainKey && Modifiers.SequenceEqual(shortcut.Modifiers);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return MainKey != KeyCode.None
                ? _allKeys.Aggregate(_allKeys.Length, (current, item) => unchecked(current * 31 + (int)item))
                : 0;
        }
    }
}
