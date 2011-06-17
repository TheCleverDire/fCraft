﻿// Copyright 2009, 2010, 2011 Matvei Stefarov <me@matvei.org>
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using fCraft.MapConversion;

namespace fCraft {
    public unsafe sealed class Map {

        public World World { get; set; }

        /// <summary> Map width, in blocks. Equivalent to Notch's X (horizontal)</summary>
        public readonly int WidthX;

        /// <summary> Map length, in blocks. Equivalent to Notch's Z (horizontal)</summary>
        public readonly int WidthY;

        /// <summary> Map height, in blocks. Equivalent to Notch's Y (vertical)</summary>
        public readonly int Height;

        /// <summary> Map boundaries. Can be useful for calculating volume or interesections. </summary>
        public readonly BoundingBox Bounds;

        public readonly int Volume;

        /// <summary> Default spawning point on the map. </summary>
        Position spawn;
        public Position Spawn {
            get {
                return spawn;
            }
            set {
                spawn = value;
                HasChangedSinceSave = true;
            }
        }

        /// <summary> Resets spawn to the default location (top center of the map). </summary>
        public void ResetSpawn() {
            Spawn = new Position {
                X = (short)(WidthX * 16),
                Y = (short)(WidthY * 16),
                H = (short)Math.Min( short.MaxValue, Height * 32 ),
                R = 0,
                L = 0
            };
        }


        /// <summary> Whether the map was modified since last time it was saved. </summary>
        public bool HasChangedSinceSave { get; internal set; }

        /// <summary> Whether the map was saved since last time it was backed up. </summary>
        public bool HasChangedSinceBackup { get; internal set; }

        // used by IsoCat and MapGenerator
        public short[,] Shadows;


        // FCMv3 additions
        public DateTime DateModified = DateTime.UtcNow;
        public DateTime DateCreated = DateTime.UtcNow;
        public Guid Guid = Guid.NewGuid();

        /// <summary> Array of map blocks.
        /// Use Index(x,y,h) to convert coordinates to array indices.
        /// Use QueueUpdate() for working on live maps to
        /// ensure that all players get updated. </summary>
        public byte[] Blocks;

        /// <summary> Map metadata, excluding zones. </summary>
        public MetadataCollection Metadata { get; private set; }


        /// <summary> Creates an empty new map of given dimensions.
        /// Dimensions cannot be changed after creation. </summary>
        /// <param name="world"> World that owns this map. May be null, and may be changed later. </param>
        /// <param name="widthX"> Width (horizontal, Notch's X). </param>
        /// <param name="widthY"> Length (horizontal, Notch's Z). </param>
        /// <param name="height"> Height (vertical, Notch's Y). </param>
        /// <param name="initBlockArray"> If true, the Blocks array will be created. </param>
        public Map( World world, int widthX, int widthY, int height, bool initBlockArray ) {
            Metadata = new MetadataCollection();
            UpdateZoneCache();

            World = world;

            WidthX = widthX;
            WidthY = widthY;
            Height = height;
            Bounds = new BoundingBox( Position.Zero, WidthX, WidthY, Height );
            Volume = Bounds.Volume;

            if( initBlockArray ) {
                Blocks = new byte[Volume];
            }
        }


        #region Saving

        public bool Save( string fileName ) {
            if( fileName == null ) throw new ArgumentNullException( "fileName" );
            string tempFileName = fileName + ".temp";

            // save to a temporary file
            try {
                HasChangedSinceSave = false;
                if( !MapUtility.TrySave( this, tempFileName, MapFormat.FCMv3 ) ) {
                    HasChangedSinceSave = true;
                }

            } catch( IOException ex ) {
                HasChangedSinceSave = true;
                Logger.Log( "Map.Save: Unable to open file \"{0}\" for writing: {1}", LogType.Error,
                               tempFileName, ex );
                try { File.Delete( tempFileName ); } catch { }
                return false;
            }

            // move newly-written file into its permanent destination
            try {
                Paths.MoveOrReplace( tempFileName, fileName );
                Logger.Log( "Saved map successfully to {0}", LogType.SystemActivity,
                            fileName );
                HasChangedSinceBackup = true;

            } catch( Exception ex ) {
                HasChangedSinceSave = true;
                Logger.Log( "Error trying to replace file \"{0}\": {1}", LogType.Error,
                            fileName, ex );
                try { File.Delete( tempFileName ); } catch { }
                return false;
            }
            return true;
        }

        #endregion


        #region Zones

        readonly Dictionary<string, Zone> zones = new Dictionary<string, Zone>();
        public Zone[] ZoneList { get; private set; }

