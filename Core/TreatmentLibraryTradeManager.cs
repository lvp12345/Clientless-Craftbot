using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AOSharp.Clientless;
using AOSharp.Common.GameData;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

namespace Craftbot.Core
{
    /// <summary>
    /// Manages special treatment library trades that use custom processing logic
    /// Integrates with unified trade logic for item return and state management
    /// Handles hacker tool usage and final combination logic
    /// </summary>
    public static class TreatmentLibraryTradeManager
    {
        // Track players who have pending treatment library trades with their target quality and variation type
        private static Dictionary<string, (int targetQuality, bool isDoctorVariation)> _pendingTreatmentLibraryTrades = new Dictionary<string, (int, bool)>();

        /// <summary>
        /// Mark a player as having a pending treatment library trade with target quality and variation type
        /// </summary>
        public static void SetPendingTreatmentLibraryTrade(string playerName, int targetQuality, bool isDoctorVariation = false)
        {
            _pendingTreatmentLibraryTrades[playerName] = (targetQuality, isDoctorVariation);
            string variationType = isDoctorVariation ? "doctor variation" : "regular";
            Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Marked {playerName} for custom treatment library processing ({variationType}) with target QL{targetQuality}");
        }

        /// <summary>
        /// Check if a player has a pending treatment library trade
        /// </summary>
        public static bool HasPendingTreatmentLibraryTrade(string playerName)
        {
            return _pendingTreatmentLibraryTrades.ContainsKey(playerName);
        }

        /// <summary>
        /// Clear pending treatment library trade for a player
        /// </summary>
        public static void ClearPendingTreatmentLibraryTrade(string playerName)
        {
            _pendingTreatmentLibraryTrades.Remove(playerName);
            Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Cleared pending treatment library trade for {playerName}");
        }

        /// <summary>
        /// Get target quality for a player's treatment library trade
        /// </summary>
        public static int? GetTargetQuality(string playerName)
        {
            if (_pendingTreatmentLibraryTrades.TryGetValue(playerName, out var tradeInfo))
            {
                return tradeInfo.targetQuality;
            }
            return null;
        }

        /// <summary>
        /// Check if a player's treatment library trade is the doctor variation
        /// </summary>
        public static bool IsDoctorVariation(string playerName)
        {
            return _pendingTreatmentLibraryTrades.TryGetValue(playerName, out var tradeInfo) && tradeInfo.isDoctorVariation;
        }

        /// <summary>
        /// Process treatment library trade with custom hacker tool logic
        /// Integrates with unified trade logic - this handles only the treatment library-specific processing
        /// The unified system handles item return, state management, and cleanup
        /// </summary>
        public static async Task ProcessTreatmentLibraryTrade(string playerName, List<Item> receivedItems)
        {
            try
            {
                // Use the same logging system as the rest of the bot
                Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Starting custom treatment library processing for {playerName}");
                Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Received {receivedItems.Count} items from {playerName}");

                // Get target quality for this player
                int? targetQuality = GetTargetQuality(playerName);
                if (!targetQuality.HasValue)
                {
                    Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] No target quality found for {playerName} - returning items");
                    ClearPendingTreatmentLibraryTrade(playerName);
                    return;
                }

                bool isDoctorVariation = IsDoctorVariation(playerName);
                string variationType = isDoctorVariation ? "doctor variation" : "regular";
                Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Target quality: QL{targetQuality.Value}, Variation: {variationType}");

                // STEP 1: Move all treatment library-related items to inventory (following unified recipe workflow)
                // Accepts both regular and advanced versions (e.g., "Advanced Portable Surgery Clinic")
                Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Step 1: Moving items to inventory for processing");
                var treatmentItems = receivedItems.Where(item =>
                    item.Name.ToLower().Contains("portable surgery clinic") ||
                    item.Name.ToLower().Contains("pharma tech tutoring device")).ToList();

                if (!treatmentItems.Any())
                {
                    Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] No treatment library materials found - returning items");
                    ClearPendingTreatmentLibraryTrade(playerName);
                    return;
                }

                Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Moving {treatmentItems.Count} treatment library-related items to inventory");
                foreach (var item in treatmentItems)
                {
                    string itemType = item.Name.ToLower().Contains("portable surgery clinic") ? "Surgery Clinic" : "Pharma Device";
                    Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Moving to inventory: {item.Name} (detected as: {itemType})");
                    item.MoveToInventory();
                    await Task.Delay(50); // Small delay between moves
                }

