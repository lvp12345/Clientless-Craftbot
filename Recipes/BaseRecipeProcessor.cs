using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AOSharp.Clientless;
using AOSharp.Common.GameData;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Craftbot.Modules;

namespace Craftbot.Recipes
{
    /// <summary>
    /// Base abstract class for recipe processors providing common functionality
    /// </summary>
    public abstract class BaseRecipeProcessor : IRecipeProcessor
    {
        /// <summary>
        /// Gets the name of this recipe processor
        /// </summary>
        public abstract string RecipeName { get; }

        /// <summary>
        /// Indicates whether this recipe should use single-item processing.
        /// Default is false (batch processing). Override in derived classes for single-item recipes.
        /// </summary>
        public virtual bool UsesSingleItemProcessing => false;

        /// <summary>
        /// Determines if this processor can handle the given item
        /// </summary>
        /// <param name="item">Item to check</param>
        /// <returns>True if this processor can handle the item</returns>
        public abstract bool CanProcess(Item item);

        /// <summary>
        /// Safe wrapper for CanProcess that excludes accidental player tools
        /// </summary>
        /// <param name="item">Item to check</param>
        /// <returns>True if this processor can handle the item and it's not an accidental tool</returns>
        protected bool CanProcessSafely(Item item)
        {
            if (item == null) return false;

            // ACCIDENTAL TOOL PROTECTION: Never process tools that players accidentally provided
            if (Core.ItemTracker.IsAccidentalPlayerTool(item))
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Skipping accidental player tool: {item.Name}");
                return false;
            }

            bool canProcess = CanProcess(item);
            RecipeUtilities.LogDebug($"[{RecipeName}] CanProcessSafely: '{item.Name}' -> {canProcess}");
            return canProcess;
        }

        /// <summary>
        /// UNIFIED CORE: Processes items using unified workflow for all recipes
        /// </summary>
        /// <param name="item">Item to process</param>
        /// <param name="targetContainer">Container to return processed items to (null for loose items)</param>
        /// <returns>Task representing the async operation</returns>
        public async Task ProcessItem(Item item, Container targetContainer)
        {
            try
            {
                LogProcessingStart(item);

                if (targetContainer != null)
                {
                    // BAG PROCESSING WORKFLOW
                    await ProcessBagWorkflow(item, targetContainer);
                }
                else
                {
                    // LOOSE ITEM PROCESSING WORKFLOW
                    await ProcessLooseItemWorkflow(item);
                }

                LogProcessingComplete(item);
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error in unified processing: {ex.Message}");

                // Failsafe: Try to move any items back to bag if applicable
                if (targetContainer != null)
                {
                    await RecipeUtilities.MoveProcessedItemsBackToContainer(targetContainer, RecipeName);
                }
            }
        }

        /// <summary>
        /// Recipe-specific processing logic - implemented by each recipe
        /// </summary>
        /// <param name="item">Item to process</param>
        /// <param name="targetContainer">Container for bag processing (null for loose items)</param>
        /// <returns>Task representing the async operation</returns>
        protected abstract Task ProcessRecipeLogic(Item item, Container targetContainer);

        /// <summary>
        /// Analyzes a collection of items to determine if they can be processed as a recipe
        /// </summary>
        /// <param name="items">Items to analyze</param>
        /// <returns>Recipe analysis result</returns>
        public virtual RecipeAnalysisResult AnalyzeItems(List<Item> items)
        {
            var processableItems = items.Where(CanProcessSafely).ToList();

            return new RecipeAnalysisResult
            {
                CanProcess = processableItems.Any(),
                ProcessableItemCount = processableItems.Count,
                Stage = "Basic",
                Description = $"Found {processableItems.Count} processable items for {RecipeName}"
            };
        }

