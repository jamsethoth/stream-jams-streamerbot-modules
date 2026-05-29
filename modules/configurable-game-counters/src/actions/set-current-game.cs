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

    public bool Execute()
    {
        if (!TryLoadConfig(out JObject config))
        {
            return true;
        }

        if (!CallerIsAllowed(config, "manage"))
        {
            CPH.LogWarn("[CGC] Set Current Game denied because caller is not a moderator or broadcaster.");
            return true;
        }

        string gameName = GetFirstStringArg("gameName", "currentGame", "rawInput", "input0");
        if (string.IsNullOrWhiteSpace(gameName))
        {
            CPH.SendMessage("Usage: set a current game name before running CGC - Set Current Game.");
            return true;
        }

        string gameKey = GetFirstStringArg("gameKey", "currentGameKey");
        if (string.IsNullOrWhiteSpace(gameKey))
        {
            gameKey = SanitizeKey(gameName);
        }
        else
        {
            gameKey = SanitizeKey(gameKey);
        }

        SetCurrentGame(gameKey, gameName.Trim(), "manual", "");

        int lockMinutes = GetManualLockMinutes(config);
        string lockUntil = "";
        if (lockMinutes > 0)
        {
            lockUntil = DateTime.UtcNow.AddMinutes(lockMinutes).ToString("o");
        }
        CPH.SetGlobalVar(ManualLockUntilGlobal, lockUntil, true);

        CPH.SendMessage($"Current counter game set to {gameName.Trim()}.");
        CPH.LogInfo($"[CGC] Current game set manually to '{gameName.Trim()}' ({gameKey}); manualLockUntilUtc='{lockUntil}'.");
        return true;
    }

    private void SetCurrentGame(string key, string name, string source, string twitchGameId)
    {
        CPH.SetGlobalVar(CurrentGameKeyGlobal, key, true);
        CPH.SetGlobalVar(CurrentGameNameGlobal, name, true);
        CPH.SetGlobalVar(CurrentGameSourceGlobal, source, true);
        CPH.SetGlobalVar(CurrentGameUpdatedUtcGlobal, DateTime.UtcNow.ToString("o"), true);
        CPH.SetGlobalVar(CurrentGameTwitchIdGlobal, twitchGameId ?? "", true);
    }

    private int GetManualLockMinutes(JObject config)
    {
        JObject currentGame = config["currentGame"] as JObject;
        JObject sync = currentGame == null ? null : currentGame["twitchSync"] as JObject;
        return GetInt(sync == null ? null : sync["manualLockMinutes"], 180);
    }

    private bool CallerIsAllowed(JObject config, string permissionKey)
    {
        if (!HasCallerContext())
        {
            return true;
        }

        JObject permissions = config["permissions"] as JObject;
        string permission = GetString(permissions, permissionKey, "moderator").ToLowerInvariant();
        if (permission == "everyone" || permission == "all")
        {
            return true;
        }

        if (permission == "broadcaster" || permission == "streamer")
        {
            return AnyBooleanArgIsTrue("isBroadcaster", "broadcaster");
        }

        return CallerIsModeratorOrBroadcaster();
    }

    private bool HasCallerContext()
    {
        string stringValue;
        bool boolValue;
        return CPH.TryGetArg("userName", out stringValue)
            || CPH.TryGetArg("username", out stringValue)
            || CPH.TryGetArg("user", out stringValue)
            || CPH.TryGetArg("login", out stringValue)
            || CPH.TryGetArg("isModerator", out boolValue)
            || CPH.TryGetArg("isBroadcaster", out boolValue);
    }

    private bool CallerIsModeratorOrBroadcaster()
    {
        return AnyBooleanArgIsTrue("isModerator", "moderator", "isBroadcaster", "broadcaster");
    }

    private bool AnyBooleanArgIsTrue(params string[] argNames)
    {
        foreach (string argName in argNames)
        {
            bool boolValue;
            if (CPH.TryGetArg(argName, out boolValue) && boolValue)
            {
                return true;
            }

            string stringValue;
            if (CPH.TryGetArg(argName, out stringValue) && bool.TryParse(stringValue, out boolValue) && boolValue)
            {
                return true;
            }
        }

        return false;
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

    private int GetInt(JToken token, int defaultValue)
    {
        if (token == null)
        {
            return defaultValue;
        }

        int parsed;
        return int.TryParse(token.ToString(), out parsed) ? parsed : defaultValue;
    }

    private string SanitizeKey(string value)
    {
        string lowered = (value ?? "").Trim().ToLowerInvariant();
        string sanitized = Regex.Replace(lowered, @"[^a-z0-9_]+", "_").Trim('_');
        return string.IsNullOrWhiteSpace(sanitized) ? "uncategorized" : sanitized;
    }
}
