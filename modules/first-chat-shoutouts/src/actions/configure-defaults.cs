using System;
using Newtonsoft.Json.Linq;

public class CPHInline
{
    private const string ConfigGlobal = "firstChatShoutouts.config";
    private const string SessionGlobal = "firstChatShoutouts.streamSessionId";
    private const string StateGlobal = "firstChatShoutouts.streamState";

    // Build-time placeholder. Do not paste this source directly into Streamer.bot.
    // tools/streamerbot_import/build_module_import.py replaces the quoted token
    // below with the JSON file named by module.json's defaultConfig field before
    // writing the generated .sb import.
    private const string DefaultConfigJsonBuildPlaceholder = "__STREAMERBOT_MODULE_DEFAULT_CONFIG_JSON__";

    public bool Execute()
    {
        EnsureGlobal(ConfigGlobal, DefaultConfigJson());
        EnsureStreamState();

        CPH.LogInfo("[FCS] Default globals are ready. Edit firstChatShoutouts.config to add automatic shoutout people and templates.");
        return true;
    }

    private void EnsureGlobal(string name, string defaultValue)
    {
        string current = CPH.GetGlobalVar<string>(name, true);
        if (string.IsNullOrWhiteSpace(current))
        {
            CPH.SetGlobalVar(name, defaultValue, true);
        }
    }

    private string DefaultConfigJson()
    {
        return DefaultConfigJsonBuildPlaceholder;
    }

    private void EnsureStreamState()
    {
        string stateJson = CPH.GetGlobalVar<string>(StateGlobal, true);
        if (string.IsNullOrWhiteSpace(stateJson))
        {
            string sessionId = CPH.GetGlobalVar<string>(SessionGlobal, true);
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                sessionId = DateTime.UtcNow.Ticks.ToString();
            }

            JObject state = CreateBlankState(sessionId, DateTime.UtcNow);
            CPH.SetGlobalVar(StateGlobal, state.ToString(Newtonsoft.Json.Formatting.None), true);
            CPH.SetGlobalVar(SessionGlobal, sessionId, true);
            return;
        }

        try
        {
            JObject state = JObject.Parse(stateJson);
            if (!IsSchemaVersionOne(state))
            {
                CPH.LogWarn($"[FCS] Existing JSON global '{StateGlobal}' has an unsupported schemaVersion. It was left unchanged.");
                return;
            }

            string activeSessionId = GetString(state, "activeSessionId");
            if (!string.IsNullOrWhiteSpace(activeSessionId))
            {
                CPH.SetGlobalVar(SessionGlobal, activeSessionId, true);
            }
        }
        catch (Exception ex)
        {
            CPH.LogWarn($"[FCS] Existing JSON global '{StateGlobal}' is invalid and was left unchanged: {ex.Message}");
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

    private bool IsSchemaVersionOne(JObject state)
    {
        int parsed;
        return int.TryParse(GetString(state, "schemaVersion"), out parsed) && parsed == 1;
    }

    private string GetString(JObject obj, string key, string defaultValue = "")
    {
        JToken token = obj == null ? null : obj[key];
        string value = token == null ? "" : token.ToString();
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }
}
