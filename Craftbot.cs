using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AOSharp.Clientless;
using AOSharp.Clientless.Logging;
using AOSharp.Common.GameData;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;
using SmokeLounge.AOtomation.Messaging.Messages.ChatMessages;
using Craftbot.Modules;

namespace Craftbot
{
    public class Craftbot : ClientlessPluginEntry
    {
        public static Config Config { get; private set; }
        public static string PluginDir { get; private set; }

        public override void Init(string pluginDir)
        {
            try
            {
                // Set the plugin directory for access by modules
                PluginDir = pluginDir;

                // Initialize logging first so we can log everything else
                // Note: Logging is initialized in PrivateMessageModule.Initialize()

                Config = Config.Load($"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}\\AOSharp\\Craftbot\\{Client.CharacterName}\\Config.json");

                // Initialize configuration system first (before other modules)
                Core.ConfigurationManager.Initialize();
                Core.MessageService.Initialize();
                Task.Run(async () => await Core.DynamicCommandHandler.Initialize());
                Core.DynamicRecipeLoader.Initialize();
                Core.ConfigurableHelpSystem.Initialize();

                // Initialize Private Message module (always active) - this also initializes logging
                PrivateMessageModule.Initialize();

                // Log successful initialization start
                PrivateMessageModule.LogInfo($"[CRAFTBOT] Initializing Craftbot plugin in directory: {pluginDir}");
                PrivateMessageModule.LogInfo($"[CRAFTBOT] Character: {Client.CharacterName}");
                PrivateMessageModule.LogInfo($"[CRAFTBOT] Configuration system initialized with hot reload support");

                // Start control panel command processor
                StartControlPanelCommandProcessor();

                // Note: Trade event handling is now done inside PrivateMessageModule.Initialize()
                // to avoid duplicate subscriptions
                PrivateMessageModule.LogDebug("[CRAFTBOT] Trade event handling configured in PrivateMessageModule");

                // Set up network message handling for Private Message trade events
                Client.MessageReceived += Network_N3MessageReceived;
                PrivateMessageModule.LogDebug("[CRAFTBOT] Network message handler registered");

                // Make the bot stand up on startup
                PrivateMessageModule.LogInfo("[CRAFTBOT] 🤖 Making bot stand up...");
                StandUp();
                PrivateMessageModule.LogInfo("[CRAFTBOT] Stand up command sent");

                // Initialize comprehensive inventory tracking
                PrivateMessageModule.LogInfo("[CRAFTBOT] Initializing comprehensive inventory tracking...");
                Core.ItemTracker.Initialize();
                PrivateMessageModule.LogInfo("[CRAFTBOT] Inventory tracking initialized");

                // Network item logger removed for cleaner startup

                // Schedule bag opening and inventory snapshot after character is fully loaded
                PrivateMessageModule.LogInfo("[CRAFTBOT] 🎒 Scheduling bag opening and inventory snapshot...");
                Task.Run(async () =>
                {
                    // Wait for character to be fully loaded first
                    await Task.Delay(3000); // Wait 3 seconds for character to load

                    // Now check and open tool bags
                    PrivateMessageModule.LogInfo("[CRAFTBOT] Opening all bags in inventory...");
                    await CheckAndOpenToolBagsAsync();
                    PrivateMessageModule.LogInfo("[CRAFTBOT] ✅ All bags opened and ready");

                    // Wait for bags to open and contents to load
                    await Task.Delay(3000); // Wait 3 seconds for bags to open
                    Core.ItemTracker.TakeInitialInventorySnapshot();
                    PrivateMessageModule.LogInfo($"[CRAFTBOT] Delayed inventory snapshot complete - {Core.ItemTracker.GetInventoryStats()}");

                    // Capture bot's original inventory AFTER bags are opened
                    await Task.Delay(2000); // Wait additional 2 seconds for bag contents to load
                    PrivateMessageModule.CaptureOriginalBotInventoryAfterBagsOpen();

                    // Startup analysis complete
                    await Task.Delay(2000); // Wait additional 2 seconds for inventory to stabilize
                    PrivateMessageModule.LogInfo("[CRAFTBOT] ✅ Bot startup completed successfully - Ready for trades!");

                    // Mark initialization as complete - bot will now accept commands and trades
                    PrivateMessageModule.MarkInitializationComplete();
                });

                PrivateMessageModule.LogInfo("[CRAFTBOT] Craftbot plugin initialization completed successfully");
                PrivateMessageModule.LogInfo("[CRAFTBOT] Running in event-driven mode - no game update handler needed");

                Logger.Information("=== CRAFTBOT READY ===");
            }
            catch (Exception ex)
            {
                // Log initialization errors
                try
                {
                    PrivateMessageModule.LogError($"[CRAFTBOT] Error during initialization: {ex.Message}");
                    PrivateMessageModule.LogError($"[CRAFTBOT] Stack trace: {ex.StackTrace}");
                }
                catch
                {
                    // If logging fails, we can't do much
                }
            }
        }



        // No settings window - automatic processing only

        // No game update handler needed - event-driven processing only













