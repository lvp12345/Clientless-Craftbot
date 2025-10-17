using System;
using System.Linq;
using System.Threading.Tasks;
using AOSharp.Core;
using AOSharp.Core.Inventory;
using System.Collections.Generic;

namespace Craftbot.Recipes
{
    /// <summary>
    /// Handles Stalker Helmet processing using unified recipe patterns
    /// Recipe: MasterComm - Personalization Device + Stalker Carapace = Stalker Helmet
    /// </summary>
    public class StalkerHelmetRecipe : BaseRecipeProcessor
    {
        public override string RecipeName => "Stalker Helmet";



        private static readonly string[] ProcessableItems = {
            "MasterComm - Personalization Device",
            "Stalker Carapace"
        };

        public override bool CanProcess(Item item)
        {
            bool canProcess = ProcessableItems.Any(processableItem => 
                item.Name.Contains(processableItem, StringComparison.OrdinalIgnoreCase));
            
            RecipeUtilities.LogDebug($"[STALKER HELMET CHECK] Item: '{item.Name}' -> Can process: {canProcess}");
            return canProcess;
        }

        protected override async Task ProcessRecipeLogic(Item item, Container targetContainer)
        {
            // Check if we have both required components
            var personalizationDevice = Inventory.Items.FirstOrDefault(invItem => 
                invItem.Name.Contains("MasterComm - Personalization Device", StringComparison.OrdinalIgnoreCase));
            
            var stalkerCarapace = Inventory.Items.FirstOrDefault(invItem => 
                invItem.Name.Contains("Stalker Carapace", StringComparison.OrdinalIgnoreCase));

            if (personalizationDevice == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Could not find MasterComm - Personalization Device in inventory");
                return;
            }

            if (stalkerCarapace == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Could not find Stalker Carapace in inventory");
                return;
            }

            // Process using unified core
            RecipeUtilities.LogDebug($"[{RecipeName}] Processing {personalizationDevice.Name} with {stalkerCarapace.Name}");
            await CombineItems(personalizationDevice, stalkerCarapace);

            // Check result
            var stalkerHelmetCount = Inventory.Items.Where(invItem => 
                invItem.Name.Contains("Stalker Helmet", StringComparison.OrdinalIgnoreCase)).Count();
            RecipeUtilities.LogDebug($"[{RecipeName}] Completed processing - now have {stalkerHelmetCount} Stalker Helmet items in inventory");
        }

        public override RecipeAnalysisResult AnalyzeItems(List<Item> items)
        {
            return AnalyzeItemsShared(items, "Stalker Helmet Processing");
        }
    }
}
