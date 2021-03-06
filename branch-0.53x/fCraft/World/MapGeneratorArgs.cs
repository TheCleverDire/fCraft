﻿// Copyright 2009, 2010, 2011 Matvei Stefarov <me@matvei.org>
using System;
using System.Xml.Linq;

namespace fCraft {

    public sealed class MapGeneratorArgs {
        const int FormatVersion = 2;

        public MapGenTheme Theme = MapGenTheme.Forest;
        public int   Seed,
                     WidthX = 256,
                     WidthY = 256,
                     Height = 96,
                     MaxHeight = 20,
                     MaxDepth = 12,
                     MaxHeightVariation = 4,
                     MaxDepthVariation;

        public bool  AddWater = true,
                     CustomWaterLevel,
                     MatchWaterCoverage;
        public int   WaterLevel = 48;
        public float WaterCoverage = .5f;

        public bool  UseBias,
                     DelayBias;
        public float Bias;
        public int   RaisedCorners,
                     LoweredCorners,
                     MidPoint;

        public int   DetailScale = 7,
                     FeatureScale = 1;
        public float Roughness = .5f;
        public bool  LayeredHeightmap,
                     MarbledHeightmap,
                     InvertHeightmap;
        public float AboveFuncExponent = 1,
                     BelowFuncExponent = 1;

        public bool  AddTrees = true;
        public int   TreeSpacingMin = 7,
                     TreeSpacingMax = 11,
                     TreeHeightMin = 5,
                     TreeHeightMax = 7;

        public bool  AddCaves,
                     AddOre,
                     AddCaveWater,
                     AddCaveLava;
        public float CaveDensity = 2,
                     CaveSize = 1;

        public bool  AddSnow;
        public int   SnowAltitude = 70,
                     SnowTransition = 7;

        public bool  AddCliffs = true,
                     CliffSmoothing = true;
        public float CliffThreshold = 1;

        public bool  AddBeaches;
        public int   BeachExtent = 6,
                     BeachHeight = 2;

        public void Validate() {
            if( RaisedCorners < 0 || RaisedCorners > 4 || LoweredCorners < 0 || RaisedCorners > 4 || RaisedCorners + LoweredCorners > 4 ) {
                throw new ArgumentOutOfRangeException( "raisedCorners/loweredCorners",
                                                       "raisedCorners and loweredCorners must be between 0 and 4." );
            }

            if( CaveDensity <= 0 || CaveSize <= 0 ) {
                throw new ArgumentOutOfRangeException( "caveDensity/caveSize",
                                                       "caveDensity and caveSize must be > 0" );
            }
            // todo: additional validation
        }

        public MapGeneratorArgs() {
            Seed = (new Random()).Next();
        }

