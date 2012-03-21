﻿// Part of fCraft | Copyright (c) 2009-2012 Matvei Stefarov <me@matvei.org> | BSD-3 | See LICENSE.txt
using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace fCraft.GUI {
    public sealed partial class AboutWindow : Form {
        public AboutWindow() {
            InitializeComponent();
            lSubheader.Text = String.Format( lSubheader.Text, Updater.CurrentRelease.VersionString );
            tCredits.Select( 0, 0 );
        }

        private void linkLabel1_LinkClicked( object sender, LinkLabelLinkClickedEventArgs e ) {
            try {
                Process.Start( "http://www.fcraft.net" );
            } catch { }
        }

        private void linkLabel2_LinkClicked( object sender, LinkLabelLinkClickedEventArgs e ) {
            try {
                Process.Start( "mailto:me@matvei.org" );
            } catch { }
        }

    }
}