using System;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

public class CPHInline
{
    private const string ConfigGlobal = "firstChatShoutouts.config";
    private const string SessionGlobal = "firstChatShoutouts.streamSessionId";
    private const string SentPrefix = "firstChatShoutouts.sent.";

    public bool Execute()
    {
        CPH.TryGetArg("targetId", out string targetId);
        CPH.TryGetArg("shoutoutLogin", out string shoutoutLogin);
        CPH.TryGetArg("shoutoutSource", out string shoutoutSource);

        targetId = NormalizeKey(targetId);
        shoutoutLogin = NormalizeLogin(shoutoutLogin);
        shoutoutSource = NormalizeKey(shoutoutSource);

        if (string.IsNullOrWhiteSpace(targetId))
        {
            CPH.LogError("[FCS] Run Shoutout was called without targetId.");
            return true;
        }

        if (string.IsNullOrWhiteSpace(shoutoutLogin))
        {
            CPH.LogError("[FCS] Run Shoutout was called without shoutoutLogin.");
            return true;
        }

        if (!TryLoadConfig(out JObject config))
        {
            return true;
        }

        JObject target = GetTarget(config, targetId);
        if (target == null)
        {
            CPH.LogWarn($"[FCS] Target '{targetId}' is not configured.");
            return true;
        }

        if (!IsEnabled(target["enabled"], true))
        {
            return true;
        }

        bool automatic = IsAutomaticSource(shoutoutSource);
        bool manualAll = IsManualAllSource(shoutoutSource);
        JObject person = FindPerson(config, shoutoutLogin);
        bool personEnabled = person != null && IsEnabled(person["enabled"], true);

        if (automatic)
        {
            if (!IsEnabled(config["automatic"] == null ? null : config["automatic"]["enabled"], true))
            {
                return true;
            }

            if (!IncludesTarget(config["automatic"] as JObject, targetId))
            {
                return true;
            }

            if (!personEnabled)
            {
                if (IsDebugEnabled(config))
                {
                    CPH.LogInfo($"[FCS] Automatic shoutout skipped for unconfigured login '{shoutoutLogin}'.");
                }

                return true;
            }

            if (AlreadyHandled(targetId, shoutoutLogin))
            {
                if (IsDebugEnabled(config))
                {
                    CPH.LogInfo($"[FCS] Automatic shoutout skipped for already handled login '{shoutoutLogin}'.");
                }

                return true;
            }
        }
        else if (manualAll)
        {
            JObject manualAllConfig = config["manualAll"] as JObject;
            if (!IsEnabled(manualAllConfig == null ? null : manualAllConfig["enabled"], true))
            {
                return true;
            }

            if (!IncludesTarget(manualAllConfig, targetId))
            {
                return true;
            }

            if (IsEnabled(manualAllConfig == null ? null : manualAllConfig["moderatorOnly"], true) && !CallerIsModeratorOrBroadcaster())
            {
                CPH.LogWarn($"[FCS] Manual shoutout-all denied for '{shoutoutLogin}' because caller is not a moderator or broadcaster.");
                return true;
            }

            if (!personEnabled)
            {
                CPH.LogWarn($"[FCS] Manual shoutout-all skipped for unconfigured login '{shoutoutLogin}'.");
                return true;
            }
        }
        else
        {
            JObject manual = config["manual"] as JObject;
            if (!IsEnabled(manual == null ? null : manual["enabled"], true))
            {
                return true;
            }

            if (!IncludesTarget(manual, targetId))
            {
                return true;
            }

            if (IsEnabled(manual == null ? null : manual["moderatorOnly"], true) && !CallerIsModeratorOrBroadcaster())
            {
                CPH.LogWarn($"[FCS] Manual shoutout denied for '{shoutoutLogin}' because caller is not a moderator or broadcaster.");
                return true;
            }

            bool allowAnyLogin = IsEnabled(manual == null ? null : manual["allowAnyLogin"], true);
            if (!allowAnyLogin && !personEnabled)
            {
                CPH.LogWarn($"[FCS] Manual shoutout skipped for unconfigured login '{shoutoutLogin}'.");
                return true;
            }
        }

        TwitchLookupResult lookup = LookupTwitchUser(shoutoutLogin, GetString(config, "lastGameFallback", "something excellent"));
        string template = GetAnnouncementTemplate(config, person);
        string message = ResolveTemplate(template, targetId, target, shoutoutLogin, lookup);

        if (string.IsNullOrWhiteSpace(message))
        {
            CPH.LogWarn($"[FCS] Shoutout template resolved blank for login '{shoutoutLogin}'.");
            return true;
        }

        bool nativeSucceeded = TryNativeTwitchShoutout(target, shoutoutLogin);
        bool announcementAttempted = TrySendTwitchAnnouncement(target, message);

        if (announcementAttempted)
        {
            MarkHandled(targetId, shoutoutLogin);
        }

        if (IsDebugEnabled(config))
        {
            CPH.LogInfo($"[FCS] Shoutout processed for '{shoutoutLogin}'. nativeSucceeded={nativeSucceeded}; announcementAttempted={announcementAttempted}.");
        }

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

    private JObject GetTarget(JObject config, string targetId)
    {
        JObject targets = config["targets"] as JObject;
        return targets == null ? null : targets[targetId] as JObject;
    }

    private JObject FindPerson(JObject config, string login)
    {
        JArray people = config["people"] as JArray;
        if (people == null)
        {
            return null;
        }

        foreach (JObject person in people.Children<JObject>())
        {
            if (string.Equals(NormalizeLogin(GetString(person, "login")), login, StringComparison.OrdinalIgnoreCase))
            {
                return person;
            }
        }

        return null;
    }

    private bool IncludesTarget(JObject section, string targetId)
    {
        if (section == null)
        {
            return true;
        }

        JArray targetIds = section["targetIds"] as JArray;
        if (targetIds == null || targetIds.Count == 0)
        {
            return true;
        }

        foreach (JToken token in targetIds)
        {
            if (string.Equals(NormalizeKey(token.ToString()), targetId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryNativeTwitchShoutout(JObject target, string login)
    {
        if (!IsTwitchTarget(target) || !IsEnabled(target["nativeShoutoutEnabled"], true))
        {
            return false;
        }

        try
        {
            bool sent = CPH.TwitchSendShoutoutByLogin(login);
            if (!sent)
            {
                CPH.LogWarn($"[FCS] Native Twitch shoutout returned false for '{login}'. The announcement will still be sent.");
            }

            return sent;
        }
        catch (Exception ex)
        {
            CPH.LogWarn($"[FCS] Native Twitch shoutout failed for '{login}': {ex.Message}");
            return false;
        }
    }

    private bool TrySendTwitchAnnouncement(JObject target, string message)
    {
        if (!IsTwitchTarget(target) || !IsEnabled(target["announcementEnabled"], true))
        {
            return false;
        }

        string color = NormalizeKey(GetString(target, "announcementColor", "purple"));
        if (!IsAnnouncementColor(color))
        {
            color = "purple";
        }

        bool useBot = IsEnabled(target["announcementUseBot"], true);
        bool fallback = IsEnabled(target["announcementFallbackToBroadcaster"], true);

        try
        {
            CPH.TwitchAnnounce(message, useBot, color, fallback);
            return true;
        }
        catch (Exception ex)
        {
            CPH.LogError($"[FCS] Twitch announcement failed: {ex.Message}");
            return false;
        }
    }

    private TwitchLookupResult LookupTwitchUser(string login, string lastGameFallback)
    {
        TwitchLookupResult result = new TwitchLookupResult();
        result.Login = login;
        result.DisplayName = login;
        result.LastGame = string.IsNullOrWhiteSpace(lastGameFallback) ? "something excellent" : lastGameFallback;
        result.ChannelTitle = "";

        try
        {
            var info = CPH.TwitchGetExtendedUserInfoByLogin(login);
            if (info == null)
            {
                return result;
            }

            result.DisplayName = FirstNonBlank(info.UserName, info.UserLogin, login);
            result.Login = NormalizeLogin(FirstNonBlank(info.UserLogin, login));
            result.LastGame = FirstNonBlank(info.Game, result.LastGame);
            result.ChannelTitle = FirstNonBlank(info.ChannelTitle, "");
            return result;
        }
        catch (Exception ex)
        {
            CPH.LogWarn($"[FCS] Could not load Twitch user info for '{login}': {ex.Message}");
            return result;
        }
    }

    private string GetAnnouncementTemplate(JObject config, JObject person)
    {
        string personTemplate = GetString(person, "announcementTemplate");
        if (!string.IsNullOrWhiteSpace(personTemplate))
        {
            return personTemplate;
        }

        return GetString(
            config,
            "defaultAnnouncementTemplate",
            "Go follow @{login} at https://twitch.tv/{login}! They were last streaming {lastGame}."
        );
    }

    private string ResolveTemplate(string template, string targetId, JObject target, string requestedLogin, TwitchLookupResult lookup)
    {
        string platform = NormalizeKey(GetString(target, "platform", "twitch"));
        string resolved = template ?? "";
        resolved = resolved.Replace("{login}", FirstNonBlank(lookup.Login, requestedLogin));
        resolved = resolved.Replace("{displayName}", FirstNonBlank(lookup.DisplayName, requestedLogin));
        resolved = resolved.Replace("{lastGame}", lookup.LastGame ?? "");
        resolved = resolved.Replace("{channelTitle}", lookup.ChannelTitle ?? "");
        resolved = resolved.Replace("{targetId}", targetId);
        resolved = resolved.Replace("{platform}", platform);
        return resolved.Trim();
    }

    private bool AlreadyHandled(string targetId, string login)
    {
        string sentGlobal = SentGlobal(targetId, login);
        return CPH.GetGlobalVar<bool?>(sentGlobal, true) ?? false;
    }

    private void MarkHandled(string targetId, string login)
    {
        CPH.SetGlobalVar(SentGlobal(targetId, login), true, true);
    }

    private string SentGlobal(string targetId, string login)
    {
        return SentPrefix + NormalizeKey(targetId) + "." + CurrentSessionId() + "." + NormalizeLogin(login);
    }

    private string CurrentSessionId()
    {
        string sessionId = CPH.GetGlobalVar<string>(SessionGlobal, true);
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            sessionId = DateTime.UtcNow.Ticks.ToString();
            CPH.SetGlobalVar(SessionGlobal, sessionId, true);
        }

        return Regex.Replace(sessionId, @"[^A-Za-z0-9_]", "");
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

    private bool IsAutomaticSource(string source)
    {
        return string.Equals(NormalizeKey(source), "automatic", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsManualAllSource(string source)
    {
        return string.Equals(NormalizeKey(source), "manual_all", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsTwitchTarget(JObject target)
    {
        return string.Equals(NormalizeKey(GetString(target, "platform", "twitch")), "twitch", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsAnnouncementColor(string color)
    {
        return color == "default" || color == "blue" || color == "green" || color == "orange" || color == "purple";
    }

    private bool IsDebugEnabled(JObject config)
    {
        return IsEnabled(config["debugLogging"], false);
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

    private string GetString(JObject obj, string key, string defaultValue = "")
    {
        JToken token = obj == null ? null : obj[key];
        string value = token == null ? "" : token.ToString();
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
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

    private string NormalizeLogin(string value)
    {
        value = (value ?? "").Trim();
        if (value.StartsWith("@"))
        {
            value = value.Substring(1);
        }

        Match match = Regex.Match(value, @"[A-Za-z0-9_]{1,25}");
        return match.Success ? match.Value.ToLowerInvariant() : "";
    }

    private class TwitchLookupResult
    {
        public string Login { get; set; }
        public string DisplayName { get; set; }
        public string LastGame { get; set; }
        public string ChannelTitle { get; set; }
    }
}
