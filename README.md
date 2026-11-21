# VrcPresence – VRChat Rich Presence for Discord

Displays the world, status (Join Me / Do Not Disturb, etc.), instance type, player count
(for example, 23/32), world image, and a “View World” button in Discord.

## Setup

### Discord application (optional)
The included `config.json` already contains an Application ID, so you can run the app without creating
your own Discord application.

If you prefer to use your own application:
1. Open the [Discord Developer Portal](https://discord.com/developers/applications) and create an application.
2. Copy its **Application ID** into `discordAppId` in `config.json`.
3. In the application, open **Rich Presence → Art Assets** and upload `assets/vrchat.png`.
   Use the asset name **`vrchat`** (without `.png`), matching `largeImageKey` in the configuration.
   This image is used as the Rich Presence image in private instances so that no world image is shown.

### Start
1. Make sure the Discord desktop app and VRChat are running.
2. Start `publish\VrcPresence.exe`. On the first launch, the program may ask whether you want to log in
   to VRChat. This is optional but enables VRChat status, world images, and maximum player counts.
3. To create the portable Windows version, install the **.NET 8 SDK** and run `build.bat`.
   The finished application is located at `publish\VrcPresence.exe`. Close any running
   VrcPresence instance before publishing it again.

### Example views

Public instance:

![Example Rich Presence in a public instance](assets/publicinstance.png)

Private instance: The world name and world image are hidden. Instead, the `vrchat` asset uploaded above
is shown.

![Example Rich Presence in a private instance](assets/privateinstance.png)

`assets/vrchat.png` is the image file that must be uploaded as the `vrchat` asset in the Discord application:

<img src="assets/vrchat.png" alt="VRChat asset for private instances" width="160">

## Settings
All settings are in `config.json`, next to the executable file. Publishing copies the file to
`publish`. Restart the tool after making changes.

