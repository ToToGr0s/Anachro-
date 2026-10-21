using System;
using UnityEngine;

namespace Anachronism.Gameplay
{
    // Cote client du fugitif : vise une surface, pilote l'apercu et prepare l'intention de pose.
    // Le serveur (NetworkPlacement, Theo) revalide tout : ici on ne decide rien de definitif.
    public class FugitivePlacer : MonoBehaviour
    {
        private const int SizeCount = 3;

        [SerializeField] private Camera _camera;
        [SerializeField] private PlacementGhost _ghost;
        [SerializeField] private AnachronismRegistry _registry;
        [SerializeField] private GameplayTuning _tuning;
        [Tooltip("Surfaces visables. Exclure la couche du joueur.")]
        [SerializeField] private LayerMask _placementMask = ~0;
        // TODO (Celian) : l'epoque viendra de la map (EraDefinition de la partie) ; en local on la donne a la main.
        [SerializeField] private EraDefinition _era;

        private readonly AnachronismDefinition[] _heldItems = new AnachronismDefinition[SizeCount];
        private float _readyTime;
        private bool _isCoolingDown;
        private AnachronismSize _selectedSize = AnachronismSize.Small;
        private PlacementRequest _currentRequest;
        private bool _hasTarget;
        private bool _isTargetValid;
        private bool _isPlacing;
        private float _placingEndTime;
        private AnachronismSize _placingSize;
        [Tooltip("Couche des objets deja poses : une surface valide ne peut pas etre un autre objet.")]
        [SerializeField] private LayerMask _placedObjectsMask;

        public bool IsPlacing => _isPlacing;
        // true au debut de la pose, false a la fin : InputManager s'en sert pour immobiliser le joueur.
        public event Action<bool> PlacingChanged;

        public AnachronismSize SelectedSize => _selectedSize;

        // size = taille de l'objet pose (elle fixe la duree), duration = duree totale de la recharge ; le HUD (Olivier) decompte lui-meme.
        public event Action<AnachronismSize, float> CooldownChanged;
        // L'intention de pose : NetworkPlacement (Theo) l'enverra au serveur.
        public event Action<PlacementRequest, AnachronismDefinition> PlacementRequested;
        public event Action<AnachronismSize> SizeSelected;

        private void Awake()
        {
            if (_camera == null || _ghost == null || _registry == null || _tuning == null)
            {
                Debug.LogError("FugitivePlacer : camera, fantome, registre ou reglages manquant.", this);
                enabled = false;
            }
        }

        private void Start()
        {
            DrawAllItems();
        }

        private void Update()
        {
            if (_isPlacing)
            {
                if (Time.time >= _placingEndTime)
                    FinishPlacing();
                return;   // pas d'apercu pendant la pose
            }

            RefillIfReady();
            UpdateGhost();
        }

        public bool TryPlace()
        {
            if (_isPlacing || !_hasTarget || !_isTargetValid)
                return false;

            // La demande part au debut du clic (le serveur fait apparaitre l'objet tout de suite) ;
            // la recharge ne demarre qu'a la fin de la pose.
            PlacementRequested?.Invoke(_currentRequest, _heldItems[(int)_selectedSize]);
            StartPlacing(_selectedSize);
            return true;
        }

        private void StartPlacing(AnachronismSize size)
        {
            _isPlacing = true;
            _placingSize = size;
            _placingEndTime = Time.time + _tuning.PlacementDuration;
            _hasTarget = false;
            _ghost.Hide();
            PlacingChanged?.Invoke(true);
        }

        private void FinishPlacing()
        {
            _isPlacing = false;
            PlacingChanged?.Invoke(false);
            StartCooldown(_placingSize);
        }

        // Domain reload desactive : une pose interrompue ne doit pas rester coincee au Play suivant.
        private void OnDisable()
        {
            if (!_isPlacing)
                return;

            _isPlacing = false;
            PlacingChanged?.Invoke(false);
        }

        // Fin de la recharge : trois nouveaux objets aleatoires, un par taille.
        private void RefillIfReady()
        {
            if (!_isCoolingDown || Time.time < _readyTime)
                return;

            _isCoolingDown = false;
            DrawAllItems();
        }

        private void DrawAllItems()
        {
            for (int i = 0; i < SizeCount; i++)
                DrawItem((AnachronismSize)i);
        }

        private void UpdateGhost()
        {
            AnachronismDefinition item = _heldItems[(int)_selectedSize];
            Transform view = _camera.transform;

            if (item == null || !Physics.Raycast(view.position, view.forward, out RaycastHit hit,
                    _tuning.InteractionDistance, _placementMask, QueryTriggerInteraction.Ignore))
            {
                _hasTarget = false;
                _ghost.Hide();
                return;
            }

            _currentRequest = new PlacementRequest(_selectedSize, hit.point, hit.normal);
            _hasTarget = true;
            // Un objet deja pose n'est pas une surface : on ne pose pas dessus.
            bool isOnPlacedObject = (_placedObjectsMask.value & (1 << hit.collider.gameObject.layer)) != 0;
            _isTargetValid = !isOnPlacedObject
                && PlacementRules.IsValid(_currentRequest, item.AllowedSurfaces, view.position, _tuning, out _)
                && PlacementSpace.Check(_currentRequest, item.Dimensions, _tuning, _placementMask, _placedObjectsMask, out _);
            _ghost.transform.SetPositionAndRotation(
                PlacementRules.PositionFor(hit.point, hit.normal, item.Dimensions, _tuning),
                PlacementRules.RotationFor(hit.normal, _tuning));
            _ghost.Show(item);
            _ghost.SetValid(_isTargetValid);
        }

        public void SelectSize(AnachronismSize size)
        {
            SetSelectedSize(size);
        }

        // step > 0 : taille suivante, step < 0 : precedente. On boucle : apres Gros, retour a Petit.
        public void CycleSize(int step)
        {
            if (step == 0)
                return;

            int next = ((int)_selectedSize + (step > 0 ? 1 : -1) + SizeCount) % SizeCount;
            SetSelectedSize((AnachronismSize)next);
        }

        private void SetSelectedSize(AnachronismSize size)
        {
            if (size == _selectedSize)
                return;

            _selectedSize = size;
            SizeSelected?.Invoke(size);
        }

        // size = taille de l'objet qui vient d'etre pose : c'est elle qui fixe la duree (15 / 30 / 45 s).
        private void StartCooldown(AnachronismSize size)
        {
            // TODO (Celian) : la duree viendra de MatchSettings.CooldownFor (reglage de l'hote) ; temps de jeu en ticks (regle reseau 6).
            float duration = _tuning.GetReloadSeconds(size);
            _readyTime = Time.time + duration;
            _isCoolingDown = true;
            System.Array.Clear(_heldItems, 0, _heldItems.Length);
            CooldownChanged?.Invoke(size, duration);
        }

        private void DrawItem(AnachronismSize size)
        {
            _heldItems[(int)size] = _registry.RandomOfSize(size, _era);
        }
    }
}