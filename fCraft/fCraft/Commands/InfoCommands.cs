﻿// Copyright 2009, 2010 Matvei Stefarov <me@matvei.org>
using System;
using System.Net;
using System.IO;
using System.Collections.Generic;
using System.Text;


namespace fCraft {
    static class InfoCommands {
        public const string RuleFile = "rules.txt";
        // Register help commands
        internal static void Init() {
            CommandList.RegisterCommand( cdWorldInfo );
            CommandList.RegisterCommand( cdInfo );
            CommandList.RegisterCommand( cdBanInfo );
            CommandList.RegisterCommand( cdClassInfo );

            CommandList.RegisterCommand( cdGetVersion );
            CommandList.RegisterCommand( cdRules );
            CommandList.RegisterCommand( cdHelp );

            CommandList.RegisterCommand( cdWhere );
            CommandList.RegisterCommand( cdWhois );

            CommandList.RegisterCommand( cdPlayers );
            CommandList.RegisterCommand( cdClasses );
        }



        static CommandDescriptor cdWorldInfo = new CommandDescriptor {
            name = "winfo",
            aliases = new string[] { "mapinfo" },
            consoleSafe = true,
            usage = "/winfo [WorldName]",
            help = "Shows information about a world: player count, map dimensions, permissions, etc." +
                   "If no WorldName is given, shows info for current world.",
            handler = WorldInfo
        };

        internal static void WorldInfo( Player player, Command cmd ) {
            string worldName = cmd.Next();
            if( worldName == null ) {
                if( player.world == null ) {
                    player.Message( "Please specify a world name when calling /winfo form console." );
                    return;
                } else {
                    worldName = player.world.name;
                }
            }

            World world = Server.FindWorld( worldName );
            if( world == null ) {
                player.Message( "Unrecognized world name: \"{0}\".", worldName );
                player.Message( "See &H/worlds&S for a list of worlds." );
                return;
            }

            player.Message( "World \"{0}\" has {1} player(s) on.",
                            world.name,
                            world.playerList.Length );

            // If map is not currently loaded, grab its header from disk
            Map map = world.map;
            if( map == null ) {
                map = Map.LoadHeaderOnly( world.GetMapName() );
            }
            if( map == null ) {
                player.Message( "Map information could not be loaded." );
            } else {
                player.Message( "Map dimensions are {0} x {1} x {2}",
                                map.widthX, map.widthY, map.height );
            }

            // Print access/build limits
            if( world.classAccess == ClassList.lowestClass && world.classBuild == ClassList.lowestClass ) {
                player.Message( "Anyone can join or build on {0}", world.name );
            } else {
                if( world.classAccess != ClassList.lowestClass ) {
                    player.Message( "Requires players to be ranked {0}{1}&S+ to join.", world.classAccess.color, world.classAccess.name );
                } else {
                    player.Message( "Anyone can join {0}", world.name );
                }
                if( world.classBuild != ClassList.lowestClass ) {
                    player.Message( "Requires players to be ranked {0}{1}&S+ to build.", world.classBuild.color, world.classBuild.name );
                } else {
                    player.Message( "Anyone can build on {0}", world.name );
                }
            }

            // Print lock/unlock information
            if( world.isLocked ) {
                player.Message( "{0} was locked {1:0}min ago by {2}",
                                world.name,
                                DateTime.UtcNow.Subtract( world.lockedDate ).TotalMinutes,
                                world.lockedBy );
            } else if( world.unlockedBy != null ) {
                player.Message( "{0} was unlocked {1:0}min ago by {2}",
                                world.name,
                                DateTime.UtcNow.Subtract( world.lockedDate ).TotalMinutes,
                                world.lockedBy );
            }
        }



        static CommandDescriptor cdPlayers = new CommandDescriptor {
            name = "players",
            consoleSafe = true,
            usage = "/players [WorldName]",
            help = "Lists all players on the server (in all worlds). " +
                   "If a WorldName is given, only lists players on that one world.",
            handler = Players
        };