        // locking is only needed when using "zones" dictionary, not "zoneList" cached array
        readonly object zoneLock = new object();


        /// <summary> Adds a new zone to the map. </summary>
        /// <param name="zone"> Zone to add. </param>
        /// <returns> True if the zone was added, false if the given zone was already on the list. </returns>
        public bool AddZone( Zone zone ) {
            if( zone == null ) throw new ArgumentNullException( "zone" );
            lock( zoneLock ) {
                if( zones.ContainsKey( zone.Name.ToLower() ) ) return false;
                zones.Add( zone.Name.ToLower(), zone );
                zone.Map = this;
                HasChangedSinceSave = true;
                UpdateZoneCache();
            }
            return true;
        }


        public void RenameZone( Zone zone, string newName ) {
            if( zone == null ) throw new ArgumentNullException( "zone" );
            if( newName == null ) throw new ArgumentNullException( "newName" );
            lock( zoneLock ) {
                zones.Remove( zone.Name.ToLower() );
                zone.Name = newName;
                zones.Add( newName.ToLower(), zone );
                HasChangedSinceSave = true;
                UpdateZoneCache();
            }
        }


        /// <summary> Removes a zone from the map. </summary>
        /// <param name="zone"> Zone to remove. </param>
        /// <returns> True if zone was removed, false if the given zone was not on the list. </returns>
        public bool RemoveZone( string zone ) {
            if( zone == null ) throw new ArgumentNullException( "zone" );
            lock( zoneLock ) {
                if( !zones.Remove( zone.ToLower() ) ) return false;
                HasChangedSinceSave = true;
                UpdateZoneCache();
            }
            return true;
        }


        /// <summary> Checks how zones affect the given player's ability to affect
        /// a block at given coordinates. </summary>
        /// <param name="x"> Block's X coordinate. </param>
        /// <param name="y"> Block's Y coordinate. </param>
        /// <param name="h"> Block's H coordinate. </param>
        /// <param name="player"> Player to check. </param>
        /// <returns> None if no zones affect the coordinate.
        /// Allow if ALL affecting zones allow the player.
        /// Deny if ANY affecting zone denies the player. </returns>
        public PermissionOverride CheckZones( int x, int y, int h, Player player ) {
            if( player == null ) throw new ArgumentNullException( "player" );

            PermissionOverride result = PermissionOverride.None;
            if( ZoneList.Length == 0 ) return result;

            Zone[] zoneListCache = ZoneList;
            for( int i = 0; i < zoneListCache.Length; i++ ) {
                if( zoneListCache[i].Bounds.Contains( x, y, h ) ) {
                    if( zoneListCache[i].Controller.Check( player.Info ) ) {
                        result = PermissionOverride.Allow;
                    } else {
                        return PermissionOverride.Deny;
                    }
                }
            }
            return result;
        }


        public bool CheckZonesDetailed( short x, short y, short h, Player player, out Zone[] allowedZones, out Zone[] deniedZones ) {
            if( player == null ) throw new ArgumentNullException( "player" );
            var allowedList = new List<Zone>();
            var deniedList = new List<Zone>();
            bool found = false;

            Zone[] zoneListCache = ZoneList;
            for( int i = 0; i < zoneListCache.Length; i++ ) {
                if( zoneListCache[i].Bounds.Contains( x, y, h ) ) {
                    found = true;
                    if( zoneListCache[i].Controller.Check( player.Info ) ) {
                        allowedList.Add( zoneListCache[i] );
                    } else {
                        deniedList.Add( zoneListCache[i] );
                    }
                }
            }
            allowedZones = allowedList.ToArray();
            deniedZones = deniedList.ToArray();
            return found;
        }


        /// <summary> Finds which zone denied player's ability to affect
        /// a block at given coordinates. Used in conjunction with CheckZones(). </summary>
        /// <param name="x"> Block's X coordinate. </param>
        /// <param name="y"> Block's Y coordinate. </param>
        /// <param name="h"> Block's H coordinate. </param>
        /// <param name="player"> Player to check. </param>
        /// <returns> First zone to deny the player.
        /// null if none of the zones deny the player. </returns>
        public Zone FindDeniedZone( int x, int y, int h, Player player ) {
            if( player == null ) throw new ArgumentNullException( "player" );
            Zone[] zoneListCache = ZoneList;
            for( int i = 0; i < zoneListCache.Length; i++ ) {
                if( zoneListCache[i].Bounds.Contains( x, y, h ) &&
                    !zoneListCache[i].Controller.Check( player.Info ) ) {
                    return zoneListCache[i];
                }
            }
            return null;
        }


