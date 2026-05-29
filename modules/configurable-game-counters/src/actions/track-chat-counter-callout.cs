using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

public class CPHInline
{
    private const string ConfigGlobal = "gameCounters.config";
    private const string CurrentGameKeyGlobal = "gameCounters.currentGame.key";
    private const string CurrentGameNameGlobal = "gameCounters.currentGame.name";
    private const string CurrentGameSourceGlobal = "gameCounters.currentGame.source";
    private const string CountGlobalPrefix = "gameCounters.counts.global.";
    private const string CountGamePrefix = "gameCounters.counts.byGame.";
    private const string LastIncrementPrefix = "gameCounters.lastIncrementUtc.";
    private const string CooldownCounterPrefix = "gameCounters.cooldowns.counter.";
    private const string CooldownUserPrefix = "gameCounters.cooldowns.user.";

    public bool Execute()
    {
        if (!TryLoadConfig(out JObject config))
        {
            return true;
        }

        JObject chatParser = config["chatParser"] as JObject;
        if (!IsEnabled(chatParser == null ? null : chatParser["enabled"], true))
        {
            return true;
        }

        if (IsIgnoredChatEvent())
        {
            return true;
        }

        string message = GetFirstStringArg("rawInput", "message", "messageStripped", "text", "input0");
        if (string.IsNullOrWhiteSpace(message))
        {
            return true;
        }

        string counterId;
        JObject counter = ResolveCounterFromMessage(config, message, out counterId);
        if (counter == null)
        {
            return true;
        }

        if (!CallerIsAllowed(config, counter, "increment"))
        {
            CPH.LogWarn($"[CGC] Counter '{counterId}' denied because caller does not have permission.");
            return true;
        }

        string userName = GetFirstStringArg("userName", "username", "user", "displayName", "login");
        if (string.IsNullOrWhiteSpace(userName))
        {
            userName = "chat";
        }

        if (IsOnCooldown(config, chatParser, counter, counterId, userName))
        {
            if (IsDebugEnabled(config))
            {
                CPH.LogInfo($"[CGC] Counter '{counterId}' ignored because it is on cooldown.");
            }
            return true;
        }

        CurrentGame game = GetCurrentGame(config);
        int amount = GetInt(counter["amount"], 1);
        bool allowNegative = GetBool(counter["allowNegative"], false);
        int globalCount = IncrementCounter(CountGlobalPrefix + counterId, amount, allowNegative);
        int gameCount = IncrementCounter(CountGamePrefix + game.Key + "." + counterId, amount, allowNegative);

        string now = DateTime.UtcNow.ToString("o");
        CPH.SetGlobalVar(LastIncrementPrefix + game.Key + "." + counterId, now, true);
        CPH.SetGlobalVar(CooldownCounterPrefix + counterId, now, true);
        CPH.SetGlobalVar(CooldownUserPrefix + counterId + "." + SanitizeKey(userName), now, true);

        if (GetBool(chatParser == null ? null : chatParser["sendResponses"], true))
        {
            string template = GetString(counter, "responseTemplate", "{label} for {gameName}: {gameCount}. All-time: {globalCount}.");
            CPH.SendMessage(ResolveTemplate(template, counterId, counter, game, userName, gameCount, globalCount));
        }

        if (IsDebugEnabled(config))
        {
            CPH.LogInfo($"[CGC] Incremented '{counterId}' for game '{game.Key}' by {amount}. Game={gameCount}, Global={globalCount}, Source={CPH.GetGlobalVar<string>(CurrentGameSourceGlobal, true)}.");
        }

        return true;
    }

    private JObject ResolveCounterFromMessage(JObject config, string message, out string counterId)
    {
        counterId = "";
        JObject counters = config["counters"] as JObject;
        if (counters == null)
        {
            CPH.LogWarn("[CGC] Config has no counters object.");
            return null;
        }

        string trimmed = (message ?? "").Trim();
        foreach (JProperty property in counters.Properties())
        {
            string candidateCounterId = SanitizeKey(property.Name);
            JObject counter = property.Value as JObject;
            if (counter == null || !IsEnabled(counter["enabled"], true))
            {
                continue;
            }

            JArray aliases = counter["aliases"] as JArray;
            if (aliases == null)
            {
                continue;
            }

            foreach (JToken aliasToken in aliases)
            {
                string alias = aliasToken.ToString().Trim();
                if (AliasMatches(trimmed, alias))
                {
                    counterId = candidateCounterId;
                    return counter;
                }
            }
        }

        return null;
    }

