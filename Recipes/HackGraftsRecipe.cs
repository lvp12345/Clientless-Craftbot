using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AOSharp.Clientless;

namespace Craftbot.Recipes
{
    /// <summary>
    /// Handles hack grafts processing using player-provided Hacker Tool
    /// SPECIAL RULE: Only uses player-provided tools, never touches bot's personal tools
    /// Recipe: Any Hacker Tool + Any Boosted Graft = Hacked Boosted-Graft
    /// </summary>
    public class HackGraftsRecipe : BaseRecipeProcessor
    {
        public override string RecipeName => "Hack Grafts";

        public override bool CanProcess(Item item)
        {
            // CRITICAL FIX: Only process UN-HACKED boosted grafts, NOT already hacked ones
            // Check for "boosted graft" or "boosted-graft" but EXCLUDE items that already contain "hacked"
            var itemNameLower = item.Name.ToLower();
            bool isAlreadyHacked = itemNameLower.Contains("hacked");
            bool isBoostedGraft = itemNameLower.Contains("boosted graft") || itemNameLower.Contains("boosted-graft");

            bool canProcess = isBoostedGraft && !isAlreadyHacked;

            RecipeUtilities.LogDebug($"[HACK GRAFTS CHECK] Item: '{item.Name}' -> Can process: {canProcess} (BoostedGraft: {isBoostedGraft}, AlreadyHacked: {isAlreadyHacked})");
            return canProcess;
        }

        protected override async Task ProcessRecipeLogic(Item item, Container targetContainer)
        {
            // CRITICAL RULE: This recipe MUST NEVER use bot's personal tools
            // ONLY use player-provided Hacker Tool
            RecipeUtilities.LogDebug($"[{RecipeName}] CRITICAL: Looking for player-provided Hacker Tool ONLY - NEVER touching bot's tools");

            var playerHackerTool = await FindPlayerProvidedHackerTool();
            if (playerHackerTool == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] ❌ CRITICAL: No player-provided Hacker Tool found - CANNOT PROCESS");
                RecipeUtilities.LogDebug($"[{RecipeName}] ❌ This recipe requires player to provide their own Hacker Tool");
                return;
            }

            // Find the boosted graft in inventory
            var boostedGraft = Inventory.Items.FirstOrDefault(invItem => CanProcess(invItem));

            if (boostedGraft == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Could not find Boosted Graft in inventory");
                return;
            }

            // Process using unified core
            RecipeUtilities.LogDebug($"[{RecipeName}] Processing {boostedGraft.Name} with player-provided {playerHackerTool.Name}");
            await CombineItems(playerHackerTool, boostedGraft);

            // Check result - use ToList() to avoid "Collection was modified" errors
            var inventorySnapshot = Inventory.Items.ToList();
            var hackedGraftCount = inventorySnapshot.Where(invItem => invItem.Name.Contains("Hacked") && invItem.Name.Contains("Graft")).Count();
            RecipeUtilities.LogDebug($"[{RecipeName}] Completed processing - now have {hackedGraftCount} Hacked Graft items in inventory");
        }

        /// <summary>
        /// CRITICAL: Finds player-provided Hacker Tool ONLY - never touches bot's personal tools
        /// This prevents bot tool destruction since this recipe consumes the tool
        /// </summary>
        private async Task<Item> FindPlayerProvidedHackerTool()
        {
            // Look for any tool with "hacker tool" in the name in inventory
            var hackerTool = Inventory.Items.FirstOrDefault(item =>
                item.Name.ToLower().Contains("hacker tool"));

            if (hackerTool != null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found player-provided Hacker Tool in inventory: {hackerTool.Name}");
                return hackerTool;
            }

            // If not in inventory, check if player provided one in bags
            // Look through all open containers for player-provided hacker tools
            foreach (var backpack in Inventory.Containers.Where(bp => bp.IsOpen))
            {
                var bagHackerTool = backpack.Items.FirstOrDefault(item =>
                    item.Name.ToLower().Contains("hacker tool"));

                if (bagHackerTool != null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Found player-provided Hacker Tool in bag {backpack.Item?.Name ?? "Unknown"}: {bagHackerTool.Name}");
                    bagHackerTool.MoveToInventory();
                    await Task.Delay(100); // Wait for tool to move
                    return bagHackerTool;
                }
            }

            RecipeUtilities.LogDebug($"[{RecipeName}] ❌ CRITICAL: No player-provided Hacker Tool found anywhere");
            RecipeUtilities.LogDebug($"[{RecipeName}] ❌ Player must provide their own Hacker Tool for this recipe");
            return null;
        }

        public override RecipeAnalysisResult AnalyzeItems(List<Item> items)
        {
            return AnalyzeItemsShared(items, "Hack Grafts Processing");
        }
    }
}
