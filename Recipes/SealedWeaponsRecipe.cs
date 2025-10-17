using System;
using System.Linq;
using System.Threading.Tasks;
using AOSharp.Core;
using AOSharp.Core.Inventory;
using System.Collections.Generic;

namespace Craftbot.Recipes
{
    /// <summary>
    /// Handles Sealed Weapons processing using player-provided Hacker Tool
    /// SPECIAL RULE: Only uses player-provided tools, never touches bot's personal tools
    /// Recipe: Any Hacker Tool + Any Sealed Weapon Receptacle = Unfinished weapon
    /// Then: Various upgrade components + Unfinished weapon = final weapon variants
    /// </summary>
    public class SealedWeaponsRecipe : BaseRecipeProcessor
    {
        public override string RecipeName => "Sealed Weapons";

        // Items that can be processed by this recipe
        private static readonly string[] ProcessableItems = {
            "Sealed Weapon Receptacle",
            "Unfinished Alsaqri Chemical Rifle",
            "Unfinished HSR Explorer 661",
            "Unfinished IMI Tellus TT",
            "Unfinished Ithaca Ki12 Vulture",
            "Unfinished River Seasons XP",
            "Unfinished Soft Pepper Pistol",
            "Unfinished Sol Chironis Systems",
            "Unfinished Summer SMP"
        };

        // Upgrade components that combine with unfinished weapons
        private static readonly string[] UpgradeComponents = {
            "Self-Repairing Ultra-X",
            "Generic Magnetic Propulsion System",
            "Flake Tubing Super-Coolant System",
            "Nano-Interfaced Cooling System",
            "Ultra Short Composite Barrel",
            "Nano Pylon",
            "Energy Pack Interface",
            "Triple Pulse Enabler",
            "Gyro Stabilizing Unit",
            "Rapid-Reload-And-Fire Gyro",
            "Shells Magazine"
        };

        public override bool CanProcess(Item item)
        {
            // Check if item is a processable weapon or upgrade component
            bool canProcess = ProcessableItems.Any(processableItem => 
                item.Name.Contains(processableItem, StringComparison.OrdinalIgnoreCase)) ||
                UpgradeComponents.Any(component => 
                item.Name.Contains(component, StringComparison.OrdinalIgnoreCase));
            
            RecipeUtilities.LogDebug($"[SEALED WEAPONS CHECK] Item: '{item.Name}' -> Can process: {canProcess}");
            return canProcess;
        }

        protected override async Task ProcessRecipeLogic(Item item, Container targetContainer)
        {
            // Check if this is a Sealed Weapon Receptacle (needs hacker tool)
            if (item.Name.Contains("Sealed Weapon Receptacle", StringComparison.OrdinalIgnoreCase))
            {
                await ProcessSealedWeaponReceptacle(item);
            }
            // Check if this is an upgrade component (needs to find matching unfinished weapon)
            else if (UpgradeComponents.Any(component => item.Name.Contains(component, StringComparison.OrdinalIgnoreCase)))
            {
                await ProcessUpgradeComponent(item);
            }
            // Check if this is an unfinished weapon (needs to find matching upgrade component)
            else if (item.Name.Contains("Unfinished", StringComparison.OrdinalIgnoreCase))
            {
                await ProcessUnfinishedWeapon(item);
            }
        }

        /// <summary>
        /// Process Sealed Weapon Receptacle with player-provided Hacker Tool
        /// </summary>
        private async Task ProcessSealedWeaponReceptacle(Item receptacle)
        {
            // CRITICAL RULE: This recipe MUST NEVER use bot's personal tools
            // ONLY use player-provided Hacker Tool
            RecipeUtilities.LogDebug($"[{RecipeName}] CRITICAL: Looking for player-provided Hacker Tool ONLY - NEVER touching bot's tools");
            
            var playerHackerTool = await FindPlayerProvidedHackerTool();
            if (playerHackerTool == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] ❌ CRITICAL: No player-provided Hacker Tool found - CANNOT PROCESS");
                RecipeUtilities.LogDebug($"[{RecipeName}] ❌ This recipe requires player to provide their own Hacker Tool");
                return;
            }

