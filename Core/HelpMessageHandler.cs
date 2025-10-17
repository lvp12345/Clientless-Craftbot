using System;
using System.Linq;
using AOSharp.Clientless;
using Craftbot.Modules;

namespace Craftbot.Core
{
    /// <summary>
    /// Handles all help-related commands in the unified message system
    /// </summary>
    public class HelpMessageHandler : IMessageHandler
    {
        public void HandleMessage(string senderName, MessageInfo messageInfo)
        {
            try
            {
                PrivateMessageModule.LogDebug($"[HELP HANDLER] Processing help request from {senderName}");

                string windowContent;
                string linkText;

                // Check if a specific category was requested
                if (messageInfo.Arguments.Length > 0)
                {
                    string category = messageInfo.Arguments[0].ToLower();
                    windowContent = CreateCategoryTemplate(category);
                    linkText = GetCategoryLinkText(category);
                    PrivateMessageModule.LogDebug($"[HELP HANDLER] Sending category help for: {category}");
                }
                else
                {
                    // Default main help window
                    windowContent = CreateMainTemplate();
                    linkText = "Craftbot Help Menu";
                    PrivateMessageModule.LogDebug($"[HELP HANDLER] Sending main help window");
                }

                // Create the proper window link format
                string windowLink = $@"<a href=""text://{windowContent}"">{linkText}</a>";

                // Send response through unified system
                UnifiedMessageHandler.SendResponse(senderName, windowLink, MessageType.Info);
            }
            catch (Exception ex)
            {
                PrivateMessageModule.LogDebug($"[HELP HANDLER] Error: {ex.Message}");
                UnifiedMessageHandler.SendResponse(senderName, "Error showing help. Please try again.", MessageType.Error);
            }
        }

        private string CreateMainTemplate()
        {
            try
            {
                // Use the working template system that VTE uses
                string template = Templates.CraftbotScriptTemplate.HelpWindow();
                if (!string.IsNullOrEmpty(template))
                {
                    int startIndex = template.IndexOf("<a href=\"text://") + 16;
                    int endIndex = template.LastIndexOf("\">");

                    if (startIndex > 15 && endIndex > startIndex)
                    {
                        string content = template.Substring(startIndex, endIndex - startIndex);
                        return content;
                    }
                }

                return CreateFallbackHelp();
            }
            catch (Exception ex)
            {
                PrivateMessageModule.LogDebug($"[HELP HANDLER] Error loading main template: {ex.Message}");
                return CreateFallbackHelp();
            }
        }

        private string CreateCategoryTemplate(string category)
        {
            try
            {
                // Use the working template system that VTE uses
                string templateName = GetTemplateName(category);
                if (!string.IsNullOrEmpty(templateName))
                {
                    string template = Templates.CraftbotScriptTemplate.CategoryHelpWindow(templateName);
                    if (!string.IsNullOrEmpty(template))
                    {
                        // Extract content between <a href="text:// and ">
                        int startIndex = template.IndexOf("<a href=\"text://") + 16;
                        int endIndex = template.LastIndexOf("\">");

                        if (startIndex > 15 && endIndex > startIndex)
                        {
                            string content = template.Substring(startIndex, endIndex - startIndex);
                            return content;
                        }
                    }
                }

                // Log that template was not found
                PrivateMessageModule.LogDebug($"[HELP HANDLER] Template not found for category: {category}");

                return $"<font color=#FF0000>Help template not found for category: {category}</font>";
            }
            catch (Exception ex)
            {
                PrivateMessageModule.LogDebug($"[HELP HANDLER] Error loading category template: {ex.Message}");
                return $"<font color=#FF0000>Error loading help for category: {category} - {ex.Message}</font>";
            }
        }

