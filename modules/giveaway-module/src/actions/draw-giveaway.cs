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
            CPH.LogWarn("[GWM] Draw Giveaway denied because caller is not a moderator or broadcaster.");
            return true;
        }

        JObject state;
        if (!TryLoadState(out state))
        {
            return true;
        }

        JArray entries = EnsureArray(state, "entries");
        JArray winners = EnsureArray(state, "winners");
        if (entries.Count == 0)
        {
            SendResponse(config, "noEntries", "");
            return true;
        }

        int winnerIndex = new Random().Next(entries.Count);
        JObject winner = entries[winnerIndex] as JObject;
        if (winner == null)
        {
            CPH.LogError("[GWM] Selected giveaway entry was not a JSON object; draw was not completed.");
            return true;
        }

        entries.RemoveAt(winnerIndex);
        winner["drawnAtUtc"] = DateTime.UtcNow.ToString("o");
        winners.Add(winner);

        SaveState(state);
        SendResponse(config, "winner", GetString(winner, "displayName", "there"));
        CPH.LogInfo($"[GWM] Giveaway winner drawn: {GetString(winner, "displayName", "unknown")}.");
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

    private bool TryLoadState(out JObject state)
    {
        state = null;
        string stateJson = CPH.GetGlobalVar<string>(StateGlobal, true);
        if (string.IsNullOrWhiteSpace(stateJson))
        {
            state = NewState();
            SaveState(state);
            return true;
        }

        try
        {
            state = JObject.Parse(stateJson);
            EnsureArray(state, "entries");
            EnsureArray(state, "winners");
            return true;
        }
        catch (Exception ex)
        {
            CPH.LogError($"[GWM] Invalid JSON in '{StateGlobal}'; draw was not completed: {ex.Message}");
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

    private JArray EnsureArray(JObject state, string key)
    {
        JArray array = state[key] as JArray;
        if (array == null)
        {
            array = new JArray();
            state[key] = array;
        }

        return array;
    }

    private void SaveState(JObject state)
    {
        state["schemaVersion"] = 1;
        state["giveawayId"] = FirstNonBlank(GetString(state, "giveawayId"), "default");
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
        string template = GetString(responses, responseKey, DefaultResponse(responseKey));
        string message = template.Replace("{displayName}", displayName ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(message))
        {
            CPH.SendMessage(message, true, true);
        }
    }

    private string DefaultResponse(string responseKey)
    {
        if (responseKey == "winner")
        {
            return "The giveaway winner is {displayName}!";
        }

        return "There are no giveaway entries to draw.";
    }

    private string GetString(JObject obj, string key, string defaultValue = "")
    {
        JToken token = obj == null ? null : obj[key];
        string value = token == null ? "" : token.ToString();
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
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

    private string NormalizeKey(string value)
    {
        return (value ?? "").Trim().ToLowerInvariant();
    }
}
