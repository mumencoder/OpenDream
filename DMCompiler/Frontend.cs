using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DMCompiler.Compiler.DMPreprocessor;
using DMCompiler.Compiler.DM;
using OpenDreamShared.Compiler;

namespace DMCompiler {
    public class DMCompilerState {
        public DMParser parser;
        public List<CompilerError> parserErrors;
        public DMASTFile ast;
    }
    public static partial class DMCompiler {

        public static DMCompilerState GetAST(List<string> files) {
            var state = new DMCompilerState();
            DMPreprocessor preprocessor = Preprocess(files);
            DMLexer dmLexer = new DMLexer(null, preprocessor.GetResult());
            state.parser = new DMParser(dmLexer, !Settings.SuppressUnimplementedWarnings);

            VerbosePrint("Parsing");
            state.ast = state.parser.File();
            state.parserErrors = state.parser.Errors;
            return state;

        }
    }
}
