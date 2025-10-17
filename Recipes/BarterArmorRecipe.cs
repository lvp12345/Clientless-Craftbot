using System;
using System.Linq;
using System.Threading.Tasks;
using AOSharp.Core;
using AOSharp.Core.Inventory;
using System.Collections.Generic;

namespace Craftbot.Recipes
{
    /// <summary>
    /// Handles Barter Armor processing using unified recipe patterns
    /// Multi-step Recipe: 
    /// Step 1: Inactive OT Metamorphing Liquid Nanobots + Nano Programming Interface = Activated OT Metamorphing Liquid Nanobots
    /// Step 2: Activated OT Nanobots + Notum Chip = Stabilized OT Metamorphing Liquid Nanobots
    /// Step 3: Stabilized OT Nanobots + Notum Chip = Super-Stabilized OT Metamorphing Liquid Nanobots
    /// Step 4: Augmented Nano Armor + Super-Stabilized OT Nanobots = Barter Armor
    /// </summary>
    public class BarterArmorRecipe : BaseRecipeProcessor
    {
        public override string RecipeName => "Barter Armor";

        private static readonly string[] ProcessableItems = {
            "Inactive OT Metamorphing Liquid Nanobots",
            "Activated OT Metamorphing Liquid Nanobots",
            "Stabilized OT Metamorphing Liquid Nanobots",
            "Super-Stabilized OT Metamorphing Liquid Nanobots",
            "Augmented Nano Armor",
            "Augmented Nano Armour",
            "Notum Chip",
            "Notum Fragment"
        };

        private static readonly string[] AugmentedArmorPieces = {
            "Augmented Nano Armor Boots",
            "Augmented Nano Armor Gloves", 
            "Augmented Nano Armor Helmet",
            "Augmented Nano Armor Pants",
            "Augmented Nano Armor Sleeves",
            "Augmented Nano Body Armor",
            "Augmented Nano Armour Boots",
            "Augmented Nano Armour Gloves", 
            "Augmented Nano Armour Helmet",
            "Augmented Nano Armour Pants",
            "Augmented Nano Armour Sleeves",
            "Augmented Nano Body Armour"
        };

        public override bool CanProcess(Item item)
        {
            bool canProcess = ProcessableItems.Any(processableItem => 
                item.Name.Contains(processableItem, StringComparison.OrdinalIgnoreCase)) ||
                AugmentedArmorPieces.Any(armorPiece => 
                item.Name.Contains(armorPiece, StringComparison.OrdinalIgnoreCase));
            
            RecipeUtilities.LogDebug($"[BARTER ARMOR CHECK] Item: '{item.Name}' -> Can process: {canProcess}");
            return canProcess;
        }

        protected override async Task ProcessRecipeLogic(Item item, Container targetContainer)
        {
            // CRITICAL FIX: Process ALL possible combinations in current inventory state
            // Instead of processing one item at a time, find all possible barter armor combinations
            await ProcessAllPossibleBarterArmor();
        }

