using System;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

public class CPHInline
{
    private const string ConfigGlobal = "firstChatShoutouts.config";
    private const string Usage = "Usage: !soautoadd <login> [custom announcement template, supports {lastGame}]";

    public bool Execute()
    {
        if (!TryLoadConfig(out JObject config))
        {
            return true;
        }

        JObject autoAdd = config["autoAdd"] as JObject;
        if (!IsEnabled(autoAdd == null ? null : autoAdd["enabled"], true))
        {
            return true;
        }

        if (IsEnabled(autoAdd == null ? null : autoAdd["moderatorOnly"], true) && !CallerIsModeratorOrBroadcaster())
        {
            CPH.LogWarn("[FCS] Auto shoutout add denied because caller is not a moderator or broadcaster.");
            return true;
        }

        ParseCommandInput(GetFirstStringArg("rawInput", "input"), out string login, out string announcementTemplate);
        if (string.IsNullOrWhiteSpace(login))
        {
            login = NormalizeLogin(GetFirstStringArg("input0", "targetUser", "targetLogin", "shoutoutLogin"));
            announcementTemplate = GetFirstStringArg("input1", "customMessage", "message", "announcementTemplate").Trim();
        }

        if (string.IsNullOrWhiteSpace(login))
        {
            CPH.SendMessage(Usage);
            return true;
        }

        JArray people = EnsurePeopleArray(config);
        bool created = UpsertPerson(people, login, announcementTemplate);

        CPH.SetGlobalVar(ConfigGlobal, config.ToString(Newtonsoft.Json.Formatting.None), true);

        string templateNote = string.IsNullOrWhiteSpace(announcementTemplate) ? "" : " with a custom announcement template";
        CPH.SendMessage($"Automatic shoutouts {(created ? "now include" : "updated")} @{login}{templateNote}.");
        CPH.LogInfo($"[FCS] Auto shoutout config {(created ? "added" : "updated")} '{login}'. customTemplate={!string.IsNullOrWhiteSpace(announcementTemplate)}.");
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

    private void ParseCommandInput(string rawInput, out string login, out string announcementTemplate)
    {
        login = "";
        announcementTemplate = "";

        rawInput = (rawInput ?? "").Trim();
        if (string.IsNullOrWhiteSpace(rawInput))
        {
            return;
        }

        Match match = Regex.Match(rawInput, @"^(\S+)(?:\s+(.*))?$");
        if (!match.Success)
        {
            return;
        }

        login = NormalizeLogin(match.Groups[1].Value);
        announcementTemplate = match.Groups.Count > 2 ? (match.Groups[2].Value ?? "").Trim() : "";
    }

    private JArray EnsurePeopleArray(JObject config)
    {
        JArray people = config["people"] as JArray;
        if (people == null)
        {
            people = new JArray();
            config["people"] = people;
        }

        return people;
    }

    private bool UpsertPerson(JArray people, string login, string announcementTemplate)
    {
        JObject person = FindPerson(people, login);
        bool created = person == null;
        if (person == null)
        {
            person = new JObject();
            people.Add(person);
        }

        person["login"] = login;
        person["enabled"] = true;

        if (!string.IsNullOrWhiteSpace(announcementTemplate))
        {
            person["announcementTemplate"] = announcementTemplate.Trim();
        }

        return created;
    }

    private JObject FindPerson(JArray people, string login)
    {
        foreach (JObject person in people.Children<JObject>())
        {
            if (string.Equals(NormalizeLogin(GetString(person, "login")), login, StringComparison.OrdinalIgnoreCase))
            {
                return person;
            }
        }

        return null;
    }

    private string GetFirstStringArg(params string[] argNames)
    {
        foreach (string argName in argNames)
        {
            string value;
            if (CPH.TryGetArg(argName, out value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return "";
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

    private string GetString(JObject obj, string key, string defaultValue = "")
    {
        JToken token = obj == null ? null : obj[key];
        string value = token == null ? "" : token.ToString();
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    private string NormalizeLogin(string value)
    {
        value = (value ?? "").Trim();
        if (value.StartsWith("@"))
        {
            value = value.Substring(1);
        }

        if (!Regex.IsMatch(value, @"^[A-Za-z0-9_]{1,25}$"))
        {
            return "";
        }

        return value.ToLowerInvariant();
    }
}
