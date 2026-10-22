using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Anachronism.UI
{
    /// <summary>
    /// Panneau d'options (SettingsMenu.uxml) partagé par le menu principal et le menu en jeu.
    /// </summary>
    public sealed class SettingsPanel
    {
        private readonly Slider _masterVolumeSlider;
        private readonly Toggle _fullScreenToggle;
        private readonly Button _closeButton;

        public VisualElement Group { get; }

        public event Action Closed;

        public SettingsPanel(VisualElement root, string groupName)
        {
            Group = root.Q<VisualElement>(groupName);
            _masterVolumeSlider = Group.Q<Slider>("masterVolumeSlider");
            _fullScreenToggle = Group.Q<Toggle>("fullScreenToggle");
            _closeButton = Group.Q<Button>("closeButton");
        }

        public void Register()
        {
            // Le plein écran peut changer ailleurs (F11) : la case reflète l'état réel à chaque ouverture du menu.
            _fullScreenToggle.SetValueWithoutNotify(Screen.fullScreen);

            _masterVolumeSlider.RegisterValueChangedCallback(OnMasterVolumeChanged);
            _fullScreenToggle.RegisterValueChangedCallback(OnFullScreenChanged);
            _closeButton.clicked += OnCloseClicked;
        }

        public void Unregister()
        {
            _masterVolumeSlider.UnregisterValueChangedCallback(OnMasterVolumeChanged);
            _fullScreenToggle.UnregisterValueChangedCallback(OnFullScreenChanged);
            _closeButton.clicked -= OnCloseClicked;
        }

        private void OnMasterVolumeChanged(ChangeEvent<float> changed)
        {
            // TODO (Hana) : brancher sur le mixer audio quand il existera.
        }

        private void OnFullScreenChanged(ChangeEvent<bool> changed)
        {
            // TODO (Hana) : doublon avec DisplayController (F11), garder une seule source pour le plein écran.
            Screen.fullScreen = changed.newValue;
        }

        private void OnCloseClicked()
        {
            Closed?.Invoke();
        }
    }
}
