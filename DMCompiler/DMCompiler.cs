using DMCompiler.Compiler.DM;
using DMCompiler.Compiler.DMM;
using DMCompiler.Compiler.DMPreprocessor;
using DMCompiler.DM;
using DMCompiler.DM.Visitors;
using Experimental = DMCompiler.Compiler.Experimental;
using OpenDreamShared.Compiler;
using OpenDreamShared.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DMCompiler {
    //TODO: Make this not a static class
    public static class DMCompiler {
        public static int ErrorCount = 0;
        public static int WarningCount = 0;
        public static DMCompilerSettings Settings;

        private static DateTime _compileStartTime;

        public static List<string> IncludedMaps;
        public static string IncludedInterface;

        public static bool Compile(DMCompilerSettings settings) {
            Settings = settings;
            if (Settings.Files == null) return false;

            //TODO: Only use InvariantCulture where necessary instead of it being the default
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            _compileStartTime = DateTime.Now;

            if (settings.SuppressUnimplementedWarnings) {
                Warning(new CompilerWarning(Location.Unknown, "Unimplemented proc & var warnings are currently suppressed"));
            }

            bool successfulCompile = false;
            if (settings.ExperimentalPreproc) {
                DMASTFile ast = GetAST(settings.Files);
                successfulCompile = CompileAST(ast) == 0;
            } else {
                DMPreprocessor preprocessor = Preprocess(settings.Files);
                if (settings.DumpPreprocessor) {
                    StringBuilder result = new();
                    foreach (Token t in preprocessor.GetResult()) {
                        result.Append(t.Text);
                    }

                    string output = Path.Join(Path.GetDirectoryName(settings.Files?[0]) ?? AppDomain.CurrentDomain.BaseDirectory, "preprocessor_dump.dm");
                    File.WriteAllText(output, result.ToString());
                    Console.WriteLine($"Preprocessor output dumped to {output}");
                }

                successfulCompile = Compile(preprocessor.GetResult());
                IncludedMaps = preprocessor.IncludedMaps;
                IncludedInterface = preprocessor.IncludedInterface;
            }

            if (successfulCompile) {
                Console.WriteLine($"Compilation succeeded with {WarningCount} warnings");

                //Output file is the first file with the extension changed to .json
                string outputFile = Path.ChangeExtension(settings.Files[0], "json");
                List<DreamMapJson> maps = ConvertMaps(IncludedMaps);

                SaveJson(maps, IncludedInterface, outputFile);
            } else {
                Console.WriteLine($"Compilation failed with {ErrorCount} errors and {WarningCount} warnings");
            }

            TimeSpan duration = DateTime.Now - _compileStartTime;
            Console.WriteLine($"Total time: {duration.ToString(@"mm\:ss")}");

            return successfulCompile;
        }

        public static DMASTFile GetAST(List<string> files) {
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
                IncludedMaps = preproc.IncludedMaps;
                IncludedInterface = preproc.IncludedInterface;
                lexer = new Experimental.PreprocessorTokenConvert(preproc.GetEnumerator());
            }
            else {
                var preproc = Preprocess(files);
                lexer = new DMLexer(null, preproc.GetResult());
            }

            DMParser dmParser = new DMParser(lexer, !Settings.SuppressUnimplementedWarnings);

            VerbosePrint("Parsing");
            DMASTFile astFile = dmParser.File();
            return astFile;

        }

        public static int CompileAST(DMASTFile ast) {
            DMObjectBuilder dmObjectBuilder = new DMObjectBuilder();
            dmObjectBuilder.BuildObjectTree(ast);

            if (ErrorCount > 0) {
                return 255;
            }
            return 0;
        }
        private static DMPreprocessor Preprocess(List<string> files) {
            DMPreprocessor preprocessor = new DMPreprocessor(true, !Settings.SuppressUnimplementedWarnings);

            if (!Settings.NoStandard) {
                string compilerDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string dmStandardDirectory = Path.Combine(compilerDirectory ?? string.Empty, "DMStandard");
                preprocessor.IncludeFile(dmStandardDirectory, "_Standard.dm");
            }

            VerbosePrint("Preprocessing");
            foreach (string file in files) {
                string directoryPath = Path.GetDirectoryName(file);
                string fileName = Path.GetFileName(file);

                preprocessor.IncludeFile(directoryPath, fileName);
            }

            return preprocessor;
        }

        private static bool Compile(List<Token> preprocessedTokens) {
            DMLexer dmLexer = new DMLexer(null, preprocessedTokens);
            DMParser dmParser = new DMParser(dmLexer, !Settings.SuppressUnimplementedWarnings);

            VerbosePrint("Parsing");
            DMASTFile astFile = dmParser.File();

            if (dmParser.Warnings.Count > 0) {
                foreach (CompilerWarning warning in dmParser.Warnings) {
                    Warning(warning);
                }
            }

            if (dmParser.Errors.Count > 0) {
                foreach (CompilerError error in dmParser.Errors) {
                    Error(error);
                }
            }

            if (astFile == null) return false;

            DMASTSimplifier astSimplifier = new DMASTSimplifier();
            VerbosePrint("Constant folding");
            astSimplifier.SimplifyAST(astFile);

            DMObjectBuilder dmObjectBuilder = new DMObjectBuilder();
            dmObjectBuilder.BuildObjectTree(astFile);

            if (ErrorCount > 0) {
                return false;
            }

            return true;
        }

        public static void Error(CompilerError error) {
            Console.WriteLine(error);
            ErrorCount++;
        }

        public static void Warning(CompilerWarning warning) {
            Console.WriteLine(warning);
            WarningCount++;
        }

        public static void VerbosePrint(string message) {
            if (!Settings.Verbose) return;

            TimeSpan duration = DateTime.Now - _compileStartTime;
            Console.WriteLine($"{duration.ToString(@"mm\:ss\.fffffff")}: {message}");
        }

        private static List<DreamMapJson> ConvertMaps(List<string> mapPaths) {
            List<DreamMapJson> maps = new();

            foreach (string mapPath in mapPaths) {
                VerbosePrint($"Converting map {mapPath}");

                DMPreprocessor preprocessor = new DMPreprocessor(false, !Settings.SuppressUnimplementedWarnings);
                preprocessor.IncludeFile(Path.GetDirectoryName(mapPath), Path.GetFileName(mapPath));

                DMLexer lexer = new DMLexer(mapPath, preprocessor.GetResult());
                DMMParser parser = new DMMParser(lexer);
                DreamMapJson map = parser.ParseMap();

                if (parser.Errors.Count > 0) {
                    foreach (CompilerError error in parser.Errors) {
                        Error(error);
                    }

                    continue;
                }

                if (parser.Warnings.Count > 0) {
                    foreach (CompilerWarning warning in parser.Warnings) {
                        Warning(warning);
                    }
                }

                maps.Add(map);
            }

            return maps;
        }

        private static void SaveJson(List<DreamMapJson> maps, string interfaceFile, string outputFile) {
            DreamCompiledJson compiledDream = new DreamCompiledJson();
            compiledDream.Strings = DMObjectTree.StringTable;
            compiledDream.Maps = maps;
            compiledDream.Interface = interfaceFile;
            compiledDream.Types = DMObjectTree.CreateJsonRepresentation();
            if (DMObjectTree.GlobalInitProc.Bytecode.Length > 0) compiledDream.GlobalInitProc = DMObjectTree.GlobalInitProc.GetJsonRepresentation();

            if (DMObjectTree.Globals.Count > 0) {
                compiledDream.Globals = new List<object>();

                foreach (DMVariable global in DMObjectTree.Globals) {
                    if (!global.TryAsJsonRepresentation(out var globalJson))
                        throw new Exception($"Failed to serialize global {global.Name}");

                    compiledDream.Globals.Add(globalJson);
                }
            }

            string json = JsonSerializer.Serialize(compiledDream, new JsonSerializerOptions() {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            File.WriteAllText(outputFile, json);
            Console.WriteLine("Saved to " + outputFile);
        }
    }

    public struct DMCompilerSettings {
        public List<string> Files;
        public bool ExperimentalPreproc;
        public bool SuppressUnimplementedWarnings;
        public bool DumpPreprocessor;
        public bool NoStandard;
        public bool Verbose;
    }
}
