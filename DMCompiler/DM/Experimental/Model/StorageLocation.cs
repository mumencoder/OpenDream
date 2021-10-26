
using System.Collections.Generic;
using OpenDreamShared.Compiler;
using OpenDreamShared.Compiler.DM;
using OpenDreamShared.Dream;

namespace DMCompiler.DM.Experimental {
    public class StorageLocation {

    }

    public class GlobalStorageLocation : StorageLocation {
        public int Offset;

        public GlobalStorageLocation(int offset) { Offset = offset; }
    }

    public class FieldStorageLocation : StorageLocation {
        public FieldStorageLocation(DMObjectModel obj) { }
    }
}
