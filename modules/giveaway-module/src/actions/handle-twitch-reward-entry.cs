using System;
using Newtonsoft.Json.Linq;

public class CPHInline
{
    private const string ConfigGlobal = "giveawayModule.config";

    public bool Execute()
    {
        JObject config;
        if (!TryLoadConfig(out config))
        {
            return true;
        }

        JObject rewardEntry = config["rewardEntry"] as JObject;
        if (!GetBool(rewardEntry == null ? null : rewardEntry["enabled"], true))
        {
            return true;
        }

        string rewardId = GetFirstStringArg("rewardId", "reward.id", "customRewardId");
        string rewardName = GetFirstStringArg("rewardName", "rewardTitle", "reward.name", "reward");
        if (!RewardMatches(rewardEntry, rewardId, rewardName))
        {
            if (IsDebugEnabled(config))
            {
                CPH.LogInfo($"[GWM] Ignored reward redemption for rewardId='{rewardId}' rewardName='{rewardName}'.");
            }

            return true;
        }

        string userId = GetFirstStringArg("twitchUserId", "userId", "userID", "user.id");
        string displayName = FirstNonBlank(
            GetFirstStringArg("displayName", "userName", "username", "user"),
            GetFirstStringArg("userLogin", "login"),
            "there"
        );
        string login = GetFirstStringArg("userLogin", "login", "userName", "username", "user");
        string redemptionId = GetFirstStringArg("redemptionId");

        if (string.IsNullOrWhiteSpace(userId))
        {
            CPH.LogWarn("[GWM] Reward entry was invoked without a Twitch user ID.");
        }

        CPH.SetArgument("entrySource", "reward");
        CPH.SetArgument("twitchUserId", userId);
        CPH.SetArgument("displayName", displayName);
        CPH.SetArgument("login", login);
        CPH.SetArgument("redemptionId", redemptionId);
        CPH.SetArgument("rewardId", rewardId);
        CPH.SetArgument("rewardName", rewardName);

        bool ran = CPH.RunAction("GWM - Enter Giveaway", true);
        if (!ran)
        {
            CPH.LogWarn($"[GWM] Core entry action returned false for reward entry from '{displayName}'.");
        }

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

    private bool RewardMatches(JObject rewardEntry, string rewardId, string rewardName)
    {
        JArray rewardIds = rewardEntry == null ? null : rewardEntry["rewardIds"] as JArray;
        JArray rewardNames = rewardEntry == null ? null : rewardEntry["rewardNames"] as JArray;
        bool hasConfiguredIds = HasValues(rewardIds);
        bool hasConfiguredNames = HasValues(rewardNames);

        if (!hasConfiguredIds && !hasConfiguredNames)
        {
            return GetBool(rewardEntry == null ? null : rewardEntry["matchAnyWhenUnconfigured"], true);
        }

        if (hasConfiguredIds && Contains(rewardIds, rewardId))
        {
            return true;
        }

        return hasConfiguredNames && Contains(rewardNames, rewardName);
    }

    private bool HasValues(JArray values)
    {
        if (values == null)
        {
            return false;
        }

        foreach (JToken value in values)
        {
            if (!string.IsNullOrWhiteSpace(value.ToString()))
            {
                return true;
            }
        }

        return false;
    }

    private bool Contains(JArray values, string expected)
    {
        if (values == null || string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        foreach (JToken value in values)
        {
            if (string.Equals(value.ToString().Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsDebugEnabled(JObject config)
    {
        return GetBool(config["debugLogging"], false);
    }

    private bool GetBool(JToken token, bool defaultValue)
    {
        if (token == null)
        {
            return defaultValue;
        }

        bool parsed;
        return bool.TryParse(token.ToString(), out parsed) ? parsed : defaultValue;
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