        /// <summary>
        /// Comprehensive barter armor processing - finds all possible combinations and processes them until complete
        /// </summary>
        private async Task ProcessAllPossibleBarterArmor()
        {
            RecipeUtilities.LogDebug($"[{RecipeName}] Starting comprehensive barter armor processing");

            int maxIterations = 20; // Safety limit
            int iteration = 0;
            bool foundCombination = true;

            while (foundCombination && iteration < maxIterations)
            {
                iteration++;
                foundCombination = false;
                RecipeUtilities.LogDebug($"[{RecipeName}] === Processing iteration {iteration} ===");

                // Step 1: Process all Inactive OT Nanobots with Nano Programming Interface
                foundCombination |= await ProcessAllStep1Combinations();

                // Step 2: Process all Activated OT Nanobots with Notum Chips/Fragments
                foundCombination |= await ProcessAllStep2Combinations();

                // Step 3: Process all Stabilized OT Nanobots with Notum Chips/Fragments
                foundCombination |= await ProcessAllStep3Combinations();

                // Step 4: Process all Augmented Nano Armor with Super-Stabilized OT Nanobots
                foundCombination |= await ProcessAllStep4Combinations();

                if (foundCombination)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Found and processed combinations in iteration {iteration}");
                    // Small delay between iterations for stability
                    await Task.Delay(200);
                }
                else
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] No more combinations found in iteration {iteration}");
                }
            }

            if (iteration >= maxIterations)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] ⚠️ Reached maximum iterations ({maxIterations}) - stopping to prevent infinite loop");
            }

            RecipeUtilities.LogDebug($"[{RecipeName}] Comprehensive barter armor processing completed after {iteration} iterations");
        }

        /// <summary>
        /// Process all possible Step 1 combinations (Inactive OT Nanobots + Nano Programming Interface)
        /// </summary>
        private async Task<bool> ProcessAllStep1Combinations()
        {
            bool foundAny = false;

            // Find all Inactive OT Nanobots in inventory
            var inactiveNanobots = Inventory.Items.Where(invItem =>
                invItem.Name.Contains("Inactive OT Metamorphing Liquid Nanobots", StringComparison.OrdinalIgnoreCase)).ToList();

            if (inactiveNanobots.Any())
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found {inactiveNanobots.Count} Inactive OT Nanobots for Step 1 processing");

                foreach (var nanobots in inactiveNanobots)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Processing Step 1 for: {nanobots.Name}");
                    await ProcessInactiveToActivated(nanobots);
                    foundAny = true;

                    // Small delay between combinations
                    await Task.Delay(100);
                }
            }

            return foundAny;
        }

        /// <summary>
        /// Process all possible Step 2 combinations (Activated OT Nanobots + Notum Chip/Fragment)
        /// </summary>
        private async Task<bool> ProcessAllStep2Combinations()
        {
            bool foundAny = false;

            // Find all Activated OT Nanobots in inventory
            var activatedNanobots = Inventory.Items.Where(invItem =>
                invItem.Name.Contains("Activated OT Metamorphing Liquid Nanobots", StringComparison.OrdinalIgnoreCase)).ToList();

            // Find all Notum Chips/Fragments in inventory
            var notumItems = Inventory.Items.Where(invItem =>
                invItem.Name.Contains("Notum Chip", StringComparison.OrdinalIgnoreCase) ||
                invItem.Name.Contains("Notum Fragment", StringComparison.OrdinalIgnoreCase)).ToList();

            if (activatedNanobots.Any() && notumItems.Any())
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found {activatedNanobots.Count} Activated OT Nanobots and {notumItems.Count} Notum items for Step 2 processing");

                foreach (var nanobots in activatedNanobots)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Processing Step 2 for: {nanobots.Name}");
                    await ProcessActivatedToStabilized(nanobots);
                    foundAny = true;

                    // Small delay between combinations
                    await Task.Delay(100);
                }
            }
            else if (activatedNanobots.Any())
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found {activatedNanobots.Count} Activated OT Nanobots but no Notum items for Step 2");
            }

            return foundAny;
        }

        /// <summary>
        /// Process all possible Step 3 combinations (Stabilized OT Nanobots + Notum Chip/Fragment)
        /// </summary>
        private async Task<bool> ProcessAllStep3Combinations()
        {
            bool foundAny = false;

            // Find all Stabilized OT Nanobots in inventory
            var stabilizedNanobots = Inventory.Items.Where(invItem =>
                invItem.Name.Contains("Stabilized OT Metamorphing Liquid Nanobots", StringComparison.OrdinalIgnoreCase) &&
                !invItem.Name.Contains("Super-Stabilized", StringComparison.OrdinalIgnoreCase)).ToList();

            // Find all Notum Chips/Fragments in inventory
            var notumItems = Inventory.Items.Where(invItem =>
                invItem.Name.Contains("Notum Chip", StringComparison.OrdinalIgnoreCase) ||
                invItem.Name.Contains("Notum Fragment", StringComparison.OrdinalIgnoreCase)).ToList();

            if (stabilizedNanobots.Any() && notumItems.Any())
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found {stabilizedNanobots.Count} Stabilized OT Nanobots and {notumItems.Count} Notum items for Step 3 processing");

                foreach (var nanobots in stabilizedNanobots)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Processing Step 3 for: {nanobots.Name}");
                    await ProcessStabilizedToSuperStabilized(nanobots);
                    foundAny = true;

                    // Small delay between combinations
                    await Task.Delay(100);
                }
            }
            else if (stabilizedNanobots.Any())
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found {stabilizedNanobots.Count} Stabilized OT Nanobots but no Notum items for Step 3");
            }

            return foundAny;
        }

        /// <summary>
        /// Process all possible Step 4 combinations (Augmented Nano Armor + Super-Stabilized OT Nanobots)
        /// </summary>
        private async Task<bool> ProcessAllStep4Combinations()
        {
            bool foundAny = false;

            // Find all Augmented Nano Armor pieces in inventory
            var armorPieces = Inventory.Items.Where(invItem =>
                AugmentedArmorPieces.Any(armorPiece => invItem.Name.Contains(armorPiece, StringComparison.OrdinalIgnoreCase))).ToList();

            // Find all Super-Stabilized OT Nanobots in inventory
            var superStabilizedNanobots = Inventory.Items.Where(invItem =>
                invItem.Name.Contains("Super-Stabilized OT Metamorphing Liquid Nanobots", StringComparison.OrdinalIgnoreCase)).ToList();

            if (armorPieces.Any() && superStabilizedNanobots.Any())
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found {armorPieces.Count} Augmented Nano Armor pieces and {superStabilizedNanobots.Count} Super-Stabilized OT Nanobots for Step 4 processing");

                foreach (var armor in armorPieces)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Processing Step 4 for: {armor.Name}");
                    await ProcessArmorToBarter(armor);
                    foundAny = true;

                    // Small delay between combinations
                    await Task.Delay(100);
                }
            }
            else if (armorPieces.Any())
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found {armorPieces.Count} Augmented Nano Armor pieces but no Super-Stabilized OT Nanobots for Step 4");
            }
            else if (superStabilizedNanobots.Any())
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found {superStabilizedNanobots.Count} Super-Stabilized OT Nanobots but no Augmented Nano Armor pieces for Step 4");
            }

            return foundAny;
        }

        /// <summary>
        /// Step 1: Process Inactive OT Nanobots with Nano Programming Interface to create Activated OT Nanobots
        /// </summary>
        private async Task ProcessInactiveToActivated(Item inactiveNanobots)
        {
            var nanoInterface = FindTool("Nano Programming Interface");
            if (nanoInterface == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Could not find Nano Programming Interface for Inactive Nanobots processing");
                return;
            }

            RecipeUtilities.LogDebug($"[{RecipeName}] Step 1: Processing {inactiveNanobots.Name} with {nanoInterface.Name}");
            await CombineItems(inactiveNanobots, nanoInterface);

            // Check result
            var activatedCount = Inventory.Items.Where(invItem => 
                invItem.Name.Contains("Activated OT Metamorphing Liquid Nanobots", StringComparison.OrdinalIgnoreCase)).Count();
            RecipeUtilities.LogDebug($"[{RecipeName}] Step 1 completed - now have {activatedCount} Activated OT Nanobots in inventory");
        }

        /// <summary>
        /// Step 2: Process Activated OT Nanobots with Notum Chip/Fragment to create Stabilized OT Nanobots
        /// </summary>
        private async Task ProcessActivatedToStabilized(Item activatedNanobots)
        {
            var notumChip = Inventory.Items.FirstOrDefault(invItem => 
                invItem.Name.Contains("Notum Chip", StringComparison.OrdinalIgnoreCase) ||
                invItem.Name.Contains("Notum Fragment", StringComparison.OrdinalIgnoreCase));
            
            if (notumChip == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Could not find Notum Chip/Fragment for Activated Nanobots processing");
                return;
            }

            RecipeUtilities.LogDebug($"[{RecipeName}] Step 2: Processing {activatedNanobots.Name} with {notumChip.Name}");
            await CombineItems(activatedNanobots, notumChip);

            // Check result
            var stabilizedCount = Inventory.Items.Where(invItem => 
                invItem.Name.Contains("Stabilized OT Metamorphing Liquid Nanobots", StringComparison.OrdinalIgnoreCase)).Count();
            RecipeUtilities.LogDebug($"[{RecipeName}] Step 2 completed - now have {stabilizedCount} Stabilized OT Nanobots in inventory");
        }

        /// <summary>
        /// Step 3: Process Stabilized OT Nanobots with Notum Chip/Fragment to create Super-Stabilized OT Nanobots
        /// </summary>
        private async Task ProcessStabilizedToSuperStabilized(Item stabilizedNanobots)
        {
            var notumChip = Inventory.Items.FirstOrDefault(invItem => 
                invItem.Name.Contains("Notum Chip", StringComparison.OrdinalIgnoreCase) ||
                invItem.Name.Contains("Notum Fragment", StringComparison.OrdinalIgnoreCase));
            
            if (notumChip == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Could not find Notum Chip/Fragment for Stabilized Nanobots processing");
                return;
            }

            RecipeUtilities.LogDebug($"[{RecipeName}] Step 3: Processing {stabilizedNanobots.Name} with {notumChip.Name}");
            await CombineItems(stabilizedNanobots, notumChip);

            // Check result
            var superStabilizedCount = Inventory.Items.Where(invItem => 
                invItem.Name.Contains("Super-Stabilized OT Metamorphing Liquid Nanobots", StringComparison.OrdinalIgnoreCase)).Count();
            RecipeUtilities.LogDebug($"[{RecipeName}] Step 3 completed - now have {superStabilizedCount} Super-Stabilized OT Nanobots in inventory");
        }

        /// <summary>
        /// Step 4: Process Augmented Nano Armor with Super-Stabilized OT Nanobots to create Barter Armor
        /// </summary>
        private async Task ProcessArmorToBarter(Item armor)
        {
            var superStabilized = Inventory.Items.FirstOrDefault(invItem => 
                invItem.Name.Contains("Super-Stabilized OT Metamorphing Liquid Nanobots", StringComparison.OrdinalIgnoreCase));
            
            if (superStabilized == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Could not find Super-Stabilized OT Nanobots for Armor processing");
                return;
            }

            RecipeUtilities.LogDebug($"[{RecipeName}] Step 4: Processing {armor.Name} with {superStabilized.Name}");
            await CombineItems(armor, superStabilized);

            // Check result
            var barterArmorCount = Inventory.Items.Where(invItem => 
                invItem.Name.Contains("Barter", StringComparison.OrdinalIgnoreCase) && 
                (invItem.Name.Contains("Armor", StringComparison.OrdinalIgnoreCase) || 
                 invItem.Name.Contains("Armour", StringComparison.OrdinalIgnoreCase))).Count();
            RecipeUtilities.LogDebug($"[{RecipeName}] Step 4 completed - now have {barterArmorCount} Barter Armor pieces in inventory");
        }

        /// <summary>
        /// Process Notum with appropriate Nanobots (steps 2 or 3)
        /// </summary>
        private async Task ProcessNotumWithNanobots(Item notum)
        {
            // Try to find Activated Nanobots first (step 2)
            var activatedNanobots = Inventory.Items.FirstOrDefault(invItem => 
                invItem.Name.Contains("Activated OT Metamorphing Liquid Nanobots", StringComparison.OrdinalIgnoreCase));
            
            if (activatedNanobots != null)
            {
                await ProcessActivatedToStabilized(activatedNanobots);
                return;
            }

            // Try to find Stabilized Nanobots (step 3)
            var stabilizedNanobots = Inventory.Items.FirstOrDefault(invItem => 
                invItem.Name.Contains("Stabilized OT Metamorphing Liquid Nanobots", StringComparison.OrdinalIgnoreCase));
            
            if (stabilizedNanobots != null)
            {
                await ProcessStabilizedToSuperStabilized(stabilizedNanobots);
                return;
            }

            RecipeUtilities.LogDebug($"[{RecipeName}] Found {notum.Name} but no compatible Nanobots to process with");
        }

        /// <summary>
        /// Process Super-Stabilized Nanobots with appropriate Armor (step 4)
        /// </summary>
        private async Task ProcessSuperStabilizedWithArmor(Item superStabilized)
        {
            var armor = Inventory.Items.FirstOrDefault(invItem => 
                AugmentedArmorPieces.Any(armorPiece => invItem.Name.Contains(armorPiece, StringComparison.OrdinalIgnoreCase)));
            
            if (armor != null)
            {
                await ProcessArmorToBarter(armor);
            }
            else
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found {superStabilized.Name} but no Augmented Nano Armor to process with");
            }
        }

        public override RecipeAnalysisResult AnalyzeItems(List<Item> items)
        {
            return AnalyzeItemsShared(items, "Barter Armor Processing");
        }
    }
}
