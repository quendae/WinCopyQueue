<p align="center">
  <img src="src/WinCopyQueue.App/Assets/logo-full.png" alt="WinCopyQueue" width="420">
</p>

<p align="center">
  <a href="README.md">English</a> · <a href="README.pl.md">Polski</a> · <a href="README.de.md">Deutsch</a> · <strong>Français</strong> · <a href="README.es.md">Español</a> · <a href="README.pt.md">Português</a> · <a href="README.zh-CN.md">简体中文</a> · <a href="README.ja.md">日本語</a>
</p>

# WinCopyQueue

WinCopyQueue ajoute à l’Explorateur Windows une file d’attente simple pour les copies et les déplacements. Au lieu de lancer plusieurs transferts en parallèle, il les exécute séquentiellement — une session après l’autre et un fichier à la fois.

L’application fonctionne dans la zone de notification et ne garde pas de fenêtre principale ouverte en permanence. Le panneau compact de la file d’attente apparaît uniquement lorsqu’un transfert est ajouté, peut être masqué à tout moment et les transferts continuent en arrière-plan.

## Téléchargement

Version actuelle : **1.0.0**

- [Télécharger l’installateur WinCopyQueue 1.0.0](https://github.com/quendae/WinCopyQueue/releases/download/v1.0.0/WinCopyQueue-Setup-1.0.0-x64.exe)
- [Télécharger WinCopyQueue.exe en version autonome](https://github.com/quendae/WinCopyQueue/releases/download/v1.0.0/WinCopyQueue.exe)
- [Voir la version v1.0.0](https://github.com/quendae/WinCopyQueue/releases/tag/v1.0.0)

WinCopyQueue fonctionne sous **Windows 10 1809 ou version ultérieure**, y compris Windows 11. L’installation se fait pour l’utilisateur courant et ne nécessite pas de droits administrateur.

> Ce dépôt ne contient actuellement aucun fichier `LICENSE`.

## Fonctionnement

1. Lancez `WinCopyQueue.exe`.
2. Dans l’Explorateur Windows, copiez ou coupez les fichiers normalement avec `Ctrl+C` / `Ctrl+X`.
3. Dans le dossier de destination, utilisez `Ctrl+V` ou choisissez **Coller avec WinCopyQueue** dans le menu contextuel.

Si un transfert est déjà en cours, le suivant est simplement ajouté à la fin de la file d’attente. Plusieurs grosses opérations n’entrent ainsi pas en concurrence pour le même disque au même moment.

Sous Windows 11, l’entrée statique du menu contextuel peut apparaître sous **Afficher plus d’options**.

<p align="center">
  <img src="docs/images/WinCopyQueue_screenshot.png" alt="WinCopyQueue pendant un transfert actif" width="480">
</p>

## Fonctionnalités principales

- copie et déplacement de fichiers individuels ou de dossiers complets,
- plusieurs sessions indépendantes dans une seule file d’attente séquentielle,
- pause et reprise de toute la file d’attente ou de fichiers individuels,
- annulation d’une session entière ou d’un fichier sélectionné,
- annulation d’une session sans supprimer les fichiers déjà copiés correctement,
- gestion des conflits avec comparaison du chemin, de la taille et de la date de modification,
- choix **Remplacer**, **Ignorer** ou **Annuler la session**, avec possibilité d’appliquer la décision aux conflits suivants,
- panneau compact affichant le fichier en cours, la progression, le nombre de fichiers et la vitesse de transfert,
- liste virtualisée et extensible de tous les fichiers et de leur état,
- historique des sessions terminées, annulées et en erreur,
- notifications système lors de l’ajout, de la fin ou de l’échec d’un transfert,
- démarrage automatique optionnel avec Windows,
- huit langues d’interface : anglais, polonais, allemand, français, espagnol, portugais, chinois simplifié et japonais.

## Copies et déplacements plus sûrs

WinCopyQueue n’écrit pas un fichier incomplet directement sous son nom final. Les données sont d’abord écrites dans un fichier temporaire `*.queue-part-*`, puis publiées sous leur nom définitif uniquement lorsque le transfert s’est terminé correctement.

Pour les copies classiques, une vérification **SHA-256** optionnelle peut être activée. WinCopyQueue calcule le hash de la source pendant la copie, puis relit la destination afin de comparer le résultat.

Lors d’un déplacement entre deux volumes différents, la vérification est exécutée automatiquement avant la suppression de la source, quel que soit le réglage de l’interface. Si la copie, la vérification ou la finalisation échoue, la source reste intacte.

## Panneau de file d’attente et zone de notification

Le panneau s’ouvre automatiquement lorsqu’un transfert est ajouté et apparaît en bas à droite de l’écran sans prendre le focus à l’Explorateur. Il peut être réduit pendant que les transferts continuent en arrière-plan.

Un double-clic sur l’icône de la zone de notification ou la commande **Afficher la file d’attente** rouvre le panneau. Le menu permet également de mettre toute la file en pause ou de la reprendre, d’activer le démarrage automatique, de réparer l’intégration à l’Explorateur et de quitter l’application.

## Conflits de fichiers

Si un fichier du même nom existe déjà à la destination, WinCopyQueue affiche les deux fichiers avec leur taille et leur date de modification. Trois actions sont disponibles :

- **Remplacer**,
- **Ignorer**,
- **Annuler la session**.

Remplacer ou Ignorer peut également être appliqué à tous les conflits suivants de la même session.

## Paramètres et diagnostic

Les paramètres utilisateur sont enregistrés dans :

```text
%LOCALAPPDATA%\WinCopyQueue\settings.json
```

Le journal de diagnostic se trouve dans :

```text
%LOCALAPPDATA%\WinCopyQueue\WinCopyQueue.log
```

La langue choisie et le réglage de vérification SHA-256 sont conservés entre les lancements.

## Ligne de commande

WinCopyQueue peut également recevoir des transferts directement depuis la ligne de commande :

```powershell
WinCopyQueue.exe --copy "D:\Destination" "D:\Fichier.txt" "D:\Dossier"
WinCopyQueue.exe --move "D:\Destination" "D:\Fichier.txt"
WinCopyQueue.exe --paste "D:\Destination"
```

Relancer l’application ne crée pas une deuxième file d’attente. Les commandes sont transmises au processus principal via un named pipe.

## Compilation du projet

Le SDK .NET 10 est requis.

```powershell
dotnet restore WinCopyQueue.slnx --configfile NuGet.Config
dotnet build WinCopyQueue.slnx --no-restore -c Release
```

Lancer l’application depuis le dépôt :

```powershell
dotnet run --project src\WinCopyQueue.App\WinCopyQueue.App.csproj --no-restore
```

### Tests

```powershell
dotnet run --project tests\WinCopyQueue.Core.SmokeTests\WinCopyQueue.Core.SmokeTests.csproj --no-build -c Release
dotnet run --project tests\WinCopyQueue.App.SmokeTests\WinCopyQueue.App.SmokeTests.csproj --no-build -c Release
```

Les smoke tests du cœur effectuent de vraies opérations sur des fichiers temporaires isolés et vérifient notamment l’ordre des sessions, les conflits, SHA-256, la pause/reprise, l’annulation, le nettoyage de l’historique et les contrôles par fichier. Les tests de l’application couvrent WPF, la localisation, la boîte de dialogue de conflit et les scénarios d’arrêt.

### Installateur

Construire l’installateur avec :

```powershell
.\installer\Build-Installer.ps1
```

Le script publie une version autonome `win-x64` et crée un installateur avec Inno Setup 7. Les binaires prêts à l’emploi sont publiés dans [Releases](https://github.com/quendae/WinCopyQueue/releases) et ne sont pas stockés dans le dépôt.

## Structure du projet

```text
src/WinCopyQueue.Core/       logique de file d’attente et opérations sur les fichiers
src/WinCopyQueue.App/        application WPF, zone de notification et intégration Explorer
tests/                       smoke tests du cœur et de l’application
installer/                   définition Inno Setup et script de compilation
```
