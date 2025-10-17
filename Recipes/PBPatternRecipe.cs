using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AOSharp.Clientless;
using SmokeLounge.AOtomation.Messaging.GameData;
using AOSharp.Common.GameData;

namespace Craftbot.Recipes
{
    /// <summary>
    /// Handles PB Pattern processing - a complex multi-step recipe involving pattern combinations and novictum processing
    /// </summary>
    public class PBPatternRecipe : BaseRecipeProcessor
    {
        public override string RecipeName => "PB Pattern";

        public enum PBPatternStage
        {
            None,
            Step1_AbhanPattern,     // Has Abhan Pattern (start 4-step combination)
            Step2_BhotaarPattern,   // Has Bhotaar Pattern (continue combination)
            Step3_ChiPattern,       // Has Chi Pattern (continue combination)
            Step4_DomPattern,       // Has Dom Pattern (continue combination)
            Step5_CompletedPattern, // Has completed 4-step pattern (start novictum processing)
            Step6_RawNovictum,      // Has raw novictum items (process with Ancient Novictum Refiner)
            Step7_SubduedNovictum,  // Has subdued novictum (ready for final enhancement)
            Step8_CrystalSource,    // Has Crystal filled by the source (combine with blueprint)
            Step9_CompleteBlueprint,// Has complete blueprint (combine with crystal)
            Step10_EtchedCrystal,   // Has etched notum crystal (combine with subdued novictum)
            Completed               // Has novictalized item (finished)
        }

        private static readonly string[] ValidPBPatternNames = {
            "Adobe Suzerain", "Aesma Daeva", "Agent of Decay", "Ahpta", "Alatyr", "Anya Aray",
            "Arch Bigot", "Biap Argil Suzerain", "Asase Ya", "Ashmara Ravin", "Ats", "Ats'usk",
            "Arete", "Ariadne", "Artemis", "Ashen Maiden", "Astarte", "Athena", "Atropos",
            "Baba Yaga", "Banshee", "Bastet", "Baubo", "Bellona", "Berchta", "Brigid",
            "Cailleach", "Cerridwen", "Churn", "Circe", "Clotho", "Coatlicue", "Cybele",
            "Demeter", "Diana", "Durga", "Ereshkigal", "Eris", "Europa", "Fortuna",
            "Freya", "Gaia", "Hecate", "Hel", "Hera", "Hestia", "Inanna", "Iris",
            "Ishtar", "Isis", "Juno", "Kali", "Lakshmi", "Leda", "Lilith", "Luna",
            "Maat", "Maeve", "Morrigan", "Nemesis", "Nike", "Nyx", "Oshun", "Pandora",
            "Persephone", "Rhea", "Saraswati", "Selene", "Sekhmet", "Shakti", "Shiva",
            "Tara", "Themis", "Tiamat", "Tyche", "Vesta", "Victoria", "Yemoja"
        };

        public override bool CanProcess(Item item)
        {
            string itemName = item.Name.ToLower();

            // Check for any PB pattern component
            // 1. Pattern items (Abhan, Bhotaar, Chi, Dom patterns) - FIXED: Handle both formats
            if (itemName.Contains("pattern:") ||
                itemName.Contains("abhan pattern") ||
                itemName.Contains("bhotaar pattern") ||
                itemName.Contains("chi pattern") ||
                itemName.Contains("dom pattern"))
                return true;

            // 2. Assembly items (intermediate combinations that need further processing)
            if (itemName.Contains("assembly") &&
                (itemName.Contains("aban") || itemName.Contains("bhotar") || itemName.Contains("chi")))
                return true;

            // 3. Completed PB patterns (goddess names)
            if (IsCompletedPBPattern(item))
                return true;

            // 4. Novictum items (but not rings)
            if (itemName.Contains("novictum") && !itemName.Contains("novictum ring") && !itemName.Contains("pure novictum ring"))
                return true;

            // 5. Crystal items (various types)
            if (itemName.Contains("crystal filled by") ||
                (itemName.Contains("crystal") && itemName.Contains("source")))
                return true;

            // 6. Blueprint items
            if (itemName.Contains("blueprint"))
                return true;

            // 7. Etched notum crystal
            if (itemName.Contains("notum crystal") && itemName.Contains("etched"))
                return true;

            return false;
        }

