using System;
using System.Collections.Generic;
using System.Linq;

namespace Craftbot.Core
{
    /// <summary>
    /// Service for sending configurable messages to players
    /// Supports message templates, funny messages, and hot reload
    /// </summary>
    public static class MessageService
    {
        private static MessageConfiguration _config;
        private static Random _random = new Random();
        private static bool _initialized = false;

        /// <summary>
        /// Initialize the message service
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            // Load initial configuration
            _config = ConfigurationManager.GetConfiguration<MessageConfiguration>("messages");
            
            // Subscribe to configuration changes for hot reload
            ConfigurationManager.ConfigurationChanged += OnConfigurationChanged;
            
            _initialized = true;
            LogDebug("[MESSAGE SERVICE] Initialized successfully");
        }

        private static void OnConfigurationChanged(string configName, object configuration)
        {
            if (configName == "messages" && configuration is MessageConfiguration newConfig)
            {
                _config = newConfig;
                LogDebug("[MESSAGE SERVICE] Configuration reloaded");
            }
        }

        #region Trade Messages

        public static string GetTradeAcceptedMessage()
        {
            EnsureInitialized();
            return _config.Trade.TradeAccepted;
        }

        public static string GetTradeCompletedMessage()
        {
            EnsureInitialized();
            return _config.Trade.TradeCompleted;
        }

        public static string GetTradeDeclinedMessage()
        {
            EnsureInitialized();
            return _config.Trade.TradeDeclined;
        }

        public static string GetProcessingStartedMessage(int itemCount)
        {
            EnsureInitialized();
            return _config.Trade.ProcessingStarted.Replace("{itemCount}", itemCount.ToString());
        }

        public static string GetRecipeCompletedMessage(string recipeName, int resultCount)
        {
            EnsureInitialized();
            return _config.Trade.RecipeCompleted
                .Replace("{recipeName}", recipeName)
                .Replace("{resultCount}", resultCount.ToString());
        }

        public static string GetMultipleRecipesDetectedMessage(string recipeType)
        {
            EnsureInitialized();
            return _config.Trade.MultipleRecipesDetected.Replace("{recipeType}", recipeType);
        }

        #endregion

        #region Help Messages

        public static string GetMainHelpMessage()
        {
            EnsureInitialized();
            return _config.Help.MainHelp;
        }

        public static string GetRecipeListMessage(string recipeList)
        {
            EnsureInitialized();
            return _config.Help.RecipeList.Replace("{recipeList}", recipeList);
        }

        public static string GetRecipeHelpMessage(string recipeName, string recipeDescription)
        {
            EnsureInitialized();
            return _config.Help.RecipeHelp
                .Replace("{recipeName}", recipeName)
                .Replace("{recipeDescription}", recipeDescription);
        }

        public static string GetStatusMessage(string status, int processedCount, string uptime)
        {
            EnsureInitialized();
            return _config.Help.StatusMessage
                .Replace("{status}", status)
                .Replace("{processedCount}", processedCount.ToString())
                .Replace("{uptime}", uptime);
        }

        public static string GetCommandListMessage(string commandList)
        {
            EnsureInitialized();
            return _config.Help.CommandList.Replace("{commandList}", commandList);
        }

        #endregion

        #region Error Messages

        public static string GetGeneralErrorMessage()
        {
            EnsureInitialized();
            return _config.Errors.GeneralError;
        }

        public static string GetRecipeErrorMessage(string recipeName, string errorDetails)
        {
            EnsureInitialized();
            return _config.Errors.RecipeError
                .Replace("{recipeName}", recipeName)
                .Replace("{errorDetails}", errorDetails);
        }

        public static string GetToolNotFoundMessage(string toolName)
        {
            EnsureInitialized();
            return _config.Errors.ToolNotFound.Replace("{toolName}", toolName);
        }

        public static string GetInsufficientMaterialsMessage(string recipeName, string requiredItems)
        {
            EnsureInitialized();
            return _config.Errors.InsufficientMaterials
                .Replace("{recipeName}", recipeName)
                .Replace("{requiredItems}", requiredItems);
        }

        #endregion

        #region Processing Messages

        public static string GetStartingRecipeMessage(string recipeName)
        {
            EnsureInitialized();
            return _config.Processing.StartingRecipe.Replace("{recipeName}", recipeName);
        }

        public static string GetRecipeStepMessage(string recipeName, int stepNumber, string stepDescription)
        {
            EnsureInitialized();
            return _config.Processing.RecipeStep
                .Replace("{recipeName}", recipeName)
                .Replace("{stepNumber}", stepNumber.ToString())
                .Replace("{stepDescription}", stepDescription);
        }

        public static string GetMultiSetProcessingMessage(int setCount, string recipeName)
        {
            EnsureInitialized();
            return _config.Processing.MultiSetProcessing
                .Replace("{setCount}", setCount.ToString())
                .Replace("{recipeName}", recipeName);
        }

        public static string GetItemCreatedMessage(string itemName)
        {
            EnsureInitialized();
            return _config.Processing.ItemCreated.Replace("{itemName}", itemName);
        }

        #endregion

        #region General Messages

        public static string GetBotStartupMessage()
        {
            EnsureInitialized();
            return _config.General.BotStartup;
        }

        public static string GetConfigReloadedMessage()
        {
            EnsureInitialized();
            return _config.General.ConfigReloaded;
        }

        public static string GetNewRecipeAddedMessage(string recipeName)
        {
            EnsureInitialized();
            return _config.General.NewRecipeAdded.Replace("{recipeName}", recipeName);
        }

        #endregion

        #region Helper Methods

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

        #endregion

        #region Template Processing

        /// <summary>
        /// Process a message template with custom parameters
        /// </summary>
        public static string ProcessTemplate(string template, Dictionary<string, string> parameters)
        {
            if (string.IsNullOrEmpty(template) || parameters == null)
                return template;

            var result = template;
            foreach (var param in parameters)
            {
                result = result.Replace($"{{{param.Key}}}", param.Value);
            }
            return result;
        }

        /// <summary>
        /// Get a custom message by category and key
        /// </summary>
        public static string GetCustomMessage(string category, string key, Dictionary<string, string> parameters = null)
        {
            EnsureInitialized();
            
            // This could be extended to support custom message categories
            var template = $"Custom message: {category}.{key}";
            
            if (parameters != null)
            {
                template = ProcessTemplate(template, parameters);
            }
            
            return template;
        }

        #endregion
    }
}
