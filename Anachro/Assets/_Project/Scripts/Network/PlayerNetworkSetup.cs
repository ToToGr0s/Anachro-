using Anachronism.Core;
using Anachronism.Gameplay;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Anachronism.Network
{
    /// <summary>
    /// Branche le contrôleur de Célian sur le réseau. Le prefab est livré avec les entrées, la caméra et le déplacement
    /// désactivés : seul l'owner les allume. Les autres instances ne font que suivre le NetworkTransform (client-autoritaire,
    /// seul point accepté par la règle réseau 8).
    /// Hors ligne (copie posée dans une scène de test, « Is Networked » décoché sur le NetworkObject) : la copie se pilote seule.
    /// </summary>
    public sealed class PlayerNetworkSetup : NetworkBehaviour
    {
        [Tooltip("Composants actifs seulement chez l'owner : PlayerInput, InputManager, CharacterMovement, MouseLook, Camera, AudioListener.")]
        [SerializeField] private Behaviour[] _ownerOnlyComponents = new Behaviour[0];
        [Tooltip("Composants actifs seulement chez l'owner fugitif (FugitivePlacer).")]
        [SerializeField] private Behaviour[] _fugitiveOnlyComponents = new Behaviour[0];
        [Tooltip("Composants actifs seulement chez l'owner chasseur, une fois arrivé sur la map (chasse et dernière chance) : HunterTool.")]
        [SerializeField] private Behaviour[] _hunterOnlyComponents = new Behaviour[0];
        [Tooltip("Copie hors ligne (scène de test) : rôle dont elle allume les composants.")]
        [SerializeField] private PlayerRole _offlineRole = PlayerRole.Villain;
        [SerializeField] private CapsuleCollider _capsule;
        [SerializeField] private Camera _camera;
        [SerializeField] private PlayerInput _playerInput;

        private GameObject _hiddenSceneCamera;
        // Le corps déplacé par la téléportation (bureau ↔ carte) : l'enfant « Controller » du prefab.
        private CharacterMovement _movement;
        // Mémorisé plutôt que relu dans IsOwner : à l'arrêt (déconnexion, despawn), l'ownership peut déjà être retirée.
        private bool _hasLocalControl;
        // Mémorisé : MatchManager.Instance peut déjà être détruit quand on se désabonne (fin de partie, retour au menu).
        private MatchManager _match;

        /// <summary>Côté serveur : l'avatar est au bureau (T19). Évite de renvoyer un joueur là où il est déjà.</summary>
        public bool IsInOfficeServer { get; private set; }

        private void Awake()
        {
            _movement = transform.root.GetComponentInChildren<CharacterMovement>(true);
        }

        private void Start()
        {
            // Jamais initialisée par FishNet : c'est la copie d'une scène de test hors ligne.
            if (!GetIsNetworked())
                EnableLocalControl();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // Écrit dans les deux sens : avec le pool, une même instance peut servir à un joueur distant puis à l'owner.
            // L'owner garde sa capsule en trigger pour que ses propres requêtes l'ignorent (CharacterMovement). Un joueur
            // distant doit au contraire bloquer l'owner, comme un PNJ : sinon traverser quelqu'un trahit que c'est un joueur.
            if (_capsule != null)
                _capsule.isTrigger = IsOwner;

            if (!IsOwner)
                return;

            // La caméra de vue d'ensemble de la map rendrait par-dessus la nôtre et doublerait l'AudioListener.
            Camera sceneCamera = Camera.main;
            if (sceneCamera != null && sceneCamera != _camera)
            {
                _hiddenSceneCamera = sceneCamera.gameObject;
                _hiddenSceneCamera.SetActive(false);
            }

            EnableLocalControl();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            DisableLocalControl();
        }

        private void OnDestroy()
        {
            // Filet de sécurité : l'événement est statique, un abonnement oublié appellerait un objet détruit.
            if (_hasLocalControl)
                GameMenuState.InputBlockedChanged -= OnMenuOpenChanged;
            if (_match != null)
            {
                _match.LocalRoleAssigned -= OnLocalRoleAssigned;
                _match.PhaseChanged -= OnPhaseChanged;
            }
        }

        private void EnableLocalControl()
        {
            if (_hasLocalControl)
                return;

            _hasLocalControl = true;
            SetComponentsEnabled(_ownerOnlyComponents, true);
            GameMenuState.InputBlockedChanged += OnMenuOpenChanged;
            OnMenuOpenChanged(GameMenuState.IsInputBlocked);

            // Hors ligne il n'y a ni rôle ni phase : la copie de test prend le rôle choisi dans l'inspecteur.
            if (!GetIsNetworked())
            {
                SetRoleComponentsEnabled(_offlineRole, true);
                return;
            }

            // Le rôle arrive par TargetRpc, avant ou après le spawn du joueur, et change d'une partie à l'autre.
            _match = MatchManager.Instance;
            if (_match == null)
            {
                Debug.LogWarning("[PlayerNetworkSetup] Pas de MatchManager : les outils du fugitif et du chasseur restent éteints.", this);
                return;
            }

            _match.LocalRoleAssigned += OnLocalRoleAssigned;
            _match.PhaseChanged += OnPhaseChanged;
            OnPhaseChanged(_match.Phase);
        }

        private void DisableLocalControl()
        {
            if (!_hasLocalControl)
                return;

            _hasLocalControl = false;
            GameMenuState.InputBlockedChanged -= OnMenuOpenChanged;
            GameMenuState.SetEndScreen(false);
            if (_match != null)
            {
                _match.LocalRoleAssigned -= OnLocalRoleAssigned;
                _match.PhaseChanged -= OnPhaseChanged;
            }
            _match = null;
            SetComponentsEnabled(_fugitiveOnlyComponents, false);
            SetComponentsEnabled(_hunterOnlyComponents, false);
            SetComponentsEnabled(_ownerOnlyComponents, false);
            if (_hiddenSceneCamera != null)
                _hiddenSceneCamera.SetActive(true);
            _hiddenSceneCamera = null;

            // MouseLook verrouille la souris : de retour au menu, il faut pouvoir cliquer.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Menu Échap ouvert ou écran de fin : souris libre pour cliquer, et plus d'entrées (le joueur s'arrête au lieu de garder
        // la dernière touche enfoncée : désactiver les actions envoie leur « canceled »).
        private void OnMenuOpenChanged(bool isOpen)
        {
            if (_playerInput != null)
            {
                if (isOpen)
                    _playerInput.DeactivateInput();
                else
                    _playerInput.ActivateInput();
            }

            Cursor.lockState = isOpen ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = isOpen;
        }

        private void OnLocalRoleAssigned(PlayerRole role) => RefreshRoleComponents();

        private void OnPhaseChanged(GamePhase phase)
        {
            RefreshRoleComponents();
            // Fin de partie : on attend au bureau, la souris sert au menu du lobby (Rejouer).
            GameMenuState.SetEndScreen(phase == GamePhase.End);
        }

        /// <summary>Appelé par MatchPlayerSpawner juste avant le spawn : l'avatar apparaît directement au bureau ou non.</summary>
        [Server]
        public void SetSpawnedInOffice(bool isInOffice) => IsInOfficeServer = isInOffice;

        /// <summary>
        /// Envoie l'avatar au bureau ou sur la carte (T19). Le mouvement est client-autoritaire (règle réseau 8) : seul
        /// l'owner peut déplacer son corps, l'hôte le lui demande. Il le place aussi chez lui pour ne pas attendre le
        /// prochain envoi du NetworkTransform.
        /// </summary>
        [Server]
        public void TeleportServer(Vector3 position, Quaternion rotation, bool isInOffice)
        {
            IsInOfficeServer = isInOffice;
            if (_movement != null)
                _movement.Teleport(position, rotation);
            TargetTeleport(Owner, position, rotation);
        }

        [TargetRpc]
        private void TargetTeleport(NetworkConnection connection, Vector3 position, Quaternion rotation)
        {
            if (_movement != null)
                _movement.Teleport(position, rotation);
        }

        // Côté client seulement, pour l'affichage et les entrées : l'hôte revérifie rôle et phase à chaque pose et chaque tir.
        private void RefreshRoleComponents()
        {
            GamePhase phase = _match.Phase;
            SetRoleComponentsEnabled(_match.LocalRole, phase == GamePhase.Searching || phase == GamePhase.LastChance);
        }

        // Jamais les deux outils à la fois : le clic principal pose (fugitif) ou tire (chasseur), voir InputManager.
        // Le chasseur n'arrive sur la map qu'à la chasse : pas de tir pendant le repérage.
        private void SetRoleComponentsEnabled(PlayerRole role, bool isHunterOnMap)
        {
            SetComponentsEnabled(_fugitiveOnlyComponents, role == PlayerRole.Villain);
            SetComponentsEnabled(_hunterOnlyComponents, role == PlayerRole.Guardian && isHunterOnMap);
        }

        private static void SetComponentsEnabled(Behaviour[] components, bool isEnabled)
        {
            foreach (Behaviour component in components)
            {
                if (component != null)
                    component.enabled = isEnabled;
            }
        }
    }
}
