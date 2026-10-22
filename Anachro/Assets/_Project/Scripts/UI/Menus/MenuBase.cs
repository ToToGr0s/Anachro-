using UnityEngine;
using UnityEngine.UIElements;

namespace Anachronism.UI
{
    /// <summary>
    /// Base commune des menus UI Toolkit : accès au UIDocument et fonctions partagées.
    /// Les menus récupèrent leurs éléments dans OnEnable : UIDocument reconstruit son arbre à chaque activation,
    /// des références prises dans Awake pointeraient vers l'ancien arbre.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public abstract class MenuBase : MonoBehaviour
    {
        private UIDocument _document;

        protected VisualElement Root => _document.rootVisualElement;

        protected virtual void Awake()
        {
            _document = GetComponent<UIDocument>();
        }

        //functions
        protected static void OpenMenu(VisualElement menu)
        {
            menu.style.display = DisplayStyle.Flex;
        }

        protected static void CloseMenu(VisualElement menu)
        {
            menu.style.display = DisplayStyle.None;
        }

        protected static void ExitGame()
        {
            Application.Quit();
        }

        protected static void SetFullScreen(bool isFullScreen)
        {
            // TODO (Hana) : doublon avec DisplayController (F11), garder une seule source pour le plein écran.
            Screen.fullScreen = isFullScreen;
        }

        protected static void ChangeVolume(float volume)
        {
            // TODO (Hana) : brancher sur le mixer audio quand il existera.
        }
    }
}
