using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AOSharp.Clientless;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using AOSharp.Common.GameData;
using Craftbot.Core;

namespace Craftbot.Recipes
{
    /// <summary>
    /// Handles implant cleaning - using Implant Disassembly Clinic to clean all implants
    /// Bot uses its own tool and returns all cleaned implants to player
    /// Called directly from clean trade processing, not through RecipeManager
    /// </summary>
    public class ImplantCleaningRecipe : BaseRecipeProcessor
    {
        public override string RecipeName => "Implant Cleaning";

        /// <summary>
        /// Implant cleaning needs time to complete
        /// </summary>
        protected override int GetCombinationDelay()
        {
            return 1000; // 1000ms for implant cleaning to complete
        }

        public override bool CanProcess(Item item)
        {
            // Only process items that are implants (not clusters)
            string itemName = item.Name.ToLower();
            bool isImplant = itemName.Contains("implant");
            bool isCluster = itemName.Contains("cluster");

            // Only process implants, not clusters
            if (isImplant && !isCluster)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] CanProcess '{item.Name}': TRUE (is implant)");
                return true;
            }

            return false;
        }

        protected override async Task ProcessRecipeLogic(Item item, Container targetContainer)
        {
            RecipeUtilities.LogDebug($"[{RecipeName}] Processing implant: {item.Name}");

            // Process all implants in inventory
            await ProcessAllImplants();
        }

        /// <summary>
        /// Process all implants in inventory using the Implant Disassembly Clinic
        /// </summary>
        private async Task ProcessAllImplants()
        {
            try
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Starting implant cleaning process");

                // CRITICAL FIX: Try to use player-provided Implant Disassembly Clinic FIRST
                // This prevents the bot from giving away its own tool
                var clinic = Inventory.Items.FirstOrDefault(invItem =>
                    invItem.Name.Contains("Implant Disassembly Clinic") &&
                    !RecipeUtilities.IsBotPersonalTool(invItem));

                // If no player-provided clinic in inventory, try to pull from player bags
                if (clinic == null)
                {
                    if (RecipeUtilities.FindAndPullPlayerProvidedTool("Implant Disassembly Clinic"))
                    {
                        // Wait a moment for tool to move to inventory
                        await Task.Delay(100);
                        clinic = Inventory.Items.FirstOrDefault(invItem =>
                            invItem.Name.Contains("Implant Disassembly Clinic") &&
                            !RecipeUtilities.IsBotPersonalTool(invItem));

                        if (clinic != null)
                        {
                            RecipeUtilities.LogDebug($"[{RecipeName}] Pulled player-provided Implant Disassembly Clinic from bag: {clinic.Name}");
                        }
                    }
                }

                // If still no player-provided clinic, use bot's tool as fallback
                if (clinic == null)
                {
                    clinic = FindTool("Implant Disassembly Clinic");
                }

                if (clinic == null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] ❌ Implant Disassembly Clinic not found!");
                    return;
                }

                RecipeUtilities.LogDebug($"[{RecipeName}] Found Implant Disassembly Clinic: {clinic.Name}");

                // Get all implants in inventory (not equipped)
                var implants = Inventory.Items.Where(i =>
                    !(i.Slot.Instance >= (int)EquipSlot.Weap_Hud1 && i.Slot.Instance <= (int)EquipSlot.Imp_Feet) &&
                    i.Name.ToLower().Contains("implant") &&
                    !i.Name.ToLower().Contains("cluster")).ToList();

                RecipeUtilities.LogDebug($"[{RecipeName}] Found {implants.Count} implants to clean");

                if (implants.Count == 0)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] No implants to clean");
                    return;
                }

                // Process each implant
                foreach (var implant in implants)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Cleaning implant: {implant.Name}");

                    // Take snapshot before processing
                    var inventoryBefore = Inventory.Items.ToList();

                    // TRADESKILL LOGIC: Source (Clinic) -> Target (Implant) -> Execute
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step 1: Setting tradeskill source to {clinic.Name}");
                    Client.Send(new CharacterActionMessage
                    {
                        Action = CharacterActionType.TradeskillSourceChanged,
                        Target = Identity.None,
                        Parameter1 = (int)clinic.Slot.Type,
                        Parameter2 = clinic.Slot.Instance
                    });

                    await Task.Delay(100);

                    RecipeUtilities.LogDebug($"[{RecipeName}] Step 2: Setting tradeskill target to {implant.Name}");
                    Client.Send(new CharacterActionMessage
                    {
                        Action = CharacterActionType.TradeskillTargetChanged,
                        Target = Identity.None,
                        Parameter1 = (int)implant.Slot.Type,
                        Parameter2 = implant.Slot.Instance
                    });

                    await Task.Delay(100);

                    RecipeUtilities.LogDebug($"[{RecipeName}] Step 3: Executing tradeskill build");
                    Client.Send(new CharacterActionMessage
                    {
                        Action = CharacterActionType.TradeskillBuildPressed,
                        Target = Identity.None,
                    });

                    // Wait for cleaning to complete
                    int delay = GetCombinationDelay();
                    RecipeUtilities.LogDebug($"[{RecipeName}] Waiting {delay}ms for implant cleaning to complete");
                    await Task.Delay(delay);

                    RecipeUtilities.LogDebug($"[{RecipeName}] Implant cleaning completed");

                    // Check for cleaned implant (result)
                    var inventoryAfter = Inventory.Items.ToList();
                    var newItems = inventoryAfter.Where(afterItem =>
                        !inventoryBefore.Any(beforeItem =>
                            beforeItem.UniqueIdentity == afterItem.UniqueIdentity)).ToList();

                    foreach (var newItem in newItems)
                    {
                        if (newItem.Slot.Type == IdentityType.Inventory &&
                            !RecipeUtilities.IsProcessingTool(newItem) &&
                            newItem.UniqueIdentity.Type != IdentityType.Container)
                        {
                            Core.ItemTracker.TrackRecipeResult(newItem, RecipeName);
                            RecipeUtilities.LogDebug($"[{RecipeName}] ✅ Cleaned implant: {newItem.Name}");
                        }
                    }

                    // Small delay between implants
                    await Task.Delay(200);
                }

                RecipeUtilities.LogDebug($"[{RecipeName}] All implants cleaned successfully");
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error processing implants: {ex.Message}");
            }
        }

        public override RecipeAnalysisResult AnalyzeItems(List<Item> items)
        {
            return AnalyzeItemsShared(items, "Implant Cleaning");
        }
    }
}

