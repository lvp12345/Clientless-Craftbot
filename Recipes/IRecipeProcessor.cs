using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AOSharp.Clientless;

namespace Craftbot.Recipes
{
    /// <summary>
    /// Interface for all recipe processors
    /// </summary>
    public interface IRecipeProcessor
    {
        /// <summary>
        /// Gets the name of this recipe processor
        /// </summary>
        string RecipeName { get; }

        /// <summary>
        /// Indicates whether this recipe should use single-item processing (one item at a time)
        /// vs batch processing (all items at once). Single-item processing is better for simple
        /// recipes to prevent inventory overflow and provide better progress feedback.
        /// </summary>
        bool UsesSingleItemProcessing { get; }

        /// <summary>
        /// Determines if this processor can handle the given item
        /// </summary>
        /// <param name="item">Item to check</param>
        /// <returns>True if this processor can handle the item</returns>
        bool CanProcess(Item item);

        /// <summary>
        /// Processes the given item using this recipe
        /// </summary>
        /// <param name="item">Item to process</param>
        /// <param name="targetContainer">Container to return processed items to</param>
        /// <returns>Task representing the async operation</returns>
        Task ProcessItem(Item item, Container targetContainer);

        /// <summary>
        /// Analyzes a collection of items to determine if they can be processed as a recipe
        /// </summary>
        /// <param name="items">Items to analyze</param>
        /// <returns>Recipe analysis result</returns>
        RecipeAnalysisResult AnalyzeItems(List<Item> items);
    }

    /// <summary>
    /// Result of recipe analysis
    /// </summary>
    public class RecipeAnalysisResult
    {
        public bool CanProcess { get; set; }
        public string Stage { get; set; }
        public int ProcessableItemCount { get; set; }
        public string Description { get; set; }
    }
}
