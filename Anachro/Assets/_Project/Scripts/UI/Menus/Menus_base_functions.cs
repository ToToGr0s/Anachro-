using UnityEngine;
using UnityEngine.UIElements;

namespace Anachronism
{
    public class Menus_base_functions : MonoBehaviour
    {
        protected UIDocument uiDocument;
        protected VisualElement root;

        protected void initialize()
        {
            uiDocument = gameObject.GetComponent<UIDocument>();
            root = uiDocument.rootVisualElement;
        }
        
        //functions
        protected void OpenMenu(VisualElement menu)
        {
            menu.style.display = DisplayStyle.Flex;
        }

        protected void CloseMenu(VisualElement menu)
        {
            menu.style.display = DisplayStyle.None;
        }
    }
}
