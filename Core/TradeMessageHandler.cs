using System;
using System.Linq;
using AOSharp.Clientless;
using Craftbot.Modules;
using Craftbot.Recipes;

namespace Craftbot.Core
{
    /// <summary>
    /// Handles all trade-related commands in the unified message system
    /// Includes generic trades and carb armor commands
    /// </summary>
    public class TradeMessageHandler : IMessageHandler
    {
        public void HandleMessage(string senderName, MessageInfo messageInfo)
        {
            try
            {
                if (messageInfo.Command == "carb")
                {
                    // Handle carb armor command
                    HandleCarbArmorCommand(senderName, messageInfo.Arguments);
                }
                else if (messageInfo.Command == "trade")
                {
                    // Check if this is a special treatment library trade
                    if (messageInfo.Arguments.Length > 0 && messageInfo.Arguments[0].ToLower() == "treatlib")
                    {
                        HandleTreatmentLibraryCommand(senderName, messageInfo.Arguments);
                    }
                    else
                    {
                        // Handle generic trade command
                        HandleGenericTradeCommand(senderName, messageInfo.Arguments);
                    }
                }
            }
            catch (Exception ex)
            {
                PrivateMessageModule.LogDebug($"[TRADE HANDLER] Error: {ex.Message}");
                UnifiedMessageHandler.SendResponse(senderName, "Error processing trade request. Please try again.", MessageType.Error);
            }
        }

        private void HandleTreatmentLibraryCommand(string senderName, string[] args)
        {
            try
            {
                PrivateMessageModule.LogDebug($"[TRADE HANDLER] Processing treatment library request from {senderName}");

                // Check for doctor variation: "trade treatlib doc 100"
                bool isDoctorVariation = false;
                int qualityArgIndex = 1;

                if (args.Length >= 3 && args[1].ToLower() == "doc")
                {
                    isDoctorVariation = true;
                    qualityArgIndex = 2;
                    PrivateMessageModule.LogDebug($"[TRADE HANDLER] Detected doctor variation request");
                }

                // Parse quality level from command: "trade treatlib 100" or "trade treatlib doc 100"
                if (args.Length < qualityArgIndex + 1)
                {
                    string formatMessage = isDoctorVariation ?
                        "❌ Invalid format. Use: trade treatlib doc 100 (where 100 is target quality)" :
                        "❌ Invalid format. Use: trade treatlib 100 (where 100 is target quality)";
                    UnifiedMessageHandler.SendResponse(senderName, formatMessage, MessageType.Error);
                    return;
                }

                if (!int.TryParse(args[qualityArgIndex], out int targetQuality))
                {
                    string formatMessage = isDoctorVariation ?
                        "❌ Invalid quality level. Use: trade treatlib doc 100" :
                        "❌ Invalid quality level. Use: trade treatlib 100";
                    UnifiedMessageHandler.SendResponse(senderName, formatMessage, MessageType.Error);
                    return;
                }

                // Validate quality level range (1-300 for AO)
                if (targetQuality < 1 || targetQuality > 300)
                {
                    UnifiedMessageHandler.SendResponse(senderName, $"❌ Quality level {targetQuality} out of range (1-300)", MessageType.Error);
                    return;
                }

                // Find the player
                var targetPlayer = FindPlayer(senderName);
                if (targetPlayer == null)
                {
                    UnifiedMessageHandler.SendResponse(senderName, "You are not in the same area. Please come stand next to me and try again.", MessageType.Error);
                    return;
                }

                // Check distance
                float distance = targetPlayer.DistanceFrom(DynelManager.LocalPlayer);
                if (distance > 10f)
                {
                    UnifiedMessageHandler.SendResponse(senderName, $"You are too far away ({distance:F1}m). Please come closer (within 10m) and try again.", MessageType.Error);
                    return;
                }

                // Mark this as a special treatment library trade
                TreatmentLibraryTradeManager.SetPendingTreatmentLibraryTrade(senderName, targetQuality, isDoctorVariation);

                // Send success message
                string libraryType = isDoctorVariation ? "Treatment and Pharmacy Library" : "Treatment Library";
                UnifiedMessageHandler.SendResponse(senderName, $"✅ {libraryType} request received! Target quality: QL{targetQuality}. Please provide: Portable Surgery Clinic and Pharma Tech Tutoring Device. Opening trade window...", MessageType.Success);

                // Auto-open trade with delay
                System.Threading.Tasks.Task.Delay(500).ContinueWith(_ =>
                {
                    try
                    {
                        PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Opening trade with player {senderName} (ID: {targetPlayer.Identity.Instance})");
                        AOSharp.Clientless.Trade.Open(targetPlayer.Identity);
                    }
                    catch (Exception ex)
                    {
                        PrivateMessageModule.LogDebug($"[TREATMENT LIBRARY] Error opening trade: {ex.Message}");
                        UnifiedMessageHandler.SendResponse(senderName, "❌ Could not open trade. Please try the command again when you're closer.", MessageType.Error);
                    }
                });
            }
            catch (Exception ex)
            {
                PrivateMessageModule.LogDebug($"[TRADE HANDLER] Error in treatment library command: {ex.Message}");
                UnifiedMessageHandler.SendResponse(senderName, "Error processing treatment library request. Please try again.", MessageType.Error);
            }
        }

