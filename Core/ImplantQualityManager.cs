using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AOSharp.Clientless;
using AOSharp.Clientless.Logging;

namespace Craftbot.Core
{
    /// <summary>
    /// Manages implant quality level requests from players
    /// Handles parsing commands like "100larm 200rarm 150head" and tracking quality targets
    /// </summary>
    public static class ImplantQualityManager
    {
        // Dictionary to store player implant quality requests
        // Key: PlayerId, Value: Dictionary of slot -> quality level
        private static Dictionary<string, Dictionary<string, int>> _playerQualityRequests = 
            new Dictionary<string, Dictionary<string, int>>();

        // Mapping of command slot names to internal slot names
        private static readonly Dictionary<string, string> SlotNameMapping = new Dictionary<string, string>
        {
            { "larm", "left-arm" },
            { "rarm", "right-arm" },
            { "head", "brain" },
            { "chest", "chest" },
            { "lwrist", "left-wrist" },
            { "rwrist", "right-wrist" },
            { "lhand", "left-hand" },
            { "rhand", "right-hand" },
            { "eye", "eyes" },
            { "ear", "ears" },
            { "waist", "waist" },
            { "legs", "legs" },
            { "feet", "feet" }
        };

        /// <summary>
        /// Parse implant quality command like "100larm 200rarm 150head"
        /// Returns true if parsing was successful
        /// </summary>
        public static bool ParseImplantQualityCommand(string playerId, string command, out string responseMessage)
        {
            responseMessage = "";
            
            try
            {
                // Clear any existing requests for this player
                _playerQualityRequests.Remove(playerId);
                
                // Pattern to match quality+slot combinations like "100larm", "200rarm"
                var pattern = @"(\d+)([a-zA-Z]+)";
                var matches = Regex.Matches(command.ToLower(), pattern);
                
                if (matches.Count == 0)
                {
                    responseMessage = "❌ Invalid format. Use: 100larm 200rarm 150head (quality+slot)";
                    return false;
                }

                var qualityRequests = new Dictionary<string, int>();
                var parsedSlots = new List<string>();

                foreach (Match match in matches)
                {
                    if (match.Groups.Count != 3)
                        continue;

                    // Extract quality level and slot name
                    if (!int.TryParse(match.Groups[1].Value, out int qualityLevel))
                    {
                        responseMessage = $"❌ Invalid quality level: {match.Groups[1].Value}";
                        return false;
                    }

                    string slotCommand = match.Groups[2].Value.ToLower();
                    
                    // Validate quality level range (1-300 for AO)
                    if (qualityLevel < 1 || qualityLevel > 300)
                    {
                        responseMessage = $"❌ Quality level {qualityLevel} out of range (1-300)";
                        return false;
                    }

                    // Map command slot name to internal slot name
                    if (!SlotNameMapping.TryGetValue(slotCommand, out string internalSlotName))
                    {
                        responseMessage = $"❌ Unknown implant slot: {slotCommand}. Valid slots: {string.Join(", ", SlotNameMapping.Keys)}";
                        return false;
                    }

                    // Check for duplicate slots
                    if (qualityRequests.ContainsKey(internalSlotName))
                    {
                        responseMessage = $"❌ Duplicate slot specified: {slotCommand}";
                        return false;
                    }

                    qualityRequests[internalSlotName] = qualityLevel;
                    parsedSlots.Add($"{qualityLevel}{slotCommand}");
                }

                // Store the quality requests for this player
                _playerQualityRequests[playerId] = qualityRequests;

                // Generate success message
                responseMessage = $"✅ Implant quality targets set: {string.Join(", ", parsedSlots)}. Now trade me your implant materials!";

                Logger.Debug($"[IMPLANT QUALITY] Player {playerId} set quality targets: {string.Join(", ", qualityRequests.Select(kv => $"{kv.Value}{kv.Key}"))}");

                return true;
            }
            catch (Exception ex)
            {
                responseMessage = $"❌ Error parsing implant command: {ex.Message}";
                Logger.Debug($"[IMPLANT QUALITY] Error parsing command for player {playerId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get the target quality level for a specific implant slot for a player
        /// Returns null if no target is set for that slot
        /// </summary>
        public static int? GetTargetQuality(string playerId, string slotName)
        {
            Logger.Debug($"[IMPLANT QUALITY] GetTargetQuality called: playerId='{playerId}', slotName='{slotName}'");
            Logger.Debug($"[IMPLANT QUALITY] Available players: {string.Join(", ", _playerQualityRequests.Keys)}");

            if (!_playerQualityRequests.TryGetValue(playerId, out var playerRequests))
            {
                Logger.Debug($"[IMPLANT QUALITY] No quality requests found for player '{playerId}'");
                return null;
            }

            Logger.Debug($"[IMPLANT QUALITY] Player '{playerId}' has slots: {string.Join(", ", playerRequests.Keys)}");

            if (!playerRequests.TryGetValue(slotName, out int qualityLevel))
            {
                Logger.Debug($"[IMPLANT QUALITY] No quality target found for slot '{slotName}' for player '{playerId}'");
                return null;
            }

            Logger.Debug($"[IMPLANT QUALITY] Found quality target: QL{qualityLevel} for slot '{slotName}' for player '{playerId}'");
            return qualityLevel;
        }

        /// <summary>
        /// Check if a player has any quality targets set
        /// </summary>
        public static bool HasQualityTargets(string playerId)
        {
            return _playerQualityRequests.ContainsKey(playerId) && _playerQualityRequests[playerId].Count > 0;
        }

        /// <summary>
        /// Get all quality targets for a player
        /// </summary>
        public static Dictionary<string, int> GetAllTargets(string playerId)
        {
            if (!_playerQualityRequests.TryGetValue(playerId, out var playerRequests))
                return new Dictionary<string, int>();

            return new Dictionary<string, int>(playerRequests);
        }

        /// <summary>
        /// Clear quality targets for a player (called after trade completion)
        /// </summary>
        public static void ClearTargets(string playerId)
        {
            _playerQualityRequests.Remove(playerId);
            Logger.Debug($"[IMPLANT QUALITY] Cleared quality targets for player {playerId}");
        }

        /// <summary>
        /// Get a summary of current targets for a player
        /// </summary>
        public static string GetTargetSummary(string playerId)
        {
            if (!_playerQualityRequests.TryGetValue(playerId, out var playerRequests) || playerRequests.Count == 0)
                return "No implant quality targets set.";

            var targets = playerRequests.Select(kv => $"{kv.Value}{SlotNameMapping.FirstOrDefault(x => x.Value == kv.Key).Key}");
            return $"Current targets: {string.Join(", ", targets)}";
        }

        /// <summary>
        /// Get help text for the implant quality command
        /// </summary>
        public static string GetHelpText()
        {
            return "📋 Implant Quality Command Help:\n" +
                   "Format: 100larm 200rarm 150head\n" +
                   "Available slots: " + string.Join(", ", SlotNameMapping.Keys) + "\n" +
                   "Quality range: 1-300\n" +
                   "Example: '200larm 250rarm 180head' sets left arm to QL200, right arm to QL250, brain to QL180";
        }
    }
}
