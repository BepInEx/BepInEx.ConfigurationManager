using UnityEngine;

namespace ConfigurationManager.Utilities
{
    // Copied from: https://github.com/fuqunaga/RapidGUI/blob/master/Runtime/Component/Utilities/FocusChecker.cs
    public static class GUIFocus
    {
        static int _lastFrameCount;
        static int _lastHotControl;
        static int _lastKeyboardControl;
        static bool _hasChanged;

        public static bool HasChanged()
        {
            int frameCount = Time.frameCount;

            if (_lastFrameCount == frameCount)
            {
                return _hasChanged;
            }

            _lastFrameCount = frameCount;

            int hotControl = GUIUtility.hotControl;
            int keyboardControl = GUIUtility.keyboardControl;

            _hasChanged = (hotControl != _lastHotControl) || (keyboardControl != _lastKeyboardControl);

            if (_hasChanged)
            {
                _lastHotControl = hotControl;
                _lastKeyboardControl = keyboardControl;
            }

            return _hasChanged;
        }
    }
}