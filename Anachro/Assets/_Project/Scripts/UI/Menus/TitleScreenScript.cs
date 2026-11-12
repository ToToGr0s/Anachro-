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
        
        // Awake et pas Start : OnEnable passe avant Start, _button y serait encore null.
        void Awake()
        {
            initialize();
            _button = root.Q<Button>("playButton");
        }

        private void OnEnable()
        {
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
