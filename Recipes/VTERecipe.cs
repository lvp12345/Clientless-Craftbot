using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AOSharp.Clientless;
using SmokeLounge.AOtomation.Messaging.GameData;
using AOSharp.Common.GameData;

namespace Craftbot.Recipes
{
    /// <summary>
    /// Handles VTE (Virral Triumvirate Egg) recipe processing - a complex 9-step recipe
    /// </summary>
    public class VTERecipe : BaseRecipeProcessor
    {
        public override string RecipeName => "VTE";

        public enum VTEStage
        {
            None,
            RawMaterials,           // Raw components (Soul Fragment, Robot Junk, Small Gold Ingot, Mantis Egg)
            Step1_LiquidGold,       // Has Liquid Gold (continue from Step 2)
            Step2_GoldWire,         // Has Gold Filigree Wire (continue from Step 3)
            Step3_CutFragments,     // Has Perfectly Cut Soul Fragment (continue from Step 4)
            Step4_NanoSensor,       // Has Nano Sensor (continue from Step 5)
            Step5_InterfacedSensor, // Has Interfaced Nano Sensor (continue from Step 6)
            Step6_PetrifiedEgg,     // Has Petrified Mantis Egg (continue from Step 7)
            Step7_PartiallyWired,   // Has Partially Wired Mantis Egg (continue wiring)
            Step7_FullyWired,       // Has Fully Wired Mantis Egg (continue from Step 8)
            Step8_IncompleteEgg,    // Has Incomplete Virral Egg (continue from Step 9)
            Step9_OneGem,           // Has Virral Egg with Gem (continue gem addition)
            Step9_TwoGems,          // Has Virral Egg with Dual Gems (continue gem addition)
            Completed               // Has Virral Triumvirate Egg (already done)
        }

        public override bool CanProcess(Item item)
        {
            // Check if this item can be part of VTE processing (raw materials OR partially processed items)
            return item.Name.Equals("Soul Fragment", StringComparison.OrdinalIgnoreCase) ||
                   item.Name.Equals("Perfectly Cut Soul Fragment", StringComparison.OrdinalIgnoreCase) ||
                   item.Name.Equals("Robot Junk", StringComparison.OrdinalIgnoreCase) ||
                   item.Name.Equals("Nano Sensor", StringComparison.OrdinalIgnoreCase) ||
                   item.Name.Equals("Interfaced Nano Sensor", StringComparison.OrdinalIgnoreCase) ||
                   item.Name.Equals("Small Gold Ingot", StringComparison.OrdinalIgnoreCase) ||
                   item.Name.Equals("Liquid Gold", StringComparison.OrdinalIgnoreCase) ||
                   item.Name.Equals("Gold Filigree Wire", StringComparison.OrdinalIgnoreCase) ||
                   item.Name.Equals("Incomplete Virral Egg", StringComparison.OrdinalIgnoreCase) ||
                   item.Name.Equals("Mantis Egg", StringComparison.OrdinalIgnoreCase) ||
                   item.Name.Equals("Petrified Mantis Egg", StringComparison.OrdinalIgnoreCase) ||
                   item.Name.Contains("Partially Wired Mantis Egg") ||
                   item.Name.Equals("Fully Wired Mantis Egg", StringComparison.OrdinalIgnoreCase) ||
                   item.Name.Equals("Incomplete Virral Egg", StringComparison.OrdinalIgnoreCase) ||
                   item.Name.Equals("Virral Egg with Gem", StringComparison.OrdinalIgnoreCase) ||
                   item.Name.Equals("Virral Egg with Dual Gems", StringComparison.OrdinalIgnoreCase) ||
                   item.Name.Equals("Virral Triumvirate Egg", StringComparison.OrdinalIgnoreCase);
        }

