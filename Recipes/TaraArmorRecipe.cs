using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AOSharp.Clientless;
using AOSharp.Common.GameData;

namespace Craftbot.Recipes
{
    /// <summary>
    /// Handles Tara armor processing using Mass Relocating Robot (Shape Soft Armor) on dragon materials
    /// </summary>
    public class TaraArmorRecipe : BaseRecipeProcessor
    {
        public override string RecipeName => "Tara Armor";

        private static readonly string[] TaraItems = {
            "Chunk of Living Dragon Flesh",
            "Lump of Living Dragon Marrow",
            "Patch of Living Dragon Skin",
            "Piece of Living Dragon Wing",
            "Shard of Living Dragon Scale",
            "Living Dragon Claws",
            "Shard of Living Dragon Skull"
        };

        public override bool CanProcess(Item item)
        {
            bool canProcess = TaraItems.Any(taraItem => 
                item.Name.Equals(taraItem, StringComparison.OrdinalIgnoreCase));
            
            RecipeUtilities.LogDebug($"[TARA CHECK] Item: '{item.Name}' -> Can process: {canProcess}");
            return canProcess;
        }

        protected override async Task ProcessRecipeLogic(Item item, Container targetContainer)
        {
            RecipeUtilities.LogDebug($"[{RecipeName}] Starting ProcessRecipeLogic for {item.Name}");

            // Get Mass Relocating Robot (Shape Soft Armor) tool using unified core
            var robot = FindTool("Mass Relocating Robot (Shape Soft Armor)");
            if (robot == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] ❌ Mass Relocating Robot (Shape Soft Armor) not found - cannot process {item.Name}");
                RecipeUtilities.LogDebug($"[{RecipeName}] ⚠️ CRITICAL: Item {item.Name} will be left unprocessed but should still be returned to player");
                return;
            }

            // Find the specific tara part in inventory using unified core
            var taraPart = Inventory.Items.FirstOrDefault(invItem =>
                CanProcess(invItem) && invItem.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase));

            if (taraPart == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] ❌ Could not find {item.Name} in inventory - this is a critical bug!");
                RecipeUtilities.LogDebug($"[{RecipeName}] Available inventory items:");
                foreach (var invItem in Inventory.Items.Where(i => i.Slot.Type == IdentityType.Inventory && !(i.Slot.Instance >= (int)EquipSlot.Weap_Hud1 && i.Slot.Instance <= (int)EquipSlot.Imp_Feet)))
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] - {invItem.Name}");
                }
                return;
            }

            // Process using unified core
            RecipeUtilities.LogDebug($"[{RecipeName}] Processing {taraPart.Name} with {robot.Name}");
            await CombineItems(robot, taraPart);

            // Check result - take snapshot to prevent collection modification issues
            var inventorySnapshot = Inventory.Items.ToList();
            var taraArmor = inventorySnapshot.Where(invItem =>
                invItem.Name.Contains("Tara") ||
                invItem.Name.Contains("Dragon")).ToList();

            if (taraArmor.Any())
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] ✅ Successfully created {taraArmor.Count} Tara armor piece(s)");
                foreach (var armor in taraArmor)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Created: {armor.Name}");
                }
            }
            else
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] ⚠️ No Tara armor pieces found after processing - this may indicate processing failed");
            }

            RecipeUtilities.LogDebug($"[{RecipeName}] Completed ProcessRecipeLogic for {item.Name}");
        }

        public override RecipeAnalysisResult AnalyzeItems(List<Item> items)
        {
            return AnalyzeItemsShared(items, "Dragon Material Processing");
        }




    }
}
