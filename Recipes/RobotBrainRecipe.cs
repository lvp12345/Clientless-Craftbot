using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AOSharp.Clientless;

namespace Craftbot.Recipes
{
    /// <summary>
    /// Handles Robot Brain processing using unified recipe patterns with multistep workflow
    /// Multi-step Recipe:
    /// Step 1: Screwdriver + Robot Junk = Nano Sensor
    /// Step 2: Bio Analyzing Computer + Nano Sensor = Basic Robot Brain
    /// Step 3: MasterComm - Personalization Device + Basic Robot Brain = Personalized Basic Robot Brain
    /// </summary>
    public class RobotBrainRecipe : BaseRecipeProcessor
    {
        public override string RecipeName => "Robot Brain";

        /// <summary>
        /// Robot Brain combinations need extra time for server to process the tradeskill
        /// </summary>
        protected override int GetCombinationDelay()
        {
            return 500; // Increased from default 200ms to fix processing failures
        }

        public enum RobotBrainStage
        {
            None,
            RawMaterials,           // Has Robot Junk (start from Step 1)
            Step1_NanoSensor,       // Has Nano Sensor (continue from Step 2)
            Step2_BasicBrain,       // Has Basic Robot Brain (continue from Step 3)
            Completed               // Has Personalized Basic Robot Brain (finished)
        }

        public override bool CanProcess(Item item)
        {
            // Check if this item can be part of Robot Brain processing (raw materials OR partially processed items)
            return item.Name.Equals("Robot Junk", StringComparison.OrdinalIgnoreCase) ||
                   item.Name.Equals("Nano Sensor", StringComparison.OrdinalIgnoreCase) ||
                   (item.Name.ToLower().Contains("basic robot brain") &&
                    !item.Name.ToLower().Contains("personalized"));
        }

        protected override async Task ProcessRecipeLogic(Item item, Container targetContainer)
        {
            RecipeUtilities.LogDebug($"[{RecipeName}] Starting Robot Brain multistep processing for {item.Name}");

            // DON'T move items - process them directly in the bag
            // The base class ProcessAllItemsUntilComplete already moved Robot Junk to inventory
            // We just need to detect the stage and process it

            // Detect current stage and process accordingly
            var currentStage = DetectRobotBrainStageFromInventory();
            RecipeUtilities.LogDebug($"[{RecipeName}] Current Robot Brain stage: {currentStage}");

            // Process based on current stage
            await ProcessRobotBrainStages(currentStage, targetContainer);

            RecipeUtilities.LogDebug($"[{RecipeName}] Completed Robot Brain processing!");
        }

        private async Task ProcessRobotBrainStages(RobotBrainStage currentStage, Container targetContainer)
        {
            var stage = currentStage;
            int maxRetries = 5; // Prevent infinite loops
            int retryCount = 0;

            while (stage != RobotBrainStage.None && stage != RobotBrainStage.Completed && retryCount < maxRetries)
            {
                retryCount++;
                bool stepSuccessful = false;
                RecipeUtilities.LogDebug($"[{RecipeName}] Processing stage: {stage} (attempt {retryCount})");

                switch (stage)
                {
                    case RobotBrainStage.RawMaterials:
                        RecipeUtilities.LogDebug($"[{RecipeName}] Processing Robot Junk (Step 1)");
                        stepSuccessful = await ProcessRobotJunkToNanoSensor();
                        break;

                    case RobotBrainStage.Step1_NanoSensor:
                        RecipeUtilities.LogDebug($"[{RecipeName}] Processing Nano Sensor (Step 2)");
                        stepSuccessful = await ProcessNanoSensorToBasicBrain();
                        break;

                    case RobotBrainStage.Step2_BasicBrain:
                        RecipeUtilities.LogDebug($"[{RecipeName}] Processing Basic Robot Brain (Step 3)");
                        stepSuccessful = await ProcessBasicBrainToPersonalized();
                        break;
                }

                if (stepSuccessful)
                {
                    // Re-detect stage after successful processing
                    await Task.Delay(500); // Allow time for items to update
                    stage = DetectRobotBrainStageFromInventory();
                    RecipeUtilities.LogDebug($"[{RecipeName}] Stage after processing: {stage}");
                }
                else
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step failed, stopping processing");
                    break;
                }
            }

