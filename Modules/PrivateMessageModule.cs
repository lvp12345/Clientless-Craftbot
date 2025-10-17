using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AOSharp.Clientless;
using AOSharp.Clientless.Logging;
using AOSharp.Clientless.Chat;
using AOSharp.Common.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.ChatMessages;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Craftbot.Templates;
using Craftbot.Recipes;
using Craftbot.Core;

using Newtonsoft.Json;

namespace Craftbot.Modules
{
    // Internal recipe analysis result for PrivateMessageModule
    internal class InternalRecipeAnalysisResult
    {
        public bool RecipeFound { get; set; }
        public string RecipeType { get; set; }
        public string Stage { get; set; }
        public int ProcessedCount { get; set; }
    }

    // Trade logging data structure
    public class TradeLogEntry
    {
        public DateTime StartTime { get; set; }
        public string PlayerName { get; set; }
        public int PlayerId { get; set; }
        public int BagsReceived { get; set; }
        public List<string> ItemsProcessed { get; set; }
        public List<string> ProcessingResults { get; set; }
        public List<string> FailedItems { get; set; }
        public int BagsReturned { get; set; }
        public DateTime? EndTime { get; set; }
        public bool IsCompleted { get; set; }

        public TradeLogEntry()
        {
            ItemsProcessed = new List<string>();
            ProcessingResults = new List<string>();
            FailedItems = new List<string>();
            IsCompleted = false;
        }
    }

    // Detailed trade logging data structure
    public class DetailedTradeLogEntry
    {
        public DateTime StartTime { get; set; }
        public string PlayerName { get; set; }
        public int PlayerId { get; set; }

        // Items received from player
        public List<string> BagsReceived { get; set; }
        public Dictionary<string, List<string>> BagContents { get; set; } // Bag name -> list of items in bag
        public List<string> LooseItemsReceived { get; set; }

        // Items returned to player
        public List<string> BagsReturned { get; set; }
        public Dictionary<string, List<string>> ReturnedBagContents { get; set; } // Bag name -> list of items in bag
        public List<string> LooseItemsReturned { get; set; }

        // Processing details
        public List<string> ItemsProcessed { get; set; }
        public List<string> ProcessingResults { get; set; }
        public List<string> FailedItems { get; set; }

        public DateTime? EndTime { get; set; }
        public bool IsCompleted { get; set; }

        public DetailedTradeLogEntry()
        {
            BagsReceived = new List<string>();
            BagContents = new Dictionary<string, List<string>>();
            LooseItemsReceived = new List<string>();
            BagsReturned = new List<string>();
            ReturnedBagContents = new Dictionary<string, List<string>>();
            LooseItemsReturned = new List<string>();
            ItemsProcessed = new List<string>();
            ProcessingResults = new List<string>();
            FailedItems = new List<string>();
            IsCompleted = false;
        }
    }

    public static class PrivateMessageModule
    {
        public static bool IsEnabled { get; private set; } = true; // Always enabled like TeamInvite
        private static bool _debugMode = true; // Set to true to enable debug logging
        private static Dictionary<int, TradeSession> _activeTradeSessions = new Dictionary<int, TradeSession>();
        private static Dictionary<int, List<Item>> _playerBags = new Dictionary<int, List<Item>>(); // Track bags received from players
        private static Identity? _currentTradeTarget = null; // Track who we're currently trading with

        // Debug logging to file
        private static string _debugLogPath = System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
            "Control Panel", "logs", "craftbot_debug.log");
        private static readonly object _logLock = new object();

        // Trade queue system with proper state management
        private static Queue<TradeRequest> _tradeQueue = new Queue<TradeRequest>();
        private static BotState _currentBotState = BotState.Ready;
        private static int? _currentProcessingPlayer = null;
        private static HashSet<int> _autoDeclinedTrades = new HashSet<int>(); // Track auto-declined trades

        private static bool _isProcessingTrade => _currentBotState != BotState.Ready;

        // Track traded items like LootManager tracks looted items (name + timestamp)
        private static Dictionary<string, double> _tradedItems = new Dictionary<string, double>();
        private static List<Identity> _preTradeInventory = new List<Identity>(); // Track all items before trade

        // Track when new items are added during return trades
        private static Dictionary<int, bool> _newItemsAddedDuringReturn = new Dictionary<int, bool>();

        // Trade logging system
        private static Dictionary<int, TradeLogEntry> _currentTradeLogs = new Dictionary<int, TradeLogEntry>();

        // Detailed trade logging system
        private static Dictionary<int, DetailedTradeLogEntry> _detailedTradeLogs = new Dictionary<int, DetailedTradeLogEntry>();
        private static HashSet<int> _playersWithPendingReturn = new HashSet<int>(); // Track players who have items ready for return
        private static Dictionary<int, List<Item>> _pendingReturns = new Dictionary<int, List<Item>>(); // Track items waiting to be returned
        // Enhanced save/recovery system
        private static Dictionary<int, DateTime> _returnTimeouts = new Dictionary<int, DateTime>(); // Track when items should timeout
        private static Dictionary<string, SavedTradeData> _savedTrades = new Dictionary<string, SavedTradeData>(); // Persistent storage by player name
        private static readonly string _savedTradesFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", "Debug", "saved_trades.json");
        private static readonly TimeSpan _returnTimeout = TimeSpan.FromMinutes(30); // 30 minute timeout - much longer to be more persistent

        // Track the last player who traded each item (for leftover recovery)
        private static Dictionary<string, string> _itemToPlayerMapping = new Dictionary<string, string>(); // ItemName_ItemId -> PlayerName
        private static HashSet<int> _activeRecoveryTrades = new HashSet<int>(); // Track players currently in recovery trades
        private static HashSet<int> _looseItemOnlyTrades = new HashSet<int>(); // Track trades that only contained loose items

        // CLIENTLESS EVENT-DRIVEN ITEM DETECTION
        private static Dictionary<int, List<Item>> _tradedItemsByPlayer = new Dictionary<int, List<Item>>(); // PlayerId -> Items received
        private static bool _isTrackingItems = false; // Flag to track when we should capture item additions

        // Item name caching for improved name resolution
        private static Dictionary<string, string> _itemNameCache = new Dictionary<string, string>(); // ItemId_HighId_Ql -> ItemName

        // SIMPLE TRADE LOGGING: Bot's original inventory at startup
        private static HashSet<int> _originalBotInventory = new HashSet<int>(); // All item IDs that belong to the bot
        private static bool _originalInventoryCaptured = false;

        // TOOL PROTECTION: Track bot's items before trade to prevent giving them away
        private static HashSet<int> _botItemIds = new HashSet<int>(); // Item IDs that belong to the bot

        // Ranks-based authorization system
        private static readonly string _ranksFolder = GetDebugFolderPath("config/ranks");
        private static readonly string _adminRankFile = Path.Combine(GetDebugFolderPath("config/ranks"), "Admin.json");
        private static readonly string _tradeLogFile = GetDebugFolderPath("trade_logs.txt");
        private static readonly string _alienArmorLogFile = GetDebugFolderPath("alien_armor.log");
        private static DateTime _lastFileCheck = DateTime.MinValue;
        private static readonly TimeSpan _fileCheckInterval = TimeSpan.FromMinutes(5); // Check files every 5 minutes
        private static string _lastSenderPlayerId = null; // Store the real player ID for responses

        // Rate limiting removed - allow unlimited help command switching

        // REMOVED: Duplicate tool tracking system - now using centralized RecipeUtilities.ReturnToolsToOriginalBags()
        // Following RULE #2: There should be ONLY ONE place for tool finding logic

        // Track when we last sent distance messages to prevent spam
        private static Dictionary<int, DateTime> _lastDistanceMessage = new Dictionary<int, DateTime>();

        // Track return trade retry attempts for persistent retry system
        private static Dictionary<int, int> _returnRetryCount = new Dictionary<int, int>();

        // Helper method to get absolute path to logs folder files
        private static string GetDebugFolderPath(string fileName)
        {
            try
            {
                // Get the directory where this DLL is located
                string assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string assemblyDirectory = System.IO.Path.GetDirectoryName(assemblyLocation);

                // For logs, put them in Control Panel/logs
                if (fileName.EndsWith(".log") || fileName == "trade_logs.txt" || fileName == "alien_armor.log")
                {
                    string logsDir = System.IO.Path.Combine(assemblyDirectory, "Control Panel", "logs");
                    System.IO.Directory.CreateDirectory(logsDir);
                    string filePath = System.IO.Path.Combine(logsDir, System.IO.Path.GetFileName(fileName));
                    LogDebug($"[FILE PATH] {fileName} will be at: {filePath}");
                    return filePath;
                }

                // For config files, keep them in Control Panel/config
                string filePath2 = System.IO.Path.Combine(assemblyDirectory, "Control Panel", fileName);
                LogDebug($"[FILE PATH] {fileName} will be at: {filePath2}");
                return filePath2;
            }
            catch (Exception ex)
            {
                LogDebug($"[FILE PATH] Error getting debug folder path for {fileName}: {ex.Message}");
                return fileName; // Fallback to relative path
            }
        }

        // Death recovery settings
        private static readonly Vector3 _intermediatePosition = new Vector3(638.4f, 66.8f, 729.1f);
        private static readonly Vector3 _standingPosition = new Vector3(638.9f, 66.8f, 727.1f);
        private static readonly int _standingPlayfield = 800; // Borealis
        private static bool _isRecoveringFromDeath = false;
        private static DateTime _lastDeathCheck = DateTime.MinValue;



