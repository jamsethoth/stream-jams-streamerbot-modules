using System;

public class CPHInline
{
    private const string ConfigGlobal = "firstChatShoutouts.config";
    private const string SessionGlobal = "firstChatShoutouts.streamSessionId";

    public bool Execute()
    {
        EnsureGlobal(ConfigGlobal, DefaultConfigJson());

        string sessionId = CPH.GetGlobalVar<string>(SessionGlobal, true);
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            CPH.SetGlobalVar(SessionGlobal, DateTime.UtcNow.Ticks.ToString(), true);
        }

        CPH.LogInfo("[FCS] Default globals are ready. Edit firstChatShoutouts.config to add automatic shoutout people and templates.");
        return true;
    }

    private void EnsureGlobal(string name, string defaultValue)
    {
        string current = CPH.GetGlobalVar<string>(name, true);
        if (string.IsNullOrWhiteSpace(current))
        {
            CPH.SetGlobalVar(name, defaultValue, true);
        }
    }

    private string DefaultConfigJson()
    {
        return @"{
  ""debugLogging"": false,
  ""lastGameFallback"": ""something excellent"",
  ""defaultAnnouncementTemplate"": ""Go follow @{login} at https://twitch.tv/{login}! They were last streaming {lastGame}."",
  ""targets"": {
    ""twitch_main"": {
      ""platform"": ""twitch"",
      ""enabled"": true,
      ""nativeShoutoutEnabled"": true,
      ""announcementEnabled"": true,
      ""announcementColor"": ""purple"",
      ""announcementUseBot"": true,
      ""announcementFallbackToBroadcaster"": true
    }
  },
  ""automatic"": {
    ""enabled"": true,
    ""targetIds"": [
      ""twitch_main""
    ]
  },
  ""manual"": {
    ""enabled"": true,
    ""targetIds"": [
      ""twitch_main""
    ],
    ""allowAnyLogin"": true,
    ""moderatorOnly"": true,
    ""aliases"": [
      ""!so"",
      ""!shoutout""
    ]
  },
  ""people"": [
    {
      ""login"": ""examplecreator"",
      ""enabled"": true,
      ""announcementTemplate"": ""Please show @{login} some love at https://twitch.tv/{login}! They were last streaming {lastGame}.""
    },
    {
      ""login"": ""anothercreator"",
      ""enabled"": true
    }
  ]
}";
    }
}
