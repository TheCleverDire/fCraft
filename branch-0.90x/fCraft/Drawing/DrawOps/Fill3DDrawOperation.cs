﻿// Part of fCraft | Copyright 2009-2013 Matvei Stefarov <me@matvei.org> | BSD-3 | See LICENSE.txt

using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace fCraft.Drawing {
    /// <summary> Draw operation that performs a 2D flood fill. 
    /// Uses player's position to determine plane of filling. </summary>
    public sealed class Fill3DDrawOperation : DrawOperation {
        IEnumerator<Vector3I> coordEnumerator;

        // fields to accommodate non-standard brushes (which require caching)
        bool nonStandardBrush;
        BitMap3D allCoords;

        public override string Name {
            get { return "Fill3D"; }
        }

        public override int ExpectedMarks {
            get { return 1; }
        }

        public override string Description {
            get {
                if (SourceBlock == Block.None) {
                    return String.Format("{0}({1})",
                                         Name,
                                         Brush.Description);
                } else {
                    return String.Format("{0}({1} -> {2})",
                                         Name,
                                         SourceBlock,
                                         Brush.Description);
                }
            }
        }

        public Block SourceBlock { get; private set; }
        public Vector3I Origin { get; private set; }


        public Fill3DDrawOperation(Player player)
            : base(player) {
            SourceBlock = Block.None;
        }


        public override bool Prepare(Vector3I[] marks) {
            if (marks == null) throw new ArgumentNullException("marks");
            if (marks.Length < 1) throw new ArgumentException("At least one mark needed.", "marks");

            Marks = marks;
            Origin = marks[0];
            SourceBlock = Map.GetBlock(Origin);

            if (Player.Info.Rank.DrawLimit == 0) {
                // Unlimited!
                Bounds = Map.Bounds;
            } else {
                // Our fill limit is cube root of DrawLimit
                double pow = Math.Pow(Player.Info.Rank.DrawLimit, 1/3d);
                int maxLimit = (int)Math.Ceiling(pow/2);

                // Compute the largest possible extent
                if (maxLimit < 1 || maxLimit > 2048) maxLimit = 2048;
                Vector3I maxDelta = new Vector3I(maxLimit, maxLimit, maxLimit);
                Bounds = new BoundingBox(Origin - maxDelta, Origin + maxDelta);
                // Clip bounds to the map, used to limit fill extent
                Bounds = Bounds.GetIntersection(Map.Bounds);
            }

            // Set everything up for filling
            Coords = Origin;

            StartTime = DateTime.UtcNow;
            Context = BlockChangeContext.Drawn | BlockChangeContext.Filled;
            BlocksTotalEstimate = Bounds.Volume;

            coordEnumerator = BlockEnumerator().GetEnumerator();

            if (Brush == null) throw new NullReferenceException(Name + ": Brush not set");
            return Brush.Begin(Player, this);
        }


        public override bool Begin() {
            if (!RaiseBeginningEvent(this)) return false;
            UndoState = Player.DrawBegin(this);
            StartTime = DateTime.UtcNow;

            if (!(Brush is NormalBrush)) {
                // for nonstandard brushes, cache all coordinates up front
                nonStandardBrush = true;

                // Generate a list if all coordinates
                allCoords = new BitMap3D(Bounds);
                while (coordEnumerator.MoveNext()) {
                    allCoords.Set(coordEnumerator.Current);
                }
                coordEnumerator.Dispose();

                // Replace our F3D enumerator with a HashSet enumerator
                coordEnumerator = allCoords.GetEnumerator();
            }

            HasBegun = true;
            Map.QueueDrawOp(this);
            RaiseBeganEvent(this);
            return true;
        }


        public override int DrawBatch(int maxBlocksToDraw) {
            return DrawBatchFromEnumerable(maxBlocksToDraw, coordEnumerator);
        }


        bool CanPlace(Vector3I coords) {
            if (nonStandardBrush && allCoords.Get(coords)) {
                return false;
            }
            return (Map.GetBlock(coords) == SourceBlock) &&
                   (Player.CanPlace(Map, coords, Brush.NextBlock(this), Context) == CanPlaceResult.Allowed);
        }


        [NotNull]
        IEnumerable<Vector3I> BlockEnumerator() {
            Stack<Vector3I> stack = new Stack<Vector3I>();
            stack.Push(Origin);

            while (stack.Count > 0) {
                Vector3I coords = stack.Pop();
                if (CanPlace(coords)) {
                    yield return coords;
                    if (coords.X - 1 >= Bounds.XMin) {
                        stack.Push(new Vector3I(coords.X - 1, coords.Y, coords.Z));
                    }
                    if (coords.X + 1 <= Bounds.XMax) {
                        stack.Push(new Vector3I(coords.X + 1, coords.Y, coords.Z));
                    }
                    if (coords.Y - 1 >= Bounds.YMin) {
                        stack.Push(new Vector3I(coords.X, coords.Y - 1, coords.Z));
                    }
                    if (coords.Y + 1 <= Bounds.YMax) {
                        stack.Push(new Vector3I(coords.X, coords.Y + 1, coords.Z));
                    }
                    if (coords.Z - 1 >= Bounds.ZMin) {
                        stack.Push(new Vector3I(coords.X, coords.Y, coords.Z - 1));
                    }
                    if (coords.Z + 1 <= Bounds.ZMax) {
                        stack.Push(new Vector3I(coords.X, coords.Y, coords.Z + 1));
                    }
                }
            }
        }
    }
}