        protected override async Task ProcessRecipeLogic(Item item, Container targetContainer)
        {
            RecipeUtilities.LogDebug($"[{RecipeName}] Starting PB pattern processing for {item.Name}");

            // NOTE: BaseRecipeProcessor now handles moving ALL processable items to inventory
            // No need to call MovePBPatternComponentsToInventory - items are already in inventory

            // Detect current stage and process accordingly
            var currentStage = DetectPBPatternStageFromInventory();
            RecipeUtilities.LogDebug($"[{RecipeName}] Current PB pattern stage: {currentStage}");

            // Process based on current stage
            await ProcessPBPatternStages(currentStage, targetContainer);

            RecipeUtilities.LogDebug($"[{RecipeName}] Completed PB pattern processing!");
        }

        public override RecipeAnalysisResult AnalyzeItems(List<Item> items)
        {
            var stage = DetectPBPatternStageFromItems(items);
            
            return new RecipeAnalysisResult
            {
                CanProcess = stage != PBPatternStage.None,
                ProcessableItemCount = items.Where(CanProcess).Count(),
                Stage = stage.ToString(),
                Description = $"PB Pattern Stage: {stage} - {items.Where(CanProcess).Count()} PB components found"
            };
        }

        private async Task ProcessPBPatternStages(PBPatternStage currentStage, Container targetContainer)
        {
            switch (currentStage)
            {
                case PBPatternStage.Step1_AbhanPattern:
                case PBPatternStage.Step2_BhotaarPattern:
                case PBPatternStage.Step3_ChiPattern:
                case PBPatternStage.Step4_DomPattern:
                    // PARTIAL PROCESSING: Only combine available patterns, don't force full workflow
                    var allPatternNames = ExtractAllPatternNamesFromInventory();
                    RecipeUtilities.LogDebug($"[{RecipeName}] Found {allPatternNames.Count} unique patterns to process: {string.Join(", ", allPatternNames)}");

                    foreach (var patternName in allPatternNames)
                    {
                        RecipeUtilities.LogDebug($"[{RecipeName}] Stage 1: Processing available patterns for {patternName}");
                        await ProcessAvailablePatternCombinations(patternName);
                    }
                    break;

                case PBPatternStage.Step5_CompletedPattern:
                    RecipeUtilities.LogDebug($"[{RecipeName}] Stage 2: Found completed pattern, checking if novictum processing is needed");
                    // Only process novictum if player provided novictum items
                    if (HasNovictumItems())
                    {
                        RecipeUtilities.LogDebug($"[{RecipeName}] Player provided novictum items - continuing to novictum processing");
                        await ProcessNovictumSteps(targetContainer);
                    }
                    else
                    {
                        RecipeUtilities.LogDebug($"[{RecipeName}] No novictum items provided - pattern combination complete");
                    }
                    break;

                case PBPatternStage.Step6_RawNovictum:
                case PBPatternStage.Step7_SubduedNovictum:
                case PBPatternStage.Step8_CrystalSource:
                case PBPatternStage.Step9_CompleteBlueprint:
                case PBPatternStage.Step10_EtchedCrystal:
                    RecipeUtilities.LogDebug($"[{RecipeName}] Processing novictum stages");
                    await ProcessNovictumSteps(targetContainer);
                    break;

                case PBPatternStage.Completed:
                    RecipeUtilities.LogDebug($"[{RecipeName}] PB pattern already completed!");
                    break;

                default:
                    RecipeUtilities.LogDebug($"[{RecipeName}] Unknown PB pattern stage: {currentStage}");
                    break;
            }
        }

        private PBPatternStage DetectPBPatternStage(Container targetContainer)
        {
            var bagItems = targetContainer.Items.ToList();
            var inventoryItems = Inventory.Items.Where(item =>
                item.Slot.Type == IdentityType.Inventory).ToList();
            var allItems = bagItems.Concat(inventoryItems).ToList();

            return DetectPBPatternStageFromItems(allItems);
        }

        private PBPatternStage DetectPBPatternStageFromInventory()
        {
            // NEW METHOD: Only check inventory items since BaseRecipeProcessor moved all items there
            var inventoryItems = Inventory.Items.Where(item =>
                item.Slot.Type == IdentityType.Inventory && !(item.Slot.Instance >= (int)EquipSlot.Weap_Hud1 && item.Slot.Instance <= (int)EquipSlot.Imp_Feet)).ToList();

            return DetectPBPatternStageFromItems(inventoryItems);
        }

