using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AOSharp.Clientless;
using AOSharp.Common.GameData;
using AOSharp.Core;
using Newtonsoft.Json;

namespace Craftbot.Core
{
    /// <summary>
    /// Dynamic command handler that registers commands from configuration
    /// Supports hot reload and automatic command registration
    /// </summary>
    public static class DynamicCommandHandler
    {
        private static CommandConfiguration _config;
        private static Dictionary<string, ConfigurableCommand> _commands = new Dictionary<string, ConfigurableCommand>();
        private static Dictionary<string, DateTime> _lastCommandTime = new Dictionary<string, DateTime>();
        private static Dictionary<string, int> _commandCounts = new Dictionary<string, int>();
        private static bool _initialized = false;
        private static DateTime _startTime = DateTime.Now;
        private static Dictionary<string, AdminTradeInfo> _pendingAdminTrades = new Dictionary<string, AdminTradeInfo>();
        private static Dictionary<uint, AdminItemLocation> _adminItemLocations = new Dictionary<uint, AdminItemLocation>();

        /// <summary>
        /// Initialize the dynamic command handler
        /// </summary>
        public static Task Initialize()
        {
            if (_initialized) return Task.CompletedTask;

            // Load initial configuration
            _config = ConfigurationManager.GetConfiguration<CommandConfiguration>("commands");
            
            // Register all commands
            RegisterCommands();
            
            // Subscribe to configuration changes for hot reload
            ConfigurationManager.ConfigurationChanged += OnConfigurationChanged;

            // Register for trade events to handle admin trades
            AOSharp.Clientless.Trade.TradeOpened += OnAdminTradeOpened;
            AOSharp.Clientless.Trade.TradeStatusChanged += OnAdminTradeStatusChanged;

            // Load admin item locations
            LoadAdminItemLocations();

            _initialized = true;
            LogDebug("[COMMAND HANDLER] Initialized successfully");
            return Task.CompletedTask;
        }

        private static void OnConfigurationChanged(string configName, object configuration)
        {
            if (configName == "commands" && configuration is CommandConfiguration newConfig)
            {
                _config = newConfig;
                RegisterCommands();
                LogDebug("[COMMAND HANDLER] Commands reloaded from configuration");
            }
        }

        private static void RegisterCommands()
        {
            _commands.Clear();
            
            foreach (var command in _config.Commands.Where(c => c.Enabled))
            {
                // Register main command name
                _commands[command.Name.ToLower()] = command;
                
                // Register aliases if enabled
                if (_config.Settings.EnableAliases)
                {
                    foreach (var alias in command.Aliases)
                    {
                        _commands[alias.ToLower()] = command;
                    }
                }
            }
            
            LogDebug($"[COMMAND HANDLER] Registered {_commands.Count} command mappings");
        }

        /// <summary>
        /// Process a chat command
        /// </summary>
        public static async Task<string> ProcessCommand(string playerName, string message)
        {
            EnsureInitialized();

            try
            {
                // Parse command and parameters
                var parts = message.Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) return null;

                var commandName = parts[0];
                var parameters = parts.Skip(1).ToArray();

                // Remove prefix if configured
                if (!string.IsNullOrEmpty(_config.Settings.CommandPrefix))
                {
                    if (!commandName.StartsWith(_config.Settings.CommandPrefix))
                        return null;
                    
                    commandName = commandName.Substring(_config.Settings.CommandPrefix.Length);
                }

                // Case sensitivity handling
                if (!_config.Settings.CaseSensitive)
                {
                    commandName = commandName.ToLower();
                }

                // Check if command exists
                if (!_commands.TryGetValue(commandName, out var command))
                {
                    return null; // Command not found
                }

                // Check cooldowns
                if (!CheckCooldown(playerName, command))
                {
                    return "Please wait before using that command again.";
                }

                // Check rank permissions
                if (!CheckRankPermission(command.Rank, playerName))
                {
                    return $"This command requires '{command.Rank}' rank or higher.";
                }

                // Check parameter requirements
                if (command.RequiresParameters && parameters.Length == 0)
                {
                    return $"Command '{command.Name}' requires parameters. Usage: {command.Description}";
                }

                // Log command usage
                if (_config.Settings.LogCommandUsage)
                {
                    LogDebug($"[COMMAND] {playerName} used command: {command.Name}");
                }

                // Execute command
                return await ExecuteCommand(command, playerName, parameters);
            }
            catch (Exception ex)
            {
                LogDebug($"[COMMAND HANDLER] Error processing command: {ex.Message}");
                return MessageService.GetGeneralErrorMessage();
            }
        }

        private static async Task<string> ExecuteCommand(ConfigurableCommand command, string playerName, string[] parameters)
        {
            switch (command.ActionType.ToLower())
            {
                case "response":
                    return ProcessResponseTemplate(command.Response, playerName, parameters);
                
                case "action":
                    return await ExecuteCustomAction(command, playerName, parameters);
                
                case "custom":
                    return await ExecuteCustomCommand(command, playerName, parameters);
                
                default:
                    return ProcessResponseTemplate(command.Response, playerName, parameters);
            }
        }

