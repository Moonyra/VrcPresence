# VrcPresence – VRChat Rich Presence für Discord

Zeigt in Discord Welt, Status (Join Me / Do Not Disturb …), Instanztyp, Spielerzahl (z. B. 23/32),
Welt-Bild und einen „Welt ansehen“-Button an.

## Setup

### Discord-Anwendung einrichten
1. Öffne das [Discord Developer Portal](https://discord.com/developers/applications) und erstelle eine Anwendung.
2. Kopiere ihre **Application ID** in `discordAppId` in der `config.json`.
3. Öffne in der Anwendung **Rich Presence → Art Assets** und lade `assets/vrchat.png` hoch.
   Verwende den Asset-Namen **`vrchat`** (ohne `.png`), passend zu `largeImageKey` in der Konfiguration.
   Dieses Bild wird als Rich-Presence-Bild in privaten Instanzen verwendet, damit dort kein Weltbild
   angezeigt wird.

### Starten
1. Stelle sicher, dass die Discord-Desktop-App und VRChat laufen.
2. Starte `publish\VrcPresence.exe`. Beim ersten Start kann das Programm nach einer VRChat-Anmeldung
   fragen. Diese ist optional; sie ermöglicht VRChat-Status, Weltbilder und maximale Spielerzahlen.
3. Zum Erstellen der portablen Windows-Version installiere das **.NET 8 SDK** und starte `build.bat`.
   Die fertige Anwendung liegt in `publish\VrcPresence.exe`. Schließe eine bereits laufende
   VrcPresence-Instanz vor dem erneuten Veröffentlichen.

### Beispielansichten

Öffentliche Instanz:

![Beispiel einer Rich Presence in einer öffentlichen Instanz](assets/publicinstance.png)

Private Instanz: Weltname und Weltbild werden ausgeblendet; stattdessen wird das oben hochgeladene
`vrchat`-Asset angezeigt.

![Beispiel einer Rich Presence in einer privaten Instanz](assets/privateinstance.png)

`assets/vrchat.png` ist die Bilddatei, die als `vrchat`-Asset in der Discord-Anwendung hochgeladen
werden muss:

![VRChat-Asset für private Instanzen](assets/vrchat.png)

## Einstellungen
Alles Einstellbare steht in `config.json` neben der ausführbaren Datei. Beim Veröffentlichen wird
die Datei nach `publish` kopiert. Nach Änderungen: Tool neu starten.

## Logs
Die Anwendung schreibt Laufzeitmeldungen und Fehler nach `%LOCALAPPDATA%\VrcPresence\VrcPresence.log`.

## Dateien
| Datei | Inhalt |
|---|---|
| `assets/vrchat.png` | Discord-Rich-Presence-Asset für private Instanzen |
| `assets/publicinstance.png` | Beispielansicht einer öffentlichen Instanz |
| `assets/privateinstance.png` | Beispielansicht einer privaten Instanz |
| `Program.cs` | Startpunkt, Hauptschleife |
| `PresenceBuilder.cs` | Baut den Discord-Text (Welt, Status, Spieler, Button, Privatsphäre) |
| `VrcLogReader.cs` | Liest Welt, Instanz und Spielerzahl aus der VRChat-Logdatei |
| `VrcApi.cs` | VRChat-Login, Status, Welt-Bild, max. Spielerzahl |
| `Ui.cs` | Live-Anzeige in der Konsole |
| `Settings.cs` | Alle Einstellungen und ihre Standardwerte (`config.json`) |
| `Models.cs` | Kleine Datenklassen |
| `GlobalUsings.cs` | Gemeinsame `using`-Anweisungen |