        internal static void Players( Player player, Command cmd ) {
            Player[] players = Server.playerList;
            if( players.Length > 0 ) {

                StringBuilder sb = new StringBuilder( "There are " );
                sb.Append( players.Length ).Append( " players on the server: " );
                bool first = true;
                foreach( Player p in players ) {
                    if( p.isHidden ) continue;
                    if( !first ) sb.Append( ", " );
                    sb.Append( p.info.playerClass.color ).Append( p.nick );
                    first = false;
                }
                player.Message( sb.ToString() );
            } else {
                player.Message( "There appear to be no players on the server." );
            }
        }



        static CommandDescriptor cdGetVersion = new CommandDescriptor {
            name = "version",
            consoleSafe = true,
            help = "Shows server software name and version.",
            handler = GetVersion
        };

        internal static void GetVersion( Player player, Command cmd ) {
            player.Message( "fCraft custom server {0}", Updater.GetVersionString() );
        }



        static CommandDescriptor cdWhere = new CommandDescriptor {
            name = "where",
            aliases = new string[] { "compass" },
            consoleSafe = true,
            usage = "/where [PlayerName]",
            help = "Shows information about the location and orientation of a player. " +
                   "If no name is given, shows player's own info.",
            handler = Where
        };

        static string compass = "N . . . nw. . . W . . . sw. . . S . . . se. . . E . . . ne. . . " +
                                "N . . . nw. . . W . . . sw. . . S . . . se. . . E . . . ne. . . ";

        internal static void Where( Player player, Command cmd ) {
            int offset;
            string name = cmd.Next();

            Player target = player;

            if( name != null ) {
                target = Server.FindPlayer( name );
                if( target != null ) {
                    player.Message( "Coordinates of player \"{0}\" (on \"{1}\"):",
                                    target.nick,
                                    target.world.name );
                } else {
                    player.NoPlayerMessage( name );
                    return;
                }
            } else if( player.world == null ) {
                player.Message( "When called form console, &H/where&S requires a player name." );
                return;
            }

            offset = (int)(target.pos.r / 255f * 64f) + 32;

            player.Message( "{0}({1},{2},{3}) - {4}[{5}{6}{7}{4}{8}]",
                            Color.Silver,
                            target.pos.x / 32,
                            target.pos.y / 32,
                            target.pos.h / 32,
                            Color.White,
                            compass.Substring( offset - 12, 11 ),
                            Color.Red,
                            compass.Substring( offset - 1, 3 ),
                            compass.Substring( offset + 2, 11 ) );
        }



        static CommandDescriptor cdHelp = new CommandDescriptor {
            name = "help",
            consoleSafe = true,
            usage = "/help [CommandName]",
            help = "...",
            handler = Help
        };

        const string HelpPrefix = "&S    ";
        internal static void Help( Player player, Command cmd ) {
            string commandName = cmd.Next();

            if( commandName == "commands" ) {
                if( cmd.Next() != null ) {
                    player.MessagePrefixed( "&S    ", "List of all available commands:&N{0}", CommandList.GetCommandList( player, true ) );
                } else {
                    player.MessagePrefixed( "&S    ", "List of all commands:&N{0}", CommandList.GetCommandList( player, false ) );
                }

            } else if( commandName != null ) {
                CommandDescriptor descriptor = CommandList.GetDescriptor( commandName );
                if( descriptor == null ) {
                    player.Message( "Unknown command: \"{0}\"", cmd.name );
                    return;
                }
                StringBuilder sb = new StringBuilder( Color.Help );
                sb.Append( descriptor.usage ).Append( "&N" );

                if( descriptor.aliases != null ) {
                    sb.Append( "Aliases: &H" );
                    bool first = true;
                    foreach( string alias in descriptor.aliases ) {
                        if( !first ) {
                            sb.Append( "&S, &H" );
                        }
                        sb.Append( alias );
                        first = false;
                    }
                    sb.Append( "&N" );
                }

                if( descriptor.helpHandler != null ) {
                    sb.Append( descriptor.helpHandler( player ) );
                } else {
                    sb.Append( descriptor.help );
                }
                player.MessagePrefixed( HelpPrefix, sb.ToString() );

            } else {
                player.Message( "To see a list of all commands, write &H/help commands" );
                player.Message( "To see detailed help for a command, write &H/help CommandName" );
                if( player.world != null ) {
                    player.Message( "To find out about your permissions, write &H/class {0}", player.info.playerClass.name );
                }
                player.Message( "To list available worlds, write &H/worlds" );
                player.Message( "To send private messages, write &H@PlayerName Message" );
                player.Message( "To message all players of a class, write &H@@Class Message" );
            }
        }