            if (stage == RobotBrainStage.Completed)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Robot Brain recipe completed successfully!");
            }
            else if (retryCount >= maxRetries)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Max retries reached, stopping processing");
            }
        }

        private RobotBrainStage DetectRobotBrainStageFromInventory()
        {
            RecipeUtilities.LogDebug($"[{RecipeName}] Detecting Robot Brain stage from inventory");

            // Count all items for multi-set processing
            var robotJunkCount = Inventory.Items.Count(item =>
                item.Name.Equals("Robot Junk", StringComparison.OrdinalIgnoreCase));
            var nanoSensorCount = Inventory.Items.Count(item =>
                item.Name.Equals("Nano Sensor", StringComparison.OrdinalIgnoreCase));
            var basicBrainCount = Inventory.Items.Count(item =>
                item.Name.ToLower().Contains("basic robot brain") &&
                !item.Name.ToLower().Contains("personalized"));
            var completedBrainCount = Inventory.Items.Count(item =>
                item.Name.ToLower().Contains("personalized basic robot brain"));

            RecipeUtilities.LogDebug($"[{RecipeName}] Multi-set inventory: {robotJunkCount} Robot Junk, {nanoSensorCount} Nano Sensors, {basicBrainCount} Basic Brains, {completedBrainCount} Completed Brains");

            // Priority processing: Process the earliest stage items first to maximize throughput
            // This allows multiple sets to be processed efficiently

            // Check for Robot Junk (Step 1) - Highest priority for multi-set processing
            if (robotJunkCount > 0)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found {robotJunkCount} Robot Junk - returning RawMaterials stage for multi-set processing");
                return RobotBrainStage.RawMaterials;
            }

            // Check for Nano Sensor (Step 2)
            if (nanoSensorCount > 0)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found {nanoSensorCount} Nano Sensors - returning Step1_NanoSensor stage");
                return RobotBrainStage.Step1_NanoSensor;
            }

            // Check for Basic Robot Brain (Step 3)
            if (basicBrainCount > 0)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found {basicBrainCount} Basic Robot Brains - returning Step2_BasicBrain stage");
                return RobotBrainStage.Step2_BasicBrain;
            }

            // Check for completed Robot Brain
            if (completedBrainCount > 0)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found {completedBrainCount} completed Personalized Basic Robot Brains - returning Completed stage");
                return RobotBrainStage.Completed;
            }

            RecipeUtilities.LogDebug($"[{RecipeName}] No Robot Brain components found - returning None stage");
            return RobotBrainStage.None;
        }

        private async Task<bool> ProcessRobotJunkToNanoSensor()
        {
            try
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 1: Processing Robot Junk to Nano Sensor (Multi-set processing)");

                var robotJunk = Inventory.Items.Where(item =>
                    item.Name.Equals("Robot Junk", StringComparison.OrdinalIgnoreCase)).ToList();
                var screwdriver = FindTool("Screwdriver");

                if (screwdriver == null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step 1: No Screwdriver found");
                    return false;
                }

                if (!robotJunk.Any())
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step 1: No Robot Junk found");
                    return false;
                }

                RecipeUtilities.LogDebug($"[{RecipeName}] Step 1: Found {robotJunk.Count} Robot Junk, converting ALL to Nano Sensors for multi-set processing");

                int processedCount = 0;
                foreach (var junk in robotJunk)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step 1: Processing Robot Junk #{processedCount + 1}/{robotJunk.Count}");
                    await CombineItems(screwdriver, junk);
                    processedCount++;

                    // Small delay between combinations for stability
                    await Task.Delay(100);
                }

                // Verify results - count all nano sensors created
                await Task.Delay(500);
                var nanoSensors = Inventory.Items.Where(item =>
                    item.Name.Equals("Nano Sensor", StringComparison.OrdinalIgnoreCase)).ToList();

                bool success = nanoSensors.Count >= processedCount;
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 1: {(success ? "Success" : "Failed")} - Created {nanoSensors.Count} Nano Sensors from {processedCount} Robot Junk");

                // CRITICAL FIX: Manually track ALL results since automatic tracking isn't working
                foreach (var sensor in nanoSensors)
                {
                    Core.ItemTracker.TrackRecipeResult(sensor, RecipeName);
                    RecipeUtilities.LogDebug($"[{RecipeName}] MANUALLY TRACKED: {sensor.Name} as recipe result");
                }

                return success;
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 1: Error - {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ProcessNanoSensorToBasicBrain()
        {
            try
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 2: Processing Nano Sensors to Basic Robot Brains (Multi-set processing)");

                var nanoSensors = Inventory.Items.Where(item =>
                    item.Name.Equals("Nano Sensor", StringComparison.OrdinalIgnoreCase)).ToList();

                // For multi-set processing, we need multiple Bio Analyzing Computers or reuse one
                var bioComputers = Inventory.Items.Where(item =>
                    item.Name.Contains("Bio Analyzing Computer")).ToList();

                if (!bioComputers.Any())
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step 2: No Bio Analyzing Computer found");
                    return false;
                }

                if (!nanoSensors.Any())
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step 2: No Nano Sensors found");
                    return false;
                }

                RecipeUtilities.LogDebug($"[{RecipeName}] Step 2: Found {nanoSensors.Count} Nano Sensors and {bioComputers.Count} Bio Analyzing Computers");
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 2: Converting ALL {nanoSensors.Count} Nano Sensors to Basic Robot Brains");

                int processedCount = 0;
                foreach (var sensor in nanoSensors)
                {
                    // Use the first available Bio Analyzing Computer (tools can be reused)
                    var bioComputer = bioComputers.First();
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step 2: Processing Nano Sensor #{processedCount + 1}/{nanoSensors.Count}");
                    await CombineItems(bioComputer, sensor);
                    processedCount++;

                    // Small delay between combinations for stability
                    await Task.Delay(100);
                }

                // Verify results - count all basic brains created
                await Task.Delay(500);
                var basicBrains = Inventory.Items.Where(item =>
                    item.Name.ToLower().Contains("basic robot brain") &&
                    !item.Name.ToLower().Contains("personalized")).ToList();

                bool success = basicBrains.Count >= processedCount;
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 2: {(success ? "Success" : "Failed")} - Created {basicBrains.Count} Basic Robot Brains from {processedCount} Nano Sensors");

                // CRITICAL FIX: Manually track ALL results since automatic tracking isn't working
                foreach (var brain in basicBrains)
                {
                    Core.ItemTracker.TrackRecipeResult(brain, RecipeName);
                    RecipeUtilities.LogDebug($"[{RecipeName}] MANUALLY TRACKED: {brain.Name} as recipe result");
                }

                return success;
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 2: Error - {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ProcessBasicBrainToPersonalized()
        {
            try
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 3: Processing Basic Robot Brains to Personalized Basic Robot Brains (Multi-set processing)");

                var basicBrains = Inventory.Items.Where(item =>
                    item.Name.ToLower().Contains("basic robot brain") &&
                    !item.Name.ToLower().Contains("personalized")).ToList();

                // For multi-set processing, we need multiple MasterComm devices or reuse one
                var masterComms = Inventory.Items.Where(item =>
                    item.Name.Contains("MasterComm - Personalization Device")).ToList();

                if (!masterComms.Any())
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step 3: No MasterComm - Personalization Device found");
                    return false;
                }

                if (!basicBrains.Any())
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step 3: No Basic Robot Brains found");
                    return false;
                }

                RecipeUtilities.LogDebug($"[{RecipeName}] Step 3: Found {basicBrains.Count} Basic Robot Brains and {masterComms.Count} MasterComm devices");
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 3: Converting ALL {basicBrains.Count} Basic Robot Brains to Personalized Basic Robot Brains");

                int processedCount = 0;
                foreach (var brain in basicBrains)
                {
                    // Use the first available MasterComm device (tools can be reused)
                    var masterComm = masterComms.First();
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step 3: Processing Basic Robot Brain #{processedCount + 1}/{basicBrains.Count}");
                    await CombineItems(masterComm, brain);
                    processedCount++;

                    // Small delay between combinations for stability
                    await Task.Delay(100);
                }

                // Verify results - count all personalized brains created
                await Task.Delay(500);
                var personalizedBrains = Inventory.Items.Where(item =>
                    item.Name.ToLower().Contains("personalized basic robot brain")).ToList();

                bool success = personalizedBrains.Count >= processedCount;
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 3: {(success ? "Success" : "Failed")} - Created {personalizedBrains.Count} Personalized Basic Robot Brains from {processedCount} Basic Robot Brains");

                // CRITICAL FIX: Manually track ALL results since automatic tracking isn't working
                foreach (var brain in personalizedBrains)
                {
                    Core.ItemTracker.TrackRecipeResult(brain, RecipeName);
                    RecipeUtilities.LogDebug($"[{RecipeName}] MANUALLY TRACKED: {brain.Name} as recipe result");
                }

                return success;
            }
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 3: Error - {ex.Message}");
                return false;
            }
        }

        public override RecipeAnalysisResult AnalyzeItems(List<Item> items)
        {
            // Create analysis based on detected stage and count complete sets
            var stage = DetectRobotBrainStageFromItems(items);
            var processableItemCount = items.Where(CanProcess).Count();

            // Count potential complete sets for multi-set processing
            var robotJunkCount = items.Count(item => item.Name.Equals("Robot Junk", StringComparison.OrdinalIgnoreCase));
            var bioComputerCount = items.Count(item => item.Name.Contains("Bio Analyzing Computer"));
            var masterCommCount = items.Count(item => item.Name.Contains("MasterComm - Personalization Device"));
            var nanoSensorCount = items.Count(item => item.Name.Equals("Nano Sensor", StringComparison.OrdinalIgnoreCase));
            var basicBrainCount = items.Count(item => item.Name.ToLower().Contains("basic robot brain") && !item.Name.ToLower().Contains("personalized"));
            var completedBrainCount = items.Count(item => item.Name.ToLower().Contains("personalized basic robot brain"));

            // Calculate potential complete sets that can be made
            int potentialCompleteSets = 0;
            if (robotJunkCount > 0 && bioComputerCount > 0 && masterCommCount > 0)
            {
                potentialCompleteSets = Math.Min(Math.Min(robotJunkCount, bioComputerCount), masterCommCount);
            }

            string description = $"Robot Brain Stage: {stage} - {processableItemCount} components found";
            if (potentialCompleteSets > 0)
            {
                description += $" (Can make {potentialCompleteSets} complete sets)";
            }
            else if (nanoSensorCount > 0 || basicBrainCount > 0 || completedBrainCount > 0)
            {
                description += $" (Partial processing: {nanoSensorCount} sensors, {basicBrainCount} basic brains, {completedBrainCount} completed)";
            }

            return new RecipeAnalysisResult
            {
                CanProcess = stage != RobotBrainStage.None,
                ProcessableItemCount = processableItemCount,
                Stage = stage.ToString(),
                Description = description
            };
        }

        private RobotBrainStage DetectRobotBrainStageFromItems(List<Item> items)
        {
            RecipeUtilities.LogDebug($"[{RecipeName}] Checking {items.Count} items for Robot Brain stage detection");

            // Check for completed Robot Brain first
            var completedBrain = items.FirstOrDefault(item =>
                item.Name.ToLower().Contains("personalized basic robot brain"));
            if (completedBrain != null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found completed Personalized Basic Robot Brain - returning Completed stage");
                return RobotBrainStage.Completed;
            }

            // Check for Basic Robot Brain (Step 3)
            if (items.Any(item => item.Name.ToLower().Contains("basic robot brain") &&
                                  !item.Name.ToLower().Contains("personalized")))
                return RobotBrainStage.Step2_BasicBrain;

            // Check for Nano Sensor (Step 2)
            if (items.Any(item => item.Name.Equals("Nano Sensor", StringComparison.OrdinalIgnoreCase)))
                return RobotBrainStage.Step1_NanoSensor;

            // Check for Robot Junk (Step 1)
            if (items.Any(item => item.Name.Equals("Robot Junk", StringComparison.OrdinalIgnoreCase)))
                return RobotBrainStage.RawMaterials;

            return RobotBrainStage.None;
        }

        /// <summary>
        /// Move Robot Brain components from bag to inventory for processing
        /// Similar to VTE's MoveComponentsToInventoryShared method
        /// </summary>
        private async Task MoveRobotBrainComponentsToInventory(Container targetContainer)
        {
            try
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Moving Robot Brain components to inventory for processing");

                // Define Robot Brain component names
                string[] robotBrainComponents = {
                    "Robot Junk",
                    "Bio Analyzing Computer",
                    "MasterComm - Personalization Device",
                    "Screwdriver",
                    "Nano Sensor",
                    "Basic Robot Brain"
                };

                // Collect all Robot Brain components from the bag
                var allItemsToMove = new List<Item>();
                foreach (var componentName in robotBrainComponents)
                {
                    var itemsToMove = targetContainer.Items.Where(item =>
                        item.Name.Equals(componentName, StringComparison.OrdinalIgnoreCase)).ToList();
                    allItemsToMove.AddRange(itemsToMove);
                }

                RecipeUtilities.LogDebug($"[{RecipeName}] Found {allItemsToMove.Count} Robot Brain components to move to inventory");

                // OVERFLOW PROTECTION: Check if moving all items is safe
                if (!Modules.PrivateMessageModule.CheckInventorySpaceForProcessing(allItemsToMove.Count, RecipeName))
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] ❌ OVERFLOW PROTECTION: Cannot move {allItemsToMove.Count} Robot Brain items - insufficient inventory space");
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
            catch (Exception ex)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Error moving Robot Brain components to inventory: {ex.Message}");
            }
        }
    }
}
