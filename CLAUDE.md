# xm4-control

Appli Windows (WinUI 3, .NET 10) + CLI `xm4` pour piloter un Sony WH-1000XM4 en Bluetooth. Voir le README
pour le protocole et la publication d'une version. `README.md` est en anglais (lu par les gens qui cherchent
l'appli), `README.fr.md` en est la version française : toujours modifier les deux ensemble.

## Commandes

- `dotnet build` / `dotnet test` depuis la racine du projet (pas besoin de Visual Studio).
- L'exe de l'appli verrouille sa sortie : arrêter `Xm4Control` (`Stop-Process -Name Xm4Control`) avant de recompiler.
- Exe de debug (`dotnet build` sans plateforme) : `src/Xm4Control.App/bin/Debug/net10.0-windows10.0.26100.0/win-x64/Xm4Control.exe`
  (`--log` : trace des trames dans `%LOCALAPPDATA%\Xm4Control\trace.log`, `--tray` : sans fenêtre). Le dossier
  `bin/x64/Debug` vient d'anciennes compilations avec `-p:Platform=x64` et n'est plus à jour.
- Tester sur le vrai casque avec la CLI : `src/Xm4Control.Cli/bin/Debug/net10.0-windows/xm4.exe status -v`
  (l'appli ne doit pas tourner : une seule connexion RFCOMM à la fois ; espacer les appels de quelques secondes).
- Version : `Directory.Build.props`. Release : GitHub Actions (`.github/workflows/build.yml`) la publie quand on
  pousse un tag `v<version>` identique, annoté (`git tag -a`) avec les nouveautés en anglais ; notes = message du
  tag (à défaut, commits depuis le tag précédent) + `.github/release-notes.md` (description complète de l'appli,
  en anglais, à tenir à jour avec le README).

## Appli (WinUI 3)

- Non empaquetée, runtime Windows App SDK embarqué, exe unique élagué et compressé (~40 Mo). Les composants IA,
  ML, recherche et widgets du Windows App SDK sont exclus (`ExcludeAssets="all"`) : sinon l'exe fait 300 Mo.
- Élagage (trimming) : pas de réflexion. Le JSON des réglages passe par le générateur de sources (`SettingsJson`).
- `Program.cs` remplace le Main généré (instance unique) ; `DispatcherShutdownMode.OnExplicitShutdown`.
- `MainViewModel` relaie `HeadsetController` ; l'état réel revient par `Changed` → `PropertyChanged("")`.
  Les liaisons x:Bind TwoWay réécrivent la valeur à chaque rafraîchissement : chaque setter ignore une valeur
  identique à l'état courant.
- Fenêtre principale (`SettingsWindow`) : page web locale `src/Xm4Control.App/Web` (HTML/CSS/JS sans framework)
  dans WebView2, servie sur l'hôte virtuel `https://app.xm4/`. `WebBridge` envoie l'état complet (`WebState`,
  JSON par générateur de sources `WebJson`) à chaque changement et applique les commandes `{cmd, value}` de la
  page. Nouveau réglage : propriété dans `WebState`, cas dans `WebBridge.Apply`, élément `data-switch`
  dans `index.html`. Hors de l'appli (navigateur), `app.js` affiche un état d'exemple pour l'aperçu.
  Casque 3D : `headset3d.js` (three.js embarqué dans `Web/vendor`, imports réécrits en chemins relatifs car la
  CSP interdit une importmap en ligne) ; rendu à la demande seulement, image `headset.png` si WebGL échoue.
  Le popup reste en WinUI (ouverture instantanée, mémoire au repos).
- Pièges WinUI rencontrés :
  - la conversion implicite bool → Visibility de x:Bind ne s'applique pas : utiliser `Ui.Show(...)` ;
  - `IsEnabled` n'existe que sur les contrôles, pas sur Grid/StackPanel ;
  - `AppWindow.ResizeClient` est ignoré une fois la fenêtre affichée : utiliser `MoveAndResize` ;
  - les fichiers `Content` (icône) doivent avoir `CopyToPublishDirectory` ;
  - H.NotifyIcon n'accepte qu'une `System.Drawing.Icon` pour une icône générée ; menus natifs sombres via
    `Ui.FollowSystemThemeInMenus` (uxtheme, non documenté).
- Changement de langue (page Application) : `MainViewModel.LanguageChanged` ; la page web se recharge avec
  `?lang=`, le popup réévalue ses liaisons (`Bindings.Update`), le menu de l'icône reprend ses textes à chaque
  mise à jour. Les textes fixes du popup sont des propriétés de `FlyoutWindow` liées par x:Bind.
- Icône de notification (`TrayIcons`) : pile dessinée en GDI+ à la taille des petites icônes de la barre des
  tâches (DPI de `Shell_TrayWnd`), blanche ou noire selon `SystemUsesLightTheme` ; redessinée seulement quand
  `TrayIcons.Look` (niveau, thème, taille) change.
- Mémoire : fenêtres créées à la demande, Réglages détruite à la fermeture ; `MemoryTrimmer` nettoie 10 s puis
  60 s après le retour en arrière-plan (WinUI relâche des ressources en différé). ~3-8 Mo au repos.
- Glyphes Segoe Fluent Icons : en XAML `&#xE7F6;`, en C# `MainViewModel.Glyph(0xE7F6)`. Ne pas écrire `""` :
  les outils d'édition le transforment en caractère invisible.

## Règles

- Textes visibles (UI, CLI, messages d'erreur) en français et en anglais, côte à côte : `T("Bruit", "Noise")`
  en C# (`Lang`, langue de Windows ou réglage `Language`), attributs `data-en` / `data-en-<attribut>` dans
  `Web/index.html` (`WebPageTests` vérifie qu'aucun ne manque), `T(fr, en)` dans `app.js`. Les traces de `--log`
  et les exceptions internes restent en français. Commentaires en français ; identifiants en anglais.
- Ne pas réécrire les sources avec PowerShell 5.1 (`Get-Content`/`Set-Content`) : il lit l'UTF-8 sans BOM
  comme de l'ANSI et abîme les accents.
- Toute nouvelle commande ou tout nouveau décodage du protocole : test dans `tests/` avec des payloads
  réellement capturés (`xm4 raw <hex> -v`), et mise à jour du tableau des deux README.
- Ne pas couper le profil mains libres (`xm4 mic off`) sans prévenir : ça coupe le micro du casque en cours d'appel.
