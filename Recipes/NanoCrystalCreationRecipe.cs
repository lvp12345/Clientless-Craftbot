using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AOSharp.Clientless;

namespace Craftbot.Recipes
{
    /// <summary>
    /// Handles Nano Crystal Creation - a complex 7-step recipe with two different processes
    /// Common Process: For most instruction discs
    /// Alternative Process: For newer instruction discs (uses Prepared Program Crystal instead of school-specific)
    /// Supports starting/finishing from any step of the process
    /// </summary>
    public class NanoCrystalCreationRecipe : BaseRecipeProcessor
    {
        public override string RecipeName => "Nano Crystal Creation";

        // Step detection patterns
        private static readonly string[] InstructionDiscPatterns = { "Instruction Disc" };
        private static readonly string[] SymbolLibraryPatterns = { "Symbol Library" };
        private static readonly string[] CompiledAlgorithmPatterns = { "Compiled Algorithm" };
        private static readonly string[] PhotonParticleEmitterPatterns = { "Photon Particle Emitter" };
        private static readonly string[] ProgrammedPhotonPatterns = { "Programmed Photon Particle Emitter" };
        private static readonly string[] CarbonrichRockPatterns = { "Carbonrich Rock" };
        private static readonly string[] CarbonrichOrePatterns = { "Carbonrich Ore" };
        private static readonly string[] PureCarbonCrystalPatterns = { "Pure Carbon Crystal" };
        private static readonly string[] ProgramCrystalPatterns = { "Program Crystal" };
        private static readonly string[] CrystalReflectionPatterns = { "Crystal Reflection Pattern" };
        private static readonly string[] PreparedProgramCrystalPatterns = { "Prepared Program Crystal" };
        private static readonly string[] NanoCrystalPatterns = { "Nano Crystal" };

        public override bool CanProcess(Item item)
        {
            // Can process any intermediate or starting item in the nano crystal creation chain
            bool canProcess =
                InstructionDiscPatterns.Any(p => item.Name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0) ||
                SymbolLibraryPatterns.Any(p => item.Name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0) ||
                CompiledAlgorithmPatterns.Any(p => item.Name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0) ||
                PhotonParticleEmitterPatterns.Any(p => item.Name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0) ||
                ProgrammedPhotonPatterns.Any(p => item.Name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0) ||
                CarbonrichRockPatterns.Any(p => item.Name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0) ||
                CarbonrichOrePatterns.Any(p => item.Name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0) ||
                PureCarbonCrystalPatterns.Any(p => item.Name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0) ||
                ProgramCrystalPatterns.Any(p => item.Name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0) ||
                CrystalReflectionPatterns.Any(p => item.Name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0) ||
                PreparedProgramCrystalPatterns.Any(p => item.Name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0);

            if (canProcess)
            {
                RecipeUtilities.LogDebug($"[NANO CRYSTAL CHECK] Item: '{item.Name}' -> Can process: True");
            }
            return canProcess;
        }