                await Task.Delay(200); // Wait for items to be moved

                // STEP 2: Process items now that they're in inventory
                Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Step 2: Processing items in inventory");
                await ProcessTreatmentLibraryWithHackerTool(playerName, targetQuality.Value, isDoctorVariation);

                Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Completed custom treatment library processing for {playerName}");

                // Clear the pending flag now that processing is complete
                ClearPendingTreatmentLibraryTrade(playerName);
            }
            catch (Exception ex)
            {
                Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Error processing treatment library trade for {playerName}: {ex.Message}");
                Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Stack trace: {ex.StackTrace}");
                // Clear the flag even on error to prevent it from affecting future trades
                ClearPendingTreatmentLibraryTrade(playerName);
            }
        }

        /// <summary>
        /// Process treatment library items with hacker tool - custom logic
        /// </summary>
        private static async Task ProcessTreatmentLibraryWithHackerTool(string playerName, int targetQuality, bool isDoctorVariation)
        {
            Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Starting hacker tool processing with target QL{targetQuality}");

            try
            {
                // Add extra delay to ensure items have moved to inventory
                await Task.Delay(500);

                // Debug: Log all items currently in inventory
                Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Current inventory items ({Inventory.Items.Count()}):");
                foreach (var item in Inventory.Items)
                {
                    Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] - {item.Name}");
                }

                // Find the required items in inventory (accepts both regular and advanced versions)
                // e.g., "Portable Surgery Clinic" or "Advanced Portable Surgery Clinic"
                var surgeryClinic = Inventory.Items.FirstOrDefault(item =>
                    item.Name.ToLower().Contains("portable surgery clinic"));
                var pharmaDevice = Inventory.Items.FirstOrDefault(item =>
                    item.Name.ToLower().Contains("pharma tech tutoring device"));

                if (surgeryClinic == null || pharmaDevice == null)
                {
                    Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Missing required items - Surgery Clinic: {surgeryClinic != null}, Pharma Device: {pharmaDevice != null}");

                    // Debug: Try different search patterns
                    var allSurgeryItems = Inventory.Items.Where(item => item.Name.ToLower().Contains("surgery")).ToList();
                    var allPharmaItems = Inventory.Items.Where(item => item.Name.ToLower().Contains("pharma")).ToList();

                    Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Items containing 'surgery': {allSurgeryItems.Count}");
                    foreach (var item in allSurgeryItems)
                    {
                        Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] - Surgery item: {item.Name}");
                    }

                    Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Items containing 'pharma': {allPharmaItems.Count}");
                    foreach (var item in allPharmaItems)
                    {
                        Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] - Pharma item: {item.Name}");
                    }

                    return;
                }

                Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Found Surgery Clinic: {surgeryClinic.Name} (QL{surgeryClinic.Ql})");
                Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Found Pharma Device: {pharmaDevice.Name} (QL{pharmaDevice.Ql})");

                // Find bot's hacker tool
                var hackerTool = await FindBotHackerTool();
                if (hackerTool == null)
                {
                    Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] No hacker tool found - cannot process");
                    return;
                }

                Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Found hacker tool: {hackerTool.Name}");

                // Hack both items to target quality
                await HackItemToQuality(hackerTool, surgeryClinic, targetQuality);
                await Task.Delay(1000); // Wait between hacking operations

                await HackItemToQuality(hackerTool, pharmaDevice, targetQuality);
                await Task.Delay(1000); // Wait between hacking operations

                // Find the hacked items
                var hackedSurgeryClinic = Inventory.Items.FirstOrDefault(item => 
                    item.Name.ToLower().Contains("hacked") && item.Name.ToLower().Contains("portable surgery clinic"));
                var hackedPharmaDevice = Inventory.Items.FirstOrDefault(item => 
                    item.Name.ToLower().Contains("hacked") && item.Name.ToLower().Contains("pharma tech tutoring device"));

                if (hackedSurgeryClinic == null || hackedPharmaDevice == null)
                {
                    Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Missing hacked items - cannot combine");
                    return;
                }

                // Combine the hacked items to create Treatment Library (regular or doctor variation)
                await CombineHackedItems(hackedPharmaDevice, hackedSurgeryClinic, targetQuality, isDoctorVariation);

                Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Completed hacker tool processing");
            }
            catch (Exception ex)
            {
                Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Error in hacker tool processing: {ex.Message}");
            }
        }

        /// <summary>
        /// Find bot's hacker tool (Advanced Hacker Tool or regular Hacker Tool)
        /// CRITICAL: Only use bot's personal hacker tool, NEVER use player-provided hacker tools
        /// </summary>
        private static async Task<Item> FindBotHackerTool()
        {
            // CRITICAL FIX: First check inventory for BOT'S hacker tool only
            var hackerTools = Inventory.Items.Where(item =>
                item.Name.ToLower().Contains("hacker tool")).ToList();

            foreach (var tool in hackerTools)
            {
                // Check if this is the bot's personal tool
                if (Core.ItemTracker.IsBotTool(tool) || Core.ItemTracker.IsBotPersonalItem(tool))
                {
                    Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Found BOT'S hacker tool in inventory: {tool.Name}");
                    return tool;
                }
                else
                {
                    Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Found PLAYER'S hacker tool in inventory: {tool.Name} - skipping (will be returned to player)");
                }
            }

            // Check containers for BOT'S hacker tool
            var containers = Inventory.Containers.Where(container => container != null && container.IsOpen).ToList();
            foreach (var container in containers)
            {
                if (container.Items == null) continue;

                var containerHackerTools = container.Items.Where(item =>
                    item.Name.ToLower().Contains("hacker tool")).ToList();

                foreach (var tool in containerHackerTools)
                {
                    // Check if this is the bot's personal tool
                    if (Core.ItemTracker.IsBotTool(tool) || Core.ItemTracker.IsBotPersonalItem(tool))
                    {
                        Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Found BOT'S hacker tool in container: {tool.Name}");
                        // Move to inventory for use
                        tool.MoveToInventory();
                        await Task.Delay(200);
                        return tool;
                    }
                    else
                    {
                        Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Found PLAYER'S hacker tool in container: {tool.Name} - skipping (will be returned to player)");
                    }
                }
            }

            Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] No BOT'S hacker tool found in inventory or bags");
            return null;
        }

        /// <summary>
        /// Use hacker tool on an item to set it to target quality
        /// </summary>
        private static async Task HackItemToQuality(Item hackerTool, Item targetItem, int targetQuality)
        {
            try
            {
                Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Hacking {targetItem.Name} (QL{targetItem.Ql}) to QL{targetQuality}");

                // EXACT TRADESKILL LOGIC: Source -> Target -> Execute
                Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Step 1: Setting tradeskill source to hacker tool {hackerTool.Name}");
                Client.Send(new CharacterActionMessage
                {
                    Action = CharacterActionType.TradeskillSourceChanged,
                    Target = Identity.None,
                    Parameter1 = (int)hackerTool.Slot.Type,
                    Parameter2 = hackerTool.Slot.Instance
                });

                await Task.Delay(100);

                Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Step 2: Setting tradeskill target to {targetItem.Name}");
                Client.Send(new CharacterActionMessage
                {
                    Action = CharacterActionType.TradeskillTargetChanged,
                    Target = Identity.None,
                    Parameter1 = (int)targetItem.Slot.Type,
                    Parameter2 = targetItem.Slot.Instance
                });

                await Task.Delay(100);

                Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Step 3: Executing tradeskill build with target QL{targetQuality}");
                Client.Send(new CharacterActionMessage
                {
                    Action = CharacterActionType.TradeskillBuildPressed,
                    Target = new Identity(IdentityType.None, targetQuality),
                });

                await Task.Delay(500); // Wait for hacking to complete

                // Check the result quality after hacking
                var hackedItem = Inventory.Items.FirstOrDefault(item =>
                    item.Name.ToLower().Contains("hacked") &&
                    item.Name.ToLower().Contains(targetItem.Name.ToLower().Replace("advanced ", "")));

                if (hackedItem != null)
                {
                    Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Hacking result: {hackedItem.Name} (QL{hackedItem.Ql}) - Target was QL{targetQuality}");
                }
                else
                {
                    Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Could not find hacked result for {targetItem.Name}");
                }
            }
            catch (Exception ex)
            {
                Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Error hacking item: {ex.Message}");
            }
        }

        /// <summary>
        /// Combine hacked items to create Treatment Library (regular or doctor variation)
        /// </summary>
        private static async Task CombineHackedItems(Item hackedPharmaDevice, Item hackedSurgeryClinic, int targetQuality, bool isDoctorVariation)
        {
            try
            {
                string resultName = isDoctorVariation ? "Treatment and Pharmacy Library" : "Treatment Library";
                string sourceItem, targetItem;

                if (isDoctorVariation)
                {
                    // Doctor variation: Surgery Clinic + Pharma Device = Treatment and Pharmacy Library
                    sourceItem = hackedSurgeryClinic.Name;
                    targetItem = hackedPharmaDevice.Name;
                    Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Combining {sourceItem} + {targetItem} to create {resultName} (doctor variation)");

                    // EXACT TRADESKILL LOGIC: Source -> Target -> Execute
                    Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Step 1: Setting tradeskill source to {sourceItem}");
                    Client.Send(new CharacterActionMessage
                    {
                        Action = CharacterActionType.TradeskillSourceChanged,
                        Target = Identity.None,
                        Parameter1 = (int)hackedSurgeryClinic.Slot.Type,
                        Parameter2 = hackedSurgeryClinic.Slot.Instance
                    });

                    await Task.Delay(100);

                    Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Step 2: Setting tradeskill target to {targetItem}");
                    Client.Send(new CharacterActionMessage
                    {
                        Action = CharacterActionType.TradeskillTargetChanged,
                        Target = Identity.None,
                        Parameter1 = (int)hackedPharmaDevice.Slot.Type,
                        Parameter2 = hackedPharmaDevice.Slot.Instance
                    });
                }
                else
                {
                    // Regular variation: Pharma Device + Surgery Clinic = Treatment Library
                    sourceItem = hackedPharmaDevice.Name;
                    targetItem = hackedSurgeryClinic.Name;
                    Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Combining {sourceItem} + {targetItem} to create {resultName} (regular)");

                    // EXACT TRADESKILL LOGIC: Source -> Target -> Execute
                    Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Step 1: Setting tradeskill source to {sourceItem}");
                    Client.Send(new CharacterActionMessage
                    {
                        Action = CharacterActionType.TradeskillSourceChanged,
                        Target = Identity.None,
                        Parameter1 = (int)hackedPharmaDevice.Slot.Type,
                        Parameter2 = hackedPharmaDevice.Slot.Instance
                    });

                    await Task.Delay(100);

                    Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Step 2: Setting tradeskill target to {targetItem}");
                    Client.Send(new CharacterActionMessage
                    {
                        Action = CharacterActionType.TradeskillTargetChanged,
                        Target = Identity.None,
                        Parameter1 = (int)hackedSurgeryClinic.Slot.Type,
                        Parameter2 = hackedSurgeryClinic.Slot.Instance
                    });
                }

                await Task.Delay(100);

                Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Step 3: Executing tradeskill build with target QL{targetQuality}");
                Client.Send(new CharacterActionMessage
                {
                    Action = CharacterActionType.TradeskillBuildPressed,
                    Target = new Identity(IdentityType.None, targetQuality),
                });

                await Task.Delay(1000); // Wait for combination to complete

                // Check the final result quality
                var finalResult = Inventory.Items.FirstOrDefault(item =>
                    item.Name.ToLower().Contains("treatment") &&
                    item.Name.ToLower().Contains("library"));

                if (finalResult != null)
                {
                    Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Final result: {finalResult.Name} (QL{finalResult.Ql}) - Target was QL{targetQuality}");

                    if (finalResult.Ql != targetQuality)
                    {
                        Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] ⚠️ Quality mismatch! Expected QL{targetQuality}, got QL{finalResult.Ql}");
                    }
                    else
                    {
                        Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] ✅ Quality target achieved: QL{finalResult.Ql}");
                    }
                }
                else
                {
                    Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Could not find final {resultName} result");
                }
            }
            catch (Exception ex)
            {
                Modules.PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Error combining hacked items: {ex.Message}");
            }
        }
    }
}
