using System;
using System.Linq;
using System.Threading.Tasks;
using AOSharp.Core;
using AOSharp.Core.Inventory;
using System.Collections.Generic;

namespace Craftbot.Recipes
{
    /// <summary>
    /// Handles Crepuscule Leather Armor processing using unified recipe patterns
    /// Multi-step Recipe: 
    /// Step 1: Small Patch + Small Patch = Patch
    /// Step 2: Patch + Small Patch = Large Patch
    /// Step 3: Patch/Large Patch + Canister of Pure Liquid Notum = Living Hide
    /// Step 4: Living Hide + Spirit Bauble = Crepuscule Leather Armor
    /// </summary>
    public class CrepusculeLeatherArmorRecipe : BaseRecipeProcessor
    {
        public override string RecipeName => "Crepuscule Leather Armor";

        private static readonly string[] ProcessableItems = {
            "Small Patch of Hard Novictum Spangled Hide",
            "Small Patch of Soft Novictum Spangled Hide",
            "Patch of Hard Novictum Spangled Hide",
            "Patch of Soft Novictum Spangled Hide",
            "Large Patch of Hard Novictum Spangled Hide",
            "Large Patch of Soft Novictum Spangled Hide",
            "Canister of Pure Liquid Notum",
            "Hard Living Hide",
            "Soft Living Hide",
            "Large Patch of Hard Living Hide",
            "Large Patch of Soft Living Hide",
            "Patch of Hard Living Hide",
            "Patch of Soft Living Hide",
            "Small Patch of Hard Living Hide",
            "Small Patch of Soft Living Hide"
        };

        private static readonly string[] SpiritBaubles = {
            "Spirit Bauble of Artillery Expertise",
            "Spirit Bauble of Artilley Expertise", // FC spelling error
            "Spirit Bauble of Control Expertise",
            "Spirit Bauble of Extermination Expertise",
            "Spirit Bauble of Infantry Expertise",
            "Spirit Bauble of Support Expertise"
        };

        public override bool CanProcess(Item item)
        {
            bool canProcess = ProcessableItems.Any(processableItem => 
                item.Name.Contains(processableItem, StringComparison.OrdinalIgnoreCase)) ||
                SpiritBaubles.Any(bauble => 
                item.Name.Contains(bauble, StringComparison.OrdinalIgnoreCase));
            
            RecipeUtilities.LogDebug($"[CREPUSCULE LEATHER CHECK] Item: '{item.Name}' -> Can process: {canProcess}");
            return canProcess;
        }

        protected override async Task ProcessRecipeLogic(Item item, Container targetContainer)
        {
            // CRITICAL FIX: Process ALL possible combinations in current inventory state
            // Instead of processing one item at a time, find all possible crepuscule leather armor combinations
            await ProcessAllPossibleCrepusculeLeatherArmor();
        }

        /// <summary>
        /// Comprehensive crepuscule leather armor processing - finds all possible combinations and processes them until complete
        /// </summary>
        private async Task ProcessAllPossibleCrepusculeLeatherArmor()
        {
            RecipeUtilities.LogDebug($"[{RecipeName}] Starting comprehensive crepuscule leather armor processing");

            int maxIterations = 20; // Safety limit
            int iteration = 0;
            bool foundCombination = true;

            while (foundCombination && iteration < maxIterations)
            {
                iteration++;
                foundCombination = false;
                RecipeUtilities.LogDebug($"[{RecipeName}] === Processing iteration {iteration} ===");

                // Step 1: Process all Small Patch + Small Patch combinations
                foundCombination |= await ProcessAllStep1Combinations();

                // Step 2: Process all Patch + Small Patch combinations
                foundCombination |= await ProcessAllStep2Combinations();

                // Step 3: Process all Patch/Large Patch + Canister of Pure Liquid Notum combinations
                foundCombination |= await ProcessAllStep3Combinations();

                // Step 4: Process all Living Hide + Spirit Bauble combinations
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

            RecipeUtilities.LogDebug($"[{RecipeName}] Comprehensive crepuscule leather armor processing completed after {iteration} iterations");
        }

        /// <summary>
        /// Process all possible Step 1 combinations (Small Patch + Small Patch = Patch)
        /// </summary>
        private async Task<bool> ProcessAllStep1Combinations()
        {
            bool foundAny = false;

            // Find all Small Patches in inventory, grouped by type (Hard/Soft)
            var hardSmallPatches = Inventory.Items.Where(invItem =>
                invItem.Name.Contains("Small Patch of Hard Novictum Spangled Hide", StringComparison.OrdinalIgnoreCase)).ToList();
            var softSmallPatches = Inventory.Items.Where(invItem =>
                invItem.Name.Contains("Small Patch of Soft Novictum Spangled Hide", StringComparison.OrdinalIgnoreCase)).ToList();

            // Process Hard patches
            while (hardSmallPatches.Count >= 2)
            {
                var patch1 = hardSmallPatches[0];
                var patch2 = hardSmallPatches[1];
                RecipeUtilities.LogDebug($"[{RecipeName}] Processing Step 1 (Hard): {patch1.Name} + {patch2.Name}");
                await ProcessSmallPatchCombining(patch1);
                foundAny = true;

                // Remove processed patches from our tracking list
                hardSmallPatches.RemoveAt(0);
                hardSmallPatches.RemoveAt(0);

                // Small delay between combinations
                await Task.Delay(100);
            }

            // Process Soft patches
            while (softSmallPatches.Count >= 2)
            {
                var patch1 = softSmallPatches[0];
                var patch2 = softSmallPatches[1];
                RecipeUtilities.LogDebug($"[{RecipeName}] Processing Step 1 (Soft): {patch1.Name} + {patch2.Name}");
                await ProcessSmallPatchCombining(patch1);
                foundAny = true;

                // Remove processed patches from our tracking list
                softSmallPatches.RemoveAt(0);
                softSmallPatches.RemoveAt(0);

                // Small delay between combinations
                await Task.Delay(100);
            }

            if (hardSmallPatches.Count == 1)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found 1 Hard Small Patch remaining - need another to combine");
            }
            if (softSmallPatches.Count == 1)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found 1 Soft Small Patch remaining - need another to combine");
            }

