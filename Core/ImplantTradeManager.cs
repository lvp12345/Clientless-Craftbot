using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AOSharp.Clientless;
using AOSharp.Clientless.Logging;
using AOSharp.Common.GameData;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using Craftbot.Recipes;

namespace Craftbot.Core
{
    /// <summary>
    /// Manages special implant trades that use custom processing logic
    /// Integrates with unified trade logic for item return and state management
    /// Handles custom quality targeting logic for implant combinations
    /// </summary>
    public static class ImplantTradeManager
    {
        // Track players who have pending implant trades
        private static HashSet<string> _pendingImplantTrades = new HashSet<string>();
        
        // Track processed combinations to prevent infinite loops
        private static HashSet<string> _processedCombinations = new HashSet<string>();

        /// <summary>
        /// Mark a player as having a pending implant trade
        /// </summary>
        public static void SetPendingImplantTrade(string playerName)
        {
            _pendingImplantTrades.Add(playerName);
            Modules.PrivateMessageModule.LogDebug($"[IMPLANT TRADE] Marked {playerName} for custom implant processing");
        }

        /// <summary>
        /// Check if a player has a pending implant trade
        /// </summary>
        public static bool HasPendingImplantTrade(string playerName)
        {
            return _pendingImplantTrades.Contains(playerName);
        }

        /// <summary>
        /// Remove a player from pending implant trades
        /// </summary>
        public static void ClearPendingImplantTrade(string playerName)
        {
            _pendingImplantTrades.Remove(playerName);
            Modules.PrivateMessageModule.LogDebug($"[IMPLANT TRADE] Cleared pending implant trade for {playerName}");
        }

