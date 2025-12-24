using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AOSharp.Clientless;
using AOSharp.Common.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
// using Newtonsoft.Json;

namespace Craftbot.Core
{
    /// <summary>
    /// Comprehensive inventory tracking system for the Craftbot
    /// Tracks bot's personal items, tool bags, received items, and recipe results
    /// </summary>
    public static class ItemTracker
    {
        // Legacy storage system (kept for compatibility)
        private static Dictionary<string, StoredItem> _storedItems = new Dictionary<string, StoredItem>();

        // Track player-provided tools that aren't required by recipes
        private static Dictionary<int, AccidentalToolInfo> _accidentalPlayerTools = new Dictionary<int, AccidentalToolInfo>();

        /// <summary>
        /// Information about a tool that was accidentally provided by a player
        /// </summary>
        public class AccidentalToolInfo
        {
            public int ItemId { get; set; }
            public int UniqueInstance { get; set; }
            public string ItemName { get; set; }
            public int PlayerId { get; set; }
            public DateTime ReceivedTime { get; set; }
            public string OriginalBag { get; set; }

            public AccidentalToolInfo(int itemId, int uniqueInstance, string itemName, int playerId, string originalBag = null)
            {
                ItemId = itemId;
                UniqueInstance = uniqueInstance;
                ItemName = itemName;
                PlayerId = playerId;
                ReceivedTime = DateTime.Now;
                OriginalBag = originalBag;
            }
        }

        // Comprehensive inventory tracking
        private static Dictionary<string, TrackedItem> _botPersonalItems = new Dictionary<string, TrackedItem>();
        private static Dictionary<string, TrackedItem> _botToolBags = new Dictionary<string, TrackedItem>();
        private static Dictionary<string, List<TrackedItem>> _toolsInBags = new Dictionary<string, List<TrackedItem>>();
        private static Dictionary<string, TrackedItem> _receivedItems = new Dictionary<string, TrackedItem>();
        private static Dictionary<string, TrackedItem> _recipeResults = new Dictionary<string, TrackedItem>();

        // Track items received from current player by Item ID (not instance, since instance is 0 for tools)
        private static Dictionary<int, ReceivedItemInfo> _currentTradeReceivedItems = new Dictionary<int, ReceivedItemInfo>();

        private static string _logFilePath;
        private static bool _initialized = false;
        private static bool _inventorySnapshotTaken = false;

        /// <summary>
        /// Information about an item received from a player in the current trade
        /// </summary>
        public class ReceivedItemInfo
        {
            public int ItemId { get; set; }
            public string ItemName { get; set; }
            public int Quality { get; set; }
            public string PlayerName { get; set; }
            public DateTime ReceivedAt { get; set; }
        }

