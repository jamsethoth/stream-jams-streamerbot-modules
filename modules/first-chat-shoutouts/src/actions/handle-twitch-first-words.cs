using System;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

public class CPHInline
{
    private const string ConfigGlobal = "firstChatShoutouts.config";
    private const string SessionGlobal = "firstChatShoutouts.streamSessionId";
    private const string StateGlobal = "firstChatShoutouts.streamState";

    public bool Execute()
    {
        string login = NormalizeLogin(GetFirstStringArg("userName", "userLogin", "login", "user", "displayName"));
        if (string.IsNullOrWhiteSpace(login))
        {
            CPH.LogWarn("[FCS] Twitch First Words fired without a recognizable user login.");
            return true;
        }

        if (!TrackEnteredConfiguredChatter("twitch_main", login))
        {
            return true;
        }

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

    private bool TrackEnteredConfiguredChatter(string targetId, string login)
    {
        JObject config;
        if (!TryLoadConfig(out config))
        {
            return true;
        }

        JObject target = GetTarget(config, targetId);
        if (target == null || !IsEnabled(target["enabled"], true))
        {
            return true;
        }

        JObject person = FindPerson(config, login);
        if (person == null || !IsEnabled(person["enabled"], true))
        {
            return true;
        }

        JObject state;
        if (!TryLoadState(out state))
        {
            return false;
        }

        JObject targetState = EnsureTargetState(state, targetId);
        JObject loginState = EnsureLoginState(targetState, login);
        bool alreadyEntered = IsEnabled(loginState["entered"], false);
        if (!alreadyEntered)
        {
            loginState["entered"] = true;
            loginState["enteredTimeUtc"] = DateTime.UtcNow.ToString("o");
        }

        JArray enteredOrder = targetState["enteredOrder"] as JArray;
        if (!EnteredOrderContains(enteredOrder, login))
        {
            enteredOrder.Add(login);
        }

        SaveState(state, config);
        return true;
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

    private bool TryLoadState(out JObject state)
    {
        state = null;
        string stateJson = CPH.GetGlobalVar<string>(StateGlobal, true);
        if (string.IsNullOrWhiteSpace(stateJson))
        {
            string sessionId = CurrentSessionId();
            state = CreateBlankState(sessionId, DateTime.UtcNow);
            SaveState(state, null);
            return true;
        }

        try
        {
            state = JObject.Parse(stateJson);
            if (!IsSchemaVersionOne(state))
            {
                CPH.LogError($"[FCS] Unsupported schemaVersion in '{StateGlobal}'. State was left unchanged.");
                state = null;
                return false;
            }

            EnsureBaseState(state, DateTime.UtcNow);
            return true;
        }
        catch (Exception ex)
        {
            CPH.LogError($"[FCS] Invalid JSON in '{StateGlobal}': {ex.Message}");
            return false;
        }
    }

    private JObject CreateBlankState(string sessionId, DateTime now)
    {
        string timestamp = now.ToString("o");
        return new JObject
        {
            ["schemaVersion"] = 1,
            ["activeSessionId"] = sessionId,
            ["activeStartedAtUtc"] = timestamp,
            ["lastUpdatedAtUtc"] = timestamp,
            ["lastRecoveredAtUtc"] = JValue.CreateNull(),
            ["targets"] = new JObject(),
            ["archivedSessions"] = new JArray()
        };
    }

    private void EnsureBaseState(JObject state, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(GetString(state, "activeSessionId")))
        {
            state["activeSessionId"] = CurrentSessionId();
        }

        if (string.IsNullOrWhiteSpace(GetString(state, "activeStartedAtUtc")))
        {
            state["activeStartedAtUtc"] = now.ToString("o");
        }

        if (string.IsNullOrWhiteSpace(GetString(state, "lastUpdatedAtUtc")))
        {
            state["lastUpdatedAtUtc"] = now.ToString("o");
        }

        if (!(state["targets"] is JObject))
        {
            state["targets"] = new JObject();
        }

        if (!(state["archivedSessions"] is JArray))
        {
            state["archivedSessions"] = new JArray();
        }
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

    private JObject EnsureTargetState(JObject state, string targetId)
    {
        JObject targets = state["targets"] as JObject;
        if (targets == null)
        {
            targets = new JObject();
            state["targets"] = targets;
        }

        string normalizedTargetId = NormalizeKey(targetId);
        JObject targetState = targets[normalizedTargetId] as JObject;
        if (targetState == null)
        {
            targetState = new JObject();
            targets[normalizedTargetId] = targetState;
        }

        if (!(targetState["enteredOrder"] is JArray))
        {
            targetState["enteredOrder"] = new JArray();
        }

        if (!(targetState["logins"] is JObject))
        {
            targetState["logins"] = new JObject();
        }

        return targetState;
    }

    private JObject EnsureLoginState(JObject targetState, string login)
    {
        JObject logins = targetState["logins"] as JObject;
        if (logins == null)
        {
            logins = new JObject();
            targetState["logins"] = logins;
        }

        string normalizedLogin = NormalizeLogin(login);
        JObject loginState = logins[normalizedLogin] as JObject;
        if (loginState == null)
        {
            loginState = new JObject
            {
                ["login"] = normalizedLogin,
                ["entered"] = false,
                ["enteredTimeUtc"] = JValue.CreateNull(),
                ["sent"] = false,
                ["sentTimeUtc"] = JValue.CreateNull(),
                ["sentSource"] = ""
            };
            logins[normalizedLogin] = loginState;
        }
        else
        {
            loginState["login"] = normalizedLogin;
            if (loginState["entered"] == null)
            {
                loginState["entered"] = false;
            }

            if (loginState["sent"] == null)
            {
                loginState["sent"] = false;
            }

            if (loginState["sentSource"] == null)
            {
                loginState["sentSource"] = "";
            }
        }

        return loginState;
    }

    private bool EnteredOrderContains(JArray enteredOrder, string login)
    {
        foreach (JToken token in enteredOrder)
        {
            if (string.Equals(NormalizeLogin(token.ToString()), login, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void SaveState(JObject state, JObject config)
    {
        DateTime now = DateTime.UtcNow;
        state["lastUpdatedAtUtc"] = now.ToString("o");
        PruneArchivedSessions(state, config, now);
        CPH.SetGlobalVar(StateGlobal, state.ToString(Newtonsoft.Json.Formatting.None), true);
        CPH.SetGlobalVar(SessionGlobal, GetString(state, "activeSessionId"), true);
    }

    private void PruneArchivedSessions(JObject state, JObject config, DateTime now)
    {
        int recoveryWindowMinutes = GetRecoveryWindowMinutes(config);
        int maxArchivedSessions = GetMaxArchivedSessions(config);
        JArray archives = state["archivedSessions"] as JArray;
        JArray pruned = new JArray();

        if (archives == null || recoveryWindowMinutes <= 0 || maxArchivedSessions <= 0 || !IsRecoveryEnabled(config))
        {
            state["archivedSessions"] = pruned;
            return;
        }

        TimeSpan recoveryWindow = TimeSpan.FromMinutes(recoveryWindowMinutes);
        foreach (JObject archive in archives.Children<JObject>())
        {
            DateTime archiveTime;
            if (TryGetArchiveTime(archive, out archiveTime) && now - archiveTime <= recoveryWindow)
            {
                pruned.Add(archive);
            }
        }

        JArray bounded = new JArray();
        int start = Math.Max(0, pruned.Count - maxArchivedSessions);
        for (int index = start; index < pruned.Count; index++)
        {
            bounded.Add(pruned[index]);
        }

        state["archivedSessions"] = bounded;
    }

    private bool TryGetArchiveTime(JObject archive, out DateTime archiveTime)
    {
        return TryParseUtc(GetString(archive, "archivedAtUtc"), out archiveTime)
            || TryParseUtc(GetString(archive, "lastUpdatedAtUtc"), out archiveTime)
            || TryParseUtc(GetString(archive, "activeStartedAtUtc"), out archiveTime);
    }

    private bool TryParseUtc(string value, out DateTime parsed)
    {
        DateTime result;
        if (DateTime.TryParse(value, out result))
        {
            parsed = result.ToUniversalTime();
            return true;
        }

        parsed = DateTime.MinValue;
        return false;
    }

    private bool IsRecoveryEnabled(JObject config)
    {
        JObject streamState = config == null ? null : config["streamState"] as JObject;
        return IsEnabled(streamState == null ? null : streamState["recoveryEnabled"], true);
    }

    private int GetRecoveryWindowMinutes(JObject config)
    {
        JObject streamState = config == null ? null : config["streamState"] as JObject;
        return GetInt(streamState, "recoveryWindowMinutes", 30);
    }

    private int GetMaxArchivedSessions(JObject config)
    {
        JObject streamState = config == null ? null : config["streamState"] as JObject;
        return GetInt(streamState, "maxArchivedSessions", 3);
    }

    private int GetInt(JObject obj, string key, int defaultValue)
    {
        int parsed;
        return int.TryParse(GetString(obj, key), out parsed) ? parsed : defaultValue;
    }

    private bool IsSchemaVersionOne(JObject state)
    {
        int parsed;
        return int.TryParse(GetString(state, "schemaVersion"), out parsed) && parsed == 1;
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

        if (!Regex.IsMatch(value, @"^[A-Za-z0-9_]{1,25}$"))
        {
            return "";
        }

        return value.ToLowerInvariant();
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
