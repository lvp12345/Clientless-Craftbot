using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AOSharp.Clientless;
using AOSharp.Common.GameData;

namespace Craftbot.Recipes
{
    /// <summary>
    /// Handles Crawler armor processing using Mass Relocating Robot (Shape Soft Armor) on crawler hide materials
    /// NOTE: Bot provides the tool - player does NOT need to provide Mass Relocating Robot
    /// </summary>
    public class CrawlerRecipe : BaseRecipeProcessor
    {
        public override string RecipeName => "Crawler Armor";

        private static readonly string[] CrawlerItems = {
            "Large Patch of Hard Skincrawler Hide",
            "Large Patch of Soft Skincrawler Hide",
            "Patch of Hard Skincrawler Hide",
            "Patch of Inflexible Skincrawler Hide",
            "Patch of Soft Skincrawler Hide",
            "Small Patch of Soft Skincrawler Hide",
            "Small Patch of Hard Skincrawler Hide"
        };

        public override bool CanProcess(Item item)
        {
            bool canProcess = CrawlerItems.Any(crawlerItem => 
                item.Name.Equals(crawlerItem, StringComparison.OrdinalIgnoreCase));
            
            RecipeUtilities.LogDebug($"[CRAWLER CHECK] Item: '{item.Name}' -> Can process: {canProcess}");
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

            // Find the specific crawler hide in inventory using unified core
            var crawlerHide = Inventory.Items.FirstOrDefault(invItem =>
                CanProcess(invItem) && invItem.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase));

            if (crawlerHide == null)
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
            RecipeUtilities.LogDebug($"[{RecipeName}] Processing {crawlerHide.Name} with {robot.Name}");
            await CombineItems(robot, crawlerHide);

            // Check result - take snapshot to prevent collection modification issues
            var inventorySnapshot = Inventory.Items.ToList();
            var crawlerArmor = inventorySnapshot.Where(invItem =>
                invItem.Name.Contains("Crawler") && 
                (invItem.Name.Contains("Body Armor") || 
                 invItem.Name.Contains("Pants") || 
                 invItem.Name.Contains("Boots") || 
                 invItem.Name.Contains("Shoulder Pad") || 
                 invItem.Name.Contains("Sleeves") || 
                 invItem.Name.Contains("Gloves") || 
                 invItem.Name.Contains("Hood"))).ToList();

            if (crawlerArmor.Any())
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] ✅ Successfully created {crawlerArmor.Count} Crawler armor piece(s)");
                foreach (var armor in crawlerArmor)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Created: {armor.Name}");
                }
            }
            else
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] ⚠️ No Crawler armor pieces found after processing - this may indicate processing failed");
            }

            RecipeUtilities.LogDebug($"[{RecipeName}] Completed ProcessRecipeLogic for {item.Name}");
        }

        public override RecipeAnalysisResult AnalyzeItems(List<Item> items)
        {
            return AnalyzeItemsShared(items, "Crawler Hide Processing");
        }
    }
}

