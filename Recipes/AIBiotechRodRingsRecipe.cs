using System;
using System.Linq;
using System.Threading.Tasks;
using AOSharp.Core;
using AOSharp.Core.Inventory;
using System.Collections.Generic;

namespace Craftbot.Recipes
{
    /// <summary>
    /// Handles AI Biotech Rod Rings processing using unified recipe patterns with multistep workflow
    /// Multi-step Recipe:
    /// Step 1: Personal Furnace + Small Gold Ingot = Liquid Gold
    /// Step 2: Wire Drawing Machine + Liquid Gold = Gold Filigree Wire
    /// Step 3: Generic Ring Template + Gold Filigree Wire = Gold Filigree Ring
    /// Step 4: Gold Filigree Ring + Biotech Rod = Biotech Rod Ring (Dark/Glowing/Pulsing variants)
    ///
    /// CRITICAL: This recipe must NOT conflict with VTE processing
    /// </summary>
    public class AIBiotechRodRingsRecipe : BaseRecipeProcessor
    {
        public override string RecipeName => "AI Biotech Rod Rings";

        public enum BiotechRingStage
        {
            None,
            RawMaterials,           // Has Small Gold Ingot + Biotech Rods/Ring Template (start from Step 1)
            Step1_LiquidGold,       // Has Liquid Gold (continue from Step 2)
            Step2_GoldWire,         // Has Gold Filigree Wire (continue from Step 3)
            Step3_GoldRing,         // Has Gold Filigree Ring (continue from Step 4)
            Completed               // Has Biotech Rod Ring (finished)
        }

        private static readonly string[] BiotechRods = {
            "Dark Biotech Rod",
            "Glowing Biotech Rod",
            "Pulsing Biotech Rod"
        };

        public override bool CanProcess(Item item)
        {
            // Check if this item can be part of Biotech Rod Ring processing (raw materials OR partially processed items)
            // CRITICAL: Only process if this is specifically for Biotech Rod Rings, NOT VTE

            // Always accept Biotech Rods and Ring Templates (unique to this recipe)
            if (BiotechRods.Any(rod => item.Name.ToLower().Contains(rod.ToLower())) ||
                item.Name.ToLower().Contains("generic ring template"))
            {
                return true;
            }

            // For shared items (gold-related), ensure we have Biotech Rod context
            if (item.Name.Equals("Small Gold Ingot", StringComparison.OrdinalIgnoreCase) ||
                item.Name.Equals("Liquid Gold", StringComparison.OrdinalIgnoreCase) ||
                item.Name.Equals("Gold Filigree Wire", StringComparison.OrdinalIgnoreCase) ||
                item.Name.Equals("Gold Filigree Ring", StringComparison.OrdinalIgnoreCase))
            {
                bool hasBiotechContext = HasBiotechRodContext();
                RecipeUtilities.LogDebug($"[AI BIOTECH ROD RINGS CHECK] Shared item '{item.Name}' -> Has Biotech context: {hasBiotechContext}");
                return hasBiotechContext;
            }

            return false;
        }

        /// <summary>
        /// Check if this item is shared with VTE recipe and could cause conflicts
        /// </summary>
        private bool IsSharedWithVTE(Item item)
        {
            return item.Name.Contains("Small Gold Ingot", StringComparison.OrdinalIgnoreCase) ||
                   item.Name.Contains("Liquid Gold", StringComparison.OrdinalIgnoreCase) ||
                   item.Name.Contains("Gold Filigree Wire", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Check if we have Biotech Rod context (Biotech Rods or Ring Templates present)
        /// This helps differentiate from VTE processing
        /// </summary>
        private bool HasBiotechRodContext()
        {
            // Check inventory first
            bool hasContext = Inventory.Items.Any(item => 
                BiotechRods.Any(rod => item.Name.Contains(rod, StringComparison.OrdinalIgnoreCase)) ||
                item.Name.Contains("Generic Ring Template", StringComparison.OrdinalIgnoreCase));

            if (hasContext) return true;

            // Check open backpacks
            foreach (var backpack in Inventory.Backpacks.Where(bp => bp.IsOpen))
            {
                hasContext = backpack.Items.Any(item => 
                    BiotechRods.Any(rod => item.Name.Contains(rod, StringComparison.OrdinalIgnoreCase)) ||
                    item.Name.Contains("Generic Ring Template", StringComparison.OrdinalIgnoreCase));
                
                if (hasContext) return true;
            }

            return false;
        }

        protected override async Task ProcessRecipeLogic(Item item, Container targetContainer)
        {
            RecipeUtilities.LogDebug($"[{RecipeName}] Starting Biotech Rod Ring multistep processing for {item.Name}");

            // SAFETY CHECK: Ensure we're not accidentally processing VTE items
            if (!HasBiotechRodContext())
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] No Biotech Rod context found - skipping to avoid VTE conflict");
                return;
            }

            // Detect current stage and process accordingly
            var currentStage = DetectBiotechRingStageFromInventory();
            RecipeUtilities.LogDebug($"[{RecipeName}] Current Biotech Ring stage: {currentStage}");

            // Process based on current stage
            await ProcessBiotechRingStages(currentStage, targetContainer);

            RecipeUtilities.LogDebug($"[{RecipeName}] Completed Biotech Rod Ring processing!");
        }

