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

        JObject state;
        if (!TryLoadState(out state))
        {
            return true;
        }

        string userId = GetFirstStringArg("twitchUserId", "userId", "userID", "user.id");
        string displayName = FirstNonBlank(GetFirstStringArg("displayName", "userName", "username", "user"), "there");
        string login = GetFirstStringArg("login", "userLogin", "userName", "username", "user");
        string source = NormalizeSource(GetFirstStringArg("entrySource", "source"));

        if (string.IsNullOrWhiteSpace(userId))
        {
            CPH.LogWarn($"[GWM] Giveaway entry rejected for '{displayName}' because Twitch user ID was missing.");
            SendResponse(config, "entryFailed", displayName);
            return true;
        }

        JArray entries = EnsureArray(state, "entries");
        JArray winners = EnsureArray(state, "winners");

        if (FindByUserId(winners, userId) != null)
        {
            SendResponse(config, "alreadyWon", displayName);
            return true;
        }

        if (FindByUserId(entries, userId) != null)
        {
            SendResponse(config, "alreadyEntered", displayName);
            return true;
        }

        JObject entry = new JObject();
        entry["twitchUserId"] = userId.Trim();
        entry["displayName"] = displayName.Trim();
        entry["login"] = login.Trim();
        entry["enteredAtUtc"] = DateTime.UtcNow.ToString("o");
        entry["source"] = source;
        AddOptionalArg(entry, "redemptionId");
        AddOptionalArg(entry, "rewardId");
        AddOptionalArg(entry, "rewardName");

        entries.Add(entry);
        SaveState(state);
        SendResponse(config, "entered", displayName);

        if (IsDebugEnabled(config))
        {
            CPH.LogInfo($"[GWM] Added giveaway entry for '{displayName}' from source '{source}'.");
        }

        return true;
    }

    private void AddOptionalArg(JObject entry, string argName)
    {
        string value = GetFirstStringArg(argName);
        if (!string.IsNullOrWhiteSpace(value))
        {
            entry[argName] = value.Trim();
        }
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
            CPH.LogError($"[GWM] Invalid JSON in '{StateGlobal}'; entry was not recorded: {ex.Message}");
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

    private JObject FindByUserId(JArray items, string userId)
    {
        foreach (JObject item in items.Children<JObject>())
        {
            if (string.Equals(GetString(item, "twitchUserId"), userId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }
        }

        return null;
    }

    private void SaveState(JObject state)
    {
        state["schemaVersion"] = 1;
        state["giveawayId"] = FirstNonBlank(GetString(state, "giveawayId"), "default");
        state["updatedAtUtc"] = DateTime.UtcNow.ToString("o");
        CPH.SetGlobalVar(StateGlobal, state.ToString(Newtonsoft.Json.Formatting.None), true);
    }

    private void SendResponse(JObject config, string responseKey, string displayName)
    {
        JObject responses = config["responses"] as JObject;
        string template = GetString(responses, responseKey, DefaultResponse(responseKey));
        string message = ResolveTemplate(template, displayName);
        if (!string.IsNullOrWhiteSpace(message))
        {
            CPH.SendMessage(message, true, true);
        }
    }

    private string DefaultResponse(string responseKey)
    {
        if (responseKey == "alreadyEntered")
        {
            return "{displayName}, you are already entered in the giveaway.";
        }

        if (responseKey == "alreadyWon")
        {
            return "{displayName}, you already won this giveaway. You can enter again after the giveaway is cleared.";
        }

        if (responseKey == "entryFailed")
        {
            return "{displayName}, I could not enter you because Twitch did not provide a user ID.";
        }

        return "{displayName}, you are entered in the giveaway!";
    }

    private string ResolveTemplate(string template, string displayName)
    {
        return (template ?? "").Replace("{displayName}", FirstNonBlank(displayName, "there")).Trim();
    }

    private bool IsDebugEnabled(JObject config)
    {
        bool parsed;
        return bool.TryParse(GetString(config, "debugLogging", "false"), out parsed) && parsed;
    }

    private string NormalizeSource(string source)
    {
        source = (source ?? "").Trim().ToLowerInvariant();
        return source == "reward" ? "reward" : "command";
    }

    private string GetFirstStringArg(params string[] argNames)
    {
        foreach (string argName in argNames)
        {
            string value;
            if (CPH.TryGetArg(argName, out value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return "";
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
}
