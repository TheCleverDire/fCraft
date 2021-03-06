﻿// Part of fCraft | Copyright (c) 2009-2012 Matvei Stefarov <me@matvei.org> | BSD-3 | See LICENSE.txt
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using Devart.Data.MySql;

namespace fCraft.MySql {
    internal sealed partial class MySqlPlayerDBProvider : IPlayerDBProvider {
        MySqlConnection connection;
        MySqlPlayerDBProviderConfig config;

        public PlayerDBProviderType Type {
            get { return PlayerDBProviderType.MySql; }
        }


        /// <summary> Adds a new PlayerInfo entry for an actual, logged-in player. </summary>
        /// <returns> A newly-created PlayerInfo entry. </returns>
        public PlayerInfo AddPlayer( string name, Rank startingRank, RankChangeType rankChangeType, IPAddress address ) {
            if( name == null ) throw new ArgumentNullException( "name" );
            if( address == null ) throw new ArgumentNullException( "address" );
            if( startingRank == null ) throw new ArgumentNullException( "startingRank" );
            using( PlayerDB.GetWriteLock() ) {
                using( MySqlTransaction transaction = connection.BeginTransaction() ) {
                    preInsertCommand.Transaction = transaction;
                    preInsertCommand.ExecuteNonQuery();
                    int id = (int)preInsertCommand.InsertId;

                    PlayerInfo info = new PlayerInfo( id, name, startingRank, rankChangeType, address );

                    MySqlCommand updateCmd = GetUpdateCommand( info );
                    updateCmd.Transaction = transaction;
                    updateCmd.ExecuteNonQuery();

                    transaction.Commit();

                    preInsertCommand.Transaction = null;
                    updateCmd.Transaction = null;
                    return info;
                }
            }
        }


        /// <summary> Adds a new PlayerInfo entry for a player who has never been online, by name. </summary>
        /// <returns> A newly-created PlayerInfo entry. </returns>
        public PlayerInfo AddUnrecognizedPlayer( string name, Rank startingRank, RankChangeType rankChangeType ) {
            if( name == null ) throw new ArgumentNullException( "name" );
            if( startingRank == null ) throw new ArgumentNullException( "startingRank" );
            using( PlayerDB.GetWriteLock() ) {
                using( MySqlTransaction transaction = connection.BeginTransaction() ) {
                    preInsertCommand.Transaction = transaction;
                    preInsertCommand.ExecuteNonQuery();
                    int id = (int)preInsertCommand.InsertId;

                    PlayerInfo info = new PlayerInfo( id, name, startingRank, rankChangeType, false );

                    MySqlCommand updateCmd = GetUpdateCommand( info );
                    updateCmd.Transaction = transaction;
                    updateCmd.ExecuteNonQuery();

                    transaction.Commit();

                    preInsertCommand.Transaction = null;
                    updateCmd.Transaction = null;
                    return info;
                }
            }
        }


        /// <summary> Inserts all data from given playerInfo directly into the database. </summary>
        /// <param name="playerInfo"> Player record to import. </param>
        public void Import( PlayerInfo playerInfo ) {
            if( playerInfo == null ) throw new ArgumentNullException( "playerInfo" );
            using( PlayerDB.GetWriteLock() ) {
                GetImportCommand( playerInfo ).ExecuteNonQuery();
            }
        }


        /// <summary> Inserts all data from given PlayerInfo list directly into the database. </summary>
        /// <param name="playerInfos"> List of player record to import. </param>
        public void Import( IEnumerable<PlayerInfo> playerInfos ) {
            if( playerInfos == null ) throw new ArgumentNullException( "playerInfos" );
            using( PlayerDB.GetWriteLock() ) {
                using( MySqlTransaction transaction = connection.BeginTransaction() ) {
                    importCommand.Transaction = transaction;
                    foreach( PlayerInfo info in playerInfos ) {
                        GetImportCommand( info ).ExecuteNonQuery();
                    }
                    transaction.Commit();
                    importCommand.Transaction = null;
                }
            }
        }


        /// <summary> Removes a PlayerInfo entry from the database. </summary>
        /// <returns> True if the entry is successfully found and removed; otherwise false. </returns>
        public bool Remove( PlayerInfo playerInfo ) {
            if( playerInfo == null ) throw new ArgumentNullException( "playerInfo" );
            using( PlayerDB.GetWriteLock() ) {
                MySqlCommand cmd = GetDeleteCommand( playerInfo.ID );
                int rowsAffected = cmd.ExecuteNonQuery();
                return (rowsAffected > 0);
            }
        }


