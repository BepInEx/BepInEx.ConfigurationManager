// Popup list created by Eric Haines
// ComboBox Extended by Hyungseok Seo.(Jerry) sdragoon@nate.com
// this oop version of ComboBox is refactored by zhujiangbo jumbozhu@gmail.com
// Modified by MarC0 / ManlyMarco

using UnityEngine;

namespace ConfigurationManager.Utilities
{
    internal class ComboBox
    {
        private static bool forceToUnShow;
        private static int useControlID = -1;
        private readonly string buttonStyle;
        private bool isClickedComboButton;
        private readonly GUIContent[] listContent;
        private readonly GUIStyle listStyle;
        private readonly int _windowYmax;

        public ComboBox(Rect rect, GUIContent buttonContent, GUIContent[] listContent, GUIStyle listStyle, float windowYmax)
        {
            Rect = rect;
            ButtonContent = buttonContent;
            this.listContent = listContent;
            buttonStyle = "button";
            this.listStyle = listStyle;
            _windowYmax = (int)windowYmax;
        }

        public Rect Rect { get; set; }

        public GUIContent ButtonContent { get; set; }

        public void Show(Action<int> onItemSelected)
        {
            if (forceToUnShow)
            {
                forceToUnShow = false;
                isClickedComboButton = false;
            }

            var done = false;
            var controlID = GUIUtility.GetControlID(FocusType.Passive);

            Vector2 currentMousePosition = Vector2.zero;
            if (Event.current.GetTypeForControl(controlID) == EventType.MouseUp)
            {
                if (isClickedComboButton)
                {
                    done = true;
                    currentMousePosition = Event.current.mousePosition;
                }
            }

            if (GUI.Button(Rect, ButtonContent, buttonStyle))
            {
                if (useControlID == -1)
                {
                    useControlID = controlID;
                    isClickedComboButton = false;
                }

                if (useControlID != controlID)
                {
                    forceToUnShow = true;
                    useControlID = controlID;
                }
                isClickedComboButton = true;
            }

            if (isClickedComboButton)
            {
                bool enabled = GUI.enabled;
                if (enabled)
                    GUI.enabled = false;

                var location = GUIUtility.GUIToScreenPoint(new Vector2(Rect.x, Rect.yMax));
                var size = new Vector2(Rect.width, listStyle.CalcHeight(listContent[0], 1) * listContent.Length);

                var innerRect = new Rect(0, 0, size.x, size.y);

                var outerRect = new Rect(location.x, location.y, size.x, size.y);
                if (outerRect.yMax > _windowYmax)
                {
                    outerRect.height = _windowYmax - outerRect.y;
                    outerRect.width += 20;
                }

                if (currentMousePosition != Vector2.zero && outerRect.Contains(GUIUtility.GUIToScreenPoint(currentMousePosition)))
                    done = false;

                CurrentDropdownDrawer = () =>
                {
                    GUI.enabled = true;

                    outerRect.position = GUIUtility.ScreenToGUIPoint(location);

                    Utils.DrawContolBackground(outerRect);

                    _scrollPosition = GUI.BeginScrollView(outerRect, _scrollPosition, innerRect, false, false);
                    {
                        const int initialSelectedItem = -1;
                        var newSelectedItemIndex = GUI.SelectionGrid(innerRect, initialSelectedItem, listContent, 1, listStyle);
                        if (newSelectedItemIndex != initialSelectedItem)
                        {
                            onItemSelected(newSelectedItemIndex);
                            isClickedComboButton = false;
                        }
                    }
                    GUI.EndScrollView();
                };

                if (enabled)
                    GUI.enabled = true;
            }

            if (done)
                isClickedComboButton = false;
        }

        private Vector2 _scrollPosition = Vector2.zero;
        public static Action CurrentDropdownDrawer { get; set; }
    }
}