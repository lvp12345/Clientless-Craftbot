# Craftbot Clientless Launcher

This launcher application loads and runs the Craftbot plugin in clientless mode using the AOSharp.Clientless framework.

## Setup

1. **Build the Craftbot plugin** first to generate `Craftbot.dll`
2. **Copy required files** to the launcher directory:
   - `Craftbot.dll` (from Craftbot build output)
   - All AOSharp.Clientless dependencies
3. **Create configuration** (see Configuration section below)

## Usage

### Command Line
```bash
CraftbotLauncher.exe <username> <password> <characterName> [dimension]
```

Example:
```bash
CraftbotLauncher.exe myuser mypass MyCharacter RubiKa2019
```

### Configuration File
1. Copy `config.example.json` to `config.json`
2. Edit with your credentials:
```json
{
  "Username": "your_username",
  "Password": "your_password", 
  "CharacterName": "YourCharacterName",
  "Dimension": "RubiKa2019"
}
```
3. Run: `CraftbotLauncher.exe`

## Available Dimensions
- `RubiKa` (Live server)
- `RubiKa2019` (2019 server)
- `Test` (Test server)

## Controls
- **Any key**: Show status
- **Q**: Quit application

## Logs
Logs are written to:
- Console (real-time)
- `logs/craftbot-YYYY-MM-DD.log` (daily rolling files)

## Troubleshooting

### Plugin Not Found
Ensure `Craftbot.dll` is in the same directory as the launcher executable.

### Connection Issues
- Verify credentials are correct
- Check dimension name spelling
- Ensure internet connection is stable

### Missing Dependencies
Make sure all AOSharp.Clientless DLLs are present:
- AOSharp.Clientless.dll
- AOSharp.Common.dll
- Serilog.dll
- Newtonsoft.Json.dll
- And other dependencies

## Security Note
Keep your `config.json` file secure and never commit it to version control with real credentials.