        static CommandDescriptor cdWhois = new CommandDescriptor {
            name = "whois",
            consoleSafe = true,
            usage = "/whois PlayerNicknameOrName",
            help = "Shows whether a player uses a real name or nickname. Note: case-sensitive.",
            handler = Whois
        };

        internal static void Whois( Player player, Command cmd ) {
            string name = cmd.Next();
            if( name == null ) {
                cdWhere.PrintUsage( player );
                return;
            }

            Player target = Server.FindPlayerByNick( name );
            if( target != null ) {
                if( target.nick != target.name ) {
                    player.Message( "Player named {0} is using a nickname \"{1}\"",
                                    target.name,
                                    target.nick );
                } else {
                    player.Message( "Player named {0} is not using any nickname.",
                                    target.name );
                }
            } else {
                player.NoPlayerMessage( name );
            }
        }



        static CommandDescriptor cdInfo = new CommandDescriptor {
            name = "info",
            aliases = new string[] { "pinfo" },
            consoleSafe = true,
            usage = "/info [PlayerName]",
            help = "Displays some information and stats about the player. " +
                   "If no name is given, shows your own stats.",
            handler = Info
        };

        internal static void Info( Player player, Command cmd ) {
            string name = cmd.Next();
            if( name == null ) {
                name = player.name;
            } else if( !player.Can( Permission.ViewOthersInfo ) ) {
                player.NoAccessMessage( Permission.ViewOthersInfo );
                return;
            }

            Player target = Server.FindPlayerByNick( name );
            if( target != null && target.nick != target.name ) {
                player.Message( "{0}Warning: Player named {1} is using a nickname \"{2}\"", Color.Red, target.name, target.nick );
                player.Message( "{0}The information below is for the REAL {1}", Color.Red, name );
            }

            PlayerInfo info;
            if( !PlayerDB.FindPlayerInfo( name, out info ) ) {
                player.ManyPlayersMessage( name );
            } else if( info != null ) {

                if( DateTime.Now.Subtract( info.lastLoginDate ).TotalDays < 1 ) {
                    player.Message( "About {0}: Last login {1:F1} hours ago from {2}",
                                    info.name,
                                    DateTime.Now.Subtract( info.lastLoginDate ).TotalHours,
                                    info.lastIP );
                } else {
                    player.Message( "About {0}: Last login {1:F1} days ago from {2}",
                                    info.name,
                                    DateTime.Now.Subtract( info.lastLoginDate ).TotalDays,
                                    info.lastIP );
                }

                player.Message( "  Logged in {0} time(s) since {1:dd MMM yyyy}.",
                                info.timesVisited,
                                info.firstLoginDate );

                player.Message( "  Built {0} and deleted {1} blocks, and wrote {2} messages.",
                                info.blocksBuilt,
                                info.blocksDeleted,
                                info.linesWritten );

                if( info.timesBannedOthers > 0 || info.timesKickedOthers > 0 ) {
                    player.Message( "  Kicked {0} and banned {1} players.", info.timesKickedOthers, info.timesBannedOthers );
                }

                if( info.timesKicked > 0 ) {
                    player.Message( "  Got kicked {0} times (so far).", info.timesKicked );
                }

                if( info.classChangedBy != "-" ) {
                    if( info.previousClass == null ) {
                        player.Message( "  Promoted to {0} by {1} on {2:dd MMM yyyy}.",
                                        info.playerClass.name,
                                        info.classChangedBy,
                                        info.classChangeDate );
                    } else if( info.previousClass.rank < info.playerClass.rank ) {
                        player.Message( "  Promoted from {0} to {1} by {2} on {3:dd MMM yyyy}.",
                                        info.previousClass.name,
                                        info.playerClass.name,
                                        info.classChangedBy,
                                        info.classChangeDate );
                        if( info.classChangeReason != null && info.classChangeReason.Length > 0 ) {
                            player.Message( "Promotion reason: " + info.classChangeReason );
                        }
                    } else {
                        player.Message( "  Demoted from {0} to {1} by {2} on {3:dd MMM yyyy}.",
                                        info.previousClass.name,
                                        info.playerClass.name,
                                        info.classChangedBy,
                                        info.classChangeDate );
                        if( info.classChangeReason != null && info.classChangeReason.Length > 0 ) {
                            player.Message( "Demotion reason: " + info.classChangeReason );
                        }
                    }
                } else {
                    player.Message( "  Class is {0} (default).",
                                    info.playerClass.name );
                }

                TimeSpan totalTime = info.totalTimeOnServer;
                if( Server.FindPlayerExact( player.name ) != null ) {
                    totalTime = totalTime.Add( DateTime.Now.Subtract( info.lastLoginDate ) );
                }
                player.Message( "  Spent a total of {0:F1} hours ({1:F1} minutes) here.",
                                totalTime.TotalHours,
                                totalTime.TotalMinutes );
            } else {
                player.NoPlayerMessage( name );
            }
        }



