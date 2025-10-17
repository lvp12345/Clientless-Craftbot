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
    /// Handles Perennium Weapons recipe processing - a complex 4-step recipe with 3 weapon variants
    /// </summary>
    public class PerenniumWeaponsRecipe : BaseRecipeProcessor
    {
        public override string RecipeName => "Perennium Weapons";

        public enum PerenniumStage
        {
            None,
            RawMaterials,           // Raw components (Sheet of Perennium, Spirit Tech Apparatus, Premium weapons)
            Step1_PerenniumParts,   // Has Perennium weapon parts (continue from Step 2)
            Step2_HackedWeapons,    // Has Hacked weapons (continue from Step 3)
            Step3_HalfFinished,     // Has Half-Finished weapons (continue from Step 4)
            Completed               // Has Superior weapons (already done)
        }

        public override bool CanProcess(Item item)
        {
            // Check if this item can be part of Perennium processing (raw materials OR partially processed items)
            return item.Name.Equals("Sheet of Perennium", StringComparison.OrdinalIgnoreCase) ||
                   item.Name.Contains("Spirit Tech Apparatus") ||
                   item.Name.Equals("Advanced Hacker Tool", StringComparison.OrdinalIgnoreCase) ||
                   item.Name.Contains("Premium Nano-Charged") ||
                   item.Name.Contains("Deluxe Nano-Charged") ||
                   (item.Name.Contains("Nano-Charged") && !item.Name.Contains("Hacked")) ||
                   item.Name.Contains("Double Perennium Barrel") ||
                   item.Name.Contains("Long Perennium Muzzle") ||
                   item.Name.Contains("Short Perennium Muzzle") ||
                   item.Name.Contains("Hacked Nano-Charged") ||
                   item.Name.Contains("Half-Finished Perennium") ||
                   item.Name.Equals("Perennium Bolts", StringComparison.OrdinalIgnoreCase) ||
                   item.Name.Contains("Superior Perennium");
        }

        protected override async Task ProcessRecipeLogic(Item item, Container targetContainer)
        {
            RecipeUtilities.LogCritical($"🔧 Starting Perennium Weapons processing for {item.Name}");

            // Move Perennium components to inventory for processing (Perennium needs all components moved)
            if (targetContainer != null)
            {
                await MoveComponentsToInventoryShared(targetContainer);
            }

            // Detect current stage and process accordingly
            var currentStage = DetectPerenniumStage(targetContainer);
            RecipeUtilities.LogDebug($"[{RecipeName}] Current Perennium stage after moving components: {currentStage}");
            RecipeUtilities.LogCritical($"📍 Perennium Stage: {currentStage}");

            // Process each step in sequence based on current stage
            await ProcessPerenniumStages(currentStage, targetContainer);

            RecipeUtilities.LogDebug($"[{RecipeName}] Completed Perennium weapons creation process!");
            RecipeUtilities.LogCritical($"🎉 Perennium weapons processing completed!");
        }

        public override RecipeAnalysisResult AnalyzeItems(List<Item> items)
        {
            // Create a temporary container-like container for analysis
            var stage = DetectPerenniumStageFromItems(items);

            return new RecipeAnalysisResult
            {
                CanProcess = stage != PerenniumStage.None,
                ProcessableItemCount = items.Where(CanProcess).Count(),
                Stage = stage.ToString(),
                Description = $"Perennium Stage: {stage} - {items.Where(CanProcess).Count()} Perennium components found"
            };
        }

        private async Task ProcessPerenniumStages(PerenniumStage currentStage, Container targetContainer)
        {
            var stage = currentStage;
            int maxRetries = 10; // Prevent infinite loops
            int retryCount = 0;

            while (stage != PerenniumStage.Completed && stage != PerenniumStage.None && retryCount < maxRetries)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Processing Perennium stage: {stage} (attempt {retryCount + 1})");
                RecipeUtilities.LogCritical($"⚡ Perennium Stage {retryCount + 1}: {stage}");

                var previousStage = stage;
                bool stepSuccessful = false;

                switch (stage)
                {
                    case PerenniumStage.RawMaterials:
                        RecipeUtilities.LogDebug($"[{RecipeName}] Processing Sheet of Perennium to weapon parts (Step 1)");
                        stepSuccessful = await ProcessPerenniumSheets(targetContainer);
                        break;

                    case PerenniumStage.Step1_PerenniumParts:
                        RecipeUtilities.LogDebug($"[{RecipeName}] Processing Perennium parts + Hacked weapons to Half-Finished (Step 3)");
                        stepSuccessful = await ProcessToHalfFinished(targetContainer);
                        break;

                    case PerenniumStage.Step2_HackedWeapons:
                        RecipeUtilities.LogDebug($"[{RecipeName}] Processing Premium weapons to Hacked weapons (Step 2)");
                        stepSuccessful = await ProcessPremiumWeapons(targetContainer);
                        break;

                    case PerenniumStage.Step3_HalfFinished:
                        RecipeUtilities.LogDebug($"[{RecipeName}] Processing Perennium parts + Hacked weapons to Half-Finished (Step 3)");
                        stepSuccessful = await ProcessToHalfFinished(targetContainer);

                        // Also check if we can proceed to Step 4 immediately
                        var halfFinishedCheck = Inventory.Items.Where(item => item.Name.Contains("Half-Finished Perennium")).ToList();
                        var boltsCheck = Inventory.Items.Where(item => item.Name.Equals("Perennium Bolts", StringComparison.OrdinalIgnoreCase)).ToList();
                        if (halfFinishedCheck.Any() && boltsCheck.Any())
                        {
                            RecipeUtilities.LogDebug($"[{RecipeName}] Also processing Half-Finished weapons to Superior (Step 4)");
                            bool step4Success = await ProcessToSuperior(targetContainer);
                            stepSuccessful = stepSuccessful || step4Success;
                        }
                        break;

                    case PerenniumStage.Completed:
                        RecipeUtilities.LogDebug($"[{RecipeName}] Perennium weapons already completed!");
                        RecipeUtilities.LogCritical($"✅ Perennium weapons already completed!");
                        return;

                    default:
                        RecipeUtilities.LogDebug($"[{RecipeName}] Unknown Perennium stage: {stage}");
                        RecipeUtilities.LogCritical($"❌ Unknown Perennium stage: {stage}");
                        return;
                }

                // Re-detect stage to see if we made progress
                var newStage = DetectPerenniumStage(targetContainer);
                RecipeUtilities.LogDebug($"[{RecipeName}] Stage after processing: {previousStage} -> {newStage} (Success: {stepSuccessful})");

                if (newStage == PerenniumStage.Completed)
                {
                    RecipeUtilities.LogCritical($"🎉 Perennium weapons completed successfully!");
                    return;
                }

                if (newStage != previousStage)
                {
                    // Progress made, continue with new stage
                    stage = newStage;
                    retryCount = 0; // Reset retry count on progress
                    RecipeUtilities.LogCritical($"✅ Progress made: {previousStage} -> {newStage}");
                }
                else if (stepSuccessful)
                {
                    // Step reported success but stage didn't change - add delay and recheck
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step successful but stage unchanged - adding delay and rechecking");
                    await Task.Delay(1000); // Give time for items to transform

                    // Recheck stage after delay
                    var recheckStage = DetectPerenniumStage(targetContainer);
                    if (recheckStage != newStage)
                    {
                        RecipeUtilities.LogDebug($"[{RecipeName}] Stage changed after delay: {newStage} -> {recheckStage}");
                        stage = recheckStage;
                        retryCount = 0; // Reset since we made progress
                    }
                    else
                    {
                        // Still no stage change after delay - increment retry
                        retryCount++;
                        RecipeUtilities.LogDebug($"[{RecipeName}] No stage change after delay, retry {retryCount}");
                    }
                }
                else
                {
                    // No progress and step failed
                    retryCount++;
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step failed, retry {retryCount}");
                    RecipeUtilities.LogCritical($"⚠️ Step failed: {stage}, retrying...");

                    // Add delay between failed retries
                    await Task.Delay(1000);
                }
            }

            if (retryCount >= maxRetries)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Perennium processing failed after {maxRetries} attempts");
                RecipeUtilities.LogCritical($"❌ Perennium processing failed after {maxRetries} attempts at stage: {stage}");

                // Add detailed failure analysis
                await AnalyzePerenniumFailure(targetContainer);
            }
        }

        private PerenniumStage DetectPerenniumStage(Container targetContainer)
        {
            // Check BOTH bag and inventory for Perennium components - PROPERLY IGNORE EQUIPPED ITEMS
            var bagItems = targetContainer.Items.ToList();
            var inventoryItems = Inventory.Items.Where(item =>
                item.Slot.Type == IdentityType.Inventory).ToList(); // Only inventory items, not equipped
            var allItems = bagItems.Concat(inventoryItems).ToList();

            return DetectPerenniumStageFromItems(allItems);
        }

        private PerenniumStage DetectPerenniumStageFromItems(List<Item> items)
        {
            RecipeUtilities.LogDebug($"[{RecipeName}] Checking {items.Count} items for Perennium stage detection");

            // Check for completed weapons first (in inventory or bag, NOT equipped)
            var completedWeapons = items.Where(item => item.Name.Contains("Superior Perennium")).ToList();
            if (completedWeapons.Any())
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found completed Superior Perennium weapons - returning Completed stage");
                return PerenniumStage.Completed;
            }

            // Check Step 3 stage (Half-Finished weapons)
            if (items.Any(item => item.Name.Contains("Half-Finished Perennium")))
                return PerenniumStage.Step3_HalfFinished;

            // Check if we have Perennium parts + weapons
            bool hasPerenniumParts = items.Any(item => item.Name.Contains("Perennium Barrel") ||
                                                      item.Name.Contains("Perennium Muzzle"));
            bool hasHackedWeapons = items.Any(item => item.Name.Contains("Hacked Nano-Charged"));
            bool hasUnhackedWeapons = items.Any(item =>
                (item.Name.Contains("Premium Nano-Charged") ||
                 item.Name.Contains("Deluxe Nano-Charged") ||
                 item.Name.Contains("Nano-Charged")) &&
                !item.Name.Contains("Hacked"));

            // If we have parts + hacked weapons, ready to combine (Step 1)
            if (hasPerenniumParts && hasHackedWeapons)
            {
                return PerenniumStage.Step1_PerenniumParts;
            }

            // If we have parts + unhacked weapons, need to hack weapons first (Step 2)
            if (hasPerenniumParts && hasUnhackedWeapons)
            {
                return PerenniumStage.Step2_HackedWeapons;
            }

            // If we have hacked weapons only (no parts yet), still Step 2
            if (hasHackedWeapons)
            {
                return PerenniumStage.Step2_HackedWeapons;
            }

            // Check Step 1 stage (Perennium parts only - ready for Step 3)
            if (hasPerenniumParts)
                return PerenniumStage.Step1_PerenniumParts;

            // Check for raw materials (need to create parts first)
            bool hasRawMaterials = items.Any(item =>
                item.Name.Equals("Sheet of Perennium", StringComparison.OrdinalIgnoreCase) ||
                item.Name.Contains("Spirit Tech Apparatus") ||
                item.Name.Equals("Advanced Hacker Tool", StringComparison.OrdinalIgnoreCase) ||
                ((item.Name.Contains("Premium Nano-Charged") ||
                  item.Name.Contains("Deluxe Nano-Charged") ||
                  item.Name.Contains("Nano-Charged")) && !item.Name.Contains("Hacked")));

            if (hasRawMaterials)
                return PerenniumStage.RawMaterials;

            return PerenniumStage.None;
        }

        private async Task<bool> ProcessPerenniumSheets(Container targetContainer)
        {
            try
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 1: Processing Sheet of Perennium to weapon parts");

                var perenniumSheets = Inventory.Items.Where(item => item.Name.Equals("Sheet of Perennium", StringComparison.OrdinalIgnoreCase)).ToList();
                var spiritTechApparatus = Inventory.Items.Where(item => item.Name.Contains("Spirit Tech Apparatus")).ToList();

                RecipeUtilities.LogDebug($"[{RecipeName}] Found {perenniumSheets.Count} perennium sheets and {spiritTechApparatus.Count} spirit tech apparatus");

                bool stepSuccess = false;
                if (perenniumSheets.Any() && spiritTechApparatus.Any())
                {
                    foreach (var sheet in perenniumSheets)
                    {
                        // Find matching Spirit Tech Apparatus for this sheet
                        var apparatus = spiritTechApparatus.FirstOrDefault();
                        if (apparatus != null)
                        {
                            RecipeUtilities.LogDebug($"[{RecipeName}] Combining {sheet.Name} with {apparatus.Name}");
                            await CombineItems(sheet, apparatus);
                            stepSuccess = true;
                        }
                    }
                }

                RecipeUtilities.LogDebug($"[{RecipeName}] Completed perennium sheet processing - Success: {stepSuccess}");
                return stepSuccess;
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error processing perennium sheets: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ProcessPremiumWeapons(Container targetContainer)
        {
            try
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 2: Processing Nano-Charged weapons to Hacked weapons");

                var premiumWeapons = Inventory.Items.Where(item =>
                    item.Name.Contains("Premium Nano-Charged") ||
                    item.Name.Contains("Deluxe Nano-Charged") ||
                    (item.Name.Contains("Nano-Charged") && !item.Name.Contains("Hacked"))).ToList();
                var hackerTool = FindTool("Advanced Hacker Tool");

                if (hackerTool != null && premiumWeapons.Any())
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Found {premiumWeapons.Count} nano-charged weapons, hacking them");
                    foreach (var weapon in premiumWeapons)
                    {
                        RecipeUtilities.LogDebug($"[{RecipeName}] Hacking weapon: {weapon.Name}");
                        await CombineItems(hackerTool, weapon);
                    }
                    RecipeUtilities.LogDebug($"[{RecipeName}] Completed nano-charged weapon hacking - Success: true");
                    return true;
                }
                else
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] No nano-charged weapons or Advanced Hacker Tool found - Success: false");
                    return false;
                }
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error processing premium weapons: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ProcessToHalfFinished(Container targetContainer)
        {
            try
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 3: Processing Perennium parts + Hacked weapons to Half-Finished");

                // Get all available components
                var perenniumParts = Inventory.Items.Where(item =>
                    item.Name.Contains("Double Perennium Barrel") ||
                    item.Name.Contains("Long Perennium Muzzle") ||
                    item.Name.Contains("Short Perennium Muzzle")).ToList();

                var hackedWeapons = Inventory.Items.Where(item => item.Name.Contains("Hacked Nano-Charged")).ToList();

                bool stepSuccess = false;

                // Process each combination
                foreach (var part in perenniumParts)
                {
                    Item compatibleWeapon = null;

                    // Match parts to compatible weapons
                    if (part.Name.Contains("Double Perennium Barrel"))
                    {
                        compatibleWeapon = hackedWeapons.FirstOrDefault(w => w.Name.Contains("Assault Rifle"));
                    }
                    else if (part.Name.Contains("Long Perennium Muzzle"))
                    {
                        compatibleWeapon = hackedWeapons.FirstOrDefault(w => w.Name.Contains("Rifle") && !w.Name.Contains("Assault"));
                    }
                    else if (part.Name.Contains("Short Perennium Muzzle"))
                    {
                        compatibleWeapon = hackedWeapons.FirstOrDefault(w => w.Name.Contains("Assault Rifle"));
                    }

                    if (compatibleWeapon != null)
                    {
                        RecipeUtilities.LogDebug($"[{RecipeName}] Combining {part.Name} with {compatibleWeapon.Name}");
                        await CombineItems(part, compatibleWeapon);
                        stepSuccess = true;

                        // Remove the used weapon from the list to avoid double-processing
                        hackedWeapons.Remove(compatibleWeapon);
                    }
                }

                RecipeUtilities.LogDebug($"[{RecipeName}] Completed half-finished processing - Success: {stepSuccess}");
                return stepSuccess;
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error processing to half-finished: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ProcessToSuperior(Container targetContainer)
        {
            try
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 4: Processing Half-Finished weapons to Superior weapons");

                var halfFinishedWeapons = Inventory.Items.Where(item => item.Name.Contains("Half-Finished Perennium")).ToList();
                var perenniumBolts = Inventory.Items.Where(item => item.Name.Equals("Perennium Bolts", StringComparison.OrdinalIgnoreCase)).ToList();

                if (perenniumBolts.Any() && halfFinishedWeapons.Any())
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Found {halfFinishedWeapons.Count} half-finished weapons and {perenniumBolts.Count} perennium bolts");

                    for (int i = 0; i < Math.Min(halfFinishedWeapons.Count, perenniumBolts.Count); i++)
                    {
                        RecipeUtilities.LogDebug($"[{RecipeName}] Combining {perenniumBolts[i].Name} with {halfFinishedWeapons[i].Name}");
                        await CombineItems(perenniumBolts[i], halfFinishedWeapons[i]);
                    }

                    RecipeUtilities.LogDebug($"[{RecipeName}] Completed superior weapon creation - Success: true");
                    return true;
                }
                else
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Missing components for superior weapon creation - Success: false");
                    return false;
                }
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error processing to superior: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Analyze why Perennium processing failed and provide detailed diagnostics
        /// </summary>
        private async Task AnalyzePerenniumFailure(Container targetContainer)
        {
            try
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] === PERENNIUM FAILURE ANALYSIS ===");

                // Check what items are currently available
                var bagItems = targetContainer.Items.ToList();
                var inventoryItems = Inventory.Items.Where(item => item.Slot.Type == IdentityType.Inventory).ToList();
                var allItems = bagItems.Concat(inventoryItems).ToList();

                RecipeUtilities.LogDebug($"[{RecipeName}] Available items: {allItems.Count} total ({bagItems.Count} in bag, {inventoryItems.Count} in inventory)");

                foreach (var item in allItems.Take(10)) // Log first 10 items
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Item: {item.Name} (QL{item.Ql})");
                }

                // Check for specific Perennium components
                var sheets = allItems.Where(item => item.Name.Equals("Sheet of Perennium", StringComparison.OrdinalIgnoreCase)).ToList();
                var apparatus = allItems.Where(item => item.Name.Contains("Spirit Tech Apparatus")).ToList();
                var premiumWeapons = allItems.Where(item => item.Name.Contains("Premium Nano-Charged") || item.Name.Contains("Deluxe Nano-Charged")).ToList();
                var hackedWeapons = allItems.Where(item => item.Name.Contains("Hacked Nano-Charged")).ToList();
                var perenniumParts = allItems.Where(item => item.Name.Contains("Perennium Barrel") || item.Name.Contains("Perennium Muzzle")).ToList();
                var halfFinished = allItems.Where(item => item.Name.Contains("Half-Finished Perennium")).ToList();
                var bolts = allItems.Where(item => item.Name.Equals("Perennium Bolts", StringComparison.OrdinalIgnoreCase)).ToList();

                RecipeUtilities.LogDebug($"[{RecipeName}] Sheets: {sheets.Count}, Apparatus: {apparatus.Count}, Premium Weapons: {premiumWeapons.Count}");
                RecipeUtilities.LogDebug($"[{RecipeName}] Hacked Weapons: {hackedWeapons.Count}, Perennium Parts: {perenniumParts.Count}");
                RecipeUtilities.LogDebug($"[{RecipeName}] Half-Finished: {halfFinished.Count}, Bolts: {bolts.Count}");

                // Check for tools
                var hackerTool = FindTool("Advanced Hacker Tool");
                RecipeUtilities.LogDebug($"[{RecipeName}] Advanced Hacker Tool available: {hackerTool != null}");

                RecipeUtilities.LogDebug($"[{RecipeName}] === END FAILURE ANALYSIS ===");
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error in failure analysis: {ex.Message}");
            }
        }
    }
}
