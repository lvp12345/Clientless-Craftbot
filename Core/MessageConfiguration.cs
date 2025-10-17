using System.Collections.Generic;

namespace Craftbot.Core
{
    /// <summary>
    /// Configuration for all bot messages organized by category
    /// This allows complete customization of bot responses without code changes
    /// </summary>
    public class MessageConfiguration
    {
        public TradeMessages Trade { get; set; } = new TradeMessages();
        public HelpMessages Help { get; set; } = new HelpMessages();
        public ErrorMessages Errors { get; set; } = new ErrorMessages();
        public ProcessingMessages Processing { get; set; } = new ProcessingMessages();
        public GeneralMessages General { get; set; } = new GeneralMessages();
    }

    public class TradeMessages
    {
        public string TradeAccepted { get; set; } = "Trade accepted! Processing your items...";
        public string TradeCompleted { get; set; } = "Processing complete! Check your items.";
        public string TradeDeclined { get; set; } = "Trade declined. Please check your items and try again.";
        public string TradeTimeout { get; set; } = "Trade timed out. Please try again.";
        public string InventoryFull { get; set; } = "My inventory is full! Please try again later.";
        public string NoItemsToProcess { get; set; } = "No processable items found in trade.";
        public string ProcessingStarted { get; set; } = "Starting to process your {itemCount} items...";
        public string ProcessingFailed { get; set; } = "Processing failed. Returning your items.";
        public string ItemsReturned { get; set; } = "All items have been returned to you.";
        public string MultipleRecipesDetected { get; set; } = "Multiple recipe types detected. Processing {recipeType} items first.";
        public string RecipeCompleted { get; set; } = "{recipeName} processing completed! Created {resultCount} items.";
    }

    public class HelpMessages
    {
        public string MainHelp { get; set; } = "Welcome to Craftbot! Available commands: help, recipes, status. Trade me items to process them!";
        public string RecipeList { get; set; } = "Available recipes: {recipeList}. For details on a specific recipe, type 'help [recipe name]'.";
        public string RecipeHelp { get; set; } = "{recipeName}: {recipeDescription}";
        public string StatusMessage { get; set; } = "Bot Status: {status} | Processed: {processedCount} items | Uptime: {uptime}";
        public string UnknownCommand { get; set; } = "Unknown command. Type 'help' for available commands.";
        public string CommandList { get; set; } = "Available commands: {commandList}";
    }

    public class ErrorMessages
    {
        public string GeneralError { get; set; } = "An error occurred while processing your request.";
        public string RecipeError { get; set; } = "Error processing {recipeName}: {errorDetails}";
        public string ToolNotFound { get; set; } = "Required tool not found: {toolName}";
        public string InsufficientMaterials { get; set; } = "Insufficient materials for {recipeName}. Need: {requiredItems}";
        public string ProcessingTimeout { get; set; } = "Processing timed out. Your items have been returned.";
        public string SystemBusy { get; set; } = "System is busy processing other requests. Please try again in a moment.";
        public string InvalidItems { get; set; } = "Some items cannot be processed: {itemList}";
        public string ConfigurationError { get; set; } = "Configuration error detected. Please contact an administrator.";
    }

    public class ProcessingMessages
    {
        public string StartingRecipe { get; set; } = "Starting {recipeName} processing...";
        public string RecipeStep { get; set; } = "{recipeName} Step {stepNumber}: {stepDescription}";
        public string RecipeProgress { get; set; } = "Processing {currentItem}/{totalItems} items...";
        public string ToolFound { get; set; } = "Found required tool: {toolName}";
        public string CombiningItems { get; set; } = "Combining {item1} with {item2}...";
        public string ItemCreated { get; set; } = "Created: {itemName}";
        public string MultiSetProcessing { get; set; } = "Processing {setCount} complete sets of {recipeName}...";
        public string StepCompleted { get; set; } = "{recipeName} Step {stepNumber} completed successfully.";
        public string AllStepsCompleted { get; set; } = "All {recipeName} processing steps completed!";
    }

    public class GeneralMessages
    {
        public string BotStartup { get; set; } = "Craftbot is now online and ready to process items!";
        public string BotShutdown { get; set; } = "Craftbot is shutting down. Thank you for using our services!";
        public string MaintenanceMode { get; set; } = "Bot is currently in maintenance mode. Please try again later.";
        public string ConfigReloaded { get; set; } = "Configuration reloaded successfully!";
        public string NewRecipeAdded { get; set; } = "New recipe added: {recipeName}";
        public string RecipeUpdated { get; set; } = "Recipe updated: {recipeName}";
        public string FeatureEnabled { get; set; } = "Feature enabled: {featureName}";
        public string FeatureDisabled { get; set; } = "Feature disabled: {featureName}";
    }

}
