﻿using System;

namespace fCraft {
    public enum Permissions {
        Chat,
        Build,
        Delete,

        PlaceGrass,
        PlaceWater, // includes placing water blocks and changing water sim parameters
        PlaceLava,  // same as above, but with lava
        PlaceAdmincrete,  // build admincrete
        DeleteAdmincrete, // delete admincrete

        Say,
        Kick,
        Ban,
        BanIP,
        BanAll,

        Promote,
        Demote,
        Hide,         // go invisible!
        ChangeName,   // change own name

        ViewOthersInfo,

        Teleport,
        Bring,
        Freeze,
        SetSpawn,

        SaveAndLoad,       // live backup/rollback
        Lock,

        ControlPhysics,

        Draw
    }
}