        public MapGeneratorArgs( string fileName ) {
            XDocument doc = XDocument.Load( fileName );
            XElement root = doc.Root;

            XAttribute versionTag = root.Attribute( "version" );
            int version = 0;
            if( versionTag != null && !String.IsNullOrEmpty(versionTag.Value) ) {
                version = Int32.Parse( versionTag.Value );
            }

            Theme = (MapGenTheme)Enum.Parse( typeof( MapGenTheme ), root.Element( "theme" ).Value, true );
            Seed = Int32.Parse( root.Element( "seed" ).Value );
            WidthX = Int32.Parse( root.Element( "dimX" ).Value );
            WidthY = Int32.Parse( root.Element( "dimY" ).Value );
            Height = Int32.Parse( root.Element( "dimH" ).Value );
            MaxHeight = Int32.Parse( root.Element( "maxHeight" ).Value );
            MaxDepth = Int32.Parse( root.Element( "maxDepth" ).Value );

            AddWater = Boolean.Parse( root.Element( "addWater" ).Value );
            if( root.Element( "customWaterLevel" ) != null ) CustomWaterLevel = Boolean.Parse( root.Element( "customWaterLevel" ).Value );
            MatchWaterCoverage = Boolean.Parse( root.Element( "matchWaterCoverage" ).Value );
            WaterLevel = Int32.Parse( root.Element( "waterLevel" ).Value );
            WaterCoverage = float.Parse( root.Element( "waterCoverage" ).Value );

            UseBias = Boolean.Parse( root.Element( "useBias" ).Value );
            if( root.Element( "delayBias" ) != null ) DelayBias = Boolean.Parse( root.Element( "delayBias" ).Value );
            Bias = float.Parse( root.Element( "bias" ).Value );
            RaisedCorners = Int32.Parse( root.Element( "raisedCorners" ).Value );
            LoweredCorners = Int32.Parse( root.Element( "loweredCorners" ).Value );
            MidPoint = Int32.Parse( root.Element( "midPoint" ).Value );

            if( version == 0 ) {
                DetailScale = Int32.Parse( root.Element( "minDetailSize" ).Value );
                FeatureScale = Int32.Parse( root.Element( "maxDetailSize" ).Value );
            } else {
                DetailScale = Int32.Parse( root.Element( "detailScale" ).Value );
                FeatureScale = Int32.Parse( root.Element( "featureScale" ).Value );
            }
            Roughness = float.Parse( root.Element( "roughness" ).Value );
            LayeredHeightmap = Boolean.Parse( root.Element( "layeredHeightmap" ).Value );
            MarbledHeightmap = Boolean.Parse( root.Element( "marbledHeightmap" ).Value );
            InvertHeightmap = Boolean.Parse( root.Element( "invertHeightmap" ).Value );
            if( root.Element( "aboveFuncExponent" ) != null ) AboveFuncExponent = float.Parse( root.Element( "aboveFuncExponent" ).Value );
            if( root.Element( "belowFuncExponent" ) != null ) BelowFuncExponent = float.Parse( root.Element( "belowFuncExponent" ).Value );

            AddTrees = Boolean.Parse( root.Element( "addTrees" ).Value );
            TreeSpacingMin = Int32.Parse( root.Element( "treeSpacingMin" ).Value );
            TreeSpacingMax = Int32.Parse( root.Element( "treeSpacingMax" ).Value );
            TreeHeightMin = Int32.Parse( root.Element( "treeHeightMin" ).Value );
            TreeHeightMax = Int32.Parse( root.Element( "treeHeightMax" ).Value );

            if( root.Element( "addCaves" ) != null ) {
                AddCaves = Boolean.Parse( root.Element( "addCaves" ).Value );
                AddCaveLava = Boolean.Parse( root.Element( "addCaveLava" ).Value );
                AddCaveWater = Boolean.Parse( root.Element( "addCaveWater" ).Value );
                AddOre = Boolean.Parse( root.Element( "addOre" ).Value );
                CaveDensity = float.Parse( root.Element( "caveDensity" ).Value );
                CaveSize = float.Parse( root.Element( "caveSize" ).Value );
            }

            if( root.Element( "addSnow" ) != null ) AddSnow = Boolean.Parse( root.Element( "addSnow" ).Value );
            if( root.Element( "snowAltitude" ) != null ) SnowAltitude = Int32.Parse( root.Element( "snowAltitude" ).Value );
            if( root.Element( "snowTransition" ) != null ) SnowTransition = Int32.Parse( root.Element( "snowTransition" ).Value );

            if( root.Element( "addCliffs" ) != null ) AddCliffs = Boolean.Parse( root.Element( "addCliffs" ).Value );
            if( root.Element( "cliffSmoothing" ) != null ) CliffSmoothing = Boolean.Parse( root.Element( "cliffSmoothing" ).Value );
            if( root.Element( "cliffThreshold" ) != null ) CliffThreshold = float.Parse( root.Element( "cliffThreshold" ).Value );

            if( root.Element( "addBeaches" ) != null ) AddBeaches = Boolean.Parse( root.Element( "addBeaches" ).Value );
            if( root.Element( "beachExtent" ) != null ) BeachExtent = Int32.Parse( root.Element( "beachExtent" ).Value );
            if( root.Element( "beachHeight" ) != null ) BeachHeight = Int32.Parse( root.Element( "beachHeight" ).Value );

            if( root.Element( "maxHeightVariation" ) != null ) MaxHeightVariation = Int32.Parse( root.Element( "maxHeightVariation" ).Value );
            if( root.Element( "maxDepthVariation" ) != null ) MaxDepthVariation = Int32.Parse( root.Element( "maxDepthVariation" ).Value );

            Validate();
        }


