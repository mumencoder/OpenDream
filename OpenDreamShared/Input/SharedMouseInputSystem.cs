﻿using Robust.Shared.Analyzers;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;
using System;
using OpenDreamShared.Dream;
using Robust.Shared.Maths;

namespace OpenDreamShared.Input;

[Virtual]
public class SharedMouseInputSystem : EntitySystem {
    protected interface IAtomClickedEvent {
        public ScreenLocation ScreenLoc { get; }
        public bool Shift { get; }
        public bool Ctrl { get; }
        public bool Alt { get; }
    }

    [Serializable, NetSerializable]
    public sealed class EntityClickedEvent : EntityEventArgs, IAtomClickedEvent {
        public EntityUid EntityUid { get; }
        public ScreenLocation ScreenLoc { get; }
        public bool Shift { get; }
        public bool Ctrl { get; }
        public bool Alt { get; }

        public EntityClickedEvent(EntityUid entityUid, ScreenLocation screenLoc, bool shift, bool ctrl, bool alt) {
            EntityUid = entityUid;
            ScreenLoc = screenLoc;
            Shift = shift;
            Ctrl = ctrl;
            Alt = alt;
        }
    }

    [Serializable, NetSerializable]
    public sealed class TurfClickedEvent : EntityEventArgs, IAtomClickedEvent {
        public Vector2i Position;
        public ScreenLocation ScreenLoc { get; }
        public int Z;
        public bool Shift { get; }
        public bool Ctrl { get; }
        public bool Alt { get; }

        public TurfClickedEvent(Vector2i position, int z, ScreenLocation screenLoc, bool shift, bool ctrl, bool alt) {
            Position = position;
            Z = z;
            ScreenLoc = screenLoc;
            Shift = shift;
            Ctrl = ctrl;
            Alt = alt;
        }
    }
}