        /// <summary> Finds a zone by name, without using autocompletion.
        /// Zone names are case-insensitive. </summary>
        /// <param name="name"> Full zone name. </param>
        /// <returns> Zone object if it was found.
        /// null if no Zone with the given name could be found. </returns>
        public Zone FindZoneExact( string name ) {
            if( name == null ) throw new ArgumentNullException( "name" );
            lock( zoneLock ) {
                Zone result;
                if( zones.TryGetValue( name.ToLower(), out result ) ) {
                    return result;
                }
            }
            return null;
        }


        /// <summary> Finds a zone by name, with autocompletion.
        /// Zone names are case-insensitive. </summary>
        /// <remarks> Note that this method is a lot slower than FindZoneExact. </remarks>
        /// <param name="name"> Full zone name. </param>
        /// <returns> Zone object if it was found.
        /// null if no Zone with the given name could be found. </returns>
        public Zone FindZone( string name ) {
            if( name == null ) throw new ArgumentNullException( "name" );
            // try to find exact match
            lock( zoneLock ) {
                Zone result;
                if( zones.TryGetValue( name.ToLower(), out result ) ) {
                    return result;
                }
            }
            // try to autocomplete
            Zone match = null;
            foreach( Zone zone in ZoneList ) {
                if( zone.Name.StartsWith( name, StringComparison.OrdinalIgnoreCase ) ) {
                    if( match == null ) {
                        // first (and hopefully only) match found
                        match = zone;
                    } else {
                        // more than one match found
                        return null;
                    }
                }
            }
            return match;
        }


        void UpdateZoneCache() {
            lock( zoneLock ) {
                Zone[] newZoneList = new Zone[zones.Count];
                int i = 0;
                foreach( Zone zone in zones.Values ) {
                    newZoneList[i++] = zone;
                }
                ZoneList = newZoneList;
            }
        }

        #endregion


        #region Block Getters / Setters

        /// <summary> Converts given coordinates to a block array index. </summary>
        /// <param name="x"> X coordinate (width). </param>
        /// <param name="y"> Y coordinate (length, Notch's Z). </param>
        /// <param name="h"> H coordinate (height, Notch's Y). </param>
        /// <returns> Index of the block in Map.Blocks array. </returns>
        public int Index( int x, int y, int h ) {
            return (h * WidthY + y) * WidthX + x;
        }


        /// <summary> Converts given coordinates to a block array index. </summary>
        /// <param name="coords"> Coordinate vector. Vector's (X,Y,Z) maps to map's (X,H,Y). </param>
        /// <returns> Index of the block in Map.Blocks array. </returns>
        public int Index( Vector3i coords ) {
            return (coords.Y * WidthY + coords.Z) * WidthX + coords.X;
        }


        /// <summary> Sets a block in a safe way.
        /// Note that using SetBlock does not relay changes to players.
        /// Use QueueUpdate() for changing blocks on live maps/worlds. </summary>
        /// <param name="x"> X coordinate (width). </param>
        /// <param name="y"> Y coordinate (length, Notch's Z). </param>
        /// <param name="h"> H coordinate (height, Notch's Y). </param>
        /// <param name="type"> Block type to set. </param>
        public void SetBlock( int x, int y, int h, Block type ) {
            if( x < WidthX && y < WidthY && h < Height && x >= 0 && y >= 0 && h >= 0 ) {
                Blocks[Index( x, y, h )] = (byte)type;
                HasChangedSinceSave = true;
            }
        }


        /// <summary> Sets a block at given coordinates. Checks bounds. </summary>
        /// <param name="x"> X coordinate (width). </param>
        /// <param name="y"> Y coordinate (length, Notch's Z). </param>
        /// <param name="h"> H coordinate (height, Notch's Y). </param>
        /// <param name="type"> Block type to set. </param>
        public void SetBlock( int x, int y, int h, byte type ) {
            if( h < Height && x < WidthX && y < WidthY && x >= 0 && y >= 0 && h >= 0 && type < 50 ) {
                Blocks[Index( x, y, h )] = type;
                HasChangedSinceSave = true;
            }
        }


        /// <summary> Sets a block at given coordinates. Checks bounds. </summary>
        /// <param name="coords"> Coordinate vector. Vector's (X,Y,Z) maps to map's (X,H,Y). </param>
        /// <param name="type"> Block type to set. </param>
        public void SetBlock( Vector3i coords, Block type ) {
            if( coords.X < WidthX && coords.Z < WidthY && coords.Y < Height && coords.X >= 0 && coords.Z >= 0 && coords.Y >= 0 && (byte)type < 50 ) {
                Blocks[Index( coords.X, coords.Z, coords.Y )] = (byte)type;
                HasChangedSinceSave = true;
            }
        }


