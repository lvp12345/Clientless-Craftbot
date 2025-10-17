using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Craftbot.Core
{
    /// <summary>
    /// Central configuration manager with hot reload capabilities
    /// Manages all configuration files in the config/ directory
    /// </summary>
    public static class ConfigurationManager
    {
        private static readonly string ConfigDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config");
        private static readonly Dictionary<string, FileSystemWatcher> _watchers = new Dictionary<string, FileSystemWatcher>();
        private static readonly Dictionary<string, object> _configurations = new Dictionary<string, object>();
        private static bool _initialized = false;

        public static event Action<string, object> ConfigurationChanged;

        /// <summary>
        /// Initialize the configuration system and set up file watchers
        /// </summary>
        public static void Initialize()
        {
            try
            {
                LogDebug("[CONFIG] Initializing Configuration Manager...");

                // Create config directory if it doesn't exist
                if (!Directory.Exists(ConfigDirectory))
                {
                    Directory.CreateDirectory(ConfigDirectory);
                    LogDebug($"[CONFIG] Created config directory: {ConfigDirectory}");
                }

                // Create default configuration files if they don't exist
                CreateDefaultConfigurations();

                // Load all existing configurations
                LoadAllConfigurations();

                // Set up file watchers for hot reload
                SetupFileWatchers();

                _initialized = true;
                LogDebug("[CONFIG] Configuration Manager initialized successfully");
            }
            catch (Exception ex)
            {
                LogDebug($"[CONFIG] Error initializing Configuration Manager: {ex.Message}");
            }
        }

        /// <summary>
        /// Get a configuration object of the specified type
        /// </summary>
        public static T GetConfiguration<T>(string configName) where T : class
        {
            if (!_initialized)
            {
                LogDebug("[CONFIG] Warning: Configuration Manager not initialized, initializing now...");
                Initialize();
            }

            if (_configurations.TryGetValue(configName, out var config))
            {
                return config as T;
            }

            LogDebug($"[CONFIG] Configuration '{configName}' not found");
            return null;
        }

        /// <summary>
        /// Reload a specific configuration file
        /// </summary>
        public static async Task ReloadConfiguration(string configName)
        {
            try
            {
                var filePath = Path.Combine(ConfigDirectory, $"{configName}.json");
                if (!File.Exists(filePath))
                {
                    LogDebug($"[CONFIG] Configuration file not found: {filePath}");
                    return;
                }

                LogDebug($"[CONFIG] Reloading configuration: {configName}");

                // Add a small delay to ensure file write is complete
                await Task.Delay(100);

                var json = File.ReadAllText(filePath);
                var configType = GetConfigurationType(configName);
                
                if (configType != null)
                {
                    var config = JsonConvert.DeserializeObject(json, configType);
                    _configurations[configName] = config;
                    
                    LogDebug($"[CONFIG] Successfully reloaded configuration: {configName}");
                    
                    // Notify listeners of configuration change
                    ConfigurationChanged?.Invoke(configName, config);
                }
                else
                {
                    LogDebug($"[CONFIG] Unknown configuration type for: {configName}");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[CONFIG] Error reloading configuration '{configName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Save a configuration object to file
        /// </summary>
        public static Task SaveConfiguration<T>(string configName, T configuration) where T : class
        {
            try
            {
                var filePath = Path.Combine(ConfigDirectory, $"{configName}.json");
                var json = JsonConvert.SerializeObject(configuration, Formatting.Indented);

                File.WriteAllText(filePath, json);
                _configurations[configName] = configuration;

                LogDebug($"[CONFIG] Saved configuration: {configName}");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                LogDebug($"[CONFIG] Error saving configuration '{configName}': {ex.Message}");
                return Task.CompletedTask;
            }
        }

        private static void CreateDefaultConfigurations()
        {
            // Create default message configuration
            var messagesPath = Path.Combine(ConfigDirectory, "messages.json");
            if (!File.Exists(messagesPath))
            {
                var defaultMessages = new MessageConfiguration();
                var json = JsonConvert.SerializeObject(defaultMessages, Formatting.Indented);
                File.WriteAllText(messagesPath, json);
                LogDebug("[CONFIG] Created default messages.json");
            }

            // Create default recipe configuration
            var recipesPath = Path.Combine(ConfigDirectory, "recipes.json");
            if (!File.Exists(recipesPath))
            {
                var defaultRecipes = new RecipeConfiguration();
                var json = JsonConvert.SerializeObject(defaultRecipes, Formatting.Indented);
                File.WriteAllText(recipesPath, json);
                LogDebug("[CONFIG] Created default recipes.json");
            }

            // Create default command configuration
            var commandsPath = Path.Combine(ConfigDirectory, "commands.json");
            if (!File.Exists(commandsPath))
            {
                var defaultCommands = new CommandConfiguration();
                var json = JsonConvert.SerializeObject(defaultCommands, Formatting.Indented);
                File.WriteAllText(commandsPath, json);
                LogDebug("[CONFIG] Created default commands.json");
            }
        }

        private static void LoadAllConfigurations()
        {
            var configFiles = Directory.GetFiles(ConfigDirectory, "*.json");
            foreach (var filePath in configFiles)
            {
                var configName = Path.GetFileNameWithoutExtension(filePath);
                var task = ReloadConfiguration(configName);
                task.Wait(); // Synchronous load during initialization
            }
        }

        private static void SetupFileWatchers()
        {
            var configFiles = Directory.GetFiles(ConfigDirectory, "*.json");
            foreach (var filePath in configFiles)
            {
                var configName = Path.GetFileNameWithoutExtension(filePath);
                SetupFileWatcher(configName);
            }
        }

        private static void SetupFileWatcher(string configName)
        {
            try
            {
                var watcher = new FileSystemWatcher(ConfigDirectory, $"{configName}.json")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };

                watcher.Changed += async (sender, e) =>
                {
                    LogDebug($"[CONFIG] Detected change in {configName}.json, reloading...");
                    await ReloadConfiguration(configName);
                };

                _watchers[configName] = watcher;
                LogDebug($"[CONFIG] Set up file watcher for {configName}.json");
            }
            catch (Exception ex)
            {
                LogDebug($"[CONFIG] Error setting up file watcher for {configName}: {ex.Message}");
            }
        }

        private static Type GetConfigurationType(string configName)
        {
            switch (configName.ToLower())
            {
                case "messages":
                    return typeof(MessageConfiguration);
                case "recipes":
                    return typeof(RecipeConfiguration);
                case "commands":
                    return typeof(CommandConfiguration);
                default:
                    return null;
            }
        }

        private static void LogDebug(string message)
        {
            // Use the existing logging system
            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [DEBUG] {message}");
        }

        /// <summary>
        /// Cleanup resources when shutting down
        /// </summary>
        public static void Shutdown()
        {
            foreach (var watcher in _watchers.Values)
            {
                watcher?.Dispose();
            }
            _watchers.Clear();
            LogDebug("[CONFIG] Configuration Manager shut down");
        }
    }
}
