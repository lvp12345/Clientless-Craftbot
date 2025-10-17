using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AOSharp.Clientless;

namespace Craftbot.Recipes
{
    /// <summary>
    /// Handles Trimmer processing using Trimmer Casing on Smelly Liquid
    /// </summary>
    public class TrimmerRecipe : BaseRecipeProcessor
    {
        public override string RecipeName => "Trimmer";

        public override bool CanProcess(Item item)
        {
            bool canProcess = item.Name.Equals("Trimmer Casing", StringComparison.OrdinalIgnoreCase) ||
                             item.Name.Equals("Smelly Liquid", StringComparison.OrdinalIgnoreCase);
            
            RecipeUtilities.LogDebug($"[TRIMMER CHECK] Item: '{item.Name}' -> Can process: {canProcess}");
            return canProcess;
        }

        protected override async Task ProcessRecipeLogic(Item item, Container targetContainer)
        {
            // Find Trimmer Casing (source/tool) using unified core
            var trimmerCasing = Inventory.Items.FirstOrDefault(invItem =>
                invItem.Name.Equals("Trimmer Casing", StringComparison.OrdinalIgnoreCase));

            if (trimmerCasing == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Trimmer Casing not found - cannot process");
                return;
            }

            // Find Smelly Liquid (target) in inventory using unified core
            var smellyLiquid = Inventory.Items.FirstOrDefault(invItem =>
                invItem.Name.Equals("Smelly Liquid", StringComparison.OrdinalIgnoreCase));

            if (smellyLiquid == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Could not find Smelly Liquid in inventory");
                return;
            }

            // Process using unified core
            RecipeUtilities.LogDebug($"[{RecipeName}] Processing {smellyLiquid.Name} with {trimmerCasing.Name}");
            await CombineItems(trimmerCasing, smellyLiquid);

            // Check result - look for Trimmer results (broader check for variations)
            var trimmerResult = Inventory.Items.Where(invItem =>
                (invItem.Name.Contains("Trimmer") && !invItem.Name.Contains("Casing")) ||
                invItem.Name.Contains("Improve Actuators")).ToList();

            if (trimmerResult.Any())
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] ✅ Successfully created {trimmerResult.Count} Trimmer item(s)");
                foreach (var result in trimmerResult)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Created: {result.Name}");
                }
            }
            else
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] ⚠️ No Trimmer items found after processing - this may indicate processing failed");
            }
        }

        public override RecipeAnalysisResult AnalyzeItems(List<Item> items)
        {
            return AnalyzeItemsShared(items, "Trimmer Processing");
        }
    }
}
