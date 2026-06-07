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

        if (!CallerIsAllowed(config, "report"))
        {
            CPH.LogWarn("[CGC] Report Counter denied because caller does not have permission.");
            return true;
        }

        string counterId;
        JObject counter = ResolveCounter(config, GetFirstStringArg("counterId", "counter", "rawInput", "input0"), out counterId);
        if (counter == null)
        {
            CPH.SendMessage("Available counters: " + AvailableCounters(config));
            return true;
        }

        CurrentGame game = GetCurrentGame(config);
        int globalCount = CPH.GetGlobalVar<int?>(CountGlobalPrefix + counterId, true) ?? 0;
        int gameCount = CPH.GetGlobalVar<int?>(CountGamePrefix + game.Key + "." + counterId, true) ?? 0;
        string template = GetString(counter, "reportTemplate", "{label} for {gameName}: {gameCount}. All-time: {globalCount}.");
        CPH.SendMessage(ResolveTemplate(template, counterId, counter, game, gameCount, globalCount));
        return true;
    }

    private JObject ResolveCounter(JObject config, string input, out string counterId)
    {
        counterId = "";
        JObject counters = config["counters"] as JObject;
        if (counters == null)
        {
            return null;
        }

        string normalizedInput = NormalizeKey(FirstToken(input));
        foreach (JProperty property in counters.Properties())
        {
            string candidateId = SanitizeKey(property.Name);
            JObject counter = property.Value as JObject;
            if (counter == null || !IsEnabled(counter["enabled"], true))
            {
                continue;
            }

            if (normalizedInput == NormalizeKey(candidateId) || normalizedInput == NormalizeKey(GetString(counter, "label", "")))
            {
                counterId = candidateId;
                return counter;
            }

            JArray aliases = counter["aliases"] as JArray;
            if (aliases == null)
            {
                continue;
            }

            foreach (JToken alias in aliases)
            {
                if (normalizedInput == NormalizeKey(alias.ToString()))
                {
                    counterId = candidateId;
                    return counter;
                }
            }
        }

        return null;
    }

    private string AvailableCounters(JObject config)
    {
        JObject counters = config["counters"] as JObject;
        if (counters == null)
        {
            return "none";
        }

        System.Collections.Generic.List<string> labels = new System.Collections.Generic.List<string>();
        foreach (JProperty property in counters.Properties())
        {
            JObject counter = property.Value as JObject;
            if (counter != null && IsEnabled(counter["enabled"], true))
            {
                labels.Add(GetString(counter, "label", property.Name));
            }
        }

        return labels.Count == 0 ? "none" : string.Join(", ", labels.ToArray());
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
        JObject permissions = config["permissions"] as JObject;
        string permission = NormalizeKey(GetString(permissions, permissionKey, "everyone"));
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

    private string ResolveTemplate(string template, string counterId, JObject counter, CurrentGame game, int gameCount, int globalCount)
    {
        return (template ?? "")
            .Replace("{counterId}", counterId)
            .Replace("{label}", GetString(counter, "label", counterId))
            .Replace("{gameKey}", game.Key)
            .Replace("{gameName}", game.Name)
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

    private string FirstToken(string value)
    {
        Match match = Regex.Match(value ?? "", @"\S+");
        return match.Success ? match.Value : "";
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

    private bool IsEnabled(JToken token, bool defaultValue)
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
