using System;
using System.Globalization;
using Newtonsoft.Json.Linq;

public class CPHInline
{
    private const string ConfigGlobal = "activityGatedAnnouncements.config";
    private const string CountPrefix = "activityGatedAnnouncements.chatCounts.";
    private const string LastSentPrefix = "activityGatedAnnouncements.lastSentUtc.";

    public bool Execute()
    {
        if (!TryLoadConfig(out JObject config))
        {
            return true;
        }

        JObject targets = config["targets"] as JObject;
        JArray jobs = config["jobs"] as JArray;

        if (targets == null)
        {
            CPH.LogError("[AGA] Config has no targets object.");
            return true;
        }

        if (jobs == null)
        {
            CPH.LogError("[AGA] Config has no jobs array.");
            return true;
        }

        DateTime now = DateTime.UtcNow;

        foreach (JObject job in jobs.Children<JObject>())
        {
            string jobId = Normalize(GetString(job, "id"));
            if (string.IsNullOrWhiteSpace(jobId))
            {
                CPH.LogWarn("[AGA] Skipping a job with no id.");
                continue;
            }

            if (!IsEnabled(job["enabled"], true))
            {
                continue;
            }

            JArray targetIds = job["targetIds"] as JArray;
            if (targetIds == null || targetIds.Count == 0)
            {
                CPH.LogWarn($"[AGA] Job '{jobId}' has no explicit targetIds.");
                continue;
            }

            foreach (JToken targetIdToken in targetIds)
            {
                string targetId = Normalize(targetIdToken.ToString());
                JObject target = targets[targetId] as JObject;

                if (target == null)
                {
                    CPH.LogWarn($"[AGA] Job '{jobId}' references unknown target '{targetId}'.");
                    continue;
                }

                if (!IsEnabled(target["enabled"], true))
                {
                    continue;
                }

                int minChats = Math.Max(1, GetInt(job, "minChats", 1));
                int intervalMinutes = Math.Max(1, GetInt(job, "intervalMinutes", 30));
                string countGlobal = CountPrefix + jobId + "." + targetId;
                int chatCount = CPH.GetGlobalVar<int?>(countGlobal, true) ?? 0;

                if (chatCount < minChats)
                {
                    continue;
                }

                if (!HasIntervalElapsed(jobId, targetId, intervalMinutes, now))
                {
                    continue;
                }

                string message = ResolveMessage(config, job, targetId);
                if (string.IsNullOrWhiteSpace(message))
                {
                    CPH.LogWarn($"[AGA] Job '{jobId}' resolved a blank message for target '{targetId}'.");
                    continue;
                }

                if (TrySendAnnouncement(job, target, targetId, message))
                {
                    CPH.SetGlobalVar(countGlobal, 0, true);
                    CPH.SetGlobalVar(LastSentPrefix + jobId + "." + targetId, now.ToString("o"), true);
                    CPH.LogInfo($"[AGA] Sent job '{jobId}' to target '{targetId}'.");
                }
            }
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

    private bool TrySendAnnouncement(JObject job, JObject target, string targetId, string message)
    {
        string jobId = Normalize(GetString(job, "id"));
        string platform = Normalize(GetString(target, "platform"));
        string senderAction = Normalize(GetString(target, "senderAction"));

        if (string.IsNullOrWhiteSpace(senderAction))
        {
            CPH.LogWarn($"[AGA] Target '{targetId}' has no senderAction.");
            return false;
        }

        if (!CPH.ActionExists(senderAction))
        {
            CPH.LogWarn($"[AGA] Sender action '{senderAction}' for target '{targetId}' does not exist.");
            return false;
        }

        CPH.SetArgument("message", message);
        CPH.SetArgument("targetId", targetId);
        CPH.SetArgument("platform", platform);
        CPH.SetArgument("jobId", jobId);

        try
        {
            bool ranAction = CPH.RunAction(senderAction, true);
            if (!ranAction)
            {
                CPH.LogWarn($"[AGA] Sender action '{senderAction}' returned false for job '{jobId}'.");
            }

            return ranAction;
        }
        catch (Exception ex)
        {
            CPH.LogError($"[AGA] Sender action '{senderAction}' failed for job '{jobId}': {ex.Message}");
            return false;
        }
    }

    private string ResolveMessage(JObject config, JObject job, string targetId)
    {
        JObject messagesByTarget = job["messagesByTarget"] as JObject;

        if (messagesByTarget != null)
        {
            string targetMessage = Normalize(ResolveConfigVariables(config, GetString(messagesByTarget, targetId)));
            if (!string.IsNullOrWhiteSpace(targetMessage))
            {
                return targetMessage;
            }
        }

        return Normalize(ResolveConfigVariables(config, GetString(job, "defaultMessage")));
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

    private bool HasIntervalElapsed(string jobId, string targetId, int intervalMinutes, DateTime nowUtc)
    {
        string lastSentGlobal = LastSentPrefix + jobId + "." + targetId;
        string lastSentValue = CPH.GetGlobalVar<string>(lastSentGlobal, true);

        if (string.IsNullOrWhiteSpace(lastSentValue))
        {
            return true;
        }

        DateTime lastSentUtc;
        if (
            !DateTime.TryParse(
                lastSentValue,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out lastSentUtc
            )
        )
        {
            CPH.LogWarn($"[AGA] Could not parse '{lastSentGlobal}' value '{lastSentValue}'. Allowing send.");
            return true;
        }

        return nowUtc >= lastSentUtc.AddMinutes(intervalMinutes);
    }

    private int GetInt(JObject obj, string key, int defaultValue)
    {
        JToken token = obj == null ? null : obj[key];
        if (token == null)
        {
            return defaultValue;
        }

        int parsed;
        return int.TryParse(token.ToString(), out parsed) ? parsed : defaultValue;
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
        return bool.TryParse(token.ToString(), out parsed) ? parsed : defaultValue;
    }

    private string Normalize(string value)
    {
        return (value ?? "").Trim();
    }
}