        private string GetTemplateName(string category)
        {
            switch (category.ToLower())
            {
                case "gemcutter": return "gemcutter";
                case "smelting": return "smelting";
                case "plasma": return "plasma";
                case "pitdemon": return "pitdemon";
                case "frederickson": return "frederickson";
                case "nanocrystal": return "nanocrystal";
                case "ice": return "ice";
                case "robotjunk": return "robotjunk";
                case "vte": return "vte";
                case "pbpattern": return "pbpattern";
                case "ringofpower": return "ringofpower";
                case "clumps": return "clumps";
                case "alienarmor": return "alienarmor";
                case "taraarmor": return "taraarmor";
                case "mantisarmor": return "mantisarmor";
                case "crawler": return "crawler";
                case "carbarmor": return "carbarmor";
                case "implants": return "implants";
                case "trimmer": return "trimmer";
                case "devalossleeve": return "devalossleeve";
                case "perenniumweapons": return "perenniumweapons";
                case "hackgrafts": return "hackgrafts";
                case "sealedweapons": return "sealedweapons";
                case "robotbrain": return "RobotBrainHelpTemplate";
                case "aibiotechrodrings": return "ai-biotech-rod-rings";
                case "stalkerhelmet": return "stalkerhelmet";
                case "barterarmor": return "barterarmor";
                case "niznosbombblaster": return "niznosbombblaster";
                case "salabimshotgun": return "salabimshotgun";
                case "crepusculeleatherarmor": return "crepusculeleatherarmor";
                case "treatmentlibrary": return "treatmentlibrary";
                case "treatlib": return "treatmentlibrary";
                default: return null;
            }
        }

        private string GetCategoryLinkText(string category)
        {
            switch (category.ToLower())
            {
                case "plasma":
                    return "Plasma Processing Help";
                case "pitdemon":
                    return "Pit Demon Heart Processing Help";
                case "frederickson":
                    return "Frederickson Sleeves De-hacking Help";
                case "nanocrystal":
                    return "Nano Crystal Repair Help";
                case "pearl":
                case "gems":
                    return "Gem Cutting Help";
                case "ice":
                    return "ICE Processing Help";
                case "smelting":
                    return "Smelting Help";
                case "vte":
                    return "VTE Creation Help";
                case "pbpattern":
                    return "PB Pattern Help";
                case "carbarmor":
                    return "Carb Armor Help";
                case "clumps":
                    return "Clump Processing Help";
                case "alienarmor":
                    return "Alien Armor Processing Help";
                case "implants":
                    return "Implant Enhancement Help";
                case "mantisarmor":
                    return "Mantis Armor Help";
                case "taraarmor":
                    return "Tara Armor Help";
                case "crawler":
                    return "Crawler Armor Help";
                case "robotjunk":
                    return "Robot Junk Help";
                case "trimmer":
                    return "Trimmer Processing Help";
                case "devalossleeve":
                    return "De'Valos Sleeve Help";
                case "perenniumweapons":
                    return "Perennium Weapons Help";
                case "hackgrafts":
                    return "Hack Grafts Help";
                case "sealedweapons":
                    return "Sealed Weapons Help";
                case "robotbrain":
                    return "Robot Brain Help";
                case "aibiotechrodrings":
                    return "AI Biotech Rod Rings Help";
                case "stalkerhelmet":
                    return "Stalker Helmet Help";
                case "barterarmor":
                    return "Barter Armor Help";
                case "niznosbombblaster":
                    return "Nizno's Bomb Blaster Help";
                case "salabimshotgun":
                    return "Salabim Shotgun Help";
                case "crepusculeleatherarmor":
                    return "Crepuscule Leather Armor Help";
                case "treatmentlibrary":
                case "treatlib":
                    return "Treatment Library Help";
                default:
                    return "Craftbot Help";
            }
        }

        private string CreateFallbackHelp()
        {
            string botName = Client.CharacterName;
            return $@"
<font color=#00D4FF>Craftbot Help Menu</font>

<font color=#FFFFFF>Send: /tell {botName} trade to start trading</font>

<font color=#00D4FF>Craftbot V1.0 - Automated Craft Bot</font>
<font color=#00D4FF>Created by Code</font>";
        }
    }
}
