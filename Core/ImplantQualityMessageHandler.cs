using System;
using System.Linq;
using System.Text.RegularExpressions;
using Craftbot.Modules;

namespace Craftbot.Core
{
    /// <summary>
    /// Handles implant quality level commands like "100larm 200rarm 150head"
    /// </summary>
    public class ImplantQualityMessageHandler : IMessageHandler
    {
        public void HandleMessage(string senderName, MessageInfo messageInfo)
        {
            try
            {
                // Reconstruct the full message for parsing
                string fullMessage = string.Join(" ", messageInfo.Arguments);
                
                // If no arguments, show help
                if (string.IsNullOrEmpty(fullMessage))
                {
                    UnifiedMessageHandler.SendResponse(senderName, ImplantQualityManager.GetHelpText());
                    return;
                }

                // Handle special sub-commands
                if (fullMessage.ToLower() == "help")
                {
                    UnifiedMessageHandler.SendResponse(senderName, ImplantQualityManager.GetHelpText());
                    return;
                }

                if (fullMessage.ToLower() == "status")
                {
                    string status = ImplantQualityManager.GetTargetSummary(senderName);
                    UnifiedMessageHandler.SendResponse(senderName, status);
                    return;
                }

                if (fullMessage.ToLower() == "clear")
                {
                    ImplantQualityManager.ClearTargets(senderName);
                    UnifiedMessageHandler.SendResponse(senderName, "✅ Implant quality targets cleared.");
                    return;
                }

                // Parse the quality level command
                if (ImplantQualityManager.ParseImplantQualityCommand(senderName, fullMessage, out string responseMessage))
                {
                    UnifiedMessageHandler.SendResponse(senderName, responseMessage, MessageType.Success);

                    // AUTO-OPEN TRADE: Automatically open trade with the player for implant processing
                    PrivateMessageModule.LogDebug($"[IMPLANT QUALITY] Auto-opening trade with {senderName} for implant processing");

                    // Mark this as a special implant trade that should bypass normal processing
                    ImplantTradeManager.SetPendingImplantTrade(senderName);

                    // Open trade with the player
                    UnifiedMessageHandler.SendResponse(senderName, "🔄 Opening trade window for implant processing...");

                    // Use a small delay to ensure the response is sent before opening trade
                    System.Threading.Tasks.Task.Delay(500).ContinueWith(_ =>
                    {
                        try
                        {
                            // Get player identity for trade opening
                            var player = AOSharp.Clientless.DynelManager.Players.FirstOrDefault(p =>
                                p.Name.Equals(senderName, StringComparison.OrdinalIgnoreCase));

                            if (player != null)
                            {
                                PrivateMessageModule.LogDebug($"[IMPLANT QUALITY] Opening trade with player {senderName} (ID: {player.Identity.Instance})");
                                AOSharp.Clientless.Trade.Open(player.Identity);
                            }
                            else
                            {
                                PrivateMessageModule.LogDebug($"[IMPLANT QUALITY] Could not find player {senderName} to open trade");
                                UnifiedMessageHandler.SendResponse(senderName, "❌ Could not open trade. Please try the command again when you're closer.");
                            }
                        }
                        catch (Exception ex)
                        {
                            PrivateMessageModule.LogDebug($"[IMPLANT QUALITY] Error opening trade with {senderName}: {ex.Message}");
                            UnifiedMessageHandler.SendResponse(senderName, "❌ Error opening trade. Please try again.");
                        }
                    });
                }
                else
                {
                    UnifiedMessageHandler.SendResponse(senderName, responseMessage, MessageType.Error);
                }
            }
            catch (Exception ex)
            {
                PrivateMessageModule.LogDebug($"[IMPLANT QUALITY] Error handling message from {senderName}: {ex.Message}");
                UnifiedMessageHandler.SendResponse(senderName, "❌ Error processing implant quality command. Please try again.", MessageType.Error);
            }
        }

        /// <summary>
        /// Check if a message contains implant slot keywords
        /// </summary>
        public static bool IsImplantQualityCommand(string message)
        {
            if (string.IsNullOrEmpty(message))
                return false;

            string lowerMessage = message.ToLower();
            
            // Check for implant slot keywords
            string[] slotKeywords = { "larm", "rarm", "head", "chest", "lwrist", "rwrist", 
                                    "lhand", "rhand", "eye", "ear", "waist", "legs", "feet" };

            foreach (string keyword in slotKeywords)
            {
                if (lowerMessage.Contains(keyword))
                    return true;
            }

            return false;
        }
    }
}
