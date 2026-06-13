using System;
using Newtonsoft.Json.Linq;

public class CPHInline
{
    private const string ConfigGlobal = "firstChatShoutouts.config";
    private const string SessionGlobal = "firstChatShoutouts.streamSessionId";
    private const string StateGlobal = "firstChatShoutouts.streamState";

    public bool Execute()
    {
        if (!CallerIsModeratorOrBroadcaster())
        {
            CPH.LogWarn("[FCS] Stream state recovery denied because caller is not a moderator or broadcaster.");
            return true;
        }

        if (!TryLoadConfig(out JObject config))
        {
            return true;
        }

        DateTime now = DateTime.UtcNow;
        RecoverySettings settings = GetRecoverySettings(config);
        if (!settings.RecoveryEnabled || settings.RecoveryWindowMinutes <= 0)
        {
            CPH.SendMessage("No recoverable shoutout session is available.");
            CPH.LogInfo("[FCS] Stream state recovery skipped because recovery is disabled.");
            return true;
        }

        JObject state;
        if (!TryLoadState(out state, now))
        {
            return true;
        }

        JArray archives = state["archivedSessions"] as JArray;
        if (archives == null || archives.Count == 0)
        {
            CPH.SendMessage("No recoverable shoutout session is available.");
            return true;
        }

        TimeSpan recoveryWindow = TimeSpan.FromMinutes(settings.RecoveryWindowMinutes);
        for (int index = archives.Count - 1; index >= 0; index--)
        {
            JObject archive = archives[index] as JObject;
            if (archive == null)
            {
                continue;
            }

            DateTime archiveTime;
            if (!TryGetArchiveTime(archive, out archiveTime) || now - archiveTime > recoveryWindow)
            {
                continue;
            }

            RestoreArchive(state, archive, now);
            archives.RemoveAt(index);
            SaveState(state, settings, now);
            CPH.SendMessage("Recovered the previous automatic shoutout session.");
            CPH.LogInfo($"[FCS] Stream shoutout session recovered as {GetString(state, "activeSessionId")}.");
            return true;
        }

        CPH.SendMessage("No recoverable shoutout session is available.");
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

    private bool TryLoadState(out JObject state, DateTime now)
    {
        state = null;
        string stateJson = CPH.GetGlobalVar<string>(StateGlobal, true);
        if (string.IsNullOrWhiteSpace(stateJson))
        {
            state = CreateBlankState(CurrentSessionId(), now);
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

            EnsureBaseState(state, now);
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

    private void RestoreArchive(JObject state, JObject archive, DateTime now)
    {
        string sessionId = FirstNonBlank(GetString(archive, "sessionId"), GetString(archive, "activeSessionId"), DateTime.UtcNow.Ticks.ToString());
        state["activeSessionId"] = sessionId;
        state["activeStartedAtUtc"] = FirstNonBlank(GetString(archive, "activeStartedAtUtc"), now.ToString("o"));
        state["lastRecoveredAtUtc"] = now.ToString("o");
        state["lastUpdatedAtUtc"] = now.ToString("o");
        state["targets"] = archive["targets"] == null ? new JObject() : archive["targets"].DeepClone();
    }

    private void SaveState(JObject state, RecoverySettings settings, DateTime now)
    {
        state["lastUpdatedAtUtc"] = now.ToString("o");
        PruneArchivedSessions(state, settings, now);
        CPH.SetGlobalVar(StateGlobal, state.ToString(Newtonsoft.Json.Formatting.None), true);
        CPH.SetGlobalVar(SessionGlobal, GetString(state, "activeSessionId"), true);
    }

    private void PruneArchivedSessions(JObject state, RecoverySettings settings, DateTime now)
    {
        JArray archives = state["archivedSessions"] as JArray;
        JArray pruned = new JArray();

        if (archives == null || !settings.RecoveryEnabled || settings.RecoveryWindowMinutes <= 0 || settings.MaxArchivedSessions <= 0)
        {
            state["archivedSessions"] = pruned;
            return;
        }

        TimeSpan recoveryWindow = TimeSpan.FromMinutes(settings.RecoveryWindowMinutes);
        foreach (JObject archive in archives.Children<JObject>())
        {
            DateTime archiveTime;
            if (TryGetArchiveTime(archive, out archiveTime) && now - archiveTime <= recoveryWindow)
            {
                pruned.Add(archive);
            }
        }

        JArray bounded = new JArray();
        int start = Math.Max(0, pruned.Count - settings.MaxArchivedSessions);
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

    private RecoverySettings GetRecoverySettings(JObject config)
    {
        JObject streamState = config == null ? null : config["streamState"] as JObject;
        return new RecoverySettings
        {
            RecoveryEnabled = IsEnabled(streamState == null ? null : streamState["recoveryEnabled"], true),
            RecoveryWindowMinutes = GetInt(streamState, "recoveryWindowMinutes", 30),
            MaxArchivedSessions = GetInt(streamState, "maxArchivedSessions", 3)
        };
    }

    private string CurrentSessionId()
    {
        string sessionId = CPH.GetGlobalVar<string>(SessionGlobal, true);
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            sessionId = DateTime.UtcNow.Ticks.ToString();
            CPH.SetGlobalVar(SessionGlobal, sessionId, true);
        }

        return sessionId;
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

    private bool IsSchemaVersionOne(JObject state)
    {
        int parsed;
        return int.TryParse(GetString(state, "schemaVersion"), out parsed) && parsed == 1;
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

    private int GetInt(JObject obj, string key, int defaultValue)
    {
        int parsed;
        return int.TryParse(GetString(obj, key), out parsed) ? parsed : defaultValue;
    }

    private string GetString(JObject obj, string key, string defaultValue = "")
    {
        JToken token = obj == null ? null : obj[key];
        string value = token == null ? "" : token.ToString();
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    private string FirstNonBlank(params string[] values)
    {
        foreach (string value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return "";
    }

    private class RecoverySettings
    {
        public bool RecoveryEnabled { get; set; }
        public int RecoveryWindowMinutes { get; set; }
        public int MaxArchivedSessions { get; set; }
    }
}
