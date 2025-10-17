using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Craftbot
{
    /// <summary>
    /// Legacy configuration class for backward compatibility
    /// Loads character-specific configuration from JSON file
    /// </summary>
    public class Config
    {
        public string CharacterName { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public Dictionary<string, CharacterSettings> CharSettings { get; set; } = new Dictionary<string, CharacterSettings>();

        private static string _configFilePath;

        public class CharacterSettings
        {
            public List<string> AuthorizedUsers { get; set; } = new List<string>();
            public Dictionary<string, bool> ModuleSettings { get; set; } = new Dictionary<string, bool>();
        }

        /// <summary>
        /// Load configuration from JSON file
        /// </summary>
        public static Config Load(string filePath)
        {
            try
            {
                _configFilePath = filePath;

                // Create directory if it doesn't exist
                string directory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Load from file if it exists
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    return JsonConvert.DeserializeObject<Config>(json) ?? new Config();
                }

                // Create default config if file doesn't exist
                var defaultConfig = new Config
                {
                    CharacterName = "DefaultCharacter",
                    Username = "DefaultUser",
                    Password = ""
                };

                // Save default config
                string defaultJson = JsonConvert.SerializeObject(defaultConfig, Formatting.Indented);
                File.WriteAllText(filePath, defaultJson);

                return defaultConfig;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading config from {filePath}: {ex.Message}");
                return new Config();
            }
        }

        /// <summary>
        /// Save configuration to JSON file (uses path from Load)
        /// </summary>
        public void Save()
        {
            if (string.IsNullOrEmpty(_configFilePath))
            {
                Console.WriteLine("Error: Config file path not set. Call Load() first.");
                return;
            }

            try
            {
                string directory = Path.GetDirectoryName(_configFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(_configFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving config to {_configFilePath}: {ex.Message}");
            }
        }
    }
}