        public static void Initialize()
        {
            try
            {
                // Clear debug log file on startup to keep it current
                ClearDebugLog();

                // CRITICAL: Reset bot state on initialization to ensure clean startup
                _currentBotState = BotState.Ready;
                _currentProcessingPlayer = null;
                _autoDeclinedTrades.Clear();
                LogDebug($"[INIT] ===== CRAFTBOT STARTUP - {DateTime.Now:yyyy-MM-dd HH:mm:ss} =====");
                LogDebug($"[INIT] Bot state reset to Ready on initialization");

                // Initialize recipe manager
                LogInfo("[INIT] Initializing Recipe Manager...");
                RecipeManager.Initialize();
                LogInfo("[INIT] Recipe Manager initialized successfully");

                // Restart transaction manager removed for stability

                // Zone refresh and restart transaction logic removed for stability

                // Load ranks and configuration files
                LogInfo("[INIT] Loading configuration files...");
                EnsureRanksExist();
                LogInfo("[INIT] Configuration files loaded successfully");

                // Load saved trades from previous sessions
                LogInfo("[INIT] Loading saved trades from previous sessions...");
                LoadSavedTrades();
                LogInfo("[INIT] Saved trades loaded successfully");

                // Initialize unified message system
                LogInfo("[INIT] Initializing Unified Message Handler...");
                UnifiedMessageHandler.Initialize();
                LogInfo("[INIT] Unified Message Handler initialized successfully");

                // Register for chat message events
                LogInfo("[INIT] Registering event handlers...");
                Client.Chat.PrivateMessageReceived += (e, msg) => OnPrivateMessageReceived(msg);
                LogDebug("[INIT] Private message event handler registered");

                // Register for trade state changes - this is the correct AOSharp way
                Trade.TradeOpened += OnTradeOpened;
                Trade.TradeStatusChanged += OnTradeStatusChanged;
                LogDebug("[INIT] Trade event handlers registered");

                // Register for container opened events to track when bags are opened
                Inventory.ContainerOpened += OnContainerOpened;
                LogDebug("[INIT] Container event handlers registered");

                // CLIENTLESS EVENT-DRIVEN ITEM DETECTION
                Inventory.ItemAdded += OnItemAdded;
                Inventory.ItemRemoved += OnItemRemoved;
                LogDebug("[INIT] Item event handlers registered");

                // DIAGNOSTIC: Check ItemData loading capability
                TestItemDataLoading();

                // SIMPLE TRADE LOGGING: Capture bot's original inventory
                CaptureOriginalBotInventory();

                // Register for game update to monitor death and recovery
                Client.OnUpdate += OnGameUpdate;

                LogInfo("[INIT] Private Message Module: ALWAYS ACTIVE - Ready to receive commands via tells");
                LogInfo($"[INIT] Debug mode is: {(_debugMode ? "ON" : "OFF")}");
                LogInfo("[INIT] Send 'help' in any chat to test (debug mode will catch it)");
                LogInfo("=== CRAFTBOT PRIVATE MESSAGE MODULE READY ===");
                LogInfo($"[INIT] Initialization completed successfully at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            }
            catch (Exception ex)
            {
                LogDebug($"Private Message Module Error: {ex.Message}");
            }
        }



        public static void SetEnabled(bool enabled)
        {
            // Private Message Module is always enabled - this method kept for compatibility
            LogDebug("Private Message Module is ALWAYS ACTIVE and cannot be disabled.");
            LogDebug("This is the main utility for remote control via private messages.");
            LogDebug($"[PM Debug] Debug mode is: {(_debugMode ? "ON" : "OFF")}");
        }

        public static bool IsDebugMode()
        {
            return _debugMode;
        }

        public static async Task ProcessAllBagsManually(string processType)
        {
            try
            {
                LogDebug($"[MANUAL {processType.ToUpper()}] Starting manual processing of all bags");

                // Find all open containers
                var containers = Inventory.Containers.Where(c => c.IsOpen).ToList();

                if (!containers.Any())
                {
                    LogDebug($"[MANUAL {processType.ToUpper()}] No open bags found");
                    return;
                }

                int totalProcessed = 0;

                foreach (var container in containers)
                {
                    LogDebug($"[MANUAL {processType.ToUpper()}] Processing bag: {container.Item?.Name ?? "Unknown"}");

                    var bagContents = container.Items.ToList();
                    int bagProcessed = 0;


                    var result = await AnalyzeBagForRecipes(container, bagContents);

                    if (result.RecipeFound)
                    {
                        LogDebug($"[MANUAL {processType.ToUpper()}] Processed {result.RecipeType} recipe in {container.Item?.Name ?? "Unknown"}");
                        bagProcessed = result.ProcessedCount;
                        totalProcessed += bagProcessed;
                    }
                    else
                    {
                        LogDebug($"[MANUAL {processType.ToUpper()}] No recipes found in {container.Item?.Name ?? "Unknown"}");
                    }

                    LogDebug($"[MANUAL {processType.ToUpper()}] Processed {bagProcessed} items from {container.Item?.Name ?? "Unknown"}");
                }

                LogDebug($"[MANUAL {processType.ToUpper()}] Completed! Total items processed: {totalProcessed}");
            }
            catch (Exception ex)
            {
                LogDebug($"[MANUAL {processType.ToUpper()}] Error: {ex.Message}");
            }
        }

        private static void OnPrivateMessageReceived(PrivateMessage msg)
        {
            try
            {
                // Extract message info from clientless message
                string senderName = msg.SenderName;
                string content = msg.Message;

                if (!string.IsNullOrEmpty(senderName) && !string.IsNullOrEmpty(content))
                {
                    ProcessCommand(senderName, content);
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Error processing private message: {ex.Message}");
            }
        }

        private static string ExtractSimpleSenderName(ChatMessageBody message)
        {
            try
            {
                // Get the actual player ID from the message for sending responses
                string actualPlayerId = ExtractSenderName(message);

                // Store the real player ID for responses
                _lastSenderPlayerId = actualPlayerId;

                // Use the actual player ID as the sender name for rate limiting and authorization
                // This way each player gets their own rate limiting and authorization
                return actualPlayerId;
            }
            catch (Exception ex)
            {
                LogDebug($"[PM] Error extracting sender: {ex.Message}");
                return "Unknown";
            }
        }

        private static string FindSenderNameInMessage(ChatMessageBody message)
        {
            try
            {
                // Look through all properties to find one that contains a player name
                var properties = message.GetType().GetProperties();

                foreach (var prop in properties)
                {
                    var value = prop.GetValue(message);
                    if (value != null)
                    {
                        // Check if this property has a Name sub-property
                        var nameProperty = value.GetType().GetProperty("Name");
                        if (nameProperty != null)
                        {
                            var name = nameProperty.GetValue(value)?.ToString();
                            if (!string.IsNullOrEmpty(name) && !uint.TryParse(name, out _))
                            {
                                LogDebug($"[PM DEBUG] Found name '{name}' in property {prop.Name}");
                                return name;
                            }
                        }

                        // Check if the value itself looks like a player name (not a number)
                        string valueStr = value.ToString();
                        if (!string.IsNullOrEmpty(valueStr) && !uint.TryParse(valueStr, out _) && valueStr.Length > 2)
                        {
                            // Check if this name exists in our nearby players
                            if (DynelManager.Players.Any(p => p.Name.Equals(valueStr, StringComparison.OrdinalIgnoreCase)))
                            {
                                LogDebug($"[PM DEBUG] Found matching player name '{valueStr}' in property {prop.Name}");
                                return valueStr;
                            }
                        }
                    }
                }

                LogDebug($"[PM DEBUG] Could not find sender name in message properties");
                return "Unknown";
            }
            catch (Exception ex)
            {
                LogDebug($"[PM DEBUG] Error finding sender name: {ex.Message}");
                return "Unknown";
            }
        }



        private static void HandleGenericPrivateMessage(ChatMessageBody message)
        {
            try
            {
                // Extract sender and content from generic message
                string senderName = ExtractSenderName(message);
                string content = ExtractMessageContent(message);

                // Only process if we have a valid player name (not "Unknown")
                if (!string.IsNullOrEmpty(senderName) && senderName != "Unknown" && !string.IsNullOrEmpty(content))
                {
                    LogDebug($"[PM] Received from {senderName}: {content}");
                    ProcessCommand(senderName, content);
                }
                else
                {
                    LogDebug($"[PM DEBUG] Skipping message - invalid sender or content");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Error handling generic private message: {ex.Message}");
            }
        }

        private static string ExtractSenderName(ChatMessageBody message)
        {
            try
            {
                // Try various properties that might contain sender information
                var properties = message.GetType().GetProperties();

                LogDebug($"[PM DEBUG] Message type: {message.GetType().Name}");
                LogDebug($"[PM DEBUG] Available properties: {string.Join(", ", properties.Select(p => p.Name))}");

                foreach (var prop in properties)
                {
                    var value = prop.GetValue(message);
                    if (value != null)
                    {
                        LogDebug($"[PM DEBUG] Property {prop.Name}: {value} (Type: {value.GetType().Name})");

                        // If it's an Identity or similar object, try to get the name
                        var nameProperty = value.GetType().GetProperty("Name");
                        if (nameProperty != null)
                        {
                            var name = nameProperty.GetValue(value)?.ToString();
                            if (!string.IsNullOrEmpty(name))
                            {
                                LogDebug($"[PM DEBUG] Found name property: {name}");
                                return name;
                            }
                        }

                        // If we find a player ID, look up the name immediately
                        if (uint.TryParse(value.ToString(), out uint playerId))
                        {
                            var player = DynelManager.Players.FirstOrDefault(p => p.Identity.Instance == playerId);
                            if (player != null)
                            {
                                LogDebug($"[PM DEBUG] Converted player ID {playerId} to name: {player.Name}");
                                return player.Name;
                            }
                        }
                    }
                }

                LogDebug($"[PM DEBUG] Could not extract sender name from message");
                return "Unknown";
            }
            catch (Exception ex)
            {
                LogDebug($"[PM DEBUG] Error extracting sender name: {ex.Message}");
                return "Unknown";
            }
        }

        private static string ExtractMessageContent(ChatMessageBody message)
        {
            try
            {
                // Try various properties that might contain message content
                var properties = message.GetType().GetProperties();
                
                foreach (var prop in properties)
                {
                    if (prop.Name.ToLower().Contains("text") || 
                        prop.Name.ToLower().Contains("content") ||
                        prop.Name.ToLower().Contains("message"))
                    {
                        var value = prop.GetValue(message);
                        if (value != null)
                        {
                            return value.ToString();
                        }
                    }
                }
                
                return "";
            }
            catch
            {
                return "";
            }
        }

        private static void CheckAndReloadFiles()
        {
            try
            {
                if (DateTime.Now - _lastFileCheck > _fileCheckInterval)
                {
                    // LogDebug("[FILE CHECK] Checking for file updates..."); // Hidden for cleaner logs
                    EnsureRanksExist();
                    _lastFileCheck = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[FILE CHECK] Error checking files: {ex.Message}");
            }
        }

        private static void ProcessCommand(string senderName, string content)
        {
            try
            {
                // LogDebug($"[PM] Received message from {senderName}: '{content}'"); // Hidden for cleaner logs (except for Hive messages)

                // Store the last sender for potential trade operations
                _lastSenderPlayerId = senderName;

                // Check if we need to reload files (but not for return command to avoid spam)
                string[] parts = content.Trim().Split(' ');
                if (parts.Length > 0 && !parts[0].ToLower().Equals("return"))
                {
                    CheckAndReloadFiles();
                }

                // UNIFIED MESSAGE PROCESSING: Route all messages through unified system
                LogDebug($"[PM] Routing message to unified system: '{content}' from {senderName}");
                UnifiedMessageHandler.ProcessMessage(_lastSenderPlayerId ?? senderName, content);
            }
            catch (Exception ex)
            {
                LogDebug($"Error processing command: {ex.Message}");
            }
        }

        // Rate limiting method removed - unlimited help command usage allowed

        public static bool IsUserAuthorized(string username)
        {
            if (string.IsNullOrEmpty(username)) return false;

            // Check if user is in Admin rank
            return IsAdmin(username);
        }

        public static void SendPrivateMessage(string targetName, string message, bool addPrefix = true)
        {
            try
            {
                // Try to find the player by ID first (since targetName might be an ID)
                uint playerId = 0;

                if (uint.TryParse(targetName, out playerId))
                {
                    // Use the player ID directly
                    Client.SendPrivateMessage(playerId, message);
                }
                else
                {
                    // targetName is a name, try to find the player ID
                    var targetPlayer = DynelManager.Players
                        .FirstOrDefault(p => p.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase));

                    if (targetPlayer != null)
                    {
                        Client.SendPrivateMessage((uint)targetPlayer.Identity.Instance, message);
                    }
                    else
                    {
                        LogDebug($"[PM] Player {targetName} not found nearby, cannot send private message");
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Error sending private message to {targetName}: {ex.Message}");
                // Fallback: Just log to debug
                LogDebug($"[PM] Would send to {targetName}: {message}");
            }
        }



        public static int? GetCurrentProcessingPlayer()
        {
            return _currentProcessingPlayer;
        }

        // PUBLIC WRAPPERS FOR UNIFIED MESSAGE SYSTEM
        public static bool IsProcessingTrade()
        {
            return _isProcessingTrade;
        }

        // Command Handlers

        private static void HandleEnableCommand(string sender, string[] args)
        {
            if (args.Length == 0)
            {
                SendPrivateMessage(sender, "Usage: enable <module>. Type 'modules' for available modules.");
                return;
            }

            string moduleName = args[0].ToLower();
            bool success = false;
            string message = "";

            try
            {
                switch (moduleName)
                {
                    default:
                        message = $"Manual modules have been removed. Only automatic trade processing is available.";
                        break;
                }

                if (success)
                {
                    Craftbot.Config.Save();
                    message = $"{moduleName} module enabled.";
                }
            }
            catch (Exception ex)
            {
                message = $"Error enabling {moduleName}: {ex.Message}";
            }

            SendPrivateMessage(sender, message);
        }

        private static void HandleDisableCommand(string sender, string[] args)
        {
            if (args.Length == 0)
            {
                SendPrivateMessage(sender, "Usage: disable <module>. Type 'modules' for available modules.");
                return;
            }

            string moduleName = args[0].ToLower();
            bool success = false;
            string message = "";

            try
            {
                switch (moduleName)
                {
                    default:
                        message = $"Manual modules have been removed. Only automatic trade processing is available.";
                        break;
                }

                if (success)
                {
                    Craftbot.Config.Save();
                    message = $"{moduleName} module disabled.";
                }
            }
            catch (Exception ex)
            {
                message = $"Error disabling {moduleName}: {ex.Message}";
            }

            SendPrivateMessage(sender, message);
        }











        public static void HandleDebugCommand(string sender, string[] args)
        {
            try
            {
                if (args.Length > 0)
                {
                    string subCommand = args[0].ToLower();
                    switch (subCommand)
                    {
                        case "inventory":
                            // Show comprehensive inventory statistics
                            var stats = Core.ItemTracker.GetInventoryStats();
                            SendPrivateMessage(sender, $"Inventory Stats: {stats}");
                            LogDebug($"[PM] Inventory stats requested by {sender}: {stats}");
                            break;

                        case "test":
                            // Run comprehensive inventory tracking test
                            var testResults = Core.ItemTracker.TestInventoryTracking();
                            SendPrivateMessage(sender, testResults);
                            LogDebug($"[PM] Inventory tracking test requested by {sender}");
                            break;

                        // Network logging debug commands removed for cleaner operation

                        case "analyze":
                            // Simple inventory analysis without network logging
                            var itemCount = Inventory.Items?.Count() ?? 0;
                            var bagCount = Inventory.Items?.Where(i => i.UniqueIdentity.Type == IdentityType.Container).Count() ?? 0;
                            SendPrivateMessage(sender, $"Inventory: {itemCount} items, {bagCount} bags");
                            LogDebug($"[PM] Simple inventory analysis: {itemCount} items, {bagCount} bags");
                            break;

                        case "bagstatus":
                            // Show current bag status
                            var bagStatus = GetBagStatus();
                            SendPrivateMessage(sender, bagStatus);
                            LogDebug($"[PM] Bag status requested by {sender}: {bagStatus}");
                            break;



                        case "itemdata":
                            // Simple ItemData test without network logging
                            if (args.Length > 1 && int.TryParse(args[1], out int itemId))
                            {
                                if (ItemData.Find(itemId, out DummyItem itemData))
                                {
                                    SendPrivateMessage(sender, $"Item ID {itemId}: {itemData.Name}");
                                    LogDebug($"[PM] ItemData test: ID {itemId} = {itemData.Name}");
                                }
                                else
                                {
                                    SendPrivateMessage(sender, $"Item ID {itemId} not found in ItemData");
                                    LogDebug($"[PM] ItemData test: ID {itemId} not found");
                                }
                            }
                            else
                            {
                                SendPrivateMessage(sender, "Usage: debug itemdata <itemId>");
                            }
                            break;

                        default:
                            SendPrivateMessage(sender, "Debug commands: inventory, test, netlog, netlogstop, analyze, itemdata <id>, or just 'debug' to toggle mode.");
                            break;
                    }
                }
                else
                {
                    _debugMode = !_debugMode;
                    string status = _debugMode ? "enabled" : "disabled";
                    SendPrivateMessage(sender, $"Debug mode {status}. Use 'debug help' for available commands.");
                    LogDebug($"[PM] Debug mode {status} by {sender}");
                }
            }
            catch (Exception ex)
            {
                SendPrivateMessage(sender, $"Error with debug command: {ex.Message}");
            }
        }

        private static void HandleModulesCommand(string sender, string[] args)
        {
            try
            {
                var modules = "Available Modules:\n";
                modules += "pearl - Pearl processing\n";
                modules += "plasma - Plasma processing\n";
                modules += "relay - Relay processing\n";
                modules += "ice - Ice processing\n";
                modules += "cookies - Cookie processing\n";
                modules += "coffee - Coffee processing\n";
                modules += "tradehelper - Trade helper functionality";

                SendPrivateMessage(sender, modules);
            }
            catch (Exception ex)
            {
                SendPrivateMessage(sender, $"Error listing modules: {ex.Message}");
            }
        }



        private static void HandleQueueCommand(string sender, string[] args)
        {
            try
            {
                LogDebug($"[PM] {sender} requested queue status");

                if (_tradeQueue.Count == 0)
                {
                    if (_isProcessingTrade && _currentProcessingPlayer.HasValue)
                    {
                        string currentPlayerName = DynelManager.Players
                            .FirstOrDefault(p => p.Identity.Instance == _currentProcessingPlayer.Value)?.Name ?? "Unknown";
                        SendPrivateMessage(sender, $"Queue is empty. Currently processing: {currentPlayerName}");
                    }
                    else
                    {
                        SendPrivateMessage(sender, "Queue is empty. No trades in progress. You can trade immediately!");
                    }
                    return;
                }

                // Build queue status message
                var queueList = _tradeQueue.ToArray();
                string queueStatus = $"Trade Queue ({queueList.Length} waiting):\n";

                if (_isProcessingTrade && _currentProcessingPlayer.HasValue)
                {
                    string currentPlayerName = DynelManager.Players
                        .FirstOrDefault(p => p.Identity.Instance == _currentProcessingPlayer.Value)?.Name ?? "Unknown";
                    queueStatus += $"Currently processing: {currentPlayerName}\n";
                }

                for (int i = 0; i < queueList.Length; i++)
                {
                    var request = queueList[i];
                    var waitTime = DateTime.Now - request.RequestTime;
                    queueStatus += $"{i + 1}. {request.PlayerName} (waiting {waitTime.Minutes}m {waitTime.Seconds}s)\n";
                }

                SendPrivateMessage(sender, queueStatus.TrimEnd());
            }
            catch (Exception ex)
            {
                LogDebug($"Error handling queue command: {ex.Message}");
                SendPrivateMessage(sender, "Error checking queue status.");
            }
        }

        public static void AddToTradeQueue(int playerId, string playerName, Vector3 position)
        {
            try
            {
                LogDebug($"[QUEUE] Attempting to add player {playerId} ({playerName}) to trade queue");

                // Check if player is already in queue
                if (_tradeQueue.Any(req => req.PlayerId == playerId))
                {
                    LogWarning($"[QUEUE] Player {playerId} ({playerName}) is already in queue - sending duplicate message");
                    SendPrivateMessage(playerId.ToString(), "You are already in the trade queue. Please wait your turn.");
                    return;
                }

                // Add to queue
                var request = new TradeRequest
                {
                    PlayerId = playerId,
                    PlayerName = playerName,
                    RequestTime = DateTime.Now,
                    LastKnownPosition = position
                };

                _tradeQueue.Enqueue(request);

                int queuePosition = _tradeQueue.Count;
                LogInfo($"[QUEUE] Added {playerName} to trade queue (position {queuePosition})");
                LogDebug($"[QUEUE] Player position: {position}, Request time: {DateTime.Now:HH:mm:ss}");

                SendPrivateMessage(playerId.ToString(),
                    $"I'm currently working on an order. You are #{queuePosition} in queue. I'll message you when it's your turn!");
                LogDebug($"[QUEUE] Queue notification sent to {playerName}");
            }
            catch (Exception ex)
            {
                LogDebug($"[QUEUE] Error adding player to queue: {ex.Message}");
                SendPrivateMessage(playerId.ToString(), "Error adding you to queue. Please try again.");
            }
        }

        private static void RemovePlayerFromQueue(int playerId)
        {
            try
            {
                // Convert queue to list, remove the player, then recreate queue
                var queueList = _tradeQueue.ToList();
                var originalCount = queueList.Count;

                queueList.RemoveAll(req => req.PlayerId == playerId);

                if (queueList.Count < originalCount)
                {
                    // Player was removed from queue
                    _tradeQueue.Clear();
                    foreach (var request in queueList)
                    {
                        _tradeQueue.Enqueue(request);
                    }

                    LogDebug($"[QUEUE] Removed player {playerId} from queue (was at some position, now queue has {queueList.Count} players)");
                }
                else
                {
                    LogDebug($"[QUEUE] Player {playerId} was not in queue (no removal needed)");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[QUEUE] Error removing player {playerId} from queue: {ex.Message}");
            }
        }

        public static void StartTradeWithPlayer(PlayerChar targetPlayer, string sender)
        {
            try
            {
                LogDebug($"[PM] Opening trade with {targetPlayer.Name} (distance: {targetPlayer.DistanceFrom(DynelManager.LocalPlayer):F1}m)");
                LogDebug($"[QUEUE DEBUG] Before opening trade - _isProcessingTrade: {_isProcessingTrade}, _currentProcessingPlayer: {_currentProcessingPlayer}");

                // DON'T mark as processing yet - wait until trade is actually accepted
                // This prevents the race condition where we auto-decline our own trade

                // Use the player's identity to open trade
                Trade.Open(targetPlayer.Identity);

                // NOTE: Message will be sent by HandleTradeOpened() when trade window actually opens
                LogDebug($"[QUEUE DEBUG] Trade opened, waiting for acceptance before marking as processing");
            }
            catch (Exception ex)
            {
                LogDebug($"[PM] Error starting trade: {ex.Message}");
                SendPrivateMessage(sender, "Error opening trade. Please try again.");
            }
        }



        private static void ProcessNextInQueue()
        {
            try
            {
                LogDebug($"[QUEUE DEBUG] ProcessNextInQueue called - _isProcessingTrade: {_isProcessingTrade}, _currentProcessingPlayer: {_currentProcessingPlayer}");

                // Safety check: Don't process queue if we're already processing a trade
                if (_isProcessingTrade)
                {
                    LogDebug($"[QUEUE] Already processing a trade with player {_currentProcessingPlayer} - skipping queue processing");
                    return;
                }

                // FIX: Additional safety check - if bot state is Ready but _currentProcessingPlayer is set, clear it
                if (_currentBotState == BotState.Ready && _currentProcessingPlayer.HasValue)
                {
                    LogDebug($"[QUEUE FIX] Bot state is Ready but _currentProcessingPlayer is set to {_currentProcessingPlayer.Value} - clearing stale data");
                    _currentProcessingPlayer = null;
                }

                if (_tradeQueue.Count == 0)
                {
                    LogDebug("[QUEUE] No more players in queue");
                    return;
                }

                var nextRequest = _tradeQueue.Dequeue();
                LogDebug($"[QUEUE] Processing next player: {nextRequest.PlayerName} (ID: {nextRequest.PlayerId})");

                // Try to find the player
                var targetPlayer = DynelManager.Players
                    .FirstOrDefault(p => p.Identity.Instance == nextRequest.PlayerId);

                if (targetPlayer != null)
                {
                    // Check distance
                    float distance = targetPlayer.DistanceFrom(DynelManager.LocalPlayer);

                    if (distance <= 10f)
                    {
                        // Start trade with next player
                        StartTradeWithPlayer(targetPlayer, nextRequest.PlayerId.ToString());
                        // NOTE: Trade opening message will be sent by HandleTradeOpened() when trade window actually opens
                    }
                    else
                    {
                        // Player moved away, notify and try next
                        SendPrivateMessage(nextRequest.PlayerId.ToString(),
                            $"It's your turn but you're too far away ({distance:F1}m). Please come closer and type 'trade' again.");

                        // Try next player immediately
                        ProcessNextInQueue();
                    }
                }
                else
                {
                    // Player not found, notify and try next
                    SendPrivateMessage(nextRequest.PlayerId.ToString(),
                        "It's your turn but you're not in the area. Please come back and type 'trade' again.");

                    // Try next player immediately
                    ProcessNextInQueue();
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[QUEUE] Error processing next in queue: {ex.Message}");

                // Reset state and try again
                _currentBotState = BotState.Ready;
                _currentProcessingPlayer = null;

                // Try next player
                if (_tradeQueue.Count > 0)
                {
                    Task.Delay(1000).ContinueWith(_ => ProcessNextInQueue());
                }
            }
        }

        private static void HandleQueueDebugCommand(string sender, string[] args)
        {
            try
            {
                LogDebug($"[PM] {sender} requested queue debug info");

                string debugInfo = "=== QUEUE DEBUG INFO ===\n";
                debugInfo += $"_isProcessingTrade: {_isProcessingTrade}\n";
                debugInfo += $"_currentProcessingPlayer: {(_currentProcessingPlayer?.ToString() ?? "null")}\n";
                debugInfo += $"Queue count: {_tradeQueue.Count}\n";
                debugInfo += $"Active trade sessions: {_activeTradeSessions.Count}\n";

                if (Trade.CurrentTarget != Identity.None)
                {
                    debugInfo += $"Currently trading with: {Trade.CurrentTarget}\n";
                }
                else
                {
                    debugInfo += "Not currently in trade\n";
                }

                debugInfo += "\nQueue contents:\n";
                var queueList = _tradeQueue.ToArray();
                for (int i = 0; i < queueList.Length; i++)
                {
                    var request = queueList[i];
                    var waitTime = DateTime.Now - request.RequestTime;
                    debugInfo += $"{i + 1}. {request.PlayerName} (ID: {request.PlayerId}, waiting {waitTime.Minutes}m {waitTime.Seconds}s)\n";
                }

                if (queueList.Length == 0)
                {
                    debugInfo += "Queue is empty\n";
                }

                LogDebug(debugInfo);
                SendPrivateMessage(sender, debugInfo);
            }
            catch (Exception ex)
            {
                LogDebug($"Error handling queue debug command: {ex.Message}");
                SendPrivateMessage(sender, "Error getting queue debug info.");
            }
        }

        private static void HandleQueueResetCommand(string sender, string[] args)
        {
            try
            {
                LogDebug($"[PM] {sender} requested manual queue reset");

                // Check if sender is admin
                if (!IsAdmin(sender))
                {
                    SendPrivateMessage(sender, "Only admins can reset the queue state.");
                    return;
                }

                LogDebug($"[QUEUE DEBUG] Before manual reset - _isProcessingTrade: {_isProcessingTrade}, _currentProcessingPlayer: {_currentProcessingPlayer}");

                // Force reset the queue state
                _currentBotState = BotState.Ready;
                _currentProcessingPlayer = null;

                LogDebug($"[QUEUE DEBUG] After manual reset - _isProcessingTrade: {_isProcessingTrade}, _currentProcessingPlayer: {_currentProcessingPlayer}");

                SendPrivateMessage(sender, "Queue state has been manually reset. Bot is now ready for new trades.");
                LogDebug($"[QUEUE] Manual queue reset completed by {sender}");
            }
            catch (Exception ex)
            {
                LogDebug($"Error handling queue reset command: {ex.Message}");
                SendPrivateMessage(sender, "Error resetting queue state.");
            }
        }

        private static void HandleInviteCommand(string sender, string[] args)
        {
            if (args.Length == 0)
            {
                SendPrivateMessage(sender, "Usage: invite <playername>");
                return;
            }

            try
            {
                string playerName = args[0];

                LogDebug($"[PM] {sender} requested team invite for {playerName}");

                // Try to find the player first
                PlayerChar targetPlayer = DynelManager.Players
                    .FirstOrDefault(p => p.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase));

                if (targetPlayer != null)
                {
                    // Found locally, send invite directly
                    Team.Invite(new Identity(IdentityType.SimpleChar, targetPlayer.Identity.Instance));
                    SendPrivateMessage(sender, $"Team invite sent to {playerName}");
                }
                else
                {
                    // Not found locally, would need to use lookup system like TeamInviteModule does
                    LogDebug($"[PM] Player {playerName} not found locally, would need character lookup");
                    SendPrivateMessage(sender, $"Player {playerName} not found in current area. Manual invite required.");
                }
            }
            catch (Exception ex)
            {
                SendPrivateMessage(sender, $"Error sending invite: {ex.Message}");
            }
        }

        private static void HandleAcceptCommand(string sender, string[] args)
        {
            try
            {
                LogDebug($"[PM] Manual accept command from {sender}");

                if (Trade.CurrentTarget != Identity.None)
                {
                    LogDebug($"[PM] Current trade target: {Trade.CurrentTarget}");
                    Trade.Accept();
                    LogDebug($"[PM] Manual accept sent successfully");
                    SendPrivateMessage(sender, "Accept command sent!");
                }
                else
                {
                    LogDebug($"[PM] No active trade target for manual accept");
                    SendPrivateMessage(sender, "No active trade to accept.");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Error in manual accept: {ex.Message}");
                SendPrivateMessage(sender, $"Error: {ex.Message}");
            }
        }



        private static void HandleConfirmCommand(string sender, string[] args)
        {
            try
            {
                LogDebug($"[PM] Manual confirm command from {sender}");

                if (Trade.CurrentTarget != Identity.None)
                {
                    LogDebug($"[PM] Current trade target: {Trade.CurrentTarget}");
                    Trade.Confirm();
                    LogDebug($"[PM] Manual confirm sent successfully");
                    SendPrivateMessage(sender, "Confirm command sent!");
                }
                else
                {
                    LogDebug($"[PM] No active trade target for manual confirm");
                    SendPrivateMessage(sender, "No active trade to confirm.");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Error in manual confirm: {ex.Message}");
                SendPrivateMessage(sender, $"Error: {ex.Message}");
            }
        }

        // File-Based Admin and Funny Response System Methods

        private static bool IsAdmin(string playerName)
        {
            if (string.IsNullOrEmpty(playerName)) return false;

            try
            {
                // Check if Admin rank file exists
                if (!File.Exists(_adminRankFile))
                {
                    return false;
                }

                // Read the Admin rank file
                string jsonContent = File.ReadAllText(_adminRankFile);
                dynamic rankData = Newtonsoft.Json.JsonConvert.DeserializeObject(jsonContent);

                if (rankData?.players == null)
                {
                    return false;
                }

                // Check if player is in the Admin rank
                foreach (var player in rankData.players)
                {
                    if (player.ToString().Equals(playerName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                LogDebug($"[RANKS] Error checking admin status for {playerName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if a player is in a specific rank
        /// </summary>
        public static bool IsPlayerInRank(string playerName, string rankName)
        {
            if (string.IsNullOrEmpty(playerName) || string.IsNullOrEmpty(rankName)) return false;

            try
            {
                string rankFile = Path.Combine(_ranksFolder, $"{rankName}.json");

                // Check if rank file exists
                if (!File.Exists(rankFile))
                {
                    return false;
                }

                // Read the rank file
                string jsonContent = File.ReadAllText(rankFile);
                dynamic rankData = Newtonsoft.Json.JsonConvert.DeserializeObject(jsonContent);

                if (rankData?.players == null)
                {
                    return false;
                }

                // Check if player is in this rank
                foreach (var player in rankData.players)
                {
                    if (player.ToString().Equals(playerName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                LogDebug($"[RANKS] Error checking if {playerName} is in rank {rankName}: {ex.Message}");
                return false;
            }
        }

        private static void EnsureRanksExist()
        {
            try
            {
                // Create ranks folder if it doesn't exist
                if (!Directory.Exists(_ranksFolder))
                {
                    Directory.CreateDirectory(_ranksFolder);
                    LogDebug($"[RANKS] Created ranks folder: {_ranksFolder}");
                }

                // Create default rank files if they don't exist
                string[] defaultRanks = { "Admin", "Moderator", "VIP", "User" };
                foreach (var rank in defaultRanks)
                {
                    string rankFile = Path.Combine(_ranksFolder, $"{rank}.json");
                    if (!File.Exists(rankFile))
                    {
                        var rankData = new { rank = rank, players = new string[] { } };
                        string json = Newtonsoft.Json.JsonConvert.SerializeObject(rankData, Newtonsoft.Json.Formatting.Indented);
                        File.WriteAllText(rankFile, json);
                        LogDebug($"[RANKS] Created default rank file: {rankFile}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[RANKS] Error ensuring ranks exist: {ex.Message}");
            }
        }

        private static void HandleReloadCommand(string sender, string[] args)
        {
            try
            {
                LogDebug($"[RELOAD] Manual file reload requested by {sender}");
                EnsureRanksExist();
                _lastFileCheck = DateTime.Now;
                SendPrivateMessage(sender, "Files reloaded successfully!");
            }
            catch (Exception ex)
            {
                SendPrivateMessage(sender, $"Error reloading files: {ex.Message}");
            }
        }

        public static void HandleAlienArmorLogCommand(string sender, string[] args)
        {
            try
            {
                // Check if sender is admin
                if (!IsAdmin(sender))
                {
                    SendPrivateMessage(sender, "Only admins can view alien armor logs.");
                    LogDebug($"[AI LOG] Non-admin {sender} attempted to access alien armor log");
                    return;
                }

                LogDebug($"[AI LOG] Admin {sender} requested alien armor log");

                // Check if file exists
                if (!File.Exists(_alienArmorLogFile))
                {
                    SendPrivateMessage(sender, "No alien armor log file found. No alien armor has been processed yet.");
                    LogDebug($"[AI LOG] Alien armor log file does not exist");
                    return;
                }

                // Read the log file
                var logLines = File.ReadAllLines(_alienArmorLogFile);

                if (logLines.Length == 0)
                {
                    SendPrivateMessage(sender, "Alien armor log is empty.");
                    return;
                }

                // Parse and format the log into a help-menu style response
                var formattedLog = FormatAlienArmorLogForDisplay(logLines);

                // Send the formatted log
                SendPrivateMessage(sender, formattedLog);
                LogDebug($"[AI LOG] Sent alien armor log to {sender} ({formattedLog.Length} characters)");
            }
            catch (Exception ex)
            {
                SendPrivateMessage(sender, $"Error reading alien armor log: {ex.Message}");
                LogDebug($"[AI LOG] Error: {ex.Message}");
            }
        }

        private static string FormatAlienArmorLogForDisplay(string[] logLines)
        {
            try
            {
                var result = new System.Text.StringBuilder();
                result.AppendLine("<a href=\"text://");
                result.AppendLine("<font color=#00D4FF>===============================================</font>");
                result.AppendLine("<font color=#00D4FF>      ALIEN ARMOR PROCESSING LOG SUMMARY     </font>");
                result.AppendLine("<font color=#00D4FF>===============================================</font>");
                result.AppendLine("");

                // Count total trades
                int totalTrades = 0;
                var recentTrades = new List<string>();

                for (int i = 0; i < logLines.Length; i++)
                {
                    if (logLines[i].Contains("=== ALIEN ARMOR TRADE LOG ==="))
                    {
                        totalTrades++;

                        // Extract trade details (next few lines after header)
                        var tradeInfo = new System.Text.StringBuilder();
                        tradeInfo.Append("<font color=#FFFF00>Trade #" + totalTrades + ":</font> ");

                        // Look for Date, Player, Duration in next lines
                        for (int j = i + 1; j < Math.Min(i + 10, logLines.Length); j++)
                        {
                            if (logLines[j].StartsWith("Date:"))
                            {
                                tradeInfo.Append(logLines[j].Replace("Date:", "").Trim());
                            }
                            else if (logLines[j].StartsWith("Player:"))
                            {
                                tradeInfo.Append(" - " + logLines[j].Replace("Player:", "").Trim());
                            }
                            else if (logLines[j].StartsWith("Duration:"))
                            {
                                tradeInfo.Append(" - " + logLines[j].Replace("Duration:", "").Trim());
                                break;
                            }
                        }

                        recentTrades.Add(tradeInfo.ToString());

                        // Only keep last 10 trades
                        if (recentTrades.Count > 10)
                        {
                            recentTrades.RemoveAt(0);
                        }
                    }
                }

                result.AppendLine($"<font color=#00FF00>Total Alien Armor Trades: {totalTrades}</font>");
                result.AppendLine("");

                if (recentTrades.Count > 0)
                {
                    result.AppendLine("<font color=#FFFF00>=== RECENT TRADES (Last " + Math.Min(10, totalTrades) + ") ===</font>");
                    foreach (var trade in recentTrades)
                    {
                        result.AppendLine(trade);
                    }
                }
                else
                {
                    result.AppendLine("<font color=#FF6600>No trades found in log.</font>");
                }

                result.AppendLine("");
                result.AppendLine("<font color=#888888>Use this log to track alien armor processing history.</font>");
                result.AppendLine("<font color=#888888>Full log available at: alien_armor.log</font>");
                result.AppendLine("");
                result.AppendLine("<font color=#00D4FF>===============================================</font>");
                result.AppendLine("\">Alien Armor Log Summary</a>");

                return result.ToString();
            }
            catch (Exception ex)
            {
                LogDebug($"[AI LOG] Error formatting log: {ex.Message}");
                return "Error formatting alien armor log.";
            }
        }

        public static void HandleReturnCommand(string sender, string[] args)
        {
            try
            {
                LogDebug($"[RETURN] Return command received from {sender}");

                // Load saved trades to check for this player (without triggering file check spam)
                LogDebug($"[RETURN] Loading saved trades...");
                LoadSavedTrades();
                LogDebug($"[RETURN] Loaded {_savedTrades.Count} saved trades from file");

                // Debug: List all saved trade keys
                if (_savedTrades.Count > 0)
                {
                    LogDebug($"[RETURN] Available saved trade keys: {string.Join(", ", _savedTrades.Keys)}");
                }

                // Get player ID for additional lookup
                var player = DynelManager.Players.FirstOrDefault(p => p.Name.Equals(sender, StringComparison.OrdinalIgnoreCase));
                string playerIdString = player?.Identity.Instance.ToString();
                LogDebug($"[RETURN] Player lookup - Name: {sender}, ID: {playerIdString ?? "not found"}, Player found: {player != null}");

                SavedTradeData savedTrade = null;
                string foundKey = null;

                // Check if player has saved items by name first
                if (_savedTrades.ContainsKey(sender))
                {
                    savedTrade = _savedTrades[sender];
                    foundKey = sender;
                    LogDebug($"[RETURN] Found saved trade by player name: {sender}");
                }
                // If not found by name, try by player ID (for timeout cases)
                else if (playerIdString != null && _savedTrades.ContainsKey(playerIdString))
                {
                    savedTrade = _savedTrades[playerIdString];
                    foundKey = playerIdString;
                    LogDebug($"[RETURN] Found saved trade by player ID: {playerIdString}");
                }
                else
                {
                    LogDebug($"[RETURN] No saved trade found for name '{sender}' or ID '{playerIdString}'");
                }

                if (savedTrade != null)
                {
                    // Check if trade hasn't expired (optional safety check)
                    if (DateTime.Now > savedTrade.TimeoutTime)
                    {
                        LogDebug($"[RETURN] Saved trade for {sender} has expired, removing");
                        _savedTrades.Remove(foundKey);
                        SaveSavedTrades();
                        SendPrivateMessage(sender, "Your saved items have expired and been removed from the system.");
                        return;
                    }

                    LogDebug($"[RETURN] Found saved trade for {sender}, attempting to restore and return items");
                    Task.Run(async () => await RestoreAndReturnSavedTrade(sender, savedTrade));
                }
                else
                {
                    SendPrivateMessage(sender, "No saved items found for your character. If you recently had a trade interrupted, please wait a moment and try again.");
                    LogDebug($"[RETURN] No saved trade found for {sender} (ID: {playerIdString ?? "unknown"})");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[RETURN] Error handling return command: {ex.Message}");
                SendPrivateMessage(sender, "Error processing return request. Please try again later.");
            }
        }

        /// <summary>
        /// Handle status command to check pending returns and retry counts
        /// </summary>
        public static void HandleStatusCommand(string senderName, string[] arguments)
        {
            try
            {
                LogDebug($"[STATUS] Status command received from {senderName}");

                // Find player ID
                var player = DynelManager.Players.FirstOrDefault(p => p.Name.Equals(senderName, StringComparison.OrdinalIgnoreCase));
                if (player == null)
                {
                    SendPrivateMessage(senderName, "Error: Could not find your player information.");
                    return;
                }

                int playerId = player.Identity.Instance;

                // Check if player has pending returns
                if (_pendingReturns.ContainsKey(playerId))
                {
                    var pendingItems = _pendingReturns[playerId];
                    var retryCount = _returnRetryCount.ContainsKey(playerId) ? _returnRetryCount[playerId] : 0;

                    string statusMessage = $"📦 Status: You have {pendingItems.Count} processed item(s) waiting for return.";

                    if (retryCount > 0)
                    {
                        statusMessage += $" I've tried to return them {retryCount} time(s) already.";
                    }

                    statusMessage += " I'll keep trying to trade them back to you automatically. Please accept the trade when you're ready!";

                    SendPrivateMessage(senderName, statusMessage);
                    LogDebug($"[STATUS] Player {senderName} has {pendingItems.Count} pending items, {retryCount} retry attempts");
                }
                else
                {
                    // Check if they have saved items in recovery system
                    LoadSavedTrades();
                    if (_savedTrades.ContainsKey(senderName))
                    {
                        var savedTrade = _savedTrades[senderName];
                        SendPrivateMessage(senderName,
                            $"💾 Status: You have {savedTrade.Items.Count} item(s) saved in the recovery system from {savedTrade.SaveTime:yyyy-MM-dd HH:mm}. " +
                            "Use '/tell " + Client.CharacterName + " return' to retrieve them.");
                        LogDebug($"[STATUS] Player {senderName} has {savedTrade.Items.Count} items in recovery system");
                    }
                    else
                    {
                        SendPrivateMessage(senderName, "✅ Status: You have no pending returns or saved items. All clear!");
                        LogDebug($"[STATUS] Player {senderName} has no pending or saved items");
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[STATUS] Error handling status command from {senderName}: {ex.Message}");
                SendPrivateMessage(senderName, "Error checking your status. Please try again later.");
            }
        }



        private static void StartTradeLog(int playerId, string playerName, int bagsReceived)
        {
            try
            {
                var tradeLog = new TradeLogEntry
                {
                    StartTime = DateTime.Now,
                    PlayerName = playerName,
                    PlayerId = playerId,
                    BagsReceived = bagsReceived
                };

                _currentTradeLogs[playerId] = tradeLog;

                // Also start detailed trade log
                var detailedTradeLog = new DetailedTradeLogEntry
                {
                    StartTime = DateTime.Now,
                    PlayerName = playerName,
                    PlayerId = playerId
                };

                _detailedTradeLogs[playerId] = detailedTradeLog;
                LogDebug($"[TRADE LOG] Started trade log for {playerName} (ID: {playerId}) with {bagsReceived} bags");
            }
            catch (Exception ex)
            {
                LogDebug($"[TRADE LOG] Error starting trade log: {ex.Message}");
            }
        }

        private static void LogProcessedItem(int playerId, string itemName, string result)
        {
            try
            {
                if (_currentTradeLogs.ContainsKey(playerId))
                {
                    var log = _currentTradeLogs[playerId];
                    log.ItemsProcessed.Add(itemName);
                    log.ProcessingResults.Add(result);
                    LogDebug($"[TRADE LOG] Logged processed item for {log.PlayerName}: {itemName} -> {result}");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[TRADE LOG] Error logging processed item: {ex.Message}");
            }
        }

        private static void LogFailedItem(int playerId, string itemName, string reason)
        {
            try
            {
                if (_currentTradeLogs.ContainsKey(playerId))
                {
                    var log = _currentTradeLogs[playerId];
                    log.FailedItems.Add($"{itemName} - {reason}");
                    LogDebug($"[TRADE LOG] Logged failed item for {log.PlayerName}: {itemName} - {reason}");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[TRADE LOG] Error logging failed item: {ex.Message}");
            }
        }

        private static void CompleteTradeLog(int playerId, int bagsReturned)
        {
            try
            {
                if (_currentTradeLogs.ContainsKey(playerId))
                {
                    var log = _currentTradeLogs[playerId];
                    log.BagsReturned = bagsReturned;
                    log.EndTime = DateTime.Now;
                    log.IsCompleted = true;

                    // Write to file
                    WriteTradeLogToFile(log);

                    // Check if this was an alien armor trade and write to alien armor log
                    if (log.ItemsProcessed.Any(item => item.Contains("Alien Armor") || item.Contains("Viralbot") || item.Contains("Bio-Material")))
                    {
                        WriteAlienArmorLogToFile(log);
                        LogDebug($"[ALIEN ARMOR LOG] Written alien armor trade log for {log.PlayerName}");
                    }

                    // Remove from current logs
                    _currentTradeLogs.Remove(playerId);

                    LogDebug($"[TRADE LOG] Completed trade log for {log.PlayerName}");
                }

                // Also complete detailed trade log (now integrated into main trade log)
                if (_detailedTradeLogs.ContainsKey(playerId))
                {
                    var detailedLog = _detailedTradeLogs[playerId];
                    detailedLog.EndTime = DateTime.Now;
                    detailedLog.IsCompleted = true;
                    // Note: Detailed log is now written as part of WriteTradeLogToFile above
                    // Remove from current logs after main log is written
                    _detailedTradeLogs.Remove(playerId);
                    LogDebug($"[DETAILED LOG] Completed detailed trade log for {detailedLog.PlayerName}");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[TRADE LOG] Error completing trade log: {ex.Message}");
            }
        }

        private static string GetPlayerName(int playerId)
        {
            try
            {
                var player = DynelManager.Players.FirstOrDefault(p => p.Identity.Instance == playerId);
                return player?.Name ?? $"Player_{playerId}";
            }
            catch
            {
                return $"Player_{playerId}";
            }
        }

        /// <summary>
        /// Public method to write alien armor trade log (called from AlienArmorRecipe)
        /// </summary>
        public static void WriteAlienArmorLog(int playerId)
        {
            try
            {
                if (_currentTradeLogs.ContainsKey(playerId))
                {
                    var log = _currentTradeLogs[playerId];
                    WriteAlienArmorLogToFile(log);
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[ALIEN ARMOR LOG] Error writing alien armor log: {ex.Message}");
            }
        }

        private static void WriteAlienArmorLogToFile(TradeLogEntry log)
        {
            try
            {
                var duration = log.EndTime.HasValue ? (log.EndTime.Value - log.StartTime).TotalSeconds : 0;
                var logEntry = new List<string>
                {
                    $"===============================================",
                    $"=== ALIEN ARMOR TRADE LOG ===",
                    $"Date: {log.StartTime:yyyy-MM-dd HH:mm:ss}",
                    $"Player: {log.PlayerName} (ID: {log.PlayerId})",
                    $"Duration: {duration:F1} seconds",
                    $"Status: {(log.IsCompleted ? "Completed" : "Incomplete")}",
                    $"",
                    $"--- ITEMS RECEIVED FROM PLAYER ---"
                };

                // Get detailed trade log if available
                DetailedTradeLogEntry detailedLog = null;
                if (_detailedTradeLogs.ContainsKey(log.PlayerId))
                {
                    detailedLog = _detailedTradeLogs[log.PlayerId];
                }

                if (detailedLog != null)
                {
                    // Log received bags and their contents
                    if (detailedLog.BagsReceived.Count > 0)
                    {
                        logEntry.Add($"Bags Received ({detailedLog.BagsReceived.Count}):");
                        foreach (var bagName in detailedLog.BagsReceived)
                        {
                            logEntry.Add($"  📦 {bagName}");
                            if (detailedLog.BagContents.ContainsKey(bagName))
                            {
                                var contents = detailedLog.BagContents[bagName];
                                logEntry.Add($"     Contents ({contents.Count} items):");
                                foreach (var item in contents)
                                {
                                    logEntry.Add($"       - {item}");
                                }
                            }
                            logEntry.Add("");
                        }
                    }
                    else
                    {
                        logEntry.Add("Bags Received: None");
                    }

                    // Log received loose items
                    if (detailedLog.LooseItemsReceived.Count > 0)
                    {
                        logEntry.Add($"Loose Items Received ({detailedLog.LooseItemsReceived.Count}):");
                        foreach (var item in detailedLog.LooseItemsReceived)
                        {
                            logEntry.Add($"  - {item}");
                        }
                    }
                    else
                    {
                        logEntry.Add("Loose Items Received: None");
                    }

                    logEntry.Add("");
                    logEntry.Add($"--- ITEMS RETURNED TO PLAYER ---");

                    // Log returned bags and their contents
                    if (detailedLog.BagsReturned.Count > 0)
                    {
                        logEntry.Add($"Bags Returned ({detailedLog.BagsReturned.Count}):");
                        foreach (var bagName in detailedLog.BagsReturned)
                        {
                            logEntry.Add($"  📦 {bagName}");
                            if (detailedLog.ReturnedBagContents.ContainsKey(bagName))
                            {
                                var contents = detailedLog.ReturnedBagContents[bagName];
                                logEntry.Add($"     Contents ({contents.Count} items):");
                                foreach (var item in contents)
                                {
                                    logEntry.Add($"       - {item}");
                                }
                            }
                            logEntry.Add("");
                        }
                    }
                    else
                    {
                        logEntry.Add("Bags Returned: None");
                    }

                    // Log returned loose items
                    if (detailedLog.LooseItemsReturned.Count > 0)
                    {
                        logEntry.Add($"Loose Items Returned ({detailedLog.LooseItemsReturned.Count}):");
                        foreach (var item in detailedLog.LooseItemsReturned)
                        {
                            logEntry.Add($"  - {item}");
                        }
                    }
                    else
                    {
                        logEntry.Add("Loose Items Returned: None");
                    }
                }
                else
                {
                    // Fallback to basic info if detailed log not available
                    logEntry.Add($"Bags Received: {log.BagsReceived}");
                    logEntry.Add($"Bags Returned: {log.BagsReturned}");
                    logEntry.Add("(Detailed item tracking not available)");
                }

                // Log processing details
                if (log.ItemsProcessed.Count > 0)
                {
                    logEntry.Add("");
                    logEntry.Add($"--- PROCESSING DETAILS ---");
                    logEntry.Add($"Items Processed ({log.ItemsProcessed.Count}):");
                    for (int i = 0; i < log.ItemsProcessed.Count; i++)
                    {
                        string result = i < log.ProcessingResults.Count ? log.ProcessingResults[i] : "Unknown result";
                        logEntry.Add($"  - {log.ItemsProcessed[i]} -> {result}");
                    }

                    if (log.FailedItems.Count > 0)
                    {
                        logEntry.Add($"Failed Items ({log.FailedItems.Count}):");
                        foreach (var failedItem in log.FailedItems)
                        {
                            logEntry.Add($"  - {failedItem}");
                        }
                    }
                }

                logEntry.Add("");
                logEntry.Add($"===============================================");
                logEntry.Add("");

                // Append to alien armor log file
                File.AppendAllLines(_alienArmorLogFile, logEntry);
                LogDebug($"[ALIEN ARMOR LOG] Written alien armor trade log to file for {log.PlayerName}");
            }
            catch (Exception ex)
            {
                LogDebug($"[ALIEN ARMOR LOG] Error writing alien armor log to file: {ex.Message}");
            }
        }

        private static void WriteTradeLogToFile(TradeLogEntry log)
        {
            try
            {
                var duration = log.EndTime.HasValue ? (log.EndTime.Value - log.StartTime).TotalSeconds : 0;
                var logEntry = new List<string>
                {
                    $"===============================================",
                    $"=== DETAILED TRADE LOG ===",
                    $"Date: {log.StartTime:yyyy-MM-dd HH:mm:ss}",
                    $"Player: {log.PlayerName} (ID: {log.PlayerId})",
                    $"Duration: {duration:F1} seconds",
                    $"Status: {(log.IsCompleted ? "Completed" : "Incomplete")}",
                    $"",
                    $"--- ITEMS RECEIVED FROM PLAYER ---"
                };

                // Get detailed trade log if available
                DetailedTradeLogEntry detailedLog = null;
                if (_detailedTradeLogs.ContainsKey(log.PlayerId))
                {
                    detailedLog = _detailedTradeLogs[log.PlayerId];
                }

                if (detailedLog != null)
                {
                    // Log received bags and their contents
                    if (detailedLog.BagsReceived.Count > 0)
                    {
                        logEntry.Add($"Bags Received ({detailedLog.BagsReceived.Count}):");
                        foreach (var bagName in detailedLog.BagsReceived)
                        {
                            logEntry.Add($"  📦 {bagName}");
                            if (detailedLog.BagContents.ContainsKey(bagName))
                            {
                                var contents = detailedLog.BagContents[bagName];
                                logEntry.Add($"     Contents ({contents.Count} items):");
                                foreach (var item in contents)
                                {
                                    logEntry.Add($"       - {item}");
                                }
                            }
                            logEntry.Add("");
                        }
                    }
                    else
                    {
                        logEntry.Add("Bags Received: None");
                    }

                    // Log received loose items
                    if (detailedLog.LooseItemsReceived.Count > 0)
                    {
                        logEntry.Add($"Loose Items Received ({detailedLog.LooseItemsReceived.Count}):");
                        foreach (var item in detailedLog.LooseItemsReceived)
                        {
                            logEntry.Add($"  - {item}");
                        }
                    }
                    else
                    {
                        logEntry.Add("Loose Items Received: None");
                    }

                    logEntry.Add("");
                    logEntry.Add($"--- ITEMS RETURNED TO PLAYER ---");

                    // Log returned bags and their contents
                    if (detailedLog.BagsReturned.Count > 0)
                    {
                        logEntry.Add($"Bags Returned ({detailedLog.BagsReturned.Count}):");
                        foreach (var bagName in detailedLog.BagsReturned)
                        {
                            logEntry.Add($"  📦 {bagName}");
                            if (detailedLog.ReturnedBagContents.ContainsKey(bagName))
                            {
                                var contents = detailedLog.ReturnedBagContents[bagName];
                                logEntry.Add($"     Contents ({contents.Count} items):");
                                foreach (var item in contents)
                                {
                                    logEntry.Add($"       - {item}");
                                }
                            }
                            logEntry.Add("");
                        }
                    }
                    else
                    {
                        logEntry.Add("Bags Returned: None");
                    }

                    // Log returned loose items
                    if (detailedLog.LooseItemsReturned.Count > 0)
                    {
                        logEntry.Add($"Loose Items Returned ({detailedLog.LooseItemsReturned.Count}):");
                        foreach (var item in detailedLog.LooseItemsReturned)
                        {
                            logEntry.Add($"  - {item}");
                        }
                    }
                    else
                    {
                        logEntry.Add("Loose Items Returned: None");
                    }
                }
                else
                {
                    // Fallback to basic info if detailed log not available
                    logEntry.Add($"Bags Received: {log.BagsReceived}");
                    logEntry.Add($"Bags Returned: {log.BagsReturned}");
                    logEntry.Add("(Detailed item tracking not available)");
                }

                // Log processing details
                if (log.ItemsProcessed.Count > 0)
                {
                    logEntry.Add("");
                    logEntry.Add($"--- PROCESSING DETAILS ---");
                    logEntry.Add($"Items Processed ({log.ItemsProcessed.Count}):");
                    for (int i = 0; i < log.ItemsProcessed.Count; i++)
                    {
                        string result = i < log.ProcessingResults.Count ? log.ProcessingResults[i] : "Unknown result";
                        logEntry.Add($"  - {log.ItemsProcessed[i]} -> {result}");
                    }

                    if (log.FailedItems.Count > 0)
                    {
                        logEntry.Add($"Failed Items ({log.FailedItems.Count}):");
                        foreach (var failedItem in log.FailedItems)
                        {
                            logEntry.Add($"  - {failedItem}");
                        }
                    }
                }

                logEntry.Add("");
                logEntry.Add($"===============================================");
                logEntry.Add("");

                // Append to file
                File.AppendAllLines(_tradeLogFile, logEntry);
                LogDebug($"[TRADE LOG] Written detailed trade log to file for {log.PlayerName}");
            }
            catch (Exception ex)
            {
                LogDebug($"[TRADE LOG] Error writing trade log to file: {ex.Message}");
            }
        }

        // Detailed trade logging functions
        private static void LogReceivedBag(int playerId, string bagName, List<string> bagContents)
        {
            try
            {
                if (_detailedTradeLogs.ContainsKey(playerId))
                {
                    var log = _detailedTradeLogs[playerId];
                    log.BagsReceived.Add(bagName);
                    log.BagContents[bagName] = new List<string>(bagContents);
                    LogDebug($"[DETAILED LOG] Logged received bag: {bagName} with {bagContents.Count} items");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[DETAILED LOG] Error logging received bag: {ex.Message}");
            }
        }

        private static void LogReceivedLooseItem(int playerId, string itemName)
        {
            try
            {
                if (_detailedTradeLogs.ContainsKey(playerId))
                {
                    var log = _detailedTradeLogs[playerId];
                    log.LooseItemsReceived.Add(itemName);
                    LogDebug($"[DETAILED LOG] Logged received loose item: {itemName}");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[DETAILED LOG] Error logging received loose item: {ex.Message}");
            }
        }

        private static void LogReturnedBag(int playerId, string bagName, List<string> bagContents)
        {
            try
            {
                if (_detailedTradeLogs.ContainsKey(playerId))
                {
                    var log = _detailedTradeLogs[playerId];
                    log.BagsReturned.Add(bagName);
                    log.ReturnedBagContents[bagName] = new List<string>(bagContents);
                    LogDebug($"[DETAILED LOG] Logged returned bag: {bagName} with {bagContents.Count} items");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[DETAILED LOG] Error logging returned bag: {ex.Message}");
            }
        }

        private static void LogReturnedLooseItem(int playerId, string itemName)
        {
            try
            {
                if (_detailedTradeLogs.ContainsKey(playerId))
                {
                    var log = _detailedTradeLogs[playerId];
                    log.LooseItemsReturned.Add(itemName);
                    LogDebug($"[DETAILED LOG] Logged returned loose item: {itemName}");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[DETAILED LOG] Error logging returned loose item: {ex.Message}");
            }
        }



        public static void HandleTradeMessage(TradeMessage tradeMsg)
        {
            try
            {
                var tradeAction = tradeMsg.Action;
                var targetIdentity = new Identity((IdentityType)tradeMsg.Param1, tradeMsg.Param2);

                // Always log decline messages for debugging return trade issues
                if (tradeAction == TradeAction.Decline)
                {
                    LogDebug($"[NETWORK] Received trade message: Decline");
                    LogDebug($"[TRADE RAW] Decline detected - Target: {targetIdentity}, CurrentTradeTarget: {(_currentTradeTarget?.ToString() ?? "NULL")}");
                }

                // Only log debug messages if debug mode is enabled
                if (_debugMode)
                {
                    // LogDebug($"[TRADE RAW] Action: {tradeAction}, Target: {targetIdentity}, Params: {tradeMsg.Param1}/{tradeMsg.Param2}/{tradeMsg.Param3}/{tradeMsg.Param4}");
                    // LogDebug($"[TRADE RAW] Current TradeTarget: {(Trade.CurrentTarget != Identity.None ? Trade.CurrentTarget.ToString() : "NULL")}");
                }

                // Always check for different trade actions (regardless of debug mode)
                if (Trade.CurrentTarget != Identity.None)
                {
                    var ourIdentity = DynelManager.LocalPlayer.Identity;

                    if (tradeAction == TradeAction.Accept)
                    {
                        if (_debugMode)
                        {
                            // LogDebug($"[TRADE RAW] Our identity: {ourIdentity}");
                            // LogDebug($"[TRADE RAW] Message target: {targetIdentity}");
                            // LogDebug($"[TRADE RAW] TradeTarget: {Trade.CurrentTarget}");
                        }

                        if (targetIdentity == ourIdentity)
                        {
                            if (_debugMode)
                            {
                                // LogDebug($"[TRADE RAW] This is OUR accept message being echoed back");
                            }
                        }
                        else if (targetIdentity == Trade.CurrentTarget)
                        {
                            if (_debugMode)
                            {
                                LogDebug($"[TRADE RAW] This is the OTHER PLAYER's accept message");
                                LogDebug($"[TRADE RAW] Responding with our accept");
                            }

                            // CHECK: Don't accept if bot is busy processing
                            if (_isProcessingTrade && _currentProcessingPlayer.HasValue)
                            {
                                LogDebug($"[TRADE RAW] Bot is busy processing - declining instead of accepting");
                                Trade.Decline();
                                return;
                            }

                            // Try accepting here instead of in TradeStateChanged
                            Trade.Accept();

                            if (_debugMode)
                                LogDebug($"[TRADE RAW] Accept sent in response to raw message");
                        }
                    }
                    else if (tradeAction == TradeAction.OtherPlayerAddItem)
                    {
                        if (_debugMode)
                            LogDebug($"[TRADE RAW] Other player added item - checking for auto-accept");

                        // Always check if this is happening during a return trade (regardless of debug mode)
                        if (Trade.CurrentTarget != Identity.None && Trade.CurrentTarget.Type == IdentityType.SimpleChar)
                        {
                            int playerId = Trade.CurrentTarget.Instance;
                            if (_pendingReturns.ContainsKey(playerId))
                            {
                                LogDebug($"[TRADE] Player {playerId} added new items during return trade - will treat as new trade");
                                _newItemsAddedDuringReturn[playerId] = true;
                            }

                            // Send message when items are added
                            var playerName = GetPlayerName(playerId);
                            SendPrivateMessage(playerId.ToString(), $"I see you added items to the trade, {playerName}! Please accept when ready.");
                        }

                        // Auto-accept when items are added
                        Trade.Accept();
                        if (_debugMode)
                            LogDebug($"[TRADE RAW] Auto-accept sent after item added");
                    }
                    else if (tradeAction == TradeAction.Decline)
                    {
                        LogDebug($"[TRADE RAW] Decline action detected in network handler");
                        // Force call to TradeStatusChanged handler since it might not be firing
                        OnTradeStatusChanged(TradeStatus.None);
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[TRADE] Error in HandleTradeMessage: {ex.Message}");
            }
        }

        private static void HandleTradeOpened(int playerId)
        {
            try
            {
                var playerName = GetPlayerName(playerId);
                LogInfo($"[TRADE] Trade window opened with player {playerId} ({playerName})");

                // Create or update trade session
                _activeTradeSessions[playerId] = new TradeSession
                {
                    PlayerId = playerId,
                    State = TradeState.Opened,
                    StartTime = DateTime.Now
                };
                LogDebug($"[TRADE] Trade session created for player {playerId}");

                // Start detailed trade logging session
                Core.TradeLogger.StartTradeSession(playerId, playerName);
                LogDebug($"[TRADE] Trade logging session started for {playerName}");

                // Check if this is a return trade - if so, don't send the opening message
                bool isReturnTrade = _pendingReturns.ContainsKey(playerId) &&
                                   _currentBotState == BotState.Returning &&
                                   _currentProcessingPlayer == playerId;

                if (!isReturnTrade)
                {
                    // Send standard trade opening message
                    LogInfo($"[TRADE] Sending trade opening message to {playerName}");
                    SendPrivateMessage(playerId.ToString(), "Trade window opened! Put items in the trade window if you want me to process them, then accept the trade.");
                    LogDebug($"[TRADE] Trade opening message sent to player {playerId}");
                }
                else
                {
                    LogInfo($"[TRADE] Return trade opened for player {playerId} ({playerName}) - skipping opening message");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Error handling trade opened: {ex.Message}");
            }
        }





        private static async Task HandleTradeCompleted(int playerId)
        {
            try
            {
                await Task.Delay(1); // Satisfy async requirement
                // LogDebug($"[PM] Trade completed with player {playerId}"); // Commented out to reduce spam

                // Clean up trade session
                if (_activeTradeSessions.ContainsKey(playerId))
                {
                    var session = _activeTradeSessions[playerId];
                    session.State = TradeState.Completed;
                    session.EndTime = DateTime.Now;

                    var duration = session.EndTime.Value - session.StartTime;
                    // LogDebug($"[PM] Trade session lasted {duration.TotalSeconds:F1} seconds"); // Commented out to reduce spam

                    _activeTradeSessions.Remove(playerId);
                }

                // Complete detailed trade logging session
                Core.TradeLogger.CompleteTradeSession(playerId);

                // Clear timeout since trade completed successfully
                if (_returnTimeouts.ContainsKey(playerId))
                {
                    _returnTimeouts.Remove(playerId);
                    // LogDebug($"[TIMEOUT] Cleared return timeout for player {playerId} - trade completed successfully"); // Commented out to reduce spam
                }

                // Check if this is a return trade (giving processed items back) or initial trade (receiving items)
                bool isReturnTrade = _pendingReturns.ContainsKey(playerId);

                if (isReturnTrade)
                {
                    // This is a return trade - we're giving processed items back to the player
                    // LogDebug($"[PM] Return trade completed for player {playerId}"); // Commented out to reduce spam

                    // Calculate what was returned and send detailed message
                    var returnedItems = _pendingReturns.ContainsKey(playerId) ? _pendingReturns[playerId] : new List<Item>();
                    int bagCount = returnedItems.Count(item => item.UniqueIdentity.Type == IdentityType.Container);
                    int looseItemCount = returnedItems.Count(item => item.UniqueIdentity.Type != IdentityType.Container);

                    string returnMessage = $"Your {bagCount} bag(s) and {looseItemCount} loose item(s) were returned! Thank you for using my services!";
                    SendPrivateMessage(playerId.ToString(), returnMessage);

                    // Complete trade log with bags returned count
                    var bagsReturned = _pendingReturns.ContainsKey(playerId) ? _pendingReturns[playerId].Count : 0;
                    CompleteTradeLog(playerId, bagsReturned);

                    // STATE TRANSITION: Returning → Ready
                    if (_currentProcessingPlayer == playerId)
                    {
                        _currentBotState = BotState.Ready;
                        _currentProcessingPlayer = null;

                        // Clear the current trade's received items tracking
                        Core.ItemTracker.ClearCurrentTradeItems();

                        LogDebug($"[BOT STATE] Return trade completed - changed to Ready");
                        LogDebug($"[QUEUE] Bot is now ready for new trades");

                        // Process any queued players
                        ProcessNextInQueue();
                    }
                }
                else
                {
                    // This is an initial trade - we're receiving items from the player
                    // NOTE: Don't try to detect items immediately - let the ProcessTradeItems method handle it
                    // The immediate detection often fails due to timing issues with the game client
                    LogDebug($"[PM] New trade completed for player {playerId} - item detection will be handled by ProcessTradeItems");

                    // Start trade log with 0 items for now - will be updated when items are detected
                    var playerName = GetPlayerName(playerId);
                    StartTradeLog(playerId, playerName, 0);

                    // Trade completion message will be sent in ProcessTradeItems with actual counts
                }

                // Check if this was the current processing player
                if (_currentProcessingPlayer == playerId)
                {
                    LogDebug($"[QUEUE] Trade completed for current processing player {playerId}");

                    // DO NOT reset processing state here - keep it true until bag processing is complete
                    // _isProcessingTrade will be reset when bag processing finishes in ReturnBagsToPlayer
                    LogDebug($"[QUEUE] Keeping _isProcessingTrade=true until bag processing completes");

                    // Process next player in queue after a short delay (to allow for item processing)
                    _ = Task.Delay(2000).ContinueWith(_ =>
                    {
                        LogDebug("[QUEUE] Checking for next player in queue...");
                        ProcessNextInQueue();
                    });
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Error handling trade completed: {ex.Message}");

                // Reset processing state on error
                _currentBotState = BotState.Ready;
                _currentProcessingPlayer = null;
            }
        }

        private static void HandleTradeDeclined(int playerId)
        {
            try
            {
                LogDebug($"[PM] Trade declined/canceled by player {playerId}");
                LogDebug($"[QUEUE DEBUG] Before decline handling - _isProcessingTrade: {_isProcessingTrade}, _currentProcessingPlayer: {_currentProcessingPlayer}");



                // Clean up trade session
                if (_activeTradeSessions.ContainsKey(playerId))
                {
                    _activeTradeSessions[playerId].State = TradeState.Declined;
                    _activeTradeSessions.Remove(playerId);
                    LogDebug($"[QUEUE DEBUG] Cleaned up trade session for player {playerId}");
                }

                SendPrivateMessage(playerId.ToString(), "Trade was declined/canceled. Feel free to try again anytime!");

                // Check if this was the current processing player
                if (_currentProcessingPlayer == playerId)
                {
                    LogDebug($"[QUEUE] Trade declined/canceled by current processing player {playerId} - resetting to ready state");

                    // Reset processing state - bot is now ready for new trades
                    _currentBotState = BotState.Ready;
                    _currentProcessingPlayer = null;

                    LogDebug($"[QUEUE DEBUG] After reset - _isProcessingTrade: {_isProcessingTrade}, _currentProcessingPlayer: {_currentProcessingPlayer}");

                    // DO NOT process queue on declined trades - bot should be ready for immediate new trades
                    // Only process queue when trades complete successfully
                    LogDebug($"[QUEUE] Bot is now ready for new trades (declined trade does not trigger queue processing)");
                }
                else if (_currentProcessingPlayer.HasValue)
                {
                    LogDebug($"[QUEUE DEBUG] Trade declined by player {playerId}, but current processing player is {_currentProcessingPlayer.Value} - no reset needed");
                }
                else
                {
                    LogDebug($"[QUEUE DEBUG] Trade declined by player {playerId}, but no current processing player - no reset needed");
                }

                // IMPORTANT: Also remove this player from the queue if they're in it
                // This prevents them from being stuck in queue after declining
                RemovePlayerFromQueue(playerId);

                // NOTE: We don't try to clear Trade.CurrentTarget because AOSharp manages it internally
                // Instead, we modified the queue logic to only rely on _isProcessingTrade
                LogDebug($"[TRADE DEBUG] Trade.CurrentTarget: {Trade.CurrentTarget} (ignored for queue logic)");
            }
            catch (Exception ex)
            {
                LogDebug($"Error handling trade declined/canceled: {ex.Message}");
                LogDebug($"[QUEUE DEBUG] Exception occurred, forcing reset of queue state");

                // Reset processing state on error
                _currentBotState = BotState.Ready;
                _currentProcessingPlayer = null;

                LogDebug($"[QUEUE DEBUG] After exception reset - _isProcessingTrade: {_isProcessingTrade}, _currentProcessingPlayer: {_currentProcessingPlayer}");
            }
        }

        private static List<Item> GetTradeItems()
        {
            try
            {
                var allTradedItems = new List<Item>();

                // Check for new bags from the trade by comparing current inventory to pre-trade inventory
                var currentContainers = Inventory.Containers.ToList();

                // Look for any new bags that appeared after trade
                var newBags = currentContainers.Where(container =>
                    !_preTradeInventory.Any(preItem =>
                        preItem.Type == IdentityType.Container &&
                        preItem.Instance == container.Identity.Instance)).ToList();

                if (newBags.Any())
                {
                    LogDebug($"[TRADE DETECTION] Found {newBags.Count} new bag(s) from trade");

                    // Convert containers to items for return
                    foreach (var container in newBags)
                    {
                        var bagItem = Inventory.Items.FirstOrDefault(item =>
                            item.UniqueIdentity.Instance == container.Identity.Instance &&
                            item.UniqueIdentity.Type == IdentityType.Container);

                        if (bagItem != null)
                        {
                            allTradedItems.Add(bagItem);
                        }
                    }
                }

                // Check for new non-bagged items in inventory
                var currentInventoryItems = Inventory.Items.Where(item =>
                    item.Slot.Type == IdentityType.Inventory &&
                    item.UniqueIdentity.Type != IdentityType.Container).ToList();

                var newLooseItems = currentInventoryItems.Where(item =>
                    !_preTradeInventory.Any(preItem =>
                        preItem.Type == IdentityType.None &&
                        preItem.Instance == item.Slot.Instance)).ToList();

                if (newLooseItems.Any())
                {
                    LogDebug($"[TRADE DETECTION] Found {newLooseItems.Count} new loose item(s) from trade using unified detection");
                    foreach (var item in newLooseItems)
                    {
                        LogDebug($"[TRADE DETECTION] New loose item: {item.Name} (ID: {item.Id}, Instance: {item.UniqueIdentity.Instance})");
                        allTradedItems.Add(item);

                        // Track non-bagged items for processing
                        double currentTime = DateTime.Now.Ticks / 10000000.0; // Convert to seconds
                        string trackingKey = $"{item.Name}_{item.Id}_{currentTime:F3}";
                        string instanceTrackingKey = $"{item.Name}_inst_{item.Id}_{currentTime:F3}";
                        _tradedItems[trackingKey] = currentTime;
                        _tradedItems[instanceTrackingKey] = currentTime;
                        LogDebug($"[TRADE DETECTION] Tracking loose item with keys: {trackingKey} and {instanceTrackingKey}");
                    }
                }

                if (allTradedItems.Any())
                {
                    LogDebug($"[TRADE DETECTION] Total traded items found: {allTradedItems.Count} ({newBags.Count} bags + {newLooseItems.Count} loose items)");
                }

                return allTradedItems;
            }
            catch (Exception ex)
            {
                LogDebug($"Error getting trade items: {ex.Message}");
                return new List<Item>();
            }
        }

        public static void OnTradeOpened(Identity traderId)
        {
            try
            {
                LogDebug($"[TRADE] Trade opened with {traderId}");

                if (traderId.Type == IdentityType.SimpleChar)
                {
                    int playerId = traderId.Instance;

                    // AUTO-DECLINE: Check if bot is currently processing
                    LogDebug($"[TRADE DEBUG] Checking auto-decline conditions:");
                    LogDebug($"[TRADE DEBUG] _isProcessingTrade: {_isProcessingTrade}");
                    LogDebug($"[TRADE DEBUG] _currentBotState: {_currentBotState}");
                    LogDebug($"[TRADE DEBUG] _currentProcessingPlayer: {_currentProcessingPlayer}");

                    if (_isProcessingTrade && _currentProcessingPlayer.HasValue)
                    {
                        // ALLOW return trades for the same player when bot is in Returning state
                        if (_currentProcessingPlayer.Value == playerId &&
                            _currentBotState == BotState.Returning &&
                            _pendingReturns.ContainsKey(playerId))
                        {
                            LogDebug($"[TRADE] ✅ ALLOWING return trade for player {playerId} - bot is in Returning state with pending returns");
                            // Don't auto-decline - this is a return trade
                        }
                        else
                        {
                            LogDebug($"[TRADE] ❌ AUTO-DECLINING trade from player {playerId}");
                            LogDebug($"[TRADE DEBUG] Current state: {_currentBotState}");
                            LogDebug($"[TRADE DEBUG] Processing player: {_currentProcessingPlayer.Value}");
                            LogDebug($"[TRADE DEBUG] Trade player: {playerId}");
                            LogDebug($"[TRADE DEBUG] Has pending returns: {_pendingReturns.ContainsKey(playerId)}");
                            LogDebug($"[TRADE DEBUG] Same player: {_currentProcessingPlayer.Value == playerId}");
                            LogDebug($"[TRADE DEBUG] Is returning state: {_currentBotState == BotState.Returning}");

                            // Mark this trade as auto-declined to prevent processing
                            _autoDeclinedTrades.Add(playerId);

                            // Get player name for message
                            var targetPlayer = DynelManager.Players.FirstOrDefault(p => p.Identity.Instance == playerId);
                            string playerName = targetPlayer?.Name ?? "Unknown";

                            if (_currentProcessingPlayer.Value == playerId)
                            {
                                // Same player trying to trade again while their order is being processed
                                SendPrivateMessage(playerId.ToString(), "I'm still working on your previous order! Please wait for me to finish processing and return your items.");
                            }
                            else
                            {
                                // Different player - add to queue
                                if (targetPlayer != null)
                                {
                                    AddToTradeQueue(playerId, targetPlayer.Name, targetPlayer.MovementComponent.Position);
                                }
                                else
                                {
                                    SendPrivateMessage(playerId.ToString(), "I'm currently processing another order. Please try again in a moment.");
                                }
                            }

                            // Decline the trade
                            Trade.Decline();
                            return;
                        }
                    }
                    else
                    {
                        LogDebug($"[TRADE] ✅ ALLOWING trade for player {playerId} - bot is ready");

                        // Clear the previous trade's received items tracking for new trade
                        Core.ItemTracker.ClearCurrentTradeItems();

                        // CRITICAL FIX: Clear player from auto-declined set when bot is ready to process their trade
                        if (_autoDeclinedTrades.Contains(playerId))
                        {
                            _autoDeclinedTrades.Remove(playerId);
                            LogDebug($"[TRADE FIX] Cleared player {playerId} from auto-declined set - ready for fresh trade");
                        }
                    }

                    _currentTradeTarget = traderId; // Store the trade target

                    // Clear new items tracking for this player (fresh start)
                    _newItemsAddedDuringReturn.Remove(playerId); // Clear new items tracking

                    // SIMPLE TRADE LOGGING: Retry capturing original inventory if it failed at startup
                    if (!_originalInventoryCaptured)
                    {
                        LogDebug($"[STARTUP INVENTORY] Retrying inventory capture on first trade");
                        CaptureOriginalBotInventory();
                    }

                    // SIMPLE TRADE LOGGING: Capture inventory if not done or if we captured 0 items
                    if (!_originalInventoryCaptured || _originalBotInventory.Count == 0)
                    {
                        LogDebug($"[STARTUP INVENTORY] Capturing inventory on trade - _originalInventoryCaptured={_originalInventoryCaptured}, count={_originalBotInventory.Count}");
                        _originalInventoryCaptured = false; // Reset flag to allow recapture
                        CaptureOriginalBotInventory();
                    }

                    // CLIENTLESS EVENT-DRIVEN APPROACH: Start tracking item additions
                    _isTrackingItems = true;
                    _tradedItemsByPlayer.Clear(); // Clear any previous data

                    // FIX: Only set _currentProcessingPlayer if bot is not already processing someone else
                    // This prevents race conditions where multiple trades open simultaneously
                    if (!_currentProcessingPlayer.HasValue || _currentProcessingPlayer.Value == playerId)
                    {
                        _currentProcessingPlayer = playerId; // Set the player ID for event tracking
                        LogDebug($"[QUEUE FIX] Set _currentProcessingPlayer to {playerId}");
                    }
                    else
                    {
                        LogDebug($"[QUEUE FIX] NOT setting _currentProcessingPlayer - already processing {_currentProcessingPlayer.Value}, new trade from {playerId}");
                    }

                    // CRITICAL FIX: Only capture pre-trade inventory for NEW trades, not return trades
                    bool isReturnTrade = _playersWithPendingReturn.Contains(playerId);
                    LogDebug($"[TRADE FIX] Player {playerId} return trade check: isReturnTrade={isReturnTrade}, _playersWithPendingReturn.Count={_playersWithPendingReturn.Count}");

                    if (!isReturnTrade)
                    {
                        // This is a new trade - capture current inventory state using ITEM IDs ONLY
                        _preTradeInventory.Clear();

                        // IMPROVED: More robust pre-trade inventory capture
                        var inventoryItems = Inventory.Items.Where(item =>
                            item.Slot.Type == IdentityType.Inventory &&
                            item.UniqueIdentity.Type != IdentityType.Container).ToList();

                        foreach (var item in inventoryItems)
                        {
                            _preTradeInventory.Add(new Identity { Type = IdentityType.None, Instance = item.Slot.Instance });
                            LogDebug($"[TRADE FIX] Captured pre-trade item: {item.Name} (SlotInstance: {item.Slot.Instance})");
                        }

                        // Add all existing containers using their Identity
                        foreach (var container in Inventory.Containers)
                        {
                            _preTradeInventory.Add(container.Identity);
                            LogDebug($"[TRADE FIX] Captured pre-trade container: {container.Identity.Instance}");
                        }

                        LogDebug($"[TRADE FIX] NEW TRADE: Captured pre-trade inventory: {_preTradeInventory.Count} items/bags before trade");

                        // ADDITIONAL: Log current inventory state for debugging
                        LogDebug($"[TRADE FIX] Current inventory has {inventoryItems.Count} items before trade");
                    }
                    else
                    {
                        // This is a return trade - keep the original pre-trade inventory from the initial trade
                        LogDebug($"[TRADE FIX] RETURN TRADE: Keeping original pre-trade inventory: {_preTradeInventory.Count} items/bags from initial trade");
                    }

                    // TOOL PROTECTION: Capture bot's current item IDs to prevent giving them away
                    _botItemIds.Clear();
                    foreach (var item in Inventory.Items)
                    {
                        _botItemIds.Add(item.Id);
                    }
                    LogDebug($"[TOOL PROTECTION] Captured {_botItemIds.Count} bot item IDs for protection");
                    LogDebug($"[TRADE PROCESSING] Started event-driven item tracking for player {playerId}");

                    HandleTradeOpened(playerId);
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[TRADE] Error in OnTradeOpened: {ex.Message}");
            }
        }

        public static void OnTradeStatusChanged(TradeStatus status)
        {
            try
            {
                LogDebug($"[TRADE] TradeStatusChanged: {Trade.CurrentTarget} -> {status}"); // Temporarily enabled for debugging

                // Use implantdispenser pattern - check Trade.Status instead of TradeState
                switch (status)
                {
                    case TradeStatus.None:
                        // Handle trade declined logic here
                        LogDebug($"[TRADE] Trade was declined");
                        LogDebug($"[TRADE DEBUG] _currentTradeTarget: {(_currentTradeTarget?.ToString() ?? "NULL")}");
                        LogDebug($"[TRADE DEBUG] _pendingReturns.Count: {_pendingReturns.Count}");
                        if (_currentTradeTarget.HasValue && _currentTradeTarget.Value.Type == IdentityType.SimpleChar)
                        {
                            int playerId = _currentTradeTarget.Value.Instance;
                            HandleTradeDeclined(playerId);

                            // If this was a return trade that got declined, schedule immediate retry
                            if (_pendingReturns.ContainsKey(playerId))
                            {
                                LogDebug($"[RETURN RETRY] Return trade declined - scheduling immediate retry for player {playerId}");

                                // Increment retry counter
                                if (!_returnRetryCount.ContainsKey(playerId))
                                    _returnRetryCount[playerId] = 0;
                                _returnRetryCount[playerId]++;

                                var retryCount = _returnRetryCount[playerId];
                                LogDebug($"[RETURN RETRY] This is retry attempt #{retryCount} for player {playerId}");

                                // IMPROVEMENT: Add maximum retry limit to prevent infinite retries
                                const int maxRetries = 50; // Allow up to 50 retry attempts
                                if (retryCount >= maxRetries)
                                {
                                    LogDebug($"[RETURN RETRY] Maximum retry attempts ({maxRetries}) reached for player {playerId} - giving up");
                                    var playerName = GetPlayerName(playerId);
                                    SendPrivateMessage(playerId.ToString(),
                                        $"❌ {playerName}, I've tried {maxRetries} times to return your items but you keep declining. " +
                                        $"Your items will be saved and you can use 'status' command to check on them. Please contact an admin if needed.");

                                    // Don't schedule another retry - items remain in _pendingReturns for manual recovery
                                    return;
                                }

                                // Send encouraging message to player
                                var currentPlayerName = GetPlayerName(playerId);
                                SendPrivateMessage(playerId.ToString(),
                                    $"I see you declined the return trade, {currentPlayerName}. I'll keep trying to give you your processed items back! " +
                                    $"(Attempt #{retryCount}/{maxRetries}) Please accept the next trade when you're ready.");

                                // Schedule retry with shorter delay for first few attempts
                                int delayMs = retryCount <= 3 ? 3000 : 10000; // 3 seconds for first 3 attempts, then 10 seconds

                                Task.Delay(delayMs).ContinueWith(_ =>
                                {
                                    LogDebug($"[RETURN RETRY] Executing retry #{retryCount} for player {playerId}");
                                    Task.Run(() => AttemptReturnToPlayer(playerId));
                                });
                            }

                            // Clear tracking for the declined trade

                            // Clear new items tracking for declined trades
                            _newItemsAddedDuringReturn.Remove(playerId);

                            // Clean up recovery trade tracking if this was a recovery trade
                            if (_activeRecoveryTrades.Contains(playerId))
                            {
                                _activeRecoveryTrades.Remove(playerId);
                                LogDebug($"[RESTORE] Recovery trade declined for player {playerId} - cleaned up tracking");
                            }

                            // Clean up auto-declined trades tracking
                            _autoDeclinedTrades.Remove(playerId);
                        }
                        else
                        {
                            // FIX: Handle case where _currentTradeTarget is not set but _currentProcessingPlayer is
                            // This can happen if trade is declined before _currentTradeTarget is stored
                            if (_currentProcessingPlayer.HasValue)
                            {
                                LogDebug($"[TRADE FIX] Trade declined but no stored trade target - using _currentProcessingPlayer: {_currentProcessingPlayer.Value}");
                                HandleTradeDeclined(_currentProcessingPlayer.Value);
                            }
                            else
                            {
                                LogDebug($"[TRADE FIX] Trade declined but no current processing player - forcing bot state reset");
                                // Force reset bot state to prevent getting stuck
                                _currentBotState = BotState.Ready;
                                _currentProcessingPlayer = null;
                                LogDebug($"[QUEUE] Forced bot state reset after trade decline with no target");
                            }
                        }

                        // Only clear pre-trade inventory if this was NOT an auto-declined trade
                        bool wasAutoDeclined = _autoDeclinedTrades.Any();

                        // Clear the stored trade target
                        _currentTradeTarget = null;

                        if (!wasAutoDeclined)
                        {
                            _preTradeInventory.Clear();
                            _tradedItems.Clear();
                            LogDebug($"[TRADE] Cleared tracking data for player-declined trade");
                        }
                        else
                        {
                            LogDebug($"[TRADE] Preserving tracking data - auto-declined trade detected");
                        }
                        break;

                    case TradeStatus.Accept:
                        // LogDebug($"[TRADE] Player accepted - bot will now confirm"); // Commented out to reduce spam

                        // CHECK: Don't confirm if bot is busy processing a DIFFERENT player
                        if (_isProcessingTrade && _currentProcessingPlayer.HasValue && _currentTradeTarget.HasValue)
                        {
                            int currentTradePlayerId = _currentTradeTarget.Value.Instance;

                            // Allow return trades for the same player
                            if (_currentProcessingPlayer.Value == currentTradePlayerId &&
                                _currentBotState == BotState.Returning &&
                                _pendingReturns.ContainsKey(currentTradePlayerId))
                            {
                                LogDebug($"[TRADE] ✅ ALLOWING return trade confirmation for player {currentTradePlayerId} - bot is returning items");
                                // Continue with confirmation
                            }
                            else
                            {
                                LogDebug($"[TRADE] Bot is busy processing different player - declining instead of confirming");
                                Trade.Decline();
                                break;
                            }
                        }

                        // Set initial processing state when player accepts trade
                        if (_currentTradeTarget.HasValue && _currentTradeTarget.Value.Type == IdentityType.SimpleChar)
                        {
                            int playerId = _currentTradeTarget.Value.Instance;
                            _currentBotState = BotState.Processing;
                            _currentProcessingPlayer = playerId;
                            // LogDebug($"[BOT STATE] Trade accepted - changed to Processing for player {playerId}");
                        }

                        // Player has accepted, now we confirm (responsive, no delay)
                        Trade.Confirm();
                        // LogDebug($"[TRADE] Trade.Confirm() sent"); // Commented out to reduce spam
                        break;

                    case TradeStatus.Confirm:
                        // LogDebug($"[TRADE] Both sides confirmed - bot will now accept to complete"); // Commented out to reduce spam

                        // CHECK: Don't complete if bot is busy processing a DIFFERENT player
                        if (_isProcessingTrade && _currentProcessingPlayer.HasValue && _currentTradeTarget.HasValue)
                        {
                            int currentTradePlayerId = _currentTradeTarget.Value.Instance;
                            if (_currentProcessingPlayer.Value != currentTradePlayerId)
                            {
                                LogDebug($"[TRADE] Bot is busy processing different player {_currentProcessingPlayer.Value} - declining trade with {currentTradePlayerId}");
                                Trade.Decline();
                                break;
                            }
                            else
                            {
                                // LogDebug($"[TRADE] Bot is processing current trade player {currentTradePlayerId} - continuing with trade");
                            }
                        }

                        // Both sides confirmed, now we accept to complete the trade (responsive, no delay)
                        Trade.Accept();
                        // LogDebug($"[TRADE] Trade.Accept() sent to complete trade"); // Commented out to reduce spam
                        break;

                    case TradeStatus.Finished:
                        // LogDebug($"[TRADE] Trade completed successfully"); // Commented out to reduce spam
                        // LogDebug($"[TRADE] Trader parameter: {trader}, Type: {trader.Type}, Instance: {trader.Instance}"); // Commented out to reduce spam
                        // LogDebug($"[TRADE] Stored trade target: {_currentTradeTarget}"); // Commented out to reduce spam

                        // Note: Removed automatic dialog closing to prevent runaway timer loops
                        // Players can manually close any remaining dialogs with ESC key

                        // Use stored trade target instead of the trader parameter (which is Identity.None)
                        if (_currentTradeTarget.HasValue && _currentTradeTarget.Value.Type == IdentityType.SimpleChar)
                        {
                            int playerId = _currentTradeTarget.Value.Instance;
                            LogDebug($"[TRADE] Using stored trade target, playerId: {playerId}");

                            // CHECK: Skip processing if this trade was auto-declined
                            if (_autoDeclinedTrades.Contains(playerId))
                            {
                                LogDebug($"[TRADE] Skipping processing for auto-declined trade from player {playerId}");
                                _autoDeclinedTrades.Remove(playerId); // Clean up
                                _currentTradeTarget = null;
                                _preTradeInventory.Clear(); // Clear tracking data
                                _tradedItems.Clear(); // Clear tracking data

                                // CRITICAL FIX: Reset bot state when skipping auto-declined trades
                                if (_currentProcessingPlayer == playerId)
                                {
                                    _currentBotState = BotState.Ready;
                                    _currentProcessingPlayer = null;
                                    LogDebug($"[BOT STATE] Auto-declined trade complete - changed to Ready");
                                    LogDebug($"[QUEUE] Bot is now ready for new trades after auto-declined trade");

                                    // Process next player in queue
                                    ProcessNextInQueue();
                                }
                                return;
                            }

                            // Check if this is an admin trade first - admin trades should not trigger any recipe processing
                            string playerName = GetPlayerNameFromId(playerId);
                            bool isAdminTrade = !string.IsNullOrEmpty(playerName) && Core.DynamicCommandHandler.IsAdminTrade(playerName);

                            if (isAdminTrade)
                            {
                                LogDebug($"[TRADE] This was an admin trade for {playerName} - skipping all recipe processing");
                                // Admin trades are handled entirely by DynamicCommandHandler
                                // Clear the stored trade target and exit
                                _currentTradeTarget = null;

                                // CRITICAL FIX: Reset bot state after admin trade completion
                                if (_currentProcessingPlayer == playerId)
                                {
                                    _currentBotState = BotState.Ready;
                                    _currentProcessingPlayer = null;
                                    LogDebug($"[BOT STATE] Admin trade complete - changed to Ready");
                                    LogDebug($"[QUEUE] Bot is now ready for new trades after admin trade");

                                    // Process any queued players
                                    ProcessNextInQueue();
                                }
                                break;
                            }

                            // Check if this was a return trade (player had pending items) BEFORE calling HandleTradeCompleted
                            if (_pendingReturns.ContainsKey(playerId))
                            {
                                LogDebug($"[TRADE] Return trade completed for player {playerId}");

                                // Calculate what was returned and send detailed message
                                var returnedItems = _pendingReturns.ContainsKey(playerId) ? _pendingReturns[playerId] : new List<Item>();
                                int bagCount = returnedItems.Count(item => item.UniqueIdentity.Type == IdentityType.Container);
                                int looseItemCount = returnedItems.Count(item => item.UniqueIdentity.Type != IdentityType.Container);

                                // FIXED TRADE LOGGING: Log the actual items that were returned, not what was received
                                // This ensures processed results (like Indigo Carmine) are logged correctly
                                if (_detailedTradeLogs.ContainsKey(playerId))
                                {
                                    var tradeLog = _detailedTradeLogs[playerId];
                                    // Log the actual returned items instead of trying to match to received items
                                    foreach (var returnedItem in returnedItems.Where(item => item.UniqueIdentity.Type != IdentityType.Container))
                                    {
                                        string returnedItemName = GetItemDisplayName(returnedItem);
                                        LogReturnedLooseItem(playerId, returnedItemName);
                                        LogDebug($"[SIMPLE TRADE LOG] ✅ LOGGED ACTUAL RETURNED ITEM: {returnedItemName} (return trade completed)");
                                    }
                                    LogDebug($"[TRADE LOG FIX] Logged {returnedItems.Count(item => item.UniqueIdentity.Type != IdentityType.Container)} actual returned items");
                                }
                                else
                                {
                                    LogDebug($"[TRADE LOG FIX] No detailed trade log found for player {playerId} - skipping return logging");
                                }

                                string returnMessage = $"Your {bagCount} bag(s) and {looseItemCount} loose item(s) were returned! Thank you for using my services!";
                                SendPrivateMessage(playerId.ToString(), returnMessage);

                                // Check if new items were added during the return trade
                                bool newItemsAdded = _newItemsAddedDuringReturn.ContainsKey(playerId) && _newItemsAddedDuringReturn[playerId];
                                // LogDebug($"[TRADE DEBUG] Checking for new items added during return: playerId={playerId}, hasKey={_newItemsAddedDuringReturn.ContainsKey(playerId)}, value={(_newItemsAddedDuringReturn.ContainsKey(playerId) ? _newItemsAddedDuringReturn[playerId] : false)}, newItemsAdded={newItemsAdded}"); // Commented out to reduce spam

                                if (newItemsAdded)
                                {
                                    LogDebug($"[TRADE] New items were added during return trade - treating as new trade");

                                    // Send message to player about the new items
                                    SendPrivateMessage(playerId.ToString(), "Bags returned! I also detected new items you added during the return - processing them now...");

                                    // CRITICAL FIX: DO NOT update _preTradeInventory after trade completion
                                    // The _preTradeInventory should remain as it was BEFORE the trade to properly detect new items
                                    LogDebug($"[TRADE FIX] Keeping original pre-trade inventory to properly detect new items from trade");
                                    LogDebug($"[TRADE FIX] Pre-trade inventory contains {_preTradeInventory.Count} items/bags from before the trade");

                                    // Clear the return trade data first
                                    _pendingReturns.Remove(playerId);
                                    _playersWithPendingReturn.Remove(playerId);
                                    _playerBags.Remove(playerId);
                                    _newItemsAddedDuringReturn.Remove(playerId);

                                    // Now treat this as a new trade - start bag processing
                                    LogDebug($"[TRADE] Starting new trade processing for player {playerId}");
                                    // Note: New items during return trade should NOT trigger zone refresh
                                    // They should be processed directly without zone refresh logic

                                    // Call HandleTradeCompleted for new items during return
                                    Task.Run(async () => await HandleTradeCompleted(playerId));

                                    // Also call ProcessTradeItems to properly process the new items (matching normal trade flow)
                                    Task.Run(async () =>
                                    {
                                        await Task.Delay(1000);
                                        await ProcessTradeItems(playerId);
                                    });
                                }
                                else
                                {
                                    // Normal return trade completion
                                    var bagsReturned = _pendingReturns.ContainsKey(playerId) ? _pendingReturns[playerId].Count : 0;

                                    // Complete trade log BEFORE cleaning up data
                                    CompleteTradeLog(playerId, bagsReturned);

                                    // Send success message to player before cleanup
                                    var currentPlayerName = GetPlayerName(playerId);
                                    var retryCount = _returnRetryCount.ContainsKey(playerId) ? _returnRetryCount[playerId] : 0;
                                    if (retryCount > 0)
                                    {
                                        SendPrivateMessage(playerId.ToString(),
                                            $"✅ Success! Your processed items have been returned after {retryCount + 1} attempt(s). Thank you for your patience, {currentPlayerName}!");
                                    }
                                    else
                                    {
                                        SendPrivateMessage(playerId.ToString(),
                                            $"✅ Success! Your processed items have been returned. Thank you for trading, {currentPlayerName}!");
                                    }

                                    // Clean up all tracking data
                                    _pendingReturns.Remove(playerId);
                                    _playersWithPendingReturn.Remove(playerId);
                                    _playerBags.Remove(playerId); // Clean up any remaining bag tracking
                                    _newItemsAddedDuringReturn.Remove(playerId); // Clean up tracking
                                    _returnRetryCount.Remove(playerId); // Clean up retry counter

                                    // STATE TRANSITION: Returning → Ready
                                    _currentBotState = BotState.Ready;
                                    _currentProcessingPlayer = null;
                                    LogDebug($"[BOT STATE] Return trade complete for player {playerId} - changed to Ready");
                                    LogDebug($"[QUEUE] Bot is now ready for new trades");

                                    // Process any queued players
                                    ProcessNextInQueue();
                                }

                                // Don't call HandleTradeCompleted for return trades - it causes duplicate messages
                            }
                            else
                            {
                                // This was a new trade - check what type of trade it was
                                LogDebug($"[TRADE] New trade completed for player {playerId}");

                                // Processing flags already set in Accept state - no need to set again
                                LogDebug($"[QUEUE] Processing state already set - _isProcessingTrade: {_isProcessingTrade}, _currentProcessingPlayer: {_currentProcessingPlayer}");

                                // Check if this is a recovery trade - if so, skip processing
                                if (_activeRecoveryTrades.Contains(playerId))
                                {
                                    LogDebug($"[TRADE] Recovery trade completed for player {playerId} - skipping processing");
                                    _activeRecoveryTrades.Remove(playerId);

                                    // Reset processing state for recovery trades
                                    _currentBotState = BotState.Ready;
                                    _currentProcessingPlayer = null;
                                    LogDebug($"[QUEUE] Recovery trade complete - bot is now ready for new trades");

                                    // Process any queued players
                                    ProcessNextInQueue();

                                    // Don't call HandleTradeCompleted for recovery trades - it causes duplicate messages
                                }
                                else
                                {
                                    // Normal trade - start processing
                                    LogDebug($"[TRADE] Starting item processing for player {playerId}");

                                    // Call HandleTradeCompleted for new trades
                                    Task.Run(async () => await HandleTradeCompleted(playerId));

                                    Task.Run(async () =>
                                    {
                                        await Task.Delay(1000);
                                        await ProcessTradeItems(playerId);
                                    });
                                }
                            }

                            // Clear the stored trade target (but keep pre-trade inventory for bag detection)
                            _currentTradeTarget = null;
                        }
                        else
                        {
                            LogDebug($"[TRADE] No valid stored trade target found");
                        }
                        break;




                }
            }
            catch (Exception ex)
            {
                LogDebug($"[TRADE] Error in OnTradeStatusChanged: {ex.Message}");
            }
        }



        private static async Task ProcessFoundBags(int playerId, List<Container> newBags)
        {
            try
            {
                LogDebug($"[BAG PROCESSING] Processing {newBags.Count} found bags for player {playerId}");

                // Check for duplicates before processing
                var duplicateCheckResult = await CheckForDuplicatesInBags(newBags);
                if (duplicateCheckResult.HasDuplicates)
                {
                    LogDebug($"[DUPLICATE DETECTION] Found duplicates in bags from player {playerId}");
                    await HandleDuplicateItems(playerId, newBags, duplicateCheckResult);
                    return;
                }

                // Convert containers to items for processing
                var bagItems = new List<Item>();
                foreach (var container in newBags)
                {
                    // Find the corresponding item in inventory
                    var bagItem = Inventory.Items.FirstOrDefault(item =>
                        item.UniqueIdentity.Instance == container.Identity.Instance &&
                        item.UniqueIdentity.Type == IdentityType.Container);

                    if (bagItem != null)
                    {
                        bagItems.Add(bagItem);
                        LogDebug($"[BAG PROCESSING] Found bag item: {bagItem.Name} (ID: {bagItem.Id}, Instance: {bagItem.UniqueIdentity.Instance})");

                        // Track this item using both ID and Instance for better matching
                        double currentTime = DateTime.Now.Ticks / 10000000.0; // Convert to seconds
                        string trackingKey = $"{bagItem.Name}_{bagItem.Id}_{currentTime:F3}";
                        string instanceTrackingKey = $"{bagItem.Name}_inst_{bagItem.UniqueIdentity.Instance}_{currentTime:F3}";
                        _tradedItems[trackingKey] = currentTime;
                        _tradedItems[instanceTrackingKey] = currentTime;
                        LogDebug($"[BAG PROCESSING] Tracking bag with keys: {trackingKey} and {instanceTrackingKey}");
                    }
                    else
                    {
                        LogDebug($"[BAG PROCESSING] Could not find item for container: {container.Item?.Name ?? "Unknown"}");
                    }
                }

                if (bagItems.Any())
                {
                    // Store containers for this player
                    _playerBags[playerId] = bagItems.ToList();

                    LogDebug($"[BAG PROCESSING] Starting immediate processing of {bagItems.Count} bag(s)");

                    // Process bags immediately
                    await ProcessPlayerBags(playerId);
                }
                else
                {
                    LogDebug($"[BAG PROCESSING] No bag items found to process");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[BAG PROCESSING] Error processing found bags: {ex.Message}");
            }
        }



        private static async Task ProcessTradedItems(int playerId, List<Item> tradedItems)
        {
            try
            {
                LogDebug($"[ITEM PROCESSING] Processing {tradedItems.Count} traded items for player {playerId}");
                int totalItemsProcessed = 0;

                foreach (var item in tradedItems)
                {
                    // Only process items that were tracked as traded (LootManager approach)
                    // Look for any tracking key that starts with the item name and ID
                    var trackingKey = _tradedItems.Keys.FirstOrDefault(key =>
                        key.StartsWith($"{item.Name}_{item.Id}_"));

                    double currentTime = DateTime.Now.Ticks / 10000000.0; // Convert to seconds
                    if (trackingKey != null && (currentTime - _tradedItems[trackingKey]) < 30.0) // 30 second window
                    {
                        LogDebug($"[ITEM PROCESSING] Processing traded item: {item.Name}");

                        LogDebug($"[ITEM PROCESSING] Item {item.Name} will be processed through RecipeManager system");
                        totalItemsProcessed++; // Count as processed since it will be handled by the recipe system

                        // Remove from tracking since we processed it
                        _tradedItems.Remove(trackingKey);
                    }
                    else
                    {
                        LogDebug($"[ITEM PROCESSING] Item {item.Name} not found in tracking or expired");
                    }

                    await Task.Delay(100);
                }

                LogDebug($"[ITEM PROCESSING] Completed processing. Processed {totalItemsProcessed} items.");

                // For loose items, we don't need to return them via trade since they're processed in place
                // Just reset the processing state and notify the player
                if (totalItemsProcessed > 0)
                {
                    LogDebug($"[ITEM PROCESSING] Successfully processed {totalItemsProcessed} loose items for player {playerId}");
                    SendPrivateMessage(playerId.ToString(), $"Processing complete! {totalItemsProcessed} item(s) were processed successfully.");
                }
                else
                {
                    LogDebug($"[ITEM PROCESSING] No items were processed for player {playerId}");
                    SendPrivateMessage(playerId.ToString(), "No items could be processed. They may not be supported or may already be processed.");
                }

                // Reset processing state for loose item trades
                if (_currentProcessingPlayer == playerId)
                {
                    _currentBotState = BotState.Ready;
                    _currentProcessingPlayer = null;
                    LogDebug($"[QUEUE] Loose items processing complete - bot is now ready for new trades");

                    // Clean up tracking data
                    _preTradeInventory.Clear();
                    _tradedItems.Clear();

                    // Clean up item-to-player mapping for completed trades
                    CleanupItemTrackingForCompletedTrade(playerId);

                    // Process any queued players
                    ProcessNextInQueue();
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[ITEM PROCESSING] Error processing traded items: {ex.Message}");
            }
        }

        private static Task<DuplicateCheckResult> CheckForDuplicatesInBags(List<Container> bags)
        {
            try
            {
                LogDebug($"[DUPLICATE CHECK] Checking {bags.Count} bags for duplicate implants and clusters");

                var result = new DuplicateCheckResult();
                var allImplants = new List<Item>();
                var allClusters = new List<Item>();

                // Collect all implants and clusters from all bags
                foreach (var bag in bags)
                {
                    foreach (var item in bag.Items)
                    {
                        if (IsImplantForDuplicateCheck(item))
                        {
                            allImplants.Add(item);
                        }
                        else if (IsClusterForDuplicateCheck(item))
                        {
                            allClusters.Add(item);
                        }
                    }
                }

                LogDebug($"[DUPLICATE CHECK] Found {allImplants.Count} implants and {allClusters.Count} clusters");

                // Check for duplicate implants (same name)
                var implantGroups = allImplants.GroupBy(i => i.Name.ToLower()).Where(g => g.Count() > 1);
                foreach (var group in implantGroups)
                {
                    result.HasDuplicates = true;
                    result.DuplicateImplants.AddRange(group);
                    LogDebug($"[DUPLICATE CHECK] Found {group.Count()} duplicate implants: {group.Key}");
                }

                // Check for duplicate clusters (same name)
                var clusterGroups = allClusters.GroupBy(c => c.Name.ToLower()).Where(g => g.Count() > 1);
                foreach (var group in clusterGroups)
                {
                    result.HasDuplicates = true;
                    result.DuplicateClusters.AddRange(group);
                    LogDebug($"[DUPLICATE CHECK] Found {group.Count()} duplicate clusters: {group.Key}");
                }

                if (result.HasDuplicates)
                {
                    LogDebug($"[DUPLICATE CHECK] DUPLICATES DETECTED! {result.DuplicateImplants.Count} duplicate implants, {result.DuplicateClusters.Count} duplicate clusters");
                }
                else
                {
                    LogDebug($"[DUPLICATE CHECK] No duplicates found - safe to proceed");
                }

                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                LogDebug($"[DUPLICATE CHECK] Error checking for duplicates: {ex.Message}");
                return Task.FromResult(new DuplicateCheckResult { HasDuplicates = false });
            }
        }

        private static async Task HandleDuplicateItems(int playerId, List<Container> bags, DuplicateCheckResult duplicateResult)
        {
            try
            {
                LogDebug($"[DUPLICATE HANDLER] Handling duplicate items for player {playerId}");

                // Build detailed error message
                var errorMessage = "DUPLICATE ITEMS DETECTED!\n\n";
                errorMessage += "Your trade contains duplicate implants or clusters which will cause crafting to fail.\n\n";

                if (duplicateResult.DuplicateImplants.Any())
                {
                    errorMessage += "Duplicate Implants:\n";
                    var implantGroups = duplicateResult.DuplicateImplants.GroupBy(i => i.Name);
                    foreach (var group in implantGroups)
                    {
                        errorMessage += $"- {group.Key} ({group.Count()} copies)\n";
                    }
                    errorMessage += "\n";
                }

                if (duplicateResult.DuplicateClusters.Any())
                {
                    errorMessage += "Duplicate Clusters:\n";
                    var clusterGroups = duplicateResult.DuplicateClusters.GroupBy(c => c.Name);
                    foreach (var group in clusterGroups)
                    {
                        errorMessage += $"- {group.Key} ({group.Count()} copies)\n";
                    }
                    errorMessage += "\n";
                }

                errorMessage += "Please remove the duplicate items from your bags and send the trade command again.\n";
                errorMessage += "Each implant and cluster should only appear ONCE in your trade.";

                // Send error message to player
                SendPrivateMessage(playerId.ToString(), errorMessage);

                // Return all bags to player immediately
                await ReturnBagsToPlayerDueToDuplicates(playerId, bags);
            }
            catch (Exception ex)
            {
                LogDebug($"[DUPLICATE HANDLER] Error handling duplicates: {ex.Message}");
            }
        }

        private static async Task ReturnBagsToPlayerDueToDuplicates(int playerId, List<Container> bags)
        {
            try
            {
                LogDebug($"[DUPLICATE RETURN] Returning {bags.Count} bags to player {playerId} due to duplicates");

                // Find the player
                var player = DynelManager.Players.FirstOrDefault(p => p.Identity.Instance == playerId);
                if (player == null)
                {
                    LogDebug($"[DUPLICATE RETURN] Player {playerId} not found nearby");
                    return;
                }

                // Convert containers to items for return
                var bagItems = new List<Item>();
                foreach (var container in bags)
                {
                    var bagItem = Inventory.Items.FirstOrDefault(item =>
                        item.UniqueIdentity.Instance == container.Identity.Instance &&
                        item.UniqueIdentity.Type == IdentityType.Container);

                    if (bagItem != null)
                    {
                        bagItems.Add(bagItem);
                    }
                }

                if (bagItems.Any())
                {
                    // Open trade to return items
                    LogDebug($"[DUPLICATE RETURN] Opening trade to return {bagItems.Count} bags");
                    Trade.Open(player.Identity);

                    // Wait for trade to open
                    await Task.Delay(1000);

                    // Add bags to trade
                    foreach (var bagItem in bagItems)
                    {
                        if (Inventory.Items.Contains(bagItem))
                        {
                            // RULE #5 EMERGENCY FAILSAFE: NEVER RETURN BOT'S PERSONAL TOOLS TO PLAYERS
                            // COMPREHENSIVE TOOL PROTECTION: Use new protection system
                            if (IsProcessingTool(bagItem))
                            {
                                LogDebug($"[TOOL PROTECTION] ❌ BLOCKED TOOL: {GetItemDisplayName(bagItem)} (ID: {bagItem.Id}) - TOOLS MUST NEVER BE GIVEN TO PLAYERS!");
                                continue; // Skip this item - DO NOT add to trade
                            }

                            LogDebug($"[DUPLICATE RETURN] Adding {bagItem.Name} to trade");
                            Trade.AddItem(bagItem.Slot);

                            // Log returned item for detailed trade log
                            if (bagItem.UniqueIdentity.Type == IdentityType.Container)
                            {
                                var container = Inventory.Containers.FirstOrDefault(c => c.Identity.Instance == bagItem.UniqueIdentity.Instance);
                                if (container != null)
                                {
                                    var bagContents = container.Items.Select(item => GetItemDisplayName(item)).ToList();
                                    LogReturnedBag(playerId, bagItem.Name, bagContents);
                                }
                                else
                                {
                                    LogReturnedBag(playerId, bagItem.Name, new List<string>());
                                }
                            }
                            else
                            {
                                // SIMPLE TRADE LOGGING: Only log items that are NOT bot's original items
                                if (!IsBotOriginalItem(bagItem.Id))
                                {
                                    LogReturnedLooseItem(playerId, GetItemDisplayName(bagItem));
                                    LogDebug($"[SIMPLE TRADE LOG] ✅ LOGGED RETURNED PLAYER ITEM: {GetItemDisplayName(bagItem)}");
                                }
                                else
                                {
                                    LogDebug($"[SIMPLE TRADE LOG] 🛡️ BLOCKED bot original item from return log: {GetItemDisplayName(bagItem)}");
                                }
                            }

                            await Task.Delay(200);
                        }
                    }

                    LogDebug($"[DUPLICATE RETURN] All bags added to trade - waiting for player to accept");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[DUPLICATE RETURN] Error returning bags: {ex.Message}");
            }
        }

        private static async Task ProcessPlayerBags(int playerId)
        {
            try
            {
                LogDebug($"[CONTAINER PROCESSING] Starting processing for player {playerId}");

                if (!_playerBags.ContainsKey(playerId))
                {
                    LogDebug($"[CONTAINER PROCESSING] No containers found for player {playerId}");
                    return;
                }

                var tradedContainers = _playerBags[playerId];
                int totalItemsProcessed = 0;

                LogDebug($"[CONTAINER PROCESSING] Processing {tradedContainers.Count} traded containers...");

                // Debug: List all containers we're about to process
                for (int i = 0; i < tradedContainers.Count; i++)
                {
                    var container = tradedContainers[i];
                    LogDebug($"[CONTAINER PROCESSING] Container {i + 1}: {container.Name} (ID: {container.Id}, Instance: {container.UniqueIdentity.Instance})");
                }

                // Debug: List all tracking keys
                LogDebug($"[CONTAINER PROCESSING] Available tracking keys ({_tradedItems.Count}): {string.Join(", ", _tradedItems.Keys.Take(10))}");

                // Open ALL containers that were stored for this player (more reliable approach)
                foreach (var containerItem in tradedContainers)
                {
                    // Since these containers are already in our _playerBags list, they must be from the trade
                    // No need for complex tracking verification - just open them all
                    LogDebug($"[CONTAINER PROCESSING] Opening container: {containerItem.Name} (ID: {containerItem.Id}, Instance: {containerItem.UniqueIdentity.Instance})");
                    containerItem.Use(); // Open the container
                    await Task.Delay(500);
                }

                // Wait for containers to open
                await Task.Delay(1000);
                LogDebug($"[CONTAINER PROCESSING] Containers opened, scanning contents...");

                // Now scan each traded container's contents
                foreach (var containerItem in tradedContainers)
                {
                    LogDebug($"[CONTAINER PROCESSING] Scanning container: {containerItem.Name} (ID: {containerItem.Id}, Instance: {containerItem.UniqueIdentity.Instance})");

                    // Find the corresponding container for this container item
                    var container = Inventory.Containers.FirstOrDefault(c =>
                        c.Identity.Instance == containerItem.UniqueIdentity.Instance);

                    if (container != null)
                    {
                        LogDebug($"[CONTAINER PROCESSING] Found container for {containerItem.Name} with {container.Items.Count} items");

                        // Scan container contents for processable items
                        var processedCount = await ScanAndProcessContainerContents(container, containerItem, playerId);
                        totalItemsProcessed += processedCount;

                        LogDebug($"[CONTAINER PROCESSING] Completed processing {containerItem.Name} - processed {processedCount} items");
                    }
                    else
                    {
                        LogDebug($"[CONTAINER PROCESSING] Could not find container for {containerItem.Name}");
                    }

                    await Task.Delay(300); // Optimized: Reduced from 500ms to 300ms
                }

                // Log completion and send message to player
                string message = totalItemsProcessed > 0
                    ? $"Processing complete! Processed {totalItemsProcessed} items."
                    : "Scan complete. No processable items found.";

                LogDebug($"[CONTAINER PROCESSING] {message}");

                // Send simplified processing result message to player
                // Count how many bags we're returning (use _playerBags since _pendingReturns isn't populated yet)
                int bagCount = 0;
                if (_playerBags.ContainsKey(playerId))
                {
                    bagCount = _playerBags[playerId].Count;
                }

                string bagText = bagCount == 1 ? "bag" : "bags";

                if (totalItemsProcessed > 0)
                {
                    SendPrivateMessage(playerId.ToString(), $"Your items were processed! Returning your {bagCount} {bagText} now.");
                }
                else
                {
                    SendPrivateMessage(playerId.ToString(), $"No items were processable. Returning your {bagCount} {bagText} now.");
                }

                // End tool session - return all tools to bags
                await EndToolSession();

                // CRITICAL: Reset processing flags BEFORE attempting return trade
                // This prevents the bot from auto-declining its own return trade
                _currentBotState = BotState.Ready;
                _currentProcessingPlayer = null;
                LogDebug($"[QUEUE] Processing complete for player {playerId} - bot is now ready for new trades");

                // Schedule container return
                await Task.Delay(1500); // Optimized: Reduced from 2000ms to 1500ms
                await ReturnBagsToPlayer(playerId);
            }
            catch (Exception ex)
            {
                LogDebug($"[CONTAINER PROCESSING] Error processing containers: {ex.Message}");
            }
        }

        private static async Task<int> ScanAndProcessContainerContents(Container container, Item bagItem, int playerId)
        {
            try
            {
                LogDebug($"[BAG SCAN] *** STARTING SCAN *** Scanning contents of container: {container.Item?.Name ?? "Unknown"}");

                // Use container.Items directly (LootManager approach)
                var bagContents = container.Items.ToList();
                int processedCount = 0;

                if (!bagContents.Any())
                {
                    LogDebug($"[BAG SCAN] Container {container.Item?.Name ?? "Unknown"} is empty");
                    return 0;
                }

                LogDebug($"[BAG SCAN] Found {bagContents.Count} items in container");

                // Log received bag contents for detailed trade log
                var itemNames = bagContents.Select(item => GetItemDisplayName(item)).ToList();
                LogReceivedBag(playerId, container.Item?.Name ?? "Unknown", itemNames);

                // Track item ownership for leftover recovery
                var playerName = GetPlayerName(playerId);
                if (!string.IsNullOrEmpty(playerName))
                {
                    // CRITICAL FIX: Track the bag container itself as received from player
                    string bagKey = $"{bagItem.Name}_{bagItem.Id}";
                    _itemToPlayerMapping[bagKey] = playerName;
                    LogDebug($"[ITEM TRACKING] Mapped bag container {bagKey} to player {playerName}");

                    // Track the bag container as received item
                    Core.ItemTracker.ProcessReceivedItems(new List<Item> { bagItem }, playerName);
                    LogDebug($"[COMPREHENSIVE TRACKING] Tracked received bag container: {bagItem.Name} from {playerName}");

                    foreach (var item in bagContents)
                    {
                        string itemKey = $"{item.Name}_{item.Id}";
                        _itemToPlayerMapping[itemKey] = playerName;
                        LogDebug($"[ITEM TRACKING] Mapped bag item {itemKey} to player {playerName}");

                        // COMPREHENSIVE INVENTORY TRACKING: Track as received item
                        Core.ItemTracker.ProcessReceivedItems(new List<Item> { item }, playerName);
                        LogDebug($"[COMPREHENSIVE TRACKING] Tracked received bag item: {item.Name} from {playerName}");
                    }
                }



                // Use the new recipe analysis system
                LogDebug($"[BAG SCAN] Using new recipe analysis system for {bagContents.Count} items");
                var result = await AnalyzeBagForRecipes(container, bagContents);
                processedCount = result.ProcessedCount;

                // NOTE: Implant processing is now handled by ImplantRecipe.cs through the RecipeManager system above
                // Removed duplicate ProcessImplantsInBag call to follow DEVELOPMENT_RULES.md Rule #6

                LogDebug($"[BAG SCAN] Completed scanning container {container.Item?.Name ?? "Unknown"}. Processed {processedCount} items.");
                return processedCount;
            }
            catch (Exception ex)
            {
                LogDebug($"[BAG SCAN] Error scanning bag contents: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// IMPLANT TRADE: Uses unified infrastructure (logging, tracking, mapping) but custom implant processing
        /// </summary>
        private static async Task<int> ScanAndProcessContainerContentsWithCustomImplantProcessing(Container container, Item bagItem, int playerId, string playerName)
        {
            try
            {
                LogDebug($"[BAG SCAN] *** STARTING IMPLANT SCAN *** Scanning contents of container: {container.Item?.Name ?? "Unknown"}");

                // Use container.Items directly (LootManager approach)
                var bagContents = container.Items.ToList();
                int processedCount = 0;

                if (!bagContents.Any())
                {
                    LogDebug($"[BAG SCAN] Container {container.Item?.Name ?? "Unknown"} is empty");
                    return 0;
                }

                LogDebug($"[BAG SCAN] Found {bagContents.Count} items in container");

                // UNIFIED INFRASTRUCTURE: Log received bag contents for detailed trade log
                var itemNames = bagContents.Select(item => GetItemDisplayName(item)).ToList();
                LogReceivedBag(playerId, container.Item?.Name ?? "Unknown", itemNames);

                // UNIFIED INFRASTRUCTURE: Track item ownership for leftover recovery
                if (!string.IsNullOrEmpty(playerName))
                {
                    // Track the bag container itself as received from player
                    string bagKey = $"{bagItem.Name}_{bagItem.Id}";
                    _itemToPlayerMapping[bagKey] = playerName;
                    LogDebug($"[ITEM TRACKING] Mapped bag container {bagKey} to player {playerName}");

                    // Track the bag container as received item
                    Core.ItemTracker.ProcessReceivedItems(new List<Item> { bagItem }, playerName);
                    LogDebug($"[COMPREHENSIVE TRACKING] Tracked received bag container: {bagItem.Name} from {playerName}");

                    foreach (var item in bagContents)
                    {
                        string itemKey = $"{item.Name}_{item.Id}";
                        _itemToPlayerMapping[itemKey] = playerName;
                        LogDebug($"[ITEM TRACKING] Mapped bag item {itemKey} to player {playerName}");

                        // COMPREHENSIVE INVENTORY TRACKING: Track as received item
                        Core.ItemTracker.ProcessReceivedItems(new List<Item> { item }, playerName);
                        LogDebug($"[COMPREHENSIVE TRACKING] Tracked received bag item: {item.Name} from {playerName}");
                    }
                }

                // CUSTOM PROCESSING: Use custom implant processing instead of recipe system
                LogDebug($"[BAG SCAN] Using custom implant processing for {bagContents.Count} items");
                LogDebug($"[BAG SCAN] About to call Core.ImplantTradeManager.ProcessImplantTrade for player: {playerName}");

                try
                {
                    await Core.ImplantTradeManager.ProcessImplantTrade(playerName, bagContents);
                    LogDebug($"[BAG SCAN] Core.ImplantTradeManager.ProcessImplantTrade completed successfully");

                    // CRITICAL: Move processed items back to bag (following unified recipe workflow)
                    LogDebug($"[BAG SCAN] Moving processed items back to bag container");
                    await Recipes.RecipeUtilities.MoveProcessedItemsBackToContainer(container, "Custom Implant Processing");
                }
                catch (Exception ex)
                {
                    LogDebug($"[BAG SCAN] ERROR in Core.ImplantTradeManager.ProcessImplantTrade: {ex.Message}");
                    LogDebug($"[BAG SCAN] Stack trace: {ex.StackTrace}");
                }

                // For now, assume all items were processed (custom implant processing doesn't return count)
                processedCount = bagContents.Count;

                LogDebug($"[BAG SCAN] Completed implant scanning container {container.Item?.Name ?? "Unknown"}. Processed {processedCount} items.");
                return processedCount;
            }
            catch (Exception ex)
            {
                LogDebug($"[BAG SCAN] Error scanning bag contents with custom implant processing: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// TREATMENT LIBRARY TRADE: Uses unified infrastructure (logging, tracking, mapping) but custom treatment library processing
        /// </summary>
        private static async Task<int> ScanAndProcessContainerContentsWithCustomTreatmentLibraryProcessing(Container container, Item bagItem, int playerId, string playerName)
        {
            try
            {
                LogDebug($"[BAG SCAN] *** STARTING TREATMENT LIBRARY SCAN *** Scanning contents of container: {container.Item?.Name ?? "Unknown"}");

                // Use container.Items directly (LootManager approach)
                var bagContents = container.Items.ToList();
                int processedCount = 0;

                if (!bagContents.Any())
                {
                    LogDebug($"[BAG SCAN] Container {container.Item?.Name ?? "Unknown"} is empty");
                    return 0;
                }

                LogDebug($"[BAG SCAN] Found {bagContents.Count} items in container");

                // Log received bag contents for detailed trade log
                var itemNames = bagContents.Select(item => GetItemDisplayName(item)).ToList();
                LogReceivedBag(playerId, container.Item?.Name ?? "Unknown", itemNames);

                // UNIFIED INFRASTRUCTURE: Track item ownership for leftover recovery
                foreach (var item in bagContents)
                {
                    string itemKey = $"{item.Name}_{item.Id}";
                    _itemToPlayerMapping[itemKey] = playerName;
                    LogDebug($"[ITEM TRACKING] Mapped bag item {itemKey} to player {playerName}");

                    // COMPREHENSIVE INVENTORY TRACKING: Track as received item
                    Core.ItemTracker.ProcessReceivedItems(new List<Item> { item }, playerName);
                    LogDebug($"[COMPREHENSIVE TRACKING] Tracked received bag item: {item.Name} from {playerName}");
                }

                // CUSTOM PROCESSING: Use custom treatment library processing instead of recipe system
                LogDebug($"[BAG SCAN] Using custom treatment library processing for {bagContents.Count} items");
                LogDebug($"[BAG SCAN] About to call Core.TreatmentLibraryTradeManager.ProcessTreatmentLibraryTrade for player: {playerName}");

                try
                {
                    await Core.TreatmentLibraryTradeManager.ProcessTreatmentLibraryTrade(playerName, bagContents);
                    LogDebug($"[BAG SCAN] Core.TreatmentLibraryTradeManager.ProcessTreatmentLibraryTrade completed successfully");

                    // CRITICAL: Move processed items back to bag (following unified recipe workflow)
                    LogDebug($"[BAG SCAN] Moving processed items back to bag container");
                    await Recipes.RecipeUtilities.MoveProcessedItemsBackToContainer(container, "Custom Treatment Library Processing");
                }
                catch (Exception ex)
                {
                    LogDebug($"[BAG SCAN] ERROR in Core.TreatmentLibraryTradeManager.ProcessTreatmentLibraryTrade: {ex.Message}");
                    LogDebug($"[BAG SCAN] Stack trace: {ex.StackTrace}");
                }

                // For now, assume all items were processed (custom treatment library processing doesn't return count)
                processedCount = bagContents.Count;

                LogDebug($"[BAG SCAN] Completed treatment library scanning container {container.Item?.Name ?? "Unknown"}. Processed {processedCount} items.");
                return processedCount;
            }
            catch (Exception ex)
            {
                LogDebug($"[BAG SCAN] Error scanning bag contents with custom treatment library processing: {ex.Message}");
                return 0;
            }
        }

        private static async Task ReturnBagsToPlayer(int playerId)
        {
            try
            {
                // LogDebug($"[BAG RETURN] Attempting to return bags to player {playerId}"); // Commented out to reduce spam

                // Store items for pending return (in case trade gets cancelled)
                if (_playerBags.ContainsKey(playerId))
                {
                    var bags = _playerBags[playerId];
                    _pendingReturns[playerId] = bags.ToList();
                    _playersWithPendingReturn.Add(playerId);

                    // Set timeout for 2 minutes from now
                    _returnTimeouts[playerId] = DateTime.Now.Add(_returnTimeout);

                    // LogDebug($"[BAG RETURN] Stored {bags.Count} items for return to player {playerId}"); // Commented out to reduce spam
                    // LogDebug($"[BAG RETURN] Return timeout set for {_returnTimeouts[playerId]:HH:mm:ss}"); // Commented out to reduce spam
                }

                // Start the timeout monitoring task
                _ = Task.Run(async () => await MonitorReturnTimeout(playerId));

                // Attempt to return items
                await AttemptReturnToPlayer(playerId);
            }
            catch (Exception ex)
            {
                LogDebug($"[BAG RETURN] Error in return process: {ex.Message}");
            }
        }

        private static async Task AttemptReturnToPlayer(int playerId)
        {
            try
            {
                // Find the player
                var player = DynelManager.Players.FirstOrDefault(p => p.Identity.Instance == playerId);
                if (player == null)
                {
                    LogDebug($"[BAG RETURN] Player {playerId} not found nearby, scheduling retry in 30 seconds");

                    // CRITICAL FIX: Always schedule retry when player not found
                    _ = Task.Delay(30000).ContinueWith(_ =>
                    {
                        LogDebug($"[BAG RETURN] Retrying return for player {playerId} (player not found retry)");
                        _ = Task.Run(() => AttemptReturnToPlayer(playerId));
                    });
                    return;
                }

                float distance = player.DistanceFrom(DynelManager.LocalPlayer);
                if (distance > 10f) // Use same distance as trade initiation (10m)
                {
                    LogDebug($"[BAG RETURN] Player {playerId} too far away ({distance:F1}m), will retry when closer");

                    // Only send distance message once every 2 minutes to avoid spam
                    bool shouldSendMessage = true;
                    if (_lastDistanceMessage.ContainsKey(playerId))
                    {
                        var timeSinceLastMessage = DateTime.Now - _lastDistanceMessage[playerId];
                        if (timeSinceLastMessage.TotalMinutes < 2)
                        {
                            shouldSendMessage = false;
                        }
                    }

                    if (shouldSendMessage)
                    {
                        // Send message to player about distance requirement
                        SendPrivateMessage(playerId.ToString(),
                            $"Your processed items are ready for return! Please come closer (within 10m) so I can trade them back to you. Current distance: {distance:F1}m");
                        _lastDistanceMessage[playerId] = DateTime.Now;
                    }

                    // Schedule a more frequent retry - every 15 seconds instead of 30
                    _ = Task.Delay(15000).ContinueWith(_ =>
                    {
                        LogDebug($"[BAG RETURN] Retrying return for player {playerId} (distance retry)");
                        _ = Task.Run(() => AttemptReturnToPlayer(playerId));
                    });

                    return;
                }

                // Check if we have items to return
                if (!_pendingReturns.ContainsKey(playerId))
                {
                    LogInfo($"[BAG RETURN] No pending items for player {playerId} - checking if this is an error");

                    // CRITICAL FIX: Check if player still has retry count (indicates items should exist)
                    if (_returnRetryCount.ContainsKey(playerId))
                    {
                        LogDebug($"[BAG RETURN] ERROR: Player {playerId} has retry count but no pending items - this indicates data corruption");
                        LogDebug($"[BAG RETURN] Cleaning up retry tracking and resetting bot state");

                        // Clean up corrupted state
                        _returnRetryCount.Remove(playerId);
                        _playersWithPendingReturn.Remove(playerId);
                        _playerBags.Remove(playerId);

                        // Send error message to player
                        SendPrivateMessage(playerId.ToString(), "❌ Error: Lost track of your items during return process. Please contact an admin if you're missing items.");
                    }

                    // CRITICAL: Reset bot state when there are no items to return
                    if (_currentProcessingPlayer == playerId)
                    {
                        _currentBotState = BotState.Ready;
                        _currentProcessingPlayer = null;
                        LogInfo($"[BOT STATE] No items to return - changed to Ready");
                        ProcessNextInQueue();
                    }

                    // Notify player that processing is complete (only if no error detected)
                    if (!_returnRetryCount.ContainsKey(playerId))
                    {
                        SendPrivateMessage(playerId.ToString(), "✅ Trade completed! Processing finished.");
                    }
                    return;
                }

                // Small delay to ensure state is fully set before opening trade
                await Task.Delay(500);

                // Open trade with player to return items
                LogDebug($"[BAG RETURN] Opening return trade to {player.Name} - State: {_currentBotState}, Pending: {_pendingReturns.ContainsKey(playerId)}");

                // CRITICAL FIX: Set _currentTradeTarget so decline handler can properly reset bot state
                _currentTradeTarget = player.Identity;
                LogDebug($"[RETURN TRADE FIX] Set _currentTradeTarget to {playerId} for return trade decline handling");

                Trade.Open(player.Identity);

                // Wait a moment for trade to open
                await Task.Delay(1000);

                // Add bags back to trade
                var itemsToReturn = _pendingReturns[playerId];
                foreach (var bag in itemsToReturn)
                {
                    if (Inventory.Items.Contains(bag)) // Make sure bag still exists
                    {
                        // RULE #5 EMERGENCY FAILSAFE: NEVER RETURN BOT'S PERSONAL TOOLS TO PLAYERS
                        // But allow player-provided tools to be returned
                        if (IsProcessingTool(bag))
                        {
                            LogDebug($"[TOOL PROTECTION] ❌ BLOCKED TOOL: {GetItemDisplayName(bag)} (ID: {bag.Id}) - TOOLS MUST NEVER BE GIVEN TO PLAYERS!");
                            continue; // Skip this item - DO NOT add to trade
                        }

                        // LogDebug($"[BAG RETURN] Adding bag {bag.Name} to trade"); // Commented out to reduce spam
                        Trade.AddItem(bag.Slot);

                        // Log returned bag for detailed trade log
                        if (bag.UniqueIdentity.Type == IdentityType.Container)
                        {
                            var container = Inventory.Containers.FirstOrDefault(c => c.Identity.Instance == bag.UniqueIdentity.Instance);
                            if (container != null)
                            {
                                var bagContents = container.Items.Select(item => GetItemDisplayName(item)).ToList();
                                LogReturnedBag(playerId, bag.Name, bagContents);
                            }
                            else
                            {
                                LogReturnedBag(playerId, bag.Name, new List<string>());
                            }
                        }
                        else
                        {
                            // TRADE LOGGING MOVED: Logging now happens only after successful return trade completion
                            // This prevents logging items as "returned" when the return trade gets declined
                            LogDebug($"[RETURN TRADE SETUP] Adding item to return trade: {GetItemDisplayName(bag)} (logging deferred until completion)");
                        }

                        await Task.Delay(150); // Optimized: Reduced from 200ms to 150ms
                    }
                }

                // LogDebug($"[BAG RETURN] Added {itemsToReturn.Count} processed bag(s) to trade"); // Commented out to reduce spam

                // Clear distance message tracking since we successfully initiated the return trade
                _lastDistanceMessage.Remove(playerId);
            }
            catch (Exception ex)
            {
                LogDebug($"[BAG RETURN] Error attempting return: {ex.Message}");

                // CRITICAL FIX: Complete trade log even if return trade fails
                // This ensures all trades are logged regardless of return trade success
                if (_currentTradeLogs.ContainsKey(playerId))
                {
                    LogDebug($"[TRADE LOG] Completing trade log due to return error for player {playerId}");
                    var bagsReturned = _pendingReturns.ContainsKey(playerId) ? _pendingReturns[playerId].Count : 0;
                    CompleteTradeLog(playerId, bagsReturned);
                }
            }
        }



        // OLD CanProcessWith methods REMOVED - now handled by individual recipe classes (PearlRecipe.CanProcess, etc.)



        // OLD CanProcessWithSmelting, CanProcessWithTara, CanProcessWithMantis methods REMOVED
        // - now handled by SmeltingRecipe.CanProcess, TaraArmorRecipe.CanProcess, MantisArmorRecipe.CanProcess



        // OLD PBPatternStage enum REMOVED - now handled by PBPatternRecipe.cs

        // OLD RingOfPowerStage enum REMOVED - now handled by VTERecipe.cs

        // OLD DetectPBPatternStage method REMOVED - now handled by PBPatternRecipe.cs

        // OLD IsCompletedPBPattern method REMOVED - now handled by PBPatternRecipe.cs

        // OLD GetValidPBPatternNames method REMOVED - now handled by PBPatternRecipe.cs

        // OLD IsValidPBPatternName method REMOVED - now handled by PBPatternRecipe.cs

        // OLD DetectRingOfPowerStage method REMOVED - now handled by VTERecipe.cs Ring of Power logic

        // OLD CanProcessWithRingOfPower method REMOVED - now handled by VTERecipe.cs Ring of Power logic



        // OLD ProcessItemWithPearl, ProcessItemWithPlasma methods REMOVED - now handled by recipe classes through BaseRecipeProcessor

        // OLD BotProcessPearl method REMOVED - now handled by PearlRecipe.cs through BaseRecipeProcessor

        // OLD BotProcessPlasma, MovePlasmaComponentsToInventory, ProcessAllMonsterPartsInInventory methods REMOVED
        // - now handled by PlasmaRecipe.cs through BaseRecipeProcessor

        // OLD BotProcessIce method REMOVED - now handled by IceRecipe.cs through BaseRecipeProcessor

        // OLD BotProcessSmelting method REMOVED - now handled by SmeltingRecipe.cs through BaseRecipeProcessor

        // OLD BotProcessTara method REMOVED - now handled by TaraArmorRecipe.cs through BaseRecipeProcessor

        // OLD BotProcessMantis method REMOVED - now handled by MantisArmorRecipe.cs through BaseRecipeProcessor



        // OLD ProcessItemWithRelay, ProcessItemWithIce methods REMOVED - now handled by recipe classes through BaseRecipeProcessor



















        // OLD ExtractPatternName method REMOVED - now handled by PBPatternRecipe.cs

        // OLD ValidateVTEComponents method REMOVED - now handled by VTERecipe.CanProcess and VTERecipe.AnalyzeItems

        // OLD VTEStage enum REMOVED - now handled by VTERecipe.VTEStage

        // OLD DetectVTEStage method REMOVED - now handled by VTERecipe.DetectVTEStage

        /// <summary>
        /// Check if there's enough inventory space for processing items
        /// </summary>
        public static bool CheckInventorySpaceForProcessing(int itemCount, string recipeName)
        {
            try
            {
                int freeSlots = Inventory.NumFreeSlots;
                int requiredSlots = itemCount + 1; // +1 safety buffer

                LogDebug($"[{recipeName}] Inventory check: {freeSlots} free slots, need {requiredSlots}");
                return freeSlots >= requiredSlots;
            }
            catch
            {
                // If check fails, assume safe (conservative approach)
                return true;
            }
        }

        /// <summary>
        /// Simple transaction logging for Craftbot (replaces ItemTracker.LogTransaction)
        /// </summary>
        public static void LogTransaction(string playerName, string message)
        {
            LogDebug($"[TRANSACTION] {playerName}: {message}");
        }

        /// <summary>
        /// CLIENTLESS EVENT-DRIVEN ITEM DETECTION: Handle item added to inventory
        /// </summary>
        private static void OnItemAdded(Item item)
        {
            try
            {
                LogDebug($"[ITEM EVENT] Item added - Tracking: {_isTrackingItems}, CurrentPlayer: {_currentProcessingPlayer}, SlotType: {item.Slot.Type}");

                // Only track items when we're expecting them from a trade
                if (!_isTrackingItems)
                {
                    LogDebug($"[ITEM EVENT] Not tracking items - ignoring");
                    return;
                }

                // Only track inventory items (not equipped items)
                if (item.Slot.Type != IdentityType.Inventory)
                {
                    LogDebug($"[ITEM EVENT] Not inventory item - ignoring");
                    return;
                }

                LogDebug($"[ITEM EVENT] Item added to inventory: {GetItemDisplayName(item)} (ID: {item.Id}, Slot: {item.Slot.Instance})");

                // For event-driven detection, we need to track items even if _currentProcessingPlayer is null
                // because the event might fire before the player is set
                int playerId = _currentProcessingPlayer ?? 0; // Use 0 as fallback

                if (!_tradedItemsByPlayer.ContainsKey(playerId))
                    _tradedItemsByPlayer[playerId] = new List<Item>();

                _tradedItemsByPlayer[playerId].Add(item);
                LogDebug($"[ITEM EVENT] Added item to player {playerId} list (total: {_tradedItemsByPlayer[playerId].Count})");

                // COMPREHENSIVE INVENTORY TRACKING: Track this as a received item
                if (playerId > 0)
                {
                    var playerName = GetPlayerName(playerId);
                    Core.ItemTracker.ProcessReceivedItems(new List<Item> { item }, playerName);
                    LogDebug($"[INVENTORY TRACKING] Tracked received item: {GetItemDisplayName(item)} from {playerName}");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[ITEM EVENT] Error in OnItemAdded: {ex.Message}");
            }
        }

        /// <summary>
        /// CLIENTLESS EVENT-DRIVEN ITEM DETECTION: Handle item removed from inventory
        /// </summary>
        private static void OnItemRemoved(Item item)
        {
            try
            {
                LogDebug($"[ITEM EVENT] Item removed from inventory: {GetItemDisplayName(item)} (ID: {item.Id})");
            }
            catch (Exception ex)
            {
                LogDebug($"[ITEM EVENT] Error in OnItemRemoved: {ex.Message}");
            }
        }

        /// <summary>
        /// Get item name with fallback for clientless API and force name loading
        /// Now includes quality level (QL) information for trade logging
        /// </summary>
        private static string GetItemDisplayName(Item item)
        {
            if (item == null) return "NULL_ITEM";

            string itemName = "";

            // Try to get the name, with fallback for empty names
            if (!string.IsNullOrEmpty(item.Name))
            {
                itemName = item.Name;
            }
            else
            {
                // CLIENTLESS FIX: Try to force item name loading from ItemData
                string forcedName = TryForceItemNameLoading(item);
                if (!string.IsNullOrEmpty(forcedName))
                {
                    itemName = forcedName;
                }
                else
                {
                    // NEW: Try to get name from network-captured item data
                    string networkName = TryGetItemNameFromNetwork(item);
                    if (!string.IsNullOrEmpty(networkName))
                    {
                        itemName = networkName;
                    }
                    else
                    {
                        // Fallback to ID-based display name
                        itemName = $"Unknown Item (ID: {item.Id})";
                    }
                }
            }

            // Extract item properties using ItemTracker (quality level and stack count)
            var (stackCount, qualityLevel) = Core.ItemTracker.ExtractItemProperties(item);

            // Add quality level if it's greater than 0
            if (qualityLevel > 0)
                itemName += $" QL{qualityLevel}";

            // Add stack count if it's greater than 1
            if (stackCount > 1)
                itemName += $" x{stackCount}";

            return itemName;
        }

        /// <summary>
        /// Test ItemData loading capability during initialization
        /// </summary>
        private static void TestItemDataLoading()
        {
            try
            {
                // LogDebug($"[ITEM DATA] Testing ItemData loading capability..."); // Hidden for cleaner logs

                // Test with a common item ID (Nano Programming Interface = 291043)
                if (ItemData.Find(291043, out DummyItem testItem))
                {
                    // LogDebug($"[ITEM DATA] ✅ Successfully loaded test item: {testItem.Name} (ID: 291043)"); // Hidden for cleaner logs
                }
                else
                {
                    LogDebug($"[ITEM DATA] ❌ Failed to load test item ID 291043");
                }

                // Test with another common item ID
                if (ItemData.Find(161699, out DummyItem testItem2))
                {
                    // LogDebug($"[ITEM DATA] ✅ Successfully loaded test item: {testItem2.Name} (ID: 161699)"); // Hidden for cleaner logs
                }
                else
                {
                    LogDebug($"[ITEM DATA] ❌ Failed to load test item ID 161699");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[ITEM DATA] Error testing ItemData loading: {ex.Message}");
            }
        }

        /// <summary>
        /// Attempt to force item name loading from ItemData in clientless
        /// </summary>
        private static string TryForceItemNameLoading(Item item)
        {
            try
            {
                // Method 1: Try to get item data directly from ItemData using the item's ID (most common)
                if (ItemData.Find(item.Id, out DummyItem itemData))
                {
                    if (!string.IsNullOrEmpty(itemData.Name))
                    {
                        LogDebug($"[ITEM NAME] Successfully loaded name from ItemData: {itemData.Name} for ID {item.Id}");
                        return itemData.Name;
                    }
                }

                // Method 2: Try using the item's HighId (for upgraded items)
                if (item.HighId != 0 && item.HighId != item.Id && ItemData.Find(item.HighId, out DummyItem highItemData))
                {
                    if (!string.IsNullOrEmpty(highItemData.Name))
                    {
                        LogDebug($"[ITEM NAME] Successfully loaded name from HighId: {highItemData.Name} for HighID {item.HighId}");
                        return highItemData.Name;
                    }
                }

                // Method 3: Try NanoItem for nano programs
                if (ItemData.Find(item.Id, out NanoItem nanoData))
                {
                    if (!string.IsNullOrEmpty(nanoData.Name))
                    {
                        LogDebug($"[ITEM NAME] Successfully loaded name from NanoItem: {nanoData.Name} for ID {item.Id}");
                        return nanoData.Name;
                    }
                }

                // Method 4: Try creating a new item with proper constructor (mimics AOSharp.Clientless Item.CreateItem)
                try
                {
                    var testItem = new Item(item.Id, item.HighId, item.Ql);
                    if (!string.IsNullOrEmpty(testItem.Name))
                    {
                        LogDebug($"[ITEM NAME] Successfully loaded name via Item constructor: {testItem.Name} for ID {item.Id}");
                        return testItem.Name;
                    }
                }
                catch (Exception createEx)
                {
                    LogDebug($"[ITEM NAME] Item constructor method failed: {createEx.Message}");
                }

                LogDebug($"[ITEM NAME] All methods failed for item ID {item.Id}, HighId {item.HighId}, Ql {item.Ql}");
                return null;
            }
            catch (Exception ex)
            {
                LogDebug($"[ITEM NAME] Error loading name for item ID {item.Id}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Try to get item name from network-captured data or known item mappings
        /// </summary>
        private static string TryGetItemNameFromNetwork(Item item)
        {
            try
            {
                // Check if we have this item name cached from previous successful lookups
                string cacheKey = $"{item.Id}_{item.HighId}_{item.Ql}";
                if (_itemNameCache.ContainsKey(cacheKey))
                {
                    LogDebug($"[ITEM NAME] Found cached name: {_itemNameCache[cacheKey]} for ID {item.Id}");
                    return _itemNameCache[cacheKey];
                }

                // Network logging disabled for cleaner logs (can be manually enabled with 'debug netlog')
                // Core.NetworkItemLogger.StartLogging();

                // Request item information if possible
                try
                {
                    // Try to get item info through info request (if item is visible)
                    if (item.UniqueIdentity != Identity.None)
                    {
                        Client.InfoRequest(item.UniqueIdentity);
                        // LogDebug($"[ITEM NAME] Requested info for item {item.Id} via network"); // Hidden for cleaner logs
                    }
                }
                catch (Exception infoEx)
                {
                    LogDebug($"[ITEM NAME] Info request failed: {infoEx.Message}");
                }

                // Use known common item mappings as fallback
                string knownName = GetKnownItemName(item.Id);
                if (!string.IsNullOrEmpty(knownName))
                {
                    // Cache the result
                    _itemNameCache[cacheKey] = knownName;
                    // LogDebug($"[ITEM NAME] Found known item name: {knownName} for ID {item.Id}"); // Hidden for cleaner logs
                    return knownName;
                }

                return null;
            }
            catch (Exception ex)
            {
                LogDebug($"[ITEM NAME] Error in TryGetItemNameFromNetwork: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get known item names for common items that appear in trades
        /// </summary>
        private static string GetKnownItemName(int itemId)
        {
            // Common items that appear in trades - based on user's trade logs
            var knownItems = new Dictionary<int, string>
            {
                // From user's trade logs - these are the actual items being traded
                { 161699, "Nano Programming Interface" },
                { 291043, "Nano Programming Interface" },
                { 101181, "Basic Implant" },
                { 166125, "Cluster" },
                { 101365, "Basic Tool" },
                { 101327, "Basic Component" },

                // Common AO items that frequently appear in trades
                { 247707, "Blood Plasma" },
                { 247708, "Perfectly Cut Gem" },
                { 247709, "Perfectly Cut Pearl" },
                { 247710, "Perfectly Cut Amber" },
                { 247711, "Perfectly Cut Coral" },
                { 247712, "Perfectly Cut Crystal Sphere" },
                { 247713, "Perfectly Cut Ember" },
                { 247714, "Perfectly Cut Ember Sphere" },
                { 247715, "Perfectly Cut Pearl of Rubi-Ka" },

                // Common implants and clusters
                { 86, "Basic Right-Arm Implant" },
                { 87, "Basic Left-Arm Implant" },
                { 88, "Basic Right-Hand Implant" },
                { 89, "Basic Left-Hand Implant" },
                { 90, "Basic Eye Implant" },
                { 91, "Basic Head Implant" },
                { 92, "Basic Chest Implant" },
                { 93, "Basic Leg Implant" },
                { 94, "Basic Feet Implant" },
                { 95, "Basic Waist Implant" }
                // Add more common items as they are identified
            };

            return knownItems.ContainsKey(itemId) ? knownItems[itemId] : null;
        }

        /// <summary>
        /// SIMPLE TRADE LOGGING: Capture bot's complete inventory at startup
        /// </summary>
        private static void CaptureOriginalBotInventory()
        {
            try
            {
                if (_originalInventoryCaptured) return;

                _originalBotInventory.Clear();

                // Capture startup inventory (debug messages hidden for cleaner logs)

                // Capture all loose items (EXCLUDE equipment pages - armor, weapons, implants)
                if (Inventory.Items != null)
                {
                    foreach (var item in Inventory.Items)
                    {
                        if (item != null)
                        {
                            // EXCLUDE equipment pages - they should NEVER be touched
                            if (item.Slot.Type == IdentityType.ArmorPage ||
                                item.Slot.Type == IdentityType.WeaponPage ||
                                item.Slot.Type == IdentityType.ImplantPage ||
                                item.Slot.Type == IdentityType.SocialPage)
                            {
                                continue; // Silently exclude equipment items
                            }

                            _originalBotInventory.Add(item.Id);
                            // LogDebug($"[STARTUP INVENTORY] Captured bot item: Id={item.Id}, Name='{item.Name}', Slot={item.Slot.Type}:{item.Slot.Instance}"); // Hidden for cleaner logs
                        }
                    }
                }
                else
                {
                    LogDebug($"[STARTUP INVENTORY] ❌ Inventory.Items is null!");
                }

                // Capture all items in bags
                if (Inventory.Containers != null)
                {
                    foreach (var container in Inventory.Containers)
                    {
                        if (container != null)
                        {
                            _originalBotInventory.Add(container.Identity.Instance); // The bag itself
                            // LogDebug($"[STARTUP INVENTORY] Captured bot bag: Id={container.Identity.Instance}");

                            if (container.Items != null)
                            {
                                foreach (var bagItem in container.Items)
                                {
                                    if (bagItem != null)
                                    {
                                        _originalBotInventory.Add(bagItem.Id);
                                        // LogDebug($"[STARTUP INVENTORY] Captured bot bag item: Id={bagItem.Id}, Name='{bagItem.Name}'");
                                    }
                                }
                            }
                        }
                    }
                }

                _originalInventoryCaptured = true;
                LogDebug($"[STARTUP INVENTORY] ✅ Captured {_originalBotInventory.Count} bot items at startup");
            }
            catch (Exception ex)
            {
                LogDebug($"[STARTUP INVENTORY] ❌ Error capturing original inventory: {ex.Message}");
                LogDebug($"[STARTUP INVENTORY] Will retry on first trade");
            }
        }

        /// <summary>
        /// SIMPLE TRADE LOGGING: Capture bot's inventory after bags are opened (called from startup)
        /// </summary>
        public static void CaptureOriginalBotInventoryAfterBagsOpen()
        {
            try
            {
                LogDebug($"[STARTUP INVENTORY] Starting post-bag-opening inventory capture...");

                // Reset the capture flag to allow recapture
                _originalInventoryCaptured = false;

                // Call the main capture method
                CaptureOriginalBotInventory();

                LogDebug($"[STARTUP INVENTORY] Post-bag-opening capture complete - total items: {_originalBotInventory.Count}");
                LogDebug($"[STARTUP INVENTORY] Bag contents should now be captured properly");
            }
            catch (Exception ex)
            {
                LogDebug($"[STARTUP INVENTORY] Error in post-bag-opening capture: {ex.Message}");
            }
        }

        /// <summary>
        /// SIMPLE TRADE LOGGING: Check if an item belongs to the bot's original inventory
        /// </summary>
        private static bool IsBotOriginalItem(int itemId)
        {
            return _originalBotInventory.Contains(itemId);
        }

        /// <summary>
        /// Clear the debug log file on startup to keep it current
        /// </summary>
        private static void ClearDebugLog()
        {
            try
            {
                lock (_logLock)
                {
                    // Clear the log file by overwriting it with startup header
                    string startupHeader = $"===== CRAFTBOT DEBUG LOG - STARTUP {DateTime.Now:yyyy-MM-dd HH:mm:ss} =====" + Environment.NewLine;
                    System.IO.File.WriteAllText(_debugLogPath, startupHeader);
                }
            }
            catch
            {
                // Silent error handling for log clearing
            }
        }

        public static void LogDebug(string message)
        {
            try
            {
                // Write to both clientless Logger AND file
                Logger.Debug(message);

                // Also write to file with timestamp
                lock (_logLock)
                {
                    string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [DEBUG] {message}";
                    System.IO.File.AppendAllText(_debugLogPath, logEntry + Environment.NewLine);
                }
            }
            catch
            {
                // Silent error handling for logging
            }
        }

        private static void LogCritical(string message)
        {
            try
            {
                // Log critical messages with special formatting
                lock (_logLock)
                {
                    string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [CRITICAL] {message}";
                    System.IO.File.AppendAllText(_debugLogPath, logEntry + Environment.NewLine);
                }

                // Also use clientless Logger for critical messages
                Logger.Error(message);
            }
            catch
            {
                // Silent error handling for logging
            }
        }

        /// <summary>
        /// Log informational messages
        /// </summary>
        public static void LogInfo(string message)
        {
            try
            {
                Logger.Information(message);

                lock (_logLock)
                {
                    string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [INFO] {message}";
                    System.IO.File.AppendAllText(_debugLogPath, logEntry + Environment.NewLine);
                }
            }
            catch
            {
                // Silent error handling for logging
            }
        }

        /// <summary>
        /// Log warning messages
        /// </summary>
        public static void LogWarning(string message)
        {
            try
            {
                Logger.Warning(message);

                lock (_logLock)
                {
                    string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [WARNING] {message}";
                    System.IO.File.AppendAllText(_debugLogPath, logEntry + Environment.NewLine);
                }
            }
            catch
            {
                // Silent error handling for logging
            }
        }

        /// <summary>
        /// Log error messages
        /// </summary>
        public static void LogError(string message)
        {
            try
            {
                Logger.Error(message);

                lock (_logLock)
                {
                    string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [ERROR] {message}";
                    System.IO.File.AppendAllText(_debugLogPath, logEntry + Environment.NewLine);
                }
            }
            catch
            {
                // Silent error handling for logging
            }
        }



        private static async Task<InternalRecipeAnalysisResult> AnalyzeBagForRecipes(Container container, List<Item> bagContents)
        {
            try
            {
                LogDebug($"[RECIPE ANALYSIS] Analyzing bag contents against all recipe processes...");











                // Use the new RecipeManager system for ALL recipe processing
                LogDebug($"[RECIPE ANALYSIS] Delegating to RecipeManager for all recipe processing");

                int processedCount = await Recipes.RecipeManager.ProcessBagEfficiently(container);

                if (processedCount > 0)
                {
                    return new InternalRecipeAnalysisResult
                    {
                        RecipeFound = true,
                        RecipeType = "Recipe System",
                        Stage = "Processed",
                        ProcessedCount = processedCount
                    };
                }

                LogDebug($"[RECIPE ANALYSIS] No recipes found for bag contents");
                return new InternalRecipeAnalysisResult { RecipeFound = false, ProcessedCount = 0 };
            }
            catch (Exception ex)
            {
                LogDebug($"[RECIPE ANALYSIS] Error analyzing bag for recipes: {ex.Message}");
                return new InternalRecipeAnalysisResult { RecipeFound = false, ProcessedCount = 0 };
            }
        }

        private static bool HasToolAvailable(string toolName)
        {
            try
            {
                // First check if tool is already in inventory
                var toolInInventory = Inventory.Items.Any(item =>
                    item.Slot.Type == IdentityType.Inventory &&
                    item.Name.Contains(toolName));

                if (toolInInventory)
                {
                    LogDebug($"[TOOL CHECK] {toolName} found in inventory");
                    return true;
                }

                // Check in all opened tool bags
                foreach (var container in Inventory.Containers.Where(c => c.IsOpen))
                {
                    var toolInBag = container.Items.Any(item => item.Name.Contains(toolName));
                    if (toolInBag)
                    {
                        LogDebug($"[TOOL CHECK] {toolName} found in bag {container.Item?.Name ?? "Unknown"}");
                        return true;
                    }
                }

                LogDebug($"[TOOL CHECK] {toolName} not found in inventory or tool bags");
                return false;
            }
            catch (Exception ex)
            {
                LogDebug($"[TOOL CHECK] Error checking for {toolName}: {ex.Message}");
                return false;
            }
        }

        private static bool EnsureToolInInventory(string toolName)
        {
            try
            {
                // First check if tool is already in inventory
                var toolInInventory = Inventory.Items.Any(item =>
                    item.Slot.Type == IdentityType.Inventory &&
                    item.Name.Contains(toolName));

                if (toolInInventory)
                {
                    LogDebug($"[TOOL ENSURE] {toolName} already in inventory");
                    return true;
                }

                // Try to pull tool from bags using centralized FindAndPullTool method
                bool pulled = RecipeUtilities.FindAndPullTool(toolName);
                if (pulled)
                {
                    LogDebug($"[TOOL ENSURE] Successfully pulled {toolName} to inventory");
                    return true;
                }

                LogDebug($"[TOOL ENSURE] Failed to get {toolName} into inventory");
                return false;
            }
            catch (Exception ex)
            {
                LogDebug($"[TOOL ENSURE] Error ensuring {toolName} in inventory: {ex.Message}");
                return false;
            }
        }

        private static int? GetPlayerIdFromContainer(Container container)
        {
            try
            {
                // Try to find the player ID from current trade context or stored data
                if (_currentProcessingPlayer.HasValue)
                {
                    return _currentProcessingPlayer.Value;
                }

                // If no current processing player, we can't determine the player ID
                return null;
            }
            catch
            {
                return null;
            }
        }

        // OLD MoveVTEComponentsToInventory method REMOVED - now handled by VTERecipe.MoveComponentsToInventoryShared

        // OLD ProcessGoldIngotsToWire method REMOVED - now handled by VTERecipe.ProcessGoldIngotsToWire

        // OLD ProcessPBPatternStep method REMOVED - now handled by PBPatternRecipe.cs

        // OLD ProcessSoulFragments method REMOVED - now handled by VTERecipe.ProcessSoulFragments

        // OLD ProcessRobotJunkToSensor method REMOVED - now handled by VTERecipe.ProcessRobotJunkToSensor

        // OLD FindCurrentPBResult method REMOVED - now handled by PBPatternRecipe.cs

        // OLD ProcessMantisEgg method REMOVED - now handled by VTERecipe.ProcessMantisEgg

        // OLD WireMantisEgg method REMOVED - now handled by VTERecipe.WireMantisEgg

        // OLD AddSensorToEgg method REMOVED - now handled by VTERecipe.AddSensorToEgg

        // OLD AddGemsToEgg method REMOVED - now handled by VTERecipe.AddGemsToEgg

        // OLD ProcessNovictumEnhancement method REMOVED - now handled by PBPatternRecipe.cs



        // OLD ProcessCrystalBlueprintCombination method REMOVED - now handled by PBPatternRecipe.cs

        // OLD ProcessFinalNovictalizedEnhancement method REMOVED - now handled by PBPatternRecipe.cs

        // OLD FindToolInInventory method REMOVED - now handled by BaseRecipeProcessor.FindTool()

        // OLD EnsureItemsReturnToBag method REMOVED - now handled by BaseRecipeProcessor.EnsureItemsReturnToBagShared()

        private static bool IsProcessingTool(Item item)
        {
            // RULE #5: TOOLS MUST NEVER UNDER ANY CIRCUMSTANCE BE GIVEN TO PLAYERS
            // COMPREHENSIVE PROTECTION: ALL tools used in ANY recipe MUST be protected

            // CRITICAL: Check if item was received from player FIRST
            // If player gave us this item, it's THEIR item, not ours - return it!
            if (Core.ItemTracker.WasReceivedFromPlayer(item))
            {
                LogDebug($"[TOOL PROTECTION] COMPREHENSIVE: {GetItemDisplayName(item)} is received from player - NOT PROTECTED");
                return false; // NOT a bot tool - it's the player's item
            }

            // CRITICAL FIX: Check bot's own items AFTER checking if received from player
            // Bot's own tools must be protected even if they're temporarily in inventory
            if (Core.ItemTracker.IsBotPersonalItem(item) || Core.ItemTracker.IsBotTool(item))
            {
                LogDebug($"[TOOL PROTECTION] CRITICAL: {GetItemDisplayName(item)} is bot's personal item/tool - PROTECTED");
                return true;
            }

            // CRITICAL FIX: Protect bot's tool bags by name AND ItemTracker
            if (item.UniqueIdentity.Type == IdentityType.Container)
            {
                string itemName = item.Name?.ToLower() ?? "";

                // ABSOLUTE PROTECTION: Bot's tool bags must NEVER be given away
                if (itemName == "tools1" || itemName == "tools2" ||
                    itemName.StartsWith("tools") || itemName.Contains("tool bag") ||
                    Core.ItemTracker.IsBotToolBag(item))
                {
                    LogDebug($"[TOOL PROTECTION] CRITICAL: {GetItemDisplayName(item)} is bot tool bag - PROTECTED");
                    return true;
                }
                else
                {
                    LogDebug($"[TOOL PROTECTION] COMPREHENSIVE: {GetItemDisplayName(item)} is player bag (not bot tool bag) - NOT PROTECTED");
                    return false;
                }
            }

            // Check if this is a player-provided item (only after checking bot's own items)
            if (Core.ItemTracker.IsReceivedItem(item))
            {
                LogDebug($"[TOOL PROTECTION] COMPREHENSIVE: {GetItemDisplayName(item)} is received from player - NOT PROTECTED");
                return false;
            }

            // CRITICAL FIX: Handle null/empty item names with ID-based protection
            if (item?.Name == null || string.IsNullOrEmpty(item.Name))
            {
                // EMERGENCY TOOL PROTECTION: Use item ID as fallback when names are empty
                // But only if not tracked as received item
                LogDebug($"[TOOL PROTECTION] Empty name for item ID {item.Id} - checking fallback protection");
                return IsToolByItemId(item.Id);
            }

            // EXACT TOOL NAMES - Every tool used in every recipe
            string[] exactToolNames = {
                // Pearl/Gem Processing
                "Jensen Gem Cutter",

                // Plasma Processing
                "Bio-Comminutor",
                "Advanced Bio-Comminutor",

                // Ice Processing
                "Nano Programming Interface",
                "Advanced Hacker Tool",

                // Implant Processing
                "Implant Disassembly Clinic",

                // General Processing
                "Screwdriver",

                // Smelting/Metal Processing
                "Precious Metal Reclaimer",

                // Armor Processing
                "Mass Relocating Robot (Shape Soft Armor)",
                "Mass Relocating Robot (Shape Hard Armor)",

                // VTE Processing
                "Ancient Novictum Refiner",
                "Wire Drawing Machine",
                "Personal Furnace",

                // Clumps Processing
                "Kyr'Ozch Structural Analyzer",
                "Kyr'Ozch Atomic Re-Structuralizing Tool",

                // CARB ARMOR TOOLS - CRITICAL PROTECTION
                "HSR - Sketch and Etch - Helmet",
                "HSR - Sketch and Etch - Chestpiece",
                "HSR - Sketch and Etch - Legs",
                "HSR - Sketch and Etch - Arms",
                "HSR - Sketch and Etch - Boots",
                "HSR - Sketch and Etch - Gloves",
                "Clanalizer",
                "Omnifier"
            };

            // PATTERN MATCHING - Catch any tool-like items
            string itemNameLower = item.Name.ToLower();
            string[] toolPatterns = {
                "tool", "cutter", "analyzer", "interface", "reclaimer",
                "relocating robot", "furnace", "machine", "screwdriver",
                "hsr - sketch and etch", "clanalizer", "omnifier",
                "bio-comminutor", "programming", "disassembly", "clinic",
                "structural analyzer"
            };

            // ANCIENT NOVICTUM REFINER - Bot's tool for PB recipe processing (PROTECT THIS)
            if (itemNameLower.Contains("ancient novictum refiner"))
            {
                return true;
            }

            // PURE NOVICTUM RING - Bot's personal item (PROTECT THIS)
            if (itemNameLower.Contains("pure novictum ring"))
            {
                return true;
            }

            // CRITICAL FIX: Exclude PB recipe novictum components from tool detection
            // "Flow of Novictum", "Subdued Flow of Novictum" etc. are PB recipe components, NOT tools
            if (itemNameLower.Contains("novictum") && !itemNameLower.Contains("pure novictum ring") && !itemNameLower.Contains("ancient novictum refiner"))
            {
                LogDebug($"[TOOL PROTECTION] RECIPE COMPONENT: {GetItemDisplayName(item)} is PB recipe novictum component - NOT PROTECTED");
                return false; // PB recipe novictum components are processable items, NOT tools
            }

            // CRITICAL FIX: Exclude monster parts from tool detection
            // Monster parts contain patterns that might match tool detection but are processable items
            if (itemNameLower.Contains("monster parts") ||
                itemNameLower.Contains("pelted monster parts"))
            {
                LogDebug($"[TOOL PROTECTION] PROCESSABLE ITEM: {GetItemDisplayName(item)} is monster parts for plasma recipe - NOT PROTECTED");
                return false; // Monster parts are processable items, NOT tools
            }

            // CRITICAL FIX: Exclude robot brain recipe results from tool detection
            // Robot brain results contain "robot" pattern but are recipe results that must be returned to players
            if (itemNameLower.Contains("robot brain") ||
                itemNameLower.Contains("basic robot brain") ||
                itemNameLower.Contains("personalized basic robot brain"))
            {
                LogDebug($"[TOOL PROTECTION] RECIPE RESULT: {GetItemDisplayName(item)} is robot brain recipe result - NOT PROTECTED");
                return false; // Robot brain results are recipe outputs, NOT tools
            }

            // CHECK EXACT TOOL NAMES FIRST
            foreach (var toolName in exactToolNames)
            {
                if (item.Name.Contains(toolName))
                {
                    return true;
                }
            }

            // CHECK TOOL PATTERNS (catch-all for any missed tools)
            foreach (var pattern in toolPatterns)
            {
                if (itemNameLower.Contains(pattern))
                {
                    return true;
                }
            }

            // ADDITIONAL HARDCODED PROTECTIONS
            // Any item with "Mass Relocating Robot" in name
            if (item.Name.Contains("Mass Relocating Robot"))
            {
                return true;
            }

            // Any HSR sketching tool (absolute protection)
            if (item.Name.Contains("HSR") && item.Name.Contains("Sketch"))
            {
                return true;
            }

            // CRITICAL FIX: Check if this specific item instance belongs to the bot
            // This uses proper instance-based tracking instead of just item IDs
            if (Core.ItemTracker.IsBotTool(item) || Core.ItemTracker.IsBotPersonalItem(item) || Core.ItemTracker.IsBotToolBag(item))
            {
                LogDebug($"[TOOL PROTECTION] CRITICAL: {GetItemDisplayName(item)} is bot's tracked item (ID:{item.Id}, Instance:{item.UniqueIdentity.Instance}) - PROTECTED");
                return true;
            }

            // FALLBACK: Check known tool IDs only if instance tracking fails
            if (IsToolByItemId(item.Id))
            {
                LogDebug($"[TOOL PROTECTION] FALLBACK: {GetItemDisplayName(item)} protected by known tool ID {item.Id} (instance tracking may have failed)");
                return true;
            }

            return false;
        }

        /// <summary>
        /// EMERGENCY TOOL PROTECTION: Check if item ID matches known tool IDs
        /// This is a fallback when item names are empty in clientless API
        /// </summary>
        private static bool IsToolByItemId(int itemId)
        {
            // CRITICAL PROTECTION 1: Known tool item IDs that must NEVER be given to players
            // These IDs are discovered through debugging and must be protected
            HashSet<int> knownToolIds = new HashSet<int>
            {
                154332, // Advanced Bio-Comminutor - CRITICAL: Bot's personal tool that was given away
                151366, // Jensen Gem Cutter - Bot's personal tool
                229870, // Ancient Novictum Refiner - Bot's personal tool
                87814,  // Advanced Hacker Tool - Bot's personal tool (from admin inventory)
                268509, // Alien Material Conversion kit - Bot's personal tool (from admin inventory)
                267751, // Ancient Engineering Device - Bot's personal tool (from admin inventory)
                95577,  // Lock Pick - CRITICAL: Bot's personal tool that was being given away

                // Add more tool IDs as they are identified through careful observation
                // TODO: Build comprehensive tool ID database through gameplay observation
            };

            // MODIFIED PROTECTION: Only check known tool IDs, not pre-trade items
            // The comprehensive inventory tracking system handles pre-trade item protection
            bool isKnownTool = knownToolIds.Contains(itemId);

            if (isKnownTool)
            {
                LogDebug($"[TOOL PROTECTION] CRITICAL: Item ID {itemId} is a known protected tool - preventing return to player");
                return true;
            }

            // REMOVED: Pre-trade item checking as it was too aggressive and blocked player items
            // The comprehensive inventory tracking system now handles this properly
            LogDebug($"[TOOL PROTECTION] Item ID {itemId} not in known tool list - allowing return");
            return false;
        }

        private static void DeleteTemporaryDataReceptacle()
        {
            try
            {
                var tempDataReceptacle = Inventory.Items.FirstOrDefault(item =>
                    item.Name.Contains("Temporary: Data Receptacle"));

                if (tempDataReceptacle != null)
                {
                    LogDebug($"[CLEANUP] Found {tempDataReceptacle.Name}, deleting it");
                    tempDataReceptacle.Delete();
                    LogDebug($"[CLEANUP] Deleted {tempDataReceptacle.Name}");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[CLEANUP] Error deleting Temporary Data Receptacle: {ex.Message}");
            }
        }



        // OLD MoveProcessedItemsBackToBackpack method REMOVED - now handled by BaseRecipeProcessor workflows

        private static void OnContainerOpened(Container container)
        {
            try
            {
                LogDebug($"[CONTAINER] Container opened: {container.Identity}");
                // This event fires when containers are opened - useful for tracking bag opening
            }
            catch (Exception ex)
            {
                LogDebug($"[CONTAINER] Error in OnContainerOpened: {ex.Message}");
            }
        }



        private static async Task ProcessTradeItems(int playerId)
        {
            try
            {
                // CRITICAL FIX: Clear alien armor consumed items tracking for new trade
                Recipes.AlienArmorRecipe.ClearConsumedItemsTracking();

                // Check if this is a special implant trade or treatment library trade
                string playerName = GetPlayerNameFromId(playerId);
                bool isImplantTrade = !string.IsNullOrEmpty(playerName) && Core.ImplantTradeManager.HasPendingImplantTrade(playerName);
                bool isTreatmentLibraryTrade = !string.IsNullOrEmpty(playerName) && Core.TreatmentLibraryTradeManager.HasPendingTreatmentLibraryTrade(playerName);

                if (isImplantTrade)
                {
                    LogDebug($"[TRADE PROCESSING] Detected pending implant trade for {playerName} - will use custom implant processing with unified return logic");
                }
                else if (isTreatmentLibraryTrade)
                {
                    LogDebug($"[TRADE PROCESSING] Detected pending treatment library trade for {playerName} - will use custom treatment library processing with unified return logic");
                }

                LogDebug($"[TRADE PROCESSING] Using event-driven item detection for player {playerId}");

                // CRITICAL FIX: Add delay to allow items to fully load after trade
                LogDebug($"[TRADE PROCESSING] Waiting for items to fully load...");
                await Task.Delay(500); // Give items time to load properly

                // Stop tracking new items
                _isTrackingItems = false;

                // EVENT-DRIVEN APPROACH: Check if we detected any items via events
                var eventDetectedItems = new List<Item>();

                if (_tradedItemsByPlayer.ContainsKey(playerId))
                {
                    eventDetectedItems.AddRange(_tradedItemsByPlayer[playerId]);
                    LogDebug($"[EVENT DETECTION] Found {_tradedItemsByPlayer[playerId].Count} items via events for player {playerId}");
                }

                // Also check fallback player ID 0 (in case events fired before player was set)
                if (_tradedItemsByPlayer.ContainsKey(0))
                {
                    eventDetectedItems.AddRange(_tradedItemsByPlayer[0]);
                    LogDebug($"[EVENT DETECTION] Found {_tradedItemsByPlayer[0].Count} items via events for fallback player 0");
                }

                if (eventDetectedItems.Any())
                {
                    LogDebug($"[EVENT DETECTION] Total event-detected items: {eventDetectedItems.Count}");
                    foreach (var item in eventDetectedItems)
                    {
                        LogDebug($"[EVENT DETECTION] Event-detected item: {GetItemDisplayName(item)} (ID: {item.Id}, Slot: {item.Slot.Instance})");
                    }
                }
                else
                {
                    LogDebug($"[EVENT DETECTION] No items detected via events for player {playerId}");
                }

                // Debug: Log pre-trade inventory details
                foreach (var preItem in _preTradeInventory.Take(5)) // Log first 5 items
                {
                    LogDebug($"[TRADE PROCESSING] Pre-trade item: Type={preItem.Type}, Instance={preItem.Instance}");
                }

                // CRITICAL FAILSAFE: Check for any leftover items from previous trades
                // ENABLED: Re-enabled with improved logic to fix item persistence issues
                CheckForLeftoverItemsFromPreviousTrades(playerId);
                LogDebug($"[LEFTOVER CHECK] Leftover item detection ENABLED to fix item persistence issues");



                // Check for new bags
                var currentContainers = Inventory.Containers.ToList();
                var newBags = currentContainers.Where(container =>
                    !_preTradeInventory.Any(preItem =>
                        preItem.Type == IdentityType.Container &&
                        preItem.Instance == container.Identity.Instance)).ToList();

                LogDebug($"[TRADE PROCESSING] Current containers: {currentContainers.Count}, New bags: {newBags.Count}");

                // Debug: Log current containers
                foreach (var container in currentContainers.Take(3)) // Log first 3 containers
                {
                    LogDebug($"[TRADE PROCESSING] Current container: Type={container.Identity.Type}, Instance={container.Identity.Instance}");
                }

                // Check for new loose items using proper clientless API
                var currentInventoryItems = Inventory.Items.Where(item =>
                    item.Slot.Type == IdentityType.Inventory &&
                    item.UniqueIdentity.Type != IdentityType.Container).ToList();

                LogDebug($"[TRADE PROCESSING] DEBUG: Total inventory items: {Inventory.Items.Count()}");
                LogDebug($"[TRADE PROCESSING] DEBUG: Filtered inventory items: {currentInventoryItems.Count}");
                LogDebug($"[TRADE PROCESSING] DEBUG: Pre-trade inventory count: {_preTradeInventory.Count}");

                // TRADE DETECTION: Use pre-trade inventory comparison (primary method)
                var newLooseItems = new List<Item>();

                if (_preTradeInventory.Count > 0)
                {
                    // UNIFIED METHOD: Compare against pre-trade inventory using UniqueIdentity.Instance (same as bags)
                    newLooseItems = currentInventoryItems.Where(item =>
                        // EXCLUDE equipment pages - they should NEVER be considered for trading
                        item.Slot.Type != IdentityType.ArmorPage &&
                        item.Slot.Type != IdentityType.WeaponPage &&
                        item.Slot.Type != IdentityType.ImplantPage &&
                        item.Slot.Type != IdentityType.SocialPage &&
                        !_preTradeInventory.Any(preItem =>
                            preItem.Type == IdentityType.None &&
                            preItem.Instance == item.Slot.Instance)).ToList();

                    LogDebug($"[TRADE DETECTION] Using unified pre-trade comparison: Found {newLooseItems.Count} new items");
                }
                else
                {
                    // Fallback method: Use bot original inventory check (EXCLUDE equipment pages)
                    foreach (var item in currentInventoryItems)
                    {
                        // EXCLUDE equipment pages - they should NEVER be considered for trading
                        if (item.Slot.Type == IdentityType.ArmorPage ||
                            item.Slot.Type == IdentityType.WeaponPage ||
                            item.Slot.Type == IdentityType.ImplantPage ||
                            item.Slot.Type == IdentityType.SocialPage)
                        {
                            continue; // Silently exclude equipment items
                        }

                        bool isBotOriginalItem = IsBotOriginalItem(item.Id);
                        if (!isBotOriginalItem)
                        {
                            newLooseItems.Add(item);
                        }
                        LogDebug($"[TRADE DETECTION] Fallback check: Id={item.Id}, UniqueInstance={item.UniqueIdentity.Instance}, isBotOriginalItem={isBotOriginalItem}");
                    }
                    LogDebug($"[TRADE DETECTION] Using fallback method (legacy): Found {newLooseItems.Count} new items");
                }

                LogDebug($"[TRADE PROCESSING] DEBUG: New loose items found: {newLooseItems.Count}");

                LogDebug($"[TRADE PROCESSING] Current inventory items: {currentInventoryItems.Count}, New loose items: {newLooseItems.Count}");

                // Debug: Log current inventory items with more details
                foreach (var item in currentInventoryItems.Take(5)) // Log first 5 items
                {
                    LogDebug($"[TRADE PROCESSING] Current item: Name='{item.Name}', Id={item.Id}, SlotType={item.Slot.Type}, SlotInstance={item.Slot.Instance}");
                }

                // Debug: Log new loose items specifically
                foreach (var item in newLooseItems.Take(5)) // Log first 5 new items
                {
                    LogDebug($"[TRADE PROCESSING] NEW LOOSE ITEM: Name='{item.Name}', Id={item.Id}, SlotType={item.Slot.Type}, SlotInstance={item.Slot.Instance}");
                }

                // CRITICAL FIX: If any new items have empty names, try refreshing inventory
                if (newLooseItems.Any(item => string.IsNullOrEmpty(item.Name)))
                {
                    LogDebug($"[TRADE PROCESSING] Items have empty names - waiting longer for full load...");
                    await Task.Delay(1000); // Wait longer

                    // Re-check inventory using proper clientless API
                    currentInventoryItems = Inventory.Items.Where(item =>
                        item.Slot.Type == IdentityType.Inventory &&
                        item.Slot.Instance >= Inventory.INVENTORY_START &&
                        item.Slot.Instance < Inventory.INVENTORY_END &&
                        item.UniqueIdentity.Type != IdentityType.Container).ToList();

                    newLooseItems = currentInventoryItems.Where(item =>
                        !_preTradeInventory.Any(preItem =>
                            preItem.Type == IdentityType.None &&
                            preItem.Instance == item.UniqueIdentity.Instance)).ToList();

                    LogDebug($"[TRADE PROCESSING] After refresh: Current inventory items: {currentInventoryItems.Count}, New loose items: {newLooseItems.Count}");

                    // Debug: Log refreshed items
                    foreach (var item in newLooseItems.Take(5))
                    {
                        LogDebug($"[TRADE PROCESSING] REFRESHED NEW ITEM: Name='{item.Name}', Id={item.Id}, SlotType={item.Slot.Type}");
                    }

                    // If items still have empty names after refresh, try one more time with longer delay
                    if (newLooseItems.Any(item => string.IsNullOrEmpty(item.Name)))
                    {
                        LogDebug($"[TRADE PROCESSING] Items still have empty names - trying one more refresh...");
                        await Task.Delay(2000); // Wait even longer

                        // Final re-check using proper clientless API
                        currentInventoryItems = Inventory.Items.Where(item =>
                            item.Slot.Type == IdentityType.Inventory &&
                            item.Slot.Instance >= Inventory.INVENTORY_START &&
                            item.Slot.Instance < Inventory.INVENTORY_END &&
                            item.UniqueIdentity.Type != IdentityType.Container).ToList();

                        newLooseItems = currentInventoryItems.Where(item =>
                            !_preTradeInventory.Any(preItem =>
                                preItem.Type == IdentityType.None &&
                                preItem.Instance == item.UniqueIdentity.Instance)).ToList();

                        LogDebug($"[TRADE PROCESSING] Final refresh: Current inventory items: {currentInventoryItems.Count}, New loose items: {newLooseItems.Count}");

                        foreach (var item in newLooseItems.Take(5))
                        {
                            LogDebug($"[TRADE PROCESSING] FINAL ITEM: Name='{item.Name}', Id={item.Id}, SlotType={item.Slot.Type}");
                        }
                    }
                }

                // Log received loose items for detailed trade log (with bot item filtering)
                foreach (var looseItem in newLooseItems)
                {
                    // Use helper method for consistent item name handling
                    string itemDisplayName = GetItemDisplayName(looseItem);

                    // TRADE LOGGING FILTER: Only log items that are NOT bot's original items
                    bool isBotOriginalItem = IsBotOriginalItem(looseItem.Id);
                    bool isReturnTrade = _pendingReturns.ContainsKey(playerId);
                    LogDebug($"[TRADE LOG] Processing loose item: {itemDisplayName}, isBotOriginal: {isBotOriginalItem}, isReturnTrade: {isReturnTrade}");

                    if (!isBotOriginalItem)
                    {
                        LogReceivedLooseItem(playerId, itemDisplayName);
                        LogDebug($"[TRADE LOG] ✅ LOGGED received player item: {itemDisplayName}");
                    }
                    else
                    {
                        LogDebug($"[TRADE LOG] 🛡️ BLOCKED bot original item from trade log: {itemDisplayName}");
                    }

                    // Track item ownership for leftover recovery (always track for processing)
                    var currentPlayerName = GetPlayerName(playerId);
                    if (!string.IsNullOrEmpty(currentPlayerName))
                    {
                        string itemKey = $"{itemDisplayName}_{looseItem.Id}";
                        _itemToPlayerMapping[itemKey] = currentPlayerName;
                        LogDebug($"[ITEM TRACKING] Mapped item {itemKey} to player {currentPlayerName}");

                        // COMPREHENSIVE INVENTORY TRACKING: Track as received item (always track for processing)
                        Core.ItemTracker.ProcessReceivedItems(new List<Item> { looseItem }, currentPlayerName);
                        LogDebug($"[COMPREHENSIVE TRACKING] Tracked received item: {itemDisplayName} from {currentPlayerName}");
                    }
                }

                // SINGLE UNIFIED WORKFLOW
                if (newBags.Any() || newLooseItems.Any())
                {
                    LogDebug($"[TRADE PROCESSING] Found new items - starting unified processing");

                    // Send combined trade completed and processing message with detailed counts
                    string processingMessage = $"Trade completed! I'm now processing your {newBags.Count} bag(s) and {newLooseItems.Count} loose item(s). Please wait while I work on them...";
                    SendPrivateMessage(playerId.ToString(), processingMessage);



                    // STATE TRANSITION: Ready → Processing
                    _currentBotState = BotState.Processing;
                    LogDebug($"[BOT STATE] Changed to Processing for player {playerId}");

                    // Store what we need to track for return
                    var bagItems = new List<Item>();

                    // Process bags if any
                    if (newBags.Any())
                    {
                        LogDebug($"[TRADE PROCESSING] Processing {newBags.Count} bags");
                        LogDebug($"[TRADE PROCESSING] isImplantTrade: {isImplantTrade}, isTreatmentLibraryTrade: {isTreatmentLibraryTrade}");

                        // Convert bags to items and store for return
                        foreach (var bag in newBags)
                        {
                            var bagItem = Inventory.Items.FirstOrDefault(item =>
                                item.UniqueIdentity.Instance == bag.Identity.Instance &&
                                item.UniqueIdentity.Type == IdentityType.Container);
                            if (bagItem != null)
                            {
                                bagItems.Add(bagItem);
                                LogDebug($"[TRADE PROCESSING] Found bag item: {bagItem.Name}");
                            }
                        }

                        // Store bags for this player
                        _playerBags[playerId] = bagItems.ToList();

                        // Open and process bags
                        foreach (var bagItem in bagItems)
                        {
                            LogDebug($"[TRADE PROCESSING] Opening bag: {bagItem.Name}");
                            bagItem.Use();
                            await Task.Delay(500);
                        }

                        await Task.Delay(1000); // Wait for bags to open

                        // Process each bag's contents
                        foreach (var bagItem in bagItems)
                        {
                            var container = Inventory.Containers.FirstOrDefault(c =>
                                c.Identity.Instance == bagItem.UniqueIdentity.Instance);

                            if (container != null)
                            {
                                LogDebug($"[TRADE PROCESSING] Processing contents of {bagItem.Name}");

                                // CUSTOM TRADE INTEGRATION: Use unified infrastructure with custom processing
                                if (isImplantTrade)
                                {
                                    LogDebug($"[TRADE PROCESSING] Using unified infrastructure with custom implant processing for bag contents");
                                    await ScanAndProcessContainerContentsWithCustomImplantProcessing(container, bagItem, playerId, playerName);
                                }
                                else if (isTreatmentLibraryTrade)
                                {
                                    LogDebug($"[TRADE PROCESSING] Using unified infrastructure with custom treatment library processing for bag contents");
                                    await ScanAndProcessContainerContentsWithCustomTreatmentLibraryProcessing(container, bagItem, playerId, playerName);
                                }
                                else
                                {
                                    // NORMAL TRADE: Use standard bag processing
                                    LogDebug($"[TRADE PROCESSING] Calling ScanAndProcessContainerContents for normal trade");
                                    await ScanAndProcessContainerContents(container, bagItem, playerId);
                                }
                            }
                        }

                        // Set up pending returns for bags so AttemptReturnToPlayer works
                        LogDebug($"[TRADE PROCESSING] Setting up pending returns for {bagItems.Count} bags");
                        _pendingReturns[playerId] = bagItems.ToList();
                    }

                    // Process loose items if any
                    if (newLooseItems.Any())
                    {
                        LogDebug($"[TRADE PROCESSING] Processing {newLooseItems.Count} loose items");

                        // CUSTOM TRADE INTEGRATION: Use unified infrastructure with custom processing
                        if (isImplantTrade)
                        {
                            LogDebug($"[TRADE PROCESSING] Using unified infrastructure with custom implant processing for {newLooseItems.Count} loose items");

                            // UNIFIED INFRASTRUCTURE: Track item ownership for leftover recovery
                            foreach (var item in newLooseItems)
                            {
                                string itemKey = $"{item.Name}_{item.Id}";
                                _itemToPlayerMapping[itemKey] = playerName;
                                LogDebug($"[ITEM TRACKING] Mapped loose item {itemKey} to player {playerName}");

                                // COMPREHENSIVE INVENTORY TRACKING: Track as received item
                                Core.ItemTracker.ProcessReceivedItems(new List<Item> { item }, playerName);
                                LogDebug($"[COMPREHENSIVE TRACKING] Tracked received loose item: {item.Name} from {playerName}");
                            }

                            // CUSTOM PROCESSING: Use custom implant processing instead of recipe system
                            await Core.ImplantTradeManager.ProcessImplantTrade(playerName, newLooseItems);

                            LogDebug($"[TRADE PROCESSING] Custom implant processing completed - continuing with unified return logic");
                        }
                        else if (isTreatmentLibraryTrade)
                        {
                            LogDebug($"[TRADE PROCESSING] Using unified infrastructure with custom treatment library processing for {newLooseItems.Count} loose items");

                            // UNIFIED INFRASTRUCTURE: Track item ownership for leftover recovery
                            foreach (var item in newLooseItems)
                            {
                                string itemKey = $"{item.Name}_{item.Id}";
                                _itemToPlayerMapping[itemKey] = playerName;
                                LogDebug($"[ITEM TRACKING] Mapped loose item {itemKey} to player {playerName}");

                                // COMPREHENSIVE INVENTORY TRACKING: Track as received item
                                Core.ItemTracker.ProcessReceivedItems(new List<Item> { item }, playerName);
                                LogDebug($"[COMPREHENSIVE TRACKING] Tracked received loose item: {item.Name} from {playerName}");
                            }

                            // CUSTOM PROCESSING: Use custom treatment library processing instead of recipe system
                            await Core.TreatmentLibraryTradeManager.ProcessTreatmentLibraryTrade(playerName, newLooseItems);

                            LogDebug($"[TRADE PROCESSING] Custom treatment library processing completed - continuing with unified return logic");
                        }
                        else
                        {
                            // NORMAL TRADE: Use standard recipe processing
                            foreach (var item in newLooseItems)
                            {
                                // Use helper method for consistent item name handling
                                string itemDisplayName = GetItemDisplayName(item);
                                LogDebug($"[TRADE PROCESSING] Processing loose item: {itemDisplayName}");

                                // Process loose items through the RecipeManager system (same as bag items)
                                bool processed = await Recipes.RecipeManager.ProcessItem(item, null); // null = loose item
                                if (processed)
                                {
                                    LogDebug($"[TRADE PROCESSING] Successfully processed loose item: {itemDisplayName}");
                                }
                                else
                                {
                                    LogInfo($"[TRADE PROCESSING] No processor found for loose item: {itemDisplayName} - will be returned to player");
                                }

                                await Task.Delay(100);
                            }
                        }

                        // CRITICAL FIX: ALWAYS check for loose items to return (including processed results)
                        // This must run regardless of whether there were loose items in the original trade
                        LogDebug($"[TRADE PROCESSING] Checking for loose items to return (including processed results)");

                        // CRITICAL FIX: Only return the processed items, NOT all inventory items including tools
                        var looseItemsToReturn = new List<Item>();

                        var currentItems = Inventory.Items.Where(item =>
                            item.Slot.Type == IdentityType.Inventory &&
                            item.Slot.Instance >= Inventory.INVENTORY_START &&
                            item.Slot.Instance < Inventory.INVENTORY_END &&
                            item.UniqueIdentity.Type != IdentityType.Container).ToList();

                        foreach (var item in currentItems)
                        {
                            // CRITICAL: Check if this item was received from the player in THIS trade
                            bool wasReceivedFromPlayer = Core.ItemTracker.WasReceivedFromPlayer(item);
                            bool isBotPersonalItem = Core.ItemTracker.IsBotPersonalItem(item);
                            bool isBotTool = Core.ItemTracker.IsBotTool(item);

                            // COMPREHENSIVE RULE: Return item if it was received from player OR if it's NOT a bot item
                            // This automatically includes ALL processed results without needing to manually track them
                            bool shouldReturn = wasReceivedFromPlayer || (!isBotPersonalItem && !isBotTool);

                            if (shouldReturn)
                            {
                                looseItemsToReturn.Add(item);
                                if (wasReceivedFromPlayer)
                                {
                                    LogDebug($"[TRADE PROCESSING] ✅ Adding player's item to return: {item.Name} (received from player)");
                                }
                                else
                                {
                                    LogDebug($"[TRADE PROCESSING] ✅ Adding processed result to return: {item.Name} (not a bot item)");
                                }
                            }
                            else
                            {
                                // RULE 2: Item is a bot's personal item or tool - DO NOT RETURN
                                LogDebug($"[TRADE PROCESSING] 🛡️ BLOCKING: {item.Name} is bot's item - keeping it");
                            }
                        }

                        // ADD loose items to existing pending returns (don't replace)
                        if (looseItemsToReturn.Any())
                        {
                            if (_pendingReturns.ContainsKey(playerId))
                            {
                                _pendingReturns[playerId].AddRange(looseItemsToReturn);
                                LogDebug($"[TRADE PROCESSING] Added {looseItemsToReturn.Count} loose items to existing {_pendingReturns[playerId].Count - looseItemsToReturn.Count} bag items for return");
                            }
                            else
                            {
                                _pendingReturns[playerId] = looseItemsToReturn;
                                LogDebug($"[TRADE PROCESSING] Set up {looseItemsToReturn.Count} loose items for return (no bags)");
                            }
                        }
                        else
                        {
                            LogDebug($"[TRADE PROCESSING] No loose items found to return");
                        }
                    }

                    // Clear implant trade flag if this was an implant trade
                    if (isImplantTrade)
                    {
                        Core.ImplantTradeManager.ClearPendingImplantTrade(playerName);
                        LogDebug($"[TRADE PROCESSING] Cleared pending implant trade for {playerName}");
                    }

                    // STATE TRANSITION: Processing → Returning
                    _currentBotState = BotState.Returning;
                    LogDebug($"[BOT STATE] Changed to Returning for player {playerId}");

                    // UNIFIED RETURN - Return everything back to player
                    LogDebug($"[TRADE PROCESSING] Processing complete - returning items to player {playerId}");
                    await Task.Delay(2000); // Wait for processing to complete
                    await AttemptReturnToPlayer(playerId);

                    // DON'T clear tracking data here - let the return trade completion handle state reset
                    // The processing state will be reset when the return trade completes successfully
                    LogDebug($"[TRADE PROCESSING] Return trade initiated - bot state will be reset when return trade completes");
                }
                else
                {
                    LogDebug($"[TRADE PROCESSING] NO NEW ITEMS DETECTED - this should not happen if items were traded!");

                    // Clear implant trade flag if this was an implant trade
                    if (isImplantTrade)
                    {
                        Core.ImplantTradeManager.ClearPendingImplantTrade(playerName);
                        LogDebug($"[TRADE PROCESSING] Cleared pending implant trade for {playerName} (no items detected)");
                    }

                    // Send immediate feedback to user
                    SendPrivateMessage(playerId.ToString(), "⚠️ I didn't detect any new items from the trade. Please try trading again.");

                    // No new items found, handle gracefully without restart
                    LogDebug($"[TRADE PROCESSING] No new items found, handling gracefully");

                    CleanupTradeTracking(playerId);

                    // Reset processing state
                    if (_currentProcessingPlayer == playerId)
                    {
                        _currentBotState = BotState.Ready;
                        _currentProcessingPlayer = null;
                        LogDebug($"[QUEUE] Processing complete - bot is now ready for new trades");
                        ProcessNextInQueue();
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"[TRADE PROCESSING] Error: {ex.Message}");
                LogError($"[TRADE PROCESSING] Stack trace: {ex.StackTrace}");
                CleanupTradeTracking(playerId);

                // CRITICAL: Reset processing state on error to prevent bot from getting stuck
                if (_currentProcessingPlayer == playerId)
                {
                    _currentBotState = BotState.Ready;
                    _currentProcessingPlayer = null;
                    LogInfo($"[QUEUE] Error occurred - bot state reset to Ready");
                    ProcessNextInQueue();
                }

                // Notify player of the error
                SendPrivateMessage(playerId.ToString(), "❌ An error occurred while processing your items. Please try again or contact an admin.");
            }
        }

        /// <summary>
        /// Get player name from player ID
        /// </summary>
        private static string GetPlayerNameFromId(int playerId)
        {
            try
            {
                var player = DynelManager.Players.FirstOrDefault(p => p.Identity.Instance == playerId);
                return player?.Name ?? "";
            }
            catch
            {
                return "";
            }
        }

        private static void CleanupTradeTracking(int playerId)
        {
            // CRITICAL FIX: Complete trade log if it exists during cleanup
            // This ensures all trades are logged even if processing fails
            if (_currentTradeLogs.ContainsKey(playerId))
            {
                LogDebug($"[TRADE LOG] Completing trade log during cleanup for player {playerId}");
                var bagsReturned = _pendingReturns.ContainsKey(playerId) ? _pendingReturns[playerId].Count : 0;
                CompleteTradeLog(playerId, bagsReturned);
            }

            // Clean up event-driven tracking
            _isTrackingItems = false;
            _tradedItemsByPlayer.Remove(playerId);
            _botItemIds.Clear(); // Clear bot item protection

            _preTradeInventory.Clear();
            _tradedItems.Clear();
        }



        private static async Task<bool> TryToOpenBags(int playerId)
        {
            try
            {
                LogDebug($"[BAG DETECTION] Checking for bags from player {playerId}");

                var currentContainers = Inventory.Containers.ToList();

                // Look for any new bags that appeared after trade
                // FIXED: Compare container.Identity directly since _preTradeInventory stores container.Identity for containers
                var newBags = currentContainers.Where(container =>
                    !_preTradeInventory.Any(preItem =>
                        preItem.Type == IdentityType.Container &&
                        preItem.Instance == container.Identity.Instance &&
                        preItem.Type == container.Identity.Type)).ToList();

                LogDebug($"[BAG DETECTION] Current containers: {currentContainers.Count}, Pre-trade containers: {_preTradeInventory.Count(p => p.Type == IdentityType.Container)}");
                LogDebug($"[BAG DETECTION] New bags found: {newBags.Count}");

                if (newBags.Any())
                {
                    LogDebug($"[BAG DETECTION] Found {newBags.Count} new bag(s), starting processing");

                    // Process bags immediately without delay
                    await ProcessFoundBags(playerId, newBags);
                    return true; // Bags found and processing started
                }
                else
                {
                    LogDebug($"[BAG DETECTION] No new bags detected");
                    return false; // No bags found
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[BAG DETECTION] Error: {ex.Message}");
                return false;
            }
        }




















        private static async Task WaitForArrival(Vector3 destination, float maxDistance)
        {
            // Clientless bots cannot move to destinations
            // This method is kept for compatibility but does nothing
            LogDebug($"[CLIENTLESS NAV] Clientless mode - cannot move to destinations");
            await Task.Delay(100); // Minimal delay for compatibility
        }



        private static void OnGameUpdate(object sender, double deltaTime)
        {
            try
            {
                // Check for death every 5 seconds to avoid spam
                if (DateTime.Now - _lastDeathCheck > TimeSpan.FromSeconds(5))
                {
                    _lastDeathCheck = DateTime.Now;
                    CheckForDeathAndRecover();

                    // Also check for pending bag returns every 5 seconds
                    CheckPendingBagReturns();
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[DEATH RECOVERY] Error in game update: {ex.Message}");
            }
        }

        private static void CheckPendingBagReturns()
        {
            try
            {
                // Check if we have any pending returns
                if (!_playersWithPendingReturn.Any())
                    return;

                // Check each player with pending returns
                var playersToCheck = _playersWithPendingReturn.ToList(); // Copy to avoid modification during iteration

                foreach (var playerId in playersToCheck)
                {
                    // Find the player
                    var player = DynelManager.Players.FirstOrDefault(p => p.Identity.Instance == playerId);
                    if (player == null)
                    {
                        // Player not found, continue checking (they might come back)
                        continue;
                    }

                    // Check if player is now within range
                    if (player.DistanceFrom(DynelManager.LocalPlayer) <= 2f)
                    {
                        LogDebug($"[BAG RETURN] Player {player.Name} is now within range, attempting return");

                        // Remove from pending check list temporarily to avoid spam
                        _playersWithPendingReturn.Remove(playerId);

                        // Attempt return
                        _ = Task.Run(async () =>
                        {
                            await AttemptReturnToPlayer(playerId);

                            // If return failed, add back to pending list
                            if (_pendingReturns.ContainsKey(playerId))
                            {
                                _playersWithPendingReturn.Add(playerId);
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[BAG RETURN] Error checking pending returns: {ex.Message}");
            }
        }

        private static void CheckForDeathAndRecover()
        {
            try
            {
                // Check if player is dead
                if (DynelManager.LocalPlayer.GetStat(Stat.Health) <= 0 && !_isRecoveringFromDeath)
                {
                    LogDebug($"[DEATH RECOVERY] Player death detected, starting recovery process");
                    _isRecoveringFromDeath = true;
                    _ = Task.Run(async () => await HandleDeathRecovery());
                }
                // DISABLED: Position checking is pointless in clientless mode and causes spam
                // Check if player is alive and far from standing position
                // else if (DynelManager.LocalPlayer.GetStat(Stat.Health) > 0 && !_isRecoveringFromDeath)
                // {
                //     float distanceFromStanding = Vector3.Distance(DynelManager.LocalPlayer.MovementComponent.Position, _standingPosition);
                //
                //     // If player is more than 1 meter from standing position and in correct playfield (very short leash)
                //     if (distanceFromStanding > 1.0f && (int)Playfield.ModelId == _standingPlayfield)
                //     {
                //         // DISABLED: This debug spam is pointless in clientless mode since bot can't navigate anyway
                //         // LogDebug($"[DEATH RECOVERY] Player is {distanceFromStanding:F1}m from standing position - clientless mode, cannot navigate");
                //         _isRecoveringFromDeath = false; // Reset since we can't navigate
                //     }
                // }
            }
            catch (Exception ex)
            {
                LogDebug($"[DEATH RECOVERY] Error checking for death: {ex.Message}");
            }
        }

        private static async Task HandleDeathRecovery()
        {
            try
            {
                LogDebug($"[DEATH RECOVERY] Waiting for resurrection...");

                // Wait for player to be resurrected (health > 0)
                int timeout = 0;
                while (DynelManager.LocalPlayer.GetStat(Stat.Health) <= 0 && timeout < 600) // 60 second timeout
                {
                    await Task.Delay(100);
                    timeout++;
                }

                if (DynelManager.LocalPlayer.GetStat(Stat.Health) > 0)
                {
                    LogDebug($"[DEATH RECOVERY] Player resurrected - clientless mode, cannot navigate back to standing position");
                    await Task.Delay(2000); // Wait a bit for resurrection to complete
                }
                else
                {
                    LogDebug($"[DEATH RECOVERY] Timeout waiting for resurrection");
                    _isRecoveringFromDeath = false;
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[DEATH RECOVERY] Error handling death recovery: {ex.Message}");
                _isRecoveringFromDeath = false;
            }
        }



        /// <summary>
        /// EXPERIMENTAL: Try various approaches to directly set bot position coordinates
        /// </summary>
        private static async Task TryDirectPositionSetting(Vector3 targetPosition)
        {
            try
            {
                LogDebug($"[TELEPORT] EXPERIMENTAL: Attempting direct position setting to {targetPosition}");

                // Log all failed movement methods for reference
                LogDebug($"[TELEPORT] Previously failed methods:");
                LogDebug($"[TELEPORT] - MovementComponent reflection (only found ChangeMovement)");
                LogDebug($"[TELEPORT] - CharacterActionMessage movement approach");
                LogDebug($"[TELEPORT] - Direct Client.Send movement commands");
                LogDebug($"[TELEPORT] - Exit.Use() attempts");

                Vector3 currentPos = DynelManager.LocalPlayer.MovementComponent.Position;
                LogDebug($"[TELEPORT] Current position: {currentPos}");
                LogDebug($"[TELEPORT] Target position: {targetPosition}");

                // Approach 1: Try reflection to find position setters
                LogDebug($"[TELEPORT] Approach 1: Exploring MovementComponent for position setters");
                var movementComponent = DynelManager.LocalPlayer.MovementComponent;
                var movementType = movementComponent.GetType();

                // Look for Position property setter
                var positionProperty = movementType.GetProperty("Position");
                if (positionProperty != null && positionProperty.CanWrite)
                {
                    LogDebug($"[TELEPORT] Found writable Position property, attempting to set");
                    positionProperty.SetValue(movementComponent, targetPosition);
                    await Task.Delay(500);

                    Vector3 newPos = DynelManager.LocalPlayer.MovementComponent.Position;
                    LogDebug($"[TELEPORT] After Position property set - new position: {newPos}");
                }
                else
                {
                    LogDebug($"[TELEPORT] Position property not writable or not found");
                }

                // Approach 2: Try Transform.Position if available
                LogDebug($"[TELEPORT] Approach 2: Exploring Transform for position setters");
                if (DynelManager.LocalPlayer.Transform != null)
                {
                    var transform = DynelManager.LocalPlayer.Transform;
                    var transformType = transform.GetType();
                    var transformPositionProperty = transformType.GetProperty("Position");

                    if (transformPositionProperty != null && transformPositionProperty.CanWrite)
                    {
                        LogDebug($"[TELEPORT] Found writable Transform.Position property, attempting to set");
                        transformPositionProperty.SetValue(transform, targetPosition);
                        await Task.Delay(500);

                        Vector3 newPos = DynelManager.LocalPlayer.MovementComponent.Position;
                        LogDebug($"[TELEPORT] After Transform.Position set - new position: {newPos}");
                    }
                    else
                    {
                        LogDebug($"[TELEPORT] Transform.Position property not writable or not found");
                    }
                }
                else
                {
                    LogDebug($"[TELEPORT] Transform not available");
                }

                // Approach 3: Try sending position update messages
                LogDebug($"[TELEPORT] Approach 3: Attempting position update messages");

                // Try different message types that might update position
                try
                {
                    // Experimental: Try using CharacterActionMessage with different action types
                    LogDebug($"[TELEPORT] Trying CharacterActionMessage with position parameters");

                    Client.Send(new CharacterActionMessage
                    {
                        Action = (CharacterActionType)999, // Experimental action type
                        Target = Identity.None,
                        Parameter1 = (int)(targetPosition.X * 1000), // Convert to int with scaling
                        Parameter2 = (int)(targetPosition.Z * 1000)  // Convert to int with scaling
                    });

                    await Task.Delay(500);
                    Vector3 newPos1 = DynelManager.LocalPlayer.MovementComponent.Position;
                    LogDebug($"[TELEPORT] After experimental message 1 - new position: {newPos1}");
                }
                catch (Exception ex)
                {
                    LogDebug($"[TELEPORT] Experimental message 1 failed: {ex.Message}");
                }

                // Approach 4: Try direct field manipulation if possible
                LogDebug($"[TELEPORT] Approach 4: Exploring direct field manipulation");
                var fields = movementType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                foreach (var field in fields)
                {
                    if (field.Name.ToLower().Contains("position") || field.Name.ToLower().Contains("pos"))
                    {
                        LogDebug($"[TELEPORT] Found position-related field: {field.Name} ({field.FieldType.Name})");

                        if (field.FieldType.Name.Contains("Vector"))
                        {
                            try
                            {
                                LogDebug($"[TELEPORT] Attempting to set field {field.Name}");
                                field.SetValue(movementComponent, targetPosition);
                                await Task.Delay(500);

                                Vector3 newPos = DynelManager.LocalPlayer.MovementComponent.Position;
                                LogDebug($"[TELEPORT] After field {field.Name} set - new position: {newPos}");
                            }
                            catch (Exception ex)
                            {
                                LogDebug($"[TELEPORT] Failed to set field {field.Name}: {ex.Message}");
                            }
                        }
                    }
                }

                // Final position check
                Vector3 finalPos = DynelManager.LocalPlayer.MovementComponent.Position;
                float finalDistance = Vector3.Distance(finalPos, targetPosition);
                LogDebug($"[TELEPORT] Final position: {finalPos}");
                LogDebug($"[TELEPORT] Distance from target: {finalDistance:F1}m");

                if (finalDistance < 1.0f)
                {
                    LogDebug($"[TELEPORT] SUCCESS: Bot appears to have been teleported to target position!");
                }
                else
                {
                    LogDebug($"[TELEPORT] FAILED: All teleport approaches unsuccessful");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[TELEPORT] Error during experimental position setting: {ex.Message}");
            }
        }

        // CheckForRestartTransactionRecovery method removed - restart logic disabled for stability

        /// <summary>
        /// Check if an item is a bot's original item (tools, etc.)
        /// </summary>
        private static bool IsBotItem(Item item)
        {
            if (item == null) return false;

            // Check if it's a known bot tool or item
            var itemName = item.Name?.ToLower() ?? "";

            // Common bot tools and items
            return itemName.Contains("mass relocating robot") ||
                   itemName.Contains("toolkit") ||
                   itemName.Contains("screwdriver") ||
                   itemName.Contains("bio analyzing computer") ||
                   itemName.Contains("mastercomm") ||
                   itemName.Contains("personalization device") ||
                   itemName.Contains("nano programming interface") ||
                   itemName.Contains("ot metamorphing liquid nanobots");
        }

        // Restart recovery helper methods removed - restart logic disabled for stability

        // TriggerBotRestart method removed - restart logic disabled for stability

        // CheckStartupZone method removed - zone refresh logic disabled for stability

        public static void Cleanup()
        {
            try
            {
                Client.Chat.PrivateMessageReceived -= (e, msg) => OnPrivateMessageReceived(msg);
                Trade.TradeOpened -= OnTradeOpened;
                Trade.TradeStatusChanged -= OnTradeStatusChanged;
                Inventory.ContainerOpened -= OnContainerOpened;
                Inventory.ItemAdded -= OnItemAdded;
                Inventory.ItemRemoved -= OnItemRemoved;
                Client.OnUpdate -= OnGameUpdate;
                _activeTradeSessions.Clear();
                _playerBags.Clear();
                _playersWithPendingReturn.Clear();
                _pendingReturns.Clear();

                LogDebug("Private Message Module cleaned up.");
            }
            catch (Exception ex)
            {
                LogDebug($"Error cleaning up Private Message Module: {ex.Message}");
            }
        }

        // OLD ProcessImplantsInBag method REMOVED - now handled by ImplantRecipe.cs through BaseRecipeProcessor
        // Following DEVELOPMENT_RULES.md Rule #6: All recipes must support both loose and bagged item processing through unified RecipeManager

        // OLD ProcessSingleImplantWithClusters method REMOVED - now handled by ImplantRecipe.cs through BaseRecipeProcessor



        // OLD IsImplant method REMOVED - now handled by ImplantRecipe.cs

        // OLD IsCluster method REMOVED - now handled by ImplantRecipe.cs

        // OLD GetImplantSlot method REMOVED - now handled by ImplantRecipe.cs

        // OLD GetClusterSlot method REMOVED - now handled by ImplantRecipe.cs

        // OLD DoesClusterMatchImplant method REMOVED - now handled by ImplantRecipe.cs

        // REMOVED: Duplicate FindAndPullTool method - now using centralized RecipeUtilities.FindAndPullTool()
        // Following RULE #2: There should be ONLY ONE place for tool finding logic

        // REMOVED: ReturnToolToBag method - now using centralized RecipeUtilities.ReturnToolsToOriginalBags()
        // Following RULE #2: There should be ONLY ONE place for tool finding logic

        public static async Task EndToolSession()
        {
            try
            {
                LogDebug($"[TOOL SESSION] Ending tool session - using centralized tool return system");

                // CRITICAL FIX: Wait for tool return to complete instead of running in background
                // This prevents inventory space issues caused by tools not being returned before next processing
                await RecipeUtilities.ReturnToolsToOriginalBags();

                LogDebug($"[TOOL SESSION] ✅ Tool session ended - all tools returned via centralized system");
            }
            catch (Exception ex)
            {
                LogDebug($"[TOOL SESSION] Error ending tool session: {ex.Message}");
            }
        }

        /// <summary>
        /// CRITICAL FAILSAFE: Check for any leftover items from previous trades that weren't properly returned
        /// This prevents items from one player contaminating another player's trade
        /// </summary>
        private static void CheckForLeftoverItemsFromPreviousTrades(int currentPlayerId)
        {
            // CRITICAL FIX: Force return specific stuck items that are known to be leftover player items
            try
            {
                LogDebug($"[LEFTOVER CHECK] Checking for specific stuck items that need to be returned to player {currentPlayerId}");

                var currentInventoryItems = Inventory.Items.Where(item =>
                    item.Slot.Type == IdentityType.Inventory &&
                    !(item.Slot.Instance >= (int)EquipSlot.Weap_Hud1 && item.Slot.Instance <= (int)EquipSlot.Imp_Feet) &&
                    item.UniqueIdentity.Type != IdentityType.Container).ToList();

                // Look for specific items that are known to be stuck player items
                var stuckPlayerItems = currentInventoryItems.Where(item =>
                    !string.IsNullOrEmpty(item.Name) &&
                    (item.Name.Contains("HSR - Sketch and Etch") ||
                     (item.Name == "Screwdriver" && !Core.ItemTracker.IsBotPersonalItem(item))) &&
                    !Core.ItemTracker.IsBotPersonalItem(item) &&
                    !Core.ItemTracker.IsBotTool(item)).ToList();

                if (stuckPlayerItems.Any())
                {
                    LogDebug($"[LEFTOVER CHECK] ⚠️ Found {stuckPlayerItems.Count} stuck player items that need to be returned!");
                    foreach (var stuckItem in stuckPlayerItems)
                    {
                        LogDebug($"[LEFTOVER CHECK] ⚠️ Stuck player item: {stuckItem.Name} (ID: {stuckItem.Id})");
                    }

                    // Add these items to the pending returns for the current player
                    var playerName = GetPlayerName(currentPlayerId);
                    if (_pendingReturns.ContainsKey(currentPlayerId))
                    {
                        _pendingReturns[currentPlayerId].AddRange(stuckPlayerItems);
                    }
                    else
                    {
                        _pendingReturns[currentPlayerId] = stuckPlayerItems.ToList();
                    }

                    LogDebug($"[LEFTOVER CHECK] ✅ Added {stuckPlayerItems.Count} stuck items to pending returns for {playerName}");
                }
                else
                {
                    LogDebug($"[LEFTOVER CHECK] ✅ No stuck player items found");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[LEFTOVER CHECK] Error checking for stuck items: {ex.Message}");
            }
            return;

            try
            {
                LogDebug($"[LEFTOVER CHECK] Checking for leftover items from previous trades before processing player {currentPlayerId}");

                // Find any items in inventory that weren't in the pre-trade inventory
                var currentInventoryItems = Inventory.Items.Where(item =>
                    item.Slot.Type == IdentityType.Inventory &&
                    !(item.Slot.Instance >= (int)EquipSlot.Weap_Hud1 && item.Slot.Instance <= (int)EquipSlot.Imp_Feet) &&
                    item.UniqueIdentity.Type != IdentityType.Container).ToList();

                var leftoverItems = currentInventoryItems.Where(item =>
                    !_preTradeInventory.Any(preItem => preItem != null && preItem.Instance == item.UniqueIdentity.Instance) &&
                    !Recipes.RecipeUtilities.IsProcessingTool(item) &&
                    !string.IsNullOrEmpty(item.Name) &&
                    !item.Name.Contains("Backpack") &&
                    !item.Name.Contains("Novictum Ring") &&
                    !item.Name.Contains("Pure Novictum Ring") &&
                    !item.Name.Contains("Temporary: Data Receptacle")).ToList();

                if (leftoverItems.Any())
                {
                    LogDebug($"[LEFTOVER CHECK] ⚠️ CRITICAL: Found {leftoverItems.Count} leftover items from previous trades!");
                    foreach (var leftoverItem in leftoverItems)
                    {
                        LogDebug($"[LEFTOVER CHECK] ⚠️ Leftover item: {leftoverItem.Name}");
                    }

                    // AUTOMATIC RECOVERY: Save leftover items to return system for recovery
                    LogDebug($"[LEFTOVER CHECK] 🔧 AUTO-RECOVERY: Automatically saving leftover items to return system");
                    SaveLeftoverItemsForRecovery(leftoverItems);

                    LogDebug($"[LEFTOVER CHECK] ✅ Leftover items saved for recovery - players can use 'return' command to retrieve them");
                }
                else
                {
                    LogDebug($"[LEFTOVER CHECK] ✅ No leftover items found - inventory is clean for player {currentPlayerId}");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[LEFTOVER CHECK] Error checking for leftover items: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves leftover items from failed trades to the return system for recovery
        /// This allows players to use the 'return' command to get their items back
        /// </summary>
        private static void SaveLeftoverItemsForRecovery(List<Item> leftoverItems)
        {
            // IMPROVED: Clean up leftover items properly without giving away tools
            LogDebug($"[LEFTOVER RECOVERY] IMPROVED - Cleaning up {leftoverItems.Count} leftover items safely");

            // Filter out any tools or protected items before cleanup
            var safeToCleanup = leftoverItems.Where(item =>
                !Recipes.RecipeUtilities.IsProcessingTool(item) &&
                !Core.ItemTracker.IsBotPersonalItem(item) &&
                !Core.ItemTracker.IsBotTool(item)).ToList();

            if (safeToCleanup.Count != leftoverItems.Count)
            {
                LogDebug($"[LEFTOVER RECOVERY] Protected {leftoverItems.Count - safeToCleanup.Count} tools/bot items from cleanup");
            }

            if (!safeToCleanup.Any())
            {
                LogDebug($"[LEFTOVER RECOVERY] No safe items to cleanup - all items are protected");
                return;
            }

            // For now, just log the items that would be cleaned up
            // TODO: Implement proper cleanup mechanism
            foreach (var item in safeToCleanup)
            {
                LogDebug($"[LEFTOVER RECOVERY] Would cleanup: {item.Name} (ID: {item.Id})");
            }

            return;

            try
            {
                LogDebug($"[LEFTOVER RECOVERY] Saving {leftoverItems.Count} leftover items to return system");

                // Load existing saved trades
                LoadSavedTrades();

                // Group leftover items by their original owner using the tracking system
                var itemsByPlayer = new Dictionary<string, List<Item>>();

                foreach (var item in leftoverItems)
                {
                    string itemKey = $"{item.Name}_{item.Id}";
                    string playerName = null;

                    // Try to find the original owner from our tracking
                    if (_itemToPlayerMapping.ContainsKey(itemKey))
                    {
                        playerName = _itemToPlayerMapping[itemKey];
                        LogDebug($"[LEFTOVER RECOVERY] Found owner for {item.Name}: {playerName}");
                    }
                    else
                    {
                        // Fallback: use a generic key for untracked items
                        playerName = $"UNKNOWN_OWNER_{DateTime.Now:yyyyMMdd_HHmmss}";
                        LogDebug($"[LEFTOVER RECOVERY] No owner found for {item.Name}, using fallback key: {playerName}");
                    }

                    if (!itemsByPlayer.ContainsKey(playerName))
                    {
                        itemsByPlayer[playerName] = new List<Item>();
                    }
                    itemsByPlayer[playerName].Add(item);
                }

                // Save items grouped by player
                foreach (var playerGroup in itemsByPlayer)
                {
                    string playerName = playerGroup.Key;
                    var playerItems = playerGroup.Value;

                    var savedItems = new List<SavedItemData>();
                    foreach (var item in playerItems)
                    {
                        savedItems.Add(new SavedItemData
                        {
                            Name = item.Name,
                            Id = item.Id,
                            Instance = item.UniqueIdentity.Instance,
                            QualityLevel = item.Ql,
                            ItemType = "leftover_loose_item"
                        });
                    }

                    var savedTrade = new SavedTradeData
                    {
                        PlayerName = playerName,
                        PlayerId = -1, // Special ID for leftover items
                        Items = savedItems,
                        SaveTime = DateTime.Now,
                        TimeoutTime = DateTime.Now.AddDays(7), // Keep for 7 days
                        TradeType = "leftover_recovery"
                    };

                    _savedTrades[playerName] = savedTrade;
                    LogDebug($"[LEFTOVER RECOVERY] ✅ Saved {savedItems.Count} leftover items for player '{playerName}'");
                }

                SaveSavedTrades();
                LogDebug($"[LEFTOVER RECOVERY] ✅ All leftover items saved to return system - players can use 'return' command");

                // Clean up the tracking for these items
                foreach (var item in leftoverItems)
                {
                    string itemKey = $"{item.Name}_{item.Id}";
                    _itemToPlayerMapping.Remove(itemKey);
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[LEFTOVER RECOVERY] Error saving leftover items: {ex.Message}");
            }
        }

        /// <summary>
        /// Cleans up item tracking for a completed trade to prevent memory leaks
        /// </summary>
        private static void CleanupItemTrackingForCompletedTrade(int playerId)
        {
            try
            {
                var playerName = GetPlayerName(playerId);
                if (string.IsNullOrEmpty(playerName)) return;

                // Remove all item mappings for this player since the trade completed successfully
                var keysToRemove = _itemToPlayerMapping.Where(kvp => kvp.Value == playerName).Select(kvp => kvp.Key).ToList();
                foreach (var key in keysToRemove)
                {
                    _itemToPlayerMapping.Remove(key);
                }

                if (keysToRemove.Any())
                {
                    LogDebug($"[ITEM TRACKING] Cleaned up {keysToRemove.Count} item mappings for completed trade with {playerName}");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[ITEM TRACKING] Error cleaning up item tracking: {ex.Message}");
            }
        }

        private static async Task MonitorReturnTimeout(int playerId)
        {
            try
            {
                LogDebug($"[TIMEOUT MONITOR] Starting timeout monitoring for player {playerId}");

                // Wait for the timeout period
                await Task.Delay(_returnTimeout);

                // Check if player still has pending returns (hasn't been completed)
                if (_pendingReturns.ContainsKey(playerId) && _returnTimeouts.ContainsKey(playerId))
                {
                    LogDebug($"[TIMEOUT] Return timeout reached for player {playerId}, saving to recovery system");

                    // Get player name for saving
                    var player = DynelManager.Players.FirstOrDefault(p => p.Identity.Instance == playerId);
                    string playerName = player?.Name ?? playerId.ToString();

                    // Save the trade data for recovery
                    await SaveTradeForRecovery(playerId, playerName);

                    // CRITICAL FIX: Complete trade log before cleanup due to timeout
                    if (_currentTradeLogs.ContainsKey(playerId))
                    {
                        LogDebug($"[TRADE LOG] Completing trade log due to timeout for player {playerId}");
                        var bagsReturned = _pendingReturns.ContainsKey(playerId) ? _pendingReturns[playerId].Count : 0;
                        CompleteTradeLog(playerId, bagsReturned);
                    }

                    // Remove from active return system
                    _pendingReturns.Remove(playerId);
                    _playersWithPendingReturn.Remove(playerId);
                    _returnTimeouts.Remove(playerId);
                    _playerBags.Remove(playerId);
                    _returnRetryCount.Remove(playerId); // Clean up retry counter

                    // Remove from queue system to allow new trades
                    RemovePlayerFromQueue(playerId);

                    LogDebug($"[TIMEOUT] Player {playerName} ({playerId}) moved to recovery system, queue cleared");

                    // Notify player if they're still online
                    if (player != null)
                    {
                        SendPrivateMessage(playerId.ToString(),
                            "Your items have been saved for recovery due to timeout. Use '/tell " + Client.CharacterName + " return' to retrieve them later.");
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[TIMEOUT MONITOR] Error monitoring timeout for player {playerId}: {ex.Message}");
            }
        }

        private static Task SaveTradeForRecovery(int playerId, string playerName)
        {
            try
            {
                LogDebug($"[SAVE RECOVERY] Saving trade data for {playerName} ({playerId})");

                if (!_pendingReturns.ContainsKey(playerId))
                {
                    LogDebug($"[SAVE RECOVERY] No pending returns found for player {playerId}");
                    return Task.CompletedTask;
                }

                var items = _pendingReturns[playerId];
                var savedItems = new List<SavedItemData>();

                foreach (var item in items)
                {
                    savedItems.Add(new SavedItemData
                    {
                        Name = item.Name,
                        Id = item.Id,
                        Instance = item.UniqueIdentity.Instance,
                        QualityLevel = item.Ql,
                        ItemType = "bag"
                    });
                }

                var savedTrade = new SavedTradeData
                {
                    PlayerName = playerName,
                    PlayerId = playerId,
                    Items = savedItems,
                    SaveTime = DateTime.Now,
                    TimeoutTime = DateTime.Now.AddDays(7), // Keep for 7 days
                    TradeType = "processing"
                };

                _savedTrades[playerName] = savedTrade;
                SaveSavedTrades();

                LogDebug($"[SAVE RECOVERY] Saved {savedItems.Count} items for {playerName}");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                LogDebug($"[SAVE RECOVERY] Error saving trade for recovery: {ex.Message}");
                return Task.CompletedTask;
            }
        }



        private static void LoadSavedTrades()
        {
            try
            {
                if (File.Exists(_savedTradesFile))
                {
                    var json = File.ReadAllText(_savedTradesFile);
                    _savedTrades = JsonConvert.DeserializeObject<Dictionary<string, SavedTradeData>>(json) ?? new Dictionary<string, SavedTradeData>();

                    // Clean up expired trades
                    var expiredTrades = _savedTrades.Where(kvp => DateTime.Now > kvp.Value.TimeoutTime).Select(kvp => kvp.Key).ToList();
                    foreach (var expiredTrade in expiredTrades)
                    {
                        _savedTrades.Remove(expiredTrade);
                    }

                    if (expiredTrades.Any())
                    {
                        SaveSavedTrades();
                        LogDebug($"[SAVED TRADES] Cleaned up {expiredTrades.Count} expired trades");
                    }

                    LogDebug($"[SAVED TRADES] Loaded {_savedTrades.Count} saved trades");
                }
                else
                {
                    _savedTrades = new Dictionary<string, SavedTradeData>();
                    LogDebug($"[SAVED TRADES] No saved trades file found, starting fresh");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[SAVED TRADES] Error loading saved trades: {ex.Message}");
                _savedTrades = new Dictionary<string, SavedTradeData>();
            }
        }

        private static void SaveSavedTrades()
        {
            try
            {
                var directory = System.IO.Path.GetDirectoryName(_savedTradesFile);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonConvert.SerializeObject(_savedTrades, Formatting.Indented);
                File.WriteAllText(_savedTradesFile, json);
                LogDebug($"[SAVED TRADES] Saved {_savedTrades.Count} trades to file");
            }
            catch (Exception ex)
            {
                LogDebug($"[SAVED TRADES] Error saving trades: {ex.Message}");
            }
        }

        private static async Task RestoreAndReturnSavedTrade(string playerName, SavedTradeData savedTrade)
        {
            try
            {
                LogDebug($"[RESTORE] Attempting to restore and return saved trade for {playerName}");

                // Find the player
                var player = DynelManager.Players.FirstOrDefault(p => p.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase));
                if (player == null)
                {
                    SendPrivateMessage(playerName, "You need to be nearby for me to return your items. Please come closer and try again.");
                    return;
                }

                // Check distance
                float distance = player.DistanceFrom(DynelManager.LocalPlayer);
                if (distance > 10f)
                {
                    SendPrivateMessage(playerName, $"Please come closer for item return. Current distance: {distance:F1}m (need within 10m)");
                    return;
                }

                // Try to find the actual items in inventory
                var itemsToReturn = new List<Item>();
                foreach (var savedItem in savedTrade.Items)
                {
                    var item = Inventory.Items.FirstOrDefault(invItem =>
                        invItem.Name == savedItem.Name &&
                        invItem.Id == savedItem.Id);

                    if (item != null)
                    {
                        itemsToReturn.Add(item);
                    }
                    else
                    {
                        LogDebug($"[RESTORE] Could not find saved item: {savedItem.Name} (ID: {savedItem.Id})");
                    }
                }

                if (!itemsToReturn.Any())
                {
                    SendPrivateMessage(playerName, "Your saved items could not be found in my inventory. They may have been lost due to a crash or restart.");
                    _savedTrades.Remove(playerName);
                    SaveSavedTrades();
                    return;
                }

                LogDebug($"[RESTORE] Found {itemsToReturn.Count} items to return to {playerName}");

                // Mark this player as being in a recovery trade (no processing/zone refresh)
                int playerId = player.Identity.Instance;
                _activeRecoveryTrades.Add(playerId);
                LogDebug($"[RESTORE] Marked player {playerId} as in recovery trade - no processing will occur");

                // Open trade and return items
                Trade.Open(player.Identity);
                await Task.Delay(1000);

                foreach (var item in itemsToReturn)
                {
                    // COMPREHENSIVE TOOL PROTECTION: Use new protection system
                    if (IsProcessingTool(item))
                    {
                        LogDebug($"[TOOL PROTECTION] ❌ BLOCKED TOOL: {GetItemDisplayName(item)} (ID: {item.Id}) - TOOLS MUST NEVER BE GIVEN TO PLAYERS!");
                        continue; // Skip this item - DO NOT add to trade
                    }

                    Trade.AddItem(item.Slot);

                    // Log returned item for detailed trade log (recovery trade)
                    if (item.UniqueIdentity.Type == IdentityType.Container)
                    {
                        var container = Inventory.Containers.FirstOrDefault(c => c.Identity.Instance == item.UniqueIdentity.Instance);
                        if (container != null)
                        {
                            var bagContents = container.Items.Select(bagItem => GetItemDisplayName(bagItem)).ToList();
                            LogReturnedBag(playerId, item.Name, bagContents);
                        }
                        else
                        {
                            LogReturnedBag(playerId, item.Name, new List<string>());
                        }
                    }
                    else
                    {
                        // SIMPLE TRADE LOGGING: Only log items that are NOT bot's original items
                        if (!IsBotOriginalItem(item.Id))
                        {
                            LogReturnedLooseItem(playerId, GetItemDisplayName(item));
                            LogDebug($"[SIMPLE TRADE LOG] ✅ LOGGED RETURNED PLAYER ITEM: {GetItemDisplayName(item)}");
                        }
                        else
                        {
                            LogDebug($"[SIMPLE TRADE LOG] 🛡️ BLOCKED bot original item from return log: {GetItemDisplayName(item)}");
                        }
                    }

                    await Task.Delay(150);
                }

                // Mark as completed and remove from saved trades
                savedTrade.IsCompleted = true;
                _savedTrades.Remove(playerName);
                SaveSavedTrades();

                SendPrivateMessage(playerName, $"Recovery trade opened! {itemsToReturn.Count} bag(s) added to trade. Please accept to complete the return.");
                LogDebug($"[RESTORE] Recovery trade initiated for {playerName} with {itemsToReturn.Count} items");
            }
            catch (Exception ex)
            {
                LogDebug($"[RESTORE] Error restoring trade for {playerName}: {ex.Message}");
                SendPrivateMessage(playerName, "Error processing your return request. Please try again later.");
            }
        }

        // Helper methods for duplicate checking (simplified versions of ImplantRecipe methods)
        private static bool IsImplantForDuplicateCheck(Item item)
        {
            try
            {
                string itemName = item.Name.ToLower();

                // Check for implant keywords (same logic as ImplantRecipe)
                return itemName.Contains("implant") ||
                       itemName.Contains("brain symbiont") ||
                       itemName.Contains("eye symbiont") ||
                       itemName.Contains("ocular symbiont") ||
                       itemName.Contains("neural symbiont") ||
                       itemName.Contains("cyberdeck") ||
                       itemName.Contains("memory chip") ||
                       itemName.Contains("processor chip");
            }
            catch
            {
                return false;
            }
        }

        private static bool IsClusterForDuplicateCheck(Item item)
        {
            try
            {
                string itemName = item.Name.ToLower();
                return itemName.Contains("cluster") ||
                       itemName.Contains("refined") ||
                       itemName.Contains("polished") ||
                       itemName.Contains("bright") ||
                       itemName.Contains("shining") ||
                       itemName.Contains("faded") ||
                       itemName.Contains("dim");
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get current bag status for debugging
        /// </summary>
        private static string GetBagStatus()
        {
            try
            {
                var status = "=== BAG STATUS ===\n";
                status += $"Inventory Items: {Inventory.Items?.Count() ?? 0}\n";
                status += $"Containers: {Inventory.Containers?.Count() ?? 0}\n\n";

                // List all bags in inventory
                var bags = Inventory.Items?.Where(item => item.UniqueIdentity.Type == IdentityType.Container).ToList() ?? new List<Item>();
                status += $"Bags in Inventory ({bags.Count}):\n";
                foreach (var bag in bags)
                {
                    status += $"  - {bag.Name} (ID: {bag.Id})\n";
                }

                // List all open containers
                status += $"\nOpen Containers ({Inventory.Containers?.Count() ?? 0}):\n";
                if (Inventory.Containers != null)
                {
                    foreach (var container in Inventory.Containers)
                    {
                        var bagItem = container.Item;
                        status += $"  - {bagItem?.Name ?? "Unknown"} (Items: {container.Items?.Count ?? 0})\n";
                    }
                }

                return status;
            }
            catch (Exception ex)
            {
                return $"Error getting bag status: {ex.Message}";
            }
        }
    }

    public class TradeSession
    {
        public int PlayerId { get; set; }
        public TradeState State { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
    }

    public class TradeRequest
    {
        public int PlayerId { get; set; }
        public string PlayerName { get; set; }
        public DateTime RequestTime { get; set; }
        public Vector3 LastKnownPosition { get; set; }
    }



    public class DuplicateCheckResult
    {
        public bool HasDuplicates { get; set; } = false;
        public List<Item> DuplicateImplants { get; set; } = new List<Item>();
        public List<Item> DuplicateClusters { get; set; } = new List<Item>();
    }



    public enum TradeState
    {
        Opened,
        FirstAccepted,
        SecondAccepted,
        Completed,
        Declined
    }

    public class SavedTradeData
    {
        public string PlayerName { get; set; }
        public int PlayerId { get; set; }
        public List<SavedItemData> Items { get; set; } = new List<SavedItemData>();
        public DateTime SaveTime { get; set; }
        public DateTime TimeoutTime { get; set; }
        public bool IsCompleted { get; set; } = false;
        public string TradeType { get; set; } // "processing", "return", etc.
    }

    public class SavedItemData
    {
        public string Name { get; set; }
        public int Id { get; set; }
        public int Instance { get; set; }
        public int QualityLevel { get; set; }
        public string ItemType { get; set; } // "bag", "item", etc.
    }

    public enum BotState
    {
        Ready,          // Bot is ready for new trades
        Processing,     // Bot is processing items (bags or loose items)
        Returning       // Bot is attempting to return processed items
    }
}