        /// <summary> Sets a block at given coordinates. Checks bounds. </summary>
        /// <param name="coords"> Coordinate vector. Vector's (X,Y,Z) maps to map's (X,H,Y). </param>
        /// <param name="type"> Block type to set. </param>
        public void SetBlock( Vector3i coords, byte type ) {
            if( coords.X < WidthX && coords.Z < WidthY && coords.Y < Height && coords.X >= 0 && coords.Z >= 0 && coords.Y >= 0 && type < 50 ) {
                Blocks[Index( coords.X, coords.Z, coords.Y )] = type;
                HasChangedSinceSave = true;
            }
        }


        /// <summary> Gets a block at given coordinates. Checks bounds. </summary>
        /// <param name="x"> X coordinate (width). </param>
        /// <param name="y"> Y coordinate (length, Notch's Z). </param>
        /// <param name="h"> H coordinate (height, Notch's Y). </param>
        /// <returns> Block type, as a byte. 255 if coordinates were out of bounds. </returns>
        public byte GetBlockByte( int x, int y, int h ) {
            if( x < WidthX && y < WidthY && h < Height && x >= 0 && y >= 0 && h >= 0 )
                return Blocks[Index( x, y, h )];
            return (byte)Block.Undefined;
        }


        /// <summary> Gets a block at given coordinates. Checks bounds. </summary>
        /// <param name="x"> X coordinate (width). </param>
        /// <param name="y"> Y coordinate (length, Notch's Z). </param>
        /// <param name="h"> H coordinate (height, Notch's Y). </param>
        /// <returns> Block type, as a Block enumeration. Undefined if coordinates were out of bounds. </returns>
        public Block GetBlock( int x, int y, int h ) {
            if( x < WidthX && y < WidthY && h < Height && x >= 0 && y >= 0 && h >= 0 )
                return (Block)Blocks[Index( x, y, h )];
            return Block.Undefined;
        }


        /// <summary> Gets a block at given coordinates. Checks bounds. </summary>
        /// <param name="coords"> Coordinate vector. Vector's (X,Y,Z) maps to map's (X,H,Y). </param>
        /// <returns> Block type, as a Block enumeration. Undefined if coordinates were out of bounds. </returns>
        public byte GetBlockByte( Vector3i coords ) {
            if( coords.X < WidthX && coords.Z < WidthY && coords.Y < Height && coords.X >= 0 && coords.Z >= 0 && coords.Y >= 0 )
                return Blocks[Index( coords.X, coords.Z, coords.Y )];
            return (byte)Block.Undefined;
        }


        /// <summary> Checks whether the given coordinate (in block units) is within the bounds of the map. </summary>
        /// <param name="x"> X coordinate (width). </param>
        /// <param name="y"> Y coordinate (length, Notch's Z). </param>
        /// <param name="h"> H coordinate (height, Notch's Y). </param>
        public bool InBounds( int x, int y, int h ) {
            return x < WidthX && y < WidthY && h < Height && x >= 0 && y >= 0 && h >= 0;
        }


        /// <summary> Checks whether the given coordinate (in block units) is within the bounds of the map. </summary>
        /// <param name="vec"> Coordinate vector. Vector's (X,Y,Z) maps to map's (X,H,Y). </param>
        public bool InBounds( Vector3i vec ) {
            return vec.X < WidthX && vec.Z < WidthY && vec.Y < Height && vec.X >= 0 && vec.Z >= 0 && vec.Y >= 0;
        }

        #endregion


        #region Block Updates & Simulation

        // Queue of block updates. Updates are applied by ProcessUpdates()
        readonly ConcurrentQueue<BlockUpdate> updates = new ConcurrentQueue<BlockUpdate>();


        public int UpdateQueueLength {
            get {
                return updates.Length;
            }
        }


        /// <summary> Queues a new block update to be processed.
        /// Due to concurrent nature of the server, there is no guarantee
        /// that updates will be applied in any specific order. </summary>
        public void QueueUpdate( BlockUpdate update ) {
            updates.Enqueue( update );
        }


        /// <summary> Clears all pending updates. </summary>
        public void ClearUpdateQueue() {
            updates.Clear();
        }


