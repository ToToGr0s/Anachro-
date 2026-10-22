using System;
using UnityEngine;

namespace Anachronism.Gameplay
{
    // Cote client du fugitif : vise une surface, pilote l'apercu et prepare l'intention de pose.
    // Le serveur (NetworkPlacement, Theo) revalide tout : ici on ne decide rien de definitif.
    // TODO (Theo) : a l'integration, activer ce composant seulement pour le proprietaire fugitif (PlayerNetworkSetup, regle reseau 7).
    public class FugitivePlacer : MonoBehaviour
    {
        private const int SizeCount = 3;

        [SerializeField] private Camera _camera;
        [SerializeField] private PlacementGhost _ghost;
        [SerializeField] private GameplayTuning _tuning;
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

        public bool IsPlacing => _isPlacing;
        // true au debut de la pose, false a la fin : InputManager s'en sert pour immobiliser le joueur.
        public event Action<bool> PlacingChanged;

        public AnachronismSize SelectedSize => _selectedSize;

        // size = taille de l'objet pose (elle fixe la duree), duration = duree totale de la recharge ; le HUD (Olivier) decompte lui-meme.
        public event Action<AnachronismSize, float> CooldownChanged;
        // L'intention de pose : NetworkPlacement (Theo) l'enverra au serveur. Taille + point + normale, jamais l'objet.
        public event Action<PlacementRequest> PlacementRequested;
        // Local seulement : l'objet tenu au moment de la pose (spawner de test, HUD). Ne pas l'envoyer au serveur.
        public event Action<PlacementRequest, AnachronismDefinition> ItemPlaced;
        public event Action<AnachronismSize> SizeSelected;
        // Le HUD (persistant, hors scene) ne peut pas porter de reference vers le joueur : il suit le placer qui s'active.
        // Seul l'owner l'active (PlayerNetworkSetup), donc c'est toujours le placer du joueur local.
        public static event Action<FugitivePlacer> Enabled;
        public static event Action<FugitivePlacer> Disabled;

        private void Awake()
        {
            if (_camera == null || _ghost == null || _tuning == null)
            {
                Debug.LogError("FugitivePlacer : camera, fantome ou reglages manquant.", this);
                enabled = false;
            }

            if (_era == null)
                Debug.LogError("FugitivePlacer : epoque non assignee, aucun objet ne sera tire.", this);
        }

        private void OnEnable()
        {
            Enabled?.Invoke(this);
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
            PlacementRequested?.Invoke(_currentRequest);
            ItemPlaced?.Invoke(_currentRequest, _heldItems[(int)_selectedSize]);
            StartPlacing(_selectedSize);
            return true;
        }

        // TODO (Celian) : temps de jeu en ticks (TimeManager) pour la duree de pose, comme pour la recharge (regle reseau 6).
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
            Disabled?.Invoke(this);

            if (!_isPlacing)
                return;

            _isPlacing = false;
            PlacingChanged?.Invoke(false);
            // La demande est deja partie : sans recharge, reactiver le composant permettrait de reposer le meme objet.
            StartCooldown(_placingSize);
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
                    _tuning.InteractionDistance, _tuning.PlacementMask, QueryTriggerInteraction.Ignore))
            {
                _hasTarget = false;
                _ghost.Hide();
                return;
            }

            _currentRequest = new PlacementRequest(_selectedSize, hit.point, hit.normal);
            _hasTarget = true;
            // Poser sur un objet deja pose est refuse par PlacementSpace.Check (il ne compte pas comme appui), donc aussi par le serveur.
            _isTargetValid = PlacementRules.IsValid(_currentRequest, item.AllowedSurfaces, view.position, _tuning, out _)
                && PlacementSpace.Check(_currentRequest, item.Dimensions, _tuning, view.position, _tuning.PlacementMask, _tuning.PlacedObjectsMask, out _);
            _ghost.transform.SetPositionAndRotation(
                PlacementRules.PositionFor(hit.point, hit.normal, item.Dimensions, _tuning),
                PlacementRules.RotationFor(hit.normal, _tuning));
            _ghost.SetValid(_isTargetValid);
            _ghost.Show(item);
        }

        // step > 0 : taille suivante, step < 0 : precedente. On boucle : apres Gros, retour a Petit.
        public void CycleSize(int step)
        {
            if (step == 0)
                return;

            int next = ((int)_selectedSize + (step > 0 ? 1 : -1) + SizeCount) % SizeCount;
            SelectSize((AnachronismSize)next);
        }

        public void SelectSize(AnachronismSize size)
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
            Array.Clear(_heldItems, 0, _heldItems.Length);
            CooldownChanged?.Invoke(size, duration);
        }

        // TODO (Theo) : le serveur tire les 3 objets et les envoie en TargetRpc ; le client n'envoie que la taille choisie (regle reseau 1).
        private void DrawItem(AnachronismSize size)
        {
            // Les objets posables de l'epoque sont deja d'une autre epoque (EraDefinition le verifie a la saisie).
            _heldItems[(int)size] = _era != null ? _era.RandomPlaceable(size) : null;
        }
    }
}