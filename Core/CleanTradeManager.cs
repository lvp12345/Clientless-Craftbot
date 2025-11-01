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
    /// Manages implant cleaning trades
    /// Marks trades as "clean" trades so they route to ImplantCleaningRecipe
    /// </summary>
    public static class CleanTradeManager
    {
        // Track players who have pending clean trades
        private static HashSet<string> _pendingCleanTrades = new HashSet<string>();

        /// <summary>
        /// Mark a player as having a pending clean trade
        /// </summary>
        public static void SetPendingCleanTrade(string playerName)
        {
            _pendingCleanTrades.Add(playerName);
            PrivateMessageModule.LogDebug($"[CLEAN TRADE] Marked {playerName} for implant cleaning");
        }

        /// <summary>
        /// Check if a player has a pending clean trade
        /// </summary>
        public static bool HasPendingCleanTrade(string playerName)
        {
            return _pendingCleanTrades.Contains(playerName);
        }

        /// <summary>
        /// Remove a player from pending clean trades
        /// </summary>
        public static void ClearPendingCleanTrade(string playerName)
        {
            _pendingCleanTrades.Remove(playerName);
            PrivateMessageModule.LogDebug($"[CLEAN TRADE] Cleared pending clean trade for {playerName}");
        }
    }
}