        /// <summary>
        /// Helper method to find a tool in inventory or bags
        /// </summary>
        /// <param name="toolName">Name of the tool to find</param>
        /// <returns>The tool item if found, null otherwise</returns>
        protected Item FindTool(string toolName)
        {
            // First check inventory
            var tool = Inventory.Items.FirstOrDefault(item => item.Name.Contains(toolName));
            if (tool != null)
            {
                return tool;
            }

            // If not in inventory, try to pull from bags
            if (RecipeUtilities.FindAndPullTool(toolName))
            {
                // Wait longer for tool to move to inventory and for item events to fire
                Task.Delay(300).Wait();

                // Try multiple times to find the tool in case inventory hasn't updated yet
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    tool = Inventory.Items.FirstOrDefault(item => item.Name.Contains(toolName));
                    if (tool != null)
                    {
                        RecipeUtilities.LogDebug($"[{RecipeName}] ✅ Found {toolName} in inventory on attempt {attempt + 1}");
                        return tool;
                    }

                    if (attempt < 2)
                    {
                        RecipeUtilities.LogDebug($"[{RecipeName}] ⚠️ Tool {toolName} not found in inventory yet, waiting... (attempt {attempt + 1}/3)");
                        Task.Delay(200).Wait();
                    }
                }

                RecipeUtilities.LogDebug($"[{RecipeName}] ❌ Tool {toolName} was moved but still not found in inventory after 3 attempts");
            }

