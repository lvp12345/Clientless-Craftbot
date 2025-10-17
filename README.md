# Craftbot

An all-in-one automation plugin for Anarchy Online that provides crafting automation, team invitations, and remote command execution.

## Overview

Craftbot is a comprehensive plugin that automates various crafting recipes and provides utility functions for Anarchy Online. It includes a management window for easy configuration and supports remote control via private messages.

## File Structure

```
Craftbot/
├── Craftbot.cs                    # Main plugin entry point
├── Config.cs                      # Configuration management
├── Core/                          # Core systems (messaging, trading, recipes)
├── Recipes/                       # Recipe processors for crafting automation
├── Modules/                       # Plugin modules (PrivateMessageModule)
├── CraftbotLauncher/              # Clientless launcher application
├── Management Window/             # Python GUI for management
├── GameData/                      # Game data files (items, playfields, etc.)
├── Templates/                     # Help message templates
├── Page 1-5.png                   # UI documentation screenshots
└── Craftbot.sln                   # Visual Studio solution file
```

## Building

### Prerequisites
- Visual Studio 2019 or later
- .NET Framework 4.8
- AOSharp.Clientless source code in `../aosharp.clientless/`

### Build Steps

1. Open `Craftbot.sln` in Visual Studio
2. Build the solution (Ctrl+Shift+B)
3. Output files are generated to `bin/Debug/Control Panel/`

The build process automatically:
- Compiles the C# plugin
- Copies the Python management window UI
- Copies the CraftbotLauncher executable

## Running

### As a Plugin
Run from the `bin/Debug/Control Panel/` directory:
```bash
CraftbotLauncher.exe <username> <password> <characterName> [dimension]
```

Example:
```bash
CraftbotLauncher.exe myuser mypass MyCharacter RubiKa2019
```

### Configuration
Create `config.json` in the launcher directory (copy from `config.example.json`):
```json
{
  "Username": "your_username",
  "Password": "your_password",
  "CharacterName": "YourCharacterName",
  "Dimension": "RubiKa2019"
}
```

Then run: `CraftbotLauncher.exe`

## Features

- **Team Invitations**: `/team invite <playername>` or `/t invite <playerid>`
- **Crafting Automation**: Automates 30+ recipes including pearls, plasma, armor, weapons, and more
- **Remote Control**: Execute commands via private messages from authorized users
- **Management Window**: Python-based GUI for easy configuration
- **Modular Design**: Enable/disable individual features as needed

## Documentation

See the included screenshots for UI documentation:
- **Page 1.png** - Main interface overview
- **Page 2.png** - Recipe configuration
- **Page 3.png** - Command settings
- **Page 4.png** - Rank management
- **Page 5.png** - Log viewer

For detailed launcher information, see `CraftbotLauncher/README.md`
