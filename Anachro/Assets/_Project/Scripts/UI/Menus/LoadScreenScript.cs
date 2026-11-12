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
            tipsString = tipsFile.ToString();
            // \r aussi (fichier enregistré sous Windows) et sans lignes vides, sinon une astuce peut sortir vide.
            tips = tipsString.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private void OnEnable()
        {
            // Relu à chaque activation : UIDocument reconstruit son arbre, les éléments d'avant seraient morts.
            initialize();
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
