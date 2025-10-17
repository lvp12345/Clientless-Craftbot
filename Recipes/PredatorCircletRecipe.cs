using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AOSharp.Clientless;

namespace Craftbot.Recipes
{
    /// <summary>
    /// Handles Predator's Circlet processing using Hacker Tool on Predator Armor Facemask
    /// Recipe: Hacker Tool + Predator Armor Facemask = Predator's Circlet
    /// </summary>
    public class PredatorCircletRecipe : BaseRecipeProcessor
    {
        public override string RecipeName => "Predator's Circlet";

        public override bool CanProcess(Item item)
        {
            // Check for Predator Armor Facemask
            bool canProcess = item.Name.Equals("Predator Armor Facemask", StringComparison.OrdinalIgnoreCase);

            RecipeUtilities.LogDebug($"[PREDATOR CIRCLET CHECK] Item: '{item.Name}' -> Can process: {canProcess}");
            return canProcess;
        }

        protected override async Task ProcessRecipeLogic(Item item, Container targetContainer)
        {
            // Find hacker tool using standard method (moves tool to inventory if needed)
            var hackerTool = FindTool("Hacker Tool");
            if (hackerTool == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] ❌ No Hacker Tool found - cannot process");
                return;
            }

            // Find the Predator Armor Facemask in inventory
            var facemask = Inventory.Items.FirstOrDefault(invItem => CanProcess(invItem));

            if (facemask == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Could not find Predator Armor Facemask in inventory");
                return;
            }

            // Process using unified core
            RecipeUtilities.LogDebug($"[{RecipeName}] Processing {facemask.Name} with {hackerTool.Name}");
            await CombineItems(hackerTool, facemask);

            // Check result
            var circletCount = Inventory.Items.Where(invItem => invItem.Name.Contains("Predator's Circlet")).Count();
            RecipeUtilities.LogDebug($"[{RecipeName}] Completed processing - now have {circletCount} Predator's Circlet items in inventory");
        }



        /// <summary>
        /// Override the combination delay for Predator Circlet recipe
        /// Increased to 500ms to allow server more time to process the tradeskill combination
        /// </summary>
        /// <returns>Delay in milliseconds</returns>
        protected override int GetCombinationDelay()
        {
            return 500; // Increased from default 200ms to fix processing failures
        }

        public override RecipeAnalysisResult AnalyzeItems(List<Item> items)
        {
            return AnalyzeItemsShared(items, "Predator's Circlet Processing");
        }
    }
}
