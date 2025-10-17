using System;
using System.IO;
using AOSharp.Core;
using AOSharp.Core.UI;

namespace Craftbot.Templates
{
    public static class BankbotHelpTemplate
    {
        public static string HelpWindow()
        {
            try
            {
                string template = GetTemplateContent("BankbotHelpTemplate");
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

        private static string GetTemplateContent(string templateName)
        {
            try
            {
                // Try to find the template file - prioritize config folder
                string[] possiblePaths = {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "help-templates", $"{templateName}.txt"),
                    Path.Combine(Craftbot.PluginDir, "Templates", $"{templateName}.txt"),
                    Path.Combine(Craftbot.PluginDir, "bin", "Debug", "Templates", $"{templateName}.txt"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", $"{templateName}.txt"),
                    $"{templateName}.txt"
                };

                foreach (string path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        return File.ReadAllText(path);
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static string CreateFallbackHelp()
        {
            return $@"<a href=""text://
<font color=#00D4FF>Bankbot V1.0 - Item Storage Bot</font>

<font color=#00D4FF>=== COMMANDS ===</font>

help - Show this help
list - List stored items

<font color=#00D4FF>=== USAGE ===</font>

To store items: Trade them to me
To get items: Use 'list' command and click 'Get'

Bot: {Client.CharacterName}
"">";
        }
    }
}
