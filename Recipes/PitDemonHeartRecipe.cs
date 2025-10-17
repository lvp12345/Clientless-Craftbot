using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AOSharp.Clientless;

namespace Craftbot.Recipes
{
    /// <summary>
    /// Handles pit demon heart processing using player-provided Advanced Bio-Comminutor
    /// SPECIAL RULE: Only uses player-provided tools, never touches bot's personal tools
    /// </summary>
    public class PitDemonHeartRecipe : BaseRecipeProcessor
    {
        public override string RecipeName => "Pit Demon Heart";

        private static readonly string[] PitDemonItems = {
            "Pit Demon Heart"
        };

        public override bool CanProcess(Item item)
        {
            bool canProcess = PitDemonItems.Any(pitItem => 
                item.Name.Equals(pitItem, StringComparison.OrdinalIgnoreCase));
            
            RecipeUtilities.LogDebug($"[PIT DEMON CHECK] Item: '{item.Name}' -> Can process: {canProcess}");
            return canProcess;
        }

        protected override async Task ProcessRecipeLogic(Item item, Container targetContainer)
        {
            // CRITICAL RULE: This recipe MUST NEVER use bot's personal tools
            // ONLY use player-provided Advanced Bio-Comminutor
            RecipeUtilities.LogDebug($"[{RecipeName}] CRITICAL: Looking for player-provided Advanced Bio-Comminutor ONLY");

            var playerBioComm = await FindPlayerProvidedBioComminutor();
            if (playerBioComm == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] ❌ CRITICAL: No player-provided Advanced Bio-Comminutor found - CANNOT PROCESS");
                RecipeUtilities.LogDebug($"[{RecipeName}] ❌ This recipe requires player to provide their own Advanced Bio-Comminutor");
                return;
            }

            RecipeUtilities.LogDebug($"[{RecipeName}] ✅ CONFIRMED: Using player-provided {playerBioComm.Name}");

            // Find the pit demon heart in inventory
            var pitHeart = Inventory.Items.FirstOrDefault(invItem =>
                CanProcess(invItem) && invItem.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase));

            if (pitHeart == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Could not find {item.Name} in inventory");
                return;
            }

            // Process using unified core
            RecipeUtilities.LogDebug($"[{RecipeName}] Processing {pitHeart.Name} with player-provided {playerBioComm.Name}");
            await CombineItems(playerBioComm, pitHeart);

            // Check result
            var indigoCount = Inventory.Items.Where(invItem => invItem.Name.Contains("Indigo Carmine")).Count();
            RecipeUtilities.LogDebug($"[{RecipeName}] Completed processing - now have {indigoCount} Indigo Carmine in inventory");
        }

        /// <summary>
        /// Finds player-provided Bio-Comminutor (Basic or Advanced), excluding bot's personal tools
        /// </summary>
        /// <returns>Player-provided Bio-Comminutor or null</returns>
        private async Task<Item> FindPlayerProvidedBioComminutor()
        {
            // Check inventory first for ANY Bio-Comminutor (Basic or Advanced)
            var bioComm = Inventory.Items.FirstOrDefault(item =>
                item.Name.Contains("Bio-Comminutor") &&
                !RecipeUtilities.IsBotPersonalTool(item));

            if (bioComm != null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found player-provided Bio-Comminutor in inventory: {bioComm.Name}");
                return bioComm;
            }

            // If not in inventory, check if we can pull from player bags
            // This will only pull from bags that came from the current trade
            if (RecipeUtilities.FindAndPullPlayerProvidedTool("Bio-Comminutor"))
            {
                // Wait a moment for tool to move to inventory
                await Task.Delay(100);
                bioComm = Inventory.Items.FirstOrDefault(item =>
                    item.Name.Contains("Bio-Comminutor") &&
                    !RecipeUtilities.IsBotPersonalTool(item));

                if (bioComm != null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Pulled player-provided Bio-Comminutor from bag: {bioComm.Name}");
                    return bioComm;
                }
            }

            RecipeUtilities.LogDebug($"[{RecipeName}] No player-provided Bio-Comminutor found");
            return null;
        }

        public override RecipeAnalysisResult AnalyzeItems(List<Item> items)
        {
            return AnalyzeItemsShared(items, "Pit Demon Heart Processing");
        }
    }
}
