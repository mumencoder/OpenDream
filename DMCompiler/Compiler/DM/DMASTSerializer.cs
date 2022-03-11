
using System;
using Newtonsoft.Json;

namespace DMCompiler.Compiler.DM {
    public class DMASTSerializer {

        public string Result = null;

        private JsonSerializerSettings _settings = new() { TypeNameHandling = TypeNameHandling.All };

        public DMASTSerializer(DMASTNode node) {
            Result = JsonConvert.SerializeObject(node, _settings);
        }
    }
}
