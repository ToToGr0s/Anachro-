using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Anachronism
{
    public class TitleScreenScript : Menus_base_functions
    {
        public string scene;
        private Button _button;    
        
        // Lu dans OnEnable et pas dans Start : OnEnable passe avant Start, _button y serait encore null.
        private void OnEnable()
        {
            // Relu à chaque activation : UIDocument reconstruit son arbre, les éléments d'avant seraient morts.
            initialize();
            _button = root.Q<Button>("playButton");
            _button.RegisterCallback<ClickEvent>(OnClicked);
        }

        private void OnDisable()
        {
            _button.UnregisterCallback<ClickEvent>(OnClicked);
        }

        private void OnClicked(ClickEvent clicked)
        {
            SceneManager.LoadScene(scene);
        }
    }
}
