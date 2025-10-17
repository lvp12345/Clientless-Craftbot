using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AOSharp.Clientless;
using Craftbot.Modules;
using Craftbot.Recipes;

namespace Craftbot.Core
{
    /// <summary>
    /// Unified message handling system that routes all player communications through a single entry point
    /// Eliminates duplicate messages and provides consistent response handling
    /// </summary>
    public static class UnifiedMessageHandler
    {
        private static Dictionary<string, IMessageHandler> _handlers;
        private static bool _initialized = false;

        /// <summary>
        /// Initialize the unified message system
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            _handlers = new Dictionary<string, IMessageHandler>(StringComparer.OrdinalIgnoreCase)
            {
                { "help", new HelpMessageHandler() },
                { "trade", new TradeMessageHandler() },
                { "carb", new TradeMessageHandler() }, // Carb armor commands also use TradeMessageHandler
                { "return", new ReturnMessageHandler() },
                { "implant", new ImplantQualityMessageHandler() },
                { "status", new StatusMessageHandler() }
            };

            _initialized = true;
        }

        /// <summary>
        /// UNIFIED ENTRY POINT: Process all incoming tell messages
        /// </summary>
        /// <param name="senderName">Player name or ID</param>
        /// <param name="message">Message content</param>
        public static async void ProcessMessage(string senderName, string message)
        {
            try
            {
                if (!_initialized)
                {
                    Initialize();
                }

                // First try the dynamic command handler for configurable commands
                PrivateMessageModule.LogDebug($"[UNIFIED MSG] Checking dynamic handler for: '{message}'");
                var commandResponse = await Core.DynamicCommandHandler.ProcessCommand(senderName, message);
                PrivateMessageModule.LogDebug($"[UNIFIED MSG] Dynamic handler response: '{commandResponse ?? "NULL"}'");
                if (!string.IsNullOrEmpty(commandResponse))
                {
                    PrivateMessageModule.LogDebug($"[UNIFIED MSG] Sending dynamic response to {senderName}");
                    PrivateMessageModule.SendPrivateMessage(senderName, commandResponse);
                    return;
                }

                // Parse the message to determine type and extract arguments for legacy handlers
                var messageInfo = ParseMessage(message);

                // Route to appropriate legacy handler
                PrivateMessageModule.LogDebug($"[UNIFIED MSG] Checking legacy handlers for command: '{messageInfo.Command}'");
                if (_handlers.ContainsKey(messageInfo.Command))
                {
                    PrivateMessageModule.LogDebug($"[UNIFIED MSG] Found legacy handler for: '{messageInfo.Command}'");
                    var handler = _handlers[messageInfo.Command];
                    handler.HandleMessage(senderName, messageInfo);
                }
                else if (messageInfo.Command == "debug")
                {
                    // Handle debug commands directly
                    PrivateMessageModule.LogDebug($"[UNIFIED MSG] Handling debug command from {senderName}");
                    PrivateMessageModule.HandleDebugCommand(senderName, messageInfo.Arguments);
                }
                else if (messageInfo.Command == "ai")
                {
                    // Handle alien armor log command (admin only)
                    PrivateMessageModule.LogDebug($"[UNIFIED MSG] Handling ai command from {senderName}");
                    PrivateMessageModule.HandleAlienArmorLogCommand(senderName, messageInfo.Arguments);
                }
                else
                {
                    PrivateMessageModule.LogDebug($"[UNIFIED MSG] No legacy handler found for: '{messageInfo.Command}'");

                    // Unknown command - check if it might be a recipe-specific help request
                    var recipeConfig = Core.ConfigurationManager.GetConfiguration<Core.RecipeConfiguration>("recipes");
                    if (recipeConfig?.Recipes != null)
                    {
                        var recipe = recipeConfig.Recipes.FirstOrDefault(r =>
                            r.Enabled && r.Name.Equals(messageInfo.Command, StringComparison.OrdinalIgnoreCase));

                        if (recipe != null)
                        {
                            PrivateMessageModule.SendPrivateMessage(senderName, recipe.HelpText);
                            return;
                        }
                    }

                    // Truly unknown command - silently ignore (following existing pattern)
                    PrivateMessageModule.LogDebug($"[UNIFIED MSG] Unknown command '{messageInfo.Command}' from {senderName}");
                }
            }
            catch (Exception ex)
            {
                PrivateMessageModule.LogDebug($"[UNIFIED MSG] Error processing message from {senderName}: {ex.Message}");
                PrivateMessageModule.SendPrivateMessage(senderName, Core.MessageService.GetGeneralErrorMessage());
            }
        }

        /// <summary>
        /// Parse incoming message to determine command type and arguments
        /// </summary>
        private static MessageInfo ParseMessage(string message)
        {
            var parts = message.Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            
            if (parts.Length == 0)
            {
                return new MessageInfo { Command = "unknown", Arguments = new string[0] };
            }

            string command = parts[0].ToLower();
            string[] arguments = parts.Skip(1).ToArray();

            // Special handling for carb armor commands
            if (command == "trade" && arguments.Length > 0 && arguments[0].Equals("carb", StringComparison.OrdinalIgnoreCase))
            {
                return new MessageInfo
                {
                    Command = "carb",
                    Arguments = arguments.Skip(1).ToArray(),
                    OriginalCommand = "trade"
                };
            }

            // Special handling for implant quality commands (detect by content, not just command)
            if (ImplantQualityMessageHandler.IsImplantQualityCommand(message))
            {
                return new MessageInfo
                {
                    Command = "implant",
                    Arguments = parts // Include all parts for parsing
                };
            }

            return new MessageInfo
            {
                Command = command,
                Arguments = arguments
            };
        }

        /// <summary>
        /// UNIFIED RESPONSE: Send messages to players with consistent formatting
        /// </summary>
        public static void SendResponse(string targetName, string message, MessageType messageType = MessageType.Standard)
        {
            try
            {
                // Apply consistent formatting based on message type
                string formattedMessage = FormatMessage(message, messageType);
                
                // Send through existing infrastructure
                PrivateMessageModule.SendPrivateMessage(targetName, formattedMessage);
            }
            catch (Exception ex)
            {
                PrivateMessageModule.LogDebug($"[UNIFIED MSG] Error sending response to {targetName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Format messages consistently based on type
        /// </summary>
        private static string FormatMessage(string message, MessageType messageType)
        {
            switch (messageType)
            {
                case MessageType.Error:
                    return $"Error: {message}";
                case MessageType.Success:
                    return message; // Success messages are already formatted
                case MessageType.Info:
                    return message;
                case MessageType.Standard:
                default:
                    return message;
            }
        }
    }

    /// <summary>
    /// Message information parsed from incoming tell
    /// </summary>
    public class MessageInfo
    {
        public string Command { get; set; }
        public string[] Arguments { get; set; }
        public string OriginalCommand { get; set; }
    }

    /// <summary>
    /// Message type for consistent formatting
    /// </summary>
    public enum MessageType
    {
        Standard,
        Info,
        Success,
        Error
    }

    /// <summary>
    /// Interface for message handlers
    /// </summary>
    public interface IMessageHandler
    {
        void HandleMessage(string senderName, MessageInfo messageInfo);
    }
}