        private PBPatternStage DetectPBPatternStageFromItems(List<Item> items)
        {
            // Check for completed PB pattern first
            if (items.Any(item => item.Name.ToLower().Contains("novictalized")))
                return PBPatternStage.Completed;

            // Check Step 10 - Etched crystal ready for final combination
            if (items.Any(item => item.Name.ToLower().Contains("etched notum crystal")))
                return PBPatternStage.Step10_EtchedCrystal;

            // Check Step 9 - Complete blueprint ready for crystal combination
            if (items.Any(item => item.Name.ToLower().Contains("complete blueprint")))
                return PBPatternStage.Step9_CompleteBlueprint;

            // Check Step 8 - Crystal filled by the source ready for blueprint combination
            if (items.Any(item => item.Name.Equals("Crystal filled by the source", StringComparison.OrdinalIgnoreCase)))
                return PBPatternStage.Step8_CrystalSource;

            // Check Step 7 - Subdued novictum ready for final enhancement
            if (items.Any(item => item.Name.ToLower().Contains("subdued") && item.Name.ToLower().Contains("novictum")))
                return PBPatternStage.Step7_SubduedNovictum;

            // Check Step 6 - Raw novictum items needing Ancient Novictum Refiner processing
            if (items.Any(item => item.Name.ToLower().Contains("novictum") && !item.Name.ToLower().Contains("subdued") && !item.Name.ToLower().Contains("novictum ring")))
                return PBPatternStage.Step6_RawNovictum;

            // Check Step 5 - Completed 4-step pattern ready for novictum processing
            if (items.Any(item => IsCompletedPBPattern(item)))
                return PBPatternStage.Step5_CompletedPattern;

            // Check Steps 1-4 - Pattern combinations - FIXED: Handle both formats and assemblies
            // If we have assemblies + remaining patterns, continue at the highest pattern stage
            bool hasAssembly = items.Any(item => item.Name.ToLower().Contains("assembly"));

            if (items.Any(item => item.Name.ToLower().Contains("dom pattern:") || item.Name.ToLower().Contains("dom pattern")) || hasAssembly)
                return PBPatternStage.Step4_DomPattern;

            if (items.Any(item => item.Name.ToLower().Contains("chi pattern:") || item.Name.ToLower().Contains("chi pattern")))
                return PBPatternStage.Step3_ChiPattern;

            if (items.Any(item => item.Name.ToLower().Contains("bhotaar pattern:") || item.Name.ToLower().Contains("bhotaar pattern")))
                return PBPatternStage.Step2_BhotaarPattern;

            if (items.Any(item => item.Name.ToLower().Contains("abhan pattern:") || item.Name.ToLower().Contains("abhan pattern")))
                return PBPatternStage.Step1_AbhanPattern;

            return PBPatternStage.None;
        }

