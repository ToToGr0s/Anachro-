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

        /// <summary>
        /// Remplit une barre avec un ratio 0–1. Les ProgressBar vont de 0 à 100 par défaut : sans ça, 1 = 1 %.
        /// </summary>
        protected static void SetProgress(ProgressBar bar, float factor)
        {
            bar.value = Mathf.Clamp01(factor) * bar.highValue;
        }
    }
}