        private async Task ProcessBiotechRingStages(BiotechRingStage currentStage, Container targetContainer)
        {
            var stage = currentStage;
            int maxRetries = 5; // Prevent infinite loops
            int retryCount = 0;

            while (stage != BiotechRingStage.None && stage != BiotechRingStage.Completed && retryCount < maxRetries)
            {
                retryCount++;
                bool stepSuccessful = false;
                RecipeUtilities.LogDebug($"[{RecipeName}] Processing stage: {stage} (attempt {retryCount})");

                switch (stage)
                {
                    case BiotechRingStage.RawMaterials:
                        RecipeUtilities.LogDebug($"[{RecipeName}] Processing Small Gold Ingot (Step 1)");
                        stepSuccessful = await ProcessGoldIngotsToLiquid();
                        break;

                    case BiotechRingStage.Step1_LiquidGold:
                        RecipeUtilities.LogDebug($"[{RecipeName}] Processing Liquid Gold (Step 2)");
                        stepSuccessful = await ProcessLiquidGoldToWire();
                        break;

                    case BiotechRingStage.Step2_GoldWire:
                        RecipeUtilities.LogDebug($"[{RecipeName}] Processing Gold Filigree Wire (Step 3)");
                        stepSuccessful = await ProcessWireToRing();
                        break;

                    case BiotechRingStage.Step3_GoldRing:
                        RecipeUtilities.LogDebug($"[{RecipeName}] Processing Gold Filigree Ring (Step 4)");
                        stepSuccessful = await ProcessRingWithBiotechRod();
                        break;
                }

                if (stepSuccessful)
                {
                    // Re-detect stage after successful processing
                    await Task.Delay(500); // Allow time for items to update
                    stage = DetectBiotechRingStageFromInventory();
                    RecipeUtilities.LogDebug($"[{RecipeName}] Stage after processing: {stage}");
                }
                else
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step failed, stopping processing");
                    break;
                }
            }

