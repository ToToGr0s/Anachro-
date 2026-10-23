using Anachronism.Gameplay;
using UnityEngine;

namespace Anachronism.Network
{
    /// <summary>
    /// Côté hôte : vérifie chaque position envoyée par un joueur (mouvement client-autoritaire, règle réseau 8).
    /// Refuse vitesse, téléportation, traversée de murs, vol et statue capturée qui bouge ; l'appelant recale alors le
    /// joueur sur LastValid. Mêmes règles que CharacterMovement (PlayerControllerSettings), avec des marges pour le lag.
    /// </summary>
    public sealed class MovementValidator
    {
        // Petite sphère au centre du corps : attrape les murs sans frotter le sol ni les rebords qu'on longe.
        private const float WallProbeRadius = 0.1f;
        private const float GroundProbeRadius = 0.2f;
        // Au-dessus du sol jusqu'à cette marge sous les pieds : encore « au sol » (marches, petits sauts, lag).
        private const float GroundMargin = 0.5f;
        // Une statue peut encore glisser du dernier paquet en route au moment de la capture.
        private const float FrozenTolerance = 0.1f;

        private readonly PlayerControllerSettings _settings;
        private readonly float _centerToFeet;
        private readonly int _blockingMask;

        private float _budget;
        private double _lastTime;
        private float _takeoffY;
        private float _airTime;
        private bool _isFrozen;

        public Vector3 LastValid { get; private set; }

        /// <param name="centerToFeet">Distance du pivot (centre de la capsule) aux pieds.</param>
        public MovementValidator(PlayerControllerSettings settings, float centerToFeet)
        {
            _settings = settings;
            _centerToFeet = centerToFeet;
            // Les autres personnages bougent sans arrêt et se poussent : seul le décor compte.
            _blockingMask = ~((1 << LayerMask.NameToLayer("Character")) | (1 << LayerMask.NameToLayer("Ignore Raycast")));
        }

        private float MaxSpeed => _settings.WalkSpeed * _settings.SprintMultiplier * _settings.ServerSpeedTolerance;

        /// <summary>Apparition ou téléportation décidée par l'hôte : nouveau point de départ.</summary>
        public void Reset(Vector3 position, double time)
        {
            LastValid = position;
            _budget = MaxSpeed * _settings.ServerMoveBurst;
            _lastTime = time;
            _takeoffY = position.y;
            _airTime = 0f;
        }

        public void SetFrozen(bool isFrozen) => _isFrozen = isFrozen;

        public bool TryAccept(Vector3 next, double time, out string reason)
        {
            float dt = Mathf.Max(0f, (float)(time - _lastTime));
            _lastTime = time;
            // Budget de distance qui se remplit avec le temps de l'hôte : des paquets groupés après un ralentissement passent,
            // une vitesse tenue au-dessus du maximum non.
            _budget = Mathf.Min(_budget + MaxSpeed * dt, MaxSpeed * _settings.ServerMoveBurst);

            Vector3 delta = next - LastValid;
            if (_isFrozen)
            {
                if (delta.sqrMagnitude > FrozenTolerance * FrozenTolerance)
                    return Refuse("capturé, ne doit plus bouger", out reason);

                return Accept(next, 0f, out reason);
            }

            float horizontal = new Vector2(delta.x, delta.z).magnitude;
            if (horizontal > _budget)
                return Refuse($"vitesse ({horizontal:0.0} m, {_budget:0.0} m permis)", out reason);

            float distance = delta.magnitude;
            if (distance > 0.001f && Physics.SphereCast(LastValid, WallProbeRadius, delta / distance, out RaycastHit wall, distance,
                    _blockingMask, QueryTriggerInteraction.Ignore))
                return Refuse($"traverse {wall.collider.name}", out reason);

            float probe = _centerToFeet - GroundProbeRadius + GroundMargin;
            bool isGrounded = Physics.SphereCast(next, GroundProbeRadius, Vector3.down, out _, probe, _blockingMask, QueryTriggerInteraction.Ignore);
            if (isGrounded)
            {
                _takeoffY = next.y;
                _airTime = 0f;
            }
            else
            {
                _airTime += dt;
                float gravity = Mathf.Max(0.01f, Mathf.Abs(_settings.Gravity));
                float jumpHeight = _settings.JumpSpeed * _settings.JumpSpeed / (2f * gravity);
                float jumpDuration = 2f * _settings.JumpSpeed / gravity;
                if (next.y > _takeoffY + jumpHeight + _settings.ServerHeightTolerance)
                    return Refuse("vol (trop haut)", out reason);
                // Une chute descend sous le point de départ ; rester en l'air au-dessus après un saut complet, c'est voler.
                if (_airTime > jumpDuration + _settings.ServerAirTimeTolerance && next.y >= _takeoffY)
                    return Refuse("vol (reste en l'air)", out reason);
            }

            return Accept(next, horizontal, out reason);
        }

        private bool Accept(Vector3 next, float horizontal, out string reason)
        {
            _budget -= horizontal;
            LastValid = next;
            reason = null;
            return true;
        }

        private static bool Refuse(string why, out string reason)
        {
            reason = why;
            return false;
        }
    }
}
