using System;

public class CPHInline
{
    public bool Execute()
    {
        CPH.TryGetArg("message", out string message);
        message = (message ?? "").Trim();

        if (string.IsNullOrWhiteSpace(message))
        {
            CPH.LogWarn("[AGA] Twitch sender received a blank message.");
            return false;
        }

        CPH.SendMessage(message, true, true);
        return true;
    }
}
