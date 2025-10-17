using System;
using System.Collections.Generic;

namespace Craftbot.Core
{
    /// <summary>
    /// Trade logging functionality for detailed trade session tracking
    /// This class provides the interface that PrivateMessageModule expects
    /// </summary>
    public static class TradeLogger
    {
        private static Dictionary<int, TradeSession> _activeSessions = new Dictionary<int, TradeSession>();

        /// <summary>
        /// Start a trade session for detailed logging
        /// </summary>
        /// <param name="playerId">Player ID</param>
        /// <param name="playerName">Player name</param>
        public static void StartTradeSession(int playerId, string playerName)
        {
            try
            {
                var session = new TradeSession
                {
                    PlayerId = playerId,
                    PlayerName = playerName,
                    StartTime = DateTime.Now
                };

                _activeSessions[playerId] = session;
                
                // Log the session start
                Modules.PrivateMessageModule.LogDebug($"[TRADE LOGGER] Started trade session for {playerName} (ID: {playerId})");
            }
            catch (Exception ex)
            {
                Modules.PrivateMessageModule.LogDebug($"[TRADE LOGGER] Error starting trade session: {ex.Message}");
            }
        }

        /// <summary>
        /// Complete a trade session
        /// </summary>
        /// <param name="playerId">Player ID</param>
        public static void CompleteTradeSession(int playerId)
        {
            try
            {
                if (_activeSessions.ContainsKey(playerId))
                {
                    var session = _activeSessions[playerId];
                    session.EndTime = DateTime.Now;
                    
                    // Log the session completion
                    var duration = (session.EndTime.Value - session.StartTime).TotalSeconds;
                    Modules.PrivateMessageModule.LogDebug($"[TRADE LOGGER] Completed trade session for {session.PlayerName} (Duration: {duration:F1}s)");
                    
                    // Remove the session
                    _activeSessions.Remove(playerId);
                }
                else
                {
                    Modules.PrivateMessageModule.LogDebug($"[TRADE LOGGER] No active session found for player {playerId}");
                }
            }
            catch (Exception ex)
            {
                Modules.PrivateMessageModule.LogDebug($"[TRADE LOGGER] Error completing trade session: {ex.Message}");
            }
        }

        /// <summary>
        /// Get active trade session for a player
        /// </summary>
        /// <param name="playerId">Player ID</param>
        /// <returns>Trade session or null if not found</returns>
        public static TradeSession GetSession(int playerId)
        {
            return _activeSessions.ContainsKey(playerId) ? _activeSessions[playerId] : null;
        }

        /// <summary>
        /// Clear all active sessions (cleanup)
        /// </summary>
        public static void ClearAllSessions()
        {
            _activeSessions.Clear();
            Modules.PrivateMessageModule.LogDebug("[TRADE LOGGER] Cleared all active trade sessions");
        }
    }

    /// <summary>
    /// Represents an active trade session
    /// </summary>
    public class TradeSession
    {
        public int PlayerId { get; set; }
        public string PlayerName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public List<string> ItemsReceived { get; set; } = new List<string>();
        public List<string> ItemsReturned { get; set; } = new List<string>();

        public double DurationSeconds => EndTime.HasValue ? (EndTime.Value - StartTime).TotalSeconds : 0;
    }
}