        private static string ProcessResponseTemplate(string template, string playerName, string[] parameters)
        {
            var replacements = new Dictionary<string, string>
            {
                ["playerName"] = playerName,
                ["uptime"] = GetUptime(),
                ["processedCount"] = GetProcessedCount().ToString(),
                ["commandList"] = GetCommandList(),
                ["recipeList"] = GetRecipeList()
            };

            // Add parameter replacements
            for (int i = 0; i < parameters.Length; i++)
            {
                replacements[$"param{i + 1}"] = parameters[i];
                replacements[$"parameter{i + 1}"] = parameters[i];
            }

            return MessageService.ProcessTemplate(template, replacements);
        }

        private static Task<string> ExecuteCustomAction(ConfigurableCommand command, string playerName, string[] parameters)
        {
            // Handle built-in actions
            switch (command.Name.ToLower())
            {
                case "reload":
                    ReloadConfigurations();
                    return Task.FromResult(MessageService.GetConfigReloadedMessage());

                case "status":
                    return Task.FromResult(MessageService.GetStatusMessage("Online", GetProcessedCount(), GetUptime()));

                case "clean":
                case "implantclean":
                case "ic":
                    // Handle implant cleaning command - open trade
                    HandleCleanCommand(playerName);
                    return Task.FromResult(ProcessResponseTemplate(command.Response, playerName, parameters));

                default:
                    return Task.FromResult(ProcessResponseTemplate(command.Response, playerName, parameters));
            }
        }

        private static async Task<string> ExecuteCustomCommand(ConfigurableCommand command, string playerName, string[] parameters)
        {
            // Handle custom command actions
            switch (command.CustomAction?.ToLower())
            {
                case "admininventoryview":
                    return await HandleAdminInventoryView(playerName);

                case "admingetitem":
                    return await HandleAdminGetItem(playerName, parameters);

                case "admingiveitem":
                    return await HandleAdminGiveItem(playerName);

                case "adminviewlocations":
                    return await HandleAdminViewLocations(playerName);

                default:
                    // This could be extended to support custom command scripts or actions
                    LogDebug($"[COMMAND] Executing custom command: {command.Name}");
                    return ProcessResponseTemplate(command.Response, playerName, parameters);
            }
        }

        private static bool CheckCooldown(string playerName, ConfigurableCommand command)
        {
            var now = DateTime.Now;
            var key = $"{playerName}:{command.Name}";

            // Check global cooldown
            if (_config.Settings.GlobalCooldown > 0)
            {
                var globalKey = $"{playerName}:global";
                if (_lastCommandTime.TryGetValue(globalKey, out var lastGlobal))
                {
                    if ((now - lastGlobal).TotalSeconds < _config.Settings.GlobalCooldown)
                    {
                        return false;
                    }
                }
                _lastCommandTime[globalKey] = now;
            }

            // Check command-specific cooldown
            if (command.Cooldown > 0)
            {
                if (_lastCommandTime.TryGetValue(key, out var lastCommand))
                {
                    if ((now - lastCommand).TotalSeconds < command.Cooldown)
                    {
                        return false;
                    }
                }
                _lastCommandTime[key] = now;
            }

            return true;
        }

        private static bool IsAdmin(string playerName)
        {
            // Use the same admin checking logic as PrivateMessageModule
            return Modules.PrivateMessageModule.IsUserAuthorized(playerName);
        }

        /// <summary>
        /// Check if a player has the required rank for a command
        /// </summary>
        private static bool CheckRankPermission(string requiredRank, string playerName)
        {
            // Everyone can use commands with "Everyone" rank
            if (requiredRank == "Everyone")
                return true;

            // Check player's rank
            // Rank hierarchy: Admin > Moderator > VIP > User > (no rank)
            var rankHierarchy = new[] { "Admin", "Moderator", "VIP", "User" };
            var requiredRankIndex = System.Array.IndexOf(rankHierarchy, requiredRank);

            if (requiredRankIndex < 0)
                return false; // Invalid rank

            // Check if player is in the required rank or higher
            foreach (var rank in rankHierarchy.Take(requiredRankIndex + 1))
            {
                if (Modules.PrivateMessageModule.IsPlayerInRank(playerName, rank))
                    return true;
            }

            return false;
        }

        private static void ReloadConfigurations()
        {
            ConfigurationManager.ReloadConfiguration("messages").Wait();
            ConfigurationManager.ReloadConfiguration("commands").Wait();
            ConfigurationManager.ReloadConfiguration("recipes").Wait();
        }

        private static string GetSpecificHelp(string commandName)
        {
            if (_commands.TryGetValue(commandName.ToLower(), out var command))
            {
                return $"{command.Name}: {command.Description}";
            }
            return $"Command '{commandName}' not found.";
        }

        private static string GetUptime()
        {
            var uptime = DateTime.Now - _startTime;
            return $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m";
        }

        private static int GetProcessedCount()
        {
            // This would be tracked by the main system
            return 0; // Placeholder
        }

        private static string GetCommandList()
        {
            var commands = _config.Commands
                .Where(c => c.Enabled && c.Rank == "Everyone")
                .Select(c => c.Name)
                .Distinct()
                .OrderBy(c => c);

            return string.Join(", ", commands);
        }

        private static string GetRecipeList()
        {
            var recipeConfig = ConfigurationManager.GetConfiguration<RecipeConfiguration>("recipes");
            if (recipeConfig?.Recipes != null)
            {
                var recipes = recipeConfig.Recipes
                    .Where(r => r.Enabled)
                    .Select(r => r.Name)
                    .OrderBy(r => r);
                
                return string.Join(", ", recipes);
            }
            return "Robot Brain, Plasma, Stalker Helmet"; // Fallback
        }

