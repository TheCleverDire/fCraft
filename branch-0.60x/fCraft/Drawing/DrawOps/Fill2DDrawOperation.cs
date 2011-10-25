﻿// Copyright 2009, 2010, 2011 Matvei Stefarov <me@matvei.org>
using System;
using System.Collections.Generic;

namespace fCraft.Drawing {
    public sealed class Fill2DDrawOperation : DrawOpWithBrush {
        const int MaxFillRadius = 32;
        const BlockChangeContext PasteContext = BlockChangeContext.Drawn | BlockChangeContext.Filled;

        public override string Name {
            get { return "Fill2D"; }
        }

        public override int ExpectedMarks {
            get { return 1; }
        }

        public override string Description {
            get {
                if( SourceBlock == Block.Undefined ) {
                    if( ReplacementBlock == Block.Undefined ) {
                        return Name;
                    } else {
                        return String.Format( "{0}({1})",
                                              Name, ReplacementBlock );
                    }
                } else {
                    return String.Format( "{0}({1} -> {2} @{3})",
                                          Name, SourceBlock, ReplacementBlock, Axis );
                }
            }
        }

        public Block SourceBlock { get; private set; }
        public Block ReplacementBlock { get; private set; }
        public Axis Axis { get; private set; }
        public Vector3I Origin { get; private set; }

        public Fill2DDrawOperation( Player player )
            : base( player ) {
            SourceBlock = Block.Undefined;
            ReplacementBlock = Block.Undefined;
        }


        public override bool ReadParams( Command cmd ) {
            if( cmd.HasNext ) {
                ReplacementBlock = cmd.NextBlock( Player );
                if( ReplacementBlock == Block.Undefined ) return false;
            }
            Brush = this;
            return true;
        }


        public override bool Prepare( Vector3I[] marks ) {
            if( marks == null ) throw new ArgumentNullException( "marks" );
            if( marks.Length < 1 ) throw new ArgumentException( "At least two marks needed.", "marks" );

            if( ReplacementBlock == Block.Undefined ) {
                if( Player.LastUsedBlockType == Block.Undefined ) {
                    Player.Message( "Cannot deduce desired replacement block. Click a block or type out the block name." );
                    return false;
                } else {
                    ReplacementBlock = Player.GetBind( Player.LastUsedBlockType );
                }
            }

            Marks = marks;
            Origin = marks[0];
            SourceBlock = Map.GetBlock( Origin );

            Vector3I playerCoords = Player.Position.ToBlockCoords();
            Vector3I lookVector = (Origin - playerCoords);
            Axis = lookVector.LongestComponent;

            Vector3I maxDelta;

            switch( Axis ) {
                case Axis.X:
                    maxDelta = new Vector3I( 0, MaxFillRadius, MaxFillRadius );
                    coordEnumerator = BlockEnumeratorX().GetEnumerator();
                    break;
                case Axis.Y:
                    maxDelta = new Vector3I( MaxFillRadius, 0, MaxFillRadius );
                    coordEnumerator = BlockEnumeratorY().GetEnumerator();
                    break;
                default: // Z
                    maxDelta = new Vector3I( MaxFillRadius, MaxFillRadius, 0 );
                    coordEnumerator = BlockEnumeratorZ().GetEnumerator();
                    break;
            }
            if( SourceBlock == ReplacementBlock ) {
                Bounds = new BoundingBox( Origin, Origin );
            } else {
                Bounds = new BoundingBox( Origin - maxDelta, Origin + maxDelta );
            }

            // Clip bounds to the map, used to limit fill extent
            Bounds = Bounds.GetIntersection( Map.Bounds );

            // Set everything up for pasting
            Brush = this;
            Coords = Origin;

            StartTime = DateTime.UtcNow;
            Context = PasteContext;
            BlocksTotalEstimate = Bounds.Volume;

            return true;
        }


        IEnumerator<Vector3I> coordEnumerator;
        public override int DrawBatch( int maxBlocksToDraw ) {
            if( SourceBlock == ReplacementBlock ) {
                IsDone = true;
                return 0;
            }
            int blocksDone = 0;
            while( coordEnumerator.MoveNext() ) {
                Coords = coordEnumerator.Current;
                if( DrawOneBlock() ) {
                    blocksDone++;
                    if( blocksDone >= maxBlocksToDraw ) return blocksDone;
                }
                if( TimeToEndBatch ) return blocksDone;
            }
            IsDone = true;
            return blocksDone;
        }


