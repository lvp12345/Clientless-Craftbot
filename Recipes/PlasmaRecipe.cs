using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AOSharp.Clientless;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace Craftbot.Recipes
{
    /// <summary>
    /// Handles plasma processing using Bio-Comminutor on monster parts
    /// </summary>
    public class PlasmaRecipe : BaseRecipeProcessor
    {
        public override string RecipeName => "Plasma";



        private static readonly string[] PlasmaItems = {
            "Monster Parts",
            "Pelted Monster Parts",
            "Pelted Monster Parts with Ivory",
            "Monster Parts with Ivory"
        };

        public override bool CanProcess(Item item)
        {
            // First check exact matches for known monster part types
            bool exactMatch = PlasmaItems.Any(plasmaItem =>
                item.Name.Equals(plasmaItem, StringComparison.OrdinalIgnoreCase));

            // Also check for any item containing "Monster Parts" to catch variations
            bool containsMatch = item.Name.ToLower().Contains("monster parts");

            bool canProcess = exactMatch || containsMatch;

            RecipeUtilities.LogDebug($"[PLASMA CHECK] Item: '{item.Name}' -> Can process: {canProcess} (Exact: {exactMatch}, Contains: {containsMatch})");
            return canProcess;
        }

        protected override async Task ProcessRecipeLogic(Item item, Container targetContainer)
        {
            // CRITICAL FIX: Try to use player-provided Bio-Comminutor FIRST
            // This prevents the bot from giving away its own tool
            var bio = Inventory.Items.FirstOrDefault(invItem =>
                invItem.Name.Contains("Bio-Comminutor") &&
                !RecipeUtilities.IsBotPersonalTool(invItem));

            // If no player-provided bio in inventory, try to pull from player bags
            if (bio == null)
            {
                if (RecipeUtilities.FindAndPullPlayerProvidedTool("Bio-Comminutor"))
                {
                    // Wait a moment for tool to move to inventory
                    await Task.Delay(100);
                    bio = Inventory.Items.FirstOrDefault(invItem =>
                        invItem.Name.Contains("Bio-Comminutor") &&
                        !RecipeUtilities.IsBotPersonalTool(invItem));

                    if (bio != null)
                    {
                        RecipeUtilities.LogDebug($"[{RecipeName}] Pulled player-provided Bio-Comminutor from bag: {bio.Name}");
                    }
                }
            }

            // If still no player-provided bio, use bot's tool as fallback
            if (bio == null)
            {
                bio = FindTool("Bio-Comminutor");
            }

            if (bio == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Bio-Comminutor not found - cannot process");
                return;
            }

            // Find the specific monster part in inventory using unified core
            var monsterPart = Inventory.Items.FirstOrDefault(invItem =>
                CanProcess(invItem) && invItem.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase));

            if (monsterPart == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Could not find {item.Name} in inventory");
                return;
            }

            // Process using unified core
            RecipeUtilities.LogDebug($"[{RecipeName}] Processing {monsterPart.Name} with {bio.Name}");
            await CombineItems(bio, monsterPart);

            // Check result
            var plasmaCount = Inventory.Items.Where(invItem => invItem.Name.Contains("Blood Plasma")).Count();
            RecipeUtilities.LogDebug($"[{RecipeName}] Completed processing - now have {plasmaCount} Blood Plasma in inventory");
        }

        public override RecipeAnalysisResult AnalyzeItems(List<Item> items)
        {
            return AnalyzeItemsShared(items, "Monster Parts Processing");
        }
    }
}
