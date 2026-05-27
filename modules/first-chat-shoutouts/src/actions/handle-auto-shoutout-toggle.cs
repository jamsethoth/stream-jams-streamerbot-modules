using System;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

public class CPHInline
{
    private const string ConfigGlobal = "firstChatShoutouts.config";

    public bool Execute()
    {
        if (!TryLoadConfig(out JObject config))
        {
            return true;
        }

        JObject autoToggle = config["autoToggle"] as JObject;
        if (!IsEnabled(autoToggle == null ? null : autoToggle["enabled"], true))
        {
            return true;
        }

        if (IsEnabled(autoToggle == null ? null : autoToggle["moderatorOnly"], true) && !CallerIsModeratorOrBroadcaster())
        {
            CPH.LogWarn("[FCS] Auto shoutout toggle denied because caller is not a moderator or broadcaster.");
            return true;
        }

        string requestedState = NormalizeKey(GetFirstStringArg("rawInput", "input0", "state", "toggle"));
        if (!TryParseEnabledState(requestedState, out bool enabled))
        {
            CPH.SendMessage("Usage: !soauto on|off");
            return true;
        }

        JObject automatic = config["automatic"] as JObject;
        if (automatic == null)
        {
            automatic = new JObject();
            config["automatic"] = automatic;
        }

        automatic["enabled"] = enabled;
        CPH.SetGlobalVar(ConfigGlobal, config.ToString(Newtonsoft.Json.Formatting.None), true);
        CPH.SendMessage($"Automatic shoutouts are now {(enabled ? "on" : "off")}.");
        CPH.LogInfo($"[FCS] automatic.enabled set to {enabled} by chat command.");
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

    private string GetFirstStringArg(params string[] argNames)
    {
        foreach (string argName in argNames)
        {
            string value;
            if (CPH.TryGetArg(argName, out value) && !string.IsNullOrWhiteSpace(value))
            {
                Match match = Regex.Match(value.Trim(), @"[A-Za-z0-9_]+");
                return match.Success ? match.Value : "";
            }
        }

        return "";
    }

    private bool TryParseEnabledState(string value, out bool enabled)
    {
        enabled = false;
        switch (NormalizeKey(value))
        {
            case "on":
            case "enable":
            case "enabled":
            case "true":
            case "1":
                enabled = true;
                return true;
            case "off":
            case "disable":
            case "disabled":
            case "false":
            case "0":
                enabled = false;
                return true;
            default:
                return false;
        }
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

    private string NormalizeKey(string value)
    {
        return (value ?? "").Trim().ToLowerInvariant();
    }
}
