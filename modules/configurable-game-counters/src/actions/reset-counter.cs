using System;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

public class CPHInline
{
    private const string ConfigGlobal = "gameCounters.config";
    private const string CurrentGameKeyGlobal = "gameCounters.currentGame.key";
    private const string CurrentGameNameGlobal = "gameCounters.currentGame.name";
    private const string CountGlobalPrefix = "gameCounters.counts.global.";
    private const string CountGamePrefix = "gameCounters.counts.byGame.";

    public bool Execute()
    {
        if (!TryLoadConfig(out JObject config))
        {
            return true;
        }

        if (!CallerIsAllowed(config, "manage"))
        {
            CPH.LogWarn("[CGC] Reset Counter denied because caller is not a moderator or broadcaster.");
            return true;
        }

        if (!ConfirmReset())
        {
            CPH.SendMessage("Reset counter refused. Run with confirm=true when you really want to reset counter state.");
            return true;
        }

        string counterId = SanitizeKey(GetFirstStringArg("counterId", "counter", "input0"));
        if (string.IsNullOrWhiteSpace(counterId) || counterId == "uncategorized")
        {
            CPH.SendMessage("Reset counter usage: provide counterId, scope, and confirm=true.");
            return true;
        }

        string scope = NormalizeKey(GetFirstStringArg("scope", "input1"));
        if (string.IsNullOrWhiteSpace(scope))
        {
            scope = "game";
        }

        CurrentGame game = GetCurrentGame(config);
        ResetCounter(counterId, scope, game);
        CPH.SendMessage($"Reset {counterId} counter scope '{scope}'.");
        return true;
    }

    private void ResetCounter(string counterId, string scope, CurrentGame game)
    {
        if (scope == "global" || scope == "both")
        {
            CPH.SetGlobalVar(CountGlobalPrefix + counterId, 0, true);
        }

        if (scope == "game" || scope == "currentgame" || scope == "both")
        {
            CPH.SetGlobalVar(CountGamePrefix + game.Key + "." + counterId, 0, true);
        }

        CPH.LogInfo($"[CGC] ResetCounter counter='{counterId}' scope='{scope}' game='{game.Key}'.");
    }

    private bool ConfirmReset()
    {
        bool confirmBool;
        if (CPH.TryGetArg("confirm", out confirmBool) && confirmBool)
        {
            return true;
        }

        string confirmText = GetFirstStringArg("confirm", "input2");
        return string.Equals(confirmText, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(confirmText, "yes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(confirmText, "confirm", StringComparison.OrdinalIgnoreCase);
    }

    private CurrentGame GetCurrentGame(JObject config)
    {
        string key = CPH.GetGlobalVar<string>(CurrentGameKeyGlobal, true);
        string name = CPH.GetGlobalVar<string>(CurrentGameNameGlobal, true);
        JObject currentGame = config["currentGame"] as JObject;
        if (string.IsNullOrWhiteSpace(key))
        {
            key = SanitizeKey(GetString(currentGame, "fallbackKey", "uncategorized"));
        }
        if (string.IsNullOrWhiteSpace(name))
        {
            name = GetString(currentGame, "fallbackName", "Uncategorized");
        }
        return new CurrentGame { Key = SanitizeKey(key), Name = name.Trim() };
    }

    private bool CallerIsAllowed(JObject config, string permissionKey)
    {
        if (!HasCallerContext())
        {
            return true;
        }

        JObject permissions = config["permissions"] as JObject;
        string permission = NormalizeKey(GetString(permissions, permissionKey, "moderator"));
        if (permission == "everyone" || permission == "all")
        {
            return true;
        }
        if (permission == "broadcaster" || permission == "streamer")
        {
            return AnyBooleanArgIsTrue("isBroadcaster", "broadcaster");
        }
        return AnyBooleanArgIsTrue("isModerator", "moderator", "isBroadcaster", "broadcaster");
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

    private class CurrentGame
    {
        public string Key;
        public string Name;
    }
}
