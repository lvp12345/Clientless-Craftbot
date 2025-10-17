using System;
using Craftbot.Modules;

namespace Craftbot.Core
{
    /// <summary>
    /// Handles status commands to check pending returns and retry counts
    /// </summary>
    public class StatusMessageHandler : IMessageHandler
    {
        public void HandleMessage(string senderName, MessageInfo messageInfo)
        {
            try
            {
                PrivateMessageModule.LogDebug($"[STATUS HANDLER] Processing status request from {senderName}");

                // Use existing status command logic from PrivateMessageModule
                PrivateMessageModule.HandleStatusCommand(senderName, messageInfo.Arguments);
            }
            catch (Exception ex)
            {
                PrivateMessageModule.LogDebug($"[STATUS HANDLER] Error: {ex.Message}");
                UnifiedMessageHandler.SendResponse(senderName, "Error processing status request. Please try again.", MessageType.Error);
            }
        }
    }
}
