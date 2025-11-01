using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AOSharp.Clientless;
using AOSharp.Common.GameData;

namespace Craftbot.Recipes
{
    /// <summary>
    /// Handles Alien Armor processing - a complex multi-step recipe with intelligent QL matching
    /// Supports entry at any step and can combine completed armor pieces
    /// </summary>
    public class AlienArmorRecipe : BaseRecipeProcessor
    {
        public override string RecipeName => "Alien Armor";

        /// <summary>
        /// Override combination delay for alien armor processing
        /// Alien armor combinations need extra time for server to process the tradeskill
        /// </summary>
        protected override int GetCombinationDelay()
        {
            return 500; // Increased from default 200ms to fix processing failures
        }

        public enum AlienArmorStage
        {
            None,
            Step1_SolidClump,           // Has Solid Clump (start from Step 1)
            Step2_BiomaterialIdentified, // Has Mutated/Pristine Bio-Material (skip Step 1)
            Step3_DNASoup,              // Has Generic DNA-Soup (skip to Step 3)
            Step4_DNACocktail,          // Has DNA Cocktail (skip to Step 4)
            Step7_FormattedViralbot,    // Has Formatted Viralbot Solution (skip to Step 7)
            Step8_FormattedArmor,       // Has Formatted Viralbot Armor (skip to Step 8)
            Completed,                  // Has final armor piece
            CombineArmor                // Has two armor pieces to combine
        }

        // Armor type names for detection
        private static readonly string[] ArmorTypes = {
            "Arithmetic", "Strong", "Enduring", "Spiritual", "Observant", "Supple"
        };

        // Armor slot names for detection
        private static readonly string[] ArmorSlots = {
            "Body Armor", "Boots", "Footwear", "Legwear", "Gloves", "Helmet", "Pants", "Sleeves",
            "Legwear", "Vest", "Sleeve", "Headwear"
        };

        // Combined armor recipes (source + target = result)
        // CRITICAL: Order matters! First element is SOURCE, second is TARGET
        // Source QL must be >= 80% of target QL
        private static readonly Dictionary<string, string[]> CombinedArmorRecipes = new Dictionary<string, string[]>
        {
            { "Combined Commando's", new[] { "Strong", "Supple" } },      // Strong + Supple
            { "Combined Mercenary's", new[] { "Strong", "Enduring" } },   // Strong + Enduring
            { "Combined Officer's", new[] { "Spiritual", "Arithmetic" } }, // Spiritual + Arithmetic
            { "Combined Paramedic's", new[] { "Spiritual", "Enduring" } }, // Spiritual + Enduring
            { "Combined Scout's", new[] { "Observant", "Arithmetic" } },   // Observant + Arithmetic
            { "Combined Sharpshooter's", new[] { "Observant", "Supple" } } // Observant + Supple
        };

        public override bool CanProcess(Item item)
        {
            string itemName = item.Name;

            // Step 1: Solid Clump
            if (itemName.Equals("Solid Clump of Kyr'Ozch Bio-Material", StringComparison.OrdinalIgnoreCase))
                return true;

            // Step 2: Identified Bio-Material
            if (itemName.Contains("Kyr'Ozch Bio-Material") &&
                (itemName.Contains("Mutated") || itemName.Contains("Pristine")))
                return true;

            // Step 3: DNA Soup
            if (itemName.Contains("Generic Kyr'Ozch DNA-Soup"))
                return true;

            // Step 4: DNA Cocktail
            if (itemName.Contains("DNA Cocktail"))
                return true;

            // Step 4-5: Viralbots (regular, not Lead)
            if (itemName.Contains("Kyr'Ozch Viralbots") && !itemName.Contains("Lead"))
                return true;

            // Step 7: Formatted Viralbot Solution
            if (itemName.Contains("Kyr'Ozch Formatted Viralbot Solution"))
                return true;

            // Step 7: Basic Clothing
            if (itemName.StartsWith("Basic ") &&
                (itemName.Contains("Fashion") || itemName.Contains("Footwear") ||
                 itemName.Contains("Gloves") || itemName.Contains("Headwear") ||
                 itemName.Contains("Legwear") || itemName.Contains("Sleeve")))
                return true;

            // Step 8: Formatted Viralbot Armor
            if (itemName.Contains("Formatted Viralbot"))
                return true;

            // Step 8: Lead Viralbots
            if (itemName.Contains("Lead Viralbots"))
                return true;

            // Final armor pieces (for combining)
            foreach (var armorType in ArmorTypes)
            {
                if (itemName.Contains(armorType) && IsArmorPiece(itemName))
                    return true;
            }

            // Combined armor pieces
            if (itemName.Contains("Combined"))
                return true;

            return false;
        }

