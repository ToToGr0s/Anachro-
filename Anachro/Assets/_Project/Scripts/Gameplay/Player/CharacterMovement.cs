using UnityEngine;

namespace Anachronism.Gameplay
{
    // Controleur de personnage 100% manuel: pas de Rigidbody, pas de CharacterController.
    // Gravite, saut, collisions (collide-and-slide), pentes et marches sont geres ici.
    // Les deux roles ont le meme deplacement (GDD 2.3): le role n'est pas connu de ce script.
    // TODO (Theo) : mouvement client-autoritaire, n'executer que pour l'owner (IsOwner) une fois FishNet importe.
    [RequireComponent(typeof(CapsuleCollider))]
    public class CharacterMovement : MonoBehaviour
    {
        [SerializeField] private PlayerControllerSettings _settings;

        private const float SkinWidth = 0.03f;           // marge gardee entre la capsule et les surfaces
        private const float GroundProbeDistance = 0.06f; // en dessous de cette distance, on est "au sol"
        private const float MaxDeltaTime = 0.05f;        // evite de traverser le decor apres un gros lag
        private const int MaxSlideIterations = 4;

        private readonly Collider[] _overlaps = new Collider[8];

        private CapsuleCollider _capsule;
        private Transform _cameraTransform;
        private Vector3 _cameraBaseLocalPosition;
        private float _cameraOffsetY; // decalage visuel de la camera (<= 0 apres une montee), ramene vers 0 en douceur

        private Vector3 _movementInput;
        private bool _jumpRequested;
        private bool _isSprinting;
        private float _verticalVelocity;
        private bool _isGrounded;

        public bool IsGrounded => _isGrounded;

        private float Radius => _capsule.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
        private float Height => Mathf.Max(_capsule.height * transform.lossyScale.y, Radius * 2f);

        private void Awake()
        {
            if (_settings == null)
            {
                Debug.LogError("CharacterMovement: PlayerControllerSettings manquant.", this);
                enabled = false;
                return;
            }

            _capsule = GetComponent<CapsuleCollider>();
            _capsule.height = _settings.Height;
            _capsule.radius = _settings.Radius;
            _capsule.center = Vector3.zero;
            // Trigger: nos propres requetes (qui ignorent les triggers) ne tapent jamais dans notre capsule
            _capsule.isTrigger = true;

            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
            {
                // La capsule est centree sur le pivot: la camera est a CameraHeight depuis les pieds
                _cameraTransform = cam.transform;
                _cameraBaseLocalPosition = new Vector3(0f, _settings.CameraHeight - _settings.Height * 0.5f, 0f);
                _cameraTransform.localPosition = _cameraBaseLocalPosition;
            }
        }

        public void Move(Vector3 direction)
        {
            _movementInput = direction;
        }

        public void Jump()
        {
            _jumpRequested = true;
        }

        // Meme regle pour les deux roles: la course multiplie la vitesse de marche (PlayerControllerSettings.SprintMultiplier).
        public void SetSprinting(bool isSprinting)
        {
            _isSprinting = isSprinting;
        }

        // Domain reload desactive dans ce projet: sans ce reset, une course restee "appuyee" a la sortie du Play
        // serait encore active au Play suivant.
        private void OnDisable()
        {
            _isSprinting = false;
        }

        // ---------- Geometrie de la capsule et requetes ----------

        private void GetCapsule(Vector3 position, out Vector3 bottomCenter, out Vector3 topCenter)
        {
            Vector3 center = position + transform.rotation * Vector3.Scale(_capsule.center, transform.lossyScale);
            float half = Height / 2f - Radius;
            bottomCenter = center + Vector3.down * half;
            topCenter = center + Vector3.up * half;
        }

        private bool CastCapsule(Vector3 position, Vector3 direction, float distance, out RaycastHit hit)
        {
            GetCapsule(position, out Vector3 bottom, out Vector3 top);
            return Physics.CapsuleCast(bottom, top, Radius, direction, out hit, distance, ~0, QueryTriggerInteraction.Ignore);
        }

        // Vrai si on touche un sol praticable. La normale du cast est fausse sur les aretes
        // (elle parait inclinee): on relit la vraie surface avec un rayon vertical.
        private bool ProbeGround(Vector3 position, float distance, out Vector3 normal, out float hitDistance)
        {
            normal = Vector3.up;
            hitDistance = 0f;
            if (!CastCapsule(position, Vector3.down, distance, out RaycastHit hit)) return false;

            normal = hit.normal;
            if (Physics.Raycast(hit.point + Vector3.up * 0.1f, Vector3.down, out RaycastHit surface, 0.3f, ~0, QueryTriggerInteraction.Ignore))
                normal = surface.normal;

            hitDistance = hit.distance;
            return Vector3.Angle(normal, Vector3.up) <= _settings.MaxSlopeAngle;
        }

