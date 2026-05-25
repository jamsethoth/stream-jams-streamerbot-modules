using System;

public class CPHInline
{
    public bool Execute()
    {
        CPH.SetArgument("targetId", "youtube_main");
        bool tracked = CPH.RunAction("AGA - Track Chat Activity", true);
        bool scheduled = CPH.RunAction("AGA - Run Announcement Scheduler", true);
        return tracked && scheduled;
    }
}