        /// <summary> Finds player by exact name. </summary>
        /// <param name="fullName"> Full, case-insensitive name of the player. </param>
        /// <returns> PlayerInfo if player was found, or null if not found. </returns>
        public PlayerInfo FindExact( string fullName ) {
            if( fullName == null ) throw new ArgumentNullException( "fullName" );
            using( PlayerDB.GetReadLock() ) {
                MySqlCommand cmd = GetFindExactCommand( fullName );
                object playerIdOrNull = cmd.ExecuteScalar();
                if( playerIdOrNull == null ) {
                    return null;
                } else {
                    int id = (int)playerIdOrNull;
                    return FindPlayerInfoByID( id );
                }
            }
        }


        /// <summary> Finds players by IP address. </summary>
        /// <param name="address"> Player's IP address. </param>
        /// <param name="limit"> Maximum number of results to return. </param>
        /// <returns> A sequence of zero or more PlayerInfos who have logged in from given IP. </returns>
        public IEnumerable<PlayerInfo> FindByIP( IPAddress address, int limit ) {
            if( address == null ) throw new ArgumentNullException( "address" );
            using( PlayerDB.GetReadLock() ) {
                MySqlCommand cmd = GetFindByIPCommand( address, limit );
                List<PlayerInfo> results = new List<PlayerInfo>();
                using( MySqlDataReader reader = cmd.ExecuteReader() ) {
                    while( reader.Read() ) {
                        int id = reader.GetInt32( 0 );
                        results.Add( FindPlayerInfoByID( id ) );
                    }
                }
                return results;
            }
        }


        /// <summary> Finds players by partial name (prefix). </summary>
        /// <param name="partialName"> Full or partial name of the player. </param>
        /// <param name="limit"> Maximum number of results to return. </param>
        /// <returns> A sequence of zero or more PlayerInfos whose names start with partialName. </returns>
        public IEnumerable<PlayerInfo> FindByPartialName( string partialName, int limit ) {
            if( partialName == null ) throw new ArgumentNullException( "partialName" );

            using( PlayerDB.GetReadLock() ) {
                MySqlCommand cmdExact = GetFindExactCommand( partialName );
                object playerIdOrNull = cmdExact.ExecuteScalar();

                if( playerIdOrNull != null ) {
                    // An exact match was found, return it
                    int id = (int)playerIdOrNull;
                    return new[] {
                        FindPlayerInfoByID( id )
                    };
                }

                MySqlCommand cmdPartial = GetFindPartialCommand( partialName + "%", limit );
                using( MySqlDataReader reader = cmdPartial.ExecuteReader() ) {
                    List<PlayerInfo> results = new List<PlayerInfo>();
                    while( reader.Read() ) {
                        // If multiple matches were found, they'll be added to the list
                        int id = reader.GetInt32( 0 );
                        results.Add( FindPlayerInfoByID( id ) );
                    }
                    // If no matches were found, the list will be empty
                    return results;
                }
            }
        }


        /// <summary> Searches for player names starting with namePart, returning just one or none of the matches. </summary>
        /// <param name="partialName"> Partial or full player name. </param>
        /// <param name="result"> PlayerInfo to output (will be set to null if no single match was found). </param>
        /// <returns> true if one or zero matches were found, false if multiple matches were found. </returns>
        public bool FindOneByPartialName( string partialName, out PlayerInfo result ) {
            if( partialName == null ) throw new ArgumentNullException( "partialName" );

            using( PlayerDB.GetReadLock() ) {
                MySqlCommand cmdExact = GetFindExactCommand( partialName );
                object playerIdOrNull = cmdExact.ExecuteScalar();

                if( playerIdOrNull != null ) {
                    // An exact match was found, return it
                    int id = (int)playerIdOrNull;
                    result = FindPlayerInfoByID( id );
                    return true;
                }

                MySqlCommand cmdPartial = GetFindPartialCommand( partialName + "%", 2 );
                using( MySqlDataReader reader = cmdPartial.ExecuteReader() ) {
                    if( !reader.Read() ) {
                        // zero matches found
                        result = null;
                        return true;
                    }
                    int id = reader.GetInt32( 0 );
                    if( !reader.Read() ) {
                        // one partial match found
                        result = FindPlayerInfoByID( id );
                        return true;
                    }
                    // multiple partial matches found
                    result = null;
                    return false;
                }
            }
        }