        // Applies pending updates and sends them to players (if applicable).
        internal void ProcessUpdates() {
            if( World == null ) {
                throw new InvalidOperationException( "Map must be assigned to a world to process updates." );
            }

            if( World.IsLocked ) {
                if( World.PendingUnload ) {
                    World.UnloadMap( true );
                }
                return;
            }

            int packetsSent = 0;
            int maxPacketsPerUpdate = Server.CalculateMaxPacketsPerUpdate( World );
            BlockUpdate update = new BlockUpdate();
            while( packetsSent < maxPacketsPerUpdate ) {
                if( !updates.Dequeue( ref update ) ) {
                    if( World.IsFlushing ) {
                        World.EndFlushMapBuffer();
                    }
                    break;
                }
                HasChangedSinceSave = true;
                if( !InBounds( update.X, update.Y, update.H ) ) continue;
                int blockIndex = Index( update.X, update.Y, update.H );
                Blocks[blockIndex] = update.BlockType; // TODO: investigate IndexOutOfRangeException here

                if( !World.IsFlushing ) {
                    World.SendToAllDelayed( PacketWriter.MakeSetBlock( update.X, update.Y, update.H, update.BlockType ), update.Origin );
                }
                packetsSent++;
            }

            if( packetsSent == 0 && World.PendingUnload ) {
                World.UnloadMap( true );
            }
        }

        #endregion


        #region Backup

        readonly object backupLock = new object();


        public void SaveBackup( string sourceName, string targetName, bool onlyIfChanged ) {
            if( sourceName == null ) throw new ArgumentNullException( "sourceName" );
            if( targetName == null ) throw new ArgumentNullException( "targetName" );
            if( onlyIfChanged && !HasChangedSinceBackup && ConfigKey.BackupOnlyWhenChanged.GetBool() ) return;

            lock( backupLock ) {
                DirectoryInfo directory = new DirectoryInfo( Paths.BackupPath );

                if( !directory.Exists ) {
                    try {
                        directory.Create();
                    } catch( Exception ex ) {
                        Logger.Log( "Map.SaveBackup: Error occured while trying to create backup directory: {0}", LogType.Error,
                                    ex );
                        return;
                    }
                }

                try {
                    HasChangedSinceBackup = false;
                    File.Copy( sourceName, targetName, true );
                } catch( Exception ex ) {
                    HasChangedSinceBackup = true;
                    Logger.Log( "Map.SaveBackup: Error occured while trying to save backup to \"{0}\": {1}", LogType.Error,
                                targetName, ex );
                    return;
                }

                if( ConfigKey.MaxBackups.GetInt() > 0 || ConfigKey.MaxBackupSize.GetInt() > 0 ) {
                    DeleteOldBackups( directory );
                }
            }

            Logger.Log( "AutoBackup: " + targetName, LogType.SystemActivity );
        }


        static void DeleteOldBackups( DirectoryInfo directory ) {
            var backupList = directory.GetFiles( "*.fcm" ).OrderBy( fi => fi.CreationTimeUtc ).ToList();

            if( ConfigKey.MaxBackups.GetInt() > 0 ) {
                while( backupList.Count > ConfigKey.MaxBackups.GetInt() ) {
                    FileInfo info = backupList[backupList.Count - 1];
                    backupList.RemoveAt( backupList.Count - 1 );
                    try {
                        File.Delete( info.FullName );
                    } catch( Exception ex ) {
                        Logger.Log( "Map.SaveBackup: Error occured while trying delete old backup \"{0}\": {1}", LogType.Error,
                                    info.FullName, ex );
                        break;
                    }
                    Logger.Log( "Map.SaveBackup: Deleted old backup \"{0}\"", LogType.SystemActivity,
                                info.Name );
                }
            }

            if( ConfigKey.MaxBackupSize.GetInt() > 0 ) {
                while( true ) {
                    FileInfo[] fis = directory.GetFiles();
                    long size = fis.Sum( fi => fi.Length );

                    if( size / 1024 / 1024 > ConfigKey.MaxBackupSize.GetInt() ) {
                        FileInfo info = backupList[backupList.Count - 1];
                        backupList.RemoveAt( backupList.Count - 1 );
                        try {
                            File.Delete( info.FullName );
                        } catch( Exception ex ) {
                            Logger.Log( "Map.SaveBackup: Error occured while trying delete old backup \"{0}\": {1}", LogType.Error,
                                        info.Name, ex );
                            break;
                        }
                        Logger.Log( "Map.SaveBackup: Deleted old backup \"{0}\"", LogType.SystemActivity,
                                    info.Name );
                    } else {
                        break;
                    }
                }
            }
        }

        #endregion


        #region Utilities

