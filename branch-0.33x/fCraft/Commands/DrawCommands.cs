﻿using System;
using System.Collections.Generic;


namespace fCraft {

    enum DrawMode {
        Cuboid,
        Ellipsoid
    }

    // VERY SLOPPY! TODO: fix everything forever
    class DrawCommands {
        World world;
        internal DrawCommands( World _world, Commands commands ) {
            world = _world;
            commands.AddCommand( "cuboid", Cuboid, true );
            commands.AddCommand( "cub", Cuboid, true );
            commands.AddCommand( "ellipsoid", Ellipsoid, true );
            commands.AddCommand( "ell", Ellipsoid, true );
            commands.AddCommand( "mark", Mark, true );
            commands.AddCommand( "undo", UndoDraw, true );
            commands.AddCommand( "cancel", CancelDraw, true );
        }


        void Cuboid( Player player, Command command ) {
            Draw( player, command, DrawMode.Cuboid );
        }

        void Ellipsoid( Player player, Command command ) {
            Draw( player, command, DrawMode.Ellipsoid );
        }

        void Draw( Player player, Command command, DrawMode mode ) {
            if( !player.Can( Permissions.Draw ) ) {
                world.NoAccessMessage( player );
                return;
            }
            if( player.drawingInProgress ) {
                player.Message( "Another draw command is already in progress. Please wait." );
                return;
            }
            string blockName = command.Next();
            Block block;
            if( blockName == null || blockName == "" ) {
                if( mode == DrawMode.Cuboid ) {
                    player.Message( "Usage: " + Color.Help + "/cuboid blockName" + Color.Sys + " or " + Color.Help + "/cub blockName" );
                } else {
                    player.Message( "Usage: " + Color.Help + "/ellipsoid blockName" + Color.Sys + " or " + Color.Help + "/ell blockName" );
                }
                return;
            }
            try {
                block = Map.GetBlockByName( blockName );
            } catch( Exception ) {
                player.Message( "Unknown block name: " + blockName );
                return;
            }
            player.tag = block;

            Permissions permission = Permissions.Build;
            switch( block ) {
                case Block.Admincrete: permission = Permissions.PlaceAdmincrete; break;
                case Block.Air: permission = Permissions.Delete; break;
                case Block.Water:
                case Block.StillWater: permission = Permissions.PlaceWater; break;
                case Block.Lava:
                case Block.StillLava: permission = Permissions.PlaceLava; break;
            }
            if( !player.Can( permission ) ) {
                player.Message( "You are not allowed to draw with this block." );
                return;
            }

            player.marksExpected = 2;
            player.markCount = 0;
            player.marks.Clear();
            player.Message( mode.ToString() + ": Place a block or type /mark to use your location." );

            if( mode == DrawMode.Cuboid ) {
                player.selectionCallback = DrawCuboid;
            } else {
                player.selectionCallback = DrawEllipsoid;
            }
        }


        void Mark( Player player, Command command ) {
            Position pos = new Position( (short)(player.pos.x / 32), (short)(player.pos.y / 32), (short)(player.pos.h / 32) );
            if( player.marksExpected > 0 ) {
                player.marks.Push( pos );
                player.markCount++;
                if( player.markCount >= player.marksExpected ) {
                    player.selectionCallback( player, player.marks.ToArray(), player.tag );
                    player.marksExpected = 0;
                } else {
                    player.Message( String.Format( "Block #{0} marked at ({1},{2},{3}). Place mark #{4}.",
                                            player.markCount, pos.x, pos.y, pos.h, player.markCount + 1 ) );
                }
            } else {
                player.Message( "There is currently not draw command to mark for." );
            }
        }


        void CancelDraw( Player player, Command command ) {
            if( player.marksExpected > 0 ) {
                player.marksExpected = 0;
            } else {
                player.Message( "There is currently nothing to cancel." );
            }
        }


        void UndoDraw( Player player, Command command ) {
            if( !player.Can( Permissions.Draw ) ) {
                world.NoAccessMessage( player );
                return;
            }
            if( player.drawUndoBuffer.Count > 0 ) {
                if( player.drawingInProgress ) {
                    player.Message( "Cannot undo a drawing-in-progress. Wait for it to finish." );
                } else {
                    world.SendToAll( Color.Sys + player.name + " initiated /drawundo. " + player.drawUndoBuffer.Count + " blocks to replace...", null );
                    while( player.drawUndoBuffer.Count > 0 ) {
                        world.map.QueueUpdate( player.drawUndoBuffer.Dequeue() );
                    }
                }
                GC.Collect();
            } else {
                player.Message( "There is currently nothing to undo." );
            }
        }


