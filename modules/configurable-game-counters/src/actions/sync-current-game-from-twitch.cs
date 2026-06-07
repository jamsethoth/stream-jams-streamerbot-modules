using System;
using System.Text.RegularExpressions;
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
    private const string PendingGameKeyGlobal = "gameCounters.pendingGame.key";
    private const string PendingGameNameGlobal = "gameCounters.pendingGame.name";
    private const string PendingGameTwitchIdGlobal = "gameCounters.pendingGame.twitchGameId";
    private const string PendingGameUpdatedUtcGlobal = "gameCounters.pendingGame.updatedUtc";

    public bool Execute()
    {
        if (!TryLoadConfig(out JObject config))
        {
            return true;
        }

        JObject currentGame = config["currentGame"] as JObject;
        JObject sync = currentGame == null ? null : currentGame["twitchSync"] as JObject;
        if (!GetBool(sync == null ? null : sync["enabled"], true))
        {
            return true;
        }

        bool gameUpdate;
        if (CPH.TryGetArg("gameUpdate", out gameUpdate) && !gameUpdate)
        {
            if (IsDebugEnabled(config))
            {
                CPH.LogInfo("[CGC] Twitch Stream Update ignored because gameUpdate was false.");
            }
            return true;
        }

        string gameId = GetFirstStringArg("gameId", "gameID", "categoryId", "categoryID");
        string gameName = GetFirstStringArg("gameName", "game", "categoryName", "category");
        if (string.IsNullOrWhiteSpace(gameId) && string.IsNullOrWhiteSpace(gameName))
        {
            CPH.LogWarn("[CGC] Twitch category sync was called without gameId or gameName.");
            return true;
        }

        string mode = GetString(sync, "mode", "autoWithManualLock");
        if (NormalizeKey(mode) == "suggest" || NormalizeKey(mode) == "suggestonly")
        {
            StorePendingGame(BuildTwitchKey(gameId, gameName), CleanGameName(gameName), gameId);
            CPH.LogInfo($"[CGC] Twitch category stored as pending game '{CleanGameName(gameName)}'.");
            return true;
        }

        if (NormalizeKey(mode) == "autoWithManualLock" && ManualLockIsActive())
        {
            StorePendingGame(BuildTwitchKey(gameId, gameName), CleanGameName(gameName), gameId);
            CPH.LogInfo($"[CGC] Twitch category '{CleanGameName(gameName)}' stored as pending because manual lock is active.");
            return true;
        }

        ApplyTwitchSync(config, sync, gameId, gameName);
        return true;
    }

    private void ApplyTwitchSync(JObject config, JObject sync, string gameId, string gameName)
    {
        string key;
        string name;
        ResolveMappedGame(config, sync, gameId, gameName, out key, out name);

        if (IsIgnoredCategory(sync, gameId, gameName))
        {
            JObject currentGame = config["currentGame"] as JObject;
            key = SanitizeKey(GetString(currentGame, "fallbackKey", "uncategorized"));
            name = GetString(currentGame, "fallbackName", "Uncategorized");
        }

        string currentKey = CPH.GetGlobalVar<string>(CurrentGameKeyGlobal, true);
        string currentTwitchId = CPH.GetGlobalVar<string>(CurrentGameTwitchIdGlobal, true);
        if (
            string.Equals(currentKey, key, StringComparison.OrdinalIgnoreCase)
            && string.Equals(currentTwitchId ?? "", gameId ?? "", StringComparison.OrdinalIgnoreCase)
        )
        {
            if (IsDebugEnabled(config))
            {
                CPH.LogInfo($"[CGC] Twitch category '{name}' already matches current game.");
            }
            return;
        }

        CPH.SetGlobalVar(CurrentGameKeyGlobal, key, true);
        CPH.SetGlobalVar(CurrentGameNameGlobal, name, true);
        CPH.SetGlobalVar(CurrentGameSourceGlobal, "twitch", true);
        CPH.SetGlobalVar(CurrentGameUpdatedUtcGlobal, DateTime.UtcNow.ToString("o"), true);
        CPH.SetGlobalVar(CurrentGameTwitchIdGlobal, gameId ?? "", true);
        CPH.SetGlobalVar(ManualLockUntilGlobal, "", true);
        CPH.LogInfo($"[CGC] Current counter game synced from Twitch category to '{name}' ({key}). Counters were not reset.");
    }

    private void ResolveMappedGame(JObject config, JObject sync, string gameId, string gameName, out string key, out string name)
    {
        key = BuildTwitchKey(gameId, gameName);
        name = CleanGameName(gameName);

        JObject mappings = sync == null ? null : sync["categoryMappings"] as JObject;
        if (mappings == null)
        {
            return;
        }

        JObject mapping = null;
        if (!string.IsNullOrWhiteSpace(gameId))
        {
            mapping = mappings[gameId] as JObject;
        }

        if (mapping == null && !string.IsNullOrWhiteSpace(gameName))
        {
            mapping = mappings[NormalizeKey(gameName)] as JObject;
        }

        if (mapping == null)
        {
            return;
        }

        key = SanitizeKey(GetString(mapping, "key", key));
        name = GetString(mapping, "name", name);
    }

    private bool IsIgnoredCategory(JObject sync, string gameId, string gameName)
    {
        JArray ignored = sync == null ? null : sync["ignoredCategories"] as JArray;
        if (ignored == null)
        {
            return false;
        }

        foreach (JToken token in ignored)
        {
            string value = NormalizeKey(token.ToString());
            if (
                string.Equals(value, NormalizeKey(gameId), StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, NormalizeKey(gameName), StringComparison.OrdinalIgnoreCase)
            )
            {
                return true;
            }
        }

        return false;
    }

    private bool ManualLockIsActive()
    {
        string value = CPH.GetGlobalVar<string>(ManualLockUntilGlobal, true);
        DateTime lockUntil;
        return DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out lockUntil)
            && lockUntil.ToUniversalTime() > DateTime.UtcNow;
    }

    private void StorePendingGame(string key, string name, string twitchGameId)
    {
        CPH.SetGlobalVar(PendingGameKeyGlobal, key, true);
        CPH.SetGlobalVar(PendingGameNameGlobal, name, true);
        CPH.SetGlobalVar(PendingGameTwitchIdGlobal, twitchGameId ?? "", true);
        CPH.SetGlobalVar(PendingGameUpdatedUtcGlobal, DateTime.UtcNow.ToString("o"), true);
    }

    private string BuildTwitchKey(string gameId, string gameName)
    {
        if (!string.IsNullOrWhiteSpace(gameId))
        {
            return "twitch_" + SanitizeKey(gameId);
        }

        return SanitizeKey(gameName);
    }

    private string CleanGameName(string gameName)
    {
        return string.IsNullOrWhiteSpace(gameName) ? "Uncategorized" : gameName.Trim();
    }

    private bool TryLoadConfig(out JObject config)
    {
        config = null;
        string configJson = CPH.GetGlobalVar<string>(ConfigGlobal, true);
        if (string.IsNullOrWhiteSpace(configJson))
        {
            CPH.LogError($"[CGC] Missing JSON global '{ConfigGlobal}'. Run CGC - Configure Defaults.");
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

    private string GetFirstStringArg(params string[] argNames)
    {
        foreach (string argName in argNames)
        {
            string value;
            if (CPH.TryGetArg(argName, out value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return "";
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

    private bool GetBool(JToken token, bool defaultValue)
    {
        if (token == null)
        {
            return defaultValue;
        }

        bool parsed;
        return bool.TryParse(token.ToString(), out parsed) ? parsed : defaultValue;
    }

    private bool IsDebugEnabled(JObject config)
    {
        return GetBool(config["debugLogging"], false);
    }

    private string NormalizeKey(string value)
    {
        return (value ?? "").Trim().ToLowerInvariant();
    }

    private string SanitizeKey(string value)
    {
        string lowered = NormalizeKey(value);
        string sanitized = Regex.Replace(lowered, @"[^a-z0-9_]+", "_").Trim('_');
        return string.IsNullOrWhiteSpace(sanitized) ? "uncategorized" : sanitized;
    }
}