        IEnumerable<Vector3I> BlockEnumeratorX() {
            Stack<Vector3I> stack = new Stack<Vector3I>();
            stack.Push( Origin );
            Vector3I coords;

            while( stack.Count > 0 ) {
                coords = stack.Pop();
                while( coords.Y >= Bounds.YMin && Map.GetBlock( coords ) == SourceBlock ) coords.Y--;
                coords.Y++;
                bool spanLeft = false;
                bool spanRight = false;
                while( coords.Y < Bounds.YMax && Map.GetBlock( coords ) == SourceBlock ) {
                    yield return coords;

                    if( coords.Z > Bounds.ZMin && Map.GetBlock( coords.X, coords.Y, coords.Z - 1 ) == SourceBlock ) {
                        if( spanLeft ) {
                            spanLeft = false;
                        } else {
                            stack.Push( new Vector3I( coords.X, coords.Y, coords.Z - 1 ) );
                            spanLeft = true;
                        }
                    }

                    if( coords.Z < Bounds.ZMax && Map.GetBlock( coords.X, coords.Y, coords.Z + 1 ) == SourceBlock ) {
                        if( spanRight ) {
                            spanRight = false;
                        } else {
                            stack.Push( new Vector3I( coords.X, coords.Y, coords.Z + 1 ) );
                            spanRight = true;
                        }
                    }
                    coords.Y++;
                }
            }
        }


        IEnumerable<Vector3I> BlockEnumeratorY() {
            Stack<Vector3I> stack = new Stack<Vector3I>();
            stack.Push( Origin );
            Vector3I coords;

            while( stack.Count > 0 ) {
                coords = stack.Pop();
                while( coords.Z >= Bounds.YMin && Map.GetBlock( coords ) == SourceBlock ) coords.Z--;
                coords.Z++;
                bool spanLeft = false;
                bool spanRight = false;
                while( coords.Z < Bounds.YMax && Map.GetBlock( coords ) == SourceBlock ) {
                    yield return coords;

                    if( coords.X > Bounds.XMin && Map.GetBlock( coords.X - 1, coords.Y, coords.Z ) == SourceBlock ) {
                        if( spanLeft ) {
                            spanLeft = false;
                        } else {
                            stack.Push( new Vector3I( coords.X - 1, coords.Y, coords.Z ) );
                            spanLeft = true;
                        }
                    }

                    if( coords.X < Bounds.XMax && Map.GetBlock( coords.X + 1, coords.Y, coords.Z ) == SourceBlock ) {
                        if( spanRight ) {
                            spanRight = false;
                        } else {
                            stack.Push( new Vector3I( coords.X + 1, coords.Y, coords.Z ) );
                            spanRight = true;
                        }
                    }
                    coords.Z++;
                }
            }
        }

        IEnumerable<Vector3I> BlockEnumeratorZ() {
            Stack<Vector3I> stack = new Stack<Vector3I>();
            stack.Push( Origin );
            Vector3I coords;

            while( stack.Count > 0 ) {
                coords = stack.Pop();
                while( coords.Y >= Bounds.YMin && Map.GetBlock( coords ) == SourceBlock ) coords.Y--;
                coords.Y++;
                bool spanLeft = false;
                bool spanRight = false;
                while( coords.Y < Bounds.YMax && Map.GetBlock( coords ) == SourceBlock ) {
                    yield return coords;

                    if( coords.X > Bounds.XMin && Map.GetBlock( coords.X - 1, coords.Y, coords.Z ) == SourceBlock ) {
                        if( spanLeft ) {
                            spanLeft = false;
                        } else {
                            stack.Push( new Vector3I( coords.X - 1, coords.Y, coords.Z ) );
                            spanLeft = true;
                        }
                    }

                    if( coords.X < Bounds.XMax && Map.GetBlock( coords.X + 1, coords.Y, coords.Z ) == SourceBlock ) {
                        if( spanRight ) {
                            spanRight = false;
                        } else {
                            stack.Push( new Vector3I( coords.X + 1, coords.Y, coords.Z ) );
                            spanRight = true;
                        }
                    }
                    coords.Y++;
                }
            }
        }


        protected override Block NextBlock() {
            return ReplacementBlock;
        }
    }
}