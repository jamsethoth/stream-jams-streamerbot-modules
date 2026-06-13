using System;
using Newtonsoft.Json.Linq;

public class CPHInline
{
    private const string ConfigGlobal = "giveawayModule.config";
    private const string StateGlobal = "giveawayModule.state";

    // Build-time placeholder. Do not paste this source directly into Streamer.bot.
    // tools/streamerbot_import/build_module_import.py replaces the quoted token
    // below with the JSON file named by module.json's defaultConfig field before
    // writing the generated .sb import.
    private const string DefaultConfigJsonBuildPlaceholder = "__STREAMERBOT_MODULE_DEFAULT_CONFIG_JSON__";

    public bool Execute()
    {
        EnsureGlobal(ConfigGlobal, DefaultConfigJson());
        EnsureStateGlobal();

        CPH.LogInfo("[GWM] Default globals are ready. Edit giveawayModule.config to configure Twitch reward matching.");
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

    private void EnsureStateGlobal()
    {
        string current = CPH.GetGlobalVar<string>(StateGlobal, true);
        if (string.IsNullOrWhiteSpace(current))
        {
            CPH.SetGlobalVar(StateGlobal, EmptyStateJson(), true);
            return;
        }

        try
        {
            JObject.Parse(current);
        }
        catch (Exception ex)
        {
            CPH.LogWarn($"[GWM] Existing '{StateGlobal}' is not valid JSON and was left unchanged: {ex.Message}");
        }
    }

    private string EmptyStateJson()
    {
        JObject state = new JObject();
        state["schemaVersion"] = 1;
        state["giveawayId"] = "default";
        state["entries"] = new JArray();
        state["winners"] = new JArray();
        state["updatedAtUtc"] = DateTime.UtcNow.ToString("o");
        return state.ToString(Newtonsoft.Json.Formatting.None);
    }

    private string DefaultConfigJson()
    {
        return DefaultConfigJsonBuildPlaceholder;
    }
}
