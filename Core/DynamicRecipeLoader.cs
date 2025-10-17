using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Craftbot.Core
{
    /// <summary>
    /// Dynamic recipe loader that scans individual recipe files and registers them automatically
    /// Supports hot reload when recipe files are added, modified, or removed
    /// </summary>
    public static class DynamicRecipeLoader
    {
        private static readonly string RecipesDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "recipes");
        private static readonly Dictionary<string, FileSystemWatcher> _watchers = new Dictionary<string, FileSystemWatcher>();
        private static readonly Dictionary<string, ConfigurableRecipe> _loadedRecipes = new Dictionary<string, ConfigurableRecipe>();
        private static bool _initialized = false;

        public static event Action<string, ConfigurableRecipe> RecipeAdded;
        public static event Action<string, ConfigurableRecipe> RecipeUpdated;
        public static event Action<string> RecipeRemoved;

        /// <summary>
        /// Initialize the dynamic recipe loader
        /// </summary>
        public static void Initialize()
        {
            try
            {
                LogDebug("[RECIPE LOADER] Initializing Dynamic Recipe Loader...");

                // Create recipes directory if it doesn't exist
                if (!Directory.Exists(RecipesDirectory))
                {
                    Directory.CreateDirectory(RecipesDirectory);
                    LogDebug($"[RECIPE LOADER] Created recipes directory: {RecipesDirectory}");
                }

                // Load all existing recipe files
                LoadAllRecipes();

                // Set up file watchers for hot reload
                SetupFileWatchers();

                _initialized = true;
                LogDebug($"[RECIPE LOADER] Initialized successfully with {_loadedRecipes.Count} recipes");
            }
            catch (Exception ex)
            {
                LogDebug($"[RECIPE LOADER] Error initializing: {ex.Message}");
            }
        }

        /// <summary>
        /// Get all loaded recipes
        /// </summary>
        public static List<ConfigurableRecipe> GetAllRecipes()
        {
            EnsureInitialized();
            return _loadedRecipes.Values.Where(r => r.Enabled).ToList();
        }

        /// <summary>
        /// Get a specific recipe by name
        /// </summary>
        public static ConfigurableRecipe GetRecipe(string recipeName)
        {
            EnsureInitialized();
            return _loadedRecipes.Values.FirstOrDefault(r => 
                r.Enabled && r.Name.Equals(recipeName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Check if an item can be processed by any recipe
        /// </summary>
        public static bool CanProcessItem(string itemName)
        {
            EnsureInitialized();
            return _loadedRecipes.Values.Any(r => 
                r.Enabled && r.ProcessableItems.Contains(itemName, StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Get recipes that can process a specific item
        /// </summary>
        public static List<ConfigurableRecipe> GetRecipesForItem(string itemName)
        {
            EnsureInitialized();
            return _loadedRecipes.Values.Where(r => 
                r.Enabled && r.ProcessableItems.Contains(itemName, StringComparer.OrdinalIgnoreCase)).ToList();
        }

        private static void LoadAllRecipes()
        {
            var recipeFiles = Directory.GetFiles(RecipesDirectory, "*.json");
            foreach (var filePath in recipeFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                
                // Skip template and other special files
                if (fileName.StartsWith("_"))
                {
                    LogDebug($"[RECIPE LOADER] Skipping template file: {fileName}");
                    continue;
                }

                LoadRecipeFile(filePath);
            }
        }

        private static void LoadRecipeFile(string filePath)
        {
            try
            {
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                LogDebug($"[RECIPE LOADER] Loading recipe file: {fileName}");

                var json = File.ReadAllText(filePath);
                var recipe = JsonConvert.DeserializeObject<ConfigurableRecipe>(json);

                if (recipe != null)
                {
                    // Validate recipe
                    if (ValidateRecipe(recipe))
                    {
                        var oldRecipe = _loadedRecipes.ContainsKey(fileName) ? _loadedRecipes[fileName] : null;
                        _loadedRecipes[fileName] = recipe;

                        if (oldRecipe == null)
                        {
                            LogDebug($"[RECIPE LOADER] Added new recipe: {recipe.Name}");
                            RecipeAdded?.Invoke(fileName, recipe);
                        }
                        else
                        {
                            LogDebug($"[RECIPE LOADER] Updated recipe: {recipe.Name}");
                            RecipeUpdated?.Invoke(fileName, recipe);
                        }
                    }
                    else
                    {
                        LogDebug($"[RECIPE LOADER] Recipe validation failed for: {fileName}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[RECIPE LOADER] Error loading recipe file {filePath}: {ex.Message}");
            }
        }

        private static bool ValidateRecipe(ConfigurableRecipe recipe)
        {
            if (string.IsNullOrEmpty(recipe.Name))
            {
                LogDebug("[RECIPE LOADER] Recipe validation failed: Name is required");
                return false;
            }

            if (recipe.Steps == null || !recipe.Steps.Any())
            {
                LogDebug("[RECIPE LOADER] Recipe validation failed: At least one step is required");
                return false;
            }

            foreach (var step in recipe.Steps)
            {
                if (string.IsNullOrEmpty(step.Tool) || string.IsNullOrEmpty(step.InputItem) || string.IsNullOrEmpty(step.OutputItem))
                {
                    LogDebug($"[RECIPE LOADER] Recipe validation failed: Step {step.StepNumber} missing required fields");
                    return false;
                }
            }

            return true;
        }

        private static void SetupFileWatchers()
        {
            try
            {
                var watcher = new FileSystemWatcher(RecipesDirectory, "*.json")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
                    EnableRaisingEvents = true
                };

                watcher.Created += OnRecipeFileChanged;
                watcher.Changed += OnRecipeFileChanged;
                watcher.Deleted += OnRecipeFileDeleted;
                watcher.Renamed += OnRecipeFileRenamed;

                _watchers["recipes"] = watcher;
                LogDebug("[RECIPE LOADER] Set up file watcher for recipe files");
            }
            catch (Exception ex)
            {
                LogDebug($"[RECIPE LOADER] Error setting up file watcher: {ex.Message}");
            }
        }

        private static async void OnRecipeFileChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                var fileName = Path.GetFileNameWithoutExtension(e.Name);
                
                // Skip template files
                if (fileName.StartsWith("_")) return;

                LogDebug($"[RECIPE LOADER] Recipe file changed: {fileName}");
                
                // Add a small delay to ensure file write is complete
                await System.Threading.Tasks.Task.Delay(100);
                
                LoadRecipeFile(e.FullPath);
            }
            catch (Exception ex)
            {
                LogDebug($"[RECIPE LOADER] Error handling file change: {ex.Message}");
            }
        }

        private static void OnRecipeFileDeleted(object sender, FileSystemEventArgs e)
        {
            try
            {
                var fileName = Path.GetFileNameWithoutExtension(e.Name);
                
                if (_loadedRecipes.ContainsKey(fileName))
                {
                    var recipe = _loadedRecipes[fileName];
                    _loadedRecipes.Remove(fileName);
                    
                    LogDebug($"[RECIPE LOADER] Recipe removed: {recipe.Name}");
                    RecipeRemoved?.Invoke(fileName);
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[RECIPE LOADER] Error handling file deletion: {ex.Message}");
            }
        }

        private static void OnRecipeFileRenamed(object sender, RenamedEventArgs e)
        {
            try
            {
                var oldFileName = Path.GetFileNameWithoutExtension(e.OldName);
                var newFileName = Path.GetFileNameWithoutExtension(e.Name);
                
                // Remove old recipe
                if (_loadedRecipes.ContainsKey(oldFileName))
                {
                    _loadedRecipes.Remove(oldFileName);
                    RecipeRemoved?.Invoke(oldFileName);
                }
                
                // Load new recipe
                if (!newFileName.StartsWith("_"))
                {
                    LoadRecipeFile(e.FullPath);
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[RECIPE LOADER] Error handling file rename: {ex.Message}");
            }
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
            _loadedRecipes.Clear();
            LogDebug("[RECIPE LOADER] Dynamic Recipe Loader shut down");
        }

        /// <summary>
        /// Get recipe statistics
        /// </summary>
        public static string GetStatistics()
        {
            EnsureInitialized();
            var enabled = _loadedRecipes.Values.Count(r => r.Enabled);
            var disabled = _loadedRecipes.Values.Count(r => !r.Enabled);
            var simple = _loadedRecipes.Values.Count(r => r.Enabled && r.Type == "Simple");
            var multiStep = _loadedRecipes.Values.Count(r => r.Enabled && r.Type == "MultiStep");
            
            return $"Loaded: {enabled} enabled, {disabled} disabled | Types: {simple} simple, {multiStep} multi-step";
        }
    }
}