        public static void Initialize()
        {
            if (_initialized) return;

            try
            {
                // Set up log file path in Control Panel/logs
                string logDir = Path.Combine(Craftbot.PluginDir, "Control Panel", "logs");
                Directory.CreateDirectory(logDir);
                _logFilePath = Path.Combine(logDir, $"craftbot_transactions_{DateTime.Now:yyyyMMdd}.log");

                // Load existing stored items if any
                LoadStoredItems();

                _initialized = true;
                LogTransaction("SYSTEM", "Craftbot ItemTracker initialized with comprehensive inventory tracking");

                // Take initial inventory snapshot if not already taken
                if (!_inventorySnapshotTaken)
                {
                    TakeInitialInventorySnapshot();
                }
            }
            catch (Exception ex)
            {
                // Silent error handling but log to file if possible
                try
                {
                    File.AppendAllText(_logFilePath ?? "bankbot_error.log",
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - SYSTEM - ERROR: {ex.Message}\n");
                }
                catch { }
            }
        }

        /// <summary>
        /// Takes a snapshot of the bot's initial inventory state
        /// This includes all items in inventory and all tool bags with their contents
        /// </summary>
        public static void TakeInitialInventorySnapshot()
        {
            if (_inventorySnapshotTaken) return;

            try
            {
                LogTransaction("SYSTEM", "========================================");
                LogTransaction("SYSTEM", "BOT STARTUP INVENTORY CAPTURE");
                LogTransaction("SYSTEM", "========================================");

                // Clear existing tracking
                _botPersonalItems.Clear();
                _botToolBags.Clear();
                _toolsInBags.Clear();

                // Track all items currently in inventory
                foreach (var item in Inventory.Items)
                {
                    var trackedItem = new TrackedItem(item);

                    if (item.UniqueIdentity.Type == IdentityType.Container)
                    {
                        // This is a tool bag
                        _botToolBags[trackedItem.GetKey()] = trackedItem;
                        LogTransaction("SYSTEM", $"BOT TOOL BAG | Name: {item.Name} | ID: {item.Id} | UniqueInstance: {item.UniqueIdentity.Instance} | SlotType: {item.Slot.Type} | SlotInstance: {item.Slot.Instance}");
                    }
                    else
                    {
                        // This is a personal item
                        _botPersonalItems[trackedItem.GetKey()] = trackedItem;
                        LogTransaction("SYSTEM", $"BOT PERSONAL ITEM | Name: {item.Name} | ID: {item.Id} | UniqueInstance: {item.UniqueIdentity.Instance} | QL: {item.Ql} | SlotType: {item.Slot.Type} | SlotInstance: {item.Slot.Instance}");
                    }
                }

                // Track all items in opened containers
                foreach (var container in Inventory.Containers)
                {
                    TrackToolBagContents(container.Item);
                }

                _inventorySnapshotTaken = true;

                LogTransaction("SYSTEM", "========================================");
                LogTransaction("SYSTEM", $"INVENTORY CAPTURE COMPLETE");
                LogTransaction("SYSTEM", $"Personal Items: {_botPersonalItems.Count}");
                LogTransaction("SYSTEM", $"Tool Bags: {_botToolBags.Count}");
                LogTransaction("SYSTEM", $"Tools in Bags: {_toolsInBags.Values.Sum(list => list.Count)}");
                LogTransaction("SYSTEM", "========================================");
            }
            catch (Exception ex)
            {
                LogTransaction("SYSTEM", $"ERROR taking initial inventory snapshot: {ex.Message}");
            }
        }

        /// <summary>
        /// Tracks the contents of a tool bag
        /// </summary>
        private static void TrackToolBagContents(Item bagItem)
        {
            if (bagItem == null) return;

            try
            {
                var container = Inventory.Containers.FirstOrDefault(c => c.Item?.UniqueIdentity == bagItem.UniqueIdentity);
                if (container != null)
                {
                    string bagKey = new TrackedItem(bagItem).GetKey();
                    if (!_toolsInBags.ContainsKey(bagKey))
                    {
                        _toolsInBags[bagKey] = new List<TrackedItem>();
                    }

                    LogTransaction("SYSTEM", $"--- Tools in bag '{bagItem.Name}' ---");
                    foreach (var tool in container.Items)
                    {
                        var trackedTool = new TrackedItem(tool);
                        _toolsInBags[bagKey].Add(trackedTool);
                        LogTransaction("SYSTEM", $"BOT TOOL | Name: {tool.Name} | ID: {tool.Id} | UniqueInstance: {tool.UniqueIdentity.Instance} | QL: {tool.Ql} | SlotType: {tool.Slot.Type} | SlotInstance: {tool.Slot.Instance}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogTransaction("SYSTEM", $"ERROR tracking bag contents for {bagItem.Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Process items received in trade - tracks them separately from bot's personal items
        /// CRITICAL FIX: Checks if items were already in bot inventory before marking as received
        /// </summary>
        public static void ProcessReceivedItems(List<Item> items, string playerName)
        {
            if (!_initialized) Initialize();

            try
            {
                // DO NOT clear here - items are added one at a time!
                // _currentTradeReceivedItems will be cleared when trade completes

                int skippedCount = 0;
                int trackedCount = 0;

                foreach (var item in items)
                {
                    // CRITICAL FIX: Skip items that were already in bot's inventory before trade
                    // This prevents bot's own items from being marked as "received from player"
                    if (IsBotPersonalItem(item))
                    {
                        LogTransaction("SYSTEM", $"SKIPPED (BOT PERSONAL): {item.Name} (ID:{item.Id}) - already in bot inventory");
                        skippedCount++;
                        continue;
                    }

                    if (IsBotToolBag(item))
                    {
                        LogTransaction("SYSTEM", $"SKIPPED (BOT TOOL BAG): {item.Name} (ID:{item.Id}) - bot's tool bag");
                        skippedCount++;
                        continue;
                    }

                    if (IsBotTool(item))
                    {
                        LogTransaction("SYSTEM", $"SKIPPED (BOT TOOL): {item.Name} (ID:{item.Id}) - bot's tool");
                        skippedCount++;
                        continue;
                    }

                    var trackedItem = new TrackedItem(item, playerName);
                    _receivedItems[trackedItem.GetKey()] = trackedItem;

                    // Track by Item ID for this trade (since Instance is 0 for tools)
                    var receivedInfo = new ReceivedItemInfo
                    {
                        ItemId = item.Id,
                        ItemName = item.Name ?? $"Item_{item.Id}",
                        Quality = item.Ql,
                        PlayerName = playerName,
                        ReceivedAt = DateTime.Now
                    };
                    _currentTradeReceivedItems[item.Id] = receivedInfo;

                    LogTransaction(playerName, $"RECEIVED: {item.Name} (ID:{item.Id}, QL:{item.Ql})");
                    trackedCount++;
                }

                LogTransaction("SYSTEM", $"Processed {trackedCount} received items from {playerName} (skipped {skippedCount} bot items)");
                LogTransaction("SYSTEM", $"Current trade received items: {string.Join(", ", _currentTradeReceivedItems.Values.Select(i => i.ItemName))}");

                // ACCIDENTAL TOOL DETECTION: Check if player provided tools that aren't required
                // Extract player ID from playerName if possible (simplified approach)
                int playerId = playerName.GetHashCode(); // Simple hash as player ID for tracking
                ScanForAccidentalTools(items, playerId);
            }
            catch (Exception ex)
            {
                LogTransaction("SYSTEM", $"ERROR processing items from {playerName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Tracks an item as a recipe result
        /// </summary>
        public static void TrackRecipeResult(Item item, string recipeName)
        {
            if (!_initialized) Initialize();

            try
            {
                var trackedItem = new TrackedItem(item, $"RECIPE:{recipeName}");
                _recipeResults[trackedItem.GetKey()] = trackedItem;
                LogTransaction("RECIPE", $"CREATED: {item.Name} from {recipeName}");
            }
            catch (Exception ex)
            {
                LogTransaction("SYSTEM", $"ERROR tracking recipe result {item.Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if an item is a bot's personal item (was in inventory before any trades)
        /// </summary>
        public static bool IsBotPersonalItem(Item item)
        {
            if (!_initialized) Initialize();

            // EXCLUSION: Monster parts, clusters, implants, nano crystals, and pearls/gems are NEVER bot personal items
            if (item?.Name != null)
            {
                var itemNameLower = item.Name.ToLower();
                if (itemNameLower.Contains("monster parts") ||
                    itemNameLower.Contains("pelted monster parts") ||
                    itemNameLower.Contains("cluster") ||
                    itemNameLower.Contains("implant") ||
                    itemNameLower.Contains("nano crystal") ||
                    itemNameLower.Contains("pearl") ||
                    itemNameLower.Contains("gem") ||
                    itemNameLower.Contains("ruby") ||
                    itemNameLower.Contains("sapphire") ||
                    itemNameLower.Contains("emerald") ||
                    itemNameLower.Contains("diamond") ||
                    itemNameLower.Contains("soul fragment") ||
                    itemNameLower.Contains("ember"))
                {
                    return false; // Processable items are NEVER bot personal items
                }
            }

            // CRITICAL: Check by UNIQUE INSTANCE ID - this is the ONLY way to identify the exact item
            var key = new TrackedItem(item).GetKey();
            bool isPersonal = _botPersonalItems.ContainsKey(key);

            if (isPersonal)
            {
                LogTransaction("SYSTEM", $"[TOOL PROTECTION] BLOCKING bot's personal item: {item.Name} | ID:{item.Id} | UniqueInstance:{item.UniqueIdentity.Instance}");
            }

            return isPersonal;
        }

        /// <summary>
        /// Checks if an item is a bot's tool bag
        /// </summary>
        public static bool IsBotToolBag(Item item)
        {
            if (!_initialized) Initialize();

            var key = new TrackedItem(item).GetKey();
            return _botToolBags.ContainsKey(key);
        }

        /// <summary>
        /// Checks if an item was received from the player in the current trade
        /// This is used to determine if we should return the item to the player
        /// </summary>
        public static bool WasReceivedFromPlayer(Item item)
        {
            if (!_initialized) Initialize();

            // CRITICAL FIX: Check by UNIQUE INSTANCE, not just Item ID
            // This prevents bot's tools from being marked as "received from player" just because
            // the player gave an item with the same Item ID
            var key = new TrackedItem(item).GetKey();
            bool wasReceived = _receivedItems.ContainsKey(key);

            if (wasReceived)
            {
                var receivedInfo = _receivedItems[key];
                LogTransaction("SYSTEM", $"[PLAYER ITEM] Item {item.Name} (ID:{item.Id}, Instance:{item.UniqueIdentity.Instance}) was received from player {receivedInfo.Source} - SAFE TO RETURN");
            }

            return wasReceived;
        }

        /// <summary>
        /// Clear the current trade's received items tracking
        /// Call this when trade is complete and items have been returned
        /// CRITICAL FIX: Also clears _receivedItems to prevent items from previous trades accumulating
        /// </summary>
        public static void ClearCurrentTradeItems()
        {
            LogTransaction("SYSTEM", $"Clearing current trade received items tracking ({_currentTradeReceivedItems.Count} items, {_receivedItems.Count} received items)");
            _currentTradeReceivedItems.Clear();

            // CRITICAL FIX: Also clear the _receivedItems dictionary to prevent items from previous trades
            // from being returned to the wrong player
            _receivedItems.Clear();

            // Also clear recipe results since they should be returned with the trade
            LogTransaction("SYSTEM", $"Clearing recipe results tracking ({_recipeResults.Count} items)");
            _recipeResults.Clear();
        }

        /// <summary>
        /// Checks if an item is a tool from the bot's tool bags
        /// </summary>
        public static bool IsBotTool(Item item)
        {
            if (!_initialized) Initialize();

            // EXCLUSION: Monster parts, clusters, implants, nano crystals, and pearls/gems are NEVER bot tools
            if (item?.Name != null)
            {
                var itemNameLower = item.Name.ToLower();
                if (itemNameLower.Contains("monster parts") ||
                    itemNameLower.Contains("pelted monster parts") ||
                    itemNameLower.Contains("cluster") ||
                    itemNameLower.Contains("implant") ||
                    itemNameLower.Contains("nano crystal") ||
                    itemNameLower.Contains("pearl") ||
                    itemNameLower.Contains("gem") ||
                    itemNameLower.Contains("ruby") ||
                    itemNameLower.Contains("sapphire") ||
                    itemNameLower.Contains("emerald") ||
                    itemNameLower.Contains("diamond") ||
                    itemNameLower.Contains("soul fragment") ||
                    itemNameLower.Contains("ember"))
                {
                    return false; // Processable items are NEVER bot tools
                }
            }

            // CRITICAL: Check by UNIQUE INSTANCE ID - this is the ONLY way to identify the exact item
            var itemKey = new TrackedItem(item).GetKey();

            foreach (var bagTools in _toolsInBags.Values)
            {
                if (bagTools.Any(tool => tool.GetKey() == itemKey))
                {
                    LogTransaction("SYSTEM", $"[TOOL PROTECTION] BLOCKING bot's tool: {item.Name} | ID:{item.Id} | UniqueInstance:{item.UniqueIdentity.Instance}");
                    return true;
                }
            }

            // CRITICAL FIX: If not found in bags, check by known tool IDs
            // This catches tools that were moved to inventory during recipe processing
            HashSet<int> knownToolIds = new HashSet<int>
            {
                154332, // Advanced Bio-Comminutor - CRITICAL: Was being given away
                151366, // Jensen Gem Cutter
                229870, // Ancient Novictum Refiner
                87814,  // Advanced Hacker Tool
                268509, // Alien Material Conversion kit
                267751, // Ancient Engineering Device
                95577,  // Lock Pick
                161699, // Nano Programming Interface - CRITICAL: Was being given away to Ducksurper
                162219, // Mass Relocating Robot (Shape Soft Armor) - CRITICAL: Was being given away - SUPER VALUABLE
            };

            if (knownToolIds.Contains(item.Id))
            {
                LogTransaction("SYSTEM", $"[TOOL PROTECTION] BLOCKING bot's tool by ID: {item.Name} | ID:{item.Id} | UniqueInstance:{item.UniqueIdentity.Instance}");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if an item was received from a player
        /// </summary>
        public static bool IsReceivedItem(Item item)
        {
            if (!_initialized) Initialize();

            var key = new TrackedItem(item).GetKey();
            return _receivedItems.ContainsKey(key);
        }

        /// <summary>
        /// Checks if an item is a recipe result
        /// </summary>
        public static bool IsRecipeResult(Item item)
        {
            if (!_initialized) Initialize();

            var key = new TrackedItem(item).GetKey();
            return _recipeResults.ContainsKey(key);
        }

        /// <summary>
        /// Gets the source of an item (bot personal, received from player, recipe result, etc.)
        /// </summary>
        public static string GetItemSource(Item item)
        {
            if (!_initialized) Initialize();

            var key = new TrackedItem(item).GetKey();

            if (_botPersonalItems.ContainsKey(key))
                return "BOT_PERSONAL";
            if (_botToolBags.ContainsKey(key))
                return "BOT_TOOL_BAG";
            if (IsBotTool(item))
                return "BOT_TOOL";
            if (_receivedItems.ContainsKey(key))
                return $"RECEIVED_FROM_{_receivedItems[key].Source}";
            if (_recipeResults.ContainsKey(key))
                return $"RECIPE_RESULT_{_recipeResults[key].Source}";

            return "UNKNOWN";
        }

        /// <summary>
        /// Gets comprehensive inventory statistics
        /// </summary>
        public static string GetInventoryStats()
        {
            if (!_initialized) Initialize();

            return $"Bot Personal Items: {_botPersonalItems.Count}, " +
                   $"Bot Tool Bags: {_botToolBags.Count}, " +
                   $"Tools in Bags: {_toolsInBags.Values.Sum(list => list.Count)}, " +
                   $"Received Items: {_receivedItems.Count}, " +
                   $"Recipe Results: {_recipeResults.Count}";
        }

        /// <summary>
        /// Gets all items that should be returned to players (received items + recipe results)
        /// </summary>
        public static List<Item> GetItemsToReturnToPlayers()
        {
            if (!_initialized) Initialize();

            var itemsToReturn = new List<Item>();

            try
            {
                foreach (var item in Inventory.Items)
                {
                    if (IsReceivedItem(item) || IsRecipeResult(item))
                    {
                        itemsToReturn.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                LogTransaction("SYSTEM", $"ERROR getting items to return: {ex.Message}");
            }

            return itemsToReturn;
        }

        /// <summary>
        /// Gets all bot's personal items and tools that should never be given away
        /// </summary>
        public static List<Item> GetBotProtectedItems()
        {
            if (!_initialized) Initialize();

            var protectedItems = new List<Item>();

            try
            {
                foreach (var item in Inventory.Items)
                {
                    if (IsBotPersonalItem(item) || IsBotToolBag(item) || IsBotTool(item))
                    {
                        protectedItems.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                LogTransaction("SYSTEM", $"ERROR getting protected items: {ex.Message}");
            }

            return protectedItems;
        }

        /// <summary>
        /// Removes an item from tracking when it's moved or consumed
        /// </summary>
        public static void RemoveFromTracking(Item item, string reason = "REMOVED")
        {
            if (!_initialized) Initialize();

            try
            {
                var key = new TrackedItem(item).GetKey();

                if (_receivedItems.ContainsKey(key))
                {
                    _receivedItems.Remove(key);
                    LogTransaction("SYSTEM", $"Removed received item from tracking: {item.Name} - {reason}");
                }

                if (_recipeResults.ContainsKey(key))
                {
                    _recipeResults.Remove(key);
                    LogTransaction("SYSTEM", $"Removed recipe result from tracking: {item.Name} - {reason}");
                }
            }
            catch (Exception ex)
            {
                LogTransaction("SYSTEM", $"ERROR removing item from tracking: {ex.Message}");
            }
        }

        /// <summary>
        /// Test method to verify inventory tracking is working correctly
        /// </summary>
        public static string TestInventoryTracking()
        {
            if (!_initialized) Initialize();

            try
            {
                var testResults = "=== INVENTORY TRACKING TEST ===\n";

                // Test current inventory items
                var currentItems = Inventory.Items.Take(5).ToList(); // Test first 5 items

                foreach (var item in currentItems)
                {
                    var source = GetItemSource(item);
                    var isBotPersonal = IsBotPersonalItem(item);
                    var isBotTool = IsBotTool(item);
                    var isReceived = IsReceivedItem(item);
                    var isRecipeResult = IsRecipeResult(item);

                    testResults += $"Item: {item.Name ?? "Unknown"}\n";
                    testResults += $"  Source: {source}\n";
                    testResults += $"  Bot Personal: {isBotPersonal}\n";
                    testResults += $"  Bot Tool: {isBotTool}\n";
                    testResults += $"  Received: {isReceived}\n";
                    testResults += $"  Recipe Result: {isRecipeResult}\n\n";
                }

                testResults += GetInventoryStats();

                LogTransaction("SYSTEM", "Inventory tracking test completed");
                return testResults;
            }
            catch (Exception ex)
            {
                LogTransaction("SYSTEM", $"ERROR in inventory tracking test: {ex.Message}");
                return $"Test failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Process a bag item - open it and index contents
        /// </summary>
        private static void ProcessBag(Item bag, string playerName)
        {
            // COMMENTED OUT - Bankbot storage functionality not needed in Craftbot
            /*
            try
            {
                LogTransaction(playerName, bag.Name); // Log the bag itself

                // Get bag contents
                var bagContents = bag.GetItems();
                if (bagContents != null && bagContents.Any())
                {
                    foreach (var bagItem in bagContents)
                    {
                        ProcessIndividualItem(bagItem, playerName);
                    }
                }
            }
            catch (Exception ex)
            {
                LogTransaction("SYSTEM", $"ERROR processing bag {bag.Name} from {playerName}: {ex.Message}");
            }
            */
        }

        /// <summary>
        /// Process an individual item and store it
        /// </summary>
        private static void ProcessIndividualItem(Item item, string playerName)
        {
            // COMMENTED OUT - Bankbot storage functionality not needed in Craftbot
            /*
            try
            {
                var storedItem = new StoredItem
                {
                    Id = item.Id,
                    Name = item.Name,
                    Quality = item.Quality,
                    Quantity = item.Quantity,
                    StoredBy = playerName,
                    StoredAt = DateTime.Now,
                    ItemInstance = item.UniqueIdentity.Instance
                };

                // Use a unique key for storage
                string itemKey = $"{item.Name}_{item.Id}_{item.UniqueIdentity.Instance}";
                _storedItems[itemKey] = storedItem;

                // Log the transaction
                LogTransaction(playerName, item.Name);

                // Save updated storage
                SaveStoredItems();
            }
            catch (Exception ex)
            {
                LogTransaction("SYSTEM", $"ERROR storing item {item.Name} from {playerName}: {ex.Message}");
            }
            */
        }

        /// <summary>
        /// Get all stored items (excluding bags for listing)
        /// </summary>
        public static List<object> GetStoredItems(bool includeBags = false)
        {
            if (!_initialized) Initialize();

            try
            {
                return _storedItems.Values
                    .Where(item => includeBags || !IsContainer(item))
                    .OrderBy(item => item.Name)
                    .Cast<object>()
                    .ToList();
            }
            catch (Exception)
            {
                return new List<object>();
            }
        }

        /// <summary>
        /// Remove an item from storage (when given to player)
        /// </summary>
        public static bool RemoveItem(string itemKey, string playerName)
        {
            if (!_initialized) Initialize();

            try
            {
                if (_storedItems.ContainsKey(itemKey))
                {
                    var item = _storedItems[itemKey];
                    _storedItems.Remove(itemKey);
                    
                    LogTransaction(playerName, $"RETRIEVED: {item.Name}");
                    SaveStoredItems();
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogTransaction("SYSTEM", $"ERROR removing item {itemKey} for {playerName}: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Find a stored item by name for retrieval
        /// </summary>
        public static object FindItemByName(string itemName)
        {
            if (!_initialized) Initialize();

            try
            {
                return _storedItems.Values
                    .FirstOrDefault(item => item.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Extract item properties safely from an Item object
        /// This method detects quality level (QL) and stack count for trade logging
        /// </summary>
        public static (int stackCount, int qualityLevel) ExtractItemProperties(Item item)
        {
            try
            {
                int stackCount = 1;
                int qualityLevel = 0;

                // Use reflection to check for Charges property (works with any AOSharp version)
                try
                {
                    var chargesProp = item.GetType().GetProperty("Charges");
                    if (chargesProp != null)
                    {
                        var value = chargesProp.GetValue(item);
                        int charges = value is int ? (int)value : (value != null ? Convert.ToInt32(value) : 1);
                        if (charges > 1)
                        {
                            stackCount = charges;
                        }
                    }
                }
                catch (Exception)
                {
                    // Silent error handling for charges detection
                }

                // Now try to extract quality level from the most likely candidates
                string[] qualityCandidates = { "Ql", "QualityLevel", "QL", "Quality", "ItemLevel", "Level" };

                // Debug: Log all properties for items that might not have QL detected
                bool debugItem = false;
                string itemName = "";
                try
                {
                    itemName = item.Name ?? "";
                    // Debug items that might be missing QL - expanded to include implants, clusters, pearls, gems, monster parts, plasma
                    if (itemName.ToLower().Contains("pearl") ||
                        itemName.ToLower().Contains("gem") ||
                        itemName.ToLower().Contains("implant") ||
                        itemName.ToLower().Contains("cluster") ||
                        itemName.ToLower().Contains("plasma") ||
                        itemName.ToLower().Contains("monster") ||
                        itemName.ToLower().Contains("blood") ||
                        itemName.ToLower().Contains("notum") ||
                        itemName.ToLower().Contains("kyr'ozch") ||
                        itemName.ToLower().Contains("clump"))
                    {
                        debugItem = true;
                        Modules.PrivateMessageModule.LogDebug($"[ITEM QL DEBUG] Analyzing item: {itemName} (ID: {item.Id})");

                        // Log all properties
                        var allProps = item.GetType().GetProperties();
                        var propStrings = new System.Collections.Generic.List<string>();
                        foreach (var p in allProps)
                        {
                            try
                            {
                                var val = p.GetValue(item);
                                propStrings.Add($"{p.Name}={val}");
                            }
                            catch
                            {
                                propStrings.Add($"{p.Name}=<error>");
                            }
                        }
                        Modules.PrivateMessageModule.LogDebug($"[ITEM QL DEBUG] Properties: {string.Join(", ", propStrings)}");
                    }
                }
                catch { }

                foreach (var propName in qualityCandidates)
                {
                    // Try property first
                    try
                    {
                        var property = item.GetType().GetProperty(propName);
                        if (property != null)
                        {
                            var value = property.GetValue(item);
                            if (value != null && int.TryParse(value.ToString(), out int ql))
                            {
                                if (ql > 0 && qualityLevel == 0) // Use first valid quality level > 0
                                {
                                    qualityLevel = ql;
                                    if (debugItem)
                                    {
                                        Modules.PrivateMessageModule.LogDebug($"[ITEM QL DEBUG] Found QL via property '{propName}': {ql}");
                                    }
                                }
                            }
                        }
                    }
                    catch { }

                    // Try field
                    try
                    {
                        var field = item.GetType().GetField(propName);
                        if (field != null)
                        {
                            var value = field.GetValue(item);
                            if (value != null && int.TryParse(value.ToString(), out int ql))
                            {
                                if (ql > 0 && qualityLevel == 0) // Use first valid quality level > 0
                                {
                                    qualityLevel = ql;
                                    if (debugItem)
                                    {
                                        Modules.PrivateMessageModule.LogDebug($"[ITEM QL DEBUG] Found QL via field '{propName}': {ql}");
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }

                if (debugItem && qualityLevel == 0)
                {
                    Modules.PrivateMessageModule.LogDebug($"[ITEM QL DEBUG] No QL found for {itemName}");
                }

                return (stackCount, qualityLevel);
            }
            catch (Exception ex)
            {
                // Return defaults on any error
                Modules.PrivateMessageModule.LogDebug($"[ITEM QL DEBUG] Error extracting properties: {ex.Message}");
                return (1, 0);
            }
        }

        /// <summary>
        /// Log a transaction to the log file
        /// </summary>
        public static void LogTransaction(string playerName, string itemName)
        {
            if (!_initialized) Initialize();

            try
            {
                string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {playerName} - {itemName}\n";
                File.AppendAllText(_logFilePath, logEntry);
            }
            catch (Exception)
            {
                // Silent error handling for logging
            }
        }

        /// <summary>
        /// Save stored items to file
        /// </summary>
        private static void SaveStoredItems()
        {
            try
            {
                string storageFile = Path.Combine(Craftbot.PluginDir, "bin", "Debug", "stored_items.json");
                // File.WriteAllText(storageFile, JsonConvert.SerializeObject(_storedItems, Formatting.Indented));
                File.WriteAllText(storageFile, "{}"); // Simplified for now
            }
            catch (Exception)
            {
                // Silent error handling
            }
        }

        /// <summary>
        /// Load stored items from file
        /// </summary>
        private static void LoadStoredItems()
        {
            try
            {
                string storageFile = Path.Combine(Craftbot.PluginDir, "bin", "Debug", "stored_items.json");
                if (File.Exists(storageFile))
                {
                    // var loaded = JsonConvert.DeserializeObject<Dictionary<string, StoredItem>>(File.ReadAllText(storageFile));
                    var loaded = new Dictionary<string, StoredItem>(); // Simplified for now
                    if (loaded != null)
                    {
                        _storedItems = loaded;
                    }
                }
            }
            catch (Exception)
            {
                // Silent error handling - start with empty storage
                _storedItems = new Dictionary<string, StoredItem>();
            }
        }

        /// <summary>
        /// Check if an item is a container/bag
        /// </summary>
        private static bool IsContainer(StoredItem item)
        {
            // Simple heuristic - bags typically have "bag", "backpack", "container" in name
            string name = item.Name.ToLower();
            return name.Contains("bag") || name.Contains("backpack") || name.Contains("container") ||
                   name.Contains("pouch") || name.Contains("satchel");
        }

        /// <summary>
        /// Track a tool that was accidentally provided by a player (not required by any recipe)
        /// </summary>
        public static void TrackAccidentalPlayerTool(Item item, int playerId, string originalBag = null)
        {
            if (item?.UniqueIdentity == null) return;

            var toolInfo = new AccidentalToolInfo(
                item.Id,
                item.UniqueIdentity.Instance,
                item.Name ?? "Unknown Tool",
                playerId,
                originalBag
            );

            _accidentalPlayerTools[item.Id] = toolInfo;
            LogTransaction("SYSTEM", $"[ACCIDENTAL TOOL] Tracked player tool: {item.Name} (ID:{item.Id}, Instance:{item.UniqueIdentity.Instance}) from player {playerId}");
        }

        /// <summary>
        /// Check if an item is an accidentally provided player tool
        /// </summary>
        public static bool IsAccidentalPlayerTool(Item item)
        {
            if (item?.UniqueIdentity == null) return false;

            if (_accidentalPlayerTools.TryGetValue(item.Id, out var toolInfo))
            {
                // Verify both ID and instance match for extra safety
                bool isMatch = toolInfo.UniqueInstance == item.UniqueIdentity.Instance;
                if (isMatch)
                {
                    LogTransaction("SYSTEM", $"[ACCIDENTAL TOOL] Confirmed accidental player tool: {item.Name} (ID:{item.Id}, Instance:{item.UniqueIdentity.Instance})");
                }
                return isMatch;
            }

            return false;
        }

        /// <summary>
        /// Get information about an accidental player tool
        /// </summary>
        public static AccidentalToolInfo GetAccidentalToolInfo(Item item)
        {
            if (item?.UniqueIdentity == null) return null;

            if (_accidentalPlayerTools.TryGetValue(item.Id, out var toolInfo))
            {
                if (toolInfo.UniqueInstance == item.UniqueIdentity.Instance)
                {
                    return toolInfo;
                }
            }

            return null;
        }

        /// <summary>
        /// Remove tracking for an accidental player tool (when it's returned to player)
        /// </summary>
        public static void RemoveAccidentalPlayerTool(Item item)
        {
            if (item?.UniqueIdentity == null) return;

            if (_accidentalPlayerTools.Remove(item.Id))
            {
                LogTransaction("SYSTEM", $"[ACCIDENTAL TOOL] Removed tracking for returned tool: {item.Name} (ID:{item.Id})");
            }
        }

        /// <summary>
        /// Clear all accidental tool tracking for a specific player (when trade completes)
        /// </summary>
        public static void ClearAccidentalToolsForPlayer(int playerId)
        {
            var toRemove = _accidentalPlayerTools.Where(kvp => kvp.Value.PlayerId == playerId).ToList();
            foreach (var kvp in toRemove)
            {
                _accidentalPlayerTools.Remove(kvp.Key);
                LogTransaction("SYSTEM", $"[ACCIDENTAL TOOL] Cleared tracking for player {playerId} tool: {kvp.Value.ItemName}");
            }
        }

        /// <summary>
        /// Check if a tool is required by any recipe for the given items
        /// </summary>
        public static bool IsToolRequiredByRecipes(Item tool, List<Item> playerItems)
        {
            if (tool?.Name == null) return false;

            // List of tools that recipes commonly require players to provide
            var commonRecipeTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Bio Analyzing Computer",
                "MasterComm - Personalization Device",
                "Screwdriver",
                "Plasma Processing Tool",
                "Gem Cutter",
                "Hacker Tool",
                "Engineering Device",
                "Conversion Kit",
                "Refiner",
                "Nano Programming Interface"
            };

            // Check if this tool matches any common recipe tool
            foreach (var recipeToolName in commonRecipeTools)
            {
                if (tool.Name.Contains(recipeToolName))
                {
                    LogTransaction("SYSTEM", $"[TOOL CHECK] {tool.Name} is a recognized recipe tool");
                    return true;
                }
            }

            // Additional check: if the tool name contains certain keywords that indicate it's a crafting tool
            var toolKeywords = new[] { "comminutor", "analyzer", "processor", "cutter", "device", "kit", "refiner" };
            if (toolKeywords.Any(keyword => tool.Name.ToLower().Contains(keyword)))
            {
                LogTransaction("SYSTEM", $"[TOOL CHECK] {tool.Name} contains crafting tool keywords - likely required");
                return true;
            }

            LogTransaction("SYSTEM", $"[TOOL CHECK] {tool.Name} does not appear to be a required recipe tool");
            return false;
        }

        /// <summary>
        /// Scan player items and detect any tools that aren't required by recipes
        /// </summary>
        public static void ScanForAccidentalTools(List<Item> playerItems, int playerId)
        {
            if (playerItems == null || !playerItems.Any()) return;

            // Get non-tool items (materials that would be processed)
            var materialItems = playerItems.Where(item => !IsLikelyTool(item)).ToList();

            // Check each item to see if it's an accidental tool
            foreach (var item in playerItems)
            {
                if (IsLikelyTool(item) && !IsToolRequiredByRecipes(item, materialItems))
                {
                    // This appears to be a tool that isn't required by recipes
                    TrackAccidentalPlayerTool(item, playerId);
                    LogTransaction("SYSTEM", $"[ACCIDENTAL TOOL] Detected unnecessary tool from player {playerId}: {item.Name}");
                }
            }
        }

        /// <summary>
        /// Check if an item is likely a tool based on its name and characteristics
        /// </summary>
        private static bool IsLikelyTool(Item item)
        {
            if (item?.Name == null) return false;

            // EXCLUSIONS: Items that are NOT tools even if they match patterns
            var itemNameLower = item.Name.ToLower();
            if (itemNameLower.Contains("monster parts") ||
                itemNameLower.Contains("pelted monster parts"))
            {
                return false; // Monster parts are processable items, NOT tools
            }

            // CRITICAL FIX: "Hacker ICE-Breaker Source" is a processable item for Ice recipe, NOT a tool
            if (itemNameLower.Contains("hacker ice-breaker source") || itemNameLower.Contains("hacker ice breaker source"))
            {
                return false; // This is a processable item for Ice recipe, NOT a tool
            }

            var toolIndicators = new[]
            {
                "comminutor", "analyzer", "processor", "cutter", "device", "kit", "refiner",
                "screwdriver", "hacker", "engineering", "bio analyzing", "mastercomm",
                "plasma processing", "gem cutter", "conversion", "nanodeck", "interface"
            };

            return toolIndicators.Any(indicator => item.Name.ToLower().Contains(indicator));
        }
    }

    /// <summary>
    /// Represents a stored item in the bank (legacy)
    /// </summary>
    public class StoredItem
    {
        public uint Id { get; set; }
        public string Name { get; set; }
        public int Quality { get; set; }
        public int Quantity { get; set; }
        public string StoredBy { get; set; }
        public DateTime StoredAt { get; set; }
        public uint ItemInstance { get; set; }

        public string GetItemKey()
        {
            // Use ID and ItemInstance only for reliable key generation
            return $"{Id}_{ItemInstance}";
        }
    }

    /// <summary>
    /// Represents a tracked item in the comprehensive inventory system
    /// </summary>
    public class TrackedItem
    {
        public uint Id { get; set; }
        public string Name { get; set; }
        public int Quality { get; set; }
        public int Quantity { get; set; }
        public uint ItemInstance { get; set; }
        public IdentityType SlotType { get; set; }
        public int SlotInstance { get; set; }
        public string Source { get; set; } // Who provided this item or what created it
        public DateTime TrackedAt { get; set; }

        public TrackedItem() { }

        public TrackedItem(Item item, string source = "BOT")
        {
            Id = (uint)item.Id;
            Name = item.Name ?? $"Item_{item.Id}"; // Handle null names
            Quality = item.Ql; // Use Ql instead of Quality
            Quantity = 1; // Items don't have quantity in clientless, default to 1
            ItemInstance = (uint)item.UniqueIdentity.Instance;
            SlotType = item.Slot.Type;
            SlotInstance = item.Slot.Instance;
            Source = source;
            TrackedAt = DateTime.Now;
        }

        public string GetKey()
        {
            // Use ID and ItemInstance only for reliable key generation
            // Don't use Name as it can be inconsistent in clientless API
            return $"{Id}_{ItemInstance}";
        }

        public override string ToString()
        {
            return $"{Name} (ID:{Id}, Q:{Quality}, Source:{Source})";
        }
    }
}
