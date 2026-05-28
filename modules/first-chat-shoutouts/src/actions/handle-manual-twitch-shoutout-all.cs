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
        string targetId = "twitch_main";
        if (!TryLoadConfig(out JObject config))
        {
            return true;
        }

        JObject target = GetTarget(config, targetId);
        if (target == null || !IsEnabled(target["enabled"], true))
        {
            return true;
        }

        JObject manualAll = config["manualAll"] as JObject;
        if (!IsEnabled(manualAll == null ? null : manualAll["enabled"], true))
        {
            return true;
        }

        if (!IncludesTarget(manualAll, targetId))
        {
            return true;
        }

        if (IsEnabled(manualAll == null ? null : manualAll["moderatorOnly"], true) && !CallerIsModeratorOrBroadcaster())
        {
            CPH.LogWarn("[FCS] Manual shoutout-all denied because caller is not a moderator or broadcaster.");
            return true;
        }

        JArray entered = LoadEnteredLog(EnteredGlobal(targetId));
        if (entered.Count == 0)
        {
            CPH.LogInfo("[FCS] Manual shoutout-all found no configured chatters for this stream.");
            return true;
        }

        int attempted = 0;
        foreach (JToken token in entered)
        {
            string login = NormalizeLogin(token.ToString());
            if (string.IsNullOrWhiteSpace(login))
            {
                continue;
            }

            JObject person = FindPerson(config, login);
            if (person == null || !IsEnabled(person["enabled"], true))
            {
                continue;
            }

            CPH.SetArgument("targetId", targetId);
            CPH.SetArgument("shoutoutLogin", login);
            CPH.SetArgument("shoutoutSource", "manual_all");

            bool ran = CPH.RunAction("FCS - Run Shoutout", true);
            if (ran)
            {
                attempted++;
            }
            else
            {
                CPH.LogWarn($"[FCS] Core shoutout action returned false during shoutout-all for '{login}'.");
            }
        }

        CPH.LogInfo($"[FCS] Manual shoutout-all processed {attempted} configured chatters.");
        return true;
    }

    private bool TryLoadConfig(out JObject config)
    {
        config = null;
        string configJson = CPH.GetGlobalVar<string>(ConfigGlobal, true);
        if (string.IsNullOrWhiteSpace(configJson))
        {
            CPH.LogError($"[FCS] Missing JSON global '{ConfigGlobal}'. Run FCS - Configure Defaults.");
            return false;
        }

        try
        {
            config = JObject.Parse(configJson);
            return true;
        }
        catch (Exception ex)
        {
            CPH.LogError($"[FCS] Invalid JSON in '{ConfigGlobal}': {ex.Message}");
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

    private bool IncludesTarget(JObject section, string targetId)
    {
        if (section == null)
        {
            return true;
        }

        JArray targetIds = section["targetIds"] as JArray;
        if (targetIds == null || targetIds.Count == 0)
        {
            return true;
        }

        foreach (JToken token in targetIds)
        {
            if (string.Equals(NormalizeKey(token.ToString()), targetId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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
            if (
                CPH.TryGetArg(argName, out stringValue)
                && bool.TryParse(stringValue, out boolValue)
                && boolValue
            )
            {
                return true;
            }
        }

        return false;
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

    private string NormalizeLogin(string value)
    {
        value = (value ?? "").Trim();
        if (value.StartsWith("@"))
        {
            value = value.Substring(1);
        }

        if (!Regex.IsMatch(value, @"^[A-Za-z0-9_]{1,25}$"))
        {
            return "";
        }

        return value.ToLowerInvariant();
    }
}