        // Track items that have been consumed in combinations to avoid double-processing
        private static HashSet<int> _consumedItemIds = new HashSet<int>();

        /// <summary>
        /// Clear the consumed items tracking - should be called at the start of each new trade
        /// </summary>
        public static void ClearConsumedItemsTracking()
        {
            _consumedItemIds.Clear();
            RecipeUtilities.LogDebug("[Alien Armor] Cleared consumed items tracking for new trade");
        }

        protected override async Task ProcessRecipeLogic(Item item, Container targetContainer)
        {
            RecipeUtilities.LogDebug($"[{RecipeName}] Starting Alien Armor processing for {item.Name}");

            // CRITICAL FIX: Check if this item was already consumed in a combination
            if (_consumedItemIds.Contains(item.Id))
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Item {item.Name} (ID: {item.Id}) was already consumed in a combination - skipping");
                return;
            }

            // Move all alien armor components to inventory
            if (targetContainer != null)
            {
                await MoveAlienArmorComponentsToInventory(targetContainer);
            }

            // First check if we can combine armor pieces
            if (await TryCombineArmorPieces())
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Armor combination completed");
                return;
            }

            // Otherwise, process the multi-step recipe
            var currentStage = DetectAlienArmorStageFromInventory();
            RecipeUtilities.LogDebug($"[{RecipeName}] Current Alien Armor stage: {currentStage}");

            await ProcessAlienArmorStages(currentStage, targetContainer);