        private bool IsCompletedPBPattern(Item item)
        {
            string itemName = item.Name.ToLower();

            // FIXED: Exclude intermediate assemblies - they should continue processing
            if (itemName.Contains("assembly"))
            {
                return false; // Assemblies are intermediate steps, not completed patterns
            }

            // Look for items that contain pattern names from our valid list
            foreach (string patternName in ValidPBPatternNames)
            {
                if (itemName.Contains(patternName.ToLower()))
                {
                    // Additional checks to ensure it's a completed pattern, not just a raw pattern
                    if (!itemName.Contains("pattern:") &&
                        !itemName.Contains("abhan") &&
                        !itemName.Contains("bhotaar") &&
                        !itemName.Contains("chi") &&
                        !itemName.Contains("dom"))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // COMMENTED OUT: No longer needed - BaseRecipeProcessor now handles moving all items to inventory
        /*
        private async Task MovePBPatternComponentsToInventory(Container targetContainer)
        {
            try
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Moving all PB pattern components to inventory");

                var componentsToMove = new List<Item>();

                // Add pattern items
                componentsToMove.AddRange(targetContainer.Items.Where(item =>
                    item.Name.ToLower().Contains("pattern:") ||
                    (item.Name.ToLower().Contains("novictum") && !item.Name.ToLower().Contains("novictum ring") && !item.Name.ToLower().Contains("pure novictum ring")) ||
                    item.Name.ToLower().Contains("crystal filled by") ||
                    (item.Name.ToLower().Contains("crystal") && item.Name.ToLower().Contains("source")) ||
                    item.Name.ToLower().Contains("blueprint") ||
                    (item.Name.ToLower().Contains("notum crystal") && item.Name.ToLower().Contains("etched")) ||
                    IsCompletedPBPattern(item)));

                // OVERFLOW PROTECTION: Check if moving all components is safe
                if (!Modules.PrivateMessageModule.CheckInventorySpaceForProcessing(componentsToMove.Count, RecipeName))
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] ❌ OVERFLOW PROTECTION: Cannot move {componentsToMove.Count} PB components - insufficient inventory space");
                    return; // Abort moving to prevent overflow
                }

                foreach (var component in componentsToMove)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Moving {component.Name} to inventory");
                    component.MoveToInventory();
                    await Task.Delay(100);
                }

                RecipeUtilities.LogDebug($"[{RecipeName}] Moved {componentsToMove.Count} PB pattern components to inventory");
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error moving PB pattern components: {ex.Message}");
            }
        }
        */

        private string ExtractPatternNameFromInventory()
        {
            try
            {
                // Look for any pattern in inventory and extract the pattern name
                var patternItem = Inventory.Items.FirstOrDefault(item => item.Name.ToLower().Contains("pattern:"));
                if (patternItem != null)
                {
                    // Extract pattern name from "Type Pattern: Name" format
                    var parts = patternItem.Name.Split(':');
                    if (parts.Length > 1)
                    {
                        var patternName = parts[1].Trim();
                        RecipeUtilities.LogDebug($"[{RecipeName}] Extracted pattern name: {patternName}");
                        return patternName;
                    }
                }

                RecipeUtilities.LogDebug($"[{RecipeName}] No pattern name could be extracted from inventory");
                return "";
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error extracting pattern name: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// FIX: Extract ALL unique pattern names from inventory to support batch processing
        /// </summary>
        private List<string> ExtractAllPatternNamesFromInventory()
        {
            var patternNames = new List<string>();

            try
            {
                // Find all pattern items in inventory - FIXED: Handle both formats
                var patternItems = Inventory.Items.Where(item =>
                    item.Name.ToLower().Contains("pattern:") ||
                    item.Name.ToLower().Contains("abhan pattern") ||
                    item.Name.ToLower().Contains("bhotaar pattern") ||
                    item.Name.ToLower().Contains("chi pattern") ||
                    item.Name.ToLower().Contains("dom pattern")).ToList();

                foreach (var patternItem in patternItems)
                {
                    string patternName = null;

                    // Handle "Type Pattern: Name" format
                    if (patternItem.Name.Contains(":"))
                    {
                        var parts = patternItem.Name.Split(':');
                        if (parts.Length > 1)
                        {
                            patternName = parts[1].Trim();
                        }
                    }
                    // Handle "Type Pattern 'Name'" format
                    else if (patternItem.Name.Contains("'"))
                    {
                        var startQuote = patternItem.Name.IndexOf("'");
                        var endQuote = patternItem.Name.LastIndexOf("'");
                        if (startQuote >= 0 && endQuote > startQuote)
                        {
                            patternName = patternItem.Name.Substring(startQuote + 1, endQuote - startQuote - 1);
                        }
                    }
                    // Handle "Type Pattern of 'Name'" format
                    else if (patternItem.Name.ToLower().Contains(" of '"))
                    {
                        var ofIndex = patternItem.Name.ToLower().IndexOf(" of '");
                        var startQuote = patternItem.Name.IndexOf("'", ofIndex);
                        var endQuote = patternItem.Name.LastIndexOf("'");
                        if (startQuote >= 0 && endQuote > startQuote)
                        {
                            patternName = patternItem.Name.Substring(startQuote + 1, endQuote - startQuote - 1);
                        }
                    }

                    if (!string.IsNullOrEmpty(patternName) && !patternNames.Contains(patternName))
                    {
                        patternNames.Add(patternName);
                        RecipeUtilities.LogDebug($"[{RecipeName}] Extracted pattern name: '{patternName}' from '{patternItem.Name}'");
                    }
                }

                RecipeUtilities.LogDebug($"[{RecipeName}] Extracted {patternNames.Count} unique pattern names from inventory");
                return patternNames;
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error extracting pattern names: {ex.Message}");
                return patternNames;
            }
        }

        private bool HasNovictumItems()
        {
            return Inventory.Items.Any(item =>
                item.Name.ToLower().Contains("novictum") &&
                !item.Name.ToLower().Contains("novictum ring") &&
                !item.Name.ToLower().Contains("pure novictum ring"));
        }

        private async Task ProcessAvailablePatternCombinations(string patternName)
        {
            try
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Processing available pattern combinations for {patternName}");

                // Get all available patterns for this pattern name - FIXED: Handle both formats
                var abhanPatterns = Inventory.Items.Where(item =>
                    (item.Name.ToLower().Contains("abhan pattern:") || item.Name.ToLower().Contains("abhan pattern")) &&
                    item.Name.ToLower().Contains(patternName.ToLower())).ToList();

                var bhotaarPatterns = Inventory.Items.Where(item =>
                    (item.Name.ToLower().Contains("bhotaar pattern:") || item.Name.ToLower().Contains("bhotaar pattern")) &&
                    item.Name.ToLower().Contains(patternName.ToLower())).ToList();

                var chiPatterns = Inventory.Items.Where(item =>
                    (item.Name.ToLower().Contains("chi pattern:") || item.Name.ToLower().Contains("chi pattern")) &&
                    item.Name.ToLower().Contains(patternName.ToLower())).ToList();

                var domPatterns = Inventory.Items.Where(item =>
                    (item.Name.ToLower().Contains("dom pattern:") || item.Name.ToLower().Contains("dom pattern")) &&
                    item.Name.ToLower().Contains(patternName.ToLower())).ToList();

                // FIXED: Also look for assemblies that can be combined further
                var assemblies = Inventory.Items.Where(item =>
                    item.Name.ToLower().Contains("assembly") &&
                    item.Name.ToLower().Contains(patternName.ToLower())).ToList();

                RecipeUtilities.LogDebug($"[{RecipeName}] Available patterns for {patternName}: Abhan={abhanPatterns.Count}, Bhotaar={bhotaarPatterns.Count}, Chi={chiPatterns.Count}, Dom={domPatterns.Count}, Assemblies={assemblies.Count}");

                // PRIORITY FIX: Process existing assemblies FIRST before starting new complete sets
                // This ensures existing work is finished before starting new combinations

                // Step 1: Process existing 2-step assemblies with Chi patterns
                var twoStepAssemblies = Inventory.Items.Where(item =>
                    item.Name.ToLower().Contains(patternName.ToLower()) &&
                    item.Name.ToLower().Contains("assembly") &&
                    !item.Name.ToLower().Contains("chi") &&
                    !item.Name.ToLower().Contains("dom")).ToList();

                while (twoStepAssemblies.Any() && chiPatterns.Any())
                {
                    var assembly = twoStepAssemblies.First();
                    var chi = chiPatterns.First();

                    RecipeUtilities.LogDebug($"[{RecipeName}] PRIORITY: Combining {chi.Name} with existing 2-step {assembly.Name}");
                    await CombineItems(chi, assembly);
                    await Task.Delay(100);

                    twoStepAssemblies.Remove(assembly);
                    chiPatterns.Remove(chi);
                }

                // Step 2: Process existing 3-step assemblies with Dom patterns
                var threeStepAssemblies = Inventory.Items.Where(item =>
                    item.Name.ToLower().Contains(patternName.ToLower()) &&
                    item.Name.ToLower().Contains("assembly") &&
                    item.Name.ToLower().Contains("chi") &&
                    !item.Name.ToLower().Contains("dom")).ToList();

                while (threeStepAssemblies.Any() && domPatterns.Any())
                {
                    var assembly = threeStepAssemblies.First();
                    var dom = domPatterns.First();

                    RecipeUtilities.LogDebug($"[{RecipeName}] PRIORITY: Combining {dom.Name} with existing 3-step {assembly.Name}");
                    await CombineItems(dom, assembly);
                    await Task.Delay(100);

                    threeStepAssemblies.Remove(assembly);
                    domPatterns.Remove(dom);
                }

                // Step 3: AFTER finishing existing assemblies, calculate complete sets from remaining patterns
                int completeSets = Math.Min(Math.Min(abhanPatterns.Count, bhotaarPatterns.Count), Math.Min(chiPatterns.Count, domPatterns.Count));

                if (completeSets > 0)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Processing {completeSets} complete sets for {patternName}");

                    for (int i = 0; i < completeSets; i++)
                    {
                        // Process one complete set: A + B + C + D
                        var abhan = abhanPatterns[i];
                        var bhotaar = bhotaarPatterns[i];
                        var chi = chiPatterns[i];
                        var dom = domPatterns[i];

                        RecipeUtilities.LogDebug($"[{RecipeName}] Processing complete set {i + 1}: {abhan.Name} + {bhotaar.Name} + {chi.Name} + {dom.Name}");

                        // Step 1: Combine Abhan + Bhotaar
                        RecipeUtilities.LogDebug($"[{RecipeName}] Combining {bhotaar.Name} with {abhan.Name}");
                        await CombineItems(bhotaar, abhan);
                        await Task.Delay(100);

                        // Step 2: Find the assembly and combine with Chi
                        var assembly = Inventory.Items.FirstOrDefault(item =>
                            item.Name.ToLower().Contains("assembly") &&
                            item.Name.ToLower().Contains(patternName.ToLower()));

                        if (assembly != null)
                        {
                            RecipeUtilities.LogDebug($"[{RecipeName}] Combining {chi.Name} with {assembly.Name}");
                            await CombineItems(chi, assembly);
                            await Task.Delay(100);

                            // Step 3: Find the new assembly and combine with Dom
                            var newAssembly = Inventory.Items.FirstOrDefault(item =>
                                item.Name.ToLower().Contains("assembly") &&
                                item.Name.ToLower().Contains(patternName.ToLower()));

                            if (newAssembly != null)
                            {
                                RecipeUtilities.LogDebug($"[{RecipeName}] Combining {dom.Name} with {newAssembly.Name}");
                                await CombineItems(dom, newAssembly);
                                await Task.Delay(100);
                            }
                        }
                    }

                    // Remove processed patterns from lists
                    for (int i = completeSets - 1; i >= 0; i--)
                    {
                        abhanPatterns.RemoveAt(i);
                        bhotaarPatterns.RemoveAt(i);
                        chiPatterns.RemoveAt(i);
                        domPatterns.RemoveAt(i);
                    }

                    // CRITICAL FIX: After complete set processing, refresh lists to find leftover assemblies and patterns
                    RecipeUtilities.LogDebug($"[{RecipeName}] Refreshing pattern lists after complete set processing");

                    // Refresh Dom patterns list (may have leftovers)
                    domPatterns = Inventory.Items.Where(item =>
                        (item.Name.ToLower().Contains("dom pattern:") || item.Name.ToLower().Contains("dom pattern")) &&
                        item.Name.ToLower().Contains(patternName.ToLower())).ToList();

                    // Find any leftover assemblies that need Dom patterns
                    assemblies = Inventory.Items.Where(item =>
                        item.Name.ToLower().Contains("assembly") &&
                        item.Name.ToLower().Contains(patternName.ToLower())).ToList();

                    RecipeUtilities.LogDebug($"[{RecipeName}] After complete sets: Leftover Dom={domPatterns.Count}, Leftover Assemblies={assemblies.Count}");

                    // Combine leftover assemblies with leftover Dom patterns
                    while (assemblies.Any() && domPatterns.Any())
                    {
                        var assembly = assemblies.First();
                        var dom = domPatterns.First();

                        RecipeUtilities.LogDebug($"[{RecipeName}] Combining leftover {dom.Name} with {assembly.Name}");
                        await CombineItems(dom, assembly);
                        await Task.Delay(100);

                        assemblies.Remove(assembly);
                        domPatterns.Remove(dom);
                    }
                }

                // Process remaining partial combinations
                // Step 1: Combine remaining Abhan + Bhotaar if both are available
                while (abhanPatterns.Any() && bhotaarPatterns.Any())
                {
                    var abhan = abhanPatterns.First();
                    var bhotaar = bhotaarPatterns.First();

                    RecipeUtilities.LogDebug($"[{RecipeName}] Combining remaining {bhotaar.Name} with {abhan.Name}");
                    await CombineItems(bhotaar, abhan);
                    await Task.Delay(100);

                    abhanPatterns.Remove(abhan);
                    bhotaarPatterns.Remove(bhotaar);
                }

                // Assembly processing moved to beginning - no duplicate processing needed here

                RecipeUtilities.LogDebug($"[{RecipeName}] Completed available pattern combinations for {patternName}");
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error in pattern combination: {ex.Message}");
            }
        }

        private async Task ProcessFourStepPatternCombination(string patternName)
        {
            try
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Starting 4-step pattern combination for {patternName}");

                int processedSets = 0;

                // Keep processing until no more complete sets are available
                while (true)
                {
                    // Re-scan inventory for available patterns (since they get consumed)
                    var abhanPatterns = Inventory.Items.Where(item =>
                        item.Name.ToLower().Contains("abhan pattern:") &&
                        item.Name.ToLower().Contains(patternName.ToLower())).ToList();
                    var bhotaarPatterns = Inventory.Items.Where(item =>
                        item.Name.ToLower().Contains("bhotaar pattern:") &&
                        item.Name.ToLower().Contains(patternName.ToLower())).ToList();
                    var chiPatterns = Inventory.Items.Where(item =>
                        item.Name.ToLower().Contains("chi pattern:") &&
                        item.Name.ToLower().Contains(patternName.ToLower())).ToList();
                    var domPatterns = Inventory.Items.Where(item =>
                        item.Name.ToLower().Contains("dom pattern:") &&
                        item.Name.ToLower().Contains(patternName.ToLower())).ToList();

                    // Check if we can make at least one complete set
                    if (abhanPatterns.Count == 0 || bhotaarPatterns.Count == 0 ||
                        chiPatterns.Count == 0 || domPatterns.Count == 0)
                    {
                        RecipeUtilities.LogDebug($"[{RecipeName}] No more complete sets available - processed {processedSets} total sets");
                        RecipeUtilities.LogDebug($"[{RecipeName}] Remaining: Abhan: {abhanPatterns.Count}, Bhotaar: {bhotaarPatterns.Count}, Chi: {chiPatterns.Count}, Dom: {domPatterns.Count}");
                        break;
                    }

                    processedSets++;
                    RecipeUtilities.LogDebug($"[{RecipeName}] Processing set {processedSets} of {patternName}");

                    // Process one complete set: Abhan + Bhotaar -> Chi -> Dom
                    await ProcessSingleCompletePatternSet(patternName);

                    // Small delay between sets for stability
                    await Task.Delay(200);
                }

                RecipeUtilities.LogDebug($"[{RecipeName}] Completed 4-step pattern combination for {processedSets} sets of {patternName}");
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error in 4-step combination: {ex.Message}");
            }
        }

        private async Task ProcessSingleCompletePatternSet(string patternName)
        {
            try
            {
                // Step 1: Get one Abhan pattern
                var abhanPattern = Inventory.Items.FirstOrDefault(item =>
                    item.Name.ToLower().Contains("abhan pattern:") &&
                    item.Name.ToLower().Contains(patternName.ToLower()));

                // Step 2: Get one Bhotaar pattern and combine with Abhan
                var bhotaarPattern = Inventory.Items.FirstOrDefault(item =>
                    item.Name.ToLower().Contains("bhotaar pattern:") &&
                    item.Name.ToLower().Contains(patternName.ToLower()));

                if (abhanPattern != null && bhotaarPattern != null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step 2: Combining {bhotaarPattern.Name} with {abhanPattern.Name}");
                    await CombineItems(bhotaarPattern, abhanPattern);
                    await Task.Delay(100);
                }

                // Step 3: Find the result from step 2 and combine with Chi pattern
                var chiPattern = Inventory.Items.FirstOrDefault(item =>
                    item.Name.ToLower().Contains("chi pattern:") &&
                    item.Name.ToLower().Contains(patternName.ToLower()));

                var step2Result = Inventory.Items.FirstOrDefault(item =>
                    item.Name.ToLower().Contains(patternName.ToLower()) &&
                    !item.Name.ToLower().Contains("pattern:"));

                if (chiPattern != null && step2Result != null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step 3: Combining {chiPattern.Name} with {step2Result.Name}");
                    await CombineItems(chiPattern, step2Result);
                    await Task.Delay(100);
                }

                // Step 4: Find the result from step 3 and combine with Dom pattern
                var domPattern = Inventory.Items.FirstOrDefault(item =>
                    item.Name.ToLower().Contains("dom pattern:") &&
                    item.Name.ToLower().Contains(patternName.ToLower()));

                var step3Result = Inventory.Items.FirstOrDefault(item =>
                    item.Name.ToLower().Contains(patternName.ToLower()) &&
                    !item.Name.ToLower().Contains("pattern:"));

                if (domPattern != null && step3Result != null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step 4: Combining {domPattern.Name} with {step3Result.Name}");
                    await CombineItems(domPattern, step3Result);
                    await Task.Delay(100);
                }

                RecipeUtilities.LogDebug($"[{RecipeName}] Completed one set of {patternName} pattern combination");
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error processing single pattern set: {ex.Message}");
            }
        }

        private async Task ProcessPBPatternStepFromInventory(string patternName, string stepType)
        {
            try
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Looking for {stepType} Pattern: {patternName} in inventory");

                // Find the FIRST available pattern of this type
                var stepPattern = Inventory.Items.FirstOrDefault(item =>
                    item.Name.ToLower().Contains($"{stepType.ToLower()} pattern:") &&
                    item.Name.ToLower().Contains(patternName.ToLower()));

                if (stepPattern != null)
                {
                    // Find the most recent result item (should be the latest combination result)
                    var currentResult = Inventory.Items.LastOrDefault(item =>
                        item.Name.ToLower().Contains(patternName.ToLower()) &&
                        !item.Name.ToLower().Contains("pattern:"));

                    if (currentResult == null)
                    {
                        // If no result yet, look for the Abhan pattern as starting point
                        currentResult = Inventory.Items.FirstOrDefault(item =>
                            item.Name.ToLower().Contains("abhan pattern:") &&
                            item.Name.ToLower().Contains(patternName.ToLower()));
                    }

                    if (currentResult != null)
                    {
                        RecipeUtilities.LogDebug($"[{RecipeName}] Combining {stepPattern.Name} with {currentResult.Name}");
                        await CombineItems(stepPattern, currentResult);
                    }
                    else
                    {
                        RecipeUtilities.LogDebug($"[{RecipeName}] No current result found to combine with {stepType} pattern");
                    }
                }
                else
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] {stepType} Pattern: {patternName} not found in inventory");
                }
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error in {stepType} step: {ex.Message}");
            }
        }

