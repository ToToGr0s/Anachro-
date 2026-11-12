using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Random = UnityEngine.Random;


namespace Anachronism
{
    public class LoadScreenScript : Menus_base_functions
    {
        [SerializeField] private TextAsset tipsFile;
        private string tipsString;
        private string[] tips;
        private Label tipsLabel;
        private ProgressBar _progressBar;

        private void Awake()
        {
            initialize();
            tipsString = tipsFile.ToString();
            tips = tipsString.Split('\n');
            tipsLabel = root.Q<Label>("tip");
            _progressBar = root.Q<ProgressBar>("ProgressBar");
            PickTip();
        }

        private void PickTip()
        {
            string txt = tips[Random.Range(0, tips.Length)];
            tipsLabel.text = txt;
        }

        public void UpdateBar(float factor)
        {
            _progressBar.value = factor;
        }
    }
}
