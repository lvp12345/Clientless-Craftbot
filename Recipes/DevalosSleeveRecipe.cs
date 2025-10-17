using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AOSharp.Clientless;

namespace Craftbot.Recipes
{
    /// <summary>
    /// Handles De'Valos Sleeve processing using Powdered Viral-Bots on Cyber Armor Sleeves (any quality)
    /// </summary>
    public class DevalosSleeveRecipe : BaseRecipeProcessor
    {
        public override string RecipeName => "De'Valos Sleeve";

        private static readonly string[] ProcessableItems = {
            "Powdered Viral-Bots"
        };

        public override bool CanProcess(Item item)
        {
            // Check for Powdered Viral-Bots (exact match)
            bool isPowderedViralBots = item.Name.Equals("Powdered Viral-Bots", StringComparison.OrdinalIgnoreCase);

            // Check for any Cyber Armor Sleeves (flexible matching for different quality levels)
            bool isCyberArmorSleeves = item.Name.ToLower().Contains("cyber armor sleeves");

            bool canProcess = isPowderedViralBots || isCyberArmorSleeves;

            // Enhanced logging to help identify when players provide wrong items
            if (!canProcess)
            {
                RecipeUtilities.LogDebug($"[DEVALOS CHECK] Item: '{item.Name}' -> Can process: False (Not Powdered Viral-Bots or Cyber Armor Sleeves)");
            }
            else
            {
                RecipeUtilities.LogDebug($"[DEVALOS CHECK] Item: '{item.Name}' -> Can process: {canProcess} (Viral-Bots: {isPowderedViralBots}, Cyber Sleeves: {isCyberArmorSleeves})");
            }

            return canProcess;
        }

        protected override async Task ProcessRecipeLogic(Item item, Container targetContainer)
        {
            // Find Powdered Viral-Bots (source/tool) using unified core
            var powderedViralBots = Inventory.Items.FirstOrDefault(invItem =>
                invItem.Name.Equals("Powdered Viral-Bots", StringComparison.OrdinalIgnoreCase));

            if (powderedViralBots == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Powdered Viral-Bots not found - cannot process");
                return;
            }

            // Find any Cyber Armor Sleeves (target) in inventory using flexible matching
            var cyberSleeves = Inventory.Items.FirstOrDefault(invItem =>
                invItem.Name.ToLower().Contains("cyber armor sleeves"));

            if (cyberSleeves == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Could not find any Cyber Armor Sleeves in inventory");
                return;
            }

            // Process using unified core
            RecipeUtilities.LogDebug($"[{RecipeName}] Processing {cyberSleeves.Name} with {powderedViralBots.Name}");
            await CombineItems(powderedViralBots, cyberSleeves);

            // Check result
            var devalosResult = Inventory.Items.Where(invItem =>
                invItem.Name.Contains("De'Valos") && invItem.Name.Contains("Sleeves")).ToList();

            if (devalosResult.Any())
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Successfully created {devalosResult.Count} De'Valos Sleeves");
            }
        }

        public override RecipeAnalysisResult AnalyzeItems(List<Item> items)
        {
            return AnalyzeItemsShared(items, "De'Valos Sleeve Processing");
        }
    }
}