        internal static void DrawCuboid( Player player, Position[] marks, object tag ) {
            player.drawingInProgress = true;
            Block drawBlock = (Block)tag;

            // find start/end coordinates
            int sx = Math.Min( marks[0].x, marks[1].x );
            int ex = Math.Max( marks[0].x, marks[1].x );
            int sy = Math.Min( marks[0].y, marks[1].y );
            int ey = Math.Max( marks[0].y, marks[1].y );
            int sh = Math.Min( marks[0].h, marks[1].h );
            int eh = Math.Max( marks[0].h, marks[1].h );

            int blocks;
            byte block;
            int step = 8;

            blocks = (ex - sx + 1) * (ey - sy + 1) * (eh - sh + 1);
            if( blocks > 2000000 ) {
                player.Message( "NOTE: This draw command is too massive to undo." );
            }

            for( int x = sx; x <= ex; x += step ) {
                for( int y = sy; y <= ey; y += step ) {
                    for( int h = sh; h <= eh; h += step ) {

                        for( int h3 = 0; h3 < step && h + h3 <= eh; h3++ ) {
                            for( int y3 = 0; y3 < step && y + y3 <= ey; y3++ ) {
                                for( int x3 = 0; x3 < step && x + x3 <= ex; x3++ ) {
                                    block = player.world.map.GetBlock( x + x3, y + y3, h + h3 );
                                    if( block == (byte)drawBlock ) continue;
                                    if( block == (byte)Block.Admincrete && !player.Can( Permissions.DeleteAdmincrete ) ) continue;
                                    player.drawUndoBuffer.Enqueue( new BlockUpdate( Player.Console, x + x3, y + y3, h + h3, block ) );
                                    player.world.map.QueueUpdate( new BlockUpdate( Player.Console, x + x3, y + y3, h + h3, (byte)drawBlock ) );
                                }
                            }
                        }

                    }
                }
            }
            player.Message( "Drawing " + blocks + " blocks... The map is now being updated." );
            player.world.log.Log( "{0} initiated drawing a cuboid containing {1} blocks of type {2}.", LogType.UserActivity,
                                  player.name,
                                  blocks,
                                  drawBlock.ToString() );
            GC.Collect();
            player.drawingInProgress = false;
        }

        static void DrawEllipsoid( Player player, Position[] marks, object tag ) {
            player.drawingInProgress = true;
            Block drawBlock = (Block)tag;

            // find start/end coordinates
            int sx = Math.Min( marks[0].x, marks[1].x );
            int ex = Math.Max( marks[0].x, marks[1].x );
            int sy = Math.Min( marks[0].y, marks[1].y );
            int ey = Math.Max( marks[0].y, marks[1].y );
            int sh = Math.Min( marks[0].h, marks[1].h );
            int eh = Math.Max( marks[0].h, marks[1].h );

            int blocks;
            byte block;
            int step = 8;

            blocks = (ex - sx + 1) * (ey - sy + 1) * (eh - sh + 1);
            if( blocks > 2000000 ) {
                player.Message( "NOTE: This draw command is too massive to undo." );
            }

            // find axis lengths
            double rx = (ex - sx + 1) / 2 + .25;
            double ry = (ey - sy + 1) / 2 + .25;
            double rh = (eh - sh + 1) / 2 + .25;

            double rx2 = 1 / (rx * rx);
            double ry2 = 1 / (ry * ry);
            double rh2 = 1 / (rh * rh);

            // find center points
            double cx = (ex + sx) / 2;
            double cy = (ey + sy) / 2;
            double ch = (eh + sh) / 2;

            // prepare to draw
            player.drawUndoBuffer.Clear();

            blocks = (int)(Math.PI * 0.75 * rx * ry * rh);
            if( blocks > 2000000 ) {
                player.Message( "NOTE: This draw command is too massive to undo." );
            }

            for( int x = sx; x <= ex; x += step ) {
                for( int y = sy; y <= ey; y += step ) {
                    for( int h = sh; h <= eh; h += step ) {

                        for( int h3 = 0; h3 < step && h + h3 <= eh; h3++ ) {
                            for( int y3 = 0; y3 < step && y + y3 <= ey; y3++ ) {
                                for( int x3 = 0; x3 < step && x + x3 <= ex; x3++ ) {

                                    // get relative coordinates
                                    double dx = (x + x3 - cx);
                                    double dy = (y + y3 - cy);
                                    double dh = (h + h3 - ch);

                                    // test if it's inside ellipse
                                    if( (dx * dx) * rx2 + (dy * dy) * ry2 + (dh * dh) * rh2 <= 1 ) {
                                        block = player.world.map.GetBlock( x + x3, y + y3, h + h3 );
                                        if( block == (byte)drawBlock ) continue;
                                        if( block == (byte)Block.Admincrete && !player.Can( Permissions.DeleteAdmincrete ) ) continue;
                                        player.drawUndoBuffer.Enqueue( new BlockUpdate( Player.Console, x + x3, y + y3, h + h3, block ) );
                                        player.world.map.QueueUpdate( new BlockUpdate( Player.Console, x + x3, y + y3, h + h3, (byte)drawBlock ) );
                                    }
                                }
                            }
                        }
                    }
                }
            }
            player.drawingInProgress = false;
            player.Message( "Drawing " + blocks + " blocks... The map is now being updated." );
            player.world.log.Log( "{0} initiated drawing a cuboid containing {1} blocks of type {2}.", LogType.UserActivity,
                                  player.name,
                                  blocks,
                                  drawBlock.ToString() );
            GC.Collect();
        }
    }
}