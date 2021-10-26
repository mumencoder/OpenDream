
using System.Collections.Generic;
using OpenDreamShared.Compiler;
using OpenDreamShared.Compiler.DM;
using OpenDreamShared.Dream;

namespace DMCompiler.DM.Experimental {
    public class RuntimeContext {
        private int _currentRegister = 0;

        public GlobalStorageLocation NewRegister() {
            var newloc = new GlobalStorageLocation(_currentRegister);
            _currentRegister += 1;
            return newloc;
        }
    }
}
