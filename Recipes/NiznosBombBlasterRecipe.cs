using System;
using System.Linq;
using System.Threading.Tasks;
using AOSharp.Core;
using AOSharp.Core.Inventory;
using System.Collections.Generic;

namespace Craftbot.Recipes
{
    /// <summary>
    /// Handles Nizno's Bomb Blaster processing using unified recipe patterns
    /// Recipe: Explosif Device + Biomaterial Tubing = Premium Nizno's Bomb Blaster
    /// </summary>
    public class NiznosBombBlasterRecipe : BaseRecipeProcessor
    {
        public override string RecipeName => "Nizno's Bomb Blaster";

        private static readonly string[] ProcessableItems = {
            "Explosif Device",
            "Biomaterial Tubing"
        };

        public override bool CanProcess(Item item)
        {
            bool canProcess = ProcessableItems.Any(processableItem => 
                item.Name.Contains(processableItem, StringComparison.OrdinalIgnoreCase));
            
            RecipeUtilities.LogDebug($"[NIZNO'S BOMB BLASTER CHECK] Item: '{item.Name}' -> Can process: {canProcess}");
            return canProcess;
        }

        protected override async Task ProcessRecipeLogic(Item item, Container targetContainer)
        {
            // Check if we have both required components
            var explosifDevice = Inventory.Items.FirstOrDefault(invItem => 
                invItem.Name.Contains("Explosif Device", StringComparison.OrdinalIgnoreCase));
            
            var biomaterialTubing = Inventory.Items.FirstOrDefault(invItem => 
                invItem.Name.Contains("Biomaterial Tubing", StringComparison.OrdinalIgnoreCase));

            if (explosifDevice == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Could not find Explosif Device in inventory");
                return;
            }

            if (biomaterialTubing == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Could not find Biomaterial Tubing in inventory");
                return;
            }

            // Process using unified core
            RecipeUtilities.LogDebug($"[{RecipeName}] Processing {explosifDevice.Name} with {biomaterialTubing.Name}");
            await CombineItems(explosifDevice, biomaterialTubing);

            // Check result
            var bombBlasterCount = Inventory.Items.Where(invItem => 
                invItem.Name.Contains("Premium Nizno's Bomb Blaster", StringComparison.OrdinalIgnoreCase) ||
                invItem.Name.Contains("Nizno's Bomb Blaster", StringComparison.OrdinalIgnoreCase)).Count();
            RecipeUtilities.LogDebug($"[{RecipeName}] Completed processing - now have {bombBlasterCount} Nizno's Bomb Blaster items in inventory");
        }

        public override RecipeAnalysisResult AnalyzeItems(List<Item> items)
        {
            return AnalyzeItemsShared(items, "Nizno's Bomb Blaster Processing");
        }
    }
}
