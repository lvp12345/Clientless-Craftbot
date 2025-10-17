using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AOSharp.Clientless;
using SmokeLounge.AOtomation.Messaging.GameData;
using AOSharp.Common.GameData;

namespace Craftbot.Recipes
{
    /// <summary>
    /// Handles Kyr'Ozch Bio-Material clump processing using Kyr'Ozch Structural Analyzer
    /// </summary>
    public class ClumpsRecipe : BaseRecipeProcessor
    {
        public override string RecipeName => "Clumps";

        private static readonly string[] ClumpItems = {
            "Solid Clump of Kyr'Ozch Bio-Material"
        };

        public override bool CanProcess(Item item)
        {
            bool canProcess = ClumpItems.Any(clumpItem =>
                item.Name.Equals(clumpItem, StringComparison.OrdinalIgnoreCase));

            RecipeUtilities.LogDebug($"[{RecipeName}] Item: '{item.Name}' -> Can process: {canProcess}");
            return canProcess;
        }

        protected override async Task ProcessRecipeLogic(Item item, Container targetContainer)
        {
            // Get Kyr'Ozch Structural Analyzer tool using unified core
            var analyzer = FindTool("Kyr'Ozch Structural Analyzer");
            if (analyzer == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Kyr'Ozch Structural Analyzer not found - cannot process");
                return;
            }

            // Find the specific clump in inventory using unified core
            var clump = Inventory.Items.FirstOrDefault(invItem =>
                CanProcess(invItem) && invItem.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase));

            if (clump == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Could not find {item.Name} in inventory");
                return;
            }

            // Process using unified core
            RecipeUtilities.LogDebug($"[{RecipeName}] Processing {clump.Name} with {analyzer.Name}");
            await CombineItems(analyzer, clump);

            RecipeUtilities.LogDebug($"[{RecipeName}] ✅ Completed processing {clump.Name}");
        }

        public override RecipeAnalysisResult AnalyzeItems(List<Item> items)
        {
            return AnalyzeItemsShared(items, "Kyr'Ozch Bio-Material Processing");
        }


    }
}
