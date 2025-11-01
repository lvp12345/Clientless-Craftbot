using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AOSharp.Clientless;

namespace Craftbot.Core
{
    /// <summary>
    /// Configurable help system that loads help templates from config/help-templates/ folder
    /// Supports hot reload when help templates are added, modified, or removed
    /// </summary>
    public static class ConfigurableHelpSystem
    {
        private static readonly string HelpTemplatesDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Control Panel", "Templates");
        private static readonly Dictionary<string, FileSystemWatcher> _watchers = new Dictionary<string, FileSystemWatcher>();
        private static readonly Dictionary<string, string> _loadedTemplates = new Dictionary<string, string>();
        private static bool _initialized = false;

        public static event Action<string, string> TemplateAdded;
        public static event Action<string, string> TemplateUpdated;
        public static event Action<string> TemplateRemoved;

        /// <summary>
        /// Initialize the configurable help system
        /// </summary>
        public static void Initialize()
        {
            try
            {
                LogDebug("[HELP SYSTEM] Initializing Configurable Help System...");

                // Create help templates directory if it doesn't exist
                if (!Directory.Exists(HelpTemplatesDirectory))
                {
                    Directory.CreateDirectory(HelpTemplatesDirectory);
                    LogDebug($"[HELP SYSTEM] Created help templates directory: {HelpTemplatesDirectory}");
                    
                    // Create default templates
                    CreateDefaultTemplates();
                }

                // Load all existing template files
                LoadAllTemplates();

                // Set up file watchers for hot reload
                SetupFileWatchers();

                _initialized = true;
                LogDebug($"[HELP SYSTEM] Initialized successfully with {_loadedTemplates.Count} templates");
            }
            catch (Exception ex)
            {
                LogDebug($"[HELP SYSTEM] Error initializing: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the main help window content
        /// </summary>
        public static string GetMainHelpWindow()
        {
            EnsureInitialized();
            
            var template = GetTemplate("main");
            if (!string.IsNullOrEmpty(template))
            {
                return template.Replace("{{ botname }}", Client.CharacterName);
            }
            
            return CreateFallbackMainHelp();
        }

        /// <summary>
        /// Get a category help window content
        /// </summary>
        public static string GetCategoryHelpWindow(string category)
        {
            EnsureInitialized();

            LogDebug($"[HELP SYSTEM] Looking for category template: {category}");
            LogDebug($"[HELP SYSTEM] Available templates: {string.Join(", ", _loadedTemplates.Keys)}");

            var template = GetTemplate(category.ToLower());
            if (!string.IsNullOrEmpty(template))
            {
                LogDebug($"[HELP SYSTEM] Found template for category: {category}");
                return template.Replace("{{ botname }}", Client.CharacterName);
            }

            LogDebug($"[HELP SYSTEM] No template found for category: {category}");
            return null;
        }

        /// <summary>
        /// Get a template by name
        /// </summary>
        public static string GetTemplate(string templateName)
        {
            EnsureInitialized();
            
            if (_loadedTemplates.TryGetValue(templateName.ToLower(), out var template))
            {
                return template;
            }
            
            return null;
        }

        /// <summary>
        /// Get all available template names
        /// </summary>
        public static List<string> GetAvailableTemplates()
        {
            EnsureInitialized();
            return _loadedTemplates.Keys.ToList();
        }

        /// <summary>
        /// Check if a template exists
        /// </summary>
        public static bool HasTemplate(string templateName)
        {
            EnsureInitialized();
            return _loadedTemplates.ContainsKey(templateName.ToLower());
        }

        private static void LoadAllTemplates()
        {
            var templateFiles = Directory.GetFiles(HelpTemplatesDirectory, "*.txt");
            foreach (var filePath in templateFiles)
            {
                LoadTemplateFile(filePath);
            }
        }

        private static string MapFileNameToTemplateKey(string fileName)
        {
            // Map template file names to expected keys
            switch (fileName)
            {
                case "craftbothelptemplate": return "main";
                case "gemcutterhelptemplate": return "gemcutter";
                case "smeltinghelptemplate": return "smelting";
                case "plasmahelptemplate": return "plasma";
                case "pitdemonhearthelptemplate": return "pitdemon";
                case "fredericksonsleevedhelptemplate": return "frederickson";
                case "nanocrystalrepairhelptemplate": return "nanocrystal";
                case "nanocrystalcreationhelptemplate": return "nanocrystal-creation";
                case "icehelptemplate": return "ice";
                case "robotjunkhelptemplate": return "robotjunk";
                case "vtehelptemplate": return "vte";
                case "pbpatternhelptemplate": return "pbpattern";
                case "ringofpowerhelptemplate": return "ringofpower";
                case "clumpshelptemplate": return "clumps";
                case "alienarmorhelptemplate": return "alienarmor";
                case "taraarmorhelptemplate": return "taraarmor";
                case "mantisarmorhelptemplate": return "mantisarmor";
                case "crawlerhelptemplate": return "crawler";
                case "carbarmorhelptemplate": return "carbarmor";
                case "implantshelptemplate": return "implants";
                case "trimmerhelptemplate": return "trimmer";
                case "devalossleevehelptemplate": return "devalossleeve";
                case "perenniumweaponshelptemplate": return "perenniumweapons";
                case "hackgraftshelptemplate": return "hackgrafts";
                case "sealedweaponhelptemplate": return "sealedweapon";
                case "aibiotechrodringshelptemplate": return "aibiotechrodrings";
                case "stalkerhelmethelptemplate": return "stalkerhelmet";
                case "barterarmorhelptemplate": return "barterarmor";
                case "niznosbombblasterhelptemplate": return "niznosbombblaster";
                case "salabimshotgunhelptemplate": return "salabimshotgun";
                case "crepusculeleatherarmorhelptemplate": return "crepusculeleatherarmor";
                case "treatmentlibraryhelptemplate": return "treatmentlibrary";
                default: return fileName; // Return as-is for any other files
            }
        }

        private static void LoadTemplateFile(string filePath)
        {
            try
            {
                var fileName = Path.GetFileNameWithoutExtension(filePath).ToLower();
                var templateKey = MapFileNameToTemplateKey(fileName);
                LogDebug($"[HELP SYSTEM] Loading template file: {fileName} -> {templateKey}");

                var content = File.ReadAllText(filePath);

                if (!string.IsNullOrEmpty(content))
                {
                    var oldTemplate = _loadedTemplates.ContainsKey(templateKey) ? _loadedTemplates[templateKey] : null;
                    _loadedTemplates[templateKey] = content;

                    if (oldTemplate == null)
                    {
                        LogDebug($"[HELP SYSTEM] Added new template: {templateKey}");
                        TemplateAdded?.Invoke(templateKey, content);
                    }
                    else
                    {
                        LogDebug($"[HELP SYSTEM] Updated template: {templateKey}");
                        TemplateUpdated?.Invoke(templateKey, content);
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[HELP SYSTEM] Error loading template file {filePath}: {ex.Message}");
            }
        }

        private static void SetupFileWatchers()
        {
            try
            {
                var watcher = new FileSystemWatcher(HelpTemplatesDirectory, "*.txt")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
                    EnableRaisingEvents = true
                };

                watcher.Created += OnTemplateFileChanged;
                watcher.Changed += OnTemplateFileChanged;
                watcher.Deleted += OnTemplateFileDeleted;
                watcher.Renamed += OnTemplateFileRenamed;

                _watchers["help-templates"] = watcher;
                LogDebug("[HELP SYSTEM] Set up file watcher for help template files");
            }
            catch (Exception ex)
            {
                LogDebug($"[HELP SYSTEM] Error setting up file watcher: {ex.Message}");
            }
        }

        private static async void OnTemplateFileChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                var fileName = Path.GetFileNameWithoutExtension(e.Name).ToLower();
                LogDebug($"[HELP SYSTEM] Template file changed: {fileName}");
                
                // Add a small delay to ensure file write is complete
                await System.Threading.Tasks.Task.Delay(100);
                
                LoadTemplateFile(e.FullPath);
            }
            catch (Exception ex)
            {
                LogDebug($"[HELP SYSTEM] Error handling file change: {ex.Message}");
            }
        }

        private static void OnTemplateFileDeleted(object sender, FileSystemEventArgs e)
        {
            try
            {
                var fileName = Path.GetFileNameWithoutExtension(e.Name).ToLower();
                
                if (_loadedTemplates.ContainsKey(fileName))
                {
                    _loadedTemplates.Remove(fileName);
                    LogDebug($"[HELP SYSTEM] Template removed: {fileName}");
                    TemplateRemoved?.Invoke(fileName);
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[HELP SYSTEM] Error handling file deletion: {ex.Message}");
            }
        }

        private static void OnTemplateFileRenamed(object sender, RenamedEventArgs e)
        {
            try
            {
                var oldFileName = Path.GetFileNameWithoutExtension(e.OldName).ToLower();
                var newFileName = Path.GetFileNameWithoutExtension(e.Name).ToLower();
                
                // Remove old template
                if (_loadedTemplates.ContainsKey(oldFileName))
                {
                    _loadedTemplates.Remove(oldFileName);
                    TemplateRemoved?.Invoke(oldFileName);
                }
                
                // Load new template
                LoadTemplateFile(e.FullPath);
            }
            catch (Exception ex)
            {
                LogDebug($"[HELP SYSTEM] Error handling file rename: {ex.Message}");
            }
        }

        private static void CreateDefaultTemplates()
        {
            // Create a simple template file as an example
            var templatePath = Path.Combine(HelpTemplatesDirectory, "_template.txt");
            var templateContent = @"<a href=""text://
<font color=#00D4FF>My Custom Help Template</font>
<font color=#00D4FF>Created by {{ botname }}</font>

<img src=tdb://id:GFX_GUI_FRIENDLIST_SPLITTER>

<font color=#00D4FF>=== CUSTOM HELP CONTENT ===</font>

<font color=#FFFFFF>Add your custom help content here!</font>
<font color=#FFFFFF>You can use {{ botname }} to insert the bot's name.</font>

<img src=tdb://id:GFX_GUI_FRIENDLIST_SPLITTER>

<font color=#888888>Custom Help Template</font>
"">Custom Help</a>";

            File.WriteAllText(templatePath, templateContent);
            LogDebug("[HELP SYSTEM] Created template example file");
        }

        private static string CreateFallbackMainHelp()
        {
            string botName = Client.CharacterName;
            return $@"<a href=""text://
<font color=#00D4FF>Craftbot V2.0 - Configuration-Based Processing Bot</font>
<font color=#00D4FF>Created by Code</font>

<img src=tdb://id:GFX_GUI_FRIENDLIST_SPLITTER>

<font color=#00D4FF>=== AVAILABLE COMMANDS ===</font>

<a href='chatcmd:///tell {botName} help'>help</a><font color=#00BDBD> - Show this help guide</font>
<a href='chatcmd:///tell {botName} recipes'>recipes</a><font color=#00BDBD> - List available recipes</font>
<a href='chatcmd:///tell {botName} status'>status</a><font color=#00BDBD> - Show bot status</font>

<img src=tdb://id:GFX_GUI_FRIENDLIST_SPLITTER>

<font color=#FFFF00>HOW TO USE:</font>
<font color=#FFFFFF>1. Trade me your items to process them</font>
<font color=#FFFFFF>2. I support multiple sets and unified processing</font>
<font color=#FFFFFF>3. All items will be returned in the same format</font>

<img src=tdb://id:GFX_GUI_FRIENDLIST_SPLITTER>

<font color=#888888>Craftbot V2.0 - Hot Reload Configuration System</font>
"">Craftbot Help Menu</a>";
        }

        private static void EnsureInitialized()
        {
            if (!_initialized)
            {
                Initialize();
            }
        }

        private static void LogDebug(string message)
        {
            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [DEBUG] {message}");

            // Also write to debug log file if possible
            try
            {
                // FIXED: BaseDirectory is already "Control Panel", just add "logs"
                var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "craftbot_debug.log");
                Directory.CreateDirectory(Path.GetDirectoryName(logPath));
                File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [DEBUG] {message}\n");
            }
            catch
            {
                // Silent error handling for logging
            }
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
            _loadedTemplates.Clear();
            LogDebug("[HELP SYSTEM] Configurable Help System shut down");
        }

        /// <summary>
        /// Get help system statistics
        /// </summary>
        public static string GetStatistics()
        {
            EnsureInitialized();
            return $"Loaded {_loadedTemplates.Count} help templates from config/help-templates/";
        }
    }
}
