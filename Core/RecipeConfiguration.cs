using System.Collections.Generic;

namespace Craftbot.Core
{
    /// <summary>
    /// Configuration for all recipes - allows adding new recipes without code changes
    /// </summary>
    public class RecipeConfiguration
    {
        public List<ConfigurableRecipe> Recipes { get; set; } = new List<ConfigurableRecipe>();
        public RecipeSettings Settings { get; set; } = new RecipeSettings();

        public RecipeConfiguration()
        {
            // Add default recipes
            InitializeDefaultRecipes();
        }

        private void InitializeDefaultRecipes()
        {
            Recipes.AddRange(new[]
            {
                new ConfigurableRecipe
                {
                    Name = "Robot Brain",
                    Description = "Multi-step recipe to create Personalized Basic Robot Brain",
                    Enabled = true,
                    Type = "MultiStep",
                    Steps = new List<RecipeStep>
                    {
                        new RecipeStep
                        {
                            StepNumber = 1,
                            Description = "Convert Robot Junk to Nano Sensor",
                            Tool = "Screwdriver",
                            InputItem = "Robot Junk",
                            OutputItem = "Nano Sensor",
                            ProcessingTime = 1000
                        },
                        new RecipeStep
                        {
                            StepNumber = 2,
                            Description = "Convert Nano Sensor to Basic Robot Brain",
                            Tool = "Bio Analyzing Computer",
                            InputItem = "Nano Sensor",
                            OutputItem = "Basic Robot Brain",
                            ProcessingTime = 1000
                        },
                        new RecipeStep
                        {
                            StepNumber = 3,
                            Description = "Convert Basic Robot Brain to Personalized Basic Robot Brain",
                            Tool = "MasterComm - Personalization Device",
                            InputItem = "Basic Robot Brain",
                            OutputItem = "Personalized Basic Robot Brain",
                            ProcessingTime = 1000
                        }
                    },
                    ProcessableItems = new List<string> { "Robot Junk", "Nano Sensor", "Basic Robot Brain" },
                    RequiredTools = new List<string> { "Screwdriver", "Bio Analyzing Computer", "MasterComm - Personalization Device" },
                    HelpText = "Robot Brain recipe: Screwdriver + Robot Junk = Nano Sensor, Bio Analyzing Computer + Nano Sensor = Basic Robot Brain, MasterComm - Personalization Device + Basic Robot Brain = Personalized Basic Robot Brain",
                    SupportsMultipleSets = true
                },
                new ConfigurableRecipe
                {
                    Name = "Plasma",
                    Description = "Convert Blood Plasma items",
                    Enabled = true,
                    Type = "Simple",
                    Steps = new List<RecipeStep>
                    {
                        new RecipeStep
                        {
                            StepNumber = 1,
                            Description = "Process Blood Plasma",
                            Tool = "Plasma Processing Tool",
                            InputItem = "Blood Plasma",
                            OutputItem = "Processed Blood Plasma",
                            ProcessingTime = 300
                        }
                    },
                    ProcessableItems = new List<string> { "Blood Plasma" },
                    RequiredTools = new List<string> { "Plasma Processing Tool" },
                    HelpText = "Plasma recipe: Process various Blood Plasma items",
                    SupportsMultipleSets = true
                },
                new ConfigurableRecipe
                {
                    Name = "Stalker Helmet",
                    Description = "Create Stalker Helmet from Stalker Carapace",
                    Enabled = true,
                    Type = "Simple",
                    Steps = new List<RecipeStep>
                    {
                        new RecipeStep
                        {
                            StepNumber = 1,
                            Description = "Create Stalker Helmet",
                            Tool = "MasterComm - Personalization Device",
                            InputItem = "Stalker Carapace",
                            OutputItem = "Stalker Helmet",
                            ProcessingTime = 500
                        }
                    },
                    ProcessableItems = new List<string> { "Stalker Carapace" },
                    RequiredTools = new List<string> { "MasterComm - Personalization Device" },
                    HelpText = "Stalker Helmet recipe: MasterComm - Personalization Device + Stalker Carapace = Stalker Helmet",
                    SupportsMultipleSets = true
                }
            });
        }
    }

    public class ConfigurableRecipe
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Enabled { get; set; } = true;
        public string Type { get; set; } // "Simple", "MultiStep", "Complex"
        public List<RecipeStep> Steps { get; set; } = new List<RecipeStep>();
        public List<string> ProcessableItems { get; set; } = new List<string>();
        public List<string> RequiredTools { get; set; } = new List<string>();
        public string HelpText { get; set; }
        public bool SupportsMultipleSets { get; set; } = false;
        public int MaxSetsPerTrade { get; set; } = 10;
        public List<string> Categories { get; set; } = new List<string>();
        public Dictionary<string, object> CustomProperties { get; set; } = new Dictionary<string, object>();
    }

    public class RecipeStep
    {
        public int StepNumber { get; set; }
        public string Description { get; set; }
        public string Tool { get; set; }
        public string InputItem { get; set; }
        public string OutputItem { get; set; }
        public int ProcessingTime { get; set; } = 1000; // milliseconds
        public bool ToolConsumed { get; set; } = false; // whether the tool is consumed in this step
        public List<string> AlternativeTools { get; set; } = new List<string>();
        public Dictionary<string, object> StepProperties { get; set; } = new Dictionary<string, object>();
    }

    public class RecipeSettings
    {
        public bool EnableHotReload { get; set; } = true;
        public int DefaultProcessingTime { get; set; } = 1000;
        public int MaxConcurrentRecipes { get; set; } = 5;
        public bool LogRecipeExecution { get; set; } = true;
        public bool AllowCustomRecipes { get; set; } = true;
        public string RecipeDataPath { get; set; } = "config/recipes/";
        public bool ValidateRecipesOnLoad { get; set; } = true;
        public int RecipeTimeoutSeconds { get; set; } = 30;
    }
}