            return null;
        }

        /// <summary>
        /// UNIFIED CORE: Combines two items using proper clientless tradeskill approach
        /// Based on the working ImplantRecipe implementation - fixes all recipes at once
        /// </summary>
        /// <param name="tool">Tool to use for combination</param>
        /// <param name="target">Target item to combine with</param>
        protected async Task CombineItems(Item tool, Item target)
        {
            try
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Combining {tool.Name} with {target.Name}");

                // Take snapshot of inventory before processing
                var inventoryBefore = Inventory.Items.ToList();
                int toolId = tool.Id;
                int targetId = target.Id;
                string toolName = tool.Name;
                string targetName = target.Name;

                // EXACT 3-STEP TRADESKILL LOGIC: Source -> Target -> Execute (replaces tool.Use())
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 1: Setting tradeskill source to {tool.Name} ({tool.Slot})");
                Client.Send(new CharacterActionMessage
                {
                    Action = CharacterActionType.TradeskillSourceChanged,
                    Target = Identity.None,
                    Parameter1 = (int)tool.Slot.Type,
                    Parameter2 = tool.Slot.Instance
                });

                await Task.Delay(100); // Small delay between steps

                RecipeUtilities.LogDebug($"[{RecipeName}] Step 2: Setting tradeskill target to {target.Name} ({target.Slot})");
                Client.Send(new CharacterActionMessage
                {
                    Action = CharacterActionType.TradeskillTargetChanged,
                    Target = Identity.None,
                    Parameter1 = (int)target.Slot.Type,
                    Parameter2 = target.Slot.Instance
                });

                await Task.Delay(100); // Small delay between steps

                RecipeUtilities.LogDebug($"[{RecipeName}] Step 3: Executing tradeskill build");
                Client.Send(new CharacterActionMessage
                {
                    Action = CharacterActionType.TradeskillBuildPressed,
                    Target = new Identity(IdentityType.None, target.Ql), // Use target's quality level
                });

                // Recipe-specific delays for different combination types
                int delay = GetCombinationDelay();
                RecipeUtilities.LogDebug($"[{RecipeName}] Waiting {delay}ms for combination to complete");
                await Task.Delay(delay);

                // CRITICAL: Verify the combination actually worked
                bool toolStillExists = Inventory.Items.Any(i => i.Id == toolId);
                bool targetStillExists = Inventory.Items.Any(i => i.Id == targetId);

                if (toolStillExists && targetStillExists)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] ❌ COMBINATION FAILED: Both {toolName} and {targetName} still exist in inventory!");
                    RecipeUtilities.LogDebug($"[{RecipeName}] This means the tradeskill did not execute successfully");
                }
                else if (toolStillExists || targetStillExists)
                {
                    string remaining = toolStillExists ? toolName : targetName;
                    RecipeUtilities.LogDebug($"[{RecipeName}] ⚠️ PARTIAL COMBINATION: {remaining} still exists in inventory");
                }
                else
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] ✅ Combination successful: Both items consumed");
                }

                // Check for new items created by the recipe
                TrackNewRecipeResults(inventoryBefore);

                RecipeUtilities.LogDebug($"[{RecipeName}] Combination completed");
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error combining items: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the appropriate delay for this recipe type
        /// </summary>
        /// <returns>Delay in milliseconds</returns>
        protected virtual int GetCombinationDelay()
        {
            // Default delay for most recipes
            return 200;
        }

        /// <summary>
        /// Tracks new items created by recipe processing
        /// </summary>
        /// <param name="inventoryBefore">Inventory state before processing</param>
        protected void TrackNewRecipeResults(List<Item> inventoryBefore)
        {
            try
            {
                var inventoryAfter = Inventory.Items.ToList();

                // Find new items that weren't in inventory before
                var newItems = inventoryAfter.Where(afterItem =>
                    !inventoryBefore.Any(beforeItem =>
                        beforeItem.UniqueIdentity == afterItem.UniqueIdentity)).ToList();

                foreach (var newItem in newItems)
                {
                    // Only track items that look like recipe results (not tools or containers)
                    if (newItem.Slot.Type == IdentityType.Inventory &&
                        !RecipeUtilities.IsProcessingTool(newItem) &&
                        newItem.UniqueIdentity.Type != IdentityType.Container)
                    {
                        Core.ItemTracker.TrackRecipeResult(newItem, RecipeName);
                        RecipeUtilities.LogDebug($"[{RecipeName}] Tracked new recipe result: {newItem.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error tracking recipe results: {ex.Message}");
            }
        }

        /// <summary>
        /// Helper method to track initial bag item count for processing verification
        /// </summary>
        /// <param name="targetContainer">Container to count items in</param>
        /// <returns>Number of items in the container</returns>
        protected int GetBagItemCount(Container targetContainer)
        {
            return targetContainer.Items.Count();
        }

        /// <summary>
        /// Helper method to log processing start
        /// </summary>
        /// <param name="item">Item being processed</param>
        protected void LogProcessingStart(Item item)
        {
            RecipeUtilities.LogDebug($"[{RecipeName}] Processing {item.Name} with {RecipeName} logic");
        }

        /// <summary>
        /// Helper method to log processing completion
        /// </summary>
        /// <param name="item">Item that was processed</param>
        protected void LogProcessingComplete(Item item)
        {
            RecipeUtilities.LogDebug($"[{RecipeName}] Completed processing {item.Name}");
        }

        /// <summary>
        /// Shared method for analyzing items using recipe-specific logic
        /// </summary>
        /// <param name="items">Items to analyze</param>
        /// <param name="stageDescription">Description of the processing stage</param>
        /// <returns>Recipe analysis result</returns>
        protected RecipeAnalysisResult AnalyzeItemsShared(List<Item> items, string stageDescription)
        {
            return RecipeUtilities.AnalyzeItemsForRecipe(items, CanProcessSafely, RecipeName, stageDescription);
        }

        /// <summary>
        /// Shared method for moving recipe components to inventory
        /// </summary>
        /// <param name="targetContainer">Container containing items</param>
        protected async Task MoveComponentsToInventoryShared(Container targetContainer)
        {
            await RecipeUtilities.MoveRecipeComponentsToInventory(targetContainer, CanProcessSafely, RecipeName);
        }

        /// <summary>
        /// Shared method for ensuring items return to bag after processing
        /// </summary>
        /// <param name="targetContainer">Container to return items to</param>
        /// <param name="initialBagCount">Initial count of items in bag</param>
        protected async Task EnsureItemsReturnToBagShared(Container targetContainer, int initialBagCount)
        {
            await RecipeUtilities.EnsureItemsReturnToBag(targetContainer, initialBagCount, RecipeName);
        }

        /// <summary>
        /// Shared method for ensuring items return to bag after processing WITHOUT returning tools
        /// This keeps tools in inventory until all recipe processing is complete
        /// </summary>
        /// <param name="targetContainer">Container to return items to</param>
        /// <param name="initialBagCount">Initial count of items in bag</param>
        protected async Task EnsureItemsReturnToBagSharedWithoutTools(Container targetContainer, int initialBagCount)
        {
            await RecipeUtilities.EnsureItemsReturnToBagWithoutTools(targetContainer, initialBagCount, RecipeName);
        }

        /// <summary>
        /// UNIFIED BAG WORKFLOW: Handles all common bag processing steps
        /// ENHANCED: Supports both single-item and batch processing based on recipe type
        /// </summary>
        private async Task ProcessBagWorkflow(Item item, Container targetContainer)
        {
            // 1. Clear any recipe-specific tracking (like processed items for implants)
            if (this is ImplantRecipe)
            {
                ImplantRecipe.ClearProcessedCombinations();
            }

            // 2. Track initial bag item count
            int initialBagCount = GetBagItemCount(targetContainer);
            RecipeUtilities.LogDebug($"[{RecipeName}] Initial bag item count: {initialBagCount}");

            // 3. Choose processing strategy based on recipe type
            if (UsesSingleItemProcessing)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Using single-item processing strategy");
                await ProcessBagSingleItem(targetContainer, initialBagCount);
            }
            else
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Using safe batch processing strategy");
                await ProcessBagBatchSafe(targetContainer, initialBagCount);
            }
        }

        /// <summary>
        /// OPTIMIZED SINGLE-ITEM PROCESSING: Group identical items to reduce CanProcess() calls
        /// Process one item at a time for simple recipes
        /// </summary>
        private async Task ProcessBagSingleItem(Container targetContainer, int initialBagCount)
        {
            // Group items by name to avoid redundant CanProcess() calls
            var itemGroups = targetContainer.Items.GroupBy(item => item.Name).ToList();
            var allProcessableItems = new List<Item>();

            RecipeUtilities.LogDebug($"[{RecipeName}] OPTIMIZED single-item processing: Checking {itemGroups.Count} unique item types");

            // Check each unique item type only once
            foreach (var itemGroup in itemGroups)
            {
                var sampleItem = itemGroup.First();
                if (CanProcessSafely(sampleItem))
                {
                    allProcessableItems.AddRange(itemGroup);
                    RecipeUtilities.LogDebug($"[{RecipeName}] Found {itemGroup.Count()}x processable {itemGroup.Key}");
                }
            }

            RecipeUtilities.LogDebug($"[{RecipeName}] Total processable items: {allProcessableItems.Count}");

            foreach (var processableItem in allProcessableItems)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Processing single item: {processableItem.Name}");

                // OVERFLOW PROTECTION: Check if moving this single item + tools is safe
                if (!Modules.PrivateMessageModule.CheckInventorySpaceForProcessing(3, RecipeName)) // Item + 2 tools max
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] ❌ OVERFLOW PROTECTION: Cannot process {processableItem.Name} - insufficient inventory space");
                    continue; // Skip this item and try next
                }

                // Move single item to inventory
                processableItem.MoveToInventory();
                await Task.Delay(200);

                // Clean up any temporary data receptacles
                RecipeUtilities.DeleteTemporaryDataReceptacle();

                // Process this single item
                await ProcessRecipeLogic(processableItem, targetContainer);

                // Move processed results back to bag immediately (keep tools for next item)
                await EnsureItemsReturnToBagSharedWithoutTools(targetContainer, initialBagCount);

                // Small delay between items
                await Task.Delay(200);
            }

            // After all items processed, return tools to bags
            await RecipeUtilities.ReturnToolsToOriginalBags();
        }

        /// <summary>
        /// ULTRA-EFFICIENT BATCH PROCESSING: Group identical items and process efficiently
        /// Eliminates redundant CanProcess() calls by grouping items by name
        /// </summary>
        private async Task ProcessBagBatchSafe(Container targetContainer, int initialBagCount)
        {
            // Group items by name to avoid redundant CanProcess() calls
            var itemGroups = targetContainer.Items.GroupBy(item => item.Name).ToList();
            var allProcessableItems = new List<Item>();

            RecipeUtilities.LogDebug($"[{RecipeName}] ULTRA-EFFICIENT batch processing: Checking {itemGroups.Count} unique item types");

            // Check each unique item type only once
            foreach (var itemGroup in itemGroups)
            {
                var sampleItem = itemGroup.First();
                if (CanProcessSafely(sampleItem))
                {
                    allProcessableItems.AddRange(itemGroup);
                    RecipeUtilities.LogDebug($"[{RecipeName}] Found {itemGroup.Count()}x processable {itemGroup.Key}");
                }
            }

            RecipeUtilities.LogDebug($"[{RecipeName}] Total processable items: {allProcessableItems.Count}");
            if (allProcessableItems.Count == 0) return;

            // Calculate safe batch size accounting for: items + tool + result space
            int freeSlots = Inventory.NumFreeSlots;
            // Reserve space for: 1 tool + 1 result + 1 safety buffer = 3 slots minimum
            int maxSafeBatchSize = Math.Max(1, freeSlots - 3);

            if (allProcessableItems.Count <= maxSafeBatchSize)
            {
                // All items fit - use normal batch processing (like Pearl recipe)
                RecipeUtilities.LogDebug($"[{RecipeName}] All {allProcessableItems.Count} items fit safely - processing as single batch");
                await ProcessBatchChunk(allProcessableItems, targetContainer, initialBagCount);
            }
            else
            {
                // Too many items - process in safe chunks
                int totalChunks = (int)Math.Ceiling((double)allProcessableItems.Count / maxSafeBatchSize);
                RecipeUtilities.LogDebug($"[{RecipeName}] Processing {allProcessableItems.Count} items in {totalChunks} chunks of max {maxSafeBatchSize} items each (inventory has {freeSlots} free slots)");

                for (int i = 0; i < allProcessableItems.Count; i += maxSafeBatchSize)
                {
                    var chunk = allProcessableItems.Skip(i).Take(maxSafeBatchSize).ToList();
                    int chunkNumber = (i / maxSafeBatchSize) + 1;

                    RecipeUtilities.LogDebug($"[{RecipeName}] Processing chunk {chunkNumber}/{totalChunks}: {chunk.Count} items");

                    await ProcessBatchChunk(chunk, targetContainer, initialBagCount);

                    // Small delay between chunks for stability
                    await Task.Delay(200);
                }
            }
        }

        /// <summary>
        /// Process a chunk of items using batch processing logic
        /// Handles: moving items + processing + returning results
        /// </summary>
        private async Task ProcessBatchChunk(List<Item> itemsToProcess, Container targetContainer, int initialBagCount)
        {
            RecipeUtilities.LogDebug($"[{RecipeName}] Processing chunk of {itemsToProcess.Count} items");

            // Move chunk to inventory
            foreach (var item in itemsToProcess)
            {
                item.MoveToInventory();
                await Task.Delay(100);
            }

            // CRITICAL FIX: Wait for items to fully appear in inventory before processing
            // This ensures the inventory snapshot in ProcessAllItemsUntilComplete will see the moved items
            await Task.Delay(300);

            // Clean up any temporary data receptacles
            RecipeUtilities.DeleteTemporaryDataReceptacle();

            // Process the chunk (same as current batch processing)
            await ProcessAllItemsUntilComplete(targetContainer);

            // Wait for processing to complete
            await Task.Delay(200); // Standard completion delay

            // Return processed results to bag (keep tools for next chunk)
            await EnsureItemsReturnToBagSharedWithoutTools(targetContainer, initialBagCount);
        }

        /// <summary>
        /// LEGACY BATCH PROCESSING: Move all items to inventory and process until complete (for multi-step recipes)
        /// DEPRECATED: Use ProcessBagBatchSafe instead for overflow protection
        /// </summary>
        private async Task ProcessBagBatch(Container targetContainer, int initialBagCount)
        {
            // Move ALL processable items from bag to inventory at once
            var allProcessableItems = targetContainer.Items.Where(CanProcessSafely).ToList();
            RecipeUtilities.LogDebug($"[{RecipeName}] Batch processing: Moving {allProcessableItems.Count} processable items to inventory");

            // OVERFLOW PROTECTION: Check if moving all items is safe
            if (!Modules.PrivateMessageModule.CheckInventorySpaceForProcessing(allProcessableItems.Count, RecipeName))
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] ❌ OVERFLOW PROTECTION: Cannot move {allProcessableItems.Count} items - insufficient inventory space");
                return; // Abort processing to prevent overflow
            }

            // Move all processable items to inventory
            foreach (var processableItem in allProcessableItems)
            {
                processableItem.MoveToInventory();
                await Task.Delay(100);
            }

            // Clean up any temporary data receptacles
            RecipeUtilities.DeleteTemporaryDataReceptacle();

            // Process ALL items until no more processing is possible
            await ProcessAllItemsUntilComplete(targetContainer);

            // Wait for processing to complete
            await Task.Delay(200); // Standard completion delay

            // Ensure all processed items get back to bag (WITHOUT returning tools yet)
            await EnsureItemsReturnToBagSharedWithoutTools(targetContainer, initialBagCount);
        }

        /// <summary>
        /// NEW METHOD: Process all items in inventory until no more processing is possible
        /// This ensures multi-component recipes (like PB) can complete all combinations
        /// SAFETY: Enhanced deadlock detection and recovery mechanisms
        /// </summary>
        private async Task ProcessAllItemsUntilComplete(Container targetContainer)
        {
            RecipeUtilities.LogDebug($"[{RecipeName}] Starting continuous processing until complete");

            bool processingOccurred = true;
            int maxIterations = 10; // Safety limit to prevent infinite loops
            int iteration = 0;
            int consecutiveFailures = 0;
            const int maxConsecutiveFailures = 3;

            // Enhanced deadlock detection
            var lastProcessableItemNames = new HashSet<string>();
            int identicalStateCount = 0;
            const int maxIdenticalStates = 2;

            // Track initial processable items for failure recovery
            // PERFORMANCE OPTIMIZATION: Only check items that could potentially be processable
            var allInventoryItems = Inventory.Items.ToList();
            var initialCandidateItems = allInventoryItems.Where(item =>
                item.Slot.Type == IdentityType.Inventory &&
                !(item.Slot.Instance >= (int)EquipSlot.Weap_Hud1 && item.Slot.Instance <= (int)EquipSlot.Imp_Feet) &&
                !Core.ItemTracker.IsBotPersonalItem(item) &&
                !Core.ItemTracker.IsBotTool(item)).ToList();

            RecipeUtilities.LogDebug($"[{RecipeName}] PERFORMANCE: Initial scan filtered {allInventoryItems.Count} total items down to {initialCandidateItems.Count} candidates");
            var initialProcessableItems = initialCandidateItems.Where(CanProcessSafely).ToList();

            while (processingOccurred && iteration < maxIterations)
            {
                iteration++;
                processingOccurred = false;
                RecipeUtilities.LogDebug($"[{RecipeName}] Processing iteration {iteration}");

                // Get all processable items currently in inventory - create safe copy to avoid collection modification errors
                var inventorySnapshot = Inventory.Items.ToList(); // Safe copy to prevent "Collection was modified" errors

                // ULTRA-PERFORMANCE OPTIMIZATION: Group identical items and check only unique types
                var candidateItems = inventorySnapshot.Where(item =>
                    item.Slot.Type == IdentityType.Inventory &&
                    !(item.Slot.Instance >= (int)EquipSlot.Weap_Hud1 && item.Slot.Instance <= (int)EquipSlot.Imp_Feet) &&
                    !Core.ItemTracker.IsBotPersonalItem(item) &&
                    !Core.ItemTracker.IsBotTool(item)).ToList();

                // Group by item name to avoid redundant CanProcess() calls
                var itemGroups = candidateItems.GroupBy(item => item.Name).ToList();
                var processableItems = new List<Item>();

                RecipeUtilities.LogDebug($"[{RecipeName}] ULTRA-PERFORMANCE: Filtered {inventorySnapshot.Count} total items down to {candidateItems.Count} candidates in {itemGroups.Count} unique types");

                // Check each unique item type only once
                foreach (var itemGroup in itemGroups)
                {
                    var sampleItem = itemGroup.First();
                    if (CanProcessSafely(sampleItem))
                    {
                        // Add all items of this processable type
                        processableItems.AddRange(itemGroup);
                        RecipeUtilities.LogDebug($"[{RecipeName}] Found {itemGroup.Count()}x processable {itemGroup.Key}");
                    }
                }

                if (processableItems.Any())
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Found {processableItems.Count} processable items in inventory");

                    // Enhanced deadlock detection - check if we're seeing the same items repeatedly
                    var currentItemNames = new HashSet<string>(processableItems.Select(item => item.Name));
                    if (currentItemNames.SetEquals(lastProcessableItemNames))
                    {
                        identicalStateCount++;
                        RecipeUtilities.LogDebug($"[{RecipeName}] ⚠️ Identical processable items detected {identicalStateCount} times in a row");

                        if (identicalStateCount >= maxIdenticalStates)
                        {
                            RecipeUtilities.LogDebug($"[{RecipeName}] ❌ DEADLOCK DETECTED: Same items found {maxIdenticalStates} times - likely missing tools or processing failure");
                            await HandleProcessingFailure(targetContainer, initialProcessableItems);
                            return;
                        }
                    }
                    else
                    {
                        identicalStateCount = 0; // Reset if items changed
                        lastProcessableItemNames = currentItemNames;
                    }

                    // Track items before processing to detect if anything actually happened
                    var itemCountBefore = processableItems.Count;

                    // Process each item - this may create new processable items
                    // Use safe iteration to prevent collection modification errors
                    bool anyItemProcessedSuccessfully = false;
                    for (int i = 0; i < processableItems.Count; i++)
                    {
                        try
                        {
                            var processableItem = processableItems[i];
                            RecipeUtilities.LogDebug($"[{RecipeName}] Processing {processableItem.Name}");

                            // Track if this specific item still exists after processing
                            var itemIdBefore = processableItem.UniqueIdentity.Instance;

                            await ProcessRecipeLogic(processableItem, targetContainer);

                            // Check if the item was actually processed (consumed/transformed)
                            var itemStillExists = Inventory.Items.Any(item => item.UniqueIdentity.Instance == itemIdBefore);
                            if (!itemStillExists)
                            {
                                anyItemProcessedSuccessfully = true;
                                RecipeUtilities.LogDebug($"[{RecipeName}] ✅ Successfully processed {processableItem.Name}");
                            }
                            else
                            {
                                RecipeUtilities.LogDebug($"[{RecipeName}] ⚠️ Item {processableItem.Name} still exists after processing - may indicate tool missing or processing failure");
                            }

                            processingOccurred = true;
                            await Task.Delay(100); // Small delay between items
                        }
                        catch (Exception ex)
                        {
                            RecipeUtilities.LogDebug($"[{RecipeName}] Error processing item {i}: {ex.Message}");
                            // Continue with next item instead of crashing
                        }
                    }

                    // Check if processing actually reduced the number of processable items
                    var itemCountAfter = Inventory.Items.Where(item =>
                        item.Slot.Type == IdentityType.Inventory &&
                        !(item.Slot.Instance >= (int)EquipSlot.Weap_Hud1 && item.Slot.Instance <= (int)EquipSlot.Imp_Feet) &&
                        CanProcessSafely(item)).Count();

                    // For batch processing recipes, individual item tracking may not work correctly
                    // So we primarily rely on the overall item count reduction
                    bool processingMadeProgress = itemCountAfter < itemCountBefore;

                    // Only trigger failure if BOTH conditions are true:
                    // 1. No reduction in processable item count
                    // 2. No individual items were successfully processed (for non-batch recipes)
                    if (!processingMadeProgress && !anyItemProcessedSuccessfully)
                    {
                        consecutiveFailures++;
                        RecipeUtilities.LogDebug($"[{RecipeName}] ⚠️ Processing iteration {iteration} made no progress (before: {itemCountBefore}, after: {itemCountAfter}, individual success: {anyItemProcessedSuccessfully}). Consecutive failures: {consecutiveFailures}");

                        if (consecutiveFailures >= maxConsecutiveFailures)
                        {
                            RecipeUtilities.LogDebug($"[{RecipeName}] ❌ PROCESSING FAILED: {maxConsecutiveFailures} consecutive failures detected - aborting to prevent infinite loop");
                            await HandleProcessingFailure(targetContainer, initialProcessableItems);
                            return;
                        }
                    }
                    else
                    {
                        if (processingMadeProgress)
                        {
                            RecipeUtilities.LogDebug($"[{RecipeName}] ✅ Processing made progress: {itemCountBefore} → {itemCountAfter} processable items");
                        }
                        consecutiveFailures = 0; // Reset failure counter on successful processing
                    }
                }
                else
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] No more processable items found - processing complete");
                    break;
                }
            }

            if (iteration >= maxIterations)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] ❌ PROCESSING FAILED: Reached maximum iterations ({maxIterations}) - aborting to prevent infinite loop");
                await HandleProcessingFailure(targetContainer, initialProcessableItems);
                return;
            }

            RecipeUtilities.LogDebug($"[{RecipeName}] ✅ Completed continuous processing successfully after {iteration} iterations");
        }

        /// <summary>
        /// SAFETY METHOD: Handles processing failures by returning items to player
        /// </summary>
        private async Task HandleProcessingFailure(Container targetContainer, List<Item> initialProcessableItems)
        {
            try
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] 🚨 HANDLING PROCESSING FAILURE - returning items to player");

                if (targetContainer != null)
                {
                    // BAG PROCESSING FAILURE: Move all remaining items back to bag
                    RecipeUtilities.LogDebug($"[{RecipeName}] Moving all remaining items back to bag due to processing failure");
                    await RecipeUtilities.MoveProcessedItemsBackToContainer(targetContainer, RecipeName);

                    // Also return any tools that were moved to inventory
                    await RecipeUtilities.ReturnToolsToOriginalBags();
                }
                else
                {
                    // LOOSE ITEM PROCESSING FAILURE: Items stay in inventory but log the failure
                    RecipeUtilities.LogDebug($"[{RecipeName}] Loose item processing failed - items remain in inventory");
                }

                // Clear any recipe-specific tracking to prevent further issues
                if (this is ImplantRecipe)
                {
                    ImplantRecipe.ClearProcessedCombinations();
                }

                RecipeUtilities.LogDebug($"[{RecipeName}] ✅ Processing failure handled - items returned to player");
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] ❌ Error handling processing failure: {ex.Message}");
            }
        }

        /// <summary>
        /// UNIFIED LOOSE ITEM WORKFLOW: Handles all common loose item processing steps
        /// </summary>
        private async Task ProcessLooseItemWorkflow(Item item)
        {
            // Standard workflow for single-item recipes
            // 1. OVERFLOW PROTECTION: Check if moving item is safe
            if (!Modules.PrivateMessageModule.CheckInventorySpaceForProcessing(1, RecipeName))
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] ❌ OVERFLOW PROTECTION: Cannot move loose item - insufficient inventory space");
                return; // Abort processing to prevent overflow
            }

            // 2. Move item to inventory for processing
            item.MoveToInventory();
            await Task.Delay(200); // Standard timing for reliability

            // 3. Clean up any temporary data receptacles
            RecipeUtilities.DeleteTemporaryDataReceptacle();

            // 4. Track the original item to ensure it gets returned if processing fails
            var originalItemName = item.Name;

            try
            {
                // 5. Execute recipe-specific processing logic
                await ProcessRecipeLogic(item, null);

                // 6. Wait for processing to complete
                await Task.Delay(200); // Standard completion delay
            }
            finally
            {
                // 7. Wait a bit longer for processing to fully complete before failsafe check
                await Task.Delay(200); // Additional delay for complex recipes

                // 8. CRITICAL FAILSAFE: Ensure any unprocessed items get returned to player
                EnsureUnprocessedItemsReturnToPlayer(originalItemName);

                // 9. Return tools to bags (loose items don't have a target bag for items)
                await Modules.PrivateMessageModule.EndToolSession();
            }
        }

        /// <summary>
        /// CRITICAL FAILSAFE: Ensures any unprocessed items get returned to player
        /// This handles cases where recipe processing fails and items are left in inventory
        /// </summary>
        /// <param name="originalItemName">Name of the original item that was being processed</param>
        private void EnsureUnprocessedItemsReturnToPlayer(string originalItemName)
        {
            try
            {
                // Check if the original item is still in inventory (meaning processing failed)
                var unprocessedItem = Inventory.Items.FirstOrDefault(invItem =>
                    invItem.Name.Equals(originalItemName, StringComparison.OrdinalIgnoreCase) && CanProcessSafely(invItem));

                if (unprocessedItem != null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] ⚠️ FAILSAFE: Found unprocessed item {unprocessedItem.Name} in inventory - returning to player");

                    // Return the unprocessed item to the player via trade
                    // This ensures the player gets their item back even if processing failed
                    // The item will be handled by the normal EndToolSession() logic
                }
                else
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] ✅ Original item {originalItemName} was successfully processed or consumed");
                }
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error in unprocessed items failsafe: {ex.Message}");
            }
        }

    }
}
