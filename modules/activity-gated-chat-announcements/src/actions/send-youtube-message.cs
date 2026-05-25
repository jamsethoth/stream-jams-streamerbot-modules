using System;

public class CPHInline
{
    public bool Execute()
    {
        CPH.TryGetArg("message", out string message);
        message = (message ?? "").Trim();

        if (string.IsNullOrWhiteSpace(message))
        {
            CPH.LogWarn("[AGA] YouTube sender received a blank message.");
            return false;
        }

        CPH.SendYouTubeMessageToLatestMonitored(message, true, true);
        return true;
    }
}
