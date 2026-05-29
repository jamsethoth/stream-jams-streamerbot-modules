using System;
using Newtonsoft.Json.Linq;

public class CPHInline
{
    private const string ConfigGlobal = "gameCounters.config";
    private const string CurrentGameKeyGlobal = "gameCounters.currentGame.key";
    private const string CurrentGameNameGlobal = "gameCounters.currentGame.name";
    private const string CurrentGameSourceGlobal = "gameCounters.currentGame.source";
    private const string CurrentGameUpdatedUtcGlobal = "gameCounters.currentGame.updatedUtc";
    private const string CurrentGameTwitchIdGlobal = "gameCounters.currentGame.twitchGameId";
    private const string ManualLockUntilGlobal = "gameCounters.currentGame.manualLockUntilUtc";

    // Build-time placeholder. Do not paste this source directly into Streamer.bot.
    // tools/streamerbot_import/build_module_import.py replaces the quoted token
    // below with the JSON file named by module.json's defaultConfig field before
    // writing the generated .sb import.
    private const string DefaultConfigJsonBuildPlaceholder = "__STREAMERBOT_MODULE_DEFAULT_CONFIG_JSON__";

    public bool Execute()
    {
        EnsureGlobal(ConfigGlobal, DefaultConfigJson());

        string fallbackKey = "uncategorized";
        string fallbackName = "Uncategorized";
        if (TryLoadConfig(out JObject config))
        {
            JObject currentGame = config["currentGame"] as JObject;
            fallbackKey = SanitizeKey(GetString(currentGame, "fallbackKey", fallbackKey));
            fallbackName = GetString(currentGame, "fallbackName", fallbackName);
        }

        EnsureGlobal(CurrentGameKeyGlobal, fallbackKey);
        EnsureGlobal(CurrentGameNameGlobal, fallbackName);
        EnsureGlobal(CurrentGameSourceGlobal, "default");
        EnsureGlobal(CurrentGameUpdatedUtcGlobal, DateTime.UtcNow.ToString("o"));
        EnsureGlobal(CurrentGameTwitchIdGlobal, "");
        EnsureGlobal(ManualLockUntilGlobal, "");

        CPH.LogInfo("[CGC] Default globals are ready. Edit gameCounters.config to add counters, aliases, templates, and Twitch sync settings.");
        return true;
    }

    private void EnsureGlobal(string name, string defaultValue)
    {
        string current = CPH.GetGlobalVar<string>(name, true);
        if (string.IsNullOrWhiteSpace(current))
        {
            CPH.SetGlobalVar(name, defaultValue, true);
        }
    }

    private bool TryLoadConfig(out JObject config)
    {
        config = null;
        string configJson = CPH.GetGlobalVar<string>(ConfigGlobal, true);
        if (string.IsNullOrWhiteSpace(configJson))
        {
            return false;
        }

        try
        {
            config = JObject.Parse(configJson);
            return true;
        }
        catch (Exception ex)
        {
            CPH.LogError($"[CGC] Invalid JSON in '{ConfigGlobal}': {ex.Message}");
            return false;
        }
    }

    private string GetString(JObject obj, string key, string defaultValue)
    {
        if (obj == null || obj[key] == null)
        {
            return defaultValue;
        }

        string value = obj[key].ToString();
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
    }

    private string SanitizeKey(string value)
    {
        string lowered = (value ?? "").Trim().ToLowerInvariant();
        string sanitized = System.Text.RegularExpressions.Regex.Replace(lowered, @"[^a-z0-9_]+", "_").Trim('_');
        return string.IsNullOrWhiteSpace(sanitized) ? "uncategorized" : sanitized;
    }

    private string DefaultConfigJson()
    {
        return DefaultConfigJsonBuildPlaceholder;
    }
}
