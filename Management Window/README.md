# Craftbot Management Window

A comprehensive Python GUI application for managing Craftbot recipes, commands, ranks, and logs.

## Folder Structure

```
Management Window/
├── README.md                    # This file
├── src/
│   └── craftbot_management_window.py    # Main application
├── docs/
│   ├── MANAGEMENT_WINDOW_README.md      # Detailed user guide
│   ├── QUICK_START_GUIDE.md             # Quick reference
│   ├── CRAFTBOT_MANAGEMENT_WINDOW_IMPLEMENTATION.md  # Technical details
│   └── IMPLEMENTATION_COMPLETE.md       # Implementation summary
└── config/
    └── (reserved for future config files)
```

## Quick Start

### Automatic Launch
```bash
CraftbotLauncher.exe
```
The management window opens automatically when you start the launcher.

### Manual Launch
```bash
python "Management Window/src/craftbot_management_window.py"
```

## Features

### 📋 Recipes Tab
- View all recipes from `config/recipes/`
- Edit recipe JSON directly
- Save changes with validation

### 🎮 Commands Tab
- View all commands from `config/commands.json`
- Edit command properties
- Save to commands.json

### 👥 Ranks Tab
- Manage players by rank (Admin, Moderator, VIP, User)
- Add/Remove players
- Create/Delete ranks
- Persistent JSON storage

### 📝 Logs Tab
- View all log files from `bin/Debug/`
- Edit log content
- Save changes
- Sorted by date

## Documentation

- **MANAGEMENT_WINDOW_README.md** - Complete user guide with all features
- **QUICK_START_GUIDE.md** - Quick reference and common tasks
- **CRAFTBOT_MANAGEMENT_WINDOW_IMPLEMENTATION.md** - Technical implementation details
- **IMPLEMENTATION_COMPLETE.md** - Implementation summary and status

## Requirements

- Python 3.6+
- tkinter (included with Python)
- No external dependencies

## File Locations

- **Recipes**: `bin/Debug/Control Panel/config/recipes/`
- **Commands**: `bin/Debug/Control Panel/config/commands.json`
- **Ranks**: `bin/Debug/Control Panel/config/ranks/`
- **Logs**: `bin/Debug/Control Panel/bin/Debug/`

## Integration

The management window is automatically launched by `CraftbotLauncher.exe` and runs in parallel with Craftbot. It searches for the Python script in multiple locations:

1. `Management Window/src/craftbot_management_window.py`
2. Parent directories
3. Launcher directory

## Troubleshooting

### Management window doesn't launch
- Ensure Python is installed: `python --version`
- Check Python is in system PATH
- Verify script exists in `Management Window/src/`
- Try manual launch

### JSON validation errors
- Ensure JSON is properly formatted
- Check for missing commas/quotes
- Use online JSON validator

### Rank files not found
- Ensure `bin/Debug/Control Panel/config/ranks/` exists
- Default ranks created automatically on first run

## Support

For detailed information, see the documentation files in the `docs/` folder.

---

**Status**: ✅ Complete and integrated with CraftbotLauncher