        protected override async Task ProcessRecipeLogic(Item item, Container targetContainer)
        {
            RecipeUtilities.LogDebug($"[{RecipeName}] Processing: {item.Name}");

            // Each item type gets processed with its corresponding tool/component
            if (IsInstructionDisc(item))
            {
                // Step 1: Symbol Library + Instruction Disc = Compiled Algorithm
                var symbolLibrary = Inventory.Items.FirstOrDefault(i =>
                    SymbolLibraryPatterns.Any(p => i.Name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0));
                if (symbolLibrary != null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step 1: {symbolLibrary.Name} + {item.Name}");
                    await CombineItems(symbolLibrary, item);
                }
            }
            else if (IsCompiledAlgorithm(item))
            {
                // Step 2: Photon Particle Emitter + Compiled Algorithm = Programmed Photon Particle Emitter
                var photonEmitter = Inventory.Items.FirstOrDefault(i =>
                    PhotonParticleEmitterPatterns.Any(p => i.Name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0));
                if (photonEmitter != null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step 2: {photonEmitter.Name} + {item.Name}");
                    await CombineItems(photonEmitter, item);
                }
            }
            else if (IsCarbonrichRock(item))
            {
                // Step 3: Jensen Extractor + Carbonrich Rock = Carbonrich Ore
                var jensenExtractor = FindTool("Jensen Personal Ore Extractor");
                if (jensenExtractor != null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step 3: {jensenExtractor.Name} + {item.Name}");
                    await CombineItems(jensenExtractor, item);
                }
            }
            else if (IsCarbonrichOre(item))
            {
                // Step 4: Isotope Separator + Carbonrich Ore = Pure Carbon Crystal
                var isotopeSeparator = FindTool("Isotope Separator");
                if (isotopeSeparator != null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step 4: {isotopeSeparator.Name} + {item.Name}");
                    await CombineItems(isotopeSeparator, item);
                }
            }
            else if (IsPureCarbonCrystal(item))
            {
                // Step 5: Neutron Displacer + Pure Carbon Crystal = Program Crystal
                var neutronDisplacer = FindTool("Neutron Displacer");
                if (neutronDisplacer != null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step 5: {neutronDisplacer.Name} + {item.Name}");
                    await CombineItems(neutronDisplacer, item);
                }
            }
            else if (IsProgramCrystal(item))
            {
                // Step 6: Crystal Reflection Pattern + Program Crystal = Prepared Program Crystal
                var crystalPattern = Inventory.Items.FirstOrDefault(i =>
                    CrystalReflectionPatterns.Any(p => i.Name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0));
                if (crystalPattern != null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step 6: {crystalPattern.Name} + {item.Name}");
                    await CombineItems(crystalPattern, item);
                }
            }
            else if (IsProgrammedPhoton(item))
            {
                // Step 7: Prepared Program Crystal + Programmed Photon Particle Emitter = Nano Crystal
                var preparedCrystal = Inventory.Items.FirstOrDefault(i =>
                    PreparedProgramCrystalPatterns.Any(p => i.Name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0));
                if (preparedCrystal != null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step 7: {preparedCrystal.Name} + {item.Name}");
                    await CombineItems(preparedCrystal, item);
                }
            }
            else if (IsPreparedProgramCrystal(item))
            {
                // Step 7 (alternative order): Prepared Program Crystal + Programmed Photon Particle Emitter = Nano Crystal
                var programmedPhoton = Inventory.Items.FirstOrDefault(i =>
                    ProgrammedPhotonPatterns.Any(p => i.Name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0));
                if (programmedPhoton != null)
                {
                    RecipeUtilities.LogDebug($"[{RecipeName}] Step 7: {item.Name} + {programmedPhoton.Name}");
                    await CombineItems(item, programmedPhoton);
                }
            }
        }

        // Type checking methods
        private bool IsInstructionDisc(Item item) =>
            InstructionDiscPatterns.Any(p => item.Name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0);

        private bool IsCarbonrichRock(Item item) =>
            CarbonrichRockPatterns.Any(p => item.Name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0);

        private bool IsCompiledAlgorithm(Item item) =>
            CompiledAlgorithmPatterns.Any(p => item.Name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0);

        private bool IsCarbonrichOre(Item item) =>
            CarbonrichOrePatterns.Any(p => item.Name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0);

        private bool IsPureCarbonCrystal(Item item) =>
            PureCarbonCrystalPatterns.Any(p => item.Name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0);

        private bool IsProgramCrystal(Item item) =>
            ProgramCrystalPatterns.Any(p => item.Name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0);

        private bool IsProgrammedPhoton(Item item) =>
            ProgrammedPhotonPatterns.Any(p => item.Name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0);

        private bool IsPreparedProgramCrystal(Item item) =>
            PreparedProgramCrystalPatterns.Any(p => item.Name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0);

        public override RecipeAnalysisResult AnalyzeItems(List<Item> items)
        {
            return AnalyzeItemsShared(items, "Nano Crystal Creation");
        }
    }
}