            // Process using unified core
            RecipeUtilities.LogDebug($"[{RecipeName}] Processing {receptacle.Name} with player-provided {playerHackerTool.Name}");
            await CombineItems(playerHackerTool, receptacle);

            // Check result
            var unfinishedCount = Inventory.Items.Where(invItem => invItem.Name.Contains("Unfinished")).Count();
            RecipeUtilities.LogDebug($"[{RecipeName}] Completed processing - now have {unfinishedCount} Unfinished weapon items in inventory");
        }

        /// <summary>
        /// Process upgrade component with matching unfinished weapon
        /// </summary>
        private async Task ProcessUpgradeComponent(Item component)
        {
            // Find matching unfinished weapon for this component
            var unfinishedWeapon = FindMatchingUnfinishedWeapon(component);
            if (unfinishedWeapon == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] No matching unfinished weapon found for {component.Name}");
                return;
            }

            // Process using unified core
            RecipeUtilities.LogDebug($"[{RecipeName}] Processing {component.Name} with {unfinishedWeapon.Name}");
            await CombineItems(component, unfinishedWeapon);

            // Check result
            var finishedCount = Inventory.Items.Where(invItem => 
                !invItem.Name.Contains("Unfinished") && 
                (invItem.Name.Contains("Advanced") || invItem.Name.Contains("Excellent") || 
                 invItem.Name.Contains("Superior") || invItem.Name.Contains("Custom") || 
                 invItem.Name.Contains("Special") || invItem.Name.Contains("Majestic") || 
                 invItem.Name.Contains("Professional") || invItem.Name.Contains("Perfect"))).Count();
            RecipeUtilities.LogDebug($"[{RecipeName}] Completed processing - now have {finishedCount} finished weapon items in inventory");
        }

        /// <summary>
        /// Process unfinished weapon with matching upgrade component
        /// </summary>
        private async Task ProcessUnfinishedWeapon(Item unfinishedWeapon)
        {
            // Find matching upgrade component for this weapon
            var component = FindMatchingUpgradeComponent(unfinishedWeapon);
            if (component == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] No matching upgrade component found for {unfinishedWeapon.Name}");
                return;
            }

            // Process using unified core
            RecipeUtilities.LogDebug($"[{RecipeName}] Processing {unfinishedWeapon.Name} with {component.Name}");
            await CombineItems(component, unfinishedWeapon);

            // Check result
            var finishedCount = Inventory.Items.Where(invItem => 
                !invItem.Name.Contains("Unfinished") && 
                (invItem.Name.Contains("Advanced") || invItem.Name.Contains("Excellent") || 
                 invItem.Name.Contains("Superior") || invItem.Name.Contains("Custom") || 
                 invItem.Name.Contains("Special") || invItem.Name.Contains("Majestic") || 
                 invItem.Name.Contains("Professional") || invItem.Name.Contains("Perfect"))).Count();
            RecipeUtilities.LogDebug($"[{RecipeName}] Completed processing - now have {finishedCount} finished weapon items in inventory");
        }

        /// <summary>
        /// Find matching unfinished weapon for an upgrade component
        /// </summary>
        private Item FindMatchingUnfinishedWeapon(Item component)
        {
            // Define the component-to-weapon mappings
            var componentMappings = new Dictionary<string, string>
            {
                { "Self-Repairing Ultra-X", "Unfinished Alsaqri Chemical Rifle" },
                { "Generic Magnetic Propulsion System", "Unfinished HSR Explorer 661" },
                { "Flake Tubing Super-Coolant System", "Unfinished IMI Tellus TT" },
                { "Nano-Interfaced Cooling System", "Unfinished Ithaca Ki12 Vulture" },
                { "Ultra Short Composite Barrel", "Unfinished River Seasons XP" },
                { "Nano Pylon", "Unfinished Soft Pepper Pistol" },
                { "Energy Pack Interface", "Unfinished Sol Chironis Systems" },
                { "Triple Pulse Enabler", "Unfinished Summer SMP" },
                { "Gyro Stabilizing Unit", "Unfinished Summer SMP" },
                { "Rapid-Reload-And-Fire Gyro", "Unfinished Summer SMP" },
                { "Shells Magazine", "Unfinished Summer SMP" }
            };

            foreach (var mapping in componentMappings)
            {
                if (component.Name.Contains(mapping.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return Inventory.Items.FirstOrDefault(item => 
                        item.Name.Contains(mapping.Value, StringComparison.OrdinalIgnoreCase));
                }
            }

            return null;
        }

        /// <summary>
        /// Find matching upgrade component for an unfinished weapon
        /// </summary>
        private Item FindMatchingUpgradeComponent(Item unfinishedWeapon)
        {
            // Define the weapon-to-component mappings (reverse of above)
            var weaponMappings = new Dictionary<string, string[]>
            {
                { "Unfinished Alsaqri Chemical Rifle", new[] { "Self-Repairing Ultra-X" } },
                { "Unfinished HSR Explorer 661", new[] { "Generic Magnetic Propulsion System" } },
                { "Unfinished IMI Tellus TT", new[] { "Flake Tubing Super-Coolant System" } },
                { "Unfinished Ithaca Ki12 Vulture", new[] { "Nano-Interfaced Cooling System" } },
                { "Unfinished River Seasons XP", new[] { "Ultra Short Composite Barrel" } },
                { "Unfinished Soft Pepper Pistol", new[] { "Nano Pylon" } },
                { "Unfinished Sol Chironis Systems", new[] { "Energy Pack Interface" } },
                { "Unfinished Summer SMP", new[] { "Triple Pulse Enabler", "Gyro Stabilizing Unit", "Rapid-Reload-And-Fire Gyro", "Shells Magazine" } }
            };

            foreach (var mapping in weaponMappings)
            {
                if (unfinishedWeapon.Name.Contains(mapping.Key, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var componentName in mapping.Value)
                    {
                        var component = Inventory.Items.FirstOrDefault(item => 
                            item.Name.Contains(componentName, StringComparison.OrdinalIgnoreCase));
                        if (component != null)
                        {
                            return component;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// CRITICAL: Finds player-provided Hacker Tool ONLY - never touches bot's personal tools
        /// This prevents bot tool destruction since this recipe consumes the tool
        /// </summary>
        private async Task<Item> FindPlayerProvidedHackerTool()
        {
            // Look for any tool with "hacker tool" in the name in inventory
            var hackerTool = Inventory.Items.FirstOrDefault(item => 
                item.Name.Contains("hacker tool", StringComparison.OrdinalIgnoreCase));
            
            if (hackerTool != null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Found player-provided Hacker Tool in inventory: {hackerTool.Name}");
                return hackerTool;
            }

            // If not in inventory, check if player provided one in bags
            // Look through all open backpacks for player-provided hacker tools
            foreach (var backpack in Inventory.Backpacks.Where(bp => bp.IsOpen))
            {
                var bagHackerTool = backpack.Items.FirstOrDefault(item => 
                    item.Name.Contains("hacker tool", StringComparison.OrdinalIgnoreCase));
                
                if (bagHackerTool != null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Found player-provided Hacker Tool in bag {backpack.Name}: {bagHackerTool.Name}");
                    bagHackerTool.MoveToInventory();
                    await Task.Delay(100); // Wait for tool to move
                    return bagHackerTool;
                }
            }

            RecipeUtilities.LogDebug($"[{RecipeName}] ❌ CRITICAL: No player-provided Hacker Tool found anywhere");
            RecipeUtilities.LogDebug($"[{RecipeName}] ❌ Player must provide their own Hacker Tool for this recipe");
            return null;
        }

        public override RecipeAnalysisResult AnalyzeItems(List<Item> items)
        {
            return AnalyzeItemsShared(items, "Sealed Weapons Processing");
        }
    }
}
