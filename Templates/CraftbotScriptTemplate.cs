using System;
using System.IO;
using AOSharp.Clientless;

namespace Craftbot.Templates
{
    public static class CraftbotScriptTemplate
    {
        public static string HelpWindow()
        {
            try
            {
                string template = GetTemplateContent("CraftbotHelpTemplate");
                if (!string.IsNullOrEmpty(template))
                {
                    // Simple string replacement for botname
                    return template.Replace("{{ botname }}", Client.CharacterName);
                }
                else
                {
                    return CreateFallbackHelp();
                }
            }
            catch
            {
                // Silent error handling
                return CreateFallbackHelp();
            }
        }

        public static string CategoryHelpWindow(string templateName)
        {
            try
            {
                string template = GetTemplateContent(templateName);
                if (!string.IsNullOrEmpty(template))
                {
                    // Simple string replacement for botname
                    return template.Replace("{{ botname }}", Client.CharacterName);
                }
                else
                {
                    return CreateFallbackHelp();
                }
            }
            catch
            {
                // Silent error handling
                return CreateFallbackHelp();
            }
        }

        private static string CreateFallbackHelp()
        {
            string botName = Client.CharacterName;
            return $@"<a href=""text://
<font color=#00D4FF>=== GEM CUTTER PROCESSING ===</font>

<font color=#FFFF00>Gems & Pearls (35 types):</font>
<font color=#FFFFFF>• Almandine, Amber, Aquamarine, Balas ruby</font>
<font color=#FFFFFF>• Black opal, Blue Pearl by Conner, Chrysoberyl</font>
<font color=#FFFFFF>• Coral, Crystal Sphere, Demantoid, Diamond</font>
<font color=#FFFFFF>• Ember, Ember Sphere, Emerald, Fire opal</font>
<font color=#FFFFFF>• Flawless Spring Crystal, Gem, Gold 2 Sphere Pearl</font>
<font color=#FFFFFF>• High Quality Silver Onyx, Hot Stone, Jet</font>
<font color=#FFFFFF>• Pearl, Pearl of Rubi-Ka, Red beryl, Ruby</font>
<font color=#FFFFFF>• Ruby Pearl, Rubi-Ka Ruby, Sapphire, Silver Pearl</font>
<font color=#FFFFFF>• Soul Fragment, Star Ruby, Topaz</font>
<font color=#FFFFFF>• Water opal, White opal</font>

<img src=tdb://id:GFX_GUI_FRIENDLIST_SPLITTER>

<font color=#00D4FF>=== IMPLANT CRAFTING ===</font>

<font color=#FFFF00>IMPORTANT WARNING:</font>
<font color=#FF0000>- DO NOT put duplicate implants in the same trade</font>
<font color=#FF0000>- DO NOT put duplicate clusters in the same trade</font>
<font color=#FF0000>- Duplicates will cause the craft to FAIL completely</font>

<img src=tdb://id:GFX_GUI_FRIENDLIST_SPLITTER>

<font color=#00D4FF>=== AVAILABLE COMMANDS ===</font>

<a href='chatcmd:///tell {botName} help'>help</a><font color=#00BDBD> - Show this processing guide</font>
<a href='chatcmd:///tell {botName} trade'>trade</a><font color=#00BDBD> - Open trade window (must be nearby)</font>

<img src=tdb://id:GFX_GUI_FRIENDLIST_SPLITTER>

<font color=#FFFF00>HOW TO USE:</font>
<font color=#FFFFFF>1. Come stand next to me</font>
<font color=#FFFFFF>2. Send: /tell {botName} trade</font>
<font color=#FFFFFF>3. Put items in bags and trade them to me</font>
<font color=#FFFFFF>4. I will process them and trade back the results</font>

<img src=tdb://id:GFX_GUI_FRIENDLIST_SPLITTER>

<font color=#888888>Craftbot V1.0 - Automated Processing Bot</font>
"">Craftbot Help Menu</a>";
        }

        private static string GetTemplateContent(string templateFile)
        {
            try
            {
                // First try the new config/help-templates folder
                string configTemplatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "help-templates", $"{templateFile}.txt");
                if (File.Exists(configTemplatePath))
                {
                    return File.ReadAllText(configTemplatePath);
                }

                // Fallback to old Templates folder for backward compatibility
                string templatePath = $"{Craftbot.PluginDir}\\Templates\\{templateFile}.txt";
                if (File.Exists(templatePath))
                {
                    return File.ReadAllText(templatePath);
                }
                else
                {
                    // Silent template loading //$"[TEMPLATE] Template file not found: {templatePath}");
                    return null;
                }
            }
            catch (Exception)
            {
                // Silent template loading //$"[TEMPLATE] Error loading template {templateFile}: {ex.Message}");
                return null;
            }
        }
    }
}
