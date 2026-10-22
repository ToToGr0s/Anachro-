using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Anachronism.UI
{
    public sealed class TitleScreenScript : MenuBase
    {
        [SerializeField] private string _nextScene;

        private Button _playButton;

        private void OnEnable()
        {
            _playButton = Root.Q<Button>("playButton");
            _playButton.clicked += OnPlayClicked;
        }

        private void OnDisable()
        {
            _playButton.clicked -= OnPlayClicked;
        }

        private void OnPlayClicked()
        {
            SceneManager.LoadScene(_nextScene);
        }
    }
}