        internal bool ValidateHeader() {
            if( !IsValidDimension( WidthX ) ) {
                Logger.Log( "Map.ValidateHeader: Invalid dimension specified for widthX: {0}.", LogType.Error, WidthX );
                return false;
            }

            if( !IsValidDimension( WidthY ) ) {
                Logger.Log( "Map.ValidateHeader: Invalid dimension specified for widthY: {0}.", LogType.Error, WidthY );
                return false;
            }

            if( !IsValidDimension( Height ) ) {
                Logger.Log( "Map.ValidateHeader: Invalid dimension specified for height: {0}.", LogType.Error, Height );
                return false;
            }

            if( Spawn.X > WidthX * 32 || Spawn.Y > WidthY * 32 || Spawn.H > Height * 32 || Spawn.X < 0 || Spawn.Y < 0 || Spawn.H < 0 ) {
                Logger.Log( "Map.ValidateHeader: Spawn coordinates are outside the valid range! Using center of the map instead.",
                            LogType.Warning );
                ResetSpawn();
            }

            return true;
        }


        // Only multiples of 16 are allowed, between 16 and 2032
        public static bool IsValidDimension( int dimension ) {
            return dimension > 0 && dimension % 16 == 0 && dimension < 2048;
        }


        public bool RemoveUnknownBlocktypes() {
            bool foundUnknownTypes = false;
            fixed( byte* ptr = Blocks ) {
                for( int j = 0; j < Blocks.Length; j++ ) {
                    if( ptr[j] > 49 ) {
                        ptr[j] = 0;
                        foundUnknownTypes = true;
                    }
                }
            }
            if( foundUnknownTypes ) HasChangedSinceSave = true;
            return !foundUnknownTypes;
        }


        public bool ConvertBlockTypes( byte[] mapping ) {
            if( mapping == null ) throw new ArgumentNullException( "mapping" );
            if( mapping.Length != 256 ) throw new ArgumentException( "mapping" );

            bool mapped = false;
            fixed( byte* ptr = Blocks ) {
                for( int j = 0; j < Blocks.Length; j++ ) {
                    if( ptr[j] > 49 ) {
                        ptr[j] = mapping[ptr[j]];
                        mapped = true;
                    }
                }
            }
            if( mapped ) HasChangedSinceSave = true;
            return mapped;
        }


        static readonly Dictionary<string, Block> BlockNames = new Dictionary<string, Block>();

