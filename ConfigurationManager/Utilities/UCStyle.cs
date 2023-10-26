using UnityEngine;

namespace ConfigurationManager.Utilities
{
    internal static class UCStyle
    {
        public static GUIStyle TextArea()
        {
            var t = GUI.skin.textArea;
            t.fontSize = Utils.Config.FontSize;
            return t;
        }

        public static GUIStyle Slider()
        {
            var t = GUI.skin.horizontalSlider;
            t.fontSize = Utils.Config.FontSize;
            return t;
        }
        public static GUIStyle TextFieldStyle()
        {
            var t = GUI.skin.textField;
            t.fontSize = Utils.Config.FontSize;
            return t;
        }

        public static GUIStyle ToggleStyle()
        {
            var t = GUI.skin.toggle;
            t.fontSize = Utils.Config.FontSize;
            return t;
        }
        public static GUIStyle ButtonStyle()
        {

            var t = GUI.skin.button;
            t.fontSize = Utils.Config.FontSize;
            return t;
        }

        public static GUIStyle LabelStyle(bool MiddleCenter = false)
        {

            var t = GUI.skin.label;
            t.fontSize = Utils.Config.FontSize;
            t.alignment = MiddleCenter ? TextAnchor.MiddleCenter : TextAnchor.UpperCenter;
            return t;
        }
    }
}
