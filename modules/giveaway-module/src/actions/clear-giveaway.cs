using System;
using Newtonsoft.Json.Linq;

public class CPHInline
{
    private const string ConfigGlobal = "giveawayModule.config";
    private const string StateGlobal = "giveawayModule.state";

    public bool Execute()
    {
        JObject config;
        if (!TryLoadConfig(out config))
        {
            return true;
        }

        if (!CallerIsAllowed(config))
        {
            CPH.LogWarn("[GWM] Clear Giveaway denied because caller is not a moderator or broadcaster.");
            return true;
        }

        JObject state = NewState();
        SaveState(state);
        SendResponse(config, "cleared", "");
        CPH.LogInfo("[GWM] Giveaway state cleared.");
        return true;
    }

    private bool TryLoadConfig(out JObject config)
    {
        config = null;
        string configJson = CPH.GetGlobalVar<string>(ConfigGlobal, true);
        if (string.IsNullOrWhiteSpace(configJson))
        {
            CPH.LogError($"[GWM] Missing JSON global '{ConfigGlobal}'. Run GWM - Configure Defaults.");
            return false;
        }

        try
        {
            config = JObject.Parse(configJson);
            return true;
        }
        catch (Exception ex)
        {
            CPH.LogError($"[GWM] Invalid JSON in '{ConfigGlobal}': {ex.Message}");
            return false;
        }
    }

    private JObject NewState()
    {
        JObject state = new JObject();
        state["schemaVersion"] = 1;
        state["giveawayId"] = "default";
        state["entries"] = new JArray();
        state["winners"] = new JArray();
        return state;
    }

    private void SaveState(JObject state)
    {
        state["updatedAtUtc"] = DateTime.UtcNow.ToString("o");
        CPH.SetGlobalVar(StateGlobal, state.ToString(Newtonsoft.Json.Formatting.None), true);
    }

    private bool CallerIsAllowed(JObject config)
    {
        if (!HasCallerContext())
        {
            return true;
        }

        JObject permissions = config["permissions"] as JObject;
        string permission = NormalizeKey(GetString(permissions, "manage", "moderator"));
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

    private void SendResponse(JObject config, string responseKey, string displayName)
    {
        JObject responses = config["responses"] as JObject;
        string template = GetString(responses, responseKey, "The giveaway has been cleared.");
        string message = template.Replace("{displayName}", displayName ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(message))
        {
            CPH.SendMessage(message, true, true);
        }
    }

    private string GetString(JObject obj, string key, string defaultValue)
    {
        JToken token = obj == null ? null : obj[key];
        string value = token == null ? "" : token.ToString();
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
    }

    private string NormalizeKey(string value)
    {
        return (value ?? "").Trim().ToLowerInvariant();
    }
}
