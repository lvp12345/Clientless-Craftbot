using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AOSharp.Clientless;

namespace Craftbot.Recipes
{
    /// <summary>
    /// Handles smelting processing using Precious Metal Reclaimer on jewelry and metal items
    /// </summary>
    public class SmeltingRecipe : BaseRecipeProcessor
    {
        public override string RecipeName => "Smelting";

        private static readonly string[] SmeltingItems = {
            "Golden Sphere",
            "Golden Ring",
            "Marriage Ring",
            "Bracer of Shielding - Elements",
            "Bracelet of Ka",
            "Golden Bracer",
            "Golden Nugget",
            "Pink Ring",
            "Silver Nugget",
            "Flower Ring",
            "OT Ring",
            "Ring of Luck - Defensive",
            "Ring of Power - Elements",
            "Ring of Suffering",
            "Ring of Zern",
            "Snake Ring",
            "Toe-Ring",
            "Ring of Luck - Offensive",
            "Bracer of Reflection - Elements",
            "Engagement Ring",
            // Additional variations to catch different naming
            "Ring of Flowers",
            "Flower-Ring",
            "Engagement-Ring"
        };

        public override bool CanProcess(Item item)
        {
            bool canProcess = SmeltingItems.Any(smeltingItem => 
                item.Name.Equals(smeltingItem, StringComparison.OrdinalIgnoreCase));

            // Additional flexible matching for rings that might have slight name variations
            if (!canProcess && item.Name.ToLower().Contains("ring"))
            {
                // Check for partial matches on ring names
                if (item.Name.ToLower().Contains("flower"))
                    canProcess = true;
                else if (item.Name.ToLower().Contains("engagement"))
                    canProcess = true;
            }

            RecipeUtilities.LogDebug($"[SMELTING CHECK] Item: '{item.Name}' -> Can process: {canProcess}");
            return canProcess;
        }

        protected override async Task ProcessRecipeLogic(Item item, Container targetContainer)
        {
            // CRITICAL FIX: Try to use player-provided Precious Metal Reclaimer FIRST
            // This prevents the bot from giving away its own tool
            var reclaimer = Inventory.Items.FirstOrDefault(invItem =>
                invItem.Name.Contains("Precious Metal Reclaimer") &&
                !RecipeUtilities.IsBotPersonalTool(invItem));

            // If no player-provided reclaimer in inventory, try to pull from player bags
            if (reclaimer == null)
            {
                if (RecipeUtilities.FindAndPullPlayerProvidedTool("Precious Metal Reclaimer"))
                {
                    // Wait a moment for tool to move to inventory
                    await Task.Delay(100);
                    reclaimer = Inventory.Items.FirstOrDefault(invItem =>
                        invItem.Name.Contains("Precious Metal Reclaimer") &&
                        !RecipeUtilities.IsBotPersonalTool(invItem));

                    if (reclaimer != null)
                    {
                        RecipeUtilities.LogDebug($"[{RecipeName}] Pulled player-provided Precious Metal Reclaimer from bag: {reclaimer.Name}");
                    }
                }
            }

            // If still no player-provided reclaimer, use bot's tool as fallback
            if (reclaimer == null)
            {
                reclaimer = FindTool("Precious Metal Reclaimer");
            }

            if (reclaimer == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Precious Metal Reclaimer not found - cannot process");
                return;
            }

            // Find the specific smelting item in inventory using unified core
            var smeltingItem = Inventory.Items.FirstOrDefault(invItem =>
                CanProcess(invItem) && invItem.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase));

            if (smeltingItem == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Could not find {item.Name} in inventory");
                return;
            }

            // Process using unified core
            RecipeUtilities.LogDebug($"[{RecipeName}] Processing {smeltingItem.Name} with {reclaimer.Name}");
            await CombineItems(reclaimer, smeltingItem);

            // Check result
            var preciousMetals = Inventory.Items.Where(invItem =>
                invItem.Name.Contains("Gold") ||
                invItem.Name.Contains("Silver") ||
                invItem.Name.Contains("Precious")).ToList();

            if (preciousMetals.Any())
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Successfully created {preciousMetals.Count} precious metal(s)");
            }
        }

        public override RecipeAnalysisResult AnalyzeItems(List<Item> items)
        {
            return AnalyzeItemsShared(items, "Metal Reclamation");
        }




    }
}