        private void HandleGenericTradeCommand(string senderName, string[] args)
        {
            try
            {
                PrivateMessageModule.LogDebug($"[TRADE HANDLER] Processing generic trade request from {senderName}");

                // Find the player
                var targetPlayer = FindPlayer(senderName);
                if (targetPlayer == null)
                {
                    UnifiedMessageHandler.SendResponse(senderName, "You are not in the same area. Please come stand next to me and try again.", MessageType.Error);
                    return;
                }

                // Check distance
                float distance = targetPlayer.DistanceFrom(DynelManager.LocalPlayer);
                if (distance > 10f)
                {
                    UnifiedMessageHandler.SendResponse(senderName, $"You are too far away ({distance:F1}m). Please come closer (within 10m) and try again.", MessageType.Error);
                    return;
                }

                // Check if bot is processing
                if (PrivateMessageModule.IsProcessingTrade())
                {
                    PrivateMessageModule.LogDebug($"[TRADE HANDLER] Adding to queue - bot is processing");
                    PrivateMessageModule.AddToTradeQueue(targetPlayer.Identity.Instance, targetPlayer.Name, targetPlayer.MovementComponent.Position);
                }
                else
                {
                    PrivateMessageModule.LogDebug($"[TRADE HANDLER] Starting trade immediately");
                    PrivateMessageModule.StartTradeWithPlayer(targetPlayer, senderName);
                }
            }
            catch (Exception ex)
            {
                PrivateMessageModule.LogDebug($"[TRADE HANDLER] Error in generic trade: {ex.Message}");
                UnifiedMessageHandler.SendResponse(senderName, "Error opening trade. Please try again.", MessageType.Error);
            }
        }

        private void HandleCarbArmorCommand(string senderName, string[] args)
        {
            try
            {
                PrivateMessageModule.LogDebug($"[TRADE HANDLER] Processing carb armor command from {senderName}: {string.Join(" ", args)}");

                // Find the player
                var targetPlayer = FindPlayer(senderName);
                if (targetPlayer == null)
                {
                    UnifiedMessageHandler.SendResponse(senderName, "You are not in the same area. Please come stand next to me and try again.", MessageType.Error);
                    return;
                }

                // Check distance
                float distance = targetPlayer.DistanceFrom(DynelManager.LocalPlayer);
                if (distance > 10f)
                {
                    UnifiedMessageHandler.SendResponse(senderName, $"You are too far away ({distance:F1}m). Please come closer (within 10m) and try again.", MessageType.Error);
                    return;
                }

                // Parse carb armor command
                int requestingPlayerId = targetPlayer.Identity.Instance;
                PrivateMessageModule.LogDebug($"[TRADE HANDLER] About to parse carb armor command for player {requestingPlayerId} with args: [{string.Join(", ", args)}]");

                int sheetsRequired = CarbArmorRecipe.ParseCarbArmorCommand(requestingPlayerId, args);

                PrivateMessageModule.LogDebug($"[TRADE HANDLER] ParseCarbArmorCommand returned: {sheetsRequired}");

                if (sheetsRequired <= 0)
                {
                    PrivateMessageModule.LogDebug($"[TRADE HANDLER] Invalid carb armor command - sheets required: {sheetsRequired}");
                    string validSlots = CarbArmorRecipe.GetValidSlotNames();
                    UnifiedMessageHandler.SendResponse(senderName,
                        $"Invalid carb armor command. Valid slot names: {validSlots}. Example: '/tell {Client.CharacterName} trade carb head chest 2sleeve'",
                        MessageType.Error);
                    return;
                }

                PrivateMessageModule.LogDebug($"[TRADE HANDLER] Valid carb armor command - {sheetsRequired} sheets required");

                // UNIFIED MESSAGE: Send single sheet requirement message (eliminates duplicate)
                UnifiedMessageHandler.SendResponse(senderName, 
                    $"Carb armor request received! Please provide {sheetsRequired} Sheet(s) of Curved Carbonum Plating. Opening trade window...", 
                    MessageType.Success);

                // Start trade
                if (PrivateMessageModule.IsProcessingTrade())
                {
                    PrivateMessageModule.LogDebug($"[TRADE HANDLER] Adding carb armor to queue");
                    PrivateMessageModule.AddToTradeQueue(requestingPlayerId, targetPlayer.Name, targetPlayer.MovementComponent.Position);
                }
                else
                {
                    PrivateMessageModule.LogDebug($"[TRADE HANDLER] Starting carb armor trade immediately");
                    PrivateMessageModule.StartTradeWithPlayer(targetPlayer, senderName);
                }
            }
            catch (Exception ex)
            {
                PrivateMessageModule.LogDebug($"[TRADE HANDLER] Error in carb armor command: {ex.Message}");
                UnifiedMessageHandler.SendResponse(senderName, "Error processing carb armor request. Please try again.", MessageType.Error);
            }
        }

        private PlayerChar FindPlayer(string senderName)
        {
            // Try to find by ID first if sender is numeric
            if (uint.TryParse(senderName, out uint playerId))
            {
                return DynelManager.Players.FirstOrDefault(p => p.Identity.Instance == playerId);
            }

            // Try by name
            return DynelManager.Players.FirstOrDefault(p => p.Name.Equals(senderName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
