﻿using System;


namespace fCraft {
    /// <summary>
    /// Enumeration of available configuration keys. See comment
    /// at the top of Config.cs for a history of changes.
    /// </summary>
    public enum ConfigKey {
        ServerName,
        MOTD,
        MaxPlayers,
        DefaultRank,
        IsPublic,
        Port,
        IP,
        UploadBandwidth,

        DefaultBuildRank,

        RankColorsInChat,
        RankColorsInWorldNames,
        RankPrefixesInChat,
        RankPrefixesInList,
        ShowJoinedWorldMessages,
        SystemMessageColor,
        HelpColor,
        SayColor,
        AnnouncementColor,
        PrivateMessageColor,
        MeColor,
        WarningColor,
        AnnouncementInterval,

        VerifyNames,
        LimitOneConnectionPerIP,
        AllowUnverifiedLAN,
        PatrolledRank,
        AntispamMessageCount,
        AntispamInterval,
        AntispamMuteDuration,
        AntispamMaxWarnings,

        RequireBanReason,
        RequireRankChangeReason,
        AnnounceKickAndBanReasons,
        AnnounceRankChanges,

        SaveOnShutdown,
        SaveInterval,

        BackupOnStartup,
        BackupOnJoin,
        BackupOnlyWhenChanged,
        BackupInterval,
        MaxBackups,
        MaxBackupSize, // in megabytes

        LogMode,
        MaxLogs,

        IRCBot,
        IRCBotNick,
        IRCBotNetwork,
        IRCBotPort,
        IRCBotChannels,
        IRCBotForwardFromServer,
        IRCBotForwardFromIRC,
        IRCBotAnnounceServerJoins,
        IRCBotAnnounceIRCJoins,
        IRCRegisteredNick,
        IRCNickServ,
        IRCNickServMessage,
        IRCMessageColor,
        IRCDelay,
        IRCThreads,

        SendRedundantBlockUpdates,
        AutomaticUpdates,
        NoPartialPositionUpdates,
        ProcessPriority,
        BlockUpdateThrottling,
        TickInterval,
        LowLatencyMode,
        SubmitCrashReports,
        MaxUndo,

        MapPath,
        LogPath,
        DataPath,

        AutoRankEnabled
    }
}
