using System;
using System.Text.RegularExpressions;

public class CPHInline
{
    public bool Execute()
    {
        string login = NormalizeLogin(GetFirstStringArg("rawInput", "input0", "targetUser", "targetLogin", "shoutoutLogin"));
        if (string.IsNullOrWhiteSpace(login))
        {
            CPH.LogWarn("[FCS] Manual shoutout command was invoked without a Twitch login.");
            return true;
        }

        CPH.SetArgument("targetId", "twitch_main");
        CPH.SetArgument("shoutoutLogin", login);
        CPH.SetArgument("shoutoutSource", "manual");

        bool ran = CPH.RunAction("FCS - Run Shoutout", true);
        if (!ran)
        {
            CPH.LogWarn($"[FCS] Core shoutout action returned false for manual login '{login}'.");
        }

        return true;
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
}