        private static void EnsureInitialized()
        {
            if (!_initialized)
            {
                _ = Task.Run(async () => await Initialize());
            }
        }

        private static Task<string> HandleAdminInventoryView(string playerName)
        {
            try
            {
                LogDebug($"[ADMIN] {playerName} requested admin inventory view");

                // Get all items in bot's inventory
                var inventoryItems = GetBotInventoryItems();

                if (!inventoryItems.Any())
                {
                    return Task.FromResult("Bot inventory is empty.");
                }

                // Export inventory data for control panel if requested
                ExportInventoryDataForControlPanel(inventoryItems);

                // Create clickable inventory list
                string inventoryContent = CreateAdminInventoryList(inventoryItems);
                return Task.FromResult(inventoryContent);
            }
            catch (Exception ex)
            {
                LogDebug($"[ADMIN] Error creating inventory view for {playerName}: {ex.Message}");
                return Task.FromResult("Error retrieving bot inventory. Please try again.");
            }
        }

        /// <summary>
        /// Get all bot inventory items (shared method for both in-game and control panel)
        /// </summary>
        public static List<InventoryItem> GetBotInventoryItems()
        {
            var inventoryItems = new List<InventoryItem>();

            try
            {
                // Get items from main inventory (EXCLUDE equipment pages)
                foreach (var item in Inventory.Items)
                {
                    // EXCLUDE equipment pages - they should NEVER be shown in admin view
                    if (item.Slot.Type == IdentityType.ArmorPage ||
                        item.Slot.Type == IdentityType.WeaponPage ||
                        item.Slot.Type == IdentityType.ImplantPage ||
                        item.Slot.Type == IdentityType.SocialPage)
                    {
                        continue; // Skip all equipment items
                    }

                    // Skip Pure Novictum Ring items as per user preference
                    if (item.Name != null && item.Name.ToLower().Contains("pure novictum ring"))
                        continue;

                    // Use item.Id as the unique identifier since UniqueIdentity.Instance can be 0
                    var itemInstance = (uint)item.Id;

                    inventoryItems.Add(new InventoryItem
                    {
                        Name = item.Name ?? $"Item_{item.Id}",
                        Id = (uint)item.Id,
                        Quality = item.Ql,
                        ItemInstance = itemInstance,
                        SlotType = item.Slot.Type,
                        SlotInstance = item.Slot.Instance
                    });
                }

                // Get items from opened containers/bags
                foreach (var container in Inventory.Containers)
                {
                    foreach (var item in container.Items)
                    {
                        // Skip Pure Novictum Ring items
                        if (item.Name != null && item.Name.ToLower().Contains("pure novictum ring"))
                            continue;

                        // Use item.Id as the unique identifier since UniqueIdentity.Instance can be 0
                        var itemInstance = (uint)item.Id;

                        inventoryItems.Add(new InventoryItem
                        {
                            Name = item.Name ?? $"Item_{item.Id}",
                            Id = (uint)item.Id,
                            Quality = item.Ql,
                            ItemInstance = itemInstance,
                            SlotType = item.Slot.Type,
                            SlotInstance = item.Slot.Instance,
                            ContainerName = container.Item.Name
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[INVENTORY] Error getting bot inventory items: {ex.Message}");
            }

            return inventoryItems;
        }

        /// <summary>
        /// Export inventory data to JSON file for control panel consumption
        /// </summary>
        private static void ExportInventoryDataForControlPanel(List<InventoryItem> inventoryItems)
        {
            try
            {
                var exportData = inventoryItems.Select(item => new
                {
                    name = item.Name,
                    id = item.ItemInstance,
                    quality = item.Quality,
                    container = item.ContainerName
                }).ToList();

                string exportPath = Path.Combine(Craftbot.PluginDir, "bin", "Debug", "bot_inventory.json");
                string jsonData = Newtonsoft.Json.JsonConvert.SerializeObject(exportData, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(exportPath, jsonData);

                LogDebug($"[INVENTORY] Exported {inventoryItems.Count} items to {exportPath}");
            }
            catch (Exception ex)
            {
                LogDebug($"[INVENTORY] Error exporting inventory data: {ex.Message}");
            }
        }

        /// <summary>
        /// Check for control panel commands and execute them
        /// </summary>
        public static void ProcessControlPanelCommands()
        {
            try
            {
                string configPath = Path.Combine(Craftbot.PluginDir, "bin", "Debug", "config");
                string commandFile = Path.Combine(configPath, "control_panel_command.tmp");

                if (File.Exists(commandFile))
                {
                    string command = File.ReadAllText(commandFile).Trim();
                    File.Delete(commandFile); // Remove the command file

                    LogDebug($"[CONTROL PANEL] Processing command: {command}");

                    if (command.Equals("aview", StringComparison.OrdinalIgnoreCase))
                    {
                        // Execute the aview command and export data
                        var inventoryItems = GetBotInventoryItems();
                        ExportInventoryDataForControlPanel(inventoryItems);
                        LogDebug($"[CONTROL PANEL] aview command executed, exported {inventoryItems.Count} items");
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[CONTROL PANEL] Error processing commands: {ex.Message}");
            }
        }

        private static async Task<string> HandleAdminGetItem(string playerName, string[] parameters)
        {
            try
            {
                if (parameters.Length == 0)
                {
                    return "Usage: adminget <item_instance>";
                }

                // Parse item ID (we're using item.Id as the identifier)
                if (!uint.TryParse(parameters[0], out uint itemId))
                {
                    return "Invalid item ID. Please use the Get links from the inventory list.";
                }

                LogDebug($"[ADMIN] {playerName} requested item with ID {itemId}");

                // Find the item in bot's inventory and track its location
                Item targetItem = null;
                AdminItemLocation itemLocation = null;

                // Check main inventory (EXCLUDE equipment pages)
                foreach (var item in Inventory.Items)
                {
                    if ((uint)item.Id == itemId)
                    {
                        // EXCLUDE equipment pages - they should NEVER be tradeable
                        if (item.Slot.Type == IdentityType.ArmorPage ||
                            item.Slot.Type == IdentityType.WeaponPage ||
                            item.Slot.Type == IdentityType.ImplantPage ||
                            item.Slot.Type == IdentityType.SocialPage)
                        {
                            return "Equipment items cannot be traded. Only inventory and container items are available.";
                        }

                        targetItem = item;
                        itemLocation = new AdminItemLocation
                        {
                            ItemInstance = itemId, // Store the item ID as the instance
                            PlayerName = playerName,
                            OriginalSlotType = item.Slot.Type,
                            OriginalSlotInstance = item.Slot.Instance,
                            ContainerName = null,
                            ContainerInstance = null,
                            LocationType = ItemLocationType.MainInventory,
                            TrackedAt = DateTime.Now
                        };
                        break;
                    }
                }

                // Check containers if not found in main inventory
                if (targetItem == null)
                {
                    foreach (var container in Inventory.Containers)
                    {
                        foreach (var item in container.Items)
                        {
                            if ((uint)item.Id == itemId)
                            {
                                targetItem = item;
                                itemLocation = new AdminItemLocation
                                {
                                    ItemInstance = itemId, // Store the item ID as the instance
                                    PlayerName = playerName,
                                    OriginalSlotType = item.Slot.Type,
                                    OriginalSlotInstance = item.Slot.Instance,
                                    ContainerName = container.Item.Name,
                                    ContainerInstance = (uint)container.Item.Id, // Use container ID instead of Instance
                                    LocationType = ItemLocationType.Container,
                                    TrackedAt = DateTime.Now
                                };
                                break;
                            }
                        }
                        if (targetItem != null) break;
                    }
                }

                if (targetItem == null)
                {
                    return "Item not found in bot inventory. It may have been moved or used.";
                }

                // Skip Pure Novictum Ring items
                if (targetItem.Name != null && targetItem.Name.ToLower().Contains("pure novictum ring"))
                {
                    return "Pure Novictum Ring items cannot be traded.";
                }

                // Store the item location for future restoration
                if (itemLocation != null)
                {
                    _adminItemLocations[itemId] = itemLocation;
                    LogDebug($"[ADMIN LOCATION] Tracked item location: {targetItem.Name} from {itemLocation.LocationType} " +
                            $"(Slot: {itemLocation.OriginalSlotType}:{itemLocation.OriginalSlotInstance}" +
                            $"{(itemLocation.ContainerName != null ? $", Container: {itemLocation.ContainerName}" : "")})");

                    // Save location data to file for persistence
                    SaveAdminItemLocations();
                }

                // Initiate trade with the admin player
                await InitiateAdminTrade(playerName, targetItem);

                LogDebug($"[ADMIN] Initiated trade with {playerName} for item: {targetItem.Name}");
                return $"Opening trade to give you: {targetItem.Name ?? "Unknown Item"}";
            }
            catch (Exception ex)
            {
                LogDebug($"[ADMIN] Error handling get item request from {playerName}: {ex.Message}");
                return "Error processing item request. Please try again.";
            }
        }

        private static async Task<string> HandleAdminGiveItem(string playerName)
        {
            try
            {
                LogDebug($"[ADMIN] {playerName} requested to give items back to bot");

                // Find the player
                var targetPlayer = DynelManager.Players.FirstOrDefault(p =>
                    p.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase));

                if (targetPlayer == null)
                {
                    return "Player not found. Please make sure you are near the bot.";
                }

                // Store the admin give trade info
                _pendingAdminTrades[playerName] = new AdminTradeInfo
                {
                    PlayerName = playerName,
                    PlayerId = (uint)targetPlayer.Identity.Instance,
                    RequestedItem = null, // No specific item for give trades
                    InitiatedAt = DateTime.Now
                };

                // Open trade with delay
                await Task.Delay(500);

                LogDebug($"[ADMIN] Opening trade with {playerName} to receive items back");
                Trade.Open(targetPlayer.Identity);

                return "Opening trade to receive items back. Please add the items you want to return to the bot.";
            }
            catch (Exception ex)
            {
                LogDebug($"[ADMIN] Error handling give item request from {playerName}: {ex.Message}");
                return "Error processing give item request. Please try again.";
            }
        }

        private static Task<string> HandleAdminViewLocations(string playerName)
        {
            try
            {
                LogDebug($"[ADMIN] {playerName} requested tracked item locations");

                if (!_adminItemLocations.Any())
                {
                    return Task.FromResult("No tracked item locations found.");
                }

                var content = new System.Text.StringBuilder();
                content.AppendLine("=== TRACKED ITEM LOCATIONS ===");
                content.AppendLine($"Total tracked items: {_adminItemLocations.Count}");
                content.AppendLine();

                var groupedByPlayer = _adminItemLocations.Values.GroupBy(loc => loc.PlayerName);

                foreach (var playerGroup in groupedByPlayer.OrderBy(g => g.Key))
                {
                    content.AppendLine($"Player: {playerGroup.Key}");
                    foreach (var location in playerGroup.OrderBy(l => l.TrackedAt))
                    {
                        var timeAgo = DateTime.Now - location.TrackedAt;
                        var timeAgoStr = timeAgo.TotalHours < 1
                            ? $"{(int)timeAgo.TotalMinutes}m ago"
                            : $"{(int)timeAgo.TotalHours}h ago";

                        content.AppendLine($"  • Instance {location.ItemInstance} - {location.LocationType}");
                        if (location.ContainerName != null)
                        {
                            content.AppendLine($"    Container: {location.ContainerName}");
                        }
                        content.AppendLine($"    Slot: {location.OriginalSlotType}:{location.OriginalSlotInstance}");
                        content.AppendLine($"    Tracked: {timeAgoStr}");
                        content.AppendLine();
                    }
                }

                return Task.FromResult(content.ToString());
            }
            catch (Exception ex)
            {
                LogDebug($"[ADMIN] Error viewing tracked locations for {playerName}: {ex.Message}");
                return Task.FromResult("Error retrieving tracked item locations.");
            }
        }

        private static async Task InitiateAdminTrade(string playerName, Item item)
        {
            try
            {
                // Find the player
                var targetPlayer = DynelManager.Players.FirstOrDefault(p =>
                    p.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase));

                if (targetPlayer == null)
                {
                    LogDebug($"[ADMIN] Player {playerName} not found for trade");
                    return;
                }

                // Store the item for trade completion
                _pendingAdminTrades[playerName] = new AdminTradeInfo
                {
                    PlayerName = playerName,
                    PlayerId = (uint)targetPlayer.Identity.Instance,
                    RequestedItem = item,
                    InitiatedAt = DateTime.Now
                };

                // Open trade with delay
                await System.Threading.Tasks.Task.Delay(500);

                LogDebug($"[ADMIN] Opening trade with {playerName} (ID: {targetPlayer.Identity.Instance})");
                Trade.Open(targetPlayer.Identity);
            }
            catch (Exception ex)
            {
                LogDebug($"[ADMIN] Error initiating trade with {playerName}: {ex.Message}");
            }
        }

        private static string CreateAdminInventoryList(List<InventoryItem> items)
        {
            try
            {
                var content = new System.Text.StringBuilder();
                content.AppendLine("<a href=\"text://");
                content.AppendLine("<font color=#FF6600>Admin Inventory View - Bot Items</font>");
                content.AppendLine("<font color=#888888>Click Get to trade item to yourself - Scroll down to see all items</font>");
                content.AppendLine("");
                content.AppendLine("<img src=tdb://id:GFX_GUI_FRIENDLIST_SPLITTER>");
                content.AppendLine("");

                // Group items by name but show ALL items (no limit)
                var groupedItems = items.GroupBy(i => i.Name).OrderBy(g => g.Key);
                int totalItemCount = 0;
                int uniqueItemTypes = 0;

                foreach (var group in groupedItems)
                {
                    uniqueItemTypes++;

                    // For items with multiple instances, show each one individually
                    foreach (var item in group.OrderBy(i => i.Quality).ThenBy(i => i.ContainerName ?? ""))
                    {
                        var displayName = item.Name;

                        if (!string.IsNullOrEmpty(item.ContainerName))
                        {
                            displayName += $" <font color=#888888>(in {item.ContainerName})</font>";
                        }

                        if (item.Quality > 0)
                        {
                            displayName += $" <font color=#FFFF00>QL{item.Quality}</font>";
                        }

                        // Create Get link using item ID for unique identification
                        content.AppendLine($"<a href='chatcmd:///tell {Client.CharacterName} adminget {item.ItemInstance}'>Get</a> - <font color=#00BDBD>{displayName}</font>");
                        totalItemCount++;
                    }

                    // Add spacing between different item types for better readability
                    if (uniqueItemTypes % 5 == 0)
                    {
                        content.AppendLine("");
                    }
                }

                content.AppendLine("");
                content.AppendLine("<img src=tdb://id:GFX_GUI_FRIENDLIST_SPLITTER>");
                content.AppendLine($"<font color=#888888>Total items: {totalItemCount} ({uniqueItemTypes} unique types)</font>");
                content.AppendLine($"<font color=#888888>All items shown - use scroll bar to navigate</font>");
                content.AppendLine("\">Items List</a>");

                return content.ToString();
            }
            catch (Exception ex)
            {
                LogDebug($"[ADMIN] Error creating inventory list: {ex.Message}");
                return "Error creating inventory list.";
            }
        }

        private static void OnAdminTradeOpened(Identity traderId)
        {
            try
            {
                LogDebug($"[ADMIN TRADE] Trade opened with player ID: {traderId.Instance}");

                // Check if this is an admin trade
                var adminTrade = _pendingAdminTrades.Values.FirstOrDefault(t => t.PlayerId == traderId.Instance);
                if (adminTrade != null)
                {
                    LogDebug($"[ADMIN TRADE] Found pending admin trade for {adminTrade.PlayerName}");

                    if (adminTrade.RequestedItem != null)
                    {
                        // This is a "get" trade - bot gives item to admin
                        Task.Run(async () =>
                        {
                            await Task.Delay(500); // Give trade window time to fully open

                            try
                            {
                                LogDebug($"[ADMIN TRADE] Adding item to trade: {adminTrade.RequestedItem.Name}");
                                Trade.AddItem(adminTrade.RequestedItem.Slot);

                                await Task.Delay(500);

                                LogDebug($"[ADMIN TRADE] Accepting trade");
                                Trade.Accept();
                            }
                            catch (Exception ex)
                            {
                                LogDebug($"[ADMIN TRADE] Error adding item to trade: {ex.Message}");
                            }
                        });
                    }
                    else
                    {
                        // This is a "give" trade - admin gives items to bot
                        LogDebug($"[ADMIN TRADE] Waiting for admin to add items to trade");
                        // Bot will wait for admin to add items and accept
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[ADMIN TRADE] Error in OnAdminTradeOpened: {ex.Message}");
            }
        }

        private static void OnAdminTradeStatusChanged(TradeStatus status)
        {
            try
            {
                LogDebug($"[ADMIN TRADE] Trade status changed to: {status}");

                // Check if this is an admin trade - we need to get the current trade target
                var currentTarget = Trade.CurrentTarget;
                if (currentTarget == null) return;

                var adminTrade = _pendingAdminTrades.Values.FirstOrDefault(t => t.PlayerId == currentTarget.Instance);
                if (adminTrade != null)
                {
                    switch (status)
                    {
                        case TradeStatus.Accept:
                            LogDebug($"[ADMIN TRADE] Player {adminTrade.PlayerName} accepted trade");

                            if (adminTrade.RequestedItem == null)
                            {
                                // This is a "give" trade - admin is giving items to bot
                                // Bot should also accept the trade
                                Task.Run(async () =>
                                {
                                    await Task.Delay(500);
                                    try
                                    {
                                        LogDebug($"[ADMIN TRADE] Bot accepting give trade");
                                        Trade.Accept();
                                    }
                                    catch (Exception ex)
                                    {
                                        LogDebug($"[ADMIN TRADE] Error accepting give trade: {ex.Message}");
                                    }
                                });
                            }
                            else
                            {
                                // This is a "get" trade - confirm the trade
                                Task.Run(async () =>
                                {
                                    await Task.Delay(500);
                                    try
                                    {
                                        LogDebug($"[ADMIN TRADE] Confirming trade");
                                        Trade.Confirm();
                                    }
                                    catch (Exception ex)
                                    {
                                        LogDebug($"[ADMIN TRADE] Error confirming trade: {ex.Message}");
                                    }
                                });
                            }
                            break;

                        case TradeStatus.Finished:
                            LogDebug($"[ADMIN TRADE] Trade completed with {adminTrade.PlayerName}");

                            if (adminTrade.RequestedItem == null)
                            {
                                // This was a "give" trade - restore items to original locations
                                Task.Run(async () =>
                                {
                                    await Task.Delay(1000); // Give time for items to appear in inventory
                                    await RestoreAdminItems(adminTrade.PlayerName);
                                });
                            }

                            // Clean up the pending trade
                            _pendingAdminTrades.Remove(adminTrade.PlayerName);
                            break;

                        case TradeStatus.None:
                            LogDebug($"[ADMIN TRADE] Trade cancelled/declined with {adminTrade.PlayerName}");

                            // Clean up the pending trade
                            _pendingAdminTrades.Remove(adminTrade.PlayerName);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[ADMIN TRADE] Error in OnAdminTradeStatusChanged: {ex.Message}");
            }
        }

        private static void SaveAdminItemLocations()
        {
            try
            {
                string locationsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "admin_item_locations.json");
                string json = JsonConvert.SerializeObject(_adminItemLocations, Formatting.Indented);
                File.WriteAllText(locationsPath, json);
                LogDebug($"[ADMIN LOCATION] Saved {_adminItemLocations.Count} item locations to file");
            }
            catch (Exception ex)
            {
                LogDebug($"[ADMIN LOCATION] Error saving item locations: {ex.Message}");
            }
        }

        private static async Task RestoreAdminItems(string playerName)
        {
            try
            {
                LogDebug($"[ADMIN RESTORE] Starting item restoration for {playerName}");

                // Find items that need to be restored for this player
                var itemsToRestore = _adminItemLocations.Values
                    .Where(loc => loc.PlayerName.Equals(playerName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (!itemsToRestore.Any())
                {
                    LogDebug($"[ADMIN RESTORE] No items to restore for {playerName}");
                    return;
                }

                LogDebug($"[ADMIN RESTORE] Found {itemsToRestore.Count} items to restore for {playerName}");

                // Get current inventory items
                var currentItems = Inventory.Items.ToList();

                foreach (var locationInfo in itemsToRestore)
                {
                    // Find the item in current inventory by ID (since we're using item.Id as the identifier)
                    var item = currentItems.FirstOrDefault(i => (uint)i.Id == locationInfo.ItemInstance);
                    if (item == null)
                    {
                        LogDebug($"[ADMIN RESTORE] Item with ID {locationInfo.ItemInstance} not found in inventory");
                        continue;
                    }

                    LogDebug($"[ADMIN RESTORE] Restoring {item.Name} to {locationInfo.LocationType}");

                    try
                    {
                        switch (locationInfo.LocationType)
                        {
                            case ItemLocationType.MainInventory:
                                // Item should go back to main inventory at specific slot
                                await RestoreToMainInventory(item, locationInfo);
                                break;

                            case ItemLocationType.Container:
                                // Item should go back to specific container
                                await RestoreToContainer(item, locationInfo);
                                break;

                            default:
                                LogDebug($"[ADMIN RESTORE] Unknown location type: {locationInfo.LocationType}");
                                break;
                        }

                        // Remove from tracking after successful restoration
                        _adminItemLocations.Remove(locationInfo.ItemInstance);
                        LogDebug($"[ADMIN RESTORE] Successfully restored {item.Name}");
                    }
                    catch (Exception ex)
                    {
                        LogDebug($"[ADMIN RESTORE] Error restoring {item.Name}: {ex.Message}");
                    }
                }

                // Clean up old location data (older than 24 hours)
                CleanupOldLocationData();

                // Save updated location data
                SaveAdminItemLocations();

                LogDebug($"[ADMIN RESTORE] Item restoration completed for {playerName}");
            }
            catch (Exception ex)
            {
                LogDebug($"[ADMIN RESTORE] Error in RestoreAdminItems for {playerName}: {ex.Message}");
            }
        }

        private static async Task RestoreToMainInventory(Item item, AdminItemLocation locationInfo)
        {
            try
            {
                LogDebug($"[ADMIN RESTORE] Restoring {item.Name} to main inventory (original slot: {locationInfo.OriginalSlotInstance})");

                // If item is already in main inventory, it's already in the right place
                if (item.Slot.Type == IdentityType.Inventory)
                {
                    LogDebug($"[ADMIN RESTORE] Item {item.Name} is already in main inventory");
                    return;
                }

                // Move item to main inventory
                item.MoveToInventory();
                await Task.Delay(200); // Give time for move to complete

                LogDebug($"[ADMIN RESTORE] Successfully moved {item.Name} to main inventory");
            }
            catch (Exception ex)
            {
                LogDebug($"[ADMIN RESTORE] Error restoring to main inventory: {ex.Message}");
            }
        }

        private static async Task RestoreToContainer(Item item, AdminItemLocation locationInfo)
        {
            try
            {
                LogDebug($"[ADMIN RESTORE] Restoring {item.Name} to container {locationInfo.ContainerName}");

                // Find the target container by name
                var targetContainer = Inventory.Containers.FirstOrDefault(c =>
                    c.Item.Name.Equals(locationInfo.ContainerName, StringComparison.OrdinalIgnoreCase));

                if (targetContainer == null)
                {
                    // Try to find by container ID if name match fails
                    if (locationInfo.ContainerInstance.HasValue)
                    {
                        targetContainer = Inventory.Containers.FirstOrDefault(c =>
                            (uint)c.Item.Id == locationInfo.ContainerInstance.Value);
                    }
                }

                if (targetContainer == null)
                {
                    LogDebug($"[ADMIN RESTORE] Target container {locationInfo.ContainerName} not found or not open - moving to main inventory instead");
                    item.MoveToInventory();
                    await Task.Delay(200);
                    return;
                }

                // Check if container has space
                if (targetContainer.Items.Count() >= 21)
                {
                    LogDebug($"[ADMIN RESTORE] Container {locationInfo.ContainerName} is full - moving to main inventory instead");
                    item.MoveToInventory();
                    await Task.Delay(200);
                    return;
                }

                // Move item to container
                LogDebug($"[ADMIN RESTORE] Moving {item.Name} to container {locationInfo.ContainerName}");
                item.MoveToContainer(targetContainer);
                await Task.Delay(200); // Give time for move to complete

                LogDebug($"[ADMIN RESTORE] Successfully moved {item.Name} to container {locationInfo.ContainerName}");
            }
            catch (Exception ex)
            {
                LogDebug($"[ADMIN RESTORE] Error restoring to container: {ex.Message}");
                // Fallback: move to main inventory if container restoration fails
                try
                {
                    item.MoveToInventory();
                    await Task.Delay(200);
                    LogDebug($"[ADMIN RESTORE] Moved {item.Name} to main inventory as fallback");
                }
                catch (Exception fallbackEx)
                {
                    LogDebug($"[ADMIN RESTORE] Fallback move also failed: {fallbackEx.Message}");
                }
            }
        }

        private static void CleanupOldLocationData()
        {
            try
            {
                var cutoffTime = DateTime.Now.AddHours(-24); // Remove data older than 24 hours
                var itemsToRemove = _adminItemLocations.Where(kvp => kvp.Value.TrackedAt < cutoffTime).ToList();

                if (itemsToRemove.Any())
                {
                    foreach (var item in itemsToRemove)
                    {
                        _adminItemLocations.Remove(item.Key);
                    }
                    LogDebug($"[ADMIN LOCATION] Cleaned up {itemsToRemove.Count} old location entries");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[ADMIN LOCATION] Error cleaning up old location data: {ex.Message}");
            }
        }

        private static void LoadAdminItemLocations()
        {
            try
            {
                string locationsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "admin_item_locations.json");
                if (File.Exists(locationsPath))
                {
                    string json = File.ReadAllText(locationsPath);
                    var locations = JsonConvert.DeserializeObject<Dictionary<uint, AdminItemLocation>>(json);
                    if (locations != null)
                    {
                        _adminItemLocations = locations;
                        LogDebug($"[ADMIN LOCATION] Loaded {_adminItemLocations.Count} item locations from file");
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[ADMIN LOCATION] Error loading item locations: {ex.Message}");
                _adminItemLocations = new Dictionary<uint, AdminItemLocation>();
            }
        }

        private static void LogDebug(string message)
        {
            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [DEBUG] {message}");
        }

        /// <summary>
        /// Get all available commands for a player
        /// </summary>
        public static List<ConfigurableCommand> GetAvailableCommands(string playerName)
        {
            EnsureInitialized();

            return _config.Commands
                .Where(c => c.Enabled && CheckRankPermission(c.Rank, playerName))
                .ToList();
        }

        /// <summary>
        /// Add a new command dynamically
        /// </summary>
        public static async Task AddCommand(ConfigurableCommand command)
        {
            EnsureInitialized();
            
            _config.Commands.Add(command);
            await ConfigurationManager.SaveConfiguration("commands", _config);
            RegisterCommands();
            
            LogDebug($"[COMMAND HANDLER] Added new command: {command.Name}");
        }

        /// <summary>
        /// Check if a trade is an admin trade (get or give)
        /// </summary>
        public static bool IsAdminTrade(string playerName)
        {
            return _pendingAdminTrades.ContainsKey(playerName);
        }

        /// <summary>
        /// Clear an admin trade from the pending list (called when trade completes)
        /// </summary>
        public static void ClearAdminTrade(string playerName)
        {
            if (_pendingAdminTrades.ContainsKey(playerName))
            {
                _pendingAdminTrades.Remove(playerName);
                LogDebug($"[ADMIN TRADE] Cleared pending admin trade for {playerName}");
            }
        }

        /// <summary>
        /// Handle the clean command - opens trade for implant cleaning
        /// </summary>
        private static void HandleCleanCommand(string playerName)
        {
            try
            {
                LogDebug($"[CLEAN COMMAND] Processing clean command from {playerName}");

                // Mark this as a clean trade so it routes to ImplantCleaningRecipe
                CleanTradeManager.SetPendingCleanTrade(playerName);

                // Find the player
                var targetPlayer = DynelManager.Players.FirstOrDefault(p =>
                    p.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase));

                if (targetPlayer == null)
                {
                    LogDebug($"[CLEAN COMMAND] Player {playerName} not found");
                    CleanTradeManager.ClearPendingCleanTrade(playerName);
                    Modules.PrivateMessageModule.SendPrivateMessage(playerName, "❌ Could not find you. Please try again when you're closer.");
                    return;
                }

                LogDebug($"[CLEAN COMMAND] Found player {playerName}, opening trade");

                // Open trade with delay to ensure message is sent first
                System.Threading.Tasks.Task.Delay(500).ContinueWith(_ =>
                {
                    try
                    {
                        LogDebug($"[CLEAN COMMAND] Opening trade with {playerName} (ID: {targetPlayer.Identity.Instance})");
                        Trade.Open(targetPlayer.Identity);
                    }
                    catch (Exception ex)
                    {
                        LogDebug($"[CLEAN COMMAND] Error opening trade: {ex.Message}");
                        CleanTradeManager.ClearPendingCleanTrade(playerName);
                        Modules.PrivateMessageModule.SendPrivateMessage(playerName, "❌ Could not open trade. Please try again when you're closer.");
                    }
                });
            }
            catch (Exception ex)
            {
                LogDebug($"[CLEAN COMMAND] Error in HandleCleanCommand: {ex.Message}");
                CleanTradeManager.ClearPendingCleanTrade(playerName);
                Modules.PrivateMessageModule.SendPrivateMessage(playerName, "Error processing clean command. Please try again.");
            }
        }
    }

    /// <summary>
    /// Represents an item in the bot's inventory for admin viewing
    /// </summary>
    public class InventoryItem
    {
        public string Name { get; set; }
        public uint Id { get; set; }
        public int Quality { get; set; }
        public uint ItemInstance { get; set; }
        public IdentityType SlotType { get; set; }
        public int SlotInstance { get; set; }
        public string ContainerName { get; set; }
    }

    /// <summary>
    /// Represents a pending admin trade operation
    /// </summary>
    public class AdminTradeInfo
    {
        public string PlayerName { get; set; }
        public uint PlayerId { get; set; }
        public Item RequestedItem { get; set; }
        public DateTime InitiatedAt { get; set; }
    }

    /// <summary>
    /// Represents the original location of an item given to an admin
    /// </summary>
    public class AdminItemLocation
    {
        public uint ItemInstance { get; set; }
        public string PlayerName { get; set; }
        public IdentityType OriginalSlotType { get; set; }
        public int OriginalSlotInstance { get; set; }
        public string ContainerName { get; set; }
        public uint? ContainerInstance { get; set; }
        public ItemLocationType LocationType { get; set; }
        public DateTime TrackedAt { get; set; }
    }

    /// <summary>
    /// Enum for different types of item storage locations
    /// </summary>
    public enum ItemLocationType
    {
        MainInventory,
        Container,
        EquipmentSlot
    }
}