        protected override async Task ProcessRecipeLogic(Item item, Container targetContainer)
        {
            RecipeUtilities.LogCritical($"🔧 Starting VTE processing for {item.Name}");

            // Move VTE components to inventory for processing (VTE needs all components moved)
            if (targetContainer != null)
            {
                await MoveComponentsToInventoryShared(targetContainer);
            }

            // Detect current stage and process accordingly
            var currentStage = DetectVTEStage(targetContainer);
            RecipeUtilities.LogDebug($"[{RecipeName}] Current VTE stage after moving components: {currentStage}");
            RecipeUtilities.LogCritical($"📍 VTE Stage: {currentStage}");

            // Process each step in sequence based on current stage
            await ProcessVTEStages(currentStage, targetContainer);

            RecipeUtilities.LogDebug($"[{RecipeName}] Completed VTE creation process!");
            RecipeUtilities.LogCritical($"🎉 VTE processing completed!");
        }

        public override RecipeAnalysisResult AnalyzeItems(List<Item> items)
        {
            // Create a temporary container-like container for analysis
            var stage = DetectVTEStageFromItems(items);

            return new RecipeAnalysisResult
            {
                CanProcess = stage != VTEStage.None,
                ProcessableItemCount = items.Where(CanProcess).Count(),
                Stage = stage.ToString(),
                Description = $"VTE Stage: {stage} - {items.Where(CanProcess).Count()} VTE components found"
            };
        }

        private async Task ProcessVTEStages(VTEStage currentStage, Container targetContainer)
        {
            var stage = currentStage;
            int maxRetries = 10; // Prevent infinite loops
            int retryCount = 0;

            while (stage != VTEStage.Completed && stage != VTEStage.None && retryCount < maxRetries)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Processing VTE stage: {stage} (attempt {retryCount + 1})");
                RecipeUtilities.LogCritical($"⚡ VTE Stage {retryCount + 1}: {stage}");

                var previousStage = stage;
                bool stepSuccessful = false;

                switch (stage)
                {
                    case VTEStage.RawMaterials:
                    case VTEStage.Step1_LiquidGold:
                    case VTEStage.Step2_GoldWire:
                        RecipeUtilities.LogDebug($"[{RecipeName}] Processing gold components (Steps 1-2)");
                        stepSuccessful = await ProcessGoldIngotsToWire(targetContainer);
                        break;

                    case VTEStage.Step3_CutFragments:
                        RecipeUtilities.LogDebug($"[{RecipeName}] Processing soul fragments (Step 3)");
                        stepSuccessful = await ProcessSoulFragments(targetContainer);
                        break;

                    case VTEStage.Step4_NanoSensor:
                    case VTEStage.Step5_InterfacedSensor:
                        RecipeUtilities.LogDebug($"[{RecipeName}] Processing robot junk to sensor (Steps 4-5)");
                        stepSuccessful = await ProcessRobotJunkToSensor(targetContainer);
                        break;

                    case VTEStage.Step6_PetrifiedEgg:
                        RecipeUtilities.LogDebug($"[{RecipeName}] Processing mantis egg (Step 6)");
                        var mantisEgg = Inventory.Items.FirstOrDefault(invItem => invItem.Name.Equals("Mantis Egg", StringComparison.OrdinalIgnoreCase));
                        if (mantisEgg != null)
                        {
                            stepSuccessful = await ProcessMantisEgg(mantisEgg, targetContainer);
                        }
                        else
                        {
                            RecipeUtilities.LogDebug($"[{RecipeName}] No Mantis Egg found in inventory for Step 6");
                            stepSuccessful = false;
                        }
                        break;

                    case VTEStage.Step7_PartiallyWired:
                    case VTEStage.Step7_FullyWired:
                        RecipeUtilities.LogDebug($"[{RecipeName}] Wiring mantis egg (Step 7)");
                        stepSuccessful = await WireMantisEgg(targetContainer);
                        break;

                    case VTEStage.Step8_IncompleteEgg:
                        RecipeUtilities.LogDebug($"[{RecipeName}] Adding sensor to egg (Step 8)");
                        stepSuccessful = await AddSensorToEgg(targetContainer);
                        break;

                    case VTEStage.Step9_OneGem:
                    case VTEStage.Step9_TwoGems:
                        RecipeUtilities.LogDebug($"[{RecipeName}] Adding gems to create final VTE (Step 9)");
                        stepSuccessful = await AddGemsToEgg(targetContainer);
                        break;

                    case VTEStage.Completed:
                        RecipeUtilities.LogDebug($"[{RecipeName}] VTE already completed!");
                        RecipeUtilities.LogCritical($"✅ VTE already completed!");
                        return;

                    default:
                        RecipeUtilities.LogDebug($"[{RecipeName}] Unknown VTE stage: {stage}");
                        RecipeUtilities.LogCritical($"❌ Unknown VTE stage: {stage}");
                        return;
                }

                // Processing delay handled by unified CombineItems method

                // Re-detect stage to see if we made progress
                var newStage = DetectVTEStage(targetContainer);
                RecipeUtilities.LogDebug($"[{RecipeName}] Stage after processing: {previousStage} -> {newStage} (Success: {stepSuccessful})");

                if (newStage == VTEStage.Completed)
                {
                    RecipeUtilities.LogCritical($"🎉 VTE completed successfully!");
                    return;
                }

                if (newStage != previousStage)
                {
                    // Progress made, continue with new stage
                    stage = newStage;
                    retryCount = 0; // Reset retry count on progress
                    RecipeUtilities.LogCritical($"✅ Progress made: {previousStage} -> {newStage}");
                }
                else if (stepSuccessful)
                {
                    // Step reported success but stage didn't change - might need more time
                    retryCount++;
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step successful but stage unchanged, retry {retryCount}");
                }
                else
                {
                    // No progress and step failed
                    retryCount++;
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step failed, retry {retryCount}");
                    RecipeUtilities.LogCritical($"⚠️ Step failed: {stage}, retrying...");
                }
            }

