using System;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

public class CPHInline
{
    private const string ConfigGlobal = "firstChatShoutouts.config";
    private const string SessionGlobal = "firstChatShoutouts.streamSessionId";
    private const string EnteredPrefix = "firstChatShoutouts.entered.";

    public bool Execute()
    {
        string login = NormalizeLogin(GetFirstStringArg("userName", "userLogin", "login", "user", "displayName"));
        if (string.IsNullOrWhiteSpace(login))
        {
            CPH.LogWarn("[FCS] Twitch First Words fired without a recognizable user login.");
            return true;
        }

        TrackEnteredConfiguredChatter("twitch_main", login);

        CPH.SetArgument("targetId", "twitch_main");
        CPH.SetArgument("shoutoutLogin", login);
        CPH.SetArgument("shoutoutSource", "automatic");

        bool ran = CPH.RunAction("FCS - Run Shoutout", true);
        if (!ran)
        {
            CPH.LogWarn($"[FCS] Core shoutout action returned false for automatic login '{login}'.");
        }

        return true;
    }

    private void TrackEnteredConfiguredChatter(string targetId, string login)
    {
        JObject config;
        if (!TryLoadConfig(out config))
        {
            return;
        }

        JObject target = GetTarget(config, targetId);
        if (target == null || !IsEnabled(target["enabled"], true))
        {
            return;
        }

        JObject person = FindPerson(config, login);
        if (person == null || !IsEnabled(person["enabled"], true))
        {
            return;
        }

        string enteredGlobal = EnteredGlobal(targetId);
        JArray entered = LoadEnteredLog(enteredGlobal);
        foreach (JToken token in entered)
        {
            if (string.Equals(NormalizeLogin(token.ToString()), login, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        entered.Add(login);
        CPH.SetGlobalVar(enteredGlobal, entered.ToString(Newtonsoft.Json.Formatting.None), true);
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
            CPH.LogWarn($"[FCS] Could not track first words chatter because config JSON is invalid: {ex.Message}");
            return false;
        }
    }

    private JObject GetTarget(JObject config, string targetId)
    {
        JObject targets = config["targets"] as JObject;
        return targets == null ? null : targets[targetId] as JObject;
    }

    private JObject FindPerson(JObject config, string login)
    {
        JArray people = config["people"] as JArray;
        if (people == null)
        {
            return null;
        }

        foreach (JObject person in people.Children<JObject>())
        {
            if (string.Equals(NormalizeLogin(GetString(person, "login")), login, StringComparison.OrdinalIgnoreCase))
            {
                return person;
            }
        }

        return null;
    }

    private JArray LoadEnteredLog(string enteredGlobal)
    {
        string existingJson = CPH.GetGlobalVar<string>(enteredGlobal, true);
        if (string.IsNullOrWhiteSpace(existingJson))
        {
            return new JArray();
        }

        try
        {
            return JArray.Parse(existingJson);
        }
        catch (Exception ex)
        {
            CPH.LogWarn($"[FCS] Could not parse entered chatter log '{enteredGlobal}': {ex.Message}");
            return new JArray();
        }
    }

    private string EnteredGlobal(string targetId)
    {
        return EnteredPrefix + NormalizeKey(targetId) + "." + CurrentSessionId();
    }

    private string CurrentSessionId()
    {
        string sessionId = CPH.GetGlobalVar<string>(SessionGlobal, true);
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            sessionId = DateTime.UtcNow.Ticks.ToString();
            CPH.SetGlobalVar(SessionGlobal, sessionId, true);
        }

        return Regex.Replace(sessionId, @"[^A-Za-z0-9_]", "");
    }

    private string GetFirstStringArg(params string[] argNames)
    {
        foreach (string argName in argNames)
        {
            string value;
            if (CPH.TryGetArg(argName, out value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return "";
    }

    private string NormalizeLogin(string value)
    {
        value = (value ?? "").Trim();
        if (value.StartsWith("@"))
        {
            value = value.Substring(1);
        }

        Match match = Regex.Match(value, @"[A-Za-z0-9_]{1,25}");
        return match.Success ? match.Value.ToLowerInvariant() : "";
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

    private string GetString(JObject obj, string key, string defaultValue = "")
    {
        JToken token = obj == null ? null : obj[key];
        string value = token == null ? "" : token.ToString();
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    private string NormalizeKey(string value)
    {
        return (value ?? "").Trim().ToLowerInvariant();
    }
}
