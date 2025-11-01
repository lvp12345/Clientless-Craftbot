using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AOSharp.Clientless;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace Craftbot.Recipes
{
    /// <summary>
    /// Handles ice processing using Nano Programming Interface on Hacker ICE-Breaker Source
    /// </summary>
    public class IceRecipe : BaseRecipeProcessor
    {
        public override string RecipeName => "Ice";

        public override bool CanProcess(Item item)
        {
            // Specific Ice item for processing
            bool canProcess = item.Name.Equals("Hacker ICE-Breaker Source", StringComparison.OrdinalIgnoreCase);
            RecipeUtilities.LogDebug($"[ICE CHECK] Item: '{item.Name}' -> Can process: {canProcess}");
            return canProcess;
        }

        protected override async Task ProcessRecipeLogic(Item item, Container targetContainer)
        {
            RecipeUtilities.LogDebug($"[{RecipeName}] *** STARTING PROCESSING *** Item: {item.Name}, From bag: {targetContainer != null}");

            // Get Nano Programming Interface tool using unified core
            var nanoProgrammingInterface = FindTool("Nano Programming Interface");
            if (nanoProgrammingInterface == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] ❌ Nano Programming Interface not found - cannot process");
                return;
            }

            RecipeUtilities.LogDebug($"[{RecipeName}] ✅ Found tool: {nanoProgrammingInterface.Name}");

            // Use the item parameter directly - it's already been moved to inventory by the base processor
            RecipeUtilities.LogDebug($"[{RecipeName}] Processing {item.Name} with {nanoProgrammingInterface.Name}");
            await CombineItems(nanoProgrammingInterface, item);

            // Check result and stack if needed
            if (Inventory.Find("Upgraded Controller Recompiler Unit", out Item upgradedUnit))
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Successfully created Upgraded Controller Recompiler Unit");
                await StackUpgradedUnits();
            }
        }

        public override RecipeAnalysisResult AnalyzeItems(List<Item> items)
        {
            return AnalyzeItemsShared(items, "ICE Processing");
        }



        /// <summary>
        /// Stack multiple Upgraded Controller Recompiler Units using proper clientless approach
        /// </summary>
        private async Task StackUpgradedUnits()
        {
            try
            {
                await Task.Delay(100);
                List<Item> upgradedUnits = Inventory.Items.Where(x => x.Name == "Upgraded Controller Recompiler Unit").ToList();

                if (upgradedUnits?.Count > 1)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Found {upgradedUnits.Count} Upgraded Controller Recompiler Units, stacking them");

                    // Use proper clientless stacking approach - stack first item onto second item
                    Client.Send(new CharacterActionMessage
                    {
                        Action = (CharacterActionType)53, // Stacking action type
                        Target = upgradedUnits[1].Slot,   // Target item to stack onto
                        Parameter1 = (int)upgradedUnits[0].Slot.Type,     // Source item slot type
                        Parameter2 = upgradedUnits[0].Slot.Instance       // Source item slot instance
                    });

                    await Task.Delay(100);
                    RecipeUtilities.LogDebug($"[{RecipeName}] Stacked Upgraded Controller Recompiler Units");
                }
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error stacking upgraded units: {ex.Message}");
            }
        }


    }
}