            return foundAny;
        }

        /// <summary>
        /// Process all possible Step 2 combinations (Patch + Small Patch = Large Patch)
        /// </summary>
        private async Task<bool> ProcessAllStep2Combinations()
        {
            bool foundAny = false;

            // Find all regular Patches (not Small, not Large) in inventory
            var hardPatches = Inventory.Items.Where(invItem =>
                invItem.Name.Contains("Patch of Hard Novictum Spangled Hide", StringComparison.OrdinalIgnoreCase) &&
                !invItem.Name.Contains("Small Patch", StringComparison.OrdinalIgnoreCase) &&
                !invItem.Name.Contains("Large Patch", StringComparison.OrdinalIgnoreCase)).ToList();
            var softPatches = Inventory.Items.Where(invItem =>
                invItem.Name.Contains("Patch of Soft Novictum Spangled Hide", StringComparison.OrdinalIgnoreCase) &&
                !invItem.Name.Contains("Small Patch", StringComparison.OrdinalIgnoreCase) &&
                !invItem.Name.Contains("Large Patch", StringComparison.OrdinalIgnoreCase)).ToList();

            // Find Small Patches for combining
            var hardSmallPatches = Inventory.Items.Where(invItem =>
                invItem.Name.Contains("Small Patch of Hard Novictum Spangled Hide", StringComparison.OrdinalIgnoreCase)).ToList();
            var softSmallPatches = Inventory.Items.Where(invItem =>
                invItem.Name.Contains("Small Patch of Soft Novictum Spangled Hide", StringComparison.OrdinalIgnoreCase)).ToList();

            // Process Hard patches
            foreach (var patch in hardPatches)
            {
                if (hardSmallPatches.Any())
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Processing Step 2 (Hard): {patch.Name} + Small Patch");
                    await ProcessPatchToLarge(patch);
                    foundAny = true;

                    // Remove one small patch from our tracking (it was consumed)
                    hardSmallPatches.RemoveAt(0);

                    // Small delay between combinations
                    await Task.Delay(100);
                }
            }

