using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AOSharp.Clientless;
using SmokeLounge.AOtomation.Messaging.GameData;
using AOSharp.Common.GameData;

namespace Craftbot.Recipes
{
    /// <summary>
    /// Handles Carbonum Armor processing - a command-based recipe system for creating armor pieces
    /// </summary>
    public class CarbArmorRecipe : BaseRecipeProcessor
    {
        public override string RecipeName => "Carb Armor";

        // Slot name mappings for command parsing
        private static readonly Dictionary<string, string> SlotNameMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "head", "HSR - Sketch and Etch - Helmet" },
            { "helmet", "HSR - Sketch and Etch - Helmet" },
            { "chest", "HSR - Sketch and Etch - Chestpiece" },
            { "breast", "HSR - Sketch and Etch - Chestpiece" },
            { "chestpiece", "HSR - Sketch and Etch - Chestpiece" },
            { "pants", "HSR - Sketch and Etch - Legs" },
            { "legs", "HSR - Sketch and Etch - Legs" },
            { "sleeve", "HSR - Sketch and Etch - Arms" },
            { "sleeves", "HSR - Sketch and Etch - Arms" },
            { "arms", "HSR - Sketch and Etch - Arms" },
            { "feet", "HSR - Sketch and Etch - Boots" },
            { "boots", "HSR - Sketch and Etch - Boots" },
            { "gloves", "HSR - Sketch and Etch - Gloves" },
            { "hands", "HSR - Sketch and Etch - Gloves" }
        };

        // Expected output names for each sketching tool
        private static readonly Dictionary<string, string> SketchingToolOutputs = new Dictionary<string, string>
        {
            { "HSR - Sketch and Etch - Helmet", "Etched Pattern for Carbonum Helmet" },
            { "HSR - Sketch and Etch - Chestpiece", "Etched Pattern for Carbonum Breastplate" },
            { "HSR - Sketch and Etch - Legs", "Etched Pattern for Carbonum Legs" },
            { "HSR - Sketch and Etch - Arms", "Etched Pattern for Carbonum Arms" },
            { "HSR - Sketch and Etch - Boots", "Etched Pattern for Carbonum Boots" },
            { "HSR - Sketch and Etch - Gloves", "Etched Pattern for Carbonum Gloves" }
        };

        // Final armor piece names
        private static readonly Dictionary<string, string> FinalArmorPieces = new Dictionary<string, string>
        {
            { "Etched Pattern for Carbonum Helmet", "Carbonum Plate Helmet" },
            { "Etched Pattern for Carbonum Breastplate", "Carbonum Breastplate" },
            { "Etched Pattern for Carbonum Legs", "Carbonum Plate Legs" },
            { "Etched Pattern for Carbonum Arms", "Carbonum Plate Arms" },
            { "Etched Pattern for Carbonum Boots", "Carbonum Plate Boots" },
            { "Etched Pattern for Carbonum Gloves", "Carbonum Plate Gloves" }
        };

        // Store command-based processing requests
        private static Dictionary<int, CarbArmorRequest> _pendingRequests = new Dictionary<int, CarbArmorRequest>();



        // Track carb armor processing completion for custom return messages
        private static Dictionary<int, string> _completionMessages = new Dictionary<int, string>();

        // Track processed pieces for loose item processing (playerId -> toolName -> processedCount)
        private static Dictionary<int, Dictionary<string, int>> _processedPieces = new Dictionary<int, Dictionary<string, int>>();

        // Flag to prevent duplicate failsafe messages during bag analysis
        private static readonly Dictionary<int, bool> _failsafeMessageSent = new Dictionary<int, bool>();

        // Track processed sessions to prevent duplicate processing
        private static readonly HashSet<string> _processedSessions = new HashSet<string>();

        // DELETED: _requestProcessed tracking - no longer needed with per-item architecture

        public override bool CanProcess(Item item)
        {
            // Simple carb item detection (like plasma)
            bool isCarbSheet = item.Name.Equals("Sheet of Curved Carbonum Plating", StringComparison.OrdinalIgnoreCase);
            bool isEtchedPattern = item.Name.Contains("Etched Pattern for Carbonum");
            bool isCarbonumPlate = item.Name.Contains("Carbonum Plate") || item.Name.Contains("Carbonum Helmet") ||
                                   item.Name.Contains("Carbonum Chest") || item.Name.Contains("Carbonum Sleeves") ||
                                   item.Name.Contains("Carbonum Pants") || item.Name.Contains("Carbonum Boots") ||
                                   item.Name.Contains("Carbonum Gloves");

            bool canProcess = isCarbSheet || isEtchedPattern || isCarbonumPlate;

            RecipeUtilities.LogDebug($"[CARB ARMOR CHECK] Item: '{item.Name}' -> Can process: {canProcess}");
            return canProcess;
        }

        public override RecipeAnalysisResult AnalyzeItems(List<Item> items)
        {
            return AnalyzeItemsShared(items, "Carb Armor Processing");
        }

        protected override async Task ProcessRecipeLogic(Item item, Container targetContainer)
        {
            // CRITICAL FIX: ProcessRecipeLogic is called ONCE PER ITEM by the base class loop
            // We must process ONLY the single item we're given, not try to process all sheets
            // The base class will call this method for each sheet in the batch

            if (targetContainer != null)
            {
                // BAG PROCESSING - Process THIS SINGLE sheet
                await ProcessSingleCarbArmorItemBag(item, targetContainer);
            }
            else
            {
                // LOOSE PROCESSING
                await ProcessSingleCarbArmorItemLoose(item);
            }
        }

        /// <summary>
        /// Process ALL carb armor sheets in the bag according to the player's command
        /// Following ImplantRecipe pattern for comprehensive processing
        /// </summary>
        private async Task ProcessAllCarbArmorSheets(Container targetContainer)
        {
            try
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Starting comprehensive carb armor sheet processing");

                var currentPlayer = Modules.PrivateMessageModule.GetCurrentProcessingPlayer();
                if (!currentPlayer.HasValue)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] No current player found for bag processing");
                    return;
                }

                // Get or create request for this player
                CarbArmorRequest request = null;
                if (_pendingRequests.ContainsKey(currentPlayer.Value))
                {
                    request = _pendingRequests[currentPlayer.Value];
                    RecipeUtilities.LogDebug($"[{RecipeName}] Found carb armor command for player {currentPlayer.Value}");
                }
                else
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] No command found - creating generic request");
                    request = new CarbArmorRequest
                    {
                        PlayerId = currentPlayer.Value,
                        SlotRequests = new Dictionary<string, int> { { "HSR - Sketch and Etch - Arms", 1 } },
                        TotalSheetsRequired = 1,
                        RequestTime = DateTime.Now
                    };
                }

                // Process all sheets in inventory according to the request
                // Following ImplantRecipe pattern: process all items that are already in inventory
                var sheets = Inventory.Items.Where(invItem => invItem.Name == "Sheet of Curved Carbonum Plating").ToList();
                RecipeUtilities.LogDebug($"[{RecipeName}] Processing {sheets.Count} sheets for player {request.PlayerId}");

                foreach (var sheet in sheets)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Processing sheet: {sheet.Name}");
                    await ProcessCarbArmorItemCore(sheet, request, targetContainer);
                    await Task.Delay(100); // Small delay between sheets
                }

                RecipeUtilities.LogDebug($"[{RecipeName}] Completed comprehensive carb armor sheet processing");
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error in ProcessAllCarbArmorSheets: {ex.Message}");
            }
        }







        /// <summary>
        /// Process a single carb armor sheet through all steps (1-3)
        /// </summary>
        private async Task<bool> ProcessSingleCarbArmorSheet(Item sheet)
        {
            try
            {
                // Check if we have any pending requests for this player
                var currentPlayerId = GetCurrentProcessingPlayerId();
                if (currentPlayerId == null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] No current player ID - skipping sheet processing");
                    return false;
                }

                var request = GetCarbArmorRequestForPlayer(currentPlayerId.Value);
                if (request == null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] No carb armor request found for player {currentPlayerId} - skipping");
                    return false;
                }

                // Find next slot that needs processing
                var nextSlot = GetNextSlotToProcess(request);
                if (string.IsNullOrEmpty(nextSlot))
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] All requested armor pieces completed - no more sheets needed");
                    return false;
                }

                RecipeUtilities.LogDebug($"[{RecipeName}] Processing sheet for slot: {nextSlot}");

                // Process this sheet through all 3 steps
                bool success = await ProcessSpecificSheet(sheet, nextSlot, currentPlayerId.Value);

                if (success)
                {
                    // Update the request completion count
                    IncrementSlotCompletion(request, nextSlot);
                    RecipeUtilities.LogDebug($"[{RecipeName}] Successfully processed sheet for {nextSlot}");
                }

                return success;
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogError($"[{RecipeName}] Error processing carb armor sheet: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Process a single carb armor piece for siding (step 3 only)
        /// </summary>
        private async Task<bool> ProcessSingleCarbArmorPiece(Item armorPiece)
        {
            try
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Processing armor piece for siding: {armorPiece.Name}");

                // Try to apply siding tools (Clanalizer/Omnifier)
                bool sidingApplied = await ApplySidingToArmorPiece(armorPiece);

                if (sidingApplied)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Successfully applied siding to {armorPiece.Name}");
                }
                else
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] No siding tools available for {armorPiece.Name}");
                }

                return sidingApplied;
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogError($"[{RecipeName}] Error processing carb armor piece: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get the current processing player ID from the base class
        /// </summary>
        private int? GetCurrentProcessingPlayerId()
        {
            // Access the protected field from BaseRecipeProcessor
            var field = typeof(BaseRecipeProcessor).GetField("_currentProcessingPlayer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var playerId = field?.GetValue(this) as int?;
            return playerId;
        }

        /// <summary>
        /// Get carb armor request for a specific player
        /// </summary>
        private CarbArmorRequest GetCarbArmorRequestForPlayer(int playerId)
        {
            return _pendingRequests.ContainsKey(playerId) ? _pendingRequests[playerId] : null;
        }

        /// <summary>
        /// Get the next slot that needs processing from the request
        /// </summary>
        private string GetNextSlotToProcess(CarbArmorRequest request)
        {
            var processedPieces = _processedPieces.ContainsKey(request.PlayerId) ?
                _processedPieces[request.PlayerId] : new Dictionary<string, int>();

            foreach (var slotRequest in request.SlotRequests)
            {
                var toolName = slotRequest.Key;
                var requestedCount = slotRequest.Value;
                var completedCount = processedPieces.ContainsKey(toolName) ? processedPieces[toolName] : 0;

                if (completedCount < requestedCount)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Next slot to process: {toolName} ({completedCount}/{requestedCount} completed)");
                    return toolName;
                }
            }

            RecipeUtilities.LogDebug($"[{RecipeName}] All requested armor pieces have been completed");
            return null;
        }

        /// <summary>
        /// Increment the completion count for a specific slot
        /// </summary>
        private void IncrementSlotCompletion(CarbArmorRequest request, string slotName)
        {
            if (!_processedPieces.ContainsKey(request.PlayerId))
            {
                _processedPieces[request.PlayerId] = new Dictionary<string, int>();
            }

            if (!_processedPieces[request.PlayerId].ContainsKey(slotName))
            {
                _processedPieces[request.PlayerId][slotName] = 0;
            }

            _processedPieces[request.PlayerId][slotName]++;

            var completed = _processedPieces[request.PlayerId][slotName];
            var requested = request.SlotRequests[slotName];
            RecipeUtilities.LogDebug($"[{RecipeName}] Completed {slotName} - Total completed: {completed}/{requested}");
        }

        /// <summary>
        /// Process a specific sheet through all 3 steps for a given slot
        /// </summary>
        private async Task<bool> ProcessSpecificSheet(Item sheet, string slotName, int playerId)
        {
            try
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Processing SPECIFIC SHEET: {sheet.Name} for tool: {slotName}");

                // Step 1: Sheet + Tool -> Etched Pattern
                var tool = FindTool(slotName);
                if (tool == null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Tool not found: {slotName}");
                    return false;
                }

                RecipeUtilities.LogDebug($"[{RecipeName}] Step 1: Combining {tool.Name} + {sheet.Name}");
                await CombineItems(tool, sheet);

                // Check if combination was successful by looking for the expected result
                await Task.Delay(200);
                var expectedPattern = SketchingToolOutputs[slotName];
                var etchedPattern = Inventory.Items.FirstOrDefault(item => item.Name == expectedPattern);
                if (etchedPattern == null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step 1 failed - could not create etched pattern: {expectedPattern}");
                    return false;
                }

                // Step 2: Screwdriver + Etched Pattern -> Armor Piece
                var screwdriver = FindTool("Screwdriver");
                if (screwdriver == null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Screwdriver not found");
                    return false;
                }

                RecipeUtilities.LogDebug($"[{RecipeName}] Step 2: Combining {screwdriver.Name} + {etchedPattern.Name}");
                await CombineItems(screwdriver, etchedPattern);

                // Check if combination was successful by looking for armor piece
                await Task.Delay(200);
                var expectedArmorPiece = FinalArmorPieces.ContainsKey(slotName) ? FinalArmorPieces[slotName] : null;
                if (expectedArmorPiece == null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] No expected armor piece mapping for {slotName}");
                    return false;
                }

                var armorPiece = Inventory.Items.FirstOrDefault(item => item.Name == expectedArmorPiece);
                if (armorPiece == null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step 2 failed - could not create armor piece: {expectedArmorPiece}");
                    return false;
                }

                // Step 3: Optional siding (Clanalizer/Omnifier)
                await ApplySidingToArmorPiece(armorPiece);

                RecipeUtilities.LogDebug($"[{RecipeName}] Successfully processed sheet for {slotName}");
                return true;
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogError($"[{RecipeName}] Error processing specific sheet: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Apply siding tools to an armor piece (step 3)
        /// </summary>
        private async Task<bool> ApplySidingToArmorPiece(Item armorPiece)
        {
            try
            {
                // Try Clanalizer first
                var clanalizer = FindTool("Clanalizer");
                if (clanalizer != null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Applying Clanalizer to {armorPiece.Name}");
                    await CombineItems(clanalizer, armorPiece);
                    return true;
                }

                // Try Omnifier if Clanalizer not found
                var omnifier = FindTool("Omnifier");
                if (omnifier != null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Applying Omnifier to {armorPiece.Name}");
                    await CombineItems(omnifier, armorPiece);
                    return true;
                }

                RecipeUtilities.LogDebug($"[{RecipeName}] No siding tools available for {armorPiece.Name}");
                return false;
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogError($"[{RecipeName}] Error applying siding: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// LOOSE ITEM PROCESSING - Command required, failsafe active
        /// </summary>
        private async Task ProcessSingleCarbArmorItemLoose(Item item)
        {
            try
            {
                var currentPlayer = Modules.PrivateMessageModule.GetCurrentProcessingPlayer();
                if (!currentPlayer.HasValue)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] LOOSE: No current player found");
                    return;
                }

                // DEBUGGING: Log detailed information about pending requests
                RecipeUtilities.LogDebug($"[{RecipeName}] LOOSE: Current processing player: {currentPlayer.Value}");
                RecipeUtilities.LogDebug($"[{RecipeName}] LOOSE: Total pending requests: {_pendingRequests.Count}");
                RecipeUtilities.LogDebug($"[{RecipeName}] LOOSE: Pending request player IDs: [{string.Join(", ", _pendingRequests.Keys)}]");

                // LOOSE PROCESSING: Command is REQUIRED
                CarbArmorRequest request = null;
                if (_pendingRequests.ContainsKey(currentPlayer.Value))
                {
                    request = _pendingRequests[currentPlayer.Value];
                    RecipeUtilities.LogDebug($"[{RecipeName}] LOOSE: Found carb armor command for player {currentPlayer.Value}");
                    RecipeUtilities.LogDebug($"[{RecipeName}] LOOSE: Request details: {request.TotalSheetsRequired} sheets for {request.SlotRequests.Count} slot types");
                }
                else
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] LOOSE: No carb armor command found for player {currentPlayer.Value}");
                    RecipeUtilities.LogDebug($"[{RecipeName}] LOOSE: Available player IDs in pending requests: [{string.Join(", ", _pendingRequests.Keys)}]");
                    RecipeUtilities.LogDebug($"[{RecipeName}] LOOSE: This indicates the player issued a carb armor command but the player ID doesn't match");
                    TriggerFailsafe(currentPlayer.Value, "No carb armor command found - player ID mismatch");
                    return;
                }

                // Process the item
                await ProcessCarbArmorItemCore(item, request, null);
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error in loose processing: {ex.Message}");
            }
        }

        /// <summary>
        /// BAG PROCESSING - No command required, failsafe disabled
        /// </summary>
        private async Task ProcessSingleCarbArmorItemBag(Item item, Container targetContainer)
        {
            try
            {
                var currentPlayer = Modules.PrivateMessageModule.GetCurrentProcessingPlayer();
                if (!currentPlayer.HasValue)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] BAG: No current player found");
                    return;
                }

                // BAG PROCESSING: Command is OPTIONAL (create generic request if needed)
                CarbArmorRequest request = null;
                if (_pendingRequests.ContainsKey(currentPlayer.Value))
                {
                    request = _pendingRequests[currentPlayer.Value];
                    RecipeUtilities.LogDebug($"[{RecipeName}] BAG: Found carb armor command for player {currentPlayer.Value}");
                }
                else
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] BAG: No command found - creating generic request (no failsafe)");
                    request = new CarbArmorRequest
                    {
                        PlayerId = currentPlayer.Value,
                        SlotRequests = new Dictionary<string, int> { { "Sketching Tool (Sleeve)", 1 } },
                        TotalSheetsRequired = 1,
                        RequestTime = DateTime.Now
                    };
                }

                // Process the item
                await ProcessCarbArmorItemCore(item, request, targetContainer);
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error in bag processing: {ex.Message}");
            }
        }

        /// <summary>
        /// SHARED CORE PROCESSING - Used by both loose and bag processing
        /// </summary>
        private async Task ProcessCarbArmorItemCore(Item item, CarbArmorRequest request, Container targetContainer)
        {
            try
            {
                var currentPlayer = request.PlayerId;

                // Process this single item
                if (item.Name.Equals("Sheet of Curved Carbonum Plating", StringComparison.OrdinalIgnoreCase))
                {
                    RecipeUtilities.LogCritical($"🔧 Processing single sheet: {item.Name}");

                    // Find the next armor piece to create from the request
                    var nextSlot = GetNextArmorSlotToProcess(request, currentPlayer);
                    if (nextSlot != null)
                    {
                        RecipeUtilities.LogDebug($"[{RecipeName}] Creating {nextSlot} armor piece from THIS SPECIFIC sheet");
                        await ProcessSingleArmorPieceFromSpecificSheet(item, nextSlot, targetContainer);

                        // Track this piece as completed
                        if (!_processedPieces.ContainsKey(currentPlayer))
                            _processedPieces[currentPlayer] = new Dictionary<string, int>();

                        if (!_processedPieces[currentPlayer].ContainsKey(nextSlot))
                            _processedPieces[currentPlayer][nextSlot] = 0;
                        _processedPieces[currentPlayer][nextSlot]++;

                        RecipeUtilities.LogDebug($"[{RecipeName}] Completed {nextSlot} - Total completed: {_processedPieces[currentPlayer].Count}");
                    }
                    else
                    {
                        RecipeUtilities.LogDebug($"[{RecipeName}] No more armor pieces to create - request may be complete");
                    }
                }
                else
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Processing non-sheet item: {item.Name}");

                    // Handle non-sheet items directly (etched patterns, armor pieces, etc.)
                    await ProcessNonSheetCarbItem(item, request);
                }
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error in core processing: {ex.Message}");
            }
        }

        // DELETED: ProcessSingleCarbArmorItem - replaced with separate loose/bag processing methods

        /// <summary>
        /// Get the next armor slot that needs to be processed from the request
        /// </summary>
        private string GetNextArmorSlotToProcess(CarbArmorRequest request, int playerId)
        {
            try
            {
                // Get already processed pieces for this player
                var processedPieces = _processedPieces.ContainsKey(playerId) ? _processedPieces[playerId] : new Dictionary<string, int>();

                // Find the next slot that still needs pieces
                foreach (var slotRequest in request.SlotRequests)
                {
                    var slotName = slotRequest.Key;
                    var requestedQuantity = slotRequest.Value;
                    var completedQuantity = processedPieces.ContainsKey(slotName) ? processedPieces[slotName] : 0;

                    if (completedQuantity < requestedQuantity)
                    {
                        RecipeUtilities.LogDebug($"[{RecipeName}] Next slot to process: {slotName} ({completedQuantity}/{requestedQuantity} completed)");
                        return slotName;
                    }
                }

                RecipeUtilities.LogDebug($"[{RecipeName}] All requested armor pieces have been completed");
                return null;
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error getting next armor slot: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Trigger failsafe for carb armor processing
        /// </summary>
        private void TriggerFailsafe(int playerId, string reason)
        {
            try
            {
                RecipeUtilities.LogCritical($"🚨 [CARB ARMOR FAILSAFE] *** FAILSAFE TRIGGERED *** Reason: {reason}");

                // Check if we've already sent the failsafe message for this player
                if (!_failsafeMessageSent.ContainsKey(playerId) || !_failsafeMessageSent[playerId])
                {
                    string botName = DynelManager.LocalPlayer?.Name ?? "bot";
                    string helpMessage = $"Returning items! Carb Armor requires command-based processing. Please use '/tell {botName} help carbarmor' for instructions, then use '/tell {botName} trade carb [slot names]' to process.";

                    // Send the message directly using Client.SendPrivateMessage
                    Client.SendPrivateMessage((uint)playerId, helpMessage);

                    // Mark that we've sent the message for this player
                    _failsafeMessageSent[playerId] = true;

                    RecipeUtilities.LogCritical($"🚨 [CARB ARMOR FAILSAFE] Sent help message to player {playerId} ({reason}): {helpMessage}");
                }
                else
                {
                    RecipeUtilities.LogCritical($"🚨 [CARB ARMOR FAILSAFE] Failsafe message already sent to player {playerId} - skipping duplicate ({reason})");
                }
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogCritical($"🚨 [CARB ARMOR FAILSAFE] Error triggering failsafe: {ex.Message}");
            }
        }

        /// <summary>
        /// Process non-sheet carb armor items (etched patterns, armor pieces) - per-item architecture
        /// </summary>
        private async Task ProcessNonSheetCarbItem(Item item, CarbArmorRequest request)
        {
            try
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Processing non-sheet item: {item.Name}");

                // Check if this is an etched pattern - if so, process just that specific armor piece
                string correspondingTool = null;
                foreach (var kvp in SketchingToolOutputs)
                {
                    if (kvp.Value.Equals(item.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        correspondingTool = kvp.Key;
                        break;
                    }
                }

                if (correspondingTool != null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Item is an etched pattern - processing single armor piece for tool: {correspondingTool}");
                    await ProcessSingleArmorPiece(correspondingTool, null);
                    return;
                }

                // Check if this is a carbonum armor piece - if so, process just the siding step
                foreach (var kvp in FinalArmorPieces)
                {
                    if (kvp.Value.Equals(item.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        RecipeUtilities.LogDebug($"[{RecipeName}] Item is a carbonum armor piece - processing siding step only");
                        await PerformStep3_OptionalSidedUpgrade(item.Name);
                        return;
                    }
                }

                RecipeUtilities.LogDebug($"[{RecipeName}] Could not determine processing type for non-sheet item: {item.Name}");
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error processing non-sheet carb item: {ex.Message}");
            }
        }

        // DELETED: ProcessCarbArmorCore method - replaced with per-item ProcessSingleCarbArmorItem architecture

        // DELETED: SendFailsafeMessage method - replaced with TriggerFailsafe method in per-item architecture

        /// <summary>
        /// Processes a single armor piece from a specific sheet (no global detection) - ITEM-SPECIFIC PROCESSING
        /// </summary>
        private async Task ProcessSingleArmorPieceFromSpecificSheet(Item specificSheet, string toolName, Container targetContainer)
        {
            try
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Processing SPECIFIC SHEET: {specificSheet.Name} for tool: {toolName}");

                string etchedPatternName = SketchingToolOutputs[toolName];
                string armorPieceName = FinalArmorPieces[etchedPatternName];

                // Step 1: Process THIS SPECIFIC sheet (not any sheet in inventory)
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 1: Processing THIS specific sheet: {specificSheet.Name}");
                await PerformStep1_CreateEtchedPatternFromSpecificSheet(specificSheet, toolName);

                // Wait for etched pattern to be created
                await Task.Delay(200);

                // Step 2: Find the newly created etched pattern and process it
                var newEtchedPattern = Inventory.Items.FirstOrDefault(i => i.Name.Equals(etchedPatternName, StringComparison.OrdinalIgnoreCase));
                if (newEtchedPattern != null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step 2: Found newly created etched pattern: {newEtchedPattern.Name}");
                    await PerformStep2_CreateArmorPiece(etchedPatternName);

                    // Wait for armor piece to be created
                    await Task.Delay(200);

                    // Step 3: Find the newly created armor piece and process it
                    var newArmorPiece = Inventory.Items.FirstOrDefault(i => i.Name.Equals(armorPieceName, StringComparison.OrdinalIgnoreCase));
                    if (newArmorPiece != null)
                    {
                        RecipeUtilities.LogDebug($"[{RecipeName}] Step 3: Found newly created armor piece: {newArmorPiece.Name}");
                        await PerformStep3_OptionalSidedUpgrade(armorPieceName);
                    }
                    else
                    {
                        RecipeUtilities.LogDebug($"[{RecipeName}] Step 3: Armor piece not found after Step 2: {armorPieceName}");
                    }
                }
                else
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step 2: Etched pattern not found after Step 1: {etchedPatternName}");
                }

                RecipeUtilities.LogDebug($"[{RecipeName}] Completed processing specific sheet: {specificSheet.Name}");
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error processing specific sheet: {ex.Message}");
            }
        }

        /// <summary>
        /// Step 1 using a specific sheet (not global detection) - FIXED VERSION
        /// </summary>
        private async Task PerformStep1_CreateEtchedPatternFromSpecificSheet(Item specificSheet, string toolName)
        {
            try
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 1: Using SPECIFIC sheet: {specificSheet.Name} (not searching inventory)");

                // Use CORE LOGIC for tool finding
                var tool = FindTool(toolName);

                if (tool == null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Tool not found: {toolName}");
                    return;
                }

                // CRITICAL FIX: Re-find the sheet in inventory after it's been moved
                // The original Item reference might be stale after MoveToInventory()
                var freshSheet = Inventory.Items.FirstOrDefault(i =>
                    i.Name.Equals("Sheet of Curved Carbonum Plating", StringComparison.OrdinalIgnoreCase) &&
                    i.Id == specificSheet.Id); // Use Id to find the exact same item

                if (freshSheet == null)
                {
                    // Fallback: just find any sheet (but log it as a fallback)
                    freshSheet = Inventory.Items.FirstOrDefault(i => i.Name.Equals("Sheet of Curved Carbonum Plating", StringComparison.OrdinalIgnoreCase));
                    RecipeUtilities.LogDebug($"[{RecipeName}] WARNING: Could not find specific sheet by Identity, using fallback sheet");
                }

                if (freshSheet == null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] ERROR: No carbonum sheet found in inventory for Step 1");
                    return;
                }

                RecipeUtilities.LogDebug($"[{RecipeName}] Step 1: Combining {tool.Name} + {freshSheet.Name} (FRESH SHEET REFERENCE)");

                // Use CORE LOGIC for combining - use the FRESH sheet reference
                await CombineItems(tool, freshSheet);

                RecipeUtilities.LogDebug($"[{RecipeName}] Step 1 complete - created etched pattern from fresh sheet reference");
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error in Step 1 with specific sheet: {ex.Message}");
            }
        }

        /// <summary>
        /// Clears any stale completion messages for a player
        /// </summary>
        /// <param name="playerId">Player ID to clear messages for</param>
        public static void ClearCompletionMessage(int playerId)
        {
            if (_completionMessages.ContainsKey(playerId))
            {
                _completionMessages.Remove(playerId);
                RecipeUtilities.LogDebug($"[CARB ARMOR] Cleared stale completion message for player {playerId}");
            }
        }

        /// <summary>
        /// Check if there's a pending carb armor request for a player
        /// </summary>
        /// <param name="playerId">Player ID to check</param>
        /// <returns>True if there's a pending request</returns>
        public static bool HasPendingRequest(int playerId)
        {
            bool hasRequest = _pendingRequests.ContainsKey(playerId);
            RecipeUtilities.LogDebug($"[CARB ARMOR] HasPendingRequest({playerId}): {hasRequest}");
            if (_pendingRequests.Any())
            {
                RecipeUtilities.LogDebug($"[CARB ARMOR] Current pending requests: {string.Join(", ", _pendingRequests.Keys)}");
            }
            else
            {
                RecipeUtilities.LogDebug($"[CARB ARMOR] No pending requests at all");
            }
            return hasRequest;
        }

        /// <summary>
        /// Parses a carb armor command and creates a processing request
        /// </summary>
        /// <param name="playerId">Player requesting the processing</param>
        /// <param name="args">Command arguments (slot names with optional quantities)</param>
        /// <returns>Number of sheets required, or -1 if invalid</returns>
        public static int ParseCarbArmorCommand(int playerId, string[] args)
        {
            try
            {
                // Clear any stale completion message for this player
                ClearCompletionMessage(playerId);

                var slotRequests = new Dictionary<string, int>();
                
                foreach (string arg in args)
                {
                    if (string.IsNullOrWhiteSpace(arg)) continue;

                    // Parse quantity and slot name (e.g., "2sleeve", "head", "3feet")
                    string slotName;
                    int quantity = 1;

                    // Check if the argument starts with a number
                    if (char.IsDigit(arg[0]))
                    {
                        // Extract the number and the slot name
                        int i = 0;
                        while (i < arg.Length && char.IsDigit(arg[i]))
                        {
                            i++;
                        }
                        
                        if (i > 0 && i < arg.Length)
                        {
                            if (int.TryParse(arg.Substring(0, i), out quantity))
                            {
                                slotName = arg.Substring(i);
                            }
                            else
                            {
                                RecipeUtilities.LogDebug($"[CarbArmor] Invalid quantity in argument: {arg}");
                                continue;
                            }
                        }
                        else
                        {
                            RecipeUtilities.LogDebug($"[CarbArmor] Invalid format in argument: {arg}");
                            continue;
                        }
                    }
                    else
                    {
                        slotName = arg;
                    }

                    // Validate slot name
                    if (!SlotNameMappings.ContainsKey(slotName))
                    {
                        RecipeUtilities.LogDebug($"[CarbArmor] Invalid slot name: {slotName}");
                        return -1; // Invalid slot name
                    }

                    string toolName = SlotNameMappings[slotName];
                    
                    // Add or update the quantity for this slot
                    if (slotRequests.ContainsKey(toolName))
                    {
                        slotRequests[toolName] += quantity;
                    }
                    else
                    {
                        slotRequests[toolName] = quantity;
                    }
                }

                if (slotRequests.Count == 0)
                {
                    RecipeUtilities.LogDebug($"[CarbArmor] No valid slot names found in command");
                    return -1; // No valid slots
                }

                // Calculate total sheets needed
                int totalSheets = slotRequests.Values.Sum();

                // Store the request for processing
                _pendingRequests[playerId] = new CarbArmorRequest
                {
                    PlayerId = playerId,
                    SlotRequests = slotRequests,
                    TotalSheetsRequired = totalSheets,
                    RequestTime = DateTime.Now
                };

                RecipeUtilities.LogDebug($"[CARB ARMOR DEBUG] *** CREATED REQUEST FOR PLAYER {playerId} ***");
                RecipeUtilities.LogDebug($"[CARB ARMOR DEBUG] Player ID type: {playerId.GetType().Name}, Value: {playerId}");
                RecipeUtilities.LogDebug($"[CARB ARMOR DEBUG] Request details: {totalSheets} sheets for {slotRequests.Count} slot types");
                RecipeUtilities.LogDebug($"[CARB ARMOR DEBUG] Slot requests: {string.Join(", ", slotRequests.Select(sr => $"{sr.Value}x {sr.Key}"))}");
                RecipeUtilities.LogDebug($"[CARB ARMOR DEBUG] Total pending requests now: {_pendingRequests.Count}");
                RecipeUtilities.LogDebug($"[CARB ARMOR DEBUG] All pending request player IDs: {string.Join(", ", _pendingRequests.Keys)}");
                return totalSheets;
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[CarbArmor] Error parsing command: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// Gets valid slot names for help display
        /// </summary>
        public static string GetValidSlotNames()
        {
            var uniqueSlots = SlotNameMappings.Keys.GroupBy(k => SlotNameMappings[k]).Select(g => g.First()).ToList();
            return string.Join(", ", uniqueSlots);
        }





        // DELETED: ProcessIndividualCarbItem method - replaced with per-item ProcessNonSheetCarbItem architecture

        /// <summary>
        /// Processes a carb armor request through all 3 steps
        /// </summary>
        private async Task ProcessCarbArmorRequest(CarbArmorRequest request, Container targetContainer)
        {
            try
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Starting carb armor processing for {request.SlotRequests.Count} slot types");

                // For bag-based processing, move sheets to inventory
                if (targetContainer != null)
                {
                    await MoveComponentsToInventoryShared(targetContainer);
                }
                else
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Processing loose items - sheets already in inventory");
                }

                // Process each slot type with the requested quantity
                foreach (var slotRequest in request.SlotRequests)
                {
                    string toolName = slotRequest.Key;
                    int quantity = slotRequest.Value;

                    RecipeUtilities.LogDebug($"[{RecipeName}] Processing {quantity}x {toolName}");

                    for (int i = 0; i < quantity; i++)
                    {
                        await ProcessSingleArmorPiece(toolName, targetContainer);

                        // Small delay between pieces for stability
                        await Task.Delay(100);
                    }
                }

                // RULE #5: Return all tools to bags after processing
                await Modules.PrivateMessageModule.EndToolSession();

                // For bag-based processing, ensure items return to bag
                if (targetContainer != null)
                {
                    await EnsureItemsReturnToBagShared(targetContainer, 0);
                }
                else
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Loose item processing - items remain in inventory for return");
                }

                RecipeUtilities.LogCritical($"✅ Carb Armor processing complete - {request.SlotRequests.Values.Sum()} pieces created");
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error in ProcessCarbArmorRequest: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes a single armor piece through all 3 steps - VTE-style detection
        /// </summary>
        private async Task ProcessSingleArmorPiece(string toolName, Container targetContainer)
        {
            try
            {
                string etchedPatternName = SketchingToolOutputs[toolName];
                string armorPieceName = FinalArmorPieces[etchedPatternName];

                // VTE-style detection: Check what stage we're at and continue from there
                var currentStage = DetectCarbArmorStage(toolName);
                RecipeUtilities.LogDebug($"[{RecipeName}] Detected current stage: {currentStage}");
                RecipeUtilities.LogDebug($"[{RecipeName}] CARB STAGE DETECTED: {currentStage} for tool {toolName}");

                switch (currentStage)
                {
                    case "Sheet":
                        RecipeUtilities.LogDebug($"[{RecipeName}] Starting from Step 1: Sheet → Etched Pattern");
                        await PerformStep1_CreateEtchedPattern(toolName);
                        await Task.Delay(200);
                        goto case "EtchedPattern"; // Continue to next step

                    case "EtchedPattern":
                        RecipeUtilities.LogDebug($"[{RecipeName}] Starting from Step 2: Etched Pattern → Armor Piece");
                        RecipeUtilities.LogDebug($"[{RecipeName}] CALLING STEP 2 with pattern: {etchedPatternName}");
                        await PerformStep2_CreateArmorPiece(etchedPatternName);
                        await Task.Delay(200);
                        goto case "ArmorPiece"; // Continue to next step

                    case "ArmorPiece":
                        RecipeUtilities.LogDebug($"[{RecipeName}] Starting from Step 3: Armor Piece → Sided Armor (optional)");
                        await PerformStep3_OptionalSidedUpgrade(armorPieceName);
                        break;

                    case "Complete":
                        RecipeUtilities.LogDebug($"[{RecipeName}] Armor piece already complete");
                        break;

                    default:
                        RecipeUtilities.LogDebug($"[{RecipeName}] Unknown stage, starting from beginning");
                        await PerformStep1_CreateEtchedPattern(toolName);
                        await Task.Delay(200);
                        await PerformStep2_CreateArmorPiece(etchedPatternName);
                        await Task.Delay(200);
                        await PerformStep3_OptionalSidedUpgrade(armorPieceName);
                        break;
                }

                RecipeUtilities.LogDebug($"[{RecipeName}] Completed single armor piece: {armorPieceName}");
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error processing single armor piece: {ex.Message}");
            }
        }

        /// <summary>
        /// Detects what stage the carb armor is currently at (VTE-style)
        /// </summary>
        private string DetectCarbArmorStage(string toolName)
        {
            try
            {
                string etchedPatternName = SketchingToolOutputs[toolName];
                string armorPieceName = FinalArmorPieces[etchedPatternName];

                // Check for final sided armor first
                var sidedArmor = Inventory.Items.FirstOrDefault(i =>
                    (i.Name.Contains("Clan") || i.Name.Contains("Omni")) &&
                    i.Name.Contains("Carbonum"));
                if (sidedArmor != null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Found sided armor: {sidedArmor.Name}");
                    return "Complete";
                }

                // Check for carbonum armor piece
                var armorPiece = Inventory.Items.FirstOrDefault(i => i.Name.Equals(armorPieceName, StringComparison.OrdinalIgnoreCase));
                if (armorPiece != null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Found armor piece: {armorPiece.Name}");
                    return "ArmorPiece";
                }

                // Check for etched pattern
                var etchedPattern = Inventory.Items.FirstOrDefault(i => i.Name.Equals(etchedPatternName, StringComparison.OrdinalIgnoreCase));
                if (etchedPattern != null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Found etched pattern: {etchedPattern.Name}");
                    return "EtchedPattern";
                }

                // Check for sheet
                var sheet = Inventory.Items.FirstOrDefault(i => i.Name.Equals("Sheet of Curved Carbonum Plating", StringComparison.OrdinalIgnoreCase));
                if (sheet != null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Found sheet: {sheet.Name}");
                    return "Sheet";
                }

                RecipeUtilities.LogDebug($"[{RecipeName}] No carb items found for this tool type");
                return "None";
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error detecting stage: {ex.Message}");
                return "Unknown";
            }
        }

        /// <summary>
        /// Step 1: Sheet of Curved Carbonum Plating + Sketching Tool → Etched Pattern
        /// </summary>
        private async Task PerformStep1_CreateEtchedPattern(string toolName)
        {
            try
            {
                // Find the sheet using standard inventory search
                var sheet = Inventory.Items.FirstOrDefault(i => i.Name.Equals("Sheet of Curved Carbonum Plating", StringComparison.OrdinalIgnoreCase));

                if (sheet == null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] No carbonum sheet found for step 1");
                    return;
                }

                // Use CORE LOGIC for tool finding
                var tool = FindTool(toolName);

                if (tool == null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Tool not found: {toolName}");
                    return;
                }

                RecipeUtilities.LogDebug($"[{RecipeName}] Step 1: Combining {tool.Name} + {sheet.Name}");

                // Use CORE LOGIC for combining
                await CombineItems(tool, sheet);

                RecipeUtilities.LogDebug($"[{RecipeName}] Step 1 complete - created etched pattern");
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error in Step 1: {ex.Message}");
            }
        }

        /// <summary>
        /// Step 2: Etched Pattern + Screwdriver → Carbonum Armor Piece
        /// </summary>
        private async Task PerformStep2_CreateArmorPiece(string etchedPatternName)
        {
            try
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] === STEP 2 START ===");
                RecipeUtilities.LogDebug($"[{RecipeName}] STEP 2 CALLED! Looking for pattern: {etchedPatternName}");
                RecipeUtilities.LogDebug($"[{RecipeName}] Looking for etched pattern: {etchedPatternName}");

                // Find the etched pattern using EXACT matching (not Contains!)
                var etchedPattern = Inventory.Items.FirstOrDefault(i => i.Name.Equals(etchedPatternName, StringComparison.OrdinalIgnoreCase));
                if (etchedPattern == null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] STEP 2 FAILED: Etched pattern not found: {etchedPatternName}");
                    RecipeUtilities.LogDebug($"[{RecipeName}] Available inventory items: {string.Join(", ", Inventory.Items.Select(i => i.Name))}");
                    return;
                }
                RecipeUtilities.LogDebug($"[{RecipeName}] ✓ Found etched pattern: {etchedPattern.Name}");

                // Use CORE LOGIC for tool finding
                var screwdriver = FindTool("Screwdriver");

                if (screwdriver != null && etchedPattern != null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Found screwdriver and etched pattern, combining them");
                    await CombineItems(screwdriver, etchedPattern);
                }
                else
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Missing components - screwdriver: {screwdriver?.Name ?? "NULL"}, pattern: {etchedPattern?.Name ?? "NULL"}");
                }

                RecipeUtilities.LogDebug($"[{RecipeName}] === STEP 2 COMPLETE ===");
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] STEP 2 ERROR: {ex.Message}");
            }
        }

        /// <summary>
        /// Step 3: Optional sided armor upgrade (Carbonum Armor + Clanalizer/Omnifier → Sided Armor)
        /// </summary>
        private async Task PerformStep3_OptionalSidedUpgrade(string armorPieceName)
        {
            try
            {
                // Find the armor piece
                var armorPiece = Inventory.Items.FirstOrDefault(i => i.Name.Equals(armorPieceName, StringComparison.OrdinalIgnoreCase));
                if (armorPiece == null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Armor piece not found for step 3: {armorPieceName}");
                    return;
                }

                // Use CORE LOGIC for tool finding - try Clanalizer first
                var siderTool = FindTool("Clanalizer");

                // If no Clanalizer, try Omnifier
                if (siderTool == null)
                {
                    siderTool = FindTool("Omnifier");
                }

                if (siderTool == null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] No sided upgrade tool found - skipping step 3");
                    return;
                }

                RecipeUtilities.LogDebug($"[{RecipeName}] Step 3: Combining {siderTool.Name} + {armorPiece.Name}");

                // Use CORE LOGIC for combining
                await CombineItems(siderTool, armorPiece);

                RecipeUtilities.LogDebug($"[{RecipeName}] Step 3 complete - created sided armor");
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error in Step 3: {ex.Message}");
            }
        }

        /// <summary>
        /// Helper method to extract player ID from container (simplified for now)
        /// </summary>
        private int? GetPlayerIdFromContainer(Container container)
        {
            // This is a simplified implementation - in practice you'd need to track
            // which backpack belongs to which player during the trade process
            // For now, return the first pending request's player ID
            var firstKey = _pendingRequests.Keys.FirstOrDefault();
            return firstKey != 0 ? (int?)firstKey : null;
        }

        /// <summary>
        /// Gets the processing step type for messaging
        /// </summary>
        private string GetProcessingStepType(Item item)
        {
            if (item.Name.Equals("Sheet of Curved Carbonum Plating", StringComparison.OrdinalIgnoreCase))
                return "Step 1 (Etching)";
            else if (item.Name.Contains("Etched Pattern for Carbonum"))
                return "Step 2 (Shaping)";
            else if (item.Name.Contains("Carbonum"))
                return "Step 3 (Siding)";
            else
                return "processing";
        }

        /// <summary>
        /// Checks if an item is valid for the current carb armor request
        /// </summary>
        private bool IsItemValidForRequest(Item item, CarbArmorRequest request)
        {
            try
            {
                // Check if it's a sheet (valid for any request)
                if (item.Name.Equals("Sheet of Curved Carbonum Plating", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // Check if it's an etched pattern that matches one of the requested tools
                foreach (var toolName in request.SlotRequests.Keys)
                {
                    if (SketchingToolOutputs.ContainsKey(toolName))
                    {
                        string expectedPattern = SketchingToolOutputs[toolName];
                        if (item.Name.Equals(expectedPattern, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }

                // Check if it's a final armor piece that matches the request
                foreach (var toolName in request.SlotRequests.Keys)
                {
                    if (SketchingToolOutputs.ContainsKey(toolName))
                    {
                        string expectedPattern = SketchingToolOutputs[toolName];
                        if (FinalArmorPieces.ContainsKey(expectedPattern))
                        {
                            string expectedArmor = FinalArmorPieces[expectedPattern];
                            if (item.Name.Equals(expectedArmor, StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error checking if item is valid for request: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets completion message for a player (used by return trade system)
        /// </summary>
        public static string GetCompletionMessage(int playerId)
        {
            if (_completionMessages.ContainsKey(playerId))
            {
                var message = _completionMessages[playerId];
                _completionMessages.Remove(playerId); // Clean up after use
                return message;
            }
            return null;
        }

        /// <summary>
        /// Cleans up all tracking data for a player (called when return trade completes)
        /// </summary>
        public static void CleanupPlayerTracking(int playerId)
        {
            bool hadRequest = _pendingRequests.ContainsKey(playerId);
            bool hadTracking = _processedPieces.ContainsKey(playerId);
            bool hadCompletion = _completionMessages.ContainsKey(playerId);
            bool hadFailsafeFlag = _failsafeMessageSent.ContainsKey(playerId);

            _pendingRequests.Remove(playerId);
            _processedPieces.Remove(playerId);
            _completionMessages.Remove(playerId);
            _failsafeMessageSent.Remove(playerId); // Clear failsafe flag for next trade session

            if (hadRequest || hadTracking || hadCompletion || hadFailsafeFlag)
            {
                RecipeUtilities.LogDebug($"[CARB ARMOR] Cleaned up tracking for player {playerId} - Request: {hadRequest}, Tracking: {hadTracking}, Completion: {hadCompletion}, Failsafe: {hadFailsafeFlag}");
            }
        }

        // Removed shared processing methods - carb armor is COMMAND-ONLY
    }

    /// <summary>
    /// Represents a carb armor processing request
    /// </summary>
    public class CarbArmorRequest
    {
        public int PlayerId { get; set; }
        public Dictionary<string, int> SlotRequests { get; set; } = new Dictionary<string, int>();
        public int TotalSheetsRequired { get; set; }
        public DateTime RequestTime { get; set; }
    }
}