        // Si la capsule est deja dans une surface (apparition, teleportation), on la repousse dehors.
        // Les casts ignorent ce qui les chevauche au depart: sans ca on traverserait le decor.
        private void Depenetrate(ref Vector3 position)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                GetCapsule(position, out Vector3 bottom, out Vector3 top);
                int count = Physics.OverlapCapsuleNonAlloc(bottom, top, Radius, _overlaps, ~0, QueryTriggerInteraction.Ignore);
                if (count == 0) return;

                for (int i = 0; i < count; i++)
                {
                    Collider other = _overlaps[i];
                    if (Physics.ComputePenetration(_capsule, position, transform.rotation,
                            other, other.transform.position, other.transform.rotation,
                            out Vector3 direction, out float distance))
                    {
                        // + SkinWidth: pile contre la surface, les casts ne la detectent plus (le sol "disparait",
                        // la gravite s'accumule et le joueur reste enfonce). Avec la marge, il est pose normalement.
                        position += direction * (distance + SkinWidth);
                    }
                }
            }
        }

        // ---------- Collisions: collide-and-slide ----------

        // Avance de "move" en glissant le long des surfaces touchees.
        // verticalPass: gravite/saut, on s'arrete sur un sol praticable au lieu de glisser dessus.
        private void SlideMove(ref Vector3 position, Vector3 move, bool allowStep, bool verticalPass)
        {
            for (int i = 0; i < MaxSlideIterations; i++)
            {
                float distance = move.magnitude;
                if (distance < 0.0001f) return;
                Vector3 direction = move / distance;

                if (!CastCapsule(position, direction, distance + SkinWidth, out RaycastHit hit))
                {
                    position += move;
                    return;
                }

                float travel = Mathf.Clamp(hit.distance - SkinWidth, 0f, distance);
                position += direction * travel;
                Vector3 remaining = move - direction * travel;

                Vector3 normal = hit.normal;
                bool walkable = Vector3.Angle(normal, Vector3.up) <= _settings.MaxSlopeAngle;

                if (walkable)
                {
                    if (verticalPass) return;                          // pose sur le sol
                    move = Vector3.ProjectOnPlane(remaining, normal);  // glisse le long de la pente
                    continue;
                }

                if (allowStep && !verticalPass && TryStepUp(ref position, remaining))
                {
                    move = remaining; // on a ete souleve, on continue d'avancer
                    continue;
                }

                move = SlideAlongSteep(remaining, normal);
            }
        }

        // Mur ou pente trop raide: on retire la part du mouvement qui va vers lui (pas de "grimpe")
        private Vector3 SlideAlongSteep(Vector3 remaining, Vector3 normal)
        {
            Vector3 flat = new Vector3(normal.x, 0f, normal.z);
            Vector3 horizontal = new Vector3(remaining.x, 0f, remaining.z);

            if (flat.sqrMagnitude > 0.0001f)
            {
                flat.Normalize();
                float into = Vector3.Dot(horizontal, flat);
                if (into < 0f) horizontal -= into * flat;
            }

            float vertical = remaining.y;
            if (normal.y < 0f) return horizontal + Vector3.up * Mathf.Min(vertical, 0f); // plafond / surplomb
            if (vertical < 0f) return horizontal + Vector3.ProjectOnPlane(Vector3.up * vertical, normal); // glisse vers le bas
            return horizontal + Vector3.up * vertical;
        }

        // Souleve le joueur sur une marche basse. Le saut de hauteur est instantane pour la physique,
        // mais la camera le rattrape en douceur (voir _cameraOffsetY dans Update): effet de glisse.
        private bool TryStepUp(ref Vector3 position, Vector3 remaining)
        {
            Vector3 horizontal = new Vector3(remaining.x, 0f, remaining.z);
            float length = horizontal.magnitude;
            if (length < 0.0001f) return false;
            Vector3 direction = horizontal / length;

            GetCapsule(position, out Vector3 bottom, out _);
            float feetY = bottom.y - Radius;

            // Hauteur du dessus de la marche, juste apres l'obstacle
            Vector3 origin = position + direction * (Radius + SkinWidth + 0.05f);
            origin.y = feetY + _settings.MaxStepHeight + 0.05f;
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit top, _settings.MaxStepHeight + 0.1f, ~0, QueryTriggerInteraction.Ignore)) return false;
            if (Vector3.Angle(top.normal, Vector3.up) > _settings.MaxSlopeAngle) return false;

            float rise = top.point.y - feetY + SkinWidth;
            if (rise <= 0.01f || rise > _settings.MaxStepHeight + SkinWidth) return false;

            // Rien au-dessus de la tete
            if (CastCapsule(position, Vector3.up, rise + SkinWidth, out _)) return false;

            // Une fois souleve, la voie doit etre libre devant (sinon c'est un mur plus haut)
            Vector3 raised = position + Vector3.up * rise;
            if (CastCapsule(raised, direction, Mathf.Max(length, 0.05f) + SkinWidth, out _)) return false;

            position = raised;
            return true;
        }

        // ---------- Boucle principale ----------

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, MaxDeltaTime);
            if (dt <= 0f) return;

            Vector3 position = transform.position;
            float heightBeforePush = position.y;
            Depenetrate(ref position);
            // Repousse vers le haut (apparition dans le sol): la vitesse de chute accumulee n'a plus de sens
            if (position.y > heightBeforePush + 0.001f && _verticalVelocity < 0f)
                _verticalVelocity = 0f;

            // Pas "au sol" tant qu'on monte (sinon un saut est annule a haute frequence d'images)
            bool onGround = ProbeGround(position, GroundProbeDistance, out Vector3 groundNormal, out _) && _verticalVelocity <= 0f;
            bool wasOnGround = onGround;

            bool jumped = false;
            if (_jumpRequested && onGround)
            {
                _verticalVelocity = _settings.JumpSpeed;
                onGround = false;
                jumped = true;
            }
            _jumpRequested = false;

            // 1) Deplacement horizontal (relatif a l'orientation du joueur, la souris tourne le corps)
            Vector3 input = Vector3.ClampMagnitude(new Vector3(_movementInput.x, 0f, _movementInput.z), 1f);
            Vector3 direction = transform.right * input.x + transform.forward * input.z;
            if (direction.sqrMagnitude > 0.0001f)
            {
                float currentSpeed = _isSprinting ? _settings.WalkSpeed * _settings.SprintMultiplier : _settings.WalkSpeed;
                float amount = currentSpeed * dt * direction.magnitude;
                Vector3 move = onGround
                    ? Vector3.ProjectOnPlane(direction, groundNormal).normalized * amount // suit la pente
                    : direction.normalized * amount;
                SlideMove(ref position, move, onGround, false);
            }

            // 2) Gravite et saut (vitesse verticale geree a la main)
            if (onGround) _verticalVelocity = 0f;
            else _verticalVelocity += _settings.Gravity * dt;

            if (Mathf.Abs(_verticalVelocity) > 0f)
                SlideMove(ref position, Vector3.up * (_verticalVelocity * dt), false, true);

            // 3) Sol apres le mouvement
            bool grounded = ProbeGround(position, GroundProbeDistance, out _, out _);
            if (grounded && _verticalVelocity > 0f) grounded = false; // on monte encore
            if (grounded) _verticalVelocity = Mathf.Min(_verticalVelocity, 0f);

            // 4) Colle au sol: descente de marche ou de pente (la camera glisse vers le bas)
            if (!grounded && wasOnGround && !jumped && _verticalVelocity <= 0f)
            {
                if (ProbeGround(position, _settings.MaxStepHeight + SkinWidth, out _, out float hitDistance))
                {
                    float drop = Mathf.Max(0f, hitDistance - SkinWidth);
                    position += Vector3.down * drop;
                    _verticalVelocity = 0f;
                    grounded = true;
                }
            }

            // Glisse: quand on marche au sol, la camera ne suit pas les changements de hauteur d'un coup
            // (marches, rebords, pentes). Elle les rattrape en douceur. En l'air (saut, chute): aucun lissage.
            if (grounded && wasOnGround && !jumped)
                _cameraOffsetY -= position.y - transform.position.y;

            _isGrounded = grounded;
            transform.position = position;
            UpdateCameraSmoothing(dt);
        }

        private void UpdateCameraSmoothing(float dt)
        {
            _cameraOffsetY = Mathf.Clamp(_cameraOffsetY, -0.6f, 0.6f);
            _cameraOffsetY = Mathf.Lerp(_cameraOffsetY, 0f, 1f - Mathf.Exp(-_settings.StepSmoothing * dt));
            if (_cameraTransform != null)
                _cameraTransform.localPosition = _cameraBaseLocalPosition + Vector3.up * _cameraOffsetY;
        }
    }
}
