# xm4-control

Appli Windows (WinForms, .NET 10) + CLI `xm4` pour piloter un Sony WH-1000XM4 en Bluetooth. Voir le README
pour le protocole.

## Commandes

- `dotnet build` / `dotnet test` depuis la racine du projet.
- L'exe de l'appli verrouille sa sortie : arrêter `Xm4Control` (`Stop-Process -Name Xm4Control`) avant de recompiler.
- Tester sur le vrai casque avec la CLI : `src/Xm4Control.Cli/bin/Debug/net10.0-windows/xm4.exe status -v`
  (l'appli ne doit pas tourner : une seule connexion RFCOMM à la fois).

## Règles

- Textes visibles (UI, CLI, messages d'erreur) et commentaires en français ; identifiants en anglais.
- Toute nouvelle commande ou tout nouveau décodage du protocole : test dans `tests/` avec des payloads
  réellement capturés (`xm4 raw <hex> -v`), et mise à jour du tableau du README.
- Ne pas couper le profil mains libres (`xm4 mic off`) sans prévenir : ça coupe le micro du casque en cours d'appel.