        private async Task ProcessPBPatternStepWithSpecificBase(string patternName, string stepType, Item baseItem)
        {
            try
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Looking for {stepType} Pattern: {patternName} to combine with specific base: {baseItem.Name}");

                // Find the FIRST available pattern of this type
                var stepPattern = Inventory.Items.FirstOrDefault(item =>
                    item.Name.ToLower().Contains($"{stepType.ToLower()} pattern:") &&
                    item.Name.ToLower().Contains(patternName.ToLower()));

                if (stepPattern != null && baseItem != null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Combining {stepPattern.Name} with {baseItem.Name}");
                    await CombineItems(stepPattern, baseItem);
                }
                else
                {
                    if (stepPattern == null)
                        RecipeUtilities.LogDebug($"[{RecipeName}] {stepType} Pattern: {patternName} not found in inventory");
                    if (baseItem == null)
                        RecipeUtilities.LogDebug($"[{RecipeName}] Base item is null");
                }
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error in {stepType} step with specific base: {ex.Message}");
            }
        }

        private async Task ProcessNovictumSteps(Container targetContainer)
        {
            try
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Processing novictum enhancement steps");

                // Step 6: Process raw novictum with Ancient Novictum Refiner
                var rawNovictum = Inventory.Items.Where(item =>
                    item.Name.ToLower().Contains("novictum") &&
                    !item.Name.ToLower().Contains("subdued") &&
                    !item.Name.ToLower().Contains("novictum ring")).ToList();

                var ancientRefiner = FindTool("Ancient Novictum Refiner");

                if (ancientRefiner != null && rawNovictum.Any())
                {
                    foreach (var novictum in rawNovictum)
                    {
                        if (!novictum.Name.ToLower().Contains("subdued"))
                        {
                            RecipeUtilities.LogDebug($"[{RecipeName}] Processing {novictum.Name} with Ancient Novictum Refiner");
                            await CombineItems(ancientRefiner, novictum);
                        }
                    }
                }

                await Task.Delay(200);

                // Step 8-9: Combine Crystal filled by the source with complete blueprints
                var crystalSource = Inventory.Items.FirstOrDefault(item =>
                    item.Name.Equals("Crystal filled by the source", StringComparison.OrdinalIgnoreCase) ||
                    (item.Name.ToLower().Contains("crystal") && item.Name.ToLower().Contains("source")));
                var completeBlueprint = Inventory.Items.FirstOrDefault(item =>
                    item.Name.ToLower().Contains("complete blueprint"));

                if (crystalSource != null && completeBlueprint != null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Combining crystal with blueprint");
                    await CombineItems(crystalSource, completeBlueprint);
                }

                await Task.Delay(200);

                // Step 10: Combine subdued novictum with etched notum crystal
                var subduedNovictum = Inventory.Items.FirstOrDefault(item =>
                    item.Name.ToLower().Contains("subdued") && item.Name.ToLower().Contains("novictum"));
                var etchedCrystal = Inventory.Items.FirstOrDefault(item =>
                    item.Name.ToLower().Contains("notum crystal") && item.Name.ToLower().Contains("etched"));

                if (subduedNovictum != null && etchedCrystal != null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Final combination: subdued novictum with etched crystal");
                    await CombineItems(subduedNovictum, etchedCrystal);
                }

                RecipeUtilities.LogDebug($"[{RecipeName}] Completed novictum processing steps");
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error processing novictum steps: {ex.Message}");
            }
        }


    }
}
