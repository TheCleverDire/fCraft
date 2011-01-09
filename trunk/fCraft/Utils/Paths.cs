﻿// Copyright 2009, 2010, 2011 Matvei Stefarov <me@matvei.org>
using System;
using System.IO;
using System.Reflection;
using System.Security;


namespace fCraft {
    public static class Paths {

        static Paths() {
            WorkingPathDefault = Path.GetFullPath( Path.GetDirectoryName( Assembly.GetExecutingAssembly().Location ) );
            WorkingPath = WorkingPathDefault;
            MapPath = MapPathDefault;
            LogPath = LogPathDefault;
        }

        /// <summary>
        /// Makes sure that the path format is valid, that it exists, that it is accessible and writeable.
        /// </summary>
        /// <param name="path">full or partial path</param>
        /// <param name="checkForWriteAccess"></param>
        /// <returns>full path of the directory (on success) or null (on failure)</returns>
        public static bool TestDirectory( string path, bool checkForWriteAccess ) {
            try {
                if( !Directory.Exists( path ) ) {
                    Directory.CreateDirectory( path );
                }
                DirectoryInfo info = new DirectoryInfo( path );
                string randomFileName = Path.Combine( info.FullName, "fCraft_write_test_" + DateTime.UtcNow.Ticks );
                if( checkForWriteAccess ) {
                    using( File.Create( randomFileName ) ) { }
                    File.Delete( randomFileName );
                }
                return true;

            } catch( Exception ex ) {
                if( ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException ) {
                    Logger.Log( "Specified path is invalid or incorrectly formatted ({0}: {1}).", LogType.Error,
                                ex.GetType().ToString(), ex.Message );
                } else if( ex is SecurityException || ex is UnauthorizedAccessException ) {
                    Logger.Log( "Cannot create directory, check permissions ({0}: {1}).", LogType.Error,
                                ex.GetType().ToString(), ex.Message );
                } else if( ex is DirectoryNotFoundException ) {
                    Logger.Log( "Cannot create directory: drive/volume does not exist or is not mounted ({0}).", LogType.Error,
                                ex.Message );
                } else if( ex is IOException ) {
                    Logger.Log( "Cannot write to specified directory ({0}: {1}).", LogType.Error,
                                ex.GetType().ToString(), ex.Message );
                } else {
                    throw ex;
                }
            }
            return false;
        }



        public const string MapPathDefault = "maps",
                            LogPathDefault = "logs",
                            ConfigFileNameDefault = "config.xml";

        public static readonly string WorkingPathDefault;

        /// <summary>
        /// Path to save maps to (default: .\maps)
        /// Can be overridden at startup via command-line argument "--mappath=",
        /// or via "MapPath" ConfigKey
        /// </summary>
        public static string MapPath { get; set; }

        /// <summary>
        /// Working path (default: whatever directory fCraft.dll is located in)
        /// Can be overridden at startup via command line argument "--path="
        /// </summary>
        public static string WorkingPath { get; set; }

        /// <summary>
        /// Path to save logs to (default: .\logs)
        /// Can be overridden at startup via command-line argument "--logpath="
        /// </summary>
        public static string LogPath { get; set; }

        /// <summary>
        /// Path to load/save config to/from (default: .\config.xml)
        /// Can be overridden at startup via command-line argument "--config="
        /// </summary>
        public static string ConfigFileName { get; set; }


        internal static bool IgnoreMapPathConfigKey = false;

        public static bool IsDefaultMapPath( string path ) {
            return String.IsNullOrEmpty( path ) || Compare( MapPathDefault, path );
        }


        public static bool Compare( string p1, string p2 ) {
            return String.Equals( Path.GetFullPath( p1 ).TrimEnd( Path.PathSeparator ),
                                  Path.GetFullPath( p2 ).TrimEnd( Path.PathSeparator ),
                                  StringComparison.Ordinal );
        }
    }
}