        static CommandDescriptor cdBanInfo = new CommandDescriptor {
            name = "baninfo",
            consoleSafe = true,
            usage = "/baninfo [PlayerName|IPAddress]",
            help = "Prints information about past and present bans/unbans associated with the PlayerName or IP. " +
                   "If no name is given, this prints your own ban info.",
            handler = BanInfo
        };

        internal static void BanInfo( Player player, Command cmd ) {
            string name = cmd.Next();
            IPAddress address;
            if( name == null ) {
                name = player.name;
            } else if( !player.Can( Permission.ViewOthersInfo ) ) {
                player.NoAccessMessage( Permission.ViewOthersInfo );
            }
            
            if( IPAddress.TryParse( name, out address ) ) {
                IPBanInfo info = IPBanList.Get( address );
                if( info != null ) {
                    player.Message( "{0} was banned by {1} on {2:dd MMM yyyy}.",
                                    info.address,
                                    info.bannedBy,
                                    info.banDate );
                    if( info.playerName != null ) {
                        player.Message( "  IP ban was banned by association with {0}",
                                        info.playerName );
                    }
                    if( info.attempts > 0 ) {
                        player.Message( "  There have been {0} attempts to log in, most recently", info.attempts );
                        player.Message( "  on {0:dd MMM yyyy} by {1}.",
                                        info.lastAttemptDate,
                                        info.lastAttemptName );
                    }
                    if( info.banReason.Length > 0 ) {
                        player.Message( "  Memo: {0}", info.banReason );
                    }
                } else {
                    player.Message( "{0} is currently NOT banned.", address );
                }
            } else {
                PlayerInfo info;
                if( !PlayerDB.FindPlayerInfo( name, out info ) ) {
                    player.ManyPlayersMessage( name );
                } else if( info != null ) {
                    if( info.banned ) {
                        player.Message( "Player {0} is currently {1}banned.", info.name, Color.Red );
                    } else {
                        player.Message( "Player {0} is currently NOT banned.", info.name );
                    }
                    if( info.bannedBy != "-" ) {
                        player.Message( "  Last banned by {0} on {1:dd MMM yyyy}.",
                                        info.bannedBy,
                                        info.banDate );
                        if( info.banReason.Length > 0 ) {
                            player.Message( "  Last ban memo: {0}", info.banReason );
                        }
                    }
                    if( info.unbannedBy != "-" ) {
                        player.Message( "  Unbanned by {0} on {1:dd MMM yyyy}.",
                                        info.unbannedBy,
                                        info.unbanDate );
                        if( info.unbanReason.Length > 0 ) {
                            player.Message( "  Last unban memo: {0}", info.unbanReason );
                        }
                    }
                    if( info.banDate != DateTime.MinValue ) {
                        TimeSpan banDuration;
                        if( info.banned ) {
                            banDuration = DateTime.Now.Subtract( info.banDate );
                        } else {
                            banDuration = info.unbanDate.Subtract( info.banDate );
                        }
                        player.Message( "  Last ban duration: {0} days and {1:F1} hours.",
                                        (int)banDuration.TotalDays,
                                        banDuration.TotalHours );
                    }
                } else {
                    player.NoPlayerMessage( name );
                }
            }
        }



