# xm4-control

Appli Windows (WinUI 3, .NET 10) + CLI `xm4` pour piloter un Sony WH-1000XM4 en Bluetooth. Voir le README
pour le protocole et la publication d'une version.

## Commandes

- `dotnet build` / `dotnet test` depuis la racine du projet (pas besoin de Visual Studio).
- L'exe de l'appli verrouille sa sortie : arrêter `Xm4Control` (`Stop-Process -Name Xm4Control`) avant de recompiler.
- Exe de debug : `src/Xm4Control.App/bin/x64/Debug/net10.0-windows10.0.26100.0/win-x64/Xm4Control.exe`
  (`--log` : trace des trames dans `%LOCALAPPDATA%\Xm4Control\trace.log`, `--tray` : sans fenêtre).
- Tester sur le vrai casque avec la CLI : `src/Xm4Control.Cli/bin/Debug/net10.0-windows/xm4.exe status -v`
  (l'appli ne doit pas tourner : une seule connexion RFCOMM à la fois ; espacer les appels de quelques secondes).
- Version : `Directory.Build.props`. Release : `dotnet publish ... -p:Platform=x64 -o publish` puis `gh release create`.

## Appli (WinUI 3)

- Non empaquetée, runtime Windows App SDK embarqué, exe unique élagué et compressé (~35 Mo). Les composants IA,
  ML, recherche et widgets du Windows App SDK sont exclus (`ExcludeAssets="all"`) : sinon l'exe fait 300 Mo.
- Élagage (trimming) : pas de réflexion. Le JSON des réglages passe par le générateur de sources (`SettingsJson`).
- `Program.cs` remplace le Main généré (instance unique) ; `DispatcherShutdownMode.OnExplicitShutdown`.
- `MainViewModel` relaie `HeadsetController` ; l'état réel revient par `Changed` → `PropertyChanged("")`.
  Les liaisons x:Bind TwoWay réécrivent la valeur à chaque rafraîchissement : chaque setter ignore une valeur
  identique à l'état courant.
- Pièges WinUI rencontrés :
  - la conversion implicite bool → Visibility de x:Bind ne s'applique pas : utiliser `Ui.Show(...)` ;
  - `IsEnabled` n'existe que sur les contrôles, pas sur Grid/StackPanel ;
  - `AppWindow.ResizeClient` est ignoré une fois la fenêtre affichée : utiliser `MoveAndResize` ;
  - les fichiers `Content` (icône) doivent avoir `CopyToPublishDirectory` ;
  - H.NotifyIcon n'accepte qu'une `System.Drawing.Icon` pour une icône générée ; menus natifs sombres via
    `Ui.FollowSystemThemeInMenus` (uxtheme, non documenté).
- Mémoire : fenêtres créées à la demande, Réglages détruite à la fermeture ; `MemoryTrimmer` nettoie 10 s puis
  60 s après le retour en arrière-plan (WinUI relâche des ressources en différé). ~3-8 Mo au repos.
- Glyphes Segoe Fluent Icons : en XAML `&#xE7F6;`, en C# `MainViewModel.Glyph(0xE7F6)`. Ne pas écrire `""` :
  les outils d'édition le transforment en caractère invisible.

## Règles

- Textes visibles (UI, CLI, messages d'erreur) et commentaires en français ; identifiants en anglais.
- Ne pas réécrire les sources avec PowerShell 5.1 (`Get-Content`/`Set-Content`) : il lit l'UTF-8 sans BOM
  comme de l'ANSI et abîme les accents.
- Toute nouvelle commande ou tout nouveau décodage du protocole : test dans `tests/` avec des payloads
  réellement capturés (`xm4 raw <hex> -v`), et mise à jour du tableau du README.
- Ne pas couper le profil mains libres (`xm4 mic off`) sans prévenir : ça coupe le micro du casque en cours d'appel.