        private void Network_N3MessageReceived(object s, Message msg)
        {
            try
            {
                // Always handle trade messages for new items detection during return trades
                if (msg.Body is TradeMessage tradeMsg)
                {
                    PrivateMessageModule.LogDebug($"[NETWORK] Received trade message: {tradeMsg.Action}");
                    PrivateMessageModule.HandleTradeMessage(tradeMsg);
                }
            }
            catch (Exception ex)
            {
                PrivateMessageModule.LogError($"[NETWORK] Error handling network message: {ex.Message}");
            }
        }

        // StandUp method implemented using Malis buffbot approach

        /// <summary>
        /// Opens all bags in inventory at startup - just open everything, no checking
        /// </summary>
        private async Task CheckAndOpenToolBagsAsync()
        {
            try
            {
                // Bag opening logic (debug messages hidden for cleaner logs)

                // Wait a bit more if inventory is still empty
                if (Inventory.Items?.Count() == 0)
                {
                    await Task.Delay(2000);
                }

                // Find ALL container items in inventory - don't check if they're open, just open them all
                var allBags = Inventory.Items?.Where(item =>
                    item.UniqueIdentity.Type == IdentityType.Container).ToList() ?? new List<Item>();

                Logger.Information($"[STARTUP] Opening all {allBags.Count} bag(s) in inventory");

                // Open every single bag using clientless-compatible method
                foreach (var bagItem in allBags)
                {
                    Logger.Information($"[STARTUP] Opening bag: {bagItem.Name}");

                    // Use regular Use() method (works in clientless mode based on other code)
                    bagItem.Use();

                    await Task.Delay(1500); // Wait longer for bag to open

                    // Check if bag opened successfully (minimal logging)
                    var openedContainer = Inventory.Containers?.FirstOrDefault(c => c.Identity.Instance == bagItem.UniqueIdentity.Instance);
                    if (openedContainer == null)
                    {
                        // Try again after a longer delay
                        await Task.Delay(1000);
                    }
                }

                Logger.Information("[STARTUP] ✅ All bags opened successfully");
            }
            catch (Exception ex)
            {
                Logger.Error($"[STARTUP] Error opening bags: {ex.Message}");
            }
        }

        /// <summary>
        /// Makes the bot stand up using the same method as Malis buffbot
        /// </summary>
        private async void StandUp()
        {
            try
            {
                // Wait a moment for LocalPlayer to be fully initialized
                await Task.Delay(1000);

                if (DynelManager.LocalPlayer == null)
                {
                    PrivateMessageModule.LogInfo("[CRAFTBOT] LocalPlayer not initialized yet, retrying in 3 seconds...");
                    _ = Task.Delay(3000).ContinueWith(_ => StandUp());
                    return;
                }

                // Use the same method as Malis buffbot for standing up
                PrivateMessageModule.LogInfo("[CRAFTBOT] Attempting to stand up using Malis buffbot method...");

                DynelManager.LocalPlayer.MovementComponent.ChangeMovement(MovementAction.LeaveSit);

                PrivateMessageModule.LogInfo("[CRAFTBOT] ✅ Stand up command sent successfully using MovementComponent.ChangeMovement");
            }
            catch (Exception ex)
            {
                PrivateMessageModule.LogWarning($"[CRAFTBOT] Error during stand up: {ex.Message}");
                PrivateMessageModule.LogInfo("[CRAFTBOT] Will retry stand up in 5 seconds...");
                _ = Task.Delay(5000).ContinueWith(_ => StandUp());
            }
        }

        /// <summary>
        /// Start the control panel command processor
        /// </summary>
        private void StartControlPanelCommandProcessor()
        {
            try
            {
                // Start a background task to periodically check for control panel commands
                Task.Run(async () =>
                {
                    while (true)
                    {
                        try
                        {
                            Core.DynamicCommandHandler.ProcessControlPanelCommands();
                            await Task.Delay(1000); // Check every second
                        }
                        catch (Exception ex)
                        {
                            PrivateMessageModule.LogError($"[CONTROL PANEL] Error in command processor: {ex.Message}");
                            await Task.Delay(5000); // Wait longer on error
                        }
                    }
                });

                PrivateMessageModule.LogInfo("[CRAFTBOT] Control panel command processor started");
            }
            catch (Exception ex)
            {
                PrivateMessageModule.LogError($"[CRAFTBOT] Error starting control panel command processor: {ex.Message}");
            }
        }

        /// <summary>
        /// Cleanup when plugin is unloaded
        /// </summary>
        public override void Teardown()
        {
            try
            {
                PrivateMessageModule.LogInfo("[CRAFTBOT] Starting plugin teardown...");

                // Shutdown configuration system
                Core.ConfigurationManager.Shutdown();
                Core.DynamicRecipeLoader.Shutdown();
                Core.ConfigurableHelpSystem.Shutdown();

                // Cleanup modules
                PrivateMessageModule.Cleanup();
                Client.MessageReceived -= Network_N3MessageReceived;

                PrivateMessageModule.LogInfo("[CRAFTBOT] Plugin teardown completed successfully");
            }
            catch (Exception ex)
            {
                try
                {
                    PrivateMessageModule.LogError($"[CRAFTBOT] Error during teardown: {ex.Message}");
                }
                catch
                {
                    // Silent error handling if logging fails
                }
            }
        }

    }
}