        static Map() {
            foreach( Block block in Enum.GetValues( typeof( Block ) ) ) {
                if( block != Block.Undefined ) {
                    BlockNames.Add( block.ToString().ToLower(), block );
                    BlockNames.Add( ((int)block).ToString(), block );
                }
            }

            // alternative names for some blocks
            BlockNames["none"] = Block.Air;
            BlockNames["aire"] = Block.Air; // common typo
            BlockNames["nothing"] = Block.Air;
            BlockNames["empty"] = Block.Air;
            BlockNames["delete"] = Block.Air;
            BlockNames["erase"] = Block.Air;
            BlockNames["blank"] = Block.Air;

            BlockNames["cement"] = Block.Stone;
            BlockNames["concrete"] = Block.Stone;

            BlockNames["gras"] = Block.Grass; // common typo

            BlockNames["soil"] = Block.Dirt;
            BlockNames["stones"] = Block.Rocks;
            BlockNames["cobblestone"] = Block.Rocks;
            BlockNames["plank"] = Block.Wood;
            BlockNames["planks"] = Block.Wood;
            BlockNames["board"] = Block.Wood;
            BlockNames["boards"] = Block.Wood;
            BlockNames["tree"] = Block.Plant;
            BlockNames["sappling"] = Block.Plant;
            BlockNames["adminium"] = Block.Admincrete;
            BlockNames["adminite"] = Block.Admincrete;
            BlockNames["opcrete"] = Block.Admincrete;
            BlockNames["hardrock"] = Block.Admincrete;
            BlockNames["solid"] = Block.Admincrete;
            BlockNames["bedrock"] = Block.Admincrete;
            BlockNames["gold_ore"] = Block.GoldOre;
            BlockNames["iron_ore"] = Block.IronOre;
            BlockNames["copper"] = Block.IronOre;
            BlockNames["copperore"] = Block.IronOre;
            BlockNames["copper_ore"] = Block.IronOre;
            BlockNames["ore"] = Block.IronOre;
            BlockNames["coals"] = Block.Coal;
            BlockNames["coalore"] = Block.Coal;
            BlockNames["blackore"] = Block.Coal;

            BlockNames["trunk"] = Block.Log;
            BlockNames["stump"] = Block.Log;
            BlockNames["treestump"] = Block.Log;
            BlockNames["treetrunk"] = Block.Log;

            BlockNames["leaf"] = Block.Leaves;
            BlockNames["foliage"] = Block.Leaves;

            BlockNames["cheese"] = Block.Sponge;

            BlockNames["redcloth"] = Block.Red;
            BlockNames["redwool"] = Block.Red;
            BlockNames["orangecloth"] = Block.Orange;
            BlockNames["orangewool"] = Block.Orange;
            BlockNames["yellowcloth"] = Block.Yellow;
            BlockNames["yellowwool"] = Block.Yellow;
            BlockNames["limecloth"] = Block.Lime;
            BlockNames["limewool"] = Block.Lime;
            BlockNames["greenyellow"] = Block.Lime;
            BlockNames["yellowgreen"] = Block.Lime;
            BlockNames["lightgreen"] = Block.Lime;
            BlockNames["lightgreencloth"] = Block.Lime;
            BlockNames["lightgreenwool"] = Block.Lime;
            BlockNames["greencloth"] = Block.Green;
            BlockNames["greenwool"] = Block.Green;
            BlockNames["springgreen"] = Block.Teal;
            BlockNames["emerald"] = Block.Teal;
            BlockNames["tealwool"] = Block.Teal;
            BlockNames["tealcloth"] = Block.Teal;
            BlockNames["aquawool"] = Block.Aqua;
            BlockNames["aquacloth"] = Block.Aqua;
            BlockNames["cyanwool"] = Block.Cyan;
            BlockNames["cyancloth"] = Block.Cyan;
            BlockNames["bluewool"] = Block.Blue;
            BlockNames["bluecloth"] = Block.Blue;
            BlockNames["indigowool"] = Block.Indigo;
            BlockNames["indigocloth"] = Block.Indigo;
            BlockNames["violetwool"] = Block.Violet;
            BlockNames["violetcloth"] = Block.Violet;
            BlockNames["lightpurple"] = Block.Violet;
            BlockNames["purple"] = Block.Violet;
            BlockNames["purplewool"] = Block.Violet;
            BlockNames["purplecloth"] = Block.Violet;
            BlockNames["fuchsia"] = Block.Magenta;
            BlockNames["magentawool"] = Block.Magenta;
            BlockNames["magentacloth"] = Block.Magenta;
            BlockNames["darkpink"] = Block.Pink;
            BlockNames["pinkwool"] = Block.Pink;
            BlockNames["pinkcloth"] = Block.Pink;
            BlockNames["cloth"] = Block.White;
            BlockNames["cotton"] = Block.White;
            BlockNames["grey"] = Block.Gray;
            BlockNames["lightgray"] = Block.Gray;
            BlockNames["lightgrey"] = Block.Gray;
            BlockNames["darkgray"] = Block.Black;
            BlockNames["darkgrey"] = Block.Black;

            BlockNames["yellow_flower"] = Block.YellowFlower;
            BlockNames["flower"] = Block.YellowFlower;
            BlockNames["rose"] = Block.RedFlower;
            BlockNames["redrose"] = Block.RedFlower;
            BlockNames["red_flower"] = Block.RedFlower;

            BlockNames["mushroom"] = Block.BrownMushroom;
            BlockNames["shroom"] = Block.BrownMushroom;
            BlockNames["brown_shroom"] = Block.BrownMushroom;
            BlockNames["red_shroom"] = Block.RedMushroom;

            BlockNames["goldblock"] = Block.Gold;
            BlockNames["goldsolid"] = Block.Gold;
            BlockNames["golden"] = Block.Gold;
            BlockNames["copper"] = Block.Gold;
            BlockNames["brass"] = Block.Gold;

            BlockNames["ironblock"] = Block.Steel;
            BlockNames["iron"] = Block.Steel;
            BlockNames["metal"] = Block.Steel;
            BlockNames["silver"] = Block.Steel;

            BlockNames["slab"] = Block.Stair;
            BlockNames["slabs"] = Block.DoubleStair;
            BlockNames["steps"] = Block.DoubleStair;
            BlockNames["stairs"] = Block.DoubleStair;
            BlockNames["doublestep"] = Block.DoubleStair;
            BlockNames["double_step"] = Block.DoubleStair;
            BlockNames["double_stair"] = Block.DoubleStair;
            BlockNames["staircasefull"] = Block.DoubleStair;
            BlockNames["step"] = Block.Stair;
            BlockNames["halfstep"] = Block.Stair;
            BlockNames["halfblock"] = Block.Stair;
            BlockNames["staircasestep"] = Block.Stair;

            BlockNames["bricks"] = Block.Brick;
            BlockNames["explosive"] = Block.TNT;
            BlockNames["dynamite"] = Block.TNT;

            BlockNames["book"] = Block.Books;
            BlockNames["shelf"] = Block.Books;
            BlockNames["shelves"] = Block.Books;
            BlockNames["bookcase"] = Block.Books;
            BlockNames["bookshelf"] = Block.Books;
            BlockNames["bookshelves"] = Block.Books;

            BlockNames["moss"] = Block.MossyRocks;
            BlockNames["mossy"] = Block.MossyRocks;
            BlockNames["stonevine"] = Block.MossyRocks;
            BlockNames["mossyrock"] = Block.MossyRocks;
            BlockNames["mossystone"] = Block.MossyRocks;
            BlockNames["mossystones"] = Block.MossyRocks;
            BlockNames["greencobblestone"] = Block.MossyRocks;
            BlockNames["mossycobblestone"] = Block.MossyRocks;
            BlockNames["mossy_cobblestone"] = Block.MossyRocks;
            BlockNames["blockthathasgreypixelsonitmostlybutsomeareactuallygreen"] = Block.MossyRocks;

            BlockNames["onyx"] = Block.Obsidian;
        }


