using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AOSharp.Clientless;

namespace Craftbot.Recipes
{
    /// <summary>
    /// Handles Mantis armor processing using Mass Relocating Robot (Shape Soft Armor) on mantidae materials
    /// </summary>
    public class MantisArmorRecipe : BaseRecipeProcessor
    {
        public override string RecipeName => "Mantis Armor";



        private static readonly string[] MantisItems = {
            "Deformed Mantidae Abdomen",
            "Deformed Mantidae Femur",
            "Deformed Mantidae Head",
            "Deformed Mantidae Tarsus",
            "Deformed Mantidae Tibia",
            "Deformed Mantidae Wing"
        };

        public override bool CanProcess(Item item)
        {
            bool canProcess = MantisItems.Any(mantisItem => 
                item.Name.Equals(mantisItem, StringComparison.OrdinalIgnoreCase));
            
            RecipeUtilities.LogDebug($"[MANTIS CHECK] Item: '{item.Name}' -> Can process: {canProcess}");
            return canProcess;
        }

        protected override async Task ProcessRecipeLogic(Item item, Container targetContainer)
        {
            // Get Mass Relocating Robot (Shape Soft Armor) tool using unified core
            var robot = FindTool("Mass Relocating Robot (Shape Soft Armor)");
            if (robot == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Mass Relocating Robot (Shape Soft Armor) not found - cannot process");
                return;
            }

            // Find the specific mantis part in inventory using unified core
            var mantisPart = Inventory.Items.FirstOrDefault(invItem =>
                CanProcess(invItem) && invItem.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase));

            if (mantisPart == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Could not find {item.Name} in inventory");
                return;
            }

            // Process using unified core
            RecipeUtilities.LogDebug($"[{RecipeName}] Processing {mantisPart.Name} with {robot.Name}");
            await CombineItems(robot, mantisPart);

            // Check result
            var mantisArmor = Inventory.Items.Where(invItem =>
                invItem.Name.Contains("Mantis") ||
                invItem.Name.Contains("Mantidae")).ToList();

            if (mantisArmor.Any())
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] ✅ Successfully created {mantisArmor.Count} Mantis armor piece(s)");
                foreach (var armor in mantisArmor)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Created: {armor.Name}");
                }
            }
            else
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] ⚠️ No Mantis armor pieces found after processing - this may indicate processing failed");
            }
        }

        public override RecipeAnalysisResult AnalyzeItems(List<Item> items)
        {
            return AnalyzeItemsShared(items, "Mantidae Material Processing");
        }




    }
}
