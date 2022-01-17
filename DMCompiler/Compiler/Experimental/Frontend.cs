using OpenDreamShared.Compiler;
using DMCompiler.Compiler.DM;
using DMCompiler.DM.Visitors;
using Experimental = DMCompiler.Compiler.Experimental;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace DMCompiler {
    public class DMCompilerState {
        public DMParser parser;
        public List<CompilerError> parserErrors;
        public DMASTFile ast;
    }
    public static partial class DMCompiler {

        public static DMCompilerState GetAST(List<string> files) {
            var state = new DMCompilerState();
            IDMLexer lexer = null;
            if (Settings.ExperimentalPreproc) {
                DMParser.ExperimentalPreproc = true;
                var preproc = new Experimental.DMPreprocessor();
                string compilerDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string dmStandardDirectory = Path.Combine(compilerDirectory ?? string.Empty, "DMStandard");
                preproc.IncludeOuter(new Experimental.SourceText(dmStandardDirectory, "_Standard.dm"));
                foreach (var file in files) {
                    var fi = new FileInfo(file);
                    preproc.IncludeOuter(new Experimental.SourceText(fi.DirectoryName, fi.Name));
                }
                lexer = new Experimental.PreprocessorTokenConvert(preproc.GetEnumerator());
            } else {
                var preproc = Preprocess(files);
                lexer = new DMLexer(null, preproc.GetResult());
            }

            state.parser = new DMParser(lexer, !Settings.SuppressUnimplementedWarnings);

            VerbosePrint("Parsing");
            state.ast = state.parser.File();
            state.parserErrors = state.parser.Errors;
            return state;

        }

        public static int CompileAST(DMASTFile ast) {
            DMObjectBuilder dmObjectBuilder = new DMObjectBuilder();
            dmObjectBuilder.BuildObjectTree(ast);

            if (ErrorCount > 0) {
                return 255;
            }

            SaveJson(new(), "", "clopen.json");
            return 0;
        }
    }
}