        const string RootTagName = "fCraftMapGeneratorArgs";
        public void Save( string fileName ) {
            XDocument document = new XDocument();
            document.Add( Serialize() );
            document.Save( fileName );
        }

        public XElement Serialize() {
            XElement root = new XElement( RootTagName );

            root.Add( new XAttribute( "version", FormatVersion ) );

            root.Add( new XElement( "theme", Theme ) );
            root.Add( new XElement( "seed", Seed ) );
            root.Add( new XElement( "dimX", WidthX ) );
            root.Add( new XElement( "dimY", WidthY ) );
            root.Add( new XElement( "dimH", Height ) );
            root.Add( new XElement( "maxHeight", MaxHeight ) );
            root.Add( new XElement( "maxDepth", MaxDepth ) );

            root.Add( new XElement( "addWater", AddWater ) );
            root.Add( new XElement( "customWaterLevel", CustomWaterLevel ) );
            root.Add( new XElement( "matchWaterCoverage", MatchWaterCoverage ) );
            root.Add( new XElement( "waterLevel", WaterLevel ) );
            root.Add( new XElement( "waterCoverage", WaterCoverage ) );

            root.Add( new XElement( "useBias", UseBias ) );
            root.Add( new XElement( "delayBias", DelayBias ) );
            root.Add( new XElement( "raisedCorners", RaisedCorners ) );
            root.Add( new XElement( "loweredCorners", LoweredCorners ) );
            root.Add( new XElement( "midPoint", MidPoint ) );
            root.Add( new XElement( "bias", Bias ) );

            root.Add( new XElement( "detailScale", DetailScale ) );
            root.Add( new XElement( "featureScale", FeatureScale ) );
            root.Add( new XElement( "roughness", Roughness ) );
            root.Add( new XElement( "layeredHeightmap", LayeredHeightmap ) );
            root.Add( new XElement( "marbledHeightmap", MarbledHeightmap ) );
            root.Add( new XElement( "invertHeightmap", InvertHeightmap ) );
            root.Add( new XElement( "aboveFuncExponent", AboveFuncExponent ) );
            root.Add( new XElement( "belowFuncExponent", BelowFuncExponent ) );

            root.Add( new XElement( "addTrees", AddTrees ) );
            root.Add( new XElement( "treeSpacingMin", TreeSpacingMin ) );
            root.Add( new XElement( "treeSpacingMax", TreeSpacingMax ) );
            root.Add( new XElement( "treeHeightMin", TreeHeightMin ) );
            root.Add( new XElement( "treeHeightMax", TreeHeightMax ) );

            root.Add( new XElement( "addCaves", AddCaves ) );
            root.Add( new XElement( "addCaveLava", AddCaveLava ) );
            root.Add( new XElement( "addCaveWater", AddCaveWater ) );
            root.Add( new XElement( "addOre", AddOre ) );
            root.Add( new XElement( "caveDensity", CaveDensity ) );
            root.Add( new XElement( "caveSize", CaveSize ) );

            root.Add( new XElement( "addSnow", AddSnow ) );
            root.Add( new XElement( "snowAltitude", SnowAltitude ) );
            root.Add( new XElement( "snowTransition", SnowTransition ) );

            root.Add( new XElement( "addCliffs", AddCliffs ) );
            root.Add( new XElement( "cliffSmoothing", CliffSmoothing ) );
            root.Add( new XElement( "cliffThreshold", CliffThreshold ) );

            root.Add( new XElement( "addBeaches", AddBeaches ) );
            root.Add( new XElement( "beachExtent", BeachExtent ) );
            root.Add( new XElement( "beachHeight", BeachHeight ) );

            root.Add( new XElement( "maxHeightVariation", MaxHeightVariation ) );
            root.Add( new XElement( "maxDepthVariation", MaxDepthVariation ) );

            return root;
        }
    }
}