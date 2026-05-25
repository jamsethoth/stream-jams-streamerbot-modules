using System;

public class CPHInline
{
    private const string ConfigGlobal = "activityGatedAnnouncements.config";
    private const string DiscordInviteGlobal = "activityGatedAnnouncements.discordInviteUrl";
    private const string TwitchChannelGlobal = "activityGatedAnnouncements.twitchChannelName";
    private const string YouTubeChannelGlobal = "activityGatedAnnouncements.youtubeChannelName";

    public bool Execute()
    {
        EnsureGlobal(DiscordInviteGlobal, "https://discord.gg/example");
        EnsureGlobal(TwitchChannelGlobal, "mychannelname");
        EnsureGlobal(YouTubeChannelGlobal, "mychannelname");
        EnsureGlobal(ConfigGlobal, DefaultConfigJson());

        CPH.LogInfo("[AGA] Default globals are ready. Edit the Discord URL, channel name globals, intervalMinutes, or minChats as needed.");
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
  ""variables"": {
    ""discordInviteUrl"": ""activityGatedAnnouncements.discordInviteUrl"",
    ""twitchChannelName"": ""activityGatedAnnouncements.twitchChannelName"",
    ""youtubeChannelName"": ""activityGatedAnnouncements.youtubeChannelName""
  },
  ""ignoredUsers"": [
    ""streamerbot"",
    ""nightbot"",
    ""streamelements""
  ],
  ""targets"": {
    ""twitch_main"": {
      ""platform"": ""twitch"",
      ""enabled"": true,
      ""senderAction"": ""AGA - Send Twitch Message"",
      ""ignoredUsers"": [
        ""{twitchChannelName}""
      ],
      ""selfUsers"": [
        ""{twitchChannelName}""
      ],
      ""broadcasterUsers"": [
        ""{twitchChannelName}""
      ]
    },
    ""youtube_main"": {
      ""platform"": ""youtube"",
      ""enabled"": true,
      ""senderAction"": ""AGA - Send YouTube Message"",
      ""ignoredUsers"": [
        ""{youtubeChannelName}""
      ],
      ""selfUsers"": [
        ""{youtubeChannelName}""
      ],
      ""broadcasterUsers"": [
        ""{youtubeChannelName}""
      ]
    }
  },
  ""jobs"": [
    {
      ""id"": ""discord"",
      ""enabled"": true,
      ""targetIds"": [
        ""twitch_main"",
        ""youtube_main""
      ],
      ""intervalMinutes"": 30,
      ""minChats"": 25,
      ""defaultMessage"": ""Join our Discord: {discordInviteUrl}"",
      ""messagesByTarget"": {
        ""youtube_main"": ""Join our Discord: {discordInviteUrl}""
      }
    }
  ]
}";
    }
}
