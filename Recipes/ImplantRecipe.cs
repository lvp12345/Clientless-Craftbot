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
    /// Handles implant processing - combining implants with clusters for enhancement
    /// SIMPLE LOGIC: cluster has "cluster", implant has "implant", match by slot name
    /// </summary>
    public class ImplantRecipe : BaseRecipeProcessor
    {
        // Track processed combinations to prevent infinite loops while allowing multiple clusters
        private static HashSet<string> _processedCombinations = new HashSet<string>();

        public override string RecipeName => "Implant";

        /// <summary>
        /// Implant combinations need more time to complete than other recipes
        /// </summary>
        protected override int GetCombinationDelay()
        {
            return 1000; // 1000ms for implant combinations
        }

        public override bool CanProcess(Item item)
        {
            // SIMPLE: cluster has "cluster", implant has "implant"
            string itemName = item.Name.ToLower();
            bool isImplant = itemName.Contains("implant");
            bool isCluster = itemName.Contains("cluster");

            RecipeUtilities.LogDebug($"[{RecipeName}] CanProcess '{item.Name}': Implant={isImplant}, Cluster={isCluster}");
            return isImplant || isCluster;
        }



        protected override async Task ProcessRecipeLogic(Item item, Container targetContainer)
        {
            RecipeUtilities.LogDebug($"[{RecipeName}] Processing individual item: {item.Name}");

            // CRITICAL FIX: Process ALL possible combinations in current inventory state
            // Instead of processing one item at a time, find all possible cluster+implant pairs
            await ProcessAllPossibleCombinations();

            // Note: The actual processing is now handled by ProcessAllPossibleCombinations
            // This method is called once per iteration and processes all possible combinations
        }

        /// <summary>
        /// CRITICAL FIX: Process ALL possible cluster+implant combinations in current inventory
        /// This ensures multi-step processing works correctly
        /// </summary>
        private async Task ProcessAllPossibleCombinations()
        {
            RecipeUtilities.LogDebug($"[{RecipeName}] Starting comprehensive combination processing");

            // Get current player ID for quality level checking
            string currentPlayerId = GetCurrentPlayerId();
            RecipeUtilities.LogDebug($"[{RecipeName}] Current player ID for quality targeting: '{currentPlayerId}'");

            // Clear processed combinations for this processing session
            _processedCombinations.Clear();
            RecipeUtilities.LogDebug($"[{RecipeName}] Cleared processed combinations for fresh session");

            bool combinationMade = true;
            int maxIterations = 20; // Safety limit
            int iteration = 0;

            // Keep processing until no more combinations are possible
            while (combinationMade && iteration < maxIterations)
            {
                iteration++;
                combinationMade = false;
                RecipeUtilities.LogDebug($"[{RecipeName}] Combination iteration {iteration}");

                // Get all clusters and implants currently in inventory
                var clusters = Inventory.Items.Where(i =>
                    !(i.Slot.Instance >= (int)EquipSlot.Weap_Hud1 && i.Slot.Instance <= (int)EquipSlot.Imp_Feet) &&
                    i.Name.ToLower().Contains("cluster")).ToList();

                var implants = Inventory.Items.Where(i =>
                    !(i.Slot.Instance >= (int)EquipSlot.Weap_Hud1 && i.Slot.Instance <= (int)EquipSlot.Imp_Feet) &&
                    i.Name.ToLower().Contains("implant")).ToList();

                RecipeUtilities.LogDebug($"[{RecipeName}] Found {clusters.Count} clusters and {implants.Count} implants");

                // Try to find matching pairs
                foreach (var cluster in clusters)
                {
                    var clusterSlot = ExtractSlotFromName(cluster.Name);
                    if (string.IsNullOrEmpty(clusterSlot)) continue;

                    // Find matching implant for this cluster
                    var matchingImplant = implants.FirstOrDefault(implant =>
                    {
                        var implantSlot = ExtractSlotFromName(implant.Name);
                        return !string.IsNullOrEmpty(implantSlot) &&
                               (implantSlot.Equals(clusterSlot, StringComparison.OrdinalIgnoreCase) ||
                                implantSlot.Replace("-", " ").Equals(clusterSlot.Replace("-", " "), StringComparison.OrdinalIgnoreCase));
                    });

                    if (matchingImplant != null)
                    {
                        // Check if this combination was already processed
                        string combinationKey = $"{cluster.UniqueIdentity.Instance}+{matchingImplant.UniqueIdentity.Instance}";
                        if (!_processedCombinations.Contains(combinationKey))
                        {
                            RecipeUtilities.LogDebug($"[{RecipeName}] Found matching pair: {cluster.Name} + {matchingImplant.Name}");

                            // Get target quality for this player
                            int? targetQuality = ImplantQualityManager.GetTargetQuality(currentPlayerId, clusterSlot);

                            // Perform the combination
                            await CombineClusterWithImplant(cluster, matchingImplant, currentPlayerId, clusterSlot, targetQuality);
                            combinationMade = true;

                            // Break out of cluster loop to refresh inventory and start over
                            break;
                        }
                    }
                }

                if (!combinationMade)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] No more combinations possible - processing complete");
                }
            }

            if (iteration >= maxIterations)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] ⚠️ Reached maximum iterations ({maxIterations}) - stopping to prevent infinite loop");
            }

            RecipeUtilities.LogDebug($"[{RecipeName}] Comprehensive combination processing completed after {iteration} iterations");
        }





        /// <summary>
        /// Clear processed combinations tracking when starting a new processing session
        /// </summary>
        public static void ClearProcessedCombinations()
        {
            _processedCombinations.Clear();
            RecipeUtilities.LogDebug("[Implant] Cleared processed combinations tracking");
        }

        /// <summary>
        /// Get the current player name for quality level checking
        /// </summary>
        private string GetCurrentPlayerId()
        {
            try
            {
                // Get the current processing player ID
                int? playerId = Modules.PrivateMessageModule.GetCurrentProcessingPlayer();
                if (!playerId.HasValue)
                    return "unknown";

                // Convert player ID to player name by looking up in DynelManager
                var player = DynelManager.Players.FirstOrDefault(p => p.Identity.Instance == playerId.Value);
                string playerName = player?.Name ?? "unknown";

                RecipeUtilities.LogDebug($"[{RecipeName}] Player ID {playerId.Value} -> Player Name '{playerName}'");
                return playerName;
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error getting player name: {ex.Message}");
                return "unknown";
            }
        }

        private async Task ProcessSingleImplantIfNoCluster(Item implant, string playerId)
        {
            RecipeUtilities.LogDebug($"[{RecipeName}] Processing implant: {implant.Name}");

            // Extract slot from implant name (e.g., "Left-Arm", "Leg", etc.)
            string implantSlot = ExtractSlotFromName(implant.Name);
            if (string.IsNullOrEmpty(implantSlot))
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Could not determine slot for implant: {implant.Name}");
                return;
            }
            RecipeUtilities.LogDebug($"[{RecipeName}] Extracted slot '{implantSlot}' from implant: {implant.Name}");

            // Check for quality level targets
            int? targetQuality = ImplantQualityManager.GetTargetQuality(playerId, implantSlot);
            if (targetQuality.HasValue)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Quality target for {implantSlot}: QL{targetQuality.Value} - will process ALL clusters using set quality targeting");
            }

            // Check if there are any matching clusters still available
            var matchingCluster = Inventory.Items.FirstOrDefault(i =>
                !(i.Slot.Instance >= (int)EquipSlot.Weap_Hud1 && i.Slot.Instance <= (int)EquipSlot.Imp_Feet) &&
                i.Name.ToLower().Contains("cluster") &&
                (i.Name.ToLower().Contains(implantSlot.ToLower()) ||
                 i.Name.ToLower().Contains(implantSlot.Replace("-", " ").ToLower())));

            if (matchingCluster != null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found matching cluster {matchingCluster.Name} for implant {implant.Name}");
                await CombineClusterWithImplant(matchingCluster, implant, playerId, implantSlot, targetQuality);
            }
            else
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] No matching cluster found for implant: {implant.Name}");
            }
        }

        private async Task ProcessSingleCluster(Item cluster, string playerId)
        {
            RecipeUtilities.LogDebug($"[{RecipeName}] Processing cluster: {cluster.Name}");

            // Extract slot from cluster name (e.g., "Left-Arm", "Leg", etc.)
            string clusterSlot = ExtractSlotFromName(cluster.Name);
            if (string.IsNullOrEmpty(clusterSlot))
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Could not determine slot for cluster: {cluster.Name}");
                return;
            }
            RecipeUtilities.LogDebug($"[{RecipeName}] Extracted slot '{clusterSlot}' from cluster: {cluster.Name}");

            // Check for quality level targets
            int? targetQuality = ImplantQualityManager.GetTargetQuality(playerId, clusterSlot);
            RecipeUtilities.LogDebug($"[{RecipeName}] Quality lookup: playerId='{playerId}', slot='{clusterSlot}', target={targetQuality?.ToString() ?? "null"}");
            if (targetQuality.HasValue)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Quality target for {clusterSlot}: QL{targetQuality.Value} - will process ALL clusters using set quality targeting");
            }

            // Find a matching implant for this cluster
            var matchingImplant = Inventory.Items.FirstOrDefault(i =>
                !(i.Slot.Instance >= (int)EquipSlot.Weap_Hud1 && i.Slot.Instance <= (int)EquipSlot.Imp_Feet) &&
                i.Name.ToLower().Contains("implant") &&
                (i.Name.ToLower().Contains(clusterSlot.ToLower()) ||
                 i.Name.ToLower().Contains(clusterSlot.Replace("-", " ").ToLower())));

            if (matchingImplant != null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found matching implant {matchingImplant.Name} for cluster {cluster.Name}");
                await CombineClusterWithImplant(cluster, matchingImplant, playerId, clusterSlot, targetQuality);
            }
            else
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] No matching implant found for cluster: {cluster.Name}");
            }
        }



        /// <summary>
        /// Combines a cluster with an implant using the correct AOSharp clientless approach
        /// Based on the malis implant dispenser implementation
        /// </summary>
        private async Task CombineClusterWithImplant(Item cluster, Item implant, string playerId, string slotName, int? targetQuality)
        {
            try
            {
                // Create unique combination key to prevent infinite loops on same items
                string combinationKey = $"{cluster.UniqueIdentity.Instance}+{implant.UniqueIdentity.Instance}";
                if (_processedCombinations.Contains(combinationKey))
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Combination {cluster.Name} + {implant.Name} already processed - skipping to prevent infinite loop");
                    return;
                }

                string qualityInfo = targetQuality.HasValue ? $" (target QL{targetQuality.Value})" : "";
                RecipeUtilities.LogDebug($"[{RecipeName}] Combining cluster {cluster.Name} with implant {implant.Name}{qualityInfo}");

                // Take snapshot of inventory before processing
                var inventoryBefore = Inventory.Items.ToList();

                // EXACT MALIS 3-STEP TRADESKILL LOGIC: Source -> Target -> Execute
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 1: Setting tradeskill source to cluster {cluster.Name} ({cluster.Slot})");
                Client.Send(new CharacterActionMessage
                {
                    Action = CharacterActionType.TradeskillSourceChanged,
                    Target = Identity.None,
                    Parameter1 = (int)cluster.Slot.Type,
                    Parameter2 = cluster.Slot.Instance
                });

                await Task.Delay(100); // Small delay between steps

                RecipeUtilities.LogDebug($"[{RecipeName}] Step 2: Setting tradeskill target to implant {implant.Name} ({implant.Slot})");
                Client.Send(new CharacterActionMessage
                {
                    Action = CharacterActionType.TradeskillTargetChanged,
                    Target = Identity.None,
                    Parameter1 = (int)implant.Slot.Type,
                    Parameter2 = implant.Slot.Instance
                });

                await Task.Delay(100); // Small delay between steps

                RecipeUtilities.LogDebug($"[{RecipeName}] Step 3: Executing tradeskill build");

                // Use target quality if specified, otherwise use implant's current quality
                int buildTargetQuality = targetQuality ?? implant.Ql;
                RecipeUtilities.LogDebug($"[{RecipeName}] Build target quality: QL{buildTargetQuality}");

                Client.Send(new CharacterActionMessage
                {
                    Action = CharacterActionType.TradeskillBuildPressed,
                    Target = new Identity(IdentityType.None, buildTargetQuality), // Use target quality level
                });

                // Wait for the combination to complete
                int delay = GetCombinationDelay();
                RecipeUtilities.LogDebug($"[{RecipeName}] Waiting {delay}ms for implant combination to complete");
                await Task.Delay(delay);

                RecipeUtilities.LogDebug($"[{RecipeName}] Implant combination completed");

                // Mark this combination as processed to prevent repeating the same exact combination
                _processedCombinations.Add(combinationKey);

                // Check for new items created by the combination
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

                        // Enhanced quality progress tracking
                        if (targetQuality.HasValue)
                        {
                            int qualityGain = newItem.Ql - implant.Ql;
                            if (newItem.Ql >= targetQuality.Value)
                            {
                                RecipeUtilities.LogDebug($"[{RecipeName}] ✅ QUALITY TARGET REACHED! {newItem.Name} is QL{newItem.Ql} (target: QL{targetQuality.Value}, gain: +{qualityGain})");
                            }
                            else
                            {
                                int remaining = targetQuality.Value - newItem.Ql;
                                RecipeUtilities.LogDebug($"[{RecipeName}] 📈 Quality progress: {newItem.Name} is QL{newItem.Ql} (target: QL{targetQuality.Value}, gain: +{qualityGain}, remaining: {remaining})");
                            }
                        }
                        else
                        {
                            int qualityGain = newItem.Ql - implant.Ql;
                            RecipeUtilities.LogDebug($"[{RecipeName}] ✅ Enhanced implant: {newItem.Name} (QL{newItem.Ql}, gain: +{qualityGain})");
                        }
                    }
                }

                RecipeUtilities.LogDebug($"[{RecipeName}] Implant combination completed");
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error combining cluster with implant: {ex.Message}");
            }
        }

        private string ExtractSlotFromName(string itemName)
        {
            // Common slot patterns in implant/cluster names
            // Handle both hyphenated and space-separated formats
            string[] slots = {
                "left-arm", "left arm",
                "right-arm", "right arm",
                "left-wrist", "left wrist",
                "right-wrist", "right wrist",
                "left-hand", "left hand",
                "right-hand", "right hand",
                "leg", "head", "eye", "ear", "waist", "chest", "feet", "back", "shoulder"
            };

            string lowerName = itemName.ToLower();
            foreach (string slot in slots)
            {
                if (lowerName.Contains(slot))
                {
                    // Return normalized format (with hyphen) for consistent matching
                    return slot.Replace(" ", "-");
                }
            }

            return null;
        }

        public override RecipeAnalysisResult AnalyzeItems(List<Item> items)
        {
            return AnalyzeItemsShared(items, "Implant Enhancement");
        }
    }
}
