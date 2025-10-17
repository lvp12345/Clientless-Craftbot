using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AOSharp.Clientless;

namespace Craftbot.Recipes
{
    /// <summary>
    /// Handles sealed weapon processing using player-provided Advanced Hacker Tool
    /// Two-stage process: 1) Hacker Tool + Sealed Weapon Receptacle = Unfinished weapon
    /// 2) Upgrade component + Unfinished weapon = Final weapon
    /// SPECIAL RULE: Only uses player-provided tools, never touches bot's personal tools
    /// </summary>
    public class SealedWeaponRecipe : BaseRecipeProcessor
    {
        public override string RecipeName => "Sealed Weapon";

        // Stage 1: Sealed Weapon Receptacles that can be hacked
        private static readonly string[] SealedWeaponReceptacles = {
            "Sealed Weapon Receptacle - Ithaca Ki12 Vulture",
            "Sealed Weapon Receptacle - Alsaqri Chemical Rifle",
            "Sealed Weapon Receptacle - HSR Explorer 661",
            "Sealed Weapon Receptacle - IMI Tellus TT",
            "Sealed Weapon Receptacle - River Seasons XP",
            "Sealed Weapon Receptacle - Soft Pepper Pistol",
            "Sealed Weapon Receptacle - Sol Chironis Systems",
            "Sealed Weapon Receptacle - Summer SMP"
        };

        // Stage 2: Upgrade components for finishing weapons
        private static readonly Dictionary<string, string> UpgradeComponents = new Dictionary<string, string>
        {
            { "Self-Repairing Ultra-X", "Unfinished Alsaqri Chemical Rifle" },
            { "Generic Magnetic Propulsion System", "Unfinished HSR Explorer 661" },
            { "Flake Tubing Super-Coolant System", "Unfinished IMI Tellus TT" },
            { "Nano-Interfaced Cooling System", "Unfinished Ithaca Ki12 Vulture" },
            { "Ultra Short Composite Barrel", "Unfinished River Seasons XP" },
            { "Nano Pylon", "Unfinished Soft Pepper Pistol" },
            { "Energy Pack Interface", "Unfinished Sol Chironis Systems" },
            { "Triple Pulse Enabler", "Unfinished Summer SMP" },
            { "Gyro Stabilizing Unit", "Unfinished Summer SMP" },
            { "Rapid-Reload-And-Fire Gyro", "Unfinished Summer SMP" },
            { "Shells Magazine", "Unfinished Summer SMP" }
        };

        public override bool CanProcess(Item item)
        {
            // Check if it's a sealed weapon receptacle
            bool isSealedReceptacle = SealedWeaponReceptacles.Any(receptacle => 
                item.Name.Equals(receptacle, StringComparison.OrdinalIgnoreCase));

            // Check if it's an upgrade component
            bool isUpgradeComponent = UpgradeComponents.Keys.Any(component => 
                item.Name.Equals(component, StringComparison.OrdinalIgnoreCase));

            // Check if it's an unfinished weapon
            bool isUnfinishedWeapon = item.Name.StartsWith("Unfinished ", StringComparison.OrdinalIgnoreCase);

            bool canProcess = isSealedReceptacle || isUpgradeComponent || isUnfinishedWeapon;
            
            RecipeUtilities.LogDebug($"[SEALED WEAPON CHECK] Item: '{item.Name}' -> Can process: {canProcess} (Receptacle: {isSealedReceptacle}, Component: {isUpgradeComponent}, Unfinished: {isUnfinishedWeapon})");
            return canProcess;
        }

        protected override async Task ProcessRecipeLogic(Item item, Container targetContainer)
        {
            // CRITICAL FIX: Process ALL possible combinations in current inventory state
            // Instead of processing one item at a time, find all possible sealed weapon combinations
            await ProcessAllPossibleSealedWeapons();
        }

