# xm4-control

Appli Windows (WPF, .NET 10) + CLI `xm4` pour piloter un Sony WH-1000XM4 en Bluetooth. Voir le README
pour le protocole et la publication d'une version.

## Commandes

- `dotnet build` / `dotnet test` depuis la racine du projet.
- L'exe de l'appli verrouille sa sortie : arrêter `Xm4Control` (`Stop-Process -Name Xm4Control`) avant de recompiler.
- Tester sur le vrai casque avec la CLI : `src/Xm4Control.Cli/bin/Debug/net10.0-windows/xm4.exe status -v`
  (l'appli ne doit pas tourner : une seule connexion RFCOMM à la fois).
- Version : `Directory.Build.props`. Release : exe autonomes publiés dans `publish/` puis `gh release create`.

## Appli

- WPF-UI 4 (contrôles Windows 11, Mica, thème système) et H.NotifyIcon.Wpf (icône de notification).
- `MainViewModel` relaie `HeadsetController` : ses setters envoient la commande, l'état réel revient par
  `Changed`, qui rafraîchit toutes les liaisons (`PropertyChanged("")`).
- Les pages de réglages sont créées par `NavigationView` : elles prennent leur DataContext dans `App.Current.ViewModel`.
- H.NotifyIcon n'accepte en `IconSource` que des images chargées depuis une URI : l'icône passe par `Icon` (GDI).
- Les `NavigationViewItem` de WPF-UI n'exposent aucun motif UI Automation cliquable.
- Mémoire : popup et Réglages créés à la première ouverture, Réglages détruite à la fermeture ;
  `MemoryTrimmer` rend la mémoire à Windows 10 s après que l'appli repasse en arrière-plan (~2 Mo au repos).

## Règles

- Textes visibles (UI, CLI, messages d'erreur) et commentaires en français ; identifiants en anglais.
- Ne pas réécrire les sources avec PowerShell 5.1 (`Get-Content`/`Set-Content`) : il lit l'UTF-8 sans BOM
  comme de l'ANSI et abîme les accents.
- Toute nouvelle commande ou tout nouveau décodage du protocole : test dans `tests/` avec des payloads
  réellement capturés (`xm4 raw <hex> -v`), et mise à jour du tableau du README.
- Ne pas couper le profil mains libres (`xm4 mic off`) sans prévenir : ça coupe le micro du casque en cours d'appel.
