
using System.Linq;
using DMCompiler.Compiler.DM;
using DMCompiler.Compiler.DMPreprocessor;
using OpenDreamShared.Compiler;
using DMCompiler.DM.Visitors;
using System.Collections.Generic;

namespace DMCompiler {

    public static partial class DMCompiler {

        public static List<CompilerEmission> errors = new();
        public static List<CompilerEmission> warnings = new();

        public static DMCompilerState GetAST(List<string> files) {
            errors = new();
            warnings = new();

            Config = new();

            var state = new DMCompilerState();
            DMPreprocessor preprocessor = Preprocess(files, new());
            DMLexer dmLexer = new DMLexer(null, preprocessor.ToList());
            state.parser = new DMParser(dmLexer, !Settings.SuppressUnimplementedWarnings);

            VerbosePrint("Parsing");
            state.ast = state.parser.File();
            state.parserErrors = state.parser.Emissions;

            DMObjectBuilder.BuildObjectTree(state.ast);
            return state;
        }
        public class DMCompilerState {
            public DMParser parser;
            public List<CompilerEmission> parserErrors;
            public DMASTFile ast;

        }
    }
}
