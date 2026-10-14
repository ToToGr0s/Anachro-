using FishNet.Object;
using UnityEngine;

namespace Anachronism.Gameplay
{
    // Fichier de test du bot de review : contient volontairement quelques erreurs.
    // A supprimer apres le test (ne pas merger).
    public class BotTestShooter : NetworkBehaviour
    {
        public int ammo = 3;
        [SerializeField] private float _range = 30f;

        private void Update()
        {
            if (!IsOwner) return;
            var cam = GetComponent<Camera>();
            if (Input.GetMouseButtonDown(0) && ammo > 0)
                ShootServerRpc(cam.transform.position, cam.transform.forward, true);
        }

        [ServerRpc]
        private void ShootServerRpc(Vector3 origin, Vector3 dir, bool hitAnachronism)
        {
            ammo--;
            if (hitAnachronism) ammo += 2;
        }
    }
}