            // Process Soft patches
            foreach (var patch in softPatches)
            {
                if (softSmallPatches.Any())
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Processing Step 2 (Soft): {patch.Name} + Small Patch");
                    await ProcessPatchToLarge(patch);
                    foundAny = true;

                    // Remove one small patch from our tracking (it was consumed)
                    softSmallPatches.RemoveAt(0);

                    // Small delay between combinations
                    await Task.Delay(100);
                }
            }

            return foundAny;
        }

        /// <summary>
        /// Process all possible Step 3 combinations (Patch/Large Patch + Canister of Pure Liquid Notum = Living Hide)
        /// </summary>
        private async Task<bool> ProcessAllStep3Combinations()
        {
            bool foundAny = false;

            // Find all Patches and Large Patches with Novictum Spangled Hide
            var patches = Inventory.Items.Where(invItem =>
                (invItem.Name.Contains("Patch of", StringComparison.OrdinalIgnoreCase) ||
                 invItem.Name.Contains("Large Patch of", StringComparison.OrdinalIgnoreCase)) &&
                invItem.Name.Contains("Novictum Spangled Hide", StringComparison.OrdinalIgnoreCase)).ToList();

            // Find all Canister of Pure Liquid Notum
            var notumCanisters = Inventory.Items.Where(invItem =>
                invItem.Name.Contains("Canister of Pure Liquid Notum", StringComparison.OrdinalIgnoreCase)).ToList();

            if (patches.Any() && notumCanisters.Any())
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found {patches.Count} patches and {notumCanisters.Count} notum canisters for Step 3 processing");

                foreach (var patch in patches)
                {
                    if (notumCanisters.Any())
                    {
                        RecipeUtilities.LogDebug($"[{RecipeName}] Processing Step 3: {patch.Name} + Canister of Pure Liquid Notum");
                        await ProcessPatchToLivingHide(patch);
                        foundAny = true;

                        // Remove one canister from our tracking (it was consumed)
                        notumCanisters.RemoveAt(0);

                        // Small delay between combinations
                        await Task.Delay(100);
                    }
                    else
                    {
                        break; // No more canisters available
                    }
                }
            }
            else if (patches.Any())
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found {patches.Count} patches but no Canister of Pure Liquid Notum for Step 3");
            }
            else if (notumCanisters.Any())
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found {notumCanisters.Count} notum canisters but no patches for Step 3");
            }

            return foundAny;
        }

        /// <summary>
        /// Process all possible Step 4 combinations (Living Hide + Spirit Bauble = Crepuscule Leather Armor)
        /// </summary>
        private async Task<bool> ProcessAllStep4Combinations()
        {
            bool foundAny = false;

            // Find all Living Hide items
            var livingHides = Inventory.Items.Where(invItem =>
                invItem.Name.Contains("Living Hide", StringComparison.OrdinalIgnoreCase)).ToList();

            // Find all Spirit Baubles
            var spiritBaubles = Inventory.Items.Where(invItem =>
                SpiritBaubles.Any(bauble => invItem.Name.Contains(bauble, StringComparison.OrdinalIgnoreCase))).ToList();

            if (livingHides.Any() && spiritBaubles.Any())
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found {livingHides.Count} living hides and {spiritBaubles.Count} spirit baubles for Step 4 processing");

                foreach (var hide in livingHides)
                {
                    if (spiritBaubles.Any())
                    {
                        RecipeUtilities.LogDebug($"[{RecipeName}] Processing Step 4: {hide.Name} + Spirit Bauble");
                        await ProcessLivingHideToArmor(hide);
                        foundAny = true;

                        // Remove one bauble from our tracking (it was consumed)
                        spiritBaubles.RemoveAt(0);

                        // Small delay between combinations
                        await Task.Delay(100);
                    }
                    else
                    {
                        break; // No more baubles available
                    }
                }
            }
            else if (livingHides.Any())
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found {livingHides.Count} living hides but no Spirit Baubles for Step 4");
            }
            else if (spiritBaubles.Any())
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found {spiritBaubles.Count} spirit baubles but no Living Hides for Step 4");
            }

            return foundAny;
        }

        /// <summary>
        /// Step 1: Process Small Patch + Small Patch = Patch
        /// </summary>
        private async Task ProcessSmallPatchCombining(Item smallPatch)
        {
            // Find another small patch of the same type (Hard or Soft)
            string patchType = smallPatch.Name.Contains("Hard") ? "Hard" : "Soft";
            var anotherSmallPatch = Inventory.Items.FirstOrDefault(invItem => 
                invItem.Name.Contains($"Small Patch of {patchType} Novictum Spangled Hide", StringComparison.OrdinalIgnoreCase) &&
                invItem != smallPatch);
            
            if (anotherSmallPatch != null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 1: Combining {smallPatch.Name} with {anotherSmallPatch.Name}");
                await CombineItems(smallPatch, anotherSmallPatch);

                // Check result
                var patchCount = Inventory.Items.Where(invItem => 
                    invItem.Name.Contains($"Patch of {patchType} Novictum Spangled Hide", StringComparison.OrdinalIgnoreCase) &&
                    !invItem.Name.Contains("Small Patch", StringComparison.OrdinalIgnoreCase) &&
                    !invItem.Name.Contains("Large Patch", StringComparison.OrdinalIgnoreCase)).Count();
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 1 completed - now have {patchCount} {patchType} Patches in inventory");
            }
            else
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Could not find another Small Patch of {patchType} to combine with");
            }
        }

        /// <summary>
        /// Step 2: Process Patch + Small Patch = Large Patch
        /// </summary>
        private async Task ProcessPatchToLarge(Item patch)
        {
            // Find a small patch of the same type
            string patchType = patch.Name.Contains("Hard") ? "Hard" : "Soft";
            var smallPatch = Inventory.Items.FirstOrDefault(invItem => 
                invItem.Name.Contains($"Small Patch of {patchType} Novictum Spangled Hide", StringComparison.OrdinalIgnoreCase));
            
            if (smallPatch != null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 2: Combining {patch.Name} with {smallPatch.Name}");
                await CombineItems(patch, smallPatch);

                // Check result
                var largePatchCount = Inventory.Items.Where(invItem => 
                    invItem.Name.Contains($"Large Patch of {patchType} Novictum Spangled Hide", StringComparison.OrdinalIgnoreCase)).Count();
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 2 completed - now have {largePatchCount} Large {patchType} Patches in inventory");
            }
            else
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Could not find Small Patch of {patchType} to combine with {patch.Name}");
            }
        }

        /// <summary>
        /// Step 3: Process Patch/Large Patch with Canister of Pure Liquid Notum to create Living Hide
        /// </summary>
        private async Task ProcessPatchToLivingHide(Item patch)
        {
            var notumCanister = Inventory.Items.FirstOrDefault(invItem => 
                invItem.Name.Contains("Canister of Pure Liquid Notum", StringComparison.OrdinalIgnoreCase));
            
            if (notumCanister == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Could not find Canister of Pure Liquid Notum for {patch.Name}");
                return;
            }

            RecipeUtilities.LogDebug($"[{RecipeName}] Step 3: Processing {patch.Name} with {notumCanister.Name}");
            await CombineItems(patch, notumCanister);

            // Check result
            var livingHideCount = Inventory.Items.Where(invItem => 
                invItem.Name.Contains("Living Hide", StringComparison.OrdinalIgnoreCase)).Count();
            RecipeUtilities.LogDebug($"[{RecipeName}] Step 3 completed - now have {livingHideCount} Living Hide items in inventory");
        }

        /// <summary>
        /// Step 3 alternative: Process Canister of Pure Liquid Notum with Patch/Large Patch
        /// </summary>
        private async Task ProcessNotumWithPatch(Item notum)
        {
            var patch = Inventory.Items.FirstOrDefault(invItem => 
                (invItem.Name.Contains("Patch of", StringComparison.OrdinalIgnoreCase) || 
                 invItem.Name.Contains("Large Patch of", StringComparison.OrdinalIgnoreCase)) &&
                invItem.Name.Contains("Novictum Spangled Hide", StringComparison.OrdinalIgnoreCase));
            
            if (patch != null)
            {
                await ProcessPatchToLivingHide(patch);
            }
            else
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found {notum.Name} but no compatible Patch to process with");
            }
        }

        /// <summary>
        /// Step 4: Process Living Hide with Spirit Bauble to create Crepuscule Leather Armor
        /// </summary>
        private async Task ProcessLivingHideToArmor(Item livingHide)
        {
            var spiritBauble = Inventory.Items.FirstOrDefault(invItem => 
                SpiritBaubles.Any(bauble => invItem.Name.Contains(bauble, StringComparison.OrdinalIgnoreCase)));
            
            if (spiritBauble == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Could not find Spirit Bauble for {livingHide.Name}");
                return;
            }

            RecipeUtilities.LogDebug($"[{RecipeName}] Step 4: Processing {livingHide.Name} with {spiritBauble.Name}");
            await CombineItems(livingHide, spiritBauble);

            // Check result
            var armorCount = Inventory.Items.Where(invItem => 
                invItem.Name.Contains("Crepuscule Leather", StringComparison.OrdinalIgnoreCase) ||
                (invItem.Name.Contains("Artillery", StringComparison.OrdinalIgnoreCase) || 
                 invItem.Name.Contains("Artilley", StringComparison.OrdinalIgnoreCase) ||
                 invItem.Name.Contains("Control", StringComparison.OrdinalIgnoreCase) ||
                 invItem.Name.Contains("Extermination", StringComparison.OrdinalIgnoreCase) ||
                 invItem.Name.Contains("Infantry", StringComparison.OrdinalIgnoreCase) ||
                 invItem.Name.Contains("Support", StringComparison.OrdinalIgnoreCase)) &&
                invItem.Name.Contains("Leather", StringComparison.OrdinalIgnoreCase)).Count();
            RecipeUtilities.LogDebug($"[{RecipeName}] Step 4 completed - now have {armorCount} Crepuscule Leather Armor pieces in inventory");
        }

        /// <summary>
        /// Step 4 alternative: Process Spirit Bauble with Living Hide
        /// </summary>
        private async Task ProcessSpiritBaubleWithHide(Item bauble)
        {
            var livingHide = Inventory.Items.FirstOrDefault(invItem => 
                invItem.Name.Contains("Living Hide", StringComparison.OrdinalIgnoreCase));
            
            if (livingHide != null)
            {
                await ProcessLivingHideToArmor(livingHide);
            }
            else
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found {bauble.Name} but no Living Hide to process with");
            }
        }

        public override RecipeAnalysisResult AnalyzeItems(List<Item> items)
        {
            return AnalyzeItemsShared(items, "Crepuscule Leather Armor Processing");
        }
    }
}
