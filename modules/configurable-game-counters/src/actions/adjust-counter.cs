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
    private const string LastIncrementPrefix = "gameCounters.lastIncrementUtc.";

    public bool Execute()
    {
        if (!TryLoadConfig(out JObject config))
        {
            return true;
        }

        if (!CallerIsAllowed(config, "manage"))
        {
            CPH.LogWarn("[CGC] Adjust Counter denied because caller is not a moderator or broadcaster.");
            return true;
        }

        string counterId = SanitizeKey(GetFirstStringArg("counterId", "counter", "input0"));
        if (string.IsNullOrWhiteSpace(counterId) || counterId == "uncategorized")
        {
            CPH.SendMessage("Adjust counter usage: provide counterId and amount.");
            return true;
        }

        int amount;
        if (!TryGetAmount(out amount))
        {
            CPH.SendMessage("Adjust counter usage: provide a numeric amount, such as -1 or 1.");
            return true;
        }

        JObject counter = GetCounter(config, counterId);
        bool allowNegative = counter != null && GetBool(counter["allowNegative"], false);
        string scope = NormalizeKey(GetFirstStringArg("scope", "input2"));
        if (string.IsNullOrWhiteSpace(scope))
        {
            scope = "both";
        }

        CurrentGame game = GetCurrentGame(config);
        int globalCount = CPH.GetGlobalVar<int?>(CountGlobalPrefix + counterId, true) ?? 0;
        int gameCount = CPH.GetGlobalVar<int?>(CountGamePrefix + game.Key + "." + counterId, true) ?? 0;

        if (scope == "global" || scope == "both")
        {
            globalCount = SetCounter(CountGlobalPrefix + counterId, globalCount + amount, allowNegative);
        }

        if (scope == "game" || scope == "currentgame" || scope == "both")
        {
            gameCount = SetCounter(CountGamePrefix + game.Key + "." + counterId, gameCount + amount, allowNegative);
            CPH.SetGlobalVar(LastIncrementPrefix + game.Key + "." + counterId, DateTime.UtcNow.ToString("o"), true);
        }

        CPH.SendMessage($"Adjusted {counterId}. {game.Name}: {gameCount}, all-time: {globalCount}.");
        CPH.LogInfo($"[CGC] Adjusted counter '{counterId}' scope '{scope}' by {amount}.");
        return true;
    }

    private int SetCounter(string globalName, int value, bool allowNegative)
    {
        if (!allowNegative && value < 0)
        {
            value = 0;
        }

        CPH.SetGlobalVar(globalName, value, true);
        return value;
    }

    private JObject GetCounter(JObject config, string counterId)
    {
        JObject counters = config["counters"] as JObject;
        return counters == null ? null : counters[counterId] as JObject;
    }

    private bool TryGetAmount(out int amount)
    {
        amount = 0;
        int parsed;
        if (CPH.TryGetArg("amount", out parsed))
        {
            amount = parsed;
            return true;
        }

        string value = GetFirstStringArg("input1");
        return int.TryParse(value, out amount);
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

    private bool GetBool(JToken token, bool defaultValue)
    {
        if (token == null)
        {
            return defaultValue;
        }
        bool parsed;
        return bool.TryParse(token.ToString(), out parsed) ? parsed : defaultValue;
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
