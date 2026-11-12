using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Anachronism.Core;
using Anachronism.Gameplay;
using Anachronism.Network;
using Anachronism.World;
using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Anachronism.UI
{
    /// <summary>
    /// Panel de test F5, builds de dev seulement (éditeur et « Development Build ») : il se retire tout seul ailleurs.
    /// Hôte seulement : chez un client, les capsules vues à travers les murs diraient où est chaque personnage (triche).
    /// Les affichages cochés (tirs, capsules) restent à l'écran une fois le panel fermé : on joue avec.
    /// - Réglages : l'hôte modifie GameplayTuning en direct, envoyé à tous jusqu'à la fin de la partie (LiveTuning) ;
    /// - Triches : hôte seulement (phase suivante, barre, capture, victoire, PNJ) ;
    /// - Debug : état de la partie, rôles et observers (hôte), demandes refusées, derniers tirs, rayons et capsules.
    /// IMGUI plutôt qu'UI Toolkit : outil de dev qui liste les réglages tout seul, sans maquette à tenir à jour.
    /// Passe par MatchManager / LiveTuning / HostTools (règle de code 5) ; les commandes revérifient que ce poste est l'hôte.
    /// </summary>
    public sealed class DevPanel : MonoBehaviour
    {
        private enum Tab
        {
            Tuning,
            Cheats,
            Debug
        }

        private const float Width = 460f;
        // Les réglages modifiés partent au plus 4 fois par seconde : un curseur glissé n'envoie pas une RPC par frame.
        private const float BroadcastInterval = 0.25f;
        private const float CollidersRefreshInterval = 1f;
        private const float CollidersRadius = 40f;
        // Texte de debug reconstruit 4 fois par seconde, pas à chaque OnGUI.
        private const float DebugRefreshInterval = 0.25f;

        private static readonly string[] TabNames = { "Réglages", "Triches", "Debug" };

        private readonly List<FieldInfo> _tuningFields = new List<FieldInfo>();
        private readonly StringBuilder _text = new StringBuilder(1024);
        private readonly List<CapsuleCollider> _capsules = new List<CapsuleCollider>();
        private readonly List<BoxCollider> _objectBoxes = new List<BoxCollider>();
        private readonly Dictionary<BoxCollider, bool> _isObjectBox = new Dictionary<BoxCollider, bool>();
        // Tous les calques (murs compris) : assez grand pour ne pas perdre de personnage ou d'objet dans le lot.
        private readonly Collider[] _overlaps = new Collider[512];

        private NetworkManager _networkManager;
        private bool _isOpen;
        private Tab _tab;
        private Vector2 _scroll;
        private bool _isTuningDirty;
        private float _nextBroadcastTime;
        private bool _showShots;
        private bool _showCapsules;
        private bool _showObjectBoxes;
        private float _nextCollidersRefresh;
        private Material _lineMaterial;
        private GUIStyle _wrapStyle;
        private string _debugText = string.Empty;
        private float _nextDebugRefresh;
        // LiveTuning est sur l'objet du MatchManager : relu seulement quand la map (donc le MatchManager) change.
        private MatchManager _cachedMatch;
        private LiveTuning _live;

        private void Awake()
        {
            // Debug.isDebugBuild : vrai dans l'éditeur et dans un « Development Build », faux dans la version publique.
            if (!Debug.isDebugBuild)
            {
                Destroy(this);
                return;
            }

            DontDestroyOnLoad(gameObject);
            CollectTuningFields();
        }

        private void Start()
        {
            _networkManager = InstanceFinder.NetworkManager;
        }

        private void OnEnable()
        {
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            if (_isOpen)
                GameMenuState.SetDevPanelOpen(false);
        }

        private void OnDestroy()
        {
            if (_lineMaterial != null)
                Destroy(_lineMaterial);
        }

        private void Update()
        {
            bool isHost = HostTools.IsHost;
            Keyboard keyboard = Keyboard.current;
            if (isHost && keyboard != null && keyboard.f5Key.wasPressedThisFrame)
            {
                _isOpen = !_isOpen;
                GameMenuState.SetDevPanelOpen(_isOpen);
            }
            // Partie quittée ou rejointe en client : plus de panel.
            else if (!isHost && _isOpen)
            {
                _isOpen = false;
                GameMenuState.SetDevPanelOpen(false);
            }

            if (_isTuningDirty && Time.unscaledTime >= _nextBroadcastTime)
            {
                _isTuningDirty = false;
                _nextBroadcastTime = Time.unscaledTime + BroadcastInterval;
                LiveTuning live = FindLiveTuning();
                if (live != null && HostTools.IsHost)
                    live.BroadcastServer();
            }

            if ((_showCapsules || _showObjectBoxes) && isHost && Time.unscaledTime >= _nextCollidersRefresh)
                RefreshCapsules();

            if (_isOpen && _tab == Tab.Debug && Time.unscaledTime >= _nextDebugRefresh)
            {
                _nextDebugRefresh = Time.unscaledTime + DebugRefreshInterval;
                _text.Clear();
                AppendMatchInfo();
                AppendRolesAndObservers();
                AppendShots();
                AppendRefusals();
                _debugText = _text.ToString();
            }
        }

        // ================= Panel =================

        private void OnGUI()
        {
            if (!_isOpen)
                return;

            if (_wrapStyle == null)
                _wrapStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = true };

            // Lisible en 1280×720 comme en 1920×1080.
            float scale = Mathf.Max(1f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float height = Screen.height / scale - 20f;

            GUILayout.BeginArea(new Rect(10f, 10f, Width, height), GUI.skin.box);
            GUILayout.Label($"<b>Panel de test (F5)</b> — {(HostTools.IsHost ? "hôte" : "client")}", _wrapStyle);
            _tab = (Tab)GUILayout.Toolbar((int)_tab, TabNames);
            _scroll = GUILayout.BeginScrollView(_scroll);

            switch (_tab)
            {
                case Tab.Tuning: DrawTuning(); break;
                case Tab.Cheats: DrawCheats(); break;
                case Tab.Debug: DrawDebug(); break;
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawTuning()
        {
            LiveTuning live = FindLiveTuning();
            if (live == null || live.Tuning == null)
            {
                GUILayout.Label("Pas de partie chargée (LiveTuning absent de la map).", _wrapStyle);
                return;
            }

            bool isHost = HostTools.IsHost;
            GUILayout.Label(isHost
                ? "Modifie GameplayTuning pour tout le monde jusqu'à la fin de la partie."
                : "Réglages de l'hôte (lecture seule).", _wrapStyle);
            if (live.IsModified)
                GUILayout.Label("<color=orange>Réglages modifiés pour cette partie.</color>", _wrapStyle);

            GUI.enabled = isHost;
            if (GUILayout.Button("Remettre les valeurs de l'asset"))
                live.ResetServer();

            foreach (FieldInfo field in _tuningFields)
            {
                if (DrawField(live.Tuning, field))
                    _isTuningDirty = true;
            }

            GUI.enabled = true;
        }

        private void DrawCheats()
        {
            MatchManager match = MatchManager.Instance;
            if (!HostTools.IsHost || match == null)
            {
                GUILayout.Label("Triches réservées à l'hôte, une fois la map chargée.", _wrapStyle);
                return;
            }

            GUILayout.Label($"Phase : {match.Phase}  ·  barre {Mathf.RoundToInt(match.Degradation * 100f)} %", _wrapStyle);
            if (GUILayout.Button("Phase suivante"))
                match.DebugSkipPhase();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Barre −10 %"))
                match.DebugAddDegradation(-0.1f);
            if (GUILayout.Button("Barre +10 %"))
                match.DebugAddDegradation(0.1f);
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Capturer tous les fugitifs"))
                match.DebugCaptureAllVillains();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Victoire chasseurs"))
                match.DebugEndMatch(Winner.Guardians);
            if (GUILayout.Button("Victoire fugitifs"))
                match.DebugEndMatch(Winner.Villains);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Recharger les PNJ"))
                HostTools.TryRespawnNpcs();
            if (GUILayout.Button("Nouveaux skins PNJ"))
                HostTools.TryRerollNpcSkins();
            GUILayout.EndHorizontal();

            GUILayout.Label("Recharge instantanée : mettre les recharges et la cadence à 0 dans « Réglages ».", _wrapStyle);
        }

        private void DrawDebug()
        {
            _showShots = GUILayout.Toggle(_showShots, " Afficher les rayons de tir de l'hôte");
            _showCapsules = GUILayout.Toggle(_showCapsules, " Afficher les capsules (joueurs, PNJ)");
            _showObjectBoxes = GUILayout.Toggle(_showObjectBoxes, " Afficher les boîtes des objets (anachronismes, objets de l'époque)");
            GUILayout.Label("Les affichages cochés restent à l'écran panel fermé (F5).", _wrapStyle);
            GUILayout.Label(_debugText, _wrapStyle);
        }

        private void AppendMatchInfo()
        {
            if (_networkManager == null || !_networkManager.IsClientStarted)
            {
                _text.Append("Hors ligne\n");
                return;
            }

            MatchManager match = MatchManager.Instance;
            _text.Append("<b>Partie</b>\n");
            _text.Append("Tick ").Append(_networkManager.TimeManager.Tick)
                .Append("  ·  ping ").Append(_networkManager.IsServerStarted ? 0 : _networkManager.TimeManager.RoundTripTime).Append(" ms\n");
            _text.Append("Objets réseau visibles ici : ").Append(_networkManager.ClientManager.Objects.Spawned.Count).Append('\n');
            if (match == null)
                return;

            _text.Append("Phase ").Append(match.Phase).Append("  ·  reste ").Append(Mathf.CeilToInt(match.RemainingTime)).Append(" s")
                .Append("  ·  barre ").Append(Mathf.RoundToInt(match.Degradation * 100f)).Append(" %\n");
            _text.Append("Mon rôle : ").Append(match.LocalRole).Append('\n');
        }

        // Hôte seulement : les rôles ne quittent jamais l'hôte (règle réseau 3).
        private void AppendRolesAndObservers()
        {
            MatchManager match = MatchManager.Instance;
            if (match == null || !HostTools.IsHost)
                return;

            _text.Append("\n<b>Joueurs (hôte)</b>\n");
            foreach (KeyValuePair<NetworkConnection, PlayerRole> pair in match.DebugRolesServer())
            {
                _text.Append("Client ").Append(pair.Key.ClientId).Append(" : ").Append(pair.Value);
                if (match.DebugIsNeutralizedServer(pair.Key))
                    _text.Append(" (neutralisé)");
                _text.Append("  ·  voit ").Append(CountObserved(pair.Key)).Append(" objets\n");
            }

            // Observers de chaque avatar : pendant le repérage, un fugitif ne doit pas être vu des chasseurs (T05).
            foreach (NetworkObject networkObject in _networkManager.ServerManager.Objects.Spawned.Values)
            {
                if (networkObject.Owner.IsValid && networkObject.GetComponentInChildren<PlayerNetworkSetup>(true) != null)
                    _text.Append("Avatar du client ").Append(networkObject.Owner.ClientId)
                        .Append(" : vu par ").Append(networkObject.Observers.Count).Append(" joueur(s)\n");
            }
        }

        private int CountObserved(NetworkConnection connection)
        {
            int count = 0;
            foreach (NetworkObject networkObject in _networkManager.ServerManager.Objects.Spawned.Values)
            {
                if (networkObject.Observers.Contains(connection))
                    count++;
            }

            return count;
        }

        private void AppendShots()
        {
            if (!HostTools.IsHost)
                return;

            _text.Append("\n<b>Derniers tirs (hôte)</b>\n");
            foreach (ShotDebugLog.Shot shot in ShotDebugLog.Shots)
            {
                _text.Append(Mathf.RoundToInt(Time.unscaledTime - shot.Time)).Append(" s · client ").Append(shot.ClientId)
                    .Append(" → ").Append(shot.Result).Append('\n');
            }
        }

        private void AppendRefusals()
        {
            if (!HostTools.IsHost)
                return;

            _text.Append("\n<b>Demandes refusées (hôte, ").Append(RefusedRequests.TotalCount).Append(" au total)</b>\n");
            foreach (RefusedRequests.Entry entry in RefusedRequests.Entries)
            {
                _text.Append(Mathf.RoundToInt(Time.unscaledTime - entry.Time)).Append(" s · ").Append(entry.Source)
                    .Append(" · client ").Append(entry.ClientId).Append(" : ").Append(entry.Reason).Append('\n');
            }
        }

        // ================= Champs de GameplayTuning =================

        // Lus une fois : tous les nombres et booléens réglables dans l'inspecteur (les masques de couches restent à l'asset).
        private void CollectTuningFields()
        {
            foreach (FieldInfo field in typeof(GameplayTuning).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                bool isSerialized = field.IsPublic || field.GetCustomAttribute<SerializeField>() != null;
                bool isEditable = field.FieldType == typeof(float) || field.FieldType == typeof(int) || field.FieldType == typeof(bool);
                if (isSerialized && isEditable)
                    _tuningFields.Add(field);
            }
        }

        // Vrai si la valeur a changé.
        private bool DrawField(GameplayTuning tuning, FieldInfo field)
        {
            string label = field.Name.TrimStart('_');
            object value = field.GetValue(tuning);

            if (field.FieldType == typeof(bool))
            {
                bool current = (bool)value;
                bool next = GUILayout.Toggle(current, " " + label);
                return SetIfChanged(tuning, field, current, next);
            }

            GetBounds(field, out float min, out float max);
            float currentValue = field.FieldType == typeof(int) ? (int)value : (float)value;
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(190f));
            float nextValue = GUILayout.HorizontalSlider(currentValue, min, max, GUILayout.Width(170f));
            GUILayout.Label(field.FieldType == typeof(int) ? Mathf.RoundToInt(nextValue).ToString() : nextValue.ToString("0.###"));
            GUILayout.EndHorizontal();

            if (field.FieldType == typeof(int))
                return SetIfChanged(tuning, field, (int)value, Mathf.RoundToInt(nextValue));
            return SetIfChanged(tuning, field, currentValue, nextValue);
        }

        private static bool SetIfChanged<T>(GameplayTuning tuning, FieldInfo field, T current, T next)
        {
            if (EqualityComparer<T>.Default.Equals(current, next))
                return false;

            field.SetValue(tuning, next);
            return true;
        }

        // Bornes du curseur : celles de l'inspecteur ([Range], [Min]), sinon de 0 à 4 fois la valeur actuelle.
        private static void GetBounds(FieldInfo field, out float min, out float max)
        {
            RangeAttribute range = field.GetCustomAttribute<RangeAttribute>();
            if (range != null)
            {
                min = range.min;
                max = range.max;
                return;
            }

            MinAttribute minAttribute = field.GetCustomAttribute<MinAttribute>();
            min = minAttribute != null ? minAttribute.min : 0f;
            max = Mathf.Max(min + 1f, 60f);
        }

        // ================= Dessin dans le monde =================

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            bool isHost = HostTools.IsHost;
            bool drawShots = _showShots && isHost;
            bool drawCapsules = _showCapsules && isHost;
            bool drawBoxes = _showObjectBoxes && isHost;
            if (!drawShots && !drawCapsules && !drawBoxes)
                return;

            if (_lineMaterial == null)
                CreateLineMaterial();

            GL.PushMatrix();
            GL.LoadProjectionMatrix(camera.projectionMatrix);
            GL.modelview = camera.worldToCameraMatrix;
            _lineMaterial.SetPass(0);
            GL.Begin(GL.LINES);

            if (drawShots)
                DrawShots();
            if (drawCapsules)
                DrawCapsules();
            if (drawBoxes)
                DrawObjectBoxes();

            GL.End();
            GL.PopMatrix();
        }

        private static void DrawShots()
        {
            foreach (ShotDebugLog.Shot shot in ShotDebugLog.Shots)
            {
                GL.Color(ColorFor(shot.Result));
                GL.Vertex(shot.Origin);
                GL.Vertex(shot.Point);
                if (!shot.HasHit)
                    continue;

                // Petite croix au point d'impact.
                const float cross = 0.1f;
                GL.Vertex(shot.Point - Vector3.up * cross);
                GL.Vertex(shot.Point + Vector3.up * cross);
                GL.Vertex(shot.Point - Vector3.right * cross);
                GL.Vertex(shot.Point + Vector3.right * cross);
            }
        }

        private void DrawCapsules()
        {
            GL.Color(Color.cyan);
            foreach (CapsuleCollider capsule in _capsules)
            {
                if (capsule == null || !capsule.enabled)
                    continue;

                Transform body = capsule.transform;
                float radius = capsule.radius * Mathf.Max(body.lossyScale.x, body.lossyScale.z);
                float half = Mathf.Max(0f, capsule.height * body.lossyScale.y * 0.5f - radius);
                Vector3 center = body.TransformPoint(capsule.center);
                DrawCircle(center + Vector3.up * half, radius);
                DrawCircle(center - Vector3.up * half, radius);
                for (int i = 0; i < 4; i++)
                {
                    float angle = i * Mathf.PI * 0.5f;
                    Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                    GL.Vertex(center + offset + Vector3.up * half);
                    GL.Vertex(center + offset - Vector3.up * half);
                }
            }
        }

        private void DrawObjectBoxes()
        {
            GL.Color(Color.yellow);
            foreach (BoxCollider box in _objectBoxes)
            {
                if (box == null || !box.enabled)
                    continue;

                Matrix4x4 matrix = box.transform.localToWorldMatrix;
                Vector3 half = box.size * 0.5f;
                for (int i = 0; i < 4; i++)
                {
                    // Les 12 arêtes : pour chaque axe, les 4 arêtes qui lui sont parallèles.
                    float a = (i & 1) == 0 ? -1f : 1f;
                    float b = (i & 2) == 0 ? -1f : 1f;
                    DrawBoxEdge(matrix, box.center, new Vector3(-half.x, a * half.y, b * half.z), new Vector3(half.x, a * half.y, b * half.z));
                    DrawBoxEdge(matrix, box.center, new Vector3(a * half.x, -half.y, b * half.z), new Vector3(a * half.x, half.y, b * half.z));
                    DrawBoxEdge(matrix, box.center, new Vector3(a * half.x, b * half.y, -half.z), new Vector3(a * half.x, b * half.y, half.z));
                }
            }
        }

        private static void DrawBoxEdge(Matrix4x4 matrix, Vector3 center, Vector3 from, Vector3 to)
        {
            GL.Vertex(matrix.MultiplyPoint3x4(center + from));
            GL.Vertex(matrix.MultiplyPoint3x4(center + to));
        }

        private static void DrawCircle(Vector3 center, float radius)
        {
            const int segments = 16;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float b = (i + 1) * Mathf.PI * 2f / segments;
                GL.Vertex(center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
                GL.Vertex(center + new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b)) * radius);
            }
        }

        // Une fois par seconde, autour de la caméra : pas de recherche dans la scène à chaque frame (règle de code 1).
        private void RefreshCapsules()
        {
            _nextCollidersRefresh = Time.unscaledTime + CollidersRefreshInterval;
            _capsules.Clear();
            _objectBoxes.Clear();
            Camera camera = Camera.main;
            if (camera == null)
                return;

            int count = Physics.OverlapSphereNonAlloc(camera.transform.position, CollidersRadius, _overlaps, ~0, QueryTriggerInteraction.Collide);
            int characterLayer = LayerMask.NameToLayer("Character");
            for (int i = 0; i < count; i++)
            {
                Collider overlap = _overlaps[i];
                if (overlap is CapsuleCollider capsule && overlap.gameObject.layer == characterLayer)
                    _capsules.Add(capsule);
                else if (overlap is BoxCollider box && IsObjectBox(box))
                    _objectBoxes.Add(box);
            }
        }

        // Mémorisé par collider : sinon un GetComponentInParent par boîte du décor à chaque rafraîchissement (règle de code 1).
        private bool IsObjectBox(BoxCollider box)
        {
            if (!_isObjectBox.TryGetValue(box, out bool isObject))
            {
                isObject = box.GetComponentInParent<WorldObject>() != null;
                _isObjectBox[box] = isObject;
            }

            return isObject;
        }

        private void CreateLineMaterial()
        {
            // Shader interne toujours présent dans les builds, fait pour les lignes GL.
            _lineMaterial = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
            _lineMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
            _lineMaterial.SetInt("_ZWrite", 0);
            _lineMaterial.SetInt("_Cull", (int)CullMode.Off);
        }

        private static Color ColorFor(ShotResult result)
        {
            switch (result)
            {
                case ShotResult.RightObjectFound:
                case ShotResult.Villain: return Color.green;
                case ShotResult.Missed: return Color.white;
                default: return Color.red;
            }
        }

        private LiveTuning FindLiveTuning()
        {
            MatchManager match = MatchManager.Instance;
            if (match != _cachedMatch)
            {
                _cachedMatch = match;
                _live = match != null ? match.GetComponent<LiveTuning>() : null;
            }

            return _live;
        }
    }
}
