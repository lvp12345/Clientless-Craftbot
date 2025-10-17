using System;
using System.IO;
using System.Diagnostics;
using AOSharp.Clientless;
using AOSharp.Clientless.Common;
using AOSharp.Common.GameData;
using Serilog;

namespace CraftbotLauncher
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== Craftbot Clientless Launcher ===");
            Console.WriteLine("Starting Craftbot in clientless mode...");

            // Create logs folder on startup - use the directory where this exe is located
            string exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string logsDir = Path.Combine(exeDir, "logs");
            Directory.CreateDirectory(logsDir);
            Console.WriteLine($"Logs folder created at: {logsDir}");

            // Setup logging
            var logger = new LoggerConfiguration()
                .WriteTo.Console()
                .MinimumLevel.Debug()
                .CreateLogger();

            try
            {
                // Launch the Python management window
                LaunchManagementWindow();

                // Get credentials from command line or config
                string username, password, characterName;
                Dimension dimension = Dimension.RubiKa2019;

                if (args.Length >= 3)
                {
                    username = args[0];
                    password = args[1];
                    characterName = args[2];

                    if (args.Length >= 4 && Enum.TryParse<Dimension>(args[3], out var dim))
                    {
                        dimension = dim;
                    }
                }
                else
                {
                    // Try to load from config file
                    var config = LoadConfig();
                    if (config != null)
                    {
                        username = config.Username;
                        password = config.Password;
                        characterName = config.CharacterName;
                        dimension = config.Dimension;
                    }
                    else
                    {
                        Console.WriteLine("Usage: CraftbotLauncher.exe <username> <password> <characterName> [dimension]");
                        Console.WriteLine("Or create a launcher-config.json file with credentials");
                        Console.WriteLine("Press any key to exit...");
                        Console.ReadKey();
                        return;
                    }
                }

                logger.Information($"Launching Craftbot for character: {characterName} on {dimension}");

                // Create clientless domain
                var clientDomain = Client.CreateInstance(username, password, characterName, dimension, logger);

                // Load the Craftbot plugin
                string pluginPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Craftbot.dll");
                if (!File.Exists(pluginPath))
                {
                    logger.Error($"Craftbot plugin not found at: {pluginPath}");
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey();
                    return;
                }

                logger.Information($"Loading Craftbot plugin from: {pluginPath}");
                clientDomain.LoadPlugin(pluginPath);

                // Start the client
                logger.Information("Starting clientless client...");
                clientDomain.Start();

                Console.WriteLine("Craftbot is now running in clientless mode!");
                Console.WriteLine("Press 'q' to quit or any other key for status...");

                // Main loop
                while (true)
                {
                    var key = Console.ReadKey(true);
                    if (key.KeyChar == 'q' || key.KeyChar == 'Q')
                    {
                        break;
                    }
                    else
                    {
                        Console.WriteLine($"Status: Running - Character: {characterName}");
                        Console.WriteLine("Press 'q' to quit...");
                    }
                }

                logger.Information("Shutting down Craftbot...");
                clientDomain.Unload();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Fatal error in Craftbot launcher");
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
            finally
            {
                logger.Dispose();
            }
        }

        static void LaunchManagementWindow()
        {
            try
            {
                // Try to launch using start.bat first (most reliable)
                string[] possibleBatPaths = new[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Management Window", "start.bat"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Management Window", "start.bat"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Management Window", "start.bat")
                };

                string batScript = null;
                foreach (var path in possibleBatPaths)
                {
                    if (File.Exists(path))
                    {
                        batScript = path;
                        break;
                    }
                }

                if (batScript != null)
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = batScript,
                        UseShellExecute = true,
                        CreateNoWindow = false
                    };

                    Process.Start(psi);
                    Console.WriteLine("Management window launched successfully!");
                    return;
                }

                // Fallback: Try to launch Python script directly
                string[] possiblePythonPaths = new[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Management Window", "src", "craftbot_management_window.py"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Management Window", "src", "craftbot_management_window.py"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Management Window", "src", "craftbot_management_window.py"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "craftbot_management_window.py")
                };

                string pythonScript = null;
                foreach (var path in possiblePythonPaths)
                {
                    if (File.Exists(path))
                    {
                        pythonScript = path;
                        break;
                    }
                }

                if (pythonScript != null)
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = "python",
                        Arguments = $"\"{pythonScript}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    Process.Start(psi);
                    Console.WriteLine("Management window launched successfully!");
                }
                else
                {
                    Console.WriteLine("Warning: Management window launcher not found. Management window will not be available.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to launch management window: {ex.Message}");
            }
        }

        private static LauncherConfig LoadConfig()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launcher-config.json");

                // Create default config if it doesn't exist
                if (!File.Exists(configPath))
                {
                    CreateDefaultLauncherConfig(configPath);
                }

                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    return Newtonsoft.Json.JsonConvert.DeserializeObject<LauncherConfig>(json);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading config: {ex.Message}");
            }
            return null;
        }

        private static void CreateDefaultLauncherConfig(string configPath)
        {
            try
            {
                // Create config with comments as a formatted string
                string configContent = @"{
  // Your Anarchy Online username
  ""Username"": ""your_username"",

  // Your Anarchy Online password
  ""Password"": ""your_password"",

  // Your character name
  ""CharacterName"": ""your_character_name"",

  // Available dimensions:
  //   - RubiKa (Live server)
  //   - RubiKa2019 (2019 server)
  //   - Test (Test server)
  ""Dimension"": ""RubiKa2019""
}";

                File.WriteAllText(configPath, configContent);

                Console.WriteLine($"Created default launcher config at: {configPath}");
                Console.WriteLine("Please edit launcher-config.json with your credentials before running again.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating default config: {ex.Message}");
            }
        }
    }

    public class LauncherConfig
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string CharacterName { get; set; }
        public Dimension Dimension { get; set; } = Dimension.RubiKa2019;
    }
}
