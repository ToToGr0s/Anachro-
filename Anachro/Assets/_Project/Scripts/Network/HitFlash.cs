using System.Collections;
using System.Collections.Generic;
using Anachronism.Gameplay;
using Anachronism.Npc;
using FishNet.Object;
using UnityEngine;

namespace Anachronism.Network
{
    /// <summary>
    /// Clignotement rouge d'un personnage touché par un tir, vu par tous ceux qui le voient. Même composant et même rendu
    /// sur le joueur et le PNJ : le flash ne dit pas ce qui a été touché (règle réseau 5). L'hôte décide (NetworkShooting),
    /// chaque client anime en local (règle réseau 4).
    /// </summary>
    public sealed class HitFlash : NetworkBehaviour
    {
        [SerializeField] private GameplayTuning _tuning;
        [Tooltip("NpcSkin de l'enfant « Model » : seul le corps clignote, pas l'objet tenu ni la vue à la première personne.")]
        [SerializeField] private NpcSkin _skin;

        // URP Lit (_BaseColor) et anciens shaders (_Color), comme la stase de PlayerSkin.
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        // Marque notre bloc : au retour, un renderer qui ne la porte plus a reçu un autre bloc entre-temps (stase d'un
        // fugitif capturé par ce tir), qu'il ne faut pas effacer.
        private static readonly int MarkerId = Shader.PropertyToID("_AnachroHitFlash");

        private readonly List<Renderer> _renderers = new List<Renderer>();
        // Bloc de chaque renderer avant le rouge, rétabli après.
        private readonly List<MaterialPropertyBlock> _saved = new List<MaterialPropertyBlock>();
        private MaterialPropertyBlock _flashBlock;
        private MaterialPropertyBlock _probe;
        private Coroutine _flash;
        private bool _isRed;

        /// <summary>Hôte : ce personnage vient d'être touché. Aucun paramètre : PNJ, fugitif et chasseur clignotent pareil.</summary>
        [Server]
        public void FlashServer() => ObserversFlash();

        [ObserversRpc]
        private void ObserversFlash()
        {
            if (_tuning == null || _skin == null || _skin.Model == null)
                return;

            // Retouché pendant un flash : on repart de zéro.
            if (_flash != null)
                StopCoroutine(_flash);
            SetRed(false);
            _flash = StartCoroutine(Flash());
        }

        private void OnDisable()
        {
            // Despawn ou retour au pool en plein flash : le prochain spawn ne doit pas rester rouge.
            _flash = null;
            SetRed(false);
        }

        private IEnumerator Flash()
        {
            int count = _tuning.HitFlashCount;
            var halfPeriod = new WaitForSeconds(_tuning.HitFlashDuration / (count * 2f));
            for (int i = 0; i < count; i++)
            {
                SetRed(true);
                yield return halfPeriod;
                SetRed(false);
                yield return halfPeriod;
            }

            _flash = null;
        }

        private void SetRed(bool isRed)
        {
            if (isRed == _isRed)
                return;

            _isRed = isRed;
            if (isRed)
                ApplyRed();
            else
                Restore();
        }

        private void ApplyRed()
        {
            if (_flashBlock == null)
            {
                _flashBlock = new MaterialPropertyBlock();
                _probe = new MaterialPropertyBlock();
            }

            Color color = _tuning.HitFlashColor;
            _flashBlock.SetColor(BaseColorId, color);
            _flashBlock.SetColor(ColorId, color);
            _flashBlock.SetFloat(MarkerId, 1f);

            // Renderers relus à chaque fois : le skin peut avoir été re-tiré depuis le dernier flash.
            _renderers.Clear();
            if (_skin.Model != null)
                _skin.Model.GetComponentsInChildren(true, _renderers);

            for (int i = 0; i < _renderers.Count; i++)
            {
                if (_saved.Count <= i)
                    _saved.Add(new MaterialPropertyBlock());

                _renderers[i].GetPropertyBlock(_saved[i]);
                _renderers[i].SetPropertyBlock(_flashBlock);
            }
        }

        private void Restore()
        {
            for (int i = 0; i < _renderers.Count; i++)
            {
                Renderer flashed = _renderers[i];
                // Modèle détruit (skin re-tiré) : rien à rétablir.
                if (flashed == null)
                    continue;

                flashed.GetPropertyBlock(_probe);
                if (!_probe.HasFloat(MarkerId))
                    continue;

                flashed.SetPropertyBlock(_saved[i].isEmpty ? null : _saved[i]);
            }

            _renderers.Clear();
        }
    }
}