        /// <summary>
        /// Comprehensive sealed weapon processing - finds all possible combinations and processes them until complete
        /// </summary>
        private async Task ProcessAllPossibleSealedWeapons()
        {
            RecipeUtilities.LogDebug($"[{RecipeName}] Starting comprehensive sealed weapon processing");

            int maxIterations = 20; // Safety limit
            int iteration = 0;
            bool foundCombination = true;

            while (foundCombination && iteration < maxIterations)
            {
                iteration++;
                foundCombination = false;
                RecipeUtilities.LogDebug($"[{RecipeName}] === Processing iteration {iteration} ===");

                // Stage 1: Process all sealed weapon receptacles with hacker tools
                foundCombination |= await ProcessAllStage1Combinations();

                // Stage 2: Process all upgrade components with unfinished weapons
                foundCombination |= await ProcessAllStage2Combinations();

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

            RecipeUtilities.LogDebug($"[{RecipeName}] Comprehensive sealed weapon processing completed after {iteration} iterations");
        }

        /// <summary>
        /// Process all possible Stage 1 combinations (sealed receptacles + hacker tools)
        /// </summary>
        private async Task<bool> ProcessAllStage1Combinations()
        {
            bool foundAny = false;

            // Find all sealed weapon receptacles in inventory
            var sealedReceptacles = Inventory.Items.Where(invItem =>
                SealedWeaponReceptacles.Any(receptacle => invItem.Name.Equals(receptacle, StringComparison.OrdinalIgnoreCase))).ToList();

            if (sealedReceptacles.Any())
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found {sealedReceptacles.Count} sealed weapon receptacles for Stage 1 processing");

                foreach (var receptacle in sealedReceptacles)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Processing Stage 1 for: {receptacle.Name}");
                    await ProcessStage1(receptacle);
                    foundAny = true;

                    // Small delay between combinations
                    await Task.Delay(100);
                }
            }

            return foundAny;
        }