            if (stage == BiotechRingStage.Completed)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Biotech Rod Ring recipe completed successfully!");
            }
            else if (retryCount >= maxRetries)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Max retries reached, stopping processing");
            }
        }

        private BiotechRingStage DetectBiotechRingStageFromInventory()
        {
            RecipeUtilities.LogDebug($"[{RecipeName}] Detecting Biotech Ring stage from inventory");

            // Check for completed Biotech Rod Ring first
            var completedRing = Inventory.Items.FirstOrDefault(item =>
                item.Name.ToLower().Contains("biotech rod ring") ||
                (item.Name.ToLower().Contains("ring") && BiotechRods.Any(rod => item.Name.ToLower().Contains(rod.Split(' ')[0].ToLower()))));
            if (completedRing != null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found completed Biotech Rod Ring - returning Completed stage");
                return BiotechRingStage.Completed;
            }

            // Check for Gold Filigree Ring (Step 4)
            var goldRing = Inventory.Items.FirstOrDefault(item =>
                item.Name.ToLower().Contains("gold filigree ring"));
            if (goldRing != null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found Gold Filigree Ring - returning Step3_GoldRing stage");
                return BiotechRingStage.Step3_GoldRing;
            }

            // Check for Gold Filigree Wire (Step 3)
            var goldWire = Inventory.Items.FirstOrDefault(item =>
                item.Name.ToLower().Contains("gold filigree wire"));
            if (goldWire != null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found Gold Filigree Wire - returning Step2_GoldWire stage");
                return BiotechRingStage.Step2_GoldWire;
            }

            // Check for Liquid Gold (Step 2)
            var liquidGold = Inventory.Items.FirstOrDefault(item =>
                item.Name.ToLower().Contains("liquid gold"));
            if (liquidGold != null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found Liquid Gold - returning Step1_LiquidGold stage");
                return BiotechRingStage.Step1_LiquidGold;
            }

            // Check for Small Gold Ingot (Step 1)
            var goldIngot = Inventory.Items.FirstOrDefault(item =>
                item.Name.Equals("Small Gold Ingot", StringComparison.OrdinalIgnoreCase));
            if (goldIngot != null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found Small Gold Ingot - returning RawMaterials stage");
                return BiotechRingStage.RawMaterials;
            }

            RecipeUtilities.LogDebug($"[{RecipeName}] No Biotech Ring components found - returning None stage");
            return BiotechRingStage.None;
        }

        private async Task<bool> ProcessGoldIngotsToLiquid()
        {
            try
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 1: Processing Small Gold Ingot to Liquid Gold");

                var goldIngots = Inventory.Items.Where(item =>
                    item.Name.Equals("Small Gold Ingot", StringComparison.OrdinalIgnoreCase)).ToList();
                var personalFurnace = FindTool("Personal Furnace");

                if (personalFurnace == null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step 1: No Personal Furnace found");
                    return false;
                }

                if (!goldIngots.Any())
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step 1: No Small Gold Ingot found");
                    return false;
                }

                RecipeUtilities.LogDebug($"[{RecipeName}] Step 1: Found {goldIngots.Count} Small Gold Ingot, converting to Liquid Gold");
                foreach (var ingot in goldIngots)
                {
                    await CombineItems(personalFurnace, ingot);
                }

                // Verify result
                await Task.Delay(500);
                var liquidGold = Inventory.Items.FirstOrDefault(item =>
                    item.Name.ToLower().Contains("liquid gold"));

                bool success = liquidGold != null;
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 1: {(success ? "Success" : "Failed")} - Liquid Gold {(success ? "created" : "not found")}");
                return success;
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 1: Error - {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ProcessLiquidGoldToWire()
        {
            try
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 2: Processing Liquid Gold to Gold Filigree Wire");

                var liquidGolds = Inventory.Items.Where(item =>
                    item.Name.ToLower().Contains("liquid gold")).ToList();
                var wireDrawingMachine = FindTool("Wire Drawing Machine");

                if (wireDrawingMachine == null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step 2: No Wire Drawing Machine found");
                    return false;
                }

                if (!liquidGolds.Any())
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step 2: No Liquid Gold found");
                    return false;
                }

                RecipeUtilities.LogDebug($"[{RecipeName}] Step 2: Found {liquidGolds.Count} Liquid Gold, converting to Gold Filigree Wire");
                foreach (var liquid in liquidGolds)
                {
                    await CombineItems(wireDrawingMachine, liquid);
                }

                // Verify result
                await Task.Delay(500);
                var goldWire = Inventory.Items.FirstOrDefault(item =>
                    item.Name.ToLower().Contains("gold filigree wire"));

                bool success = goldWire != null;
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 2: {(success ? "Success" : "Failed")} - Gold Filigree Wire {(success ? "created" : "not found")}");
                return success;
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 2: Error - {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ProcessWireToRing()
        {
            try
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 3: Processing Gold Filigree Wire to Gold Filigree Ring");

                var goldWires = Inventory.Items.Where(item =>
                    item.Name.ToLower().Contains("gold filigree wire")).ToList();
                var ringTemplate = Inventory.Items.FirstOrDefault(item =>
                    item.Name.ToLower().Contains("generic ring template"));

                if (ringTemplate == null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step 3: No Generic Ring Template found");
                    return false;
                }

                if (!goldWires.Any())
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step 3: No Gold Filigree Wire found");
                    return false;
                }

                RecipeUtilities.LogDebug($"[{RecipeName}] Step 3: Found {goldWires.Count} Gold Filigree Wire, converting to Gold Filigree Ring");
                foreach (var wire in goldWires)
                {
                    await CombineItems(ringTemplate, wire);
                }

                // Verify result
                await Task.Delay(500);
                var goldRing = Inventory.Items.FirstOrDefault(item =>
                    item.Name.ToLower().Contains("gold filigree ring"));

                bool success = goldRing != null;
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 3: {(success ? "Success" : "Failed")} - Gold Filigree Ring {(success ? "created" : "not found")}");
                return success;
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 3: Error - {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ProcessRingWithBiotechRod()
        {
            try
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 4: Processing Gold Filigree Ring with Biotech Rod");

                var goldRings = Inventory.Items.Where(item =>
                    item.Name.ToLower().Contains("gold filigree ring")).ToList();
                var biotechRods = Inventory.Items.Where(item =>
                    BiotechRods.Any(rod => item.Name.ToLower().Contains(rod.ToLower()))).ToList();

                if (!goldRings.Any())
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step 4: No Gold Filigree Ring found");
                    return false;
                }

                if (!biotechRods.Any())
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step 4: No Biotech Rod found");
                    return false;
                }

                RecipeUtilities.LogDebug($"[{RecipeName}] Step 4: Found {goldRings.Count} Gold Filigree Ring and {biotechRods.Count} Biotech Rod, creating Biotech Rod Ring");

                // Process each ring with a biotech rod
                int processed = 0;
                foreach (var ring in goldRings)
                {
                    if (processed < biotechRods.Count)
                    {
                        await CombineItems(ring, biotechRods[processed]);
                        processed++;
                    }
                }

                // Verify result
                await Task.Delay(500);
                var biotechRodRing = Inventory.Items.FirstOrDefault(item =>
                    item.Name.ToLower().Contains("biotech rod ring") ||
                    (item.Name.ToLower().Contains("ring") && BiotechRods.Any(rod => item.Name.ToLower().Contains(rod.Split(' ')[0].ToLower()))));

                bool success = biotechRodRing != null;
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 4: {(success ? "Success" : "Failed")} - Biotech Rod Ring {(success ? "created" : "not found")}");
                return success;
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 4: Error - {ex.Message}");
                return false;
            }
        }

        public override RecipeAnalysisResult AnalyzeItems(List<Item> items)
        {
            // Create analysis based on detected stage
            var stage = DetectBiotechRingStageFromItems(items);

            return new RecipeAnalysisResult
            {
                CanProcess = stage != BiotechRingStage.None,
                ProcessableItemCount = items.Where(CanProcess).Count(),
                Stage = stage.ToString(),
                Description = $"Biotech Ring Stage: {stage} - {items.Where(CanProcess).Count()} Biotech Ring components found"
            };
        }

        private BiotechRingStage DetectBiotechRingStageFromItems(List<Item> items)
        {
            RecipeUtilities.LogDebug($"[{RecipeName}] Checking {items.Count} items for Biotech Ring stage detection");

            // Check for completed Biotech Rod Ring first
            var completedRing = items.FirstOrDefault(item =>
                item.Name.ToLower().Contains("biotech rod ring") ||
                (item.Name.ToLower().Contains("ring") && BiotechRods.Any(rod => item.Name.ToLower().Contains(rod.Split(' ')[0].ToLower()))));
            if (completedRing != null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found completed Biotech Rod Ring - returning Completed stage");
                return BiotechRingStage.Completed;
            }

            // Check for Gold Filigree Ring (Step 4)
            if (items.Any(item => item.Name.ToLower().Contains("gold filigree ring")))
                return BiotechRingStage.Step3_GoldRing;

            // Check for Gold Filigree Wire (Step 3)
            if (items.Any(item => item.Name.ToLower().Contains("gold filigree wire")))
                return BiotechRingStage.Step2_GoldWire;

            // Check for Liquid Gold (Step 2)
            if (items.Any(item => item.Name.ToLower().Contains("liquid gold")))
                return BiotechRingStage.Step1_LiquidGold;

            // Check for Small Gold Ingot (Step 1)
            if (items.Any(item => item.Name.Equals("Small Gold Ingot", StringComparison.OrdinalIgnoreCase)))
                return BiotechRingStage.RawMaterials;

            return BiotechRingStage.None;
        }

        /// <summary>
        /// Check if we have Biotech Rod context to differentiate from VTE processing
        /// </summary>
        private bool HasBiotechRodContext()
        {
            // Check if we have any Biotech Rods or Ring Templates in inventory
            return Inventory.Items.Any(item =>
                BiotechRods.Any(rod => item.Name.ToLower().Contains(rod.ToLower())) ||
                item.Name.ToLower().Contains("generic ring template"));
        }
    }
}
