using System;

public class CPHInline
{
    public bool Execute()
    {
        string userId = GetFirstStringArg("twitchUserId", "userId", "userID", "user.id");
        string displayName = FirstNonBlank(
            GetFirstStringArg("displayName", "userName", "username", "user"),
            GetFirstStringArg("userLogin", "login"),
            "there"
        );
        string login = GetFirstStringArg("userLogin", "login", "userName", "username", "user");

        if (string.IsNullOrWhiteSpace(userId))
        {
            CPH.LogWarn("[GWM] Command entry was invoked without a Twitch user ID.");
        }

        CPH.SetArgument("entrySource", "command");
        CPH.SetArgument("twitchUserId", userId);
        CPH.SetArgument("displayName", displayName);
        CPH.SetArgument("login", login);

        bool ran = CPH.RunAction("GWM - Enter Giveaway", true);
        if (!ran)
        {
            CPH.LogWarn($"[GWM] Core entry action returned false for command entry from '{displayName}'.");
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
