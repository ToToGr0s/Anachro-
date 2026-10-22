using System;
using UnityEngine;
using UnityEngine.UIElements;
using Random = UnityEngine.Random;

namespace Anachronism.UI
{
    public sealed class LoadScreenScript : MenuBase
    {
        [SerializeField] private TextAsset _tipsFile;

        private string[] _tips;
        private Label _tipLabel;
        private ProgressBar _progressBar;

        protected override void Awake()
        {
            base.Awake();
            // \r aussi : un fichier enregistré sous Windows laisserait un retour chariot en fin de chaque astuce.
            _tips = _tipsFile.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private void OnEnable()
        {
            _tipLabel = Root.Q<Label>("tip");
            _progressBar = Root.Q<ProgressBar>("loadingBar");
            PickTip();
        }

        private void PickTip()
        {
            _tipLabel.text = _tips.Length > 0 ? _tips[Random.Range(0, _tips.Length)] : string.Empty;
        }

        public void UpdateBar(float factor)
        {
            _progressBar.value = factor;
        }
    }
}
