# ThirdParty

Code externe. **Ne pas modifier**, sauf les ajouts locaux listés ici.

| Dossier | Version | Source |
|---|---|---|
| `FishNet/` | 4.7.3 | Asset Store « FishNet: Networking Evolved » |
| `FishySteamworks/` | 4.1.1 | https://github.com/FirstGearGames/FishySteamworks/releases |
| Steamworks.NET | commit `ba71581f` | Package Manager (URL Git figée dans `Packages/manifest.json`) |

## Ajouts et retraits locaux (à refaire après chaque mise à jour)

- `FishySteamworks/FishySteamworks.asmdef` : le plugin n'a pas d'asmdef. Sans lui, il compile dans
  `Assembly-CSharp` et `Anachronism.Runtime` ne peut pas le référencer. Une mise à jour de
  FishySteamworks peut l'écraser : le remettre (réf. `FishNet.Runtime`, `GameKit.Dependencies`,
  `com.rlabrecque.steamworks.net`).
- `FishNet/Demos/` supprimé : inutile au jeu. Le décocher à l'import lors d'une mise à jour de FishNet.

## Mettre à jour FishNet

L'Asset Store importe dans `Assets/FishNet/` : importer, puis glisser le contenu dans `ThirdParty/FishNet/`
**depuis la fenêtre Project de Unity** (jamais l'explorateur, sinon les `.meta` sont perdus).
