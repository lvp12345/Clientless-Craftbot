using System;
using System.Linq;
using System.Threading.Tasks;
using AOSharp.Core;
using AOSharp.Core.Inventory;
using System.Collections.Generic;

namespace Craftbot.Recipes
{
    /// <summary>
    /// Handles Salabim Shotgun processing using unified recipe patterns
    /// Recipe: Fused Biotech Material + Biomaterial Tubing = Salabim Shotgun
    /// </summary>
    public class SalabimShotgunRecipe : BaseRecipeProcessor
    {
        public override string RecipeName => "Salabim Shotgun";



        private static readonly string[] ProcessableItems = {
            "Fused Biotech Material",
            "Biomaterial Tubing"
        };

        public override bool CanProcess(Item item)
        {
            bool canProcess = ProcessableItems.Any(processableItem => 
                item.Name.Contains(processableItem, StringComparison.OrdinalIgnoreCase));
            
            RecipeUtilities.LogDebug($"[SALABIM SHOTGUN CHECK] Item: '{item.Name}' -> Can process: {canProcess}");
            return canProcess;
        }

        protected override async Task ProcessRecipeLogic(Item item, Container targetContainer)
        {
            // Check if we have both required components
            var fusedBiotechMaterial = Inventory.Items.FirstOrDefault(invItem => 
                invItem.Name.Contains("Fused Biotech Material", StringComparison.OrdinalIgnoreCase));
            
            var biomaterialTubing = Inventory.Items.FirstOrDefault(invItem => 
                invItem.Name.Contains("Biomaterial Tubing", StringComparison.OrdinalIgnoreCase));

            if (fusedBiotechMaterial == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Could not find Fused Biotech Material in inventory");
                return;
            }

            if (biomaterialTubing == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Could not find Biomaterial Tubing in inventory");
                return;
            }

            // Process using unified core
            RecipeUtilities.LogDebug($"[{RecipeName}] Processing {fusedBiotechMaterial.Name} with {biomaterialTubing.Name}");
            await CombineItems(fusedBiotechMaterial, biomaterialTubing);

            // Check result
            var shotgunCount = Inventory.Items.Where(invItem => 
                invItem.Name.Contains("Salabim Shotgun", StringComparison.OrdinalIgnoreCase)).Count();
            RecipeUtilities.LogDebug($"[{RecipeName}] Completed processing - now have {shotgunCount} Salabim Shotgun items in inventory");
        }

        public override RecipeAnalysisResult AnalyzeItems(List<Item> items)
        {
            return AnalyzeItemsShared(items, "Salabim Shotgun Processing");
        }
    }
}