            if (retryCount >= maxRetries)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] VTE processing failed after {maxRetries} attempts");
                RecipeUtilities.LogCritical($"❌ VTE processing failed after {maxRetries} attempts at stage: {stage}");
            }
        }

        private async Task MoveRequiredComponentsForStage(VTEStage stage, Container targetContainer)
        {
            try
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Moving required components for stage: {stage}");

                // Only move the specific components needed for this stage to avoid inventory overflow
                switch (stage)
                {
                    case VTEStage.RawMaterials:
                    case VTEStage.Step1_LiquidGold:
                    case VTEStage.Step2_GoldWire:
                        // Need gold ingots and personal furnaces for gold processing
                        await MoveSpecificItems(targetContainer, new[] { "Small Gold Ingot", "Personal Furnace" });
                        break;

                    case VTEStage.Step3_CutFragments:
                        // Need soul fragments
                        await MoveSpecificItems(targetContainer, new[] { "Soul Fragment" });
                        break;

                    case VTEStage.Step4_NanoSensor:
                    case VTEStage.Step5_InterfacedSensor:
                        // Need robot junk
                        await MoveSpecificItems(targetContainer, new[] { "Robot Junk" });
                        break;

                    case VTEStage.Step6_PetrifiedEgg:
                        // Need mantis egg and cut soul fragment
                        await MoveSpecificItems(targetContainer, new[] { "Mantis Egg", "Perfectly Cut Soul Fragment" });
                        break;

                    case VTEStage.Step7_PartiallyWired:
                    case VTEStage.Step7_FullyWired:
                        // Need gold wire
                        await MoveSpecificItems(targetContainer, new[] { "Gold Filigree Wire" });
                        break;

                    case VTEStage.Step8_IncompleteEgg:
                        // Need interfaced sensor
                        await MoveSpecificItems(targetContainer, new[] { "Interfaced Nano Sensor" });
                        break;

                    case VTEStage.Step9_OneGem:
                    case VTEStage.Step9_TwoGems:
                        // Need gems
                        await MoveSpecificItems(targetContainer, new[] { "Gem" });
                        break;
                }

                RecipeUtilities.LogDebug($"[{RecipeName}] Completed moving components for stage: {stage}");
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error moving components for stage: {ex.Message}");
            }
        }

        private async Task MoveSpecificItems(Container targetContainer, string[] itemNames)
        {
            // Collect all items to move first
            var allItemsToMove = new List<Item>();
            foreach (var itemName in itemNames)
            {
                var itemsToMove = targetContainer.Items.Where(item =>
                    item.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase)).ToList();
                allItemsToMove.AddRange(itemsToMove);
            }

            // OVERFLOW PROTECTION: Check if moving all items is safe
            if (!Modules.PrivateMessageModule.CheckInventorySpaceForProcessing(allItemsToMove.Count, RecipeName))
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] ❌ OVERFLOW PROTECTION: Cannot move {allItemsToMove.Count} VTE items - insufficient inventory space");
                return; // Abort moving to prevent overflow
            }

            // Move all items
            foreach (var itemToMove in allItemsToMove)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Moving {itemToMove.Name} to inventory");
                itemToMove.MoveToInventory();
                await Task.Delay(100); // Movement delay
            }
        }

        private VTEStage DetectVTEStage(Container targetContainer)
        {
            // Check BOTH bag and inventory for VTE components - PROPERLY IGNORE EQUIPPED ITEMS
            var bagItems = targetContainer.Items.ToList();
            var inventoryItems = Inventory.Items.Where(item =>
                item.Slot.Type == IdentityType.Inventory).ToList(); // Only inventory items, not equipped
            var allItems = bagItems.Concat(inventoryItems).ToList();

            return DetectVTEStageFromItems(allItems);
        }

        private VTEStage DetectVTEStageFromItems(List<Item> items)
        {
            RecipeUtilities.LogDebug($"[{RecipeName}] Checking {items.Count} items for VTE stage detection");

            // Check for completed VTE first (in inventory or bag, NOT equipped)
            var completedVTE = items.FirstOrDefault(item => item.Name.Equals("Virral Triumvirate Egg", StringComparison.OrdinalIgnoreCase));
            if (completedVTE != null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found completed VTE - returning Completed stage");
                return VTEStage.Completed;
            }

            // Check Step 9 stages (gem addition)
            if (items.Any(item => item.Name.Equals("Virral Egg with Dual Gems", StringComparison.OrdinalIgnoreCase)))
                return VTEStage.Step9_TwoGems;

            if (items.Any(item => item.Name.Equals("Virral Egg with Gem", StringComparison.OrdinalIgnoreCase)))
                return VTEStage.Step9_OneGem;

            // Check Step 8 stage
            if (items.Any(item => item.Name.Equals("Incomplete Virral Egg", StringComparison.OrdinalIgnoreCase)))
                return VTEStage.Step9_OneGem;

            // Check Step 7-8 stages
            if (items.Any(item => item.Name.Equals("Fully Wired Mantis Egg", StringComparison.OrdinalIgnoreCase)) &&
                items.Any(item => item.Name.Equals("Interfaced Nano Sensor", StringComparison.OrdinalIgnoreCase)))
                return VTEStage.Step8_IncompleteEgg;

            if (items.Any(item => item.Name.Equals("Fully Wired Mantis Egg", StringComparison.OrdinalIgnoreCase)))
                return VTEStage.Step7_FullyWired;

            if (items.Any(item => item.Name.Contains("Partially Wired Mantis Egg")))
                return VTEStage.Step7_PartiallyWired;

            // Check earlier stages
            if (items.Any(item => item.Name.Equals("Petrified Mantis Egg", StringComparison.OrdinalIgnoreCase)))
                return VTEStage.Step6_PetrifiedEgg;

            if (items.Any(item => item.Name.Equals("Interfaced Nano Sensor", StringComparison.OrdinalIgnoreCase)))
                return VTEStage.Step5_InterfacedSensor;

            if (items.Any(item => item.Name.Equals("Nano Sensor", StringComparison.OrdinalIgnoreCase)))
                return VTEStage.Step4_NanoSensor;

            if (items.Any(item => item.Name.Equals("Perfectly Cut Soul Fragment", StringComparison.OrdinalIgnoreCase)))
                return VTEStage.Step3_CutFragments;

            if (items.Any(item => item.Name.Equals("Gold Filigree Wire", StringComparison.OrdinalIgnoreCase)))
                return VTEStage.Step2_GoldWire;

            if (items.Any(item => item.Name.Equals("Liquid Gold", StringComparison.OrdinalIgnoreCase)))
                return VTEStage.Step1_LiquidGold;

            // Check for raw materials
            bool hasRawMaterials = items.Any(item =>
                item.Name.Equals("Soul Fragment", StringComparison.OrdinalIgnoreCase) ||
                item.Name.Equals("Robot Junk", StringComparison.OrdinalIgnoreCase) ||
                item.Name.Equals("Small Gold Ingot", StringComparison.OrdinalIgnoreCase) ||
                item.Name.Equals("Mantis Egg", StringComparison.OrdinalIgnoreCase));

            if (hasRawMaterials)
                return VTEStage.RawMaterials;

            return VTEStage.None;
        }



        private async Task<bool> ProcessGoldIngotsToWire(Container targetContainer)
        {
            try
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 1-2: Processing Gold Ingots to Gold Filigree Wire");

                // Step 1: Convert Small Gold Ingots to Liquid Gold using Personal Furnace
                var goldIngots = Inventory.Items.Where(item => item.Name.Equals("Small Gold Ingot", StringComparison.OrdinalIgnoreCase)).ToList();
                var personalFurnaces = Inventory.Items.Where(item => item.Name.Equals("Personal Furnace", StringComparison.OrdinalIgnoreCase)).ToList();

                RecipeUtilities.LogDebug($"[{RecipeName}] Found {goldIngots.Count} gold ingots and {personalFurnaces.Count} personal furnaces");

                bool step1Success = false;
                if (goldIngots.Any() && personalFurnaces.Any())
                {
                    for (int i = 0; i < Math.Min(goldIngots.Count, personalFurnaces.Count); i++)
                    {
                        RecipeUtilities.LogDebug($"[{RecipeName}] Combining {personalFurnaces[i].Name} with {goldIngots[i].Name}");
                        await CombineItems(personalFurnaces[i], goldIngots[i]);
                        step1Success = true;
                    }
                }

                // Step 2: Convert Liquid Gold to Gold Filigree Wire using Wire Drawing Machine
                var liquidGold = Inventory.Items.Where(item => item.Name.Equals("Liquid Gold", StringComparison.OrdinalIgnoreCase)).ToList();
                var wireDrawingMachine = FindTool("Wire Drawing Machine");

                bool step2Success = false;
                if (wireDrawingMachine != null && liquidGold.Any())
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Found {liquidGold.Count} liquid gold, converting to wire");
                    foreach (var liquid in liquidGold)
                    {
                        await CombineItems(wireDrawingMachine, liquid);
                        step2Success = true;
                    }
                }

                bool overallSuccess = step1Success || step2Success;
                RecipeUtilities.LogDebug($"[{RecipeName}] Completed gold processing steps - Success: {overallSuccess}");
                return overallSuccess;
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error processing gold ingots: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ProcessSoulFragments(Container targetContainer)
        {
            try
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 3: Processing Soul Fragments");

                var soulFragments = Inventory.Items.Where(item => item.Name.Equals("Soul Fragment", StringComparison.OrdinalIgnoreCase)).ToList();
                var jensenCutter = FindTool("Jensen Gem Cutter");

                if (jensenCutter != null && soulFragments.Any())
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Found {soulFragments.Count} soul fragments, cutting them");
                    foreach (var fragment in soulFragments)
                    {
                        await CombineItems(jensenCutter, fragment);
                    }
                    RecipeUtilities.LogDebug($"[{RecipeName}] Completed soul fragment processing - Success: true");
                    return true;
                }
                else
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] No soul fragments or Jensen Cutter found - Success: false");
                    return false;
                }
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error processing soul fragments: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ProcessRobotJunkToSensor(Container targetContainer)
        {
            try
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 4-5: Processing Robot Junk to Interfaced Nano Sensor");

                // Step 4: Robot Junk to Nano Sensor
                var robotJunk = Inventory.Items.Where(item => item.Name.Equals("Robot Junk", StringComparison.OrdinalIgnoreCase)).ToList();
                var screwdriver = FindTool("Screwdriver");

                bool step4Success = false;
                if (screwdriver != null && robotJunk.Any())
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Found {robotJunk.Count} robot junk, converting to nano sensor");
                    foreach (var junk in robotJunk)
                    {
                        await CombineItems(screwdriver, junk);
                        step4Success = true;
                    }
                }

                // Step 5: Nano Sensor to Interfaced Nano Sensor
                var nanoSensors = Inventory.Items.Where(item => item.Name.Equals("Nano Sensor", StringComparison.OrdinalIgnoreCase)).ToList();
                var jensenCutter = FindTool("Jensen Gem Cutter");

                bool step5Success = false;
                if (jensenCutter != null && nanoSensors.Any())
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Found {nanoSensors.Count} nano sensors, interfacing them");
                    foreach (var sensor in nanoSensors)
                    {
                        await CombineItems(jensenCutter, sensor);
                        step5Success = true;
                    }
                }

                bool overallSuccess = step4Success || step5Success;
                RecipeUtilities.LogDebug($"[{RecipeName}] Completed robot junk to sensor processing - Success: {overallSuccess}");
                return overallSuccess;
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error processing robot junk: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ProcessMantisEgg(Item mantisEgg, Container targetContainer)
        {
            try
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 6: Processing Mantis Egg to Petrified Mantis Egg");

                var cutSoulFragment = Inventory.Items.FirstOrDefault(item => item.Name.Equals("Perfectly Cut Soul Fragment", StringComparison.OrdinalIgnoreCase));

                if (cutSoulFragment != null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Combining {cutSoulFragment.Name} with {mantisEgg.Name}");
                    await CombineItems(cutSoulFragment, mantisEgg);
                    RecipeUtilities.LogDebug($"[{RecipeName}] Completed mantis egg processing - Success: true");
                    return true;
                }
                else
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] No Perfectly Cut Soul Fragment found - Success: false");
                    return false;
                }
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error processing mantis egg: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> WireMantisEgg(Container targetContainer)
        {
            try
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 7: Wiring Mantis Egg");

                var petrifiedEgg = Inventory.Items.FirstOrDefault(item => item.Name.Equals("Petrified Mantis Egg", StringComparison.OrdinalIgnoreCase));
                var goldWires = Inventory.Items.Where(item => item.Name.Equals("Gold Filigree Wire", StringComparison.OrdinalIgnoreCase)).ToList();

                if (petrifiedEgg != null && goldWires.Count >= 3)
                {
                    // First wiring - creates Partially Wired Mantis Egg
                    RecipeUtilities.LogDebug($"[{RecipeName}] First wiring: {goldWires[0].Name} with {petrifiedEgg.Name}");
                    await CombineItems(goldWires[0], petrifiedEgg);

                    // Second wiring - creates another Partially Wired Mantis Egg
                    var partiallyWired1 = Inventory.Items.FirstOrDefault(item => item.Name.Contains("Partially Wired Mantis Egg"));
                    if (partiallyWired1 != null && goldWires.Count >= 2)
                    {
                        RecipeUtilities.LogDebug($"[{RecipeName}] Second wiring: {goldWires[1].Name} with {partiallyWired1.Name}");
                        await CombineItems(goldWires[1], partiallyWired1);

                        // Third wiring - creates Fully Wired Mantis Egg
                        var partiallyWired2 = Inventory.Items.FirstOrDefault(item => item.Name.Contains("Partially Wired Mantis Egg"));
                        if (partiallyWired2 != null && goldWires.Count >= 3)
                        {
                            RecipeUtilities.LogDebug($"[{RecipeName}] Third wiring: {goldWires[2].Name} with {partiallyWired2.Name}");
                            await CombineItems(goldWires[2], partiallyWired2);
                            RecipeUtilities.LogDebug($"[{RecipeName}] Completed mantis egg wiring - Success: true");
                            return true;
                        }
                    }
                }

                RecipeUtilities.LogDebug($"[{RecipeName}] Could not complete mantis egg wiring - missing components");
                return false;
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error wiring mantis egg: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> AddSensorToEgg(Container targetContainer)
        {
            try
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 8: Adding Sensor to Egg");

                var fullyWiredEgg = Inventory.Items.FirstOrDefault(item => item.Name.Equals("Fully Wired Mantis Egg", StringComparison.OrdinalIgnoreCase));
                var interfacedSensor = Inventory.Items.FirstOrDefault(item => item.Name.Equals("Interfaced Nano Sensor", StringComparison.OrdinalIgnoreCase));

                if (fullyWiredEgg != null && interfacedSensor != null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Combining {interfacedSensor.Name} with {fullyWiredEgg.Name}");
                    await CombineItems(interfacedSensor, fullyWiredEgg);
                    RecipeUtilities.LogDebug($"[{RecipeName}] Completed sensor addition - Success: true");
                    return true;
                }
                else
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Missing components for sensor addition - Success: false");
                    return false;
                }
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error adding sensor to egg: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> AddGemsToEgg(Container targetContainer)
        {
            try
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 9: Adding Gems to create final VTE");

                var jensenCutter = FindTool("Jensen Gem Cutter");
                if (jensenCutter == null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] No Jensen Gem Cutter found for gem processing");
                    return false;
                }

                var gems = Inventory.Items.Where(item => item.Name.Equals("Gem", StringComparison.OrdinalIgnoreCase)).ToList();
                if (gems.Count < 3)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Need 3 gems but only found {gems.Count}");
                    return false;
                }

                // First gem addition
                var incompleteEgg = Inventory.Items.FirstOrDefault(item => item.Name.Equals("Incomplete Virral Egg", StringComparison.OrdinalIgnoreCase));
                if (incompleteEgg != null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Adding first gem");
                    await CombineItems(jensenCutter, gems[0]);

                    var cutGem1 = Inventory.Items.FirstOrDefault(item => item.Name.Contains("Cut"));
                    if (cutGem1 != null)
                    {
                        await CombineItems(cutGem1, incompleteEgg);
                    }
                }

                // Second gem addition
                var eggWithOneGem = Inventory.Items.FirstOrDefault(item => item.Name.Equals("Virral Egg with Gem", StringComparison.OrdinalIgnoreCase));
                if (eggWithOneGem != null && gems.Count >= 2)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Adding second gem");
                    await CombineItems(jensenCutter, gems[1]);

                    var cutGem2 = Inventory.Items.FirstOrDefault(item => item.Name.Contains("Cut"));
                    if (cutGem2 != null)
                    {
                        await CombineItems(cutGem2, eggWithOneGem);
                    }
                }

                // Final combination
                var eggWithTwoGems = Inventory.Items.FirstOrDefault(item => item.Name.Equals("Virral Egg with Dual Gems", StringComparison.OrdinalIgnoreCase));
                if (eggWithTwoGems != null && gems.Count >= 3)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Adding final gem to complete VTE");
                    await CombineItems(jensenCutter, gems[2]);

                    var cutGem3 = Inventory.Items.FirstOrDefault(item => item.Name.Contains("Cut"));
                    if (cutGem3 != null)
                    {
                        await CombineItems(cutGem3, eggWithTwoGems);
                        RecipeUtilities.LogDebug($"[{RecipeName}] Completed gem addition - VTE should be created!");
                        return true;
                    }
                }

                RecipeUtilities.LogDebug($"[{RecipeName}] Could not complete all gem additions");
                return false;
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error adding gems to egg: {ex.Message}");
                return false;
            }
        }
    }
}
