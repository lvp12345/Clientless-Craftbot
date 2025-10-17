using System;
using System.Collections.Generic;
using System.Linq;
using AOSharp.Core;
using Craftbot.Modules;

namespace Craftbot.Core
{
    /// <summary>
    /// Central message handler for Craftbot commands
    /// </summary>
    public static class MessageHandler
    {
        private static bool _initialized = false;
        private static Dictionary<string, Action<string, string[]>> _commandHandlers;

        public static void Initialize()
        {
            if (_initialized) return;

            try
            {
                _commandHandlers = new Dictionary<string, Action<string, string[]>>(StringComparer.OrdinalIgnoreCase)
                {
                    { "help", HandleHelpCommand },
                    { "list", HandleListCommand },
                    { "get", HandleGetCommand }
                };

                _initialized = true;
                ItemTracker.LogTransaction("SYSTEM", "MessageHandler initialized");
            }
            catch (Exception ex)
            {
                ItemTracker.LogTransaction("SYSTEM", $"Error initializing MessageHandler: {ex.Message}");
            }
        }

        /// <summary>
        /// Process a command from a player
        /// </summary>
        public static void ProcessCommand(string senderName, string message)
        {
            try
            {
                if (!_initialized) Initialize();

                // Parse the command
                string[] parts = message.Trim().Split(' ');
                if (parts.Length == 0) return;

                string command = parts[0].ToLower();
                string[] args = parts.Skip(1).ToArray();

                // Check if command exists
                if (_commandHandlers.ContainsKey(command))
                {
                    _commandHandlers[command](senderName, args);
                }
                else
                {
                    PrivateMessageModule.SendPrivateMessage(senderName, 
                        "Unknown command. Type 'help' for available commands.");
                }
            }
            catch (Exception ex)
            {
                ItemTracker.LogTransaction("SYSTEM", $"Error processing command from {senderName}: {ex.Message}");
                PrivateMessageModule.SendPrivateMessage(senderName, 
                    "Error processing command. Please try again.");
            }
        }

        private static void HandleHelpCommand(string senderName, string[] args)
        {
            try
            {
                string helpContent = Templates.CraftbotScriptTemplate.HelpWindow();
                PrivateMessageModule.SendPrivateMessage(senderName, helpContent);
                ItemTracker.LogTransaction(senderName, "HELP REQUESTED");
            }
            catch (Exception ex)
            {
                ItemTracker.LogTransaction("SYSTEM", $"Error sending help to {senderName}: {ex.Message}");
                PrivateMessageModule.SendPrivateMessage(senderName, 
                    "Error loading help. Available commands: help, list, get <item>");
            }
        }

        private static void HandleListCommand(string senderName, string[] args)
        {
            try
            {
                var storedItems = ItemTracker.GetStoredItems(false); // Exclude bags
                
                if (!storedItems.Any())
                {
                    PrivateMessageModule.SendPrivateMessage(senderName, "No items currently stored in the bank.");
                    return;
                }

                // Create clickable list
                string listContent = CreateClickableItemList(storedItems);
                PrivateMessageModule.SendPrivateMessage(senderName, listContent);
                
                ItemTracker.LogTransaction(senderName, $"LIST REQUESTED - {storedItems.Count} items");
            }
            catch (Exception ex)
            {
                ItemTracker.LogTransaction("SYSTEM", $"Error sending list to {senderName}: {ex.Message}");
                PrivateMessageModule.SendPrivateMessage(senderName, "Error retrieving item list. Please try again.");
            }
        }

        private static void HandleGetCommand(string senderName, string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    PrivateMessageModule.SendPrivateMessage(senderName, "Usage: get <item name>");
                    return;
                }

                string itemName = string.Join(" ", args);
                var item = ItemTracker.FindItemByName(itemName);
                
                if (item == null)
                {
                    PrivateMessageModule.SendPrivateMessage(senderName, $"Item '{itemName}' not found in storage.");
                    return;
                }

                // Initiate trade with the player
                TradingSystem.InitiateTrade(senderName, item);
                
                ItemTracker.LogTransaction(senderName, $"GET REQUESTED: {item.Name}");
            }
            catch (Exception ex)
            {
                ItemTracker.LogTransaction("SYSTEM", $"Error handling get command for {senderName}: {ex.Message}");
                PrivateMessageModule.SendPrivateMessage(senderName, "Error processing get request. Please try again.");
            }
        }

        private static string CreateClickableItemList(List<StoredItem> items)
        {
            try
            {
                var content = new System.Text.StringBuilder();
                content.AppendLine("<a href=\"text://");
                content.AppendLine("<font color=#00D4FF>Craftbot - Stored Items</font>");
                content.AppendLine("");
                content.AppendLine("<img src=tdb://id:GFX_GUI_FRIENDLIST_SPLITTER>");
                content.AppendLine("");

                foreach (var item in items.Take(20)) // Limit to 20 items to avoid message size issues
                {
                    content.AppendLine($"<a href='chatcmd:///tell {Client.CharacterName} get {item.Name}'>Get</a> - <font color=#00BDBD>{item.Name}</font>");
                }

                if (items.Count > 20)
                {
                    content.AppendLine($"<font color=#FFFF00>... and {items.Count - 20} more items</font>");
                }

                content.AppendLine("");
                content.AppendLine("<img src=tdb://id:GFX_GUI_FRIENDLIST_SPLITTER>");
                content.AppendLine("\">");

                return content.ToString();
            }
            catch (Exception)
            {
                // Fallback to simple text list
                return string.Join("\n", items.Take(10).Select(i => $"- {i.Name}"));
            }
        }
    }
}
