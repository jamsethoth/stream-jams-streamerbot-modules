using System;

public class CPHInline
{
    private const string ConfigGlobal = "firstChatShoutouts.config";
    private const string SessionGlobal = "firstChatShoutouts.streamSessionId";

    // Build-time placeholder. Do not paste this source directly into Streamer.bot.
    // tools/streamerbot_import/build_module_import.py replaces the quoted token
    // below with the JSON file named by module.json's defaultConfig field before
    // writing the generated .sb import.
    private const string DefaultConfigJsonBuildPlaceholder = "__STREAMERBOT_MODULE_DEFAULT_CONFIG_JSON__";

    public bool Execute()
    {
        EnsureGlobal(ConfigGlobal, DefaultConfigJson());

        string sessionId = CPH.GetGlobalVar<string>(SessionGlobal, true);
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            CPH.SetGlobalVar(SessionGlobal, DateTime.UtcNow.Ticks.ToString(), true);
        }

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
}
