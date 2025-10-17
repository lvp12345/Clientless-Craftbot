using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AOSharp.Clientless;
using SmokeLounge.AOtomation.Messaging.GameData;
using AOSharp.Common.GameData;

namespace Craftbot.Recipes
{
    /// <summary>
    /// Shared utility methods for all recipe processors
    /// </summary>
    public static class RecipeUtilities
    {
        // Track which bag tools came from so we can return them
        // Use unique item keys instead of just names to handle duplicate tool names
        private static Dictionary<string, string> _toolOriginBags = new Dictionary<string, string>();

        /// <summary>
        /// Finds and pulls a tool from bags to inventory with robust error handling
        /// </summary>
        /// <param name="toolName">Name of the tool to find</param>
        /// <returns>True if tool was found and successfully moved to inventory</returns>
        public static bool FindAndPullTool(string toolName)
        {
            try
            {
                // First check already open containers
                foreach (var container in Inventory.Containers.Where(bp => bp.IsOpen))
                {
                    var tool = container.Items.FirstOrDefault(item => item.Name.Contains(toolName));
                    if (tool != null)
                    {
                        LogDebug($"[TOOL SEARCH] Found {toolName} in open bag {container.Item?.Name ?? "Unknown"}, moving to inventory");

                        // Track where this tool came from so we can return it later
                        // Use unique item key to handle duplicate tool names
                        string toolKey = $"{tool.Name}_{tool.Id}_{tool.UniqueIdentity.Instance}";
                        _toolOriginBags[toolKey] = container.Item?.Name ?? "Unknown";
                        LogDebug($"[TOOL TRACKING] Tracking tool: {toolKey} from bag: {container.Item?.Name ?? "Unknown"}");

                        // Attempt to move tool with retry logic
                        if (MoveToolToInventoryWithRetry(tool, toolName))
                        {
                            return true;
                        }
                        else
                        {
                            LogDebug($"[TOOL SEARCH] ❌ Failed to move {toolName} to inventory after retries");
                            // Remove from tracking since move failed
                            _toolOriginBags.Remove(toolKey);
                        }
                    }
                }

                // If not found in open bags, try to open tool bags in inventory
                LogDebug($"[TOOL SEARCH] {toolName} not found in open bags, checking for tool bags to open");
                var toolBags = Inventory.Items.Where(item =>
                    item.UniqueIdentity.Type == IdentityType.Container &&
                    (item.Name.ToLower().Contains("tool") ||
                     item.Name.ToLower().Contains("bag") ||
                     item.Name.ToLower().Contains("backpack")) &&
                    // Only open bags that aren't already open (to avoid closing player bags)
                    !Inventory.Containers.Any(bp => bp.Identity.Instance == item.UniqueIdentity.Instance)).ToList();

                foreach (var bagItem in toolBags)
                {
                    LogDebug($"[TOOL SEARCH] Opening potential tool bag: {bagItem.Name}");
                    bagItem.Use(); // Open the bag
                    Task.Delay(100).Wait(); // Wait for bag to open

                    // Check if this newly opened bag contains the tool
                    var newContainer = Inventory.Containers.FirstOrDefault(bp =>
                        bp.Identity.Instance == bagItem.UniqueIdentity.Instance);

                    if (newContainer != null)
                    {
                        var tool = newContainer.Items.FirstOrDefault(item => item.Name.Contains(toolName));
                        if (tool != null)
                        {
                            LogDebug($"[TOOL SEARCH] Found {toolName} in newly opened bag {newContainer.Item?.Name ?? "Unknown"}, moving to inventory");

                            // Track where this tool came from so we can return it later
                            // Use unique item key to handle duplicate tool names
                            string toolKey = $"{tool.Name}_{tool.Id}_{tool.UniqueIdentity.Instance}";
                            _toolOriginBags[toolKey] = newContainer.Item?.Name ?? "Unknown";
                            LogDebug($"[TOOL TRACKING] Tracking tool: {toolKey} from bag: {newContainer.Item?.Name ?? "Unknown"}");

                            // Attempt to move tool with retry logic
                            if (MoveToolToInventoryWithRetry(tool, toolName))
                            {
                                return true;
                            }
                            else
                            {
                                LogDebug($"[TOOL SEARCH] ❌ Failed to move {toolName} to inventory after retries");
                                // Remove from tracking since move failed
                                _toolOriginBags.Remove(toolKey);
                            }
                        }
                    }
                }

                LogDebug($"[TOOL SEARCH] {toolName} not found in any bags (checked {toolBags.Count} potential tool bags)");
                return false;
            }
            catch (Exception ex)
            {
                LogDebug($"[TOOL SEARCH] Error finding {toolName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Returns tools to their original bags after processing
        /// CRITICAL FIX: Bot tools go to bot tool bags, player tools stay in inventory for return to player
        /// </summary>
        public static async Task ReturnToolsToOriginalBags()
        {
            try
            {
                foreach (var toolEntry in _toolOriginBags.ToList())
                {
                    string toolKey = toolEntry.Key;
                    string originBagName = toolEntry.Value;

                    // Parse the tool key to get the unique identifiers
                    var keyParts = toolKey.Split('_');
                    if (keyParts.Length < 3)
                    {
                        LogDebug($"[TOOL RETURN] Invalid tool key format: {toolKey}");
                        _toolOriginBags.Remove(toolKey);
                        continue;
                    }

                    string toolName = keyParts[0];
                    if (!int.TryParse(keyParts[1], out int toolId) || !int.TryParse(keyParts[2], out int toolInstance))
                    {
                        LogDebug($"[TOOL RETURN] Could not parse tool ID/Instance from key: {toolKey}");
                        _toolOriginBags.Remove(toolKey);
                        continue;
                    }

                    // Find the specific tool in inventory using unique identifiers
                    var tool = Inventory.Items.FirstOrDefault(item =>
                        item.Name == toolName &&
                        item.Id == toolId &&
                        item.UniqueIdentity.Instance == toolInstance);

                    if (tool != null)
                    {
                        // CRITICAL FIX: Check if this is a bot tool or player-provided tool
                        bool isBotTool = Core.ItemTracker.IsBotTool(tool);
                        bool isFromBotToolBag = originBagName.ToLower().StartsWith("tools") || originBagName.ToLower().Contains("tool bag");

                        if (isBotTool || isFromBotToolBag)
                        {
                            // This is a bot tool - return it to its EXACT original bag
                            var originalBag = Inventory.Containers.FirstOrDefault(bp =>
                                bp.Item?.Name?.Equals(originBagName, StringComparison.OrdinalIgnoreCase) == true);

                            if (originalBag != null)
                            {
                                LogDebug($"[TOOL RETURN] Returning bot tool {toolName} (ID:{toolId}, Instance:{toolInstance}) to original bag {originalBag.Item?.Name}");
                                tool.MoveToContainer(originalBag);
                                await Task.Delay(100);
                            }
                            else
                            {
                                // Fallback: try any bot tool bag if original not found
                                var anyBotToolBag = Inventory.Containers.FirstOrDefault(bp =>
                                    bp.Item?.Name?.ToLower().StartsWith("tools") == true);

                                if (anyBotToolBag != null)
                                {
                                    LogDebug($"[TOOL RETURN] Original bag '{originBagName}' not found, using fallback bag {anyBotToolBag.Item?.Name} for {toolName}");
                                    tool.MoveToContainer(anyBotToolBag);
                                    await Task.Delay(100);
                                }
                                else
                                {
                                    LogDebug($"[TOOL RETURN] No bot tool bag found for {toolName} - keeping in inventory");
                                }
                            }
                        }
                        else
                        {
                            // This is a player-provided tool - leave it in inventory to be returned to player
                            LogDebug($"[TOOL RETURN] Player-provided tool {toolName} from bag {originBagName} - leaving in inventory for return to player");
                        }
                    }
                    else
                    {
                        LogDebug($"[TOOL RETURN] Tool not found in inventory: {toolName} (ID:{toolId}, Instance:{toolInstance})");
                    }

                    // Remove from tracking
                    _toolOriginBags.Remove(toolKey);
                }

                // Clear all tool tracking after returning tools
                _toolOriginBags.Clear();
                LogDebug($"[TOOL RETURN] ✅ All tools returned to bags and tracking cleared");
            }
            catch (Exception ex)
            {
                LogDebug($"[TOOL RETURN] Error returning tools to original bags: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes temporary data receptacle items from inventory
        /// </summary>
        public static void DeleteTemporaryDataReceptacle()
        {
            try
            {
                var tempItems = Inventory.Items.Where(item => 
                    item.Name.Contains("Temporary: Data Receptacle")).ToList();
                
                foreach (var item in tempItems)
                {
                    LogDebug($"[CLEANUP] Deleting {item.Name}");
                    item.Delete();
                }
            }
            catch (Exception ex)
            {
                LogDebug($"[CLEANUP] Error deleting temporary items: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if an item is a processing tool that should never leave inventory
        /// RULE #5: TOOLS MUST NEVER UNDER ANY CIRCUMSTANCE BE GIVEN TO PLAYERS
        /// </summary>
        /// <param name="item">Item to check</param>
        /// <returns>True if item is a processing tool</returns>
        public static bool IsProcessingTool(Item item)
        {
            // COMPREHENSIVE PROTECTION: ALL tools used in ANY recipe MUST be protected

            // EXACT TOOL NAMES - Every tool used in every recipe
            string[] exactToolNames = {
                // Pearl/Gem Processing
                "Jensen Gem Cutter",
                "Jensen Personal Ore Extractor",  // CRITICAL: Bot's personal ore extractor - NEVER give to players

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
                "tool", "cutter", "interface", "reclaimer",
                "robot", "furnace", "machine", "screwdriver",
                "hsr - sketch and etch", "clanalizer", "omnifier",
                "bio-comminutor", "programming", "disassembly", "clinic",
                "structural analyzer"
            };

            // SPECIAL ROBOT PATTERN - More specific to avoid false positives with robot brain results
            string[] robotToolPatterns = {
                "mass relocating robot", "robot junk", "robot memory"
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
                return false; // PB recipe novictum components are processable items, NOT tools
            }

            // CRITICAL FIX: Exclude recipe results FIRST before checking tool patterns
            // This prevents robot brain results from being incorrectly marked as tools
            if (itemNameLower.Contains("robot brain") ||
                itemNameLower.Contains("nano sensor") ||
                itemNameLower.Contains("personalized basic robot brain"))
            {
                return false; // These are recipe results, not tools
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

            // CHECK ROBOT TOOL PATTERNS (more specific to avoid false positives)
            foreach (var robotPattern in robotToolPatterns)
            {
                if (itemNameLower.Contains(robotPattern))
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

            return false;
        }

        /// <summary>
        /// Checks if an item is a processed result that should remain loose for return to players
        /// These are items created as a result of recipe processing, not original items from bags
        /// </summary>
        /// <param name="item">Item to check</param>
        /// <returns>True if item is a processed result that should remain loose</returns>
        public static bool IsProcessedResult(Item item)
        {
            if (item == null) return false;

            string itemName = item.Name;
            if (string.IsNullOrEmpty(itemName)) return false; // CLIENTLESS FIX: Handle null/empty names

            string itemNameLower = itemName.ToLower();

            // PLASMA PROCESSING RESULTS
            if (itemName.Contains("Blood Plasma"))
                return true;

            // ROBOT BRAIN PROCESSING RESULTS
            if (itemName.Contains("Personalized Basic Robot Brain") ||
                itemName.Contains("Basic Robot Brain") ||
                itemName.Contains("Nano Sensor"))
                return true;

            // SMELTING PROCESSING RESULTS - Be more specific to avoid false positives
            if (itemNameLower.Contains("gold ingot") || itemNameLower.Contains("silver ingot"))
                return true;
            if (itemNameLower.Contains("precious metal"))
                return true;

            // ICE PROCESSING RESULTS
            if (itemName.Contains("Upgraded Controller Recompiler Unit"))
                return true;

            // PREDATOR'S CIRCLET PROCESSING RESULTS
            if (itemName.Contains("Predator's Circlet"))
                return true;

            // VTE PROCESSING RESULTS
            if (itemName.Contains("Liquid Gold"))
                return true;
            if (itemName.Contains("Gold Filigree Wire"))
                return true;
            if (itemName.Contains("Perfectly Cut Soul Fragment"))
                return true;
            if (itemName.Contains("Interfaced Nano Sensor"))
                return true;

            // ROBOT BRAIN PROCESSING RESULTS (Multi-step recipe)
            if (itemName.Contains("Personalized Basic Robot Brain"))
                return true;
            if (itemName.Contains("Basic Robot Brain") && !itemName.Contains("Personalized"))
                return true;
            // NOTE: "Nano Sensor" can be both input and output, treating as input for now

            // TRIMMER PROCESSING RESULTS
            if (itemName.Contains("Trimmer") && !itemName.Contains("Casing")) // Results but not raw materials
                return true;

            // CLUMPS PROCESSING RESULTS - Items created from clump processing
            if (itemNameLower.Contains("kyr'ozch") && !itemNameLower.Contains("bio-material") && !itemNameLower.Contains("analyzer"))
                return true;

            // PB PATTERN PROCESSING RESULTS - Completed patterns
            if (itemNameLower.Contains("pattern") && itemNameLower.Contains("completed"))
                return true;

            // PEARL/GEM PROCESSING RESULTS - Jensen Gem Cutter results
            if (itemName.Contains("Perfectly Cut"))
                return true;

            // TREATMENT LIBRARY PROCESSING RESULTS
            if (itemName.Contains("Treatment Library") || itemName.Contains("Treatment and Pharmacy Library"))
                return true;

            // MANTIS ARMOR PROCESSING RESULTS
            if (itemName.Contains("Mantis") || itemName.Contains("Mantidae"))
                return true;

            // TARA ARMOR PROCESSING RESULTS - Dragon armor pieces
            if (itemName.Contains("Tara") ||
                (itemName.Contains("Dragon") && (itemName.Contains("Armor") || itemName.Contains("Sleeves") || itemName.Contains("Gloves") || itemName.Contains("Boots") || itemName.Contains("Helmet") || itemName.Contains("Pants") || itemName.Contains("Circlet"))))
                return true;

            // CRAWLER ARMOR PROCESSING RESULTS
            if (itemName.Contains("Crawler"))
                return true;

            // DE'VALOS SLEEVE PROCESSING RESULTS
            if (itemName.Contains("De'Valos Sleeves") || itemName.Contains("Devalos Sleeves"))
                return true;

            return false;
        }

        /// <summary>
        /// Moves processed items back to the target container, excluding processing tools, containers, and processed results
        /// CRITICAL FIX: Exclude processed results (like Blood Plasma) from being moved back - they should remain loose for return trade
        /// </summary>
        /// <param name="targetContainer">Container to move items to</param>
        /// <param name="processType">Type of processing for logging</param>
        public static async Task MoveProcessedItemsBackToContainer(Container targetContainer, string processType)
        {
            try
            {
                LogDebug($"[MOVE BACK] Moving {processType} processed items back to container");

                // CRITICAL DEBUG: First log ALL items in inventory to see what we have
                LogDebug($"[MOVE BACK DEBUG] ALL INVENTORY ITEMS ({Inventory.Items.Count()}):");
                foreach (var invItem in Inventory.Items)
                {
                    LogDebug($"[MOVE BACK DEBUG] Inventory item: {invItem.Name} (Slot: {invItem.Slot.Type}, Instance: {invItem.Slot.Instance})");
                }

                // CRITICAL FIX: Return ALL items to player - bot must never keep anything
                var itemsToMove = Inventory.Items.Where(item =>
                    !(item.Slot.Instance >= (int)EquipSlot.Weap_Hud1 && item.Slot.Instance <= (int)EquipSlot.Imp_Feet) && // NEVER move equipped items
                    item.Slot.Type == IdentityType.Inventory && // ONLY items in inventory slots, NOT equipped
                    !IsProcessingTool(item) &&
                    !item.Name.Contains("Backpack") && // NEVER move containers into other containers
                    !item.Name.Contains("Novictum Ring") && // Exclude equipped novictum rings
                    !item.Name.Contains("Pure Novictum Ring") && // Exclude equipped pure novictum rings
                    !item.Name.Contains("Temporary: Data Receptacle")).ToList(); // INCLUDE ALL ITEMS - even processed results

                // ACCIDENTAL TOOL PROTECTION: Always include accidental player tools in return items
                var accidentalTools = Inventory.Items.Where(item =>
                    item.Slot.Type == IdentityType.Inventory &&
                    Core.ItemTracker.IsAccidentalPlayerTool(item)).ToList();

                if (accidentalTools.Any())
                {
                    LogDebug($"[ACCIDENTAL TOOL] Found {accidentalTools.Count} accidental player tools to return");
                    foreach (var tool in accidentalTools)
                    {
                        if (!itemsToMove.Contains(tool))
                        {
                            itemsToMove.Add(tool);
                            LogDebug($"[ACCIDENTAL TOOL] Added accidental tool to return list: {tool.Name}");
                        }
                    }
                }

                // CRITICAL DEBUG: Log all items being considered for return
                LogDebug($"[MOVE BACK DEBUG] Found {itemsToMove.Count} total items to consider for return");
                foreach (var item in itemsToMove)
                {
                    LogDebug($"[MOVE BACK DEBUG] Considering item: {item.Name}");
                }

                // CRITICAL DEBUG: Check specifically for robot brain results
                var robotBrainResults = Inventory.Items.Where(item =>
                    item.Name.Contains("Personalized Basic Robot Brain") ||
                    item.Name.Contains("Basic Robot Brain") ||
                    item.Name.Contains("Nano Sensor")).ToList();
                LogDebug($"[MOVE BACK DEBUG] Found {robotBrainResults.Count} robot brain results in inventory:");
                foreach (var result in robotBrainResults)
                {
                    LogDebug($"[MOVE BACK DEBUG] Robot brain result: {result.Name} (Slot: {result.Slot.Type}, Instance: {result.Slot.Instance}, IsProcessingTool: {IsProcessingTool(result)})");
                }

                var processedResults = new List<Item>();
                var unprocessedItems = new List<Item>();

                // Separate processed results from unprocessed items for logging
                foreach (var item in itemsToMove)
                {
                    if (IsProcessedResult(item))
                    {
                        processedResults.Add(item);
                    }
                    else
                    {
                        unprocessedItems.Add(item);
                    }
                }

                LogDebug($"[MOVE BACK] Found {unprocessedItems.Count} unprocessed items and {processedResults.Count} processed results to return");

                // CRITICAL FAILSAFE: If no items found but processing was attempted, log warning
                if (itemsToMove.Count == 0)
                {
                    LogDebug($"[MOVE BACK] ⚠️ WARNING: No items found to return for {processType} - this may indicate processing failure");
                    LogDebug($"[MOVE BACK] ⚠️ Checking for any remaining player items in inventory...");

                    // Look for any items that might be player items (not bot tools)
                    // CRITICAL: Include ALL non-bot items to prevent item loss
                    var potentialPlayerItems = Inventory.Items.Where(invItem =>
                        !IsProcessingTool(invItem) &&
                        invItem.Slot.Type == IdentityType.Inventory &&
                        !Core.ItemTracker.IsBotPersonalItem(invItem) &&
                        !Core.ItemTracker.IsBotTool(invItem) &&
                        // Include common recipe materials that might be stuck
                        (invItem.Name.Contains("Bio Analyzing Computer") ||
                         invItem.Name.Contains("MasterComm") ||
                         invItem.Name.Contains("Nano Sensor") ||
                         invItem.Name.Contains("Robot Junk") ||
                         invItem.Name.Contains("Basic Robot Brain") ||
                         // Generic check for any non-bot item
                         !invItem.Name.Contains("Screwdriver"))).ToList();

                    if (potentialPlayerItems.Any())
                    {
                        LogDebug($"[MOVE BACK] ⚠️ Found {potentialPlayerItems.Count} potential player items still in inventory:");
                        foreach (var item in potentialPlayerItems)
                        {
                            LogDebug($"[MOVE BACK] ⚠️ Potential player item: {item.Name}");
                            itemsToMove.Add(item); // Add to return list
                        }

                        // Recalculate processed vs unprocessed after adding potential items
                        processedResults.Clear();
                        unprocessedItems.Clear();
                        foreach (var item in itemsToMove)
                        {
                            if (IsProcessedResult(item))
                            {
                                processedResults.Add(item);
                            }
                            else
                            {
                                unprocessedItems.Add(item);
                            }
                        }
                        LogDebug($"[MOVE BACK] After failsafe: {unprocessedItems.Count} unprocessed items and {processedResults.Count} processed results to return");
                    }
                }

                // IMPROVED BAG RETURN: Handle space issues more intelligently
                var temporarilyMovedBags = new List<Item>();

                foreach (var item in itemsToMove)
                {
                    bool itemMoved = false;
                    int attempts = 0;
                    const int maxAttempts = 3;

                    while (!itemMoved && attempts < maxAttempts)
                    {
                        attempts++;

                        // Check if bag has space
                        if (targetContainer.Items.Count() < 21)
                        {
                            string itemType = IsProcessedResult(item) ? "PROCESSED RESULT" : "unprocessed item";
                            LogDebug($"[MOVE BACK] Moving {itemType}: {item.Name} back to bag (attempt {attempts})");

                            try
                            {
                                item.MoveToContainer(targetContainer);
                                await Task.Delay(100);
                                itemMoved = true;

                                // ACCIDENTAL TOOL CLEANUP: Remove tracking for returned accidental tools
                                if (Core.ItemTracker.IsAccidentalPlayerTool(item))
                                {
                                    Core.ItemTracker.RemoveAccidentalPlayerTool(item);
                                    LogDebug($"[ACCIDENTAL TOOL] Removed tracking for returned tool: {item.Name}");
                                }
                            }
                            catch (Exception ex)
                            {
                                LogDebug($"[MOVE BACK] ⚠️ Failed to move {item.Name} to bag: {ex.Message}");
                            }
                        }
                        else
                        {
                            // Bag is full - need to make space
                            LogDebug($"[MOVE BACK] ⚠️ Bag is full ({targetContainer.Items.Count()}/21) - making space for {item.Name}");

                            // Find the least important item to temporarily move out
                            var itemToMoveOut = targetContainer.Items
                                .Where(bagItem => !itemsToMove.Contains(bagItem)) // Don't move items we're trying to put back
                                .Where(bagItem => bagItem.Name.Contains("Backpack") || bagItem.Name.Contains("Bag")) // Prefer moving bags
                                .FirstOrDefault();

                            if (itemToMoveOut == null)
                            {
                                // No bags to move, try any non-essential item
                                itemToMoveOut = targetContainer.Items
                                    .Where(bagItem => !itemsToMove.Contains(bagItem))
                                    .FirstOrDefault();
                            }

                            if (itemToMoveOut != null)
                            {
                                LogDebug($"[MOVE BACK] Temporarily moving {itemToMoveOut.Name} to inventory to make space");
                                try
                                {
                                    itemToMoveOut.MoveToInventory();
                                    temporarilyMovedBags.Add(itemToMoveOut);
                                    await Task.Delay(100);
                                }
                                catch (Exception ex)
                                {
                                    LogDebug($"[MOVE BACK] ⚠️ Failed to move {itemToMoveOut.Name} out of bag: {ex.Message}");
                                    break; // Can't make space, abort this item
                                }
                            }
                            else
                            {
                                LogDebug($"[MOVE BACK] ❌ Cannot make space in bag - no items available to move out");
                                break; // Can't make space, abort this item
                            }
                        }
                    }

                    if (!itemMoved)
                    {
                        LogDebug($"[MOVE BACK] ❌ FAILED: Could not return {item.Name} to bag after {maxAttempts} attempts");
                        // Item remains in inventory - will be handled by failsafe system
                    }
                }

                // Try to return temporarily moved items back to the bag
                foreach (var tempItem in temporarilyMovedBags)
                {
                    if (targetContainer.Items.Count() < 21)
                    {
                        LogDebug($"[MOVE BACK] Returning temporarily moved item: {tempItem.Name}");
                        try
                        {
                            tempItem.MoveToContainer(targetContainer);
                            await Task.Delay(100);
                        }
                        catch (Exception ex)
                        {
                            LogDebug($"[MOVE BACK] ⚠️ Could not return temporarily moved item {tempItem.Name}: {ex.Message}");
                            // Item remains in inventory - will be handled by failsafe
                        }
                    }
                    else
                    {
                        LogDebug($"[MOVE BACK] ⚠️ Bag still full - {tempItem.Name} remains in inventory");
                        // Item remains in inventory - will be handled by failsafe
                    }
                }

                // Log summary of what was returned
                if (processedResults.Any())
                {
                    LogDebug($"[MOVE BACK] ✅ Returned {processedResults.Count} processed results to player:");
                    foreach (var result in processedResults)
                    {
                        LogDebug($"[MOVE BACK] ✅ Returned processed result: {result.Name}");
                    }
                }

                LogDebug($"[MOVE BACK] Finished moving {processType} items back to container - ALL items returned to player");
            }
            catch (Exception ex)
            {
                LogDebug($"[MOVE BACK] Error moving {processType} items: {ex.Message}");
            }
        }

        /// <summary>
        /// Logs debug messages to file only (NO in-game chat)
        /// </summary>
        /// <param name="message">Message to log</param>
        public static void LogDebug(string message)
        {
            // Use the same logging system as PrivateMessageModule
            try
            {
                Modules.PrivateMessageModule.LogDebug($"[RECIPE] {message}");
            }
            catch (Exception ex)
            {
                // If RecipeUtilities.LogDebug fails, try to log the error via PrivateMessageModule directly
                try
                {
                    Modules.PrivateMessageModule.LogError($"[RECIPE ERROR] Failed to log: {message}, Error: {ex.Message}");
                }
                catch
                {
                    // Complete failure - silently fail
                }
            }
        }

        /// <summary>
        /// Logs informational messages to file only (NO in-game chat)
        /// </summary>
        /// <param name="message">Message to log</param>
        public static void LogInfo(string message)
        {
            try
            {
                Modules.PrivateMessageModule.LogInfo($"[RECIPE] {message}");
            }
            catch (Exception ex)
            {
                try
                {
                    Modules.PrivateMessageModule.LogError($"[RECIPE ERROR] Failed to log info: {message}, Error: {ex.Message}");
                }
                catch
                {
                    // Complete failure - silently fail
                }
            }
        }

        /// <summary>
        /// Logs warning messages to file only (NO in-game chat)
        /// </summary>
        /// <param name="message">Message to log</param>
        public static void LogWarning(string message)
        {
            try
            {
                Modules.PrivateMessageModule.LogWarning($"[RECIPE] {message}");
            }
            catch (Exception ex)
            {
                try
                {
                    Modules.PrivateMessageModule.LogError($"[RECIPE ERROR] Failed to log warning: {message}, Error: {ex.Message}");
                }
                catch
                {
                    // Complete failure - silently fail
                }
            }
        }

        /// <summary>
        /// Logs error messages to file only (NO in-game chat)
        /// </summary>
        /// <param name="message">Message to log</param>
        public static void LogError(string message)
        {
            try
            {
                Modules.PrivateMessageModule.LogError($"[RECIPE] {message}");
            }
            catch
            {
                // Complete failure - silently fail
            }
        }

        /// <summary>
        /// Logs critical messages to file only (NO in-game chat)
        /// </summary>
        /// <param name="message">Message to log</param>
        public static void LogCritical(string message)
        {
            // Log to file only - NO in-game chat spam
            try
            {
                string logDir = Path.Combine(Environment.CurrentDirectory, "Control Panel", "logs");
                Directory.CreateDirectory(logDir);
                string logPath = Path.Combine(logDir, "craftbot_debug.log");
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string logEntry = $"[{timestamp}] CRITICAL: {message}\n";
                File.AppendAllText(logPath, logEntry);
            }
            catch
            {
                // Silently fail - no chat spam
            }
        }

        /// <summary>
        /// Analyzes items for recipe processing - shared logic for all recipes
        /// </summary>
        /// <param name="items">Items to analyze</param>
        /// <param name="canProcessFunc">Function to check if an item can be processed</param>
        /// <param name="recipeName">Name of the recipe for logging</param>
        /// <param name="stageDescription">Description of the processing stage</param>
        /// <returns>Recipe analysis result</returns>
        public static RecipeAnalysisResult AnalyzeItemsForRecipe(List<Item> items, Func<Item, bool> canProcessFunc, string recipeName, string stageDescription)
        {
            var processableItems = items.Where(canProcessFunc).ToList();

            return new RecipeAnalysisResult
            {
                CanProcess = processableItems.Any(),
                ProcessableItemCount = processableItems.Count,
                Stage = stageDescription,
                Description = $"Found {processableItems.Count} items for {recipeName} processing"
            };
        }

        /// <summary>
        /// Moves recipe components to inventory - shared logic for all recipes
        /// </summary>
        /// <param name="targetContainer">Container containing items</param>
        /// <param name="canProcessFunc">Function to check if an item should be moved</param>
        /// <param name="recipeName">Name of the recipe for logging</param>
        public static async Task MoveRecipeComponentsToInventory(Container targetContainer, Func<Item, bool> canProcessFunc, string recipeName)
        {
            try
            {
                LogDebug($"[{recipeName}] Moving all {recipeName}-related components to inventory");

                var componentsToMove = targetContainer.Items.Where(canProcessFunc).ToList();

                // OVERFLOW PROTECTION: Check if moving all components is safe
                if (!Modules.PrivateMessageModule.CheckInventorySpaceForProcessing(componentsToMove.Count, recipeName))
                {
                    LogDebug($"[{recipeName}] ❌ OVERFLOW PROTECTION: Cannot move {componentsToMove.Count} components - insufficient inventory space");
                    return; // Abort moving to prevent overflow
                }

                foreach (var component in componentsToMove)
                {
                    LogDebug($"[{recipeName}] Moving {component.Name} to inventory");
                    component.MoveToInventory();
                    await Task.Delay(100); // Standard movement delay
                }

                LogDebug($"[{recipeName}] Moved {componentsToMove.Count} {recipeName} components to inventory");
            }
            catch (Exception ex)
            {
                LogDebug($"[{recipeName}] Error moving {recipeName} components: {ex.Message}");
            }
        }

        /// <summary>
        /// Ensures items return to bag after processing - shared logic for all recipes
        /// </summary>
        /// <param name="targetContainer">Container to return items to</param>
        /// <param name="initialBagCount">Initial count of items in bag</param>
        /// <param name="recipeName">Name of the recipe for logging</param>
        public static async Task EnsureItemsReturnToBag(Container targetContainer, int initialBagCount, string recipeName)
        {
            try
            {
                // Move processed items back to container, excluding processing tools
                await MoveProcessedItemsBackToContainer(targetContainer, recipeName);

                // Return tools to their original bags
                await ReturnToolsToOriginalBags();

                // Verify items returned properly
                int finalBagCount = targetContainer.Items.Count();
                LogDebug($"[{recipeName}] Bag count: Initial={initialBagCount}, Final={finalBagCount}");
            }
            catch (Exception ex)
            {
                LogDebug($"[{recipeName}] Error ensuring items return to bag: {ex.Message}");
            }
        }

        /// <summary>
        /// Ensures items return to bag after processing WITHOUT returning tools - keeps tools in inventory
        /// This is used during bag processing to keep tools available for subsequent items
        /// </summary>
        /// <param name="targetContainer">Container to return items to</param>
        /// <param name="initialBagCount">Initial count of items in bag</param>
        /// <param name="recipeName">Name of the recipe for logging</param>
        public static async Task EnsureItemsReturnToBagWithoutTools(Container targetContainer, int initialBagCount, string recipeName)
        {
            try
            {
                // Move processed items back to backpack, excluding processing tools
                await MoveProcessedItemsBackToContainer(targetContainer, recipeName);

                // DO NOT return tools to their original bags - keep them in inventory for next items
                LogDebug($"[{recipeName}] ✅ Items moved back to bag, tools kept in inventory for continued processing");

                // Verify items returned properly
                int finalBagCount = targetContainer.Items.Count();
                LogDebug($"[{recipeName}] Bag count: Initial={initialBagCount}, Final={finalBagCount}");
            }
            catch (Exception ex)
            {
                LogDebug($"[{recipeName}] Error ensuring items return to bag without tools: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if an item is the bot's personal tool (should never be returned to players)
        /// vs a player-provided tool (should be returned when no recipe uses it)
        /// </summary>
        /// <param name="item">Item to check</param>
        /// <returns>True if this is the bot's personal tool</returns>
        public static bool IsBotPersonalTool(Item item)
        {
            // Access the pre-trade inventory from PrivateMessageModule
            // This requires reflection since _preTradeInventory is private
            try
            {
                var moduleType = Type.GetType("Craftbot.Modules.PrivateMessageModule");
                var preTradeField = moduleType.GetField("_preTradeInventory",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

                if (preTradeField != null)
                {
                    var preTradeInventory = preTradeField.GetValue(null) as List<Identity>;
                    if (preTradeInventory != null)
                    {
                        // First check if the item itself was in pre-trade inventory
                        bool wasInPreTrade = preTradeInventory.Any(preItem => preItem.Instance == item.Id);
                        LogDebug($"[TOOL CHECK] {item.Name} was in pre-trade inventory: {wasInPreTrade}");

                        if (wasInPreTrade)
                        {
                            return true; // Item was directly in bot's inventory before trade
                        }

                        // If not in inventory, check if it came from a bot's pre-trade bag
                        // Use unique item key to match the new tracking format
                        string itemKey = $"{item.Name}_{item.Id}_{item.UniqueIdentity.Instance}";
                        if (_toolOriginBags.ContainsKey(itemKey))
                        {
                            string originBagName = _toolOriginBags[itemKey];

                            // Check if the origin bag was in pre-trade inventory
                            var originBagInPreTrade = preTradeInventory.Any(preItem =>
                                preItem.Type == IdentityType.Container &&
                                Inventory.Containers.Any(bag =>
                                    bag.Identity.Instance == preItem.Instance &&
                                    bag.Item?.Name == originBagName));

                            LogDebug($"[TOOL CHECK] {item.Name} came from bag '{originBagName}', bag was in pre-trade: {originBagInPreTrade}");
                            return originBagInPreTrade; // If bag was pre-trade, tool is bot's personal tool
                        }

                        LogDebug($"[TOOL CHECK] {item.Name} has no tracked origin bag - assuming player-provided");
                        return false; // No origin tracking means it's likely player-provided
                    }
                }

                LogDebug($"[TOOL CHECK] Could not access pre-trade inventory - assuming player-provided tool: {item.Name}");
                return false; // If we can't determine, assume it's player-provided (safer)
            }
            catch (Exception ex)
            {
                LogDebug($"[TOOL CHECK] Error checking tool ownership for {item.Name}: {ex.Message}");
                return false; // If error, assume player-provided (safer)
            }
        }

        /// <summary>
        /// Finds and pulls a player-provided tool from bags (not bot's personal tools) with robust error handling
        /// </summary>
        /// <param name="toolName">Name of the tool to find</param>
        /// <returns>True if tool was found and successfully pulled to inventory</returns>
        public static bool FindAndPullPlayerProvidedTool(string toolName)
        {
            try
            {
                LogDebug($"[PLAYER TOOL] Looking for player-provided {toolName} in bags");

                // Check all containers for the tool
                foreach (var container in Inventory.Containers)
                {
                    var tool = container.Items.FirstOrDefault(item => item.Name.Contains(toolName));
                    if (tool != null)
                    {
                        LogDebug($"[PLAYER TOOL] Found {toolName} in bag {container.Item?.Name ?? "Unknown"}, pulling to inventory");

                        // Track the tool's origin for later return
                        // Use unique item key to handle duplicate tool names
                        string toolKey = $"{tool.Name}_{tool.Id}_{tool.UniqueIdentity.Instance}";
                        if (!_toolOriginBags.ContainsKey(toolKey))
                        {
                            _toolOriginBags[toolKey] = container.Item?.Name ?? "Unknown";
                            LogDebug($"[PLAYER TOOL] Tracking {toolKey} origin: {container.Item?.Name ?? "Unknown"}");
                        }

                        // Attempt to move tool with retry logic
                        if (MoveToolToInventoryWithRetry(tool, toolName))
                        {
                            return true;
                        }
                        else
                        {
                            LogDebug($"[PLAYER TOOL] ❌ Failed to move {toolName} to inventory after retries");
                            // Remove from tracking since move failed
                            _toolOriginBags.Remove(toolKey);
                        }
                    }
                }

                LogDebug($"[PLAYER TOOL] {toolName} not found in any bags");
                return false;
            }
            catch (Exception ex)
            {
                LogDebug($"[PLAYER TOOL] Error finding player-provided {toolName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Attempts to move a tool to inventory with retry logic and verification
        /// </summary>
        /// <param name="tool">The tool item to move</param>
        /// <param name="toolName">Name of the tool for logging</param>
        /// <returns>True if tool was successfully moved to inventory</returns>
        private static bool MoveToolToInventoryWithRetry(Item tool, string toolName)
        {
            const int maxRetries = 3;
            const int retryDelayMs = 200;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    LogDebug($"[TOOL MOVE] Attempt {attempt}/{maxRetries}: Moving {toolName} to inventory");

                    // Store original location for verification
                    var originalSlot = tool.Slot;
                    var originalInstance = tool.UniqueIdentity.Instance;

                    // Attempt the move
                    tool.MoveToInventory();
                    Task.Delay(retryDelayMs).Wait();

                    // Verify the tool actually moved to inventory
                    var toolInInventory = Inventory.Items.FirstOrDefault(item =>
                        item.UniqueIdentity.Instance == originalInstance &&
                        item.Slot.Type == IdentityType.Inventory);

                    if (toolInInventory != null)
                    {
                        LogDebug($"[TOOL MOVE] ✅ Successfully moved {toolName} to inventory on attempt {attempt}");
                        return true;
                    }
                    else
                    {
                        LogDebug($"[TOOL MOVE] ⚠️ Attempt {attempt} failed: {toolName} not found in inventory after move");

                        // Check if tool is still in original location
                        if (tool.Slot.Type == originalSlot.Type && tool.Slot.Instance == originalSlot.Instance)
                        {
                            LogDebug($"[TOOL MOVE] Tool {toolName} remained in original location - move failed silently");
                        }
                        else
                        {
                            LogDebug($"[TOOL MOVE] Tool {toolName} moved but not to inventory - unexpected location");
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogDebug($"[TOOL MOVE] ❌ Attempt {attempt} threw exception: {ex.Message}");
                }

                // Wait before retry (except on last attempt)
                if (attempt < maxRetries)
                {
                    Task.Delay(retryDelayMs).Wait();
                }
            }

            LogDebug($"[TOOL MOVE] ❌ CRITICAL: Failed to move {toolName} to inventory after {maxRetries} attempts");
            return false;
        }
    }
}
