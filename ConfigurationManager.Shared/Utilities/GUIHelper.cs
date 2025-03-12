using System.Collections.Generic;
using UnityEngine;

namespace ConfigurationManager.Utilities
{
    /// <summary>
    /// Helper class for GUI operations.
    /// </summary>
    public static class GUIHelper
    {
        private static readonly Stack<Color> _colorStack = new Stack<Color>();
        private static int _lastFrameCount;
        private static int _lastHotControl;
        private static int _lastKeyboardControl;
        private static bool _hasChanged;


        // Red close button (#BF3030)
        public static readonly Color RedCloseButton = new Color(0.749f, 0.188f, 0.188f, 1f);

// Darker red cancel button (#541B1B)
        public static readonly Color DarkRedCancelButton = new Color(0.329f, 0.106f, 0.106f, 1f);

// Light green for setting text (#A7EDA7)
        public static readonly Color LightGreenSettingText = new Color(0.655f, 0.929f, 0.655f, 1f);

// Dark green for save button (#1C401B)
        public static readonly Color DarkGreenSaveButton = new Color(0.110f, 0.251f, 0.106f, 1f);

// Dark grey for background of left panel (#262626)
        public static readonly Color DarkGreyLeftPanel = new Color(0.153f, 0.153f, 0.153f, 1f);

// Black background for entire panel (#0D0D0D)
        public static readonly Color BlackPanelBackground = new Color(0.05f, 0.05f, 0.05f, 1f);

// White background for entire panel (#0D0D0D)
        public static readonly Color WhitePanelBackground = new Color(1f, 1f, 1f, 1f);

// Medium black background for category section (#1F1F1F)
        public static readonly Color MediumBlackCategorySection = new Color(0.122f, 0.122f, 0.122f, 1f);

// Medium black background for category header (#121212)
        public static readonly Color MediumBlackCategoryHeader = new Color(0.071f, 0.071f, 0.071f, 1f);

// Light grey for sliders (#4C4C4C)
        public static readonly Color LightGreySliders = new Color(0.298f, 0.298f, 0.298f, 1f);

// Medium grey for sliders (#404040)
        public static readonly Color MediumGreySliders = new Color(0.251f, 0.251f, 0.251f, 1f);

        public static readonly Color SettingDescription = new Color(0.4f, 0.4f, 0.4f, 1f);

// Green for class/type name (#148B32)
        public static readonly Color GreenClassTypeName = new Color(0.078f, 0.545f, 0.125f, 1f);

// Yellow/tan for highlight (#989076)
        public static readonly Color YellowTanHighlight = new Color(0.6f, 0.55f, 0.45f, 0.5f);

        // Default value color (#FFF4AC)
        public static readonly Color DefaultValueColor = new Color(1f, 0.95f, 0.67f, 1f);

        public static readonly Color RangeValueColor = Color.cyan;


        /// <summary>
        /// Begins the color change.
        /// </summary>
        /// <param name="color"></param>
        public static void BeginColor(Color color)
        {
            if (color == default) return;
            _colorStack.Push(GUI.color);
            GUI.color = color;
        }

        /// <summary>
        /// Ends the color change.
        /// </summary>
        public static void EndColor()
        {
            if (_colorStack.Count > 0) GUI.color = _colorStack.Pop();
        }

        /// <summary>
        /// Checks if the enter key is pressed.
        /// </summary>
        /// <returns></returns>
        public static bool IsEnterPressed()
        {
            return Event.current.isKey && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
        }

        /// <summary>
        /// Checks if the GUI has changed.
        /// </summary>
        /// <returns></returns>
        public static bool HasChanged()
        {
            int frameCount = Time.frameCount;
            if (_lastFrameCount == frameCount)
                return _hasChanged;
            _lastFrameCount = frameCount;
            int hotControl = GUIUtility.hotControl;
            int keyboardControl = GUIUtility.keyboardControl;
            _hasChanged = hotControl != _lastHotControl || keyboardControl != _lastKeyboardControl;
            if (_hasChanged)
            {
                _lastHotControl = hotControl;
                _lastKeyboardControl = keyboardControl;
            }

            return _hasChanged;
        }

        /// <summary>
        /// Creates a label with a specified color. Optionally a style can be specified.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="color"></param>
        /// <param name="style"></param>
        /// <param name="options"></param>
        public static void CreateLabelWithColor(string text, Color color = default, GUIStyle style = null, params GUILayoutOption[] options)
        {
            BeginColor(color);
            if (style == null)
            {
                GUILayout.Label(text, options);
            }
            else
            {
                GUILayout.Label(text, style, options);
            }

            EndColor();
        }

        /// <summary>
        /// Creates a button with a specified color. Optionally a style can be specified.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="color"></param>
        /// <param name="style"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static bool CreateButtonWithColor(string text, Color color = default, GUIStyle style = null, params GUILayoutOption[] options)
        {
            //BeginColor(color);
            bool value = style == null ? GUILayout.Button(text, options) : GUILayout.Button(text, style, options);
            //EndColor();
            return value;
        }
        
        /// <summary>
        /// Creates a button with a specified color. Optionally a style can be specified.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="r"></param>
        /// <param name="style"></param>
        /// <returns></returns>
        public static bool CreateButtonWithColor(string text, Rect r, GUIStyle style = null)
        {
            return style == null ? GUI.Button(r, text) : GUI.Button(r, text, style);
        }

        /// <summary>
        /// Creates a button with a specified color. Optionally a style can be specified.
        /// </summary>
        /// <param name="content"></param>
        /// <param name="style"></param>
        /// <param name="color"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static bool CreateButtonWithColor(GUIContent content, GUIStyle style, Color color = default, params GUILayoutOption[] options)
        {
            // BeginColor(color);
            bool value = style == null ? GUILayout.Button(content, options) : GUILayout.Button(content, style, options);
            // EndColor();
            return value;
        }

        /// <summary>
        /// Creates a toggle with a specified color. Optionally a style can be specified.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="text"></param>
        /// <param name="color"></param>
        /// <param name="style"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static bool CreateToggleWithColor(bool value, string text, Color color = default, GUIStyle style = null, params GUILayoutOption[] options)
        {
            //BeginColor(color);
            bool newValue = style == null ? GUILayout.Toggle(value, text, options) : GUILayout.Toggle(value, text, style, options);
            //EndColor();
            return newValue;
        }

        /// <summary>
        /// Creates a window with a specified color. Optionally a style can be specified.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="screenRect"></param>
        /// <param name="func"></param>
        /// <param name="text"></param>
        /// <param name="color"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static Rect CreateWindowWithColor(int id, Rect screenRect, GUI.WindowFunction func, string text, Color color = default, params GUILayoutOption[] options)
        {
            BeginColor(color);
            Rect value = GUILayout.Window(id, screenRect, func, text);
            EndColor();
            return value;
        }

        public static string CreateTextFieldWithColor(string text, Color color = default, params GUILayoutOption[] options)
        {
            BeginColor(color);
            string value = GUILayout.TextField(text, options);
            EndColor();
            return value;
        }
    }
}