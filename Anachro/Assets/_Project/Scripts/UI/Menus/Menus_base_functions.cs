using Anachronism.Network;
using FishNet;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Anachronism
{
    public class Menus_base_functions : MonoBehaviour
    {
        protected UIDocument uiDocument;
        protected VisualElement root;

        protected void initialize()
        {
            uiDocument = gameObject.GetComponent<UIDocument>();
            root = uiDocument.rootVisualElement;
        }
        
        //functions
        protected void OpenMenu(VisualElement menu)
        {
            menu.style.display = DisplayStyle.Flex;
        }

        protected void CloseMenu(VisualElement menu)
        {
            menu.style.display = DisplayStyle.None;
        }

        // En ligne : on quitte la partie par SteamLobby, qui ferme la connexion (et la session si c'est l'hôte) ;
        // FishNet ramène ensuite ce joueur à l'écran titre. Un LoadScene seul laisserait la connexion ouverte.
        // Hors ligne (scène testée seule), on charge la scène comme avant.
        protected void LeaveOnlineGame(string offlineScene)
        {
            SteamLobby steamLobby = InstanceFinder.NetworkManager != null
                ? InstanceFinder.NetworkManager.GetComponent<SteamLobby>()
                : null;
            if (steamLobby != null && InstanceFinder.NetworkManager.IsClientStarted)
            {
                steamLobby.LeaveGame();
                return;
            }

            SceneManager.LoadScene(offlineScene);
        }
    }
}
