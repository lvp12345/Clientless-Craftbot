using System.Collections.Generic;

namespace Craftbot.Core
{
    /// <summary>
    /// Configuration for chat commands - allows adding new commands without code changes
    /// </summary>
    public class CommandConfiguration
    {
        public List<ConfigurableCommand> Commands { get; set; } = new List<ConfigurableCommand>();
        public CommandSettings Settings { get; set; } = new CommandSettings();

        public CommandConfiguration()
        {
            // Add default commands
            InitializeDefaultCommands();
        }

        private void InitializeDefaultCommands()
        {
            Commands.AddRange(new[]
            {

                new ConfigurableCommand
                {
                    Name = "recipes",
                    Aliases = new List<string> { "recipe", "r" },
                    Description = "List available recipes",
                    Response = "Available recipes: {recipeList}. Trade me items to process them!",
                    Enabled = true,
                    RequiresParameters = false,
                    Category = "Recipes",
                    Cooldown = 5,
                    Rank = "Everyone"
                },
                new ConfigurableCommand
                {
                    Name = "status",
                    Aliases = new List<string> { "stat", "s" },
                    Description = "Show bot status",
                    Response = "Bot Status: Online | Processed: {processedCount} items | Uptime: {uptime}",
                    Enabled = true,
                    RequiresParameters = false,
                    Category = "General",
                    Cooldown = 10,
                    Rank = "Everyone"
                },
                new ConfigurableCommand
                {
                    Name = "robotbrain",
                    Aliases = new List<string> { "robot", "brain", "rb" },
                    Description = "Show Robot Brain recipe help",
                    Response = "Robot Brain recipe: Screwdriver + Robot Junk = Nano Sensor, Bio Analyzing Computer + Nano Sensor = Basic Robot Brain, MasterComm - Personalization Device + Basic Robot Brain = Personalized Basic Robot Brain. Supports multiple sets!",
                    Enabled = true,
                    RequiresParameters = false,
                    Category = "Recipes",
                    Cooldown = 0,
                    Rank = "Everyone"
                },
                new ConfigurableCommand
                {
                    Name = "plasma",
                    Aliases = new List<string> { "blood" },
                    Description = "Show Plasma recipe help",
                    Response = "Plasma recipe: Process various Blood Plasma items. Just trade me your Blood Plasma items!",
                    Enabled = true,
                    RequiresParameters = false,
                    Category = "Recipes",
                    Cooldown = 0,
                    Rank = "Everyone"
                },
                new ConfigurableCommand
                {
                    Name = "reload",
                    Aliases = new List<string> { "refresh" },
                    Description = "Reload configuration files (Admin only)",
                    Response = "Configuration reloaded successfully!",
                    Enabled = true,
                    RequiresParameters = false,
                    Category = "Admin",
                    Cooldown = 0,
                    Rank = "Admin"
                },
                new ConfigurableCommand
                {
                    Name = "version",
                    Aliases = new List<string> { "ver", "v" },
                    Description = "Show bot version",
                    Response = "Craftbot v2.0 - Configuration-based hot-reload system enabled!",
                    Enabled = true,
                    RequiresParameters = false,
                    Category = "General",
                    Cooldown = 30,
                    Rank = "Everyone"
                },
                new ConfigurableCommand
                {
                    Name = "uptime",
                    Aliases = new List<string> { "up" },
                    Description = "Show bot uptime",
                    Response = "Bot uptime: {uptime}",
                    Enabled = true,
                    RequiresParameters = false,
                    Category = "General",
                    Cooldown = 10,
                    Rank = "Everyone"
                },
                new ConfigurableCommand
                {
                    Name = "clean",
                    Aliases = new List<string> { "implantclean", "ic" },
                    Description = "Clean implants using Implant Disassembly Clinic",
                    Response = "Opening trade to clean your implants. Please add the implants you want cleaned!",
                    Enabled = true,
                    RequiresParameters = false,
                    Category = "Recipes",
                    Cooldown = 5,
                    Rank = "Everyone",
                    ActionType = "action"
                }
            });
        }
    }

    public class ConfigurableCommand
    {
        public string Name { get; set; }
        public List<string> Aliases { get; set; } = new List<string>();
        public string Description { get; set; }
        public string Response { get; set; }
        public bool Enabled { get; set; } = true;
        public bool RequiresParameters { get; set; } = false;
        public string Category { get; set; } = "General";
        public int Cooldown { get; set; } = 0; // seconds
        public string Rank { get; set; } = "Everyone"; // "Everyone", "Admin", "Moderator", "VIP", "User"
        public List<string> RequiredPermissions { get; set; } = new List<string>();
        public Dictionary<string, object> CustomProperties { get; set; } = new Dictionary<string, object>();
        public string ActionType { get; set; } = "Response"; // "Response", "Action", "Custom"
        public string CustomAction { get; set; } // For custom command actions
    }

    public class CommandSettings
    {
        public bool EnableHotReload { get; set; } = true;
        public bool CaseSensitive { get; set; } = false;
        public string CommandPrefix { get; set; } = ""; // Empty means no prefix required
        public bool LogCommandUsage { get; set; } = true;
        public int GlobalCooldown { get; set; } = 1; // seconds between any commands from same user
        public bool AllowCustomCommands { get; set; } = true;
        public List<string> AdminUsers { get; set; } = new List<string>();
        public bool EnableAliases { get; set; } = true;
        public int MaxCommandsPerMinute { get; set; } = 10;
        public bool EnableFunnyResponses { get; set; } = true;
    }
}