            RecipeUtilities.LogDebug($"[{RecipeName}] Completed Alien Armor processing!");
        }

        private async Task MoveAlienArmorComponentsToInventory(Container targetContainer)
        {
            RecipeUtilities.LogDebug($"[{RecipeName}] Moving alien armor components from bag to inventory");

            var itemsToMove = targetContainer.Items.Where(CanProcess).ToList();
            foreach (var item in itemsToMove)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Moving {item.Name} to inventory");
                item.MoveToInventory();
                await Task.Delay(200);
            }
        }

        private AlienArmorStage DetectAlienArmorStageFromInventory()
        {
            // Check for armor combination opportunity first
            if (HasCombinableArmorPieces())
                return AlienArmorStage.CombineArmor;

            // Check for single completed armor pieces (final products)
            var completedArmorPieces = Inventory.Items.Where(i => IsBasicArmorPiece(i.Name)).ToList();
            if (completedArmorPieces.Any() && !HasCombinableArmorPieces())
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Detected completed armor piece(s): {string.Join(", ", completedArmorPieces.Select(i => i.Name))}");
                return AlienArmorStage.Completed;
            }

            // Check for each stage in reverse order (most processed first)
            // Step 8: Check if we have BOTH Formatted Viralbot Armor AND Lead Viralbots
            bool hasFormattedArmor = Inventory.Items.Any(i => i.Name.Contains("Formatted Viralbot") && IsArmorPiece(i.Name));
            bool hasLeadViralbots = Inventory.Items.Any(i => i.Name.Contains("Lead Viralbots"));

            if (hasFormattedArmor && hasLeadViralbots)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Detected Step 8: Has Formatted Armor AND Lead Viralbots");
                return AlienArmorStage.Step8_FormattedArmor;
            }

            if (Inventory.Items.Any(i => i.Name.Contains("Kyr'Ozch Formatted Viralbot Solution")))
                return AlienArmorStage.Step7_FormattedViralbot;

            // Check for plain Kyr'Ozch Viralbots (not Lead, not Memory-Wiped, not Formatted)
            // This is Step 4 - needs to be processed with DNA Cocktail
            if (Inventory.Items.Any(i => i.Name.Contains("Kyr'Ozch Viralbots") &&
                !i.Name.Contains("Lead") &&
                !i.Name.Contains("Memory-Wiped") &&
                !i.Name.Contains("Formatted")))
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Detected plain Kyr'Ozch Viralbots - starting at Step 4");
                return AlienArmorStage.Step4_DNACocktail;
            }

            if (Inventory.Items.Any(i => i.Name.Contains("DNA Cocktail")))
                return AlienArmorStage.Step4_DNACocktail;

            if (Inventory.Items.Any(i => i.Name.Contains("Generic Kyr'Ozch DNA-Soup")))
                return AlienArmorStage.Step3_DNASoup;

            if (Inventory.Items.Any(i => i.Name.Contains("Kyr'Ozch Bio-Material") &&
                (i.Name.Contains("Mutated") || i.Name.Contains("Pristine"))))
                return AlienArmorStage.Step2_BiomaterialIdentified;

            if (Inventory.Items.Any(i => i.Name.Equals("Solid Clump of Kyr'Ozch Bio-Material", StringComparison.OrdinalIgnoreCase)))
                return AlienArmorStage.Step1_SolidClump;

            return AlienArmorStage.None;
        }

        private async Task ProcessAlienArmorStages(AlienArmorStage currentStage, Container targetContainer)
        {
            var stage = currentStage;
            int maxRetries = 10;
            int retryCount = 0;

            while (stage != AlienArmorStage.None && stage != AlienArmorStage.Completed && retryCount < maxRetries)
            {
                retryCount++;
                bool stepSuccessful = false;
                RecipeUtilities.LogDebug($"[{RecipeName}] Processing stage: {stage} (attempt {retryCount})");

                switch (stage)
                {
                    case AlienArmorStage.Step1_SolidClump:
                        stepSuccessful = await ProcessStep1_IdentifyClump();
                        break;

                    case AlienArmorStage.Step2_BiomaterialIdentified:
                        stepSuccessful = await ProcessStep2_CreateDNASoup();
                        break;

                    case AlienArmorStage.Step3_DNASoup:
                        stepSuccessful = await ProcessStep3_CreateDNACocktail();
                        break;

                    case AlienArmorStage.Step4_DNACocktail:
                        stepSuccessful = await ProcessStep4to6_CreateFormattedSolution();
                        break;

                    case AlienArmorStage.Step7_FormattedViralbot:
                        stepSuccessful = await ProcessStep7_CreateFormattedArmor();
                        break;

                    case AlienArmorStage.Step8_FormattedArmor:
                        stepSuccessful = await ProcessStep8_CreateFinalArmor();
                        break;

                    case AlienArmorStage.CombineArmor:
                        stepSuccessful = await TryCombineArmorPieces();
                        break;
                }

                if (stepSuccessful)
                {
                    await Task.Delay(500);
                    stage = DetectAlienArmorStageFromInventory();
                    RecipeUtilities.LogDebug($"[{RecipeName}] Stage after processing: {stage}");
                }
                else
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step failed or incomplete, stopping processing");
                    break;
                }
            }

            if (stage == AlienArmorStage.Completed)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Alien Armor recipe completed successfully!");
            }
        }

        // Step 1: Kyr'Ozch Structural Analyzer + Solid Clump → Mutated/Pristine Bio-Material
        private async Task<bool> ProcessStep1_IdentifyClump()
        {
            RecipeUtilities.LogDebug($"[{RecipeName}] Step 1: Identifying Solid Clump");

            var analyzer = FindTool("Kyr'Ozch Structural Analyzer");
            var clump = Inventory.Items.FirstOrDefault(i =>
                i.Name.Equals("Solid Clump of Kyr'Ozch Bio-Material", StringComparison.OrdinalIgnoreCase));

            if (analyzer == null || clump == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Missing components for Step 1");
                return false;
            }

            await CombineItems(analyzer, clump);
            return true;
        }

        // Step 2: Uncle Bazzit's Generic Nano-Solvent + Bio-Material → Generic DNA-Soup
        private async Task<bool> ProcessStep2_CreateDNASoup()
        {
            RecipeUtilities.LogDebug($"[{RecipeName}] Step 2: Creating DNA Soup");

            var solvent = FindTool("Uncle Bazzit's Generic Nano-Solvent");
            var bioMaterial = Inventory.Items.FirstOrDefault(i =>
                i.Name.Contains("Kyr'Ozch Bio-Material") &&
                (i.Name.Contains("Mutated") || i.Name.Contains("Pristine")));

            if (solvent == null || bioMaterial == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Missing components for Step 2");
                return false;
            }

            await CombineItems(solvent, bioMaterial);
            return true;
        }

        // Step 3: Essential Human DNA + Generic DNA-Soup → DNA Cocktail
        private async Task<bool> ProcessStep3_CreateDNACocktail()
        {
            RecipeUtilities.LogDebug($"[{RecipeName}] Step 3: Creating DNA Cocktail");

            var humanDNA = FindTool("Essential Human DNA");
            var dnaSoup = Inventory.Items.FirstOrDefault(i => i.Name.Contains("Generic Kyr'Ozch DNA-Soup"));

            if (humanDNA == null || dnaSoup == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Missing components for Step 3");
                return false;
            }

            await CombineItems(humanDNA, dnaSoup);
            return true;
        }

        // Steps 4-6: Process viralbots and combine with DNA Cocktail
        private async Task<bool> ProcessStep4to6_CreateFormattedSolution()
        {
            RecipeUtilities.LogDebug($"[{RecipeName}] Steps 4-6: Creating Formatted Viralbot Solution");

            // Step 4: Atomic Re-Structuralizing Tool + Kyr'Ozch Viralbots → Memory-Wiped Viralbots
            var atomicTool = FindTool("Kyr'Ozch Atomic Re-Structuralizing Tool");
            var viralbots = Inventory.Items.FirstOrDefault(i =>
                i.Name.Contains("Kyr'Ozch Viralbots") && !i.Name.Contains("Lead") &&
                !i.Name.Contains("Memory-Wiped") && !i.Name.Contains("Formatted"));

            if (atomicTool != null && viralbots != null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 4: Memory-wiping viralbots");
                await CombineItems(atomicTool, viralbots);
                await Task.Delay(500);
            }

            // Step 5: Nano Programming Interface + Memory-Wiped Viralbots → Formatted Viralbots
            var nanoInterface = FindTool("Nano Programming Interface");
            var memoryWipedViralbots = Inventory.Items.FirstOrDefault(i => i.Name.Contains("Memory-Wiped Kyr'Ozch Viralbots"));

            if (nanoInterface != null && memoryWipedViralbots != null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 5: Formatting viralbots");
                await CombineItems(nanoInterface, memoryWipedViralbots);
                await Task.Delay(500);
            }

            // Step 6: Formatted Viralbots + DNA Cocktail → Formatted Viralbot Solution
            var formattedViralbots = Inventory.Items.FirstOrDefault(i =>
                i.Name.Contains("Formatted Kyr'Ozch Viralbots") && !i.Name.Contains("Solution"));
            var dnaCocktail = Inventory.Items.FirstOrDefault(i => i.Name.Contains("DNA Cocktail"));

            if (formattedViralbots != null && dnaCocktail != null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Step 6: Creating Formatted Viralbot Solution");
                await CombineItems(formattedViralbots, dnaCocktail);
                return true;
            }

            return false;
        }

        // Step 7: Basic Clothing + Formatted Viralbot Solution → Formatted Viralbot Armor
        private async Task<bool> ProcessStep7_CreateFormattedArmor()
        {
            RecipeUtilities.LogDebug($"[{RecipeName}] Step 7: Creating Formatted Viralbot Armor");

            var basicClothing = Inventory.Items.FirstOrDefault(i =>
                i.Name.StartsWith("Basic ") &&
                (i.Name.Contains("Fashion") || i.Name.Contains("Footwear") ||
                 i.Name.Contains("Gloves") || i.Name.Contains("Headwear") ||
                 i.Name.Contains("Legwear") || i.Name.Contains("Sleeve")));

            var formattedSolution = Inventory.Items.FirstOrDefault(i =>
                i.Name.Contains("Kyr'Ozch Formatted Viralbot Solution"));

            if (basicClothing == null || formattedSolution == null)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Missing components for Step 7");
                return false;
            }

            await CombineItems(basicClothing, formattedSolution);
            return true;
        }

        // Step 8: Lead Viralbots + Formatted Viralbot Armor → Final Armor
        private async Task<bool> ProcessStep8_CreateFinalArmor()
        {
            RecipeUtilities.LogDebug($"[{RecipeName}] Step 8: Creating Final Armor");

            // Find all formatted armor pieces and lead viralbots
            var formattedArmor = Inventory.Items.Where(i =>
                i.Name.Contains("Formatted Viralbot") && !i.Name.Contains("Solution")).ToList();

            var leadViralbots = Inventory.Items.Where(i => i.Name.Contains("Lead Viralbots")).ToList();

            if (!formattedArmor.Any() || !leadViralbots.Any())
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Missing components for Step 8");
                return false;
            }

            // Match lead viralbots to formatted armor using intelligent QL matching
            var matches = MatchViralbotsToArmor(formattedArmor, leadViralbots);

            foreach (var match in matches)
            {
                RecipeUtilities.LogDebug($"[{RecipeName}] Combining {match.LeadViralbots.Name} with {match.FormattedArmor.Name}");
                await CombineItems(match.LeadViralbots, match.FormattedArmor);
                await Task.Delay(500);
            }

            return matches.Any();
        }

        // Intelligent QL matching for lead viralbots and formatted armor
        private List<(Item LeadViralbots, Item FormattedArmor)> MatchViralbotsToArmor(
            List<Item> formattedArmor, List<Item> leadViralbots)
        {
            var matches = new List<(Item, Item)>();

            // Sort formatted armor by QL descending (process highest QL first)
            var sortedArmor = formattedArmor.OrderByDescending(i => GetItemQL(i)).ToList();
            var availableViralbots = new List<Item>(leadViralbots);

            foreach (var armor in sortedArmor)
            {
                int armorQL = GetItemQL(armor);
                int requiredViralbotsQL = (int)(armorQL * 0.8); // 80% rule

                // Find the lowest QL viralbots that still meets the requirement
                var matchingViralbots = availableViralbots
                    .Where(v => GetItemQL(v) >= requiredViralbotsQL)
                    .OrderBy(v => GetItemQL(v))
                    .FirstOrDefault();

                if (matchingViralbots != null)
                {
                    matches.Add((matchingViralbots, armor));
                    availableViralbots.Remove(matchingViralbots);
                    RecipeUtilities.LogDebug($"[{RecipeName}] Matched QL{GetItemQL(matchingViralbots)} viralbots to QL{armorQL} armor");
                }
                else
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] No suitable viralbots found for QL{armorQL} armor (need QL{requiredViralbotsQL}+)");
                }
            }

            return matches;
        }

        // Try to combine two armor pieces into combined armor
        private async Task<bool> TryCombineArmorPieces()
        {
            RecipeUtilities.LogDebug($"[{RecipeName}] Checking for combinable armor pieces");

            // Group armor pieces by slot
            var armorBySlot = new Dictionary<string, List<Item>>();

            foreach (var item in Inventory.Items)
            {
                var slot = GetArmorSlot(item.Name);
                if (slot != null && IsBasicArmorPiece(item.Name))
                {
                    if (!armorBySlot.ContainsKey(slot))
                        armorBySlot[slot] = new List<Item>();
                    armorBySlot[slot].Add(item);
                }
            }

            bool anyCombined = false;

            // Try to combine armor pieces in each slot
            foreach (var slot in armorBySlot.Keys)
            {
                var pieces = armorBySlot[slot];
                if (pieces.Count < 2) continue;

                // Try to find valid combinations
                var combinations = FindValidArmorCombinations(pieces);

                foreach (var combo in combinations)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Combining {combo.Source.Name} + {combo.Target.Name} → {combo.ResultType}");

                    // Track the IDs of items being consumed
                    int sourceId = combo.Source.Id;
                    int targetId = combo.Target.Id;
                    RecipeUtilities.LogDebug($"[{RecipeName}] Marking items as consumed: Source ID={sourceId}, Target ID={targetId}");

                    await CombineItems(combo.Source, combo.Target);

                    // Mark both items as consumed so they won't be processed again
                    _consumedItemIds.Add(sourceId);
                    _consumedItemIds.Add(targetId);
                    RecipeUtilities.LogDebug($"[{RecipeName}] Items marked as consumed in combination");

                    await Task.Delay(500);
                    anyCombined = true;
                }
            }

            return anyCombined;
        }

        private List<ArmorCombination> FindValidArmorCombinations(List<Item> armorPieces)
        {
            var combinations = new List<ArmorCombination>();

            for (int i = 0; i < armorPieces.Count; i++)
            {
                for (int j = i + 1; j < armorPieces.Count; j++)
                {
                    var piece1 = armorPieces[i];
                    var piece2 = armorPieces[j];

                    var type1 = GetArmorType(piece1.Name);
                    var type2 = GetArmorType(piece2.Name);

                    if (type1 == null || type2 == null) continue;

                    // CRITICAL: Check BOTH possible orders since order matters in AO
                    // Try type1 + type2
                    var resultType = GetCombinedArmorType(type1, type2);
                    Item source, target;

                    if (resultType != null)
                    {
                        // type1 is source, type2 is target
                        source = piece1;
                        target = piece2;
                    }
                    else
                    {
                        // Try type2 + type1
                        resultType = GetCombinedArmorType(type2, type1);
                        if (resultType == null) continue; // Neither order works

                        // type2 is source, type1 is target
                        source = piece2;
                        target = piece1;
                    }

                    // Check 80% QL rule: source QL must be >= 80% of target QL
                    int sourceQL = GetItemQL(source);
                    int targetQL = GetItemQL(target);
                    int requiredSourceQL = (int)(targetQL * 0.8);

                    if (sourceQL >= requiredSourceQL)
                    {
                        combinations.Add(new ArmorCombination
                        {
                            Source = source,
                            Target = target,
                            ResultType = resultType
                        });
                    }
                    else
                    {
                        RecipeUtilities.LogDebug($"[{RecipeName}] Cannot combine {source.Name} QL{sourceQL} + {target.Name} QL{targetQL}: Source QL too low (need {requiredSourceQL})");
                    }
                }
            }

            return combinations;
        }

        // Helper methods
        private bool IsArmorPiece(string itemName)
        {
            return ArmorSlots.Any(slot => itemName.Contains(slot));
        }

        private bool IsBasicArmorPiece(string itemName)
        {
            // Check if it's a basic armor type (not combined)
            if (itemName.Contains("Combined")) return false;
            return ArmorTypes.Any(type => itemName.Contains(type)) && IsArmorPiece(itemName);
        }

        private string GetArmorType(string itemName)
        {
            return ArmorTypes.FirstOrDefault(type => itemName.Contains(type));
        }

        private string GetArmorSlot(string itemName)
        {
            return ArmorSlots.FirstOrDefault(slot => itemName.Contains(slot));
        }

        private string GetCombinedArmorType(string type1, string type2)
        {
            foreach (var recipe in CombinedArmorRecipes)
            {
                var types = recipe.Value;
                // CRITICAL: Order matters! types[0] is SOURCE, types[1] is TARGET
                // Only match if type1 is source and type2 is target
                if (types[0] == type1 && types[1] == type2)
                {
                    return recipe.Key;
                }
            }
            return null;
        }

        private bool HasCombinableArmorPieces()
        {
            var armorPieces = Inventory.Items.Where(i => IsBasicArmorPiece(i.Name)).ToList();

            // Group by slot
            var bySlot = armorPieces.GroupBy(i => GetArmorSlot(i.Name));

            // Check if any slot has 2+ pieces that can combine
            foreach (var group in bySlot)
            {
                if (group.Count() >= 2)
                {
                    var pieces = group.ToList();
                    for (int i = 0; i < pieces.Count; i++)
                    {
                        for (int j = i + 1; j < pieces.Count; j++)
                        {
                            var type1 = GetArmorType(pieces[i].Name);
                            var type2 = GetArmorType(pieces[j].Name);
                            if (GetCombinedArmorType(type1, type2) != null)
                                return true;
                        }
                    }
                }
            }

            return false;
        }

        private int GetItemQL(Item item)
        {
            try
            {
                // Try to get QL from item properties
                var (stackCount, qualityLevel) = Core.ItemTracker.ExtractItemProperties(item);
                if (qualityLevel > 0)
                    return qualityLevel;

                // Fallback to item.Ql if available
                return item.Ql;
            }
            catch
            {
                return 1; // Default QL
            }
        }

        public override RecipeAnalysisResult AnalyzeItems(List<Item> items)
        {
            var stage = DetectAlienArmorStageFromItems(items);

            return new RecipeAnalysisResult
            {
                CanProcess = stage != AlienArmorStage.None,
                ProcessableItemCount = items.Where(CanProcess).Count(),
                Stage = stage.ToString(),
                Description = $"Alien Armor Stage: {stage} - {items.Where(CanProcess).Count()} components found"
            };
        }

        private AlienArmorStage DetectAlienArmorStageFromItems(List<Item> items)
        {
            // Similar to DetectAlienArmorStageFromInventory but works on a list
            if (items.Any(i => i.Name.Contains("Formatted Viralbot") && IsArmorPiece(i.Name)))
                return AlienArmorStage.Step8_FormattedArmor;

            if (items.Any(i => i.Name.Contains("Kyr'Ozch Formatted Viralbot Solution")))
                return AlienArmorStage.Step7_FormattedViralbot;

            // Check for plain Kyr'Ozch Viralbots (not Lead, not Memory-Wiped, not Formatted)
            // This is Step 4 - needs to be processed with DNA Cocktail
            if (items.Any(i => i.Name.Contains("Kyr'Ozch Viralbots") &&
                !i.Name.Contains("Lead") &&
                !i.Name.Contains("Memory-Wiped") &&
                !i.Name.Contains("Formatted")))
                return AlienArmorStage.Step4_DNACocktail;

            if (items.Any(i => i.Name.Contains("DNA Cocktail")))
                return AlienArmorStage.Step4_DNACocktail;

            if (items.Any(i => i.Name.Contains("Generic Kyr'Ozch DNA-Soup")))
                return AlienArmorStage.Step3_DNASoup;

            if (items.Any(i => i.Name.Contains("Kyr'Ozch Bio-Material") &&
                (i.Name.Contains("Mutated") || i.Name.Contains("Pristine"))))
                return AlienArmorStage.Step2_BiomaterialIdentified;

            if (items.Any(i => i.Name.Equals("Solid Clump of Kyr'Ozch Bio-Material", StringComparison.OrdinalIgnoreCase)))
                return AlienArmorStage.Step1_SolidClump;

            return AlienArmorStage.None;
        }

        private class ArmorCombination
        {
            public Item Source { get; set; }
            public Item Target { get; set; }
            public string ResultType { get; set; }
        }
    }
}
