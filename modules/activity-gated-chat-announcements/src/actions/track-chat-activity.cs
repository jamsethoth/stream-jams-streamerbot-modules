using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

public class CPHInline
{
    private const string ConfigGlobal = "activityGatedAnnouncements.config";
    private const string CountPrefix = "activityGatedAnnouncements.chatCounts.";

    public bool Execute()
    {
        CPH.TryGetArg("targetId", out string targetId);
        targetId = Normalize(targetId);

        if (string.IsNullOrWhiteSpace(targetId))
        {
            CPH.LogError("[AGA] Track Chat Activity was called without targetId.");
            return true;
        }

        if (!TryLoadConfig(out JObject config))
        {
            return true;
        }

        JObject targets = config["targets"] as JObject;
        JObject target = targets == null ? null : targets[targetId] as JObject;

        if (target == null)
        {
            CPH.LogWarn($"[AGA] Chat activity target '{targetId}' is not configured.");
            return true;
        }

        if (!IsEnabled(target["enabled"], true))
        {
            return true;
        }

        if (IsIgnoredChatEvent(config, target))
        {
            if (IsDebugEnabled(config))
            {
                CPH.LogInfo($"[AGA] Ignored chat event for target '{targetId}'.");
            }

            return true;
        }

        JArray jobs = config["jobs"] as JArray;
        if (jobs == null)
        {
            CPH.LogWarn("[AGA] Config has no jobs array.");
            return true;
        }

        int incrementedJobs = 0;

        foreach (JObject job in jobs.Children<JObject>())
        {
            string jobId = Normalize(GetString(job, "id"));
            if (string.IsNullOrWhiteSpace(jobId))
            {
                CPH.LogWarn("[AGA] Skipping a job with no id.");
                continue;
            }

            if (!IsEnabledJobForTarget(job, targetId))
            {
                continue;
            }

            string countGlobal = CountPrefix + jobId + "." + targetId;
            int currentCount = CPH.GetGlobalVar<int?>(countGlobal, true) ?? 0;
            CPH.SetGlobalVar(countGlobal, currentCount + 1, true);
            incrementedJobs++;
        }

        if (IsDebugEnabled(config))
        {
            CPH.LogInfo($"[AGA] Counted chat for target '{targetId}' across {incrementedJobs} job(s).");
        }

        return true;
    }

    private bool TryLoadConfig(out JObject config)
    {
        config = null;
        string configJson = CPH.GetGlobalVar<string>(ConfigGlobal, true);

        if (string.IsNullOrWhiteSpace(configJson))
        {
            CPH.LogError($"[AGA] Missing JSON global '{ConfigGlobal}'.");
            return false;
        }

        try
        {
            config = JObject.Parse(configJson);
            return true;
        }
        catch (Exception ex)
        {
            CPH.LogError($"[AGA] Invalid JSON in '{ConfigGlobal}': {ex.Message}");
            return false;
        }
    }

    private bool IsIgnoredChatEvent(JObject config, JObject target)
    {
        if (AnyBooleanArgIsTrue(
            "isBot",
            "bot",
            "fromBot",
            "isBroadcaster",
            "isMe",
            "isSelf",
            "isSystem",
            "isSystemMessage"
        ))
        {
            return true;
        }

        string messageType = GetFirstStringArg("messageType", "chatMessageType", "eventType");
        if (EqualsAny(messageType, "system", "notice", "automod", "announcement"))
        {
            return true;
        }

        string userName = GetFirstStringArg("userName", "username", "user", "displayName", "login");
        if (string.IsNullOrWhiteSpace(userName))
        {
            return false;
        }

        HashSet<string> ignoredUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddConfiguredNames(ignoredUsers, config, config["ignoredUsers"]);
        AddConfiguredNames(ignoredUsers, config, target["ignoredUsers"]);
        AddConfiguredNames(ignoredUsers, config, target["selfUsers"]);
        AddConfiguredNames(ignoredUsers, config, target["broadcasterUsers"]);
        AddConfiguredNames(ignoredUsers, config, target["botUsers"]);

        return ignoredUsers.Contains(userName);
    }

    private bool IsEnabledJobForTarget(JObject job, string targetId)
    {
        if (!IsEnabled(job["enabled"], true))
        {
            return false;
        }

        JArray targetIds = job["targetIds"] as JArray;
        if (targetIds == null || targetIds.Count == 0)
        {
            CPH.LogWarn($"[AGA] Job '{GetString(job, "id")}' has no explicit targetIds.");
            return false;
        }

        foreach (JToken token in targetIds)
        {
            if (string.Equals(Normalize(token.ToString()), targetId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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

    private string GetFirstStringArg(params string[] argNames)
    {
        foreach (string argName in argNames)
        {
            string value;
            if (CPH.TryGetArg(argName, out value) && !string.IsNullOrWhiteSpace(value))
            {
                return Normalize(value);
            }
        }

        return "";
    }

    private bool EqualsAny(string value, params string[] candidates)
    {
        foreach (string candidate in candidates)
        {
            if (string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void AddConfiguredNames(HashSet<string> names, JObject config, JToken token)
    {
        if (token == null)
        {
            return;
        }

        JArray array = token as JArray;
        if (array != null)
        {
            foreach (JToken item in array)
            {
                AddName(names, ResolveConfigVariables(config, item.ToString()));
            }

            return;
        }

        AddName(names, ResolveConfigVariables(config, token.ToString()));
    }

    private string ResolveConfigVariables(JObject config, string value)
    {
        string resolved = value ?? "";
        JObject variables = config["variables"] as JObject;

        if (variables == null)
        {
            return resolved;
        }

        foreach (JProperty variable in variables.Properties())
        {
            string globalName = Normalize(variable.Value.ToString());
            if (string.IsNullOrWhiteSpace(globalName))
            {
                continue;
            }

            string globalValue = CPH.GetGlobalVar<string>(globalName, true) ?? "";
            resolved = resolved.Replace("{" + variable.Name + "}", globalValue);
        }

        return resolved;
    }

    private void AddName(HashSet<string> names, string name)
    {
        name = Normalize(name);
        if (!string.IsNullOrWhiteSpace(name))
        {
            names.Add(name);
        }
    }

    private string GetString(JObject obj, string key)
    {
        JToken token = obj == null ? null : obj[key];
        return token == null ? "" : token.ToString();
    }

    private bool IsEnabled(JToken token, bool defaultValue)
    {
        if (token == null)
        {
            return defaultValue;
        }

        bool parsed;
        if (bool.TryParse(token.ToString(), out parsed))
        {
            return parsed;
        }

        return defaultValue;
    }

    private bool IsDebugEnabled(JObject config)
    {
        return IsEnabled(config["debugLogging"], false);
    }

    private string Normalize(string value)
    {
        return (value ?? "").Trim();
    }
}
