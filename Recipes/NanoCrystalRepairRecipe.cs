using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AOSharp.Clientless;

namespace Craftbot.Recipes
{
    /// <summary>
    /// Handles nano crystal repair using player-provided Nano Programming Interface
    /// SPECIAL RULE: Only uses player-provided tools, never touches bot's personal tools
    /// Requires 1 Nano Programming Interface per crystal
    /// </summary>
    public class NanoCrystalRepairRecipe : BaseRecipeProcessor
    {
        public override string RecipeName => "Nano Crystal Repair";

        /// <summary>
        /// CRITICAL: Use single-item processing so each crystal gets processed individually
        /// This ensures we find a new player-provided tool for each crystal
        /// </summary>
        public override bool UsesSingleItemProcessing => true;

        private static readonly string[] DamagedCrystalPrefixes = {
            "Badly Corroded Crystal",
            "Failed Repaired Crystal",
            "Badly Eroded Crystal",
            "Hacked Corroded Crystal",
            "Blood Stained and Corroded Crystal",
            "Overcharged Corroded Nano Crystal",
            "Severly Corroded Shadow Crystal",
            "Cracked and Miskept Shadow Crystal",
            "Snow Crashed Shadow Crystal",
            "Cracked Crystal",
            "Tainted Shadow Crystal",
            "Dirty Money Shadow Crystal",
            "Weird looking"
        };

        public override bool CanProcess(Item item)
        {
            bool canProcess = DamagedCrystalPrefixes.Any(prefix => 
                item.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            
            RecipeUtilities.LogDebug($"[CRYSTAL REPAIR CHECK] Item: '{item.Name}' -> Can process: {canProcess}");
            return canProcess;
        }

        protected override async Task ProcessRecipeLogic(Item item, Container targetContainer)
        {
            // CRITICAL RULE: This recipe MUST NEVER use bot's personal tools
            // ONLY use player-provided Nano Programming Interface
            // Requires 1 tool per crystal
            RecipeUtilities.LogDebug($"[{RecipeName}] CRITICAL: Looking for player-provided Nano Programming Interface ONLY - NEVER touching bot's tools");
            
            var playerNanoTool = await FindPlayerProvidedNanoTool();
            if (playerNanoTool == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] ❌ CRITICAL: No player-provided Nano Programming Interface found - CANNOT PROCESS");
                RecipeUtilities.LogDebug($"[{RecipeName}] ❌ This recipe requires player to provide their own Nano Programming Interface (1 per crystal)");
                return;
            }
            
            RecipeUtilities.LogDebug($"[{RecipeName}] ✅ CONFIRMED: Using player-provided {playerNanoTool.Name} - NOT touching bot's tools");

            // Find the damaged crystal in inventory
            var crystal = Inventory.Items.FirstOrDefault(invItem =>
                CanProcess(invItem) && invItem.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase));

            if (crystal == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Could not find {item.Name} in inventory");
                return;
            }

            // Process using unified core
            RecipeUtilities.LogDebug($"[{RecipeName}] Processing {crystal.Name} with player-provided {playerNanoTool.Name}");
            await CombineItems(playerNanoTool, crystal);

            // Check result - look for any crystal that doesn't match the damaged prefixes
            var repairedCount = Inventory.Items.Where(invItem => 
                invItem.Name.Contains("Crystal") && 
                !DamagedCrystalPrefixes.Any(prefix => invItem.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))).Count();
            
            RecipeUtilities.LogDebug($"[{RecipeName}] Completed processing - now have {repairedCount} repaired crystals in inventory");
        }

        /// <summary>
        /// Finds player-provided Nano Programming Interface, excluding bot's personal tools
        /// </summary>
        /// <returns>Player-provided Nano Programming Interface or null</returns>
        private async Task<Item> FindPlayerProvidedNanoTool()
        {
            // Check inventory first for Nano Programming Interface
            var nanoTool = Inventory.Items.FirstOrDefault(item => 
                item.Name.Contains("Nano Programming Interface") && 
                !RecipeUtilities.IsBotPersonalTool(item));

            if (nanoTool != null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found player-provided Nano Programming Interface in inventory: {nanoTool.Name}");
                return nanoTool;
            }

            // If not in inventory, check if we can pull from player bags
            // This will only pull from bags that came from the current trade
            if (RecipeUtilities.FindAndPullPlayerProvidedTool("Nano Programming Interface"))
            {
                // Wait a moment for tool to move to inventory
                await Task.Delay(100);
                nanoTool = Inventory.Items.FirstOrDefault(item => 
                    item.Name.Contains("Nano Programming Interface") && 
                    !RecipeUtilities.IsBotPersonalTool(item));
                
                if (nanoTool != null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Pulled player-provided Nano Programming Interface from bag: {nanoTool.Name}");
                    return nanoTool;
                }
            }

            RecipeUtilities.LogDebug($"[{RecipeName}] No player-provided Nano Programming Interface found");
            return null;
        }

        public override RecipeAnalysisResult AnalyzeItems(List<Item> items)
        {
            return AnalyzeItemsShared(items, "Nano Crystal Repair");
        }
    }
}
