using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AOSharp.Clientless;

namespace Craftbot.Recipes
{
    /// <summary>
    /// Handles Frederickson Micro-kinetic Sleeves de-hacking using player-provided Hacker Tool
    /// SPECIAL RULE: Only uses player-provided tools, never touches bot's personal tools
    /// </summary>
    public class FredericksonSleevesRecipe : BaseRecipeProcessor
    {
        public override string RecipeName => "Frederickson Sleeves De-hacking";

        private static readonly string[] FredericksonItems = {
            "Frederickson Micro-kinetic Sleeves"
        };

        public override bool CanProcess(Item item)
        {
            bool canProcess = FredericksonItems.Any(fredItem => 
                item.Name.Equals(fredItem, StringComparison.OrdinalIgnoreCase));
            
            RecipeUtilities.LogDebug($"[FREDERICKSON CHECK] Item: '{item.Name}' -> Can process: {canProcess}");
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
            
            RecipeUtilities.LogDebug($"[{RecipeName}] ✅ CONFIRMED: Using player-provided {playerHackerTool.Name} - NOT touching bot's tools");

            // Find the Frederickson sleeves in inventory
            var sleeves = Inventory.Items.FirstOrDefault(invItem =>
                CanProcess(invItem) && invItem.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase));

            if (sleeves == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Could not find {item.Name} in inventory");
                return;
            }

            // Process using unified core
            RecipeUtilities.LogDebug($"[{RecipeName}] Processing {sleeves.Name} with player-provided {playerHackerTool.Name}");
            await CombineItems(playerHackerTool, sleeves);

            // Check result
            var dehackedCount = Inventory.Items.Where(invItem => invItem.Name.Contains("De-Hacked Frederickson")).Count();
            RecipeUtilities.LogDebug($"[{RecipeName}] Completed processing - now have {dehackedCount} De-Hacked Frederickson items in inventory");
        }

        /// <summary>
        /// Finds player-provided Hacker Tool, excluding bot's personal tools
        /// </summary>
        /// <returns>Player-provided Hacker Tool or null</returns>
        private async Task<Item> FindPlayerProvidedHackerTool()
        {
            // Check inventory first for ANY Hacker Tool
            var hackerTool = Inventory.Items.FirstOrDefault(item => 
                item.Name.Contains("Hacker Tool") && 
                !RecipeUtilities.IsBotPersonalTool(item));

            if (hackerTool != null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found player-provided Hacker Tool in inventory: {hackerTool.Name}");
                return hackerTool;
            }

            // If not in inventory, check if we can pull from player bags
            // This will only pull from bags that came from the current trade
            if (RecipeUtilities.FindAndPullPlayerProvidedTool("Hacker Tool"))
            {
                // Wait a moment for tool to move to inventory
                await Task.Delay(100);
                hackerTool = Inventory.Items.FirstOrDefault(item => 
                    item.Name.Contains("Hacker Tool") && 
                    !RecipeUtilities.IsBotPersonalTool(item));
                
                if (hackerTool != null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Pulled player-provided Hacker Tool from bag: {hackerTool.Name}");
                    return hackerTool;
                }
            }

            RecipeUtilities.LogDebug($"[{RecipeName}] No player-provided Hacker Tool found");
            return null;
        }

        public override RecipeAnalysisResult AnalyzeItems(List<Item> items)
        {
            return AnalyzeItemsShared(items, "Frederickson Sleeves De-hacking");
        }
    }
}