        /// <summary> Finds player by name pattern. </summary>
        /// <param name="pattern"> Pattern to search for.
        /// Asterisk (*) matches zero or more characters.
        /// Question mark (?) matches exactly one character. </param>
        /// <param name="limit"> Maximum number of results to return. </param>
        /// <returns> A sequence of zero or more PlayerInfos whose names match the pattern. </returns>
        public IEnumerable<PlayerInfo> FindByPattern( string pattern, int limit ) {
            if( pattern == null ) throw new ArgumentNullException( "pattern" );
            string processedPattern = pattern.Replace( "_", "\\_" ) // escape underscores
                                             .Replace( '*', '%' ) // zero-or-more-characters wildcard
                                             .Replace( '?', '_' ); // single-character wildcard

            using( PlayerDB.GetReadLock() ) {
                MySqlCommand cmdPartial = GetFindPartialCommand( processedPattern, limit );
                using( MySqlDataReader reader = cmdPartial.ExecuteReader() ) {
                    List<PlayerInfo> results = new List<PlayerInfo>();
                    while( reader.Read() ) {
                        int id = reader.GetInt32( 0 );
                        results.Add( FindPlayerInfoByID( id ) );
                    }
                    return results;
                }
            }
        }


        /// <summary> Changes ranks of all players in one transaction. </summary>
        public void MassRankChange( Player player, Rank from, Rank to, string reason ) {
            throw new NotImplementedException();
        }


        /// <summary> Swaps records of two players in one transaction. </summary>
        public void SwapInfo( PlayerInfo player1, PlayerInfo player2 ) {
            throw new NotImplementedException();
        }


        #region Loading

        /// <summary> Initializes the provider, and allocates PlayerInfo objects for all players. </summary>
        public IEnumerable<PlayerInfo> Load() {
            connection = new MySqlConnection();

            if(PlayerDB.MySqlProviderSettings!=null){
                try {
                    config = new MySqlPlayerDBProviderConfig( PlayerDB.MySqlProviderSettings );
                } catch( Exception ex ) {
                    throw new MisconfigurationException( "MySqlPlayerDBProvider: Error parsing configuration: " + ex.Message, ex );
                }
            } else {
                throw new MisconfigurationException( "MySqlPlayerDBProvider: Configuration missing from config file." );
            }

            connection.Host = config.Host;
            connection.Port = config.Port;
            connection.Database = config.Database;
            connection.UserId = config.UserID;
            connection.Password = config.Password;
            connection.Open();

            CheckSchema();

            PrepareCommands();

            using( MySqlCommand cmd = new MySqlCommand( LoadAllQuery, connection ) ) {
                using( MySqlDataReader reader = cmd.ExecuteReader() ) {
                    while( reader.Read() ) {
                        yield return LoadInfo( reader );
                    }
                }
            }
        }