        /// <summary>
        /// Process implant trade with custom quality targeting logic
        /// Integrates with unified trade logic - this handles only the implant-specific processing
        /// The unified system handles item return, state management, and cleanup
        /// </summary>
        public static async Task ProcessImplantTrade(string playerName, List<Item> receivedItems)
        {
            try
            {
                // Use the same logging system as the rest of the bot
                Modules.PrivateMessageModule.LogDebug($"[IMPLANT TRADE] Starting custom implant processing for {playerName}");
                Modules.PrivateMessageModule.LogDebug($"[IMPLANT TRADE] Received {receivedItems.Count} items from {playerName}");

                // Note: For quality targeting, we don't track processed combinations
                // since we want to combine ALL clusters into implants to reach target quality

                // STEP 1: Move all implant-related items to inventory (following unified recipe workflow)
                Modules.PrivateMessageModule.LogDebug($"[IMPLANT TRADE] Step 1: Moving items to inventory for processing");
                var implantItems = receivedItems.Where(item =>
                    item.Name.ToLower().Contains("cluster") || item.Name.ToLower().Contains("implant")).ToList();

                if (!implantItems.Any())
                {
                    Modules.PrivateMessageModule.LogDebug($"[IMPLANT TRADE] No implant materials found - returning items");
                    return;
                }

                Modules.PrivateMessageModule.LogDebug($"[IMPLANT TRADE] Moving {implantItems.Count} implant-related items to inventory");
                foreach (var item in implantItems)
                {
                    Modules.PrivateMessageModule.LogDebug($"[IMPLANT TRADE] Moving to inventory: {item.Name}");
                    item.MoveToInventory();
                    await Task.Delay(50); // Small delay between moves
                }

                await Task.Delay(200); // Wait for items to be moved

                // STEP 2: Process items now that they're in inventory
                Modules.PrivateMessageModule.LogDebug($"[IMPLANT TRADE] Step 2: Processing items in inventory");
                await ProcessImplantsWithQualityTargeting(playerName, new List<Item>(), new List<Item>());

                Modules.PrivateMessageModule.LogDebug($"[IMPLANT TRADE] Completed custom implant processing for {playerName}");
            }
            catch (Exception ex)
            {
                Modules.PrivateMessageModule.LogDebug($"[IMPLANT TRADE] Error processing implant trade for {playerName}: {ex.Message}");
                Modules.PrivateMessageModule.LogDebug($"[IMPLANT TRADE] Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Process all clusters with quality targeting - custom logic
        /// </summary>
        private static async Task ProcessImplantsWithQualityTargeting(string playerName, List<Item> clusters, List<Item> implants)
        {
            Modules.PrivateMessageModule.LogDebug($"[IMPLANT TRADE] Starting quality targeting processing");

            bool processingOccurred = true;
            int maxIterations = 20;
            int iteration = 0;

            while (processingOccurred && iteration < maxIterations)
            {
                iteration++;
                processingOccurred = false;
                Modules.PrivateMessageModule.LogDebug($"[IMPLANT TRADE] Quality targeting iteration {iteration}");

                // Get current clusters and implants in inventory
                // CRITICAL SAFETY: Keep EquipSlot filter to prevent bot from giving away its own equipped implants!
                // DEBUG: Log slot instances to understand why SlotInstance 66 is being filtered
                var allInventoryItems = Inventory.Items.Where(i => i.Slot.Type == IdentityType.Inventory).ToList();
                Modules.PrivateMessageModule.LogDebug($"[IMPLANT TRADE DEBUG] Total inventory items: {allInventoryItems.Count}");

                foreach (var item in allInventoryItems.Where(i => i.Name.ToLower().Contains("implant") || i.Name.ToLower().Contains("cluster")))
                {
                    bool isFiltered = (item.Slot.Instance >= (int)EquipSlot.Weap_Hud1 && item.Slot.Instance <= (int)EquipSlot.Imp_Feet);
                    Modules.PrivateMessageModule.LogDebug($"[IMPLANT TRADE DEBUG] Item: {item.Name}, SlotInstance: {item.Slot.Instance}, Filtered: {isFiltered}");
                }

                // FIXED: Use exact same filtering logic as working ImplantRecipe.cs (no Slot.Type check)
                var currentClusters = Inventory.Items.Where(i =>
                    !(i.Slot.Instance >= (int)EquipSlot.Weap_Hud1 && i.Slot.Instance <= (int)EquipSlot.Imp_Feet) &&
                    i.Name.ToLower().Contains("cluster")).ToList();

                var currentImplants = Inventory.Items.Where(i =>
                    !(i.Slot.Instance >= (int)EquipSlot.Weap_Hud1 && i.Slot.Instance <= (int)EquipSlot.Imp_Feet) &&
                    i.Name.ToLower().Contains("implant")).ToList();

                Modules.PrivateMessageModule.LogDebug($"[IMPLANT TRADE] Found {currentClusters.Count} clusters and {currentImplants.Count} implants in inventory");

                // Process each cluster with matching implants
                foreach (var cluster in currentClusters)
                {
                    string clusterSlot = ExtractSlotFromName(cluster.Name);
                    Modules.PrivateMessageModule.LogDebug($"[IMPLANT TRADE] Processing cluster: {cluster.Name} (slot: {clusterSlot})");
                    if (string.IsNullOrEmpty(clusterSlot)) continue;

                    // Get quality target for this slot
                    int? targetQuality = ImplantQualityManager.GetTargetQuality(playerName, clusterSlot);
                    if (targetQuality.HasValue)
                    {
                        Modules.PrivateMessageModule.LogDebug($"[IMPLANT TRADE] Quality target for {clusterSlot}: QL{targetQuality.Value}");
                    }

                    // Find matching implant for this cluster
                    var matchingImplant = currentImplants.FirstOrDefault(implant =>
                    {
                        string implantSlot = ExtractSlotFromName(implant.Name);
                        return !string.IsNullOrEmpty(implantSlot) &&
                               implantSlot.Equals(clusterSlot, StringComparison.OrdinalIgnoreCase);
                    });

                    if (matchingImplant != null)
                    {
                        // For quality targeting, always process clusters - don't track combinations
                        // since we want to combine ALL clusters into the implant to reach target quality
                        Modules.PrivateMessageModule.LogDebug($"[IMPLANT TRADE] Processing cluster {cluster.Name} with implant {matchingImplant.Name}");
                        await CombineClusterWithImplant(cluster, matchingImplant, clusterSlot, targetQuality);
                        processingOccurred = true;
                        await Task.Delay(600); // Longer delay for implant combinations
                    }
                    else
                    {
                        Modules.PrivateMessageModule.LogDebug($"[IMPLANT TRADE] No matching implant found for cluster {cluster.Name} (slot: {clusterSlot})");
                    }
                }

                if (!processingOccurred)
                {
                    Modules.PrivateMessageModule.LogDebug($"[IMPLANT TRADE] No more combinations possible - quality targeting complete");
                    break;
                }
            }

            if (iteration >= maxIterations)
            {
                Modules.PrivateMessageModule.LogDebug($"[IMPLANT TRADE] ⚠️ Reached maximum iterations ({maxIterations}) for quality targeting");
            }

            Modules.PrivateMessageModule.LogDebug($"[IMPLANT TRADE] ✅ Completed custom implant processing after {iteration} iterations");
        }

        /// <summary>
        /// Extract slot name from item name (e.g., "Left-Arm", "Brain", etc.)
        /// </summary>
        private static string ExtractSlotFromName(string itemName)
        {
            if (string.IsNullOrEmpty(itemName))
                return "";

            string lowerName = itemName.ToLower();

            // Map item name patterns to slot names
            if (lowerName.Contains("left-arm") || lowerName.Contains("left arm"))
                return "left-arm";
            if (lowerName.Contains("right-arm") || lowerName.Contains("right arm"))
                return "right-arm";
            if (lowerName.Contains("brain") || lowerName.Contains("head"))
                return "brain";
            if (lowerName.Contains("chest"))
                return "chest";
            if (lowerName.Contains("left-wrist") || lowerName.Contains("left wrist"))
                return "left-wrist";
            if (lowerName.Contains("right-wrist") || lowerName.Contains("right wrist"))
                return "right-wrist";
            if (lowerName.Contains("left-hand") || lowerName.Contains("left hand"))
                return "left-hand";
            if (lowerName.Contains("right-hand") || lowerName.Contains("right hand"))
                return "right-hand";
            if (lowerName.Contains("eyes") || lowerName.Contains("eye"))
                return "eye";
            if (lowerName.Contains("ears") || lowerName.Contains("ear"))
                return "ear";
            if (lowerName.Contains("waist"))
                return "waist";
            if (lowerName.Contains("legs") || lowerName.Contains("leg"))
                return "legs";
            if (lowerName.Contains("feet") || lowerName.Contains("foot"))
                return "feet";

            return "";
        }

        /// <summary>
        /// Combine cluster with implant using quality targeting
        /// </summary>
        private static async Task CombineClusterWithImplant(Item cluster, Item implant, string slotName, int? targetQuality)
        {
            try
            {
                string qualityInfo = targetQuality.HasValue ? $" (target QL{targetQuality.Value})" : "";
                Modules.PrivateMessageModule.LogDebug($"[IMPLANT TRADE] Combining cluster {cluster.Name} with implant {implant.Name}{qualityInfo}");

                // Take snapshot of inventory before processing
                var inventoryBefore = Inventory.Items.ToList();

                // EXACT MALIS 3-STEP TRADESKILL LOGIC: Source -> Target -> Execute (copied from working ImplantRecipe.cs)
                Modules.PrivateMessageModule.LogDebug($"[IMPLANT TRADE] Step 1: Setting tradeskill source to cluster {cluster.Name} ({cluster.Slot})");
                Client.Send(new CharacterActionMessage
                {
                    Action = CharacterActionType.TradeskillSourceChanged,
                    Target = Identity.None,
                    Parameter1 = (int)cluster.Slot.Type,
                    Parameter2 = cluster.Slot.Instance
                });

                await Task.Delay(100); // Small delay between steps

                Modules.PrivateMessageModule.LogDebug($"[IMPLANT TRADE] Step 2: Setting tradeskill target to implant {implant.Name} ({implant.Slot})");
                Client.Send(new CharacterActionMessage
                {
                    Action = CharacterActionType.TradeskillTargetChanged,
                    Target = Identity.None,
                    Parameter1 = (int)implant.Slot.Type,
                    Parameter2 = implant.Slot.Instance
                });

                await Task.Delay(100); // Small delay between steps

                Modules.PrivateMessageModule.LogDebug($"[IMPLANT TRADE] Step 3: Executing tradeskill build");

                // Use target quality if specified, otherwise use implant's current quality
                int buildTargetQuality = targetQuality ?? implant.Ql;
                Modules.PrivateMessageModule.LogDebug($"[IMPLANT TRADE] Build target quality: QL{buildTargetQuality}");

                Client.Send(new CharacterActionMessage
                {
                    Action = CharacterActionType.TradeskillBuildPressed,
                    Target = new Identity(IdentityType.None, buildTargetQuality), // Use target quality level
                });

                // Wait for the combination to complete (using same delay as working ImplantRecipe.cs)
                int delay = 500; // 500ms for implant combinations (same as ImplantRecipe.cs)
                Modules.PrivateMessageModule.LogDebug($"[IMPLANT TRADE] Waiting {delay}ms for implant combination to complete");
                await Task.Delay(delay);

                Modules.PrivateMessageModule.LogDebug($"[IMPLANT TRADE] Implant combination completed");

                // Track new items created by the combination
                var inventoryAfter = Inventory.Items.ToList();
                var newItems = inventoryAfter.Where(afterItem =>
                    !inventoryBefore.Any(beforeItem =>
                        beforeItem.UniqueIdentity == afterItem.UniqueIdentity)).ToList();

                foreach (var newItem in newItems)
                {
                    if (newItem.Slot.Type == IdentityType.Inventory &&
                        newItem.UniqueIdentity.Type != IdentityType.Container)
                    {
                        // Enhanced quality progress tracking
                        if (targetQuality.HasValue)
                        {
                            int qualityGain = newItem.Ql - implant.Ql;
                            if (newItem.Ql >= targetQuality.Value)
                            {
                                Modules.PrivateMessageModule.LogDebug($"[IMPLANT TRADE] ✅ QUALITY TARGET REACHED! {newItem.Name} is QL{newItem.Ql} (target: QL{targetQuality.Value}, gain: +{qualityGain})");
                            }
                            else
                            {
                                int remaining = targetQuality.Value - newItem.Ql;
                                Modules.PrivateMessageModule.LogDebug($"[IMPLANT TRADE] 📈 Quality progress: {newItem.Name} is QL{newItem.Ql} (target: QL{targetQuality.Value}, gain: +{qualityGain}, remaining: {remaining})");
                            }
                        }
                        else
                        {
                            int qualityGain = newItem.Ql - implant.Ql;
                            Modules.PrivateMessageModule.LogDebug($"[IMPLANT TRADE] ✅ Enhanced implant: {newItem.Name} (QL{newItem.Ql}, gain: +{qualityGain})");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Modules.PrivateMessageModule.LogDebug($"[IMPLANT TRADE] Error combining cluster with implant: {ex.Message}");
            }
        }
    }
}
