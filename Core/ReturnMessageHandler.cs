using System;
using Craftbot.Modules;

namespace Craftbot.Core
{
    /// <summary>
    /// Handles return/recovery commands in the unified message system
    /// </summary>
    public class ReturnMessageHandler : IMessageHandler
    {
        public void HandleMessage(string senderName, MessageInfo messageInfo)
        {
            try
            {
                PrivateMessageModule.LogDebug($"[RETURN HANDLER] Processing return request from {senderName}");

                // Use existing return command logic from PrivateMessageModule
                PrivateMessageModule.HandleReturnCommand(senderName, messageInfo.Arguments);
            }
            catch (Exception ex)
            {
                PrivateMessageModule.LogDebug($"[RETURN HANDLER] Error: {ex.Message}");
                UnifiedMessageHandler.SendResponse(senderName, "Error processing return request. Please try again.", MessageType.Error);
            }
        }
    }
}