    private bool AliasMatches(string message, string alias)
    {
        if (string.IsNullOrWhiteSpace(message) || string.IsNullOrWhiteSpace(alias))
        {
            return false;
        }

        if (!message.StartsWith(alias, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return message.Length == alias.Length || char.IsWhiteSpace(message[alias.Length]);
    }

    private int IncrementCounter(string globalName, int amount, bool allowNegative)
    {
        int current = CPH.GetGlobalVar<int?>(globalName, true) ?? 0;
        int next = current + amount;
        if (!allowNegative && next < 0)
        {
            next = 0;
        }

        CPH.SetGlobalVar(globalName, next, true);
        return next;
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

    private bool IsOnCooldown(JObject config, JObject chatParser, JObject counter, string counterId, string userName)
    {
        int globalSeconds = GetInt(counter["globalCooldownSeconds"], GetInt(chatParser == null ? null : chatParser["globalCooldownSeconds"], 0));
        int userSeconds = GetInt(counter["perUserCooldownSeconds"], GetInt(chatParser == null ? null : chatParser["perUserCooldownSeconds"], 0));
        DateTime now = DateTime.UtcNow;

        if (globalSeconds > 0 && IsWithinCooldown(CooldownCounterPrefix + counterId, now, globalSeconds))
        {
            return true;
        }

        if (userSeconds > 0 && IsWithinCooldown(CooldownUserPrefix + counterId + "." + SanitizeKey(userName), now, userSeconds))
        {
            return true;
        }

        return false;
    }

    private bool IsWithinCooldown(string globalName, DateTime now, int seconds)
    {
        string value = CPH.GetGlobalVar<string>(globalName, true);
        DateTime last;
        return DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out last)
            && (now - last.ToUniversalTime()).TotalSeconds < seconds;
    }

    private bool CallerIsAllowed(JObject config, JObject counter, string permissionKey)
    {
        string permission = GetString(counter, "permission", "");
        if (string.IsNullOrWhiteSpace(permission))
        {
            JObject permissions = config["permissions"] as JObject;
            permission = GetString(permissions, permissionKey, "everyone");
        }

        permission = NormalizeKey(permission);
        if (permission == "everyone" || permission == "all")
        {
            return true;
        }

        if (permission == "moderator" || permission == "moderators" || permission == "mod" || permission == "mods")
        {
            return CallerIsModeratorOrBroadcaster();
        }

        if (permission == "broadcaster" || permission == "streamer")
        {
            return AnyBooleanArgIsTrue("isBroadcaster", "broadcaster");
        }

        return true;
    }

    private bool IsIgnoredChatEvent()
    {
        return AnyBooleanArgIsTrue("isBot", "bot", "fromBot", "isSystem", "isSystemMessage");
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

    private string ResolveTemplate(string template, string counterId, JObject counter, CurrentGame game, string userName, int gameCount, int globalCount)
    {
        return (template ?? "")
            .Replace("{counterId}", counterId)
            .Replace("{label}", GetString(counter, "label", counterId))
            .Replace("{gameKey}", game.Key)
            .Replace("{gameName}", game.Name)
            .Replace("{user}", userName)
            .Replace("{gameCount}", gameCount.ToString())
            .Replace("{globalCount}", globalCount.ToString());
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

    private bool GetBool(JToken token, bool defaultValue)
    {
        if (token == null)
        {
            return defaultValue;
        }

        bool parsed;
        return bool.TryParse(token.ToString(), out parsed) ? parsed : defaultValue;
    }

    private bool IsEnabled(JToken token, bool defaultValue)
    {
        return GetBool(token, defaultValue);
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

    private class CurrentGame
    {
        public string Key;
        public string Name;
    }
}