        /// <summary>
        /// Process all possible Stage 2 combinations (upgrade components + unfinished weapons)
        /// </summary>
        private async Task<bool> ProcessAllStage2Combinations()
        {
            bool foundAny = false;

            // Find all upgrade components in inventory
            var upgradeComponents = Inventory.Items.Where(invItem =>
                UpgradeComponents.Keys.Any(component => invItem.Name.Equals(component, StringComparison.OrdinalIgnoreCase))).ToList();

            // Find all unfinished weapons in inventory
            var unfinishedWeapons = Inventory.Items.Where(invItem =>
                invItem.Name.StartsWith("Unfinished ", StringComparison.OrdinalIgnoreCase)).ToList();

            if (upgradeComponents.Any() && unfinishedWeapons.Any())
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found {upgradeComponents.Count} upgrade components and {unfinishedWeapons.Count} unfinished weapons for Stage 2 processing");

                foreach (var component in upgradeComponents)
                {
                    // Find compatible unfinished weapon for this component
                    if (UpgradeComponents.TryGetValue(component.Name, out string requiredUnfinished))
                    {
                        var compatibleWeapon = unfinishedWeapons.FirstOrDefault(weapon =>
                            weapon.Name.Equals(requiredUnfinished, StringComparison.OrdinalIgnoreCase));

                        if (compatibleWeapon != null)
                        {
                            RecipeUtilities.LogDebug($"[{RecipeName}] Processing Stage 2: {component.Name} + {compatibleWeapon.Name}");
                            await ProcessStage2(component, compatibleWeapon);
                            foundAny = true;

                            // Small delay between combinations
                            await Task.Delay(100);
                        }
                        else
                        {
                            RecipeUtilities.LogDebug($"[{RecipeName}] No compatible unfinished weapon found for {component.Name} (needs {requiredUnfinished})");
                        }
                    }
                }
            }
            else if (upgradeComponents.Any())
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found {upgradeComponents.Count} upgrade components but no unfinished weapons for Stage 2");
            }

            return foundAny;
        }

        /// <summary>
        /// Stage 1: Process sealed weapon receptacle with Advanced Hacker Tool
        /// </summary>
        private async Task ProcessStage1(Item sealedReceptacle)
        {
            // CRITICAL RULE: This recipe MUST NEVER use bot's personal tools
            // ONLY use player-provided Advanced Hacker Tool
            RecipeUtilities.LogDebug($"[{RecipeName}] STAGE 1: Looking for player-provided Advanced Hacker Tool ONLY");

            var playerHackerTool = await FindPlayerProvidedHackerTool();
            if (playerHackerTool == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] ❌ CRITICAL: No player-provided Advanced Hacker Tool found - CANNOT PROCESS STAGE 1");
                RecipeUtilities.LogDebug($"[{RecipeName}] ❌ Stage 1 requires player to provide their own Advanced Hacker Tool");
                return;
            }

            // Process using unified core
            RecipeUtilities.LogDebug($"[{RecipeName}] STAGE 1: Processing {sealedReceptacle.Name} with player-provided {playerHackerTool.Name}");
            await CombineItems(playerHackerTool, sealedReceptacle);

            // Check result
            var unfinishedCount = Inventory.Items.Where(invItem => invItem.Name.StartsWith("Unfinished ")).Count();
            RecipeUtilities.LogDebug($"[{RecipeName}] STAGE 1 Completed - now have {unfinishedCount} Unfinished weapons in inventory");
        }

        /// <summary>
        /// Stage 2: Process upgrade component with unfinished weapon
        /// </summary>
        private async Task ProcessStage2(Item upgradeComponent, Item unfinishedWeapon)
        {
            // Check if this upgrade component is compatible with this unfinished weapon
            if (UpgradeComponents.TryGetValue(upgradeComponent.Name, out string requiredUnfinished))
            {
                if (!unfinishedWeapon.Name.Equals(requiredUnfinished, StringComparison.OrdinalIgnoreCase))
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] STAGE 2: Upgrade component {upgradeComponent.Name} is not compatible with {unfinishedWeapon.Name}");
                    RecipeUtilities.LogDebug($"[{RecipeName}] STAGE 2: Expected {requiredUnfinished}");
                    return;
                }
            }

            // Process using unified core
            RecipeUtilities.LogDebug($"[{RecipeName}] STAGE 2: Processing {upgradeComponent.Name} with {unfinishedWeapon.Name}");
            await CombineItems(upgradeComponent, unfinishedWeapon);

            // Check result - look for completed weapons (anything that's not "Unfinished")
            var completedWeapons = Inventory.Items.Where(invItem => 
                !invItem.Name.StartsWith("Unfinished ") && 
                !invItem.Name.Contains("Sealed Weapon Receptacle") &&
                !UpgradeComponents.Keys.Contains(invItem.Name)).ToList();

            RecipeUtilities.LogDebug($"[{RecipeName}] STAGE 2 Completed - now have {completedWeapons.Count} completed weapons in inventory");
        }

        /// <summary>
        /// Finds player-provided Advanced Hacker Tool, excluding bot's personal tools
        /// </summary>
        /// <returns>Player-provided Advanced Hacker Tool or null</returns>
        private async Task<Item> FindPlayerProvidedHackerTool()
        {
            // Check inventory first for Advanced Hacker Tool specifically
            var hackerTool = Inventory.Items.FirstOrDefault(item => 
                item.Name.Contains("Advanced Hacker Tool") && 
                !RecipeUtilities.IsBotPersonalTool(item));

            if (hackerTool != null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found player-provided Advanced Hacker Tool in inventory: {hackerTool.Name}");
                return hackerTool;
            }

            // If not in inventory, check if we can pull from player bags
            // This will only pull from bags that came from the current trade
            if (RecipeUtilities.FindAndPullPlayerProvidedTool("Advanced Hacker Tool"))
            {
                // Wait a moment for tool to move to inventory
                await Task.Delay(100);
                hackerTool = Inventory.Items.FirstOrDefault(item => 
                    item.Name.Contains("Advanced Hacker Tool") && 
                    !RecipeUtilities.IsBotPersonalTool(item));
                
                if (hackerTool != null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Pulled player-provided Advanced Hacker Tool from bag: {hackerTool.Name}");
                    return hackerTool;
                }
            }

            RecipeUtilities.LogDebug($"[{RecipeName}] No player-provided Advanced Hacker Tool found");
            return null;
        }

        public override RecipeAnalysisResult AnalyzeItems(List<Item> items)
        {
            return AnalyzeItemsShared(items, "Sealed Weapon Processing");
        }
    }
}