        PlayerInfo LoadInfo( IDataRecord reader ) {
            int id = reader.GetInt32( (int)Field.ID );
            PlayerInfo info = new PlayerInfo( id );

            info.Name = reader.GetString( (int)Field.Name );
            info.DisplayedName = reader.GetString( (int)Field.DisplayedName );
            info.LastSeen = ReadDate( reader, Field.LastSeen );

            // Rank
            info.Rank = ReadRank( reader, Field.Rank );
            info.PreviousRank = ReadRank( reader, Field.PreviousRank );

            info.RankChangeType = (RankChangeType)reader.GetByte( (int)Field.RankChangeType );
            if( info.RankChangeType != RankChangeType.Default ) {
                info.RankChangeDate = ReadDate( reader, Field.RankChangeDate );
                info.RankChangedBy = reader.GetString( (int)Field.RankChangedBy );
                info.RankChangeReason = reader.GetString( (int)Field.RankChangeReason );
            }

            // Bans
            info.BanStatus = (BanStatus)reader.GetByte( (int)Field.BanStatus );
            info.BanDate = ReadDate( reader, Field.BanDate );
            info.BannedBy = reader.GetString( (int)Field.BannedBy );
            info.BanReason = reader.GetString( (int)Field.BanReason );
            if( info.BanStatus == BanStatus.Banned ) {
                info.BannedUntil = ReadDate( reader, Field.BannedUntil );
                info.LastFailedLoginDate = ReadDate( reader, Field.LastFailedLoginDate );
                info.LastFailedLoginIP = ReadIPAddress( reader, Field.LastFailedLoginIP );
            } else {
                info.UnbanDate = ReadDate( reader, Field.UnbanDate );
                info.UnbannedBy = reader.GetString( (int)Field.UnbannedBy );
                info.UnbanReason = reader.GetString( (int)Field.UnbanReason );
            }

            // Stats
            info.FirstLoginDate = ReadDate( reader, Field.FirstLoginDate );
            info.LastLoginDate = ReadDate( reader, Field.LastLoginDate );
            info.TotalTime = ReadTimeSpan( reader, Field.TotalTime );
            info.BlocksBuilt = reader.GetInt32( (int)Field.BlocksBuilt );
            info.BlocksDeleted = reader.GetInt32( (int)Field.BlocksDeleted );
            info.BlocksDrawn = reader.GetInt64( (int)Field.BlocksDrawn );
            info.TimesVisited = reader.GetInt32( (int)Field.TimesVisited );
            info.MessagesWritten = reader.GetInt32( (int)Field.MessagesWritten );
            info.TimesKickedOthers = reader.GetInt32( (int)Field.TimesKickedOthers );
            info.TimesBannedOthers = reader.GetInt32( (int)Field.TimesBannedOthers );

            // Kicks
            info.TimesKicked = reader.GetInt32( (int)Field.TimesKicked );
            if( info.TimesKicked > 0 ) {
                info.LastKickDate = ReadDate( reader, Field.LastKickDate );
                info.LastKickBy = reader.GetString( (int)Field.LastKickBy );
                info.LastKickReason = reader.GetString( (int)Field.LastKickReason );
            }

            // Freeze/Mute
            info.IsFrozen = reader.GetBoolean( (int)Field.IsFrozen );
            if( info.IsFrozen ) {
                info.FrozenOn = ReadDate( reader, Field.FrozenOn );
                info.FrozenBy = reader.GetString( (int)Field.FrozenBy );
            }
            info.MutedUntil = ReadDate( reader, Field.MutedUntil );
            if( info.MutedUntil != DateTime.MinValue ) {
                info.MutedBy = reader.GetString( (int)Field.MutedBy );
            }

            // Misc
            info.Password = reader.GetString( (int)Field.Password );
            info.LastModified = ReadDate( reader, Field.LastModified );
            // skip Field.IsOnline
            info.IsHidden = reader.GetBoolean( (int)Field.IsHidden );
            info.LastIP = ReadIPAddress( reader, Field.LastIP );
            info.LeaveReason = (LeaveReason)reader.GetByte( (int)Field.LeaveReason );
            info.BandwidthUseMode = (BandwidthUseMode)reader.GetByte( (int)Field.BandwidthUseMode );
            return info;
        }


        static DateTime ReadDate( IDataRecord reader, Field field ) {
            return reader.GetInt64( (int)field ).ToDateTime();
        }


        Rank ReadRank( IDataRecord reader, Field field ) {
            int rankId = reader.GetInt16( (int)field );
            Rank result;
            if( rankMapping.TryGetValue( rankId, out result ) ) {
                return result;
            } else {
                return null;
            }
        }


        static IPAddress ReadIPAddress( IDataRecord reader, Field field ) {
            return new IPAddress( (uint)reader.GetInt32( (int)field ) );
        }


        static TimeSpan ReadTimeSpan( IDataRecord reader, Field field ) {
            return new TimeSpan( reader.GetInt32( (int)field ) * TimeSpan.TicksPerSecond );
        }

        #endregion


        /// <summary> Saves the whole database. </summary>
        public void Save() {
            using( PlayerDB.GetWriteLock() ) {
                var playersToUpdate = PlayerDB.List.Where( p => p.Changed );
                using( MySqlTransaction transaction = connection.BeginTransaction() ) {
                    MySqlCommand cmd = null;
                    foreach( PlayerInfo info in playersToUpdate ) {
                        lock( info.SyncRoot ) {
                            info.Changed = false;
                            cmd = GetUpdateCommand( info );
                        }
                        cmd.Transaction = transaction;
                        cmd.ExecuteNonQuery();
                    }
                    if( cmd != null ) {
                        transaction.Commit();
                        cmd.Transaction = null;
                    }
                }
            }
        }


        static PlayerInfo FindPlayerInfoByID( int id ) {
            PlayerInfo result = PlayerDB.FindByID( id );
            if( result == null ) {
                throw new DataException( "Player id " + id + " was found, but no corresponding PlayerInfo exists. Database must be out of sync." );
            }
            return result;
        }
    }
}