        static CommandDescriptor cdClassInfo = new CommandDescriptor {
            name = "cinfo",
            aliases = new string[] { "class", "classinfo" },
            consoleSafe = true,
            usage = "/cinfo ClassName",
            help = "Shows a list of permissions granted to a class. To see a list of all classes, use &H/classes",
            handler = ClassInfo
        };

        // Shows general information about a particular class.
        internal static void ClassInfo( Player player, Command cmd ) {
            PlayerClass playerClass = ClassList.FindClass( cmd.Next() );
            if( playerClass != null ) {
                bool first = true;
                StringBuilder sb = new StringBuilder( "Players of class " );
                sb.AppendFormat( "Players of class {0}{1}&S can do the following: ",
                                 playerClass.color,
                                 playerClass.name );
                for( int i = 0; i < playerClass.permissions.Length; i++ ) {
                    if( playerClass.permissions[i] ) {
                        if( !first ) {
                            sb.Append( ", " );
                        }
                        sb.Append( (Permission)i );
                        first = false;
                    }
                }
                player.Message( sb.ToString() );
                if( playerClass.Can( Permission.Draw ) ) {
                    player.Message( "Draw command limit: " + playerClass.drawLimit + " blocks." );
                }
            }
        }



        static CommandDescriptor cdClasses = new CommandDescriptor {
            name = "classes",
            consoleSafe = true,
            help = "Shows a list of all defined classes/ranks.",
            handler = Classes
        };

        internal static void Classes( Player player, Command cmd ) {
            player.Message( "Below is a list of classes. For detail see &H{0}", cdClassInfo.usage );
            foreach( PlayerClass classListEntry in ClassList.classesByIndex ) {
                player.Message( "{0}    {1} (rank {2})",
                                classListEntry.color,
                                classListEntry.name,
                                classListEntry.rank );
            }
        }



        static CommandDescriptor cdRules = new CommandDescriptor {
            name = "rules",
            consoleSafe = true,
            help = "Shows a list of rules defined by server operator(s).",
            handler = Rules
        };

        const string RulesFile = "rules.txt";

        // Prints rules (if any are defined)
        internal static void Rules( Player player, Command cmd ) {
            if( !File.Exists( RulesFile ) ) {
                player.Message( "Rules: Use common sense!" );
            } else {
                try {
                    foreach( string ruleLine in File.ReadAllLines( RuleFile ) ) {
                        if( ruleLine.Trim().Length > 0 ) {
                            player.Message( Color.Announcement + ruleLine );
                        }
                    }
                } catch( Exception ex ) {
                    Logger.Log( "Error while trying to retrieve rules.txt: {0}", LogType.Error, ex.Message );
                    player.Message( "Rules: Use common sense!" );
                }
            }
        }
    }
}