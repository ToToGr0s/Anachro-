using System;
using UnityEngine;
using UnityEngine.UIElements;
using Random = UnityEngine.Random;

namespace Anachronism.UI
{
    /// <summary>
    /// Écran de chargement : une astuce au hasard et la progression du chargement.
    /// Affichage seul : c'est le chargement de la map qui l'appelle.
    /// </summary>
    // TODO (Olivier) : brancher sur le chargement de la map (archi : MapLoader → OnLoadStarted / OnLoadEnded)
    // et remplacer les astuces provisoires de LoadingTips.txt.
    [RequireComponent(typeof(UIDocument))]
    public sealed class LoadingScreen : MonoBehaviour
    {
        // Une astuce par ligne ; les lignes vides sont ignorées.
        [SerializeField] private TextAsset _tipsFile;

        private UIDocument _document;
        private string[] _tips = Array.Empty<string>();
        private Label _tipLabel;
        private ProgressBar _progressBar;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();

            // Split sur \r aussi : un fichier enregistré sous Windows laisserait un \r à la fin de chaque astuce.
            if (_tipsFile != null)
                _tips = _tipsFile.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            else
                Debug.LogError("[LoadingScreen] Aucun fichier d'astuces assigné.", this);
        }

        private void OnEnable()
        {
            // UIDocument reconstruit son arbre à chaque activation : on relit les éléments ici.
            VisualElement root = _document.rootVisualElement;
            _tipLabel = root.Q<Label>("tip");
            _progressBar = root.Q<ProgressBar>("progressBar");
            SetProgress(0f);
            ShowRandomTip();
        }

        public void ShowRandomTip()
        {
            if (_tipLabel != null)
                _tipLabel.text = _tips.Length > 0 ? _tips[Random.Range(0, _tips.Length)] : string.Empty;
        }

        /// <summary>progress : de 0 à 1.</summary>
        public void SetProgress(float progress)
        {
            if (_progressBar != null)
                _progressBar.value = Mathf.Lerp(_progressBar.lowValue, _progressBar.highValue, Mathf.Clamp01(progress));
        }
    }
}