        public void CalculateShadows() {
            if( Shadows != null ) return;

            Shadows = new short[WidthX, WidthY];
            for( int x = 0; x < WidthX; x++ ) {
                for( int y = 0; y < WidthY; y++ ) {
                    for( short h = (short)(Height - 1); h >= 0; h-- ) {
                        switch( GetBlock( x, y, h ) ) {
                            case Block.Air:
                            case Block.BrownMushroom:
                            case Block.Glass:
                            case Block.Leaves:
                            case Block.RedFlower:
                            case Block.RedMushroom:
                            case Block.YellowFlower:
                                continue;
                            default:
                                Shadows[x, y] = h;
                                break;
                        }
                        break;
                    }
                }
            }
        }


        /// <summary> Tries to find a blocktype by name. </summary>
        /// <param name="blockName"> Name of the block. </param>
        /// <returns> Described Block, or Block.Undefined if name could not be recognized. </returns>
        internal static Block GetBlockByName( string blockName ) {
            if( blockName == null ) throw new ArgumentNullException( "blockName" );
            Block result;
            if( BlockNames.TryGetValue( blockName.ToLower(), out result ) ) {
                return result;
            } else {
                return Block.Undefined;
            }
        }


        /// <summary> Writes a copy of the current map to a given stream, compressed with GZipStream. </summary>
        /// <param name="stream"> Stream to write the compressed data to. </param>
        /// <param name="prependBlockCount"> If true, prepends block data with signed, 32bit, big-endian block count. </param>
        public void GetCompressedCopy( Stream stream, bool prependBlockCount ) {
            if( stream == null ) throw new ArgumentNullException( "stream" );
            using( GZipStream compressor = new GZipStream( stream, CompressionMode.Compress ) ) {
                if( prependBlockCount ) {
                    // convert block count to big-endian
                    int convertedBlockCount = IPAddress.HostToNetworkOrder( Blocks.Length );
                    // write block count to gzip stream
                    compressor.Write( BitConverter.GetBytes( convertedBlockCount ), 0, sizeof( int ) );
                }
                compressor.Write( Blocks, 0, Blocks.Length );
            }
        }


        public void MakeFloodBarrier() {
            for( int x = 0; x < WidthX; x++ ) {
                for( int y = 0; y < WidthY; y++ ) {
                    SetBlock( x, y, 0, Block.Admincrete );
                }
            }

            for( int x = 0; x < WidthX; x++ ) {
                for( int h = 0; h < Height / 2; h++ ) {
                    SetBlock( x, 0, h, Block.Admincrete );
                    SetBlock( x, WidthY - 1, h, Block.Admincrete );
                }
            }

            for( int y = 0; y < WidthY; y++ ) {
                for( int h = 0; h < Height / 2; h++ ) {
                    SetBlock( 0, y, h, Block.Admincrete );
                    SetBlock( WidthX - 1, y, h, Block.Admincrete );
                }
            }
        }


        public int SearchColumn( int x, int y, Block id ) {
            return SearchColumn( x, y, id, Height - 1 );
        }


        public int SearchColumn( int x, int y, Block id, int startH ) {
            for( int h = startH; h > 0; h-- ) {
                if( GetBlock( x, y, h ) == id ) {
                    return h;
                }
            }
            return -1; // -1 means 'not found'
        }

        #endregion
    }
}