using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AOSharp.Clientless;

namespace Craftbot.Recipes
{
    /// <summary>
    /// Manages all recipe processors and coordinates recipe processing
    /// </summary>
    public static class RecipeManager
    {
        private static List<IRecipeProcessor> _processors = new List<IRecipeProcessor>();
        private static bool _initialized = false;

        /// <summary>
        /// Initializes the recipe manager with all available processors
        /// </summary>
        public static void Initialize()
        {
            RecipeUtilities.LogDebug($"[RECIPE MANAGER] *** INITIALIZE CALLED *** _initialized={_initialized}");

            if (_initialized)
            {
                RecipeUtilities.LogDebug($"[RECIPE MANAGER] Already initialized, skipping");
                return;
            }

            try
            {
                // Load all recipe processors (CarbArmorRecipe FIRST for priority)
                _processors.Clear();
                try { _processors.Add(new CarbArmorRecipe()); } catch (Exception ex) { RecipeUtilities.LogDebug($"[RECIPE MANAGER] ❌ CarbArmorRecipe failed: {ex.Message}"); }
                try { _processors.Add(new PlasmaRecipe()); } catch (Exception ex) { RecipeUtilities.LogDebug($"[RECIPE MANAGER] ❌ PlasmaRecipe failed: {ex.Message}"); }
                try { _processors.Add(new PitDemonHeartRecipe()); } catch (Exception ex) { RecipeUtilities.LogDebug($"[RECIPE MANAGER] ❌ PitDemonHeartRecipe failed: {ex.Message}"); }
                try { _processors.Add(new FredericksonSleevesRecipe()); } catch (Exception ex) { RecipeUtilities.LogDebug($"[RECIPE MANAGER] ❌ FredericksonSleevesRecipe failed: {ex.Message}"); }
                try { _processors.Add(new NanoCrystalRepairRecipe()); } catch (Exception ex) { RecipeUtilities.LogDebug($"[RECIPE MANAGER] ❌ NanoCrystalRepairRecipe failed: {ex.Message}"); }
                try { _processors.Add(new PearlRecipe()); } catch (Exception ex) { RecipeUtilities.LogDebug($"[RECIPE MANAGER] ❌ PearlRecipe failed: {ex.Message}"); }
                try { _processors.Add(new SmeltingRecipe()); } catch (Exception ex) { RecipeUtilities.LogDebug($"[RECIPE MANAGER] ❌ SmeltingRecipe failed: {ex.Message}"); }
                try { _processors.Add(new IceRecipe()); } catch (Exception ex) { RecipeUtilities.LogDebug($"[RECIPE MANAGER] ❌ IceRecipe failed: {ex.Message}"); }
                try { _processors.Add(new RobotBrainRecipe()); } catch (Exception ex) { RecipeUtilities.LogDebug($"[RECIPE MANAGER] ❌ RobotBrainRecipe failed: {ex.Message}"); }
                // NOTE: RobotJunkRecipe removed - RobotBrainRecipe handles all Robot Junk processing with multi-step support
                try { _processors.Add(new VTERecipe()); } catch (Exception ex) { RecipeUtilities.LogDebug($"[RECIPE MANAGER] ❌ VTERecipe failed: {ex.Message}"); }
                try { _processors.Add(new PBPatternRecipe()); } catch (Exception ex) { RecipeUtilities.LogDebug($"[RECIPE MANAGER] ❌ PBPatternRecipe failed: {ex.Message}"); }
                try { _processors.Add(new TaraArmorRecipe()); } catch (Exception ex) { RecipeUtilities.LogDebug($"[RECIPE MANAGER] ❌ TaraArmorRecipe failed: {ex.Message}"); }
                try { _processors.Add(new MantisArmorRecipe()); } catch (Exception ex) { RecipeUtilities.LogDebug($"[RECIPE MANAGER] ❌ MantisArmorRecipe failed: {ex.Message}"); }
                try { _processors.Add(new CrawlerRecipe()); } catch (Exception ex) { RecipeUtilities.LogDebug($"[RECIPE MANAGER] ❌ CrawlerRecipe failed: {ex.Message}"); }
                try { _processors.Add(new ImplantRecipe()); } catch (Exception ex) { RecipeUtilities.LogDebug($"[RECIPE MANAGER] ❌ ImplantRecipe failed: {ex.Message}"); }
                try { _processors.Add(new ClumpsRecipe()); } catch (Exception ex) { RecipeUtilities.LogDebug($"[RECIPE MANAGER] ❌ ClumpsRecipe failed: {ex.Message}"); }
                try { _processors.Add(new AlienArmorRecipe()); } catch (Exception ex) { RecipeUtilities.LogDebug($"[RECIPE MANAGER] ❌ AlienArmorRecipe failed: {ex.Message}"); }
                try { _processors.Add(new TrimmerRecipe()); } catch (Exception ex) { RecipeUtilities.LogDebug($"[RECIPE MANAGER] ❌ TrimmerRecipe failed: {ex.Message}"); }
                try { _processors.Add(new DevalosSleeveRecipe()); } catch (Exception ex) { RecipeUtilities.LogDebug($"[RECIPE MANAGER] ❌ DevalosSleeveRecipe failed: {ex.Message}"); }
                try { _processors.Add(new PerenniumWeaponsRecipe()); } catch (Exception ex) { RecipeUtilities.LogDebug($"[RECIPE MANAGER] ❌ PerenniumWeaponsRecipe failed: {ex.Message}"); }
                try { _processors.Add(new HackGraftsRecipe()); } catch (Exception ex) { RecipeUtilities.LogDebug($"[RECIPE MANAGER] ❌ HackGraftsRecipe failed: {ex.Message}"); }
                try { _processors.Add(new PredatorCircletRecipe()); } catch (Exception ex) { RecipeUtilities.LogDebug($"[RECIPE MANAGER] ❌ PredatorCircletRecipe failed: {ex.Message}"); }
                try { _processors.Add(new SealedWeaponRecipe()); } catch (Exception ex) { RecipeUtilities.LogDebug($"[RECIPE MANAGER] ❌ SealedWeaponRecipe failed: {ex.Message}"); }

                RecipeUtilities.LogDebug($"[RECIPE MANAGER] ✅ Loaded {_processors.Count} recipe processors");
                _initialized = true;
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[RECIPE MANAGER] ❌ ERROR during initialization: {ex.Message}");
                RecipeUtilities.LogDebug($"[RECIPE MANAGER] Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Registers a recipe processor
        /// </summary>
        /// <param name="processor">Recipe processor to register</param>
        public static void RegisterProcessor(IRecipeProcessor processor)
        {
            if (!_processors.Contains(processor))
            {
                _processors.Add(processor);
                // Silent registration
            }
        }

        /// <summary>
        /// Finds a processor that can handle the given item
        /// </summary>
        /// <param name="item">Item to find processor for</param>
        /// <returns>Recipe processor that can handle the item, or null if none found</returns>
        public static IRecipeProcessor FindProcessorForItem(Item item)
        {
            string itemName = item?.Name ?? "NULL_ITEM";
            RecipeUtilities.LogDebug($"[RECIPE MANAGER DEBUG] Looking for processor for '{itemName}'");

            // Null safety checks
            if (item == null || string.IsNullOrEmpty(item.Name))
            {
                RecipeUtilities.LogWarning($"[RECIPE MANAGER DEBUG] Cannot find processor for null/empty item name");
                return null;
            }

            foreach (var processor in _processors)
            {
                try
                {
                    bool canProcess = processor.CanProcess(item);
                    RecipeUtilities.LogDebug($"[RECIPE MANAGER DEBUG] {processor.RecipeName}.CanProcess('{item.Name}') = {canProcess}");
                    if (canProcess)
                    {
                        RecipeUtilities.LogDebug($"[RECIPE MANAGER DEBUG] ✅ Found processor: {processor.RecipeName}");
                        return processor;
                    }
                }
                catch (Exception ex)
                {
                    RecipeUtilities.LogError($"[RECIPE MANAGER DEBUG] Error checking {processor.RecipeName} for item '{item.Name}': {ex.Message}");
                }
            }
            RecipeUtilities.LogDebug($"[RECIPE MANAGER DEBUG] ❌ No processor found for '{item.Name}'");
            return null;
        }

        /// <summary>
        /// UNIFIED CORE: Processes items using unified logic for both loose and bag processing
        /// </summary>
        /// <param name="item">Item to process (for loose items) or trigger item (for bag processing)</param>
        /// <param name="targetContainer">Container to return processed items to (null for loose items)</param>
        /// <returns>True if item was processed, false otherwise</returns>
        public static async Task<bool> ProcessItem(Item item, Container targetContainer)
        {
            string itemName = item?.Name ?? "NULL_ITEM";
            RecipeUtilities.LogDebug($"[RECIPE MANAGER] *** ENTRY POINT *** ProcessItem called for {itemName}");

            try
            {
                // Null safety checks
                if (item == null)
                {
                    RecipeUtilities.LogError("[RECIPE MANAGER] Item is null - cannot process");
                    return false;
                }

                if (string.IsNullOrEmpty(item.Name))
                {
                    RecipeUtilities.LogWarning($"[RECIPE MANAGER] Item has empty name (ID: {item.Id}) - cannot process");
                    return false;
                }

                RecipeUtilities.LogDebug($"[RECIPE MANAGER] ProcessItem called for {item.Name}, container: {targetContainer?.Item?.Name ?? "NULL"}");
                RecipeUtilities.LogDebug($"[RECIPE MANAGER] Available processors: {_processors.Count}");

                // Debug: List all processors
                for (int i = 0; i < _processors.Count; i++)
                {
                    RecipeUtilities.LogDebug($"[RECIPE MANAGER] Processor {i}: {_processors[i].RecipeName}");
                }

                bool result = await ProcessItemUnified(item, targetContainer, false);
                RecipeUtilities.LogDebug($"[RECIPE MANAGER] ProcessItem result for {item.Name}: {result}");
                return result;
            }
            catch (Exception ex)
            {
                // Log the error instead of silent handling
                RecipeUtilities.LogDebug($"[RECIPE MANAGER] ERROR in ProcessItem for {item.Name}: {ex.Message}");
                RecipeUtilities.LogDebug($"[RECIPE MANAGER] Stack trace: {ex.StackTrace}");
                return false;
            }
            finally
            {
                RecipeUtilities.LogDebug($"[RECIPE MANAGER] *** EXIT POINT *** ProcessItem finished for {item.Name}");
            }
        }

        /// <summary>
        /// UNIFIED CORE: Processes items with unified logic that handles both loose and bag cases
        /// </summary>
        /// <param name="item">Item to process</param>
        /// <param name="targetContainer">Container for bag processing (null for loose items)</param>
        /// <param name="isBagProcessing">True if this is part of bag processing, false for loose items</param>
        /// <returns>True if item was processed, false otherwise</returns>
        private static async Task<bool> ProcessItemUnified(Item item, Container targetContainer, bool isBagProcessing)
        {
            try
            {
                var processor = FindProcessorForItem(item);
                if (processor != null)
                {
                    if (targetContainer == null)
                    {
                        // LOOSE ITEM PROCESSING
                        RecipeUtilities.LogDebug($"[RECIPE MANAGER UNIFIED] Processing loose item {item.Name} with {processor.RecipeName}");
                        await processor.ProcessItem(item, null);
                    }
                    else
                    {
                        // BAG ITEM PROCESSING
                        RecipeUtilities.LogDebug($"[RECIPE MANAGER UNIFIED] Processing bag item {item.Name} with {processor.RecipeName} (bag: {targetContainer.Item?.Name ?? "Unknown"})");
                        await processor.ProcessItem(item, targetContainer);
                    }
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[RECIPE MANAGER UNIFIED] Error processing item {item.Name}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Analyzes a bag of items for recipe processing opportunities
        /// </summary>
        /// <param name="container">Container to analyze</param>
        /// <param name="bagContents">Items in the bag</param>
        /// <returns>Recipe analysis result</returns>
        public static RecipeAnalysisResult AnalyzeBagForRecipes(Container container, List<Item> bagContents)
        {
            try
            {
                RecipeUtilities.LogDebug($"[RECIPE MANAGER] Analyzing bag contents against all recipe processes...");

                // Check each processor in priority order
                foreach (var processor in _processors)
                {
                    var result = processor.AnalyzeItems(bagContents);
                    if (result.CanProcess)
                    {
                        RecipeUtilities.LogDebug($"[RECIPE MANAGER] {processor.RecipeName} can process {result.ProcessableItemCount} items");
                        return result;
                    }
                }

                // No recipe processors found items they can handle
                return new RecipeAnalysisResult
                {
                    CanProcess = false,
                    ProcessableItemCount = 0,
                    Stage = "None",
                    Description = "No recipe processors found for these items"
                };
            }
            catch (Exception ex)
            {
                // Silent error handling
                return new RecipeAnalysisResult
                {
                    CanProcess = false,
                    ProcessableItemCount = 0,
                    Stage = "Error",
                    Description = $"Error during analysis: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Gets all registered recipe processors
        /// </summary>
        /// <returns>List of all recipe processors</returns>
        public static List<IRecipeProcessor> GetAllProcessors()
        {
            return new List<IRecipeProcessor>(_processors);
        }

        /// <summary>
        /// Checks if any processor can handle the given item
        /// </summary>
        /// <param name="item">Item to check</param>
        /// <returns>True if any processor can handle the item</returns>
        public static bool CanProcessAnyItem(Item item)
        {
            return _processors.Any(processor => processor.CanProcess(item));
        }

        /// <summary>
        /// ULTRA-EFFICIENT bag processing with item grouping to eliminate redundant checks
        /// Groups identical items together and processes them as batches instead of individually
        /// </summary>
        /// <param name="container">Container to process</param>
        /// <returns>Number of items processed</returns>
        public static async Task<int> ProcessBagEfficiently(Container container)
        {
            try
            {
                RecipeUtilities.LogDebug($"[RECIPE MANAGER] Starting ULTRA-EFFICIENT bag processing for {container.Item?.Name ?? "Unknown"}");
                RecipeUtilities.LogDebug($"[RECIPE MANAGER] Initialized: {_initialized}, Processors count: {_processors.Count}");

                // Ensure initialization
                if (!_initialized)
                {
                    RecipeUtilities.LogDebug($"[RECIPE MANAGER] Not initialized, calling Initialize()");
                    Initialize();
                    RecipeUtilities.LogDebug($"[RECIPE MANAGER] After Initialize: Processors count: {_processors.Count}");
                }

                // 1. Get all items and GROUP BY NAME to eliminate redundant checks
                var allItems = container.Items.ToList();
                var itemGroups = allItems.GroupBy(item => item.Name).ToList();
                int totalProcessed = 0;

                RecipeUtilities.LogDebug($"[RECIPE MANAGER] Found {allItems.Count} total items grouped into {itemGroups.Count} unique item types");
                foreach (var group in itemGroups)
                {
                    RecipeUtilities.LogDebug($"[RECIPE MANAGER] - {group.Count()}x {group.Key}");
                }

                // 2. For each recipe processor (in priority order)
                foreach (var processor in _processors)
                {
                    RecipeUtilities.LogDebug($"[RECIPE MANAGER] Checking processor: {processor.RecipeName}");

                    // 3. Check each UNIQUE item type only ONCE instead of checking every individual item
                    var processableGroups = new List<IGrouping<string, Item>>();
                    int totalItemsForProcessor = 0;

                    foreach (var itemGroup in itemGroups)
                    {
                        // Test only ONE item from each group instead of all items
                        var sampleItem = itemGroup.First();
                        if (processor.CanProcess(sampleItem))
                        {
                            processableGroups.Add(itemGroup);
                            totalItemsForProcessor += itemGroup.Count();
                            RecipeUtilities.LogDebug($"[RECIPE MANAGER] {processor.RecipeName} can process {itemGroup.Count()}x {itemGroup.Key}");
                        }
                    }

                    RecipeUtilities.LogDebug($"[RECIPE MANAGER] {processor.RecipeName} found {totalItemsForProcessor} processable items across {processableGroups.Count} item types");

                    if (processableGroups.Any())
                    {
                        // Process all items of this type together
                        var allProcessableItems = processableGroups.SelectMany(group => group).ToList();

                        RecipeUtilities.LogDebug($"[RECIPE MANAGER] Using unified processing for {totalItemsForProcessor} {processor.RecipeName} items");

                        // FIX: Pass any item as trigger, but the BaseRecipeProcessor will handle ALL processable items in the container
                        // The processor's batch processing logic will find and process all items of the same type
                        await processor.ProcessItem(allProcessableItems.First(), container);
                        totalProcessed += totalItemsForProcessor;

                        // Remove processed item groups from the list
                        foreach (var processedGroup in processableGroups)
                        {
                            itemGroups.Remove(processedGroup);
                        }
                    }
                }

                RecipeUtilities.LogDebug($"[RECIPE MANAGER] ULTRA-EFFICIENT processing completed. Processed {totalProcessed} items.");

                // CRITICAL FIX: Return all tools to their original bags after ALL bag processing is complete
                RecipeUtilities.LogDebug($"[RECIPE MANAGER] ✅ All bag processing complete - returning tools to original bags");
                await RecipeUtilities.ReturnToolsToOriginalBags();

                return totalProcessed;
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[RECIPE MANAGER] Error in efficient bag processing: {ex.Message}");

                // CRITICAL FIX: Return tools even if there was an error during processing
                try
                {
                    RecipeUtilities.LogDebug($"[RECIPE MANAGER] ⚠️ Error occurred - attempting to return tools to original bags");
                    await RecipeUtilities.ReturnToolsToOriginalBags();
                }
                catch (Exception toolEx)
                {
                    RecipeUtilities.LogDebug($"[RECIPE MANAGER] Error returning tools after processing error: {toolEx.Message}");
                }

                return 0;
            }
        }
    }
}
