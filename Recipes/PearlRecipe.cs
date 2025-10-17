using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AOSharp.Clientless;

namespace Craftbot.Recipes
{
    /// <summary>
    /// Handles pearl/gem processing using Jensen Gem Cutter
    /// </summary>
    public class PearlRecipe : BaseRecipeProcessor
    {
        public override string RecipeName => "Pearl";

        private static readonly string[] PearlItems = {
            // Original items
            "Blue Pearl by Conner",
            "Crystal Sphere",
            "Ember",
            "Ember Sphere",
            "Flawless Spring Crystal",
            "Gem",
            "High Quality Silver Onyx",
            "Hot Stone",
            "Pearl of Rubi-Ka",
            "Rubi-Ka Ruby",
            "Ruby",
            "Silver Pearl",
            "Soul Fragment",
            "Gold 2 Sphere Pearl by Peters & Tool",
            "Shining 2 Sphere by Pearl, Peters & Tool",
            "Pearl",
            // Additional gems from comprehensive list
            "Almandine",
            "Amber",
            "Aquamarine",
            "Balas ruby",
            "Black opal",
            "Chrysoberyl",
            "Coral",
            "Demantoid",
            "Diamond",
            "Emerald",
            "Fire opal",
            "Jet",
            "Red beryl",
            "Ruby Pearl",
            "Sapphire",
            "Star Ruby",
            "Topaz",
            "Water opal",
            "White opal"
        };

        public override bool CanProcess(Item item)
        {
            return PearlItems.Any(pearlItem => 
                item.Name.Equals(pearlItem, StringComparison.OrdinalIgnoreCase));
        }

        protected override async Task ProcessRecipeLogic(Item item, Container targetContainer)
        {
            // Get Jensen Gem Cutter tool using unified core
            var cutter = FindTool("Jensen Gem Cutter");
            if (cutter == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Jensen Gem Cutter not found - cannot process");
                return;
            }

            // Find the specific pearl in inventory using unified core
            var pearl = Inventory.Items.FirstOrDefault(invItem =>
                CanProcess(invItem) && invItem.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase));

            if (pearl == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Could not find {item.Name} in inventory");
                return;
            }

            // Process using unified core
            RecipeUtilities.LogDebug($"[{RecipeName}] Processing {pearl.Name} with {cutter.Name}");
            await CombineItems(cutter, pearl);

            // Check result - Jensen Gem Cutter creates "Perfectly Cut [ItemName]"
            string expectedResult = $"Perfectly Cut {pearl.Name}";
            if (Inventory.Find(expectedResult, out Item perfectGem))
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Successfully created {expectedResult}");
            }
            else
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] ⚠️ Expected result '{expectedResult}' not found in inventory");
            }
        }

        public override RecipeAnalysisResult AnalyzeItems(List<Item> items)
        {
            return AnalyzeItemsShared(items, "Gem Cutting");
        }

        /* ===== PEARL BACKUP =====
         * Original Pearl recipe ProcessRecipeLogic method - kept as backup for fallback
         * This method worked perfectly for processing multiple items efficiently
         *
        protected override async Task ProcessRecipeLogic(Item item, Container targetContainer)
        {
            // Get Jensen Gem Cutter tool using unified core
            var cutter = FindTool("Jensen Gem Cutter");
            if (cutter == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Jensen Gem Cutter not found - cannot process");
                return;
            }

            // Find the specific pearl in inventory using unified core
            var pearl = Inventory.Items.FirstOrDefault(invItem =>
                CanProcess(invItem) && invItem.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase));

            if (pearl == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Could not find {item.Name} in inventory");
                return;
            }

            // Process using unified core
            RecipeUtilities.LogDebug($"[{RecipeName}] Processing {pearl.Name} with {cutter.Name}");
            await CombineItems(cutter, pearl);

            // Check result - Jensen Gem Cutter creates "Perfectly Cut [ItemName]"
            string expectedResult = $"Perfectly Cut {pearl.Name}";
            if (Inventory.Find(expectedResult, out Item perfectGem))
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Successfully created {expectedResult}");
            }
            else
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] ⚠️ Expected result '{expectedResult}' not found in inventory");
            }
        }
         * ===== END PEARL BACKUP =====
         */

    }
}
