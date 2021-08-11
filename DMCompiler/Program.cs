using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using DMCompiler.Compiler.DMM;
using DMCompiler.DM;
using DMCompiler.DM.Visitors;
using OpenDreamShared.Compiler;
using OpenDreamShared.Compiler.DM;
using OpenDreamShared.Compiler.DMPreprocessor;
using OpenDreamShared.Json;

namespace DMCompiler {
    class Program {
        public static int _errorCount = 0;
        public static string[] CompilerArgs;
        public static List<string> CompiledFiles = new List<string>(1);

        static void Main(string[] args) {
            if (!VerifyArguments(args)) return;
            CompilerArgs = args;

            DateTime startTime = DateTime.Now;

            DMPreprocessor preprocessor = Preprocess(CompiledFiles);
            if (HasArgument("--dump-preprocessor"))
            {
                StringBuilder result = new();
                foreach (Token t in preprocessor.GetResult()) {
                    result.Append(t.Text);
                }

                string output = Path.ChangeExtension(CompiledFiles[0], "dm") ?? Path.Join(System.AppDomain.CurrentDomain.BaseDirectory, "preprocessor_dump.dm");
                File.WriteAllText(output, result.ToString());
                Console.WriteLine($"Preprocessor output dumped to {output}");
            }


            if (Compile(preprocessor.GetResult())) {
                //Output file is the first file with the extension changed to .json
                string outputFile = Path.ChangeExtension(CompiledFiles[0], "json");
                List<DreamMapJson> maps = ConvertMaps(preprocessor.IncludedMaps);

                SaveJson(maps, preprocessor.IncludedInterface, outputFile);
                DateTime endTime = DateTime.Now;
                TimeSpan duration = endTime - startTime;
                Console.WriteLine($"Total time: {duration.ToString(@"mm\:ss")}");
            } else {
                //Compile errors, exit with an error code
                Environment.Exit(1);
            }
        }

        private static bool VerifyArguments(string[] args) {
            bool file_to_compile = false;

            foreach (string arg in args) {
                if (Path.HasExtension(arg))
                {
                    string extension = Path.GetExtension(arg);
                    if(extension != ".dme" && extension != ".dm") {
                        Console.WriteLine(arg + " is not a valid DME or DM file, aborting");

                        return false;
                    }

                    CompiledFiles.Add(arg);
                    file_to_compile = true;
                    Console.WriteLine($"Compiling {Path.GetFileName(arg)}");
                }
            }

            if (!file_to_compile)
            {
                Console.WriteLine("At least one DME or DM file must be provided as an argument");
                return false;
            }

            return true;
        }

        private static bool HasArgument(string arg)
        {
            return CompilerArgs.Contains(arg);
        }

        private static DMPreprocessor Preprocess(List<string> files) {
            DMPreprocessor preprocessor = new DMPreprocessor(true);

            string compilerDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string dmStandardDirectory = Path.Combine(compilerDirectory ?? string.Empty, "DMStandard");
            preprocessor.IncludeFile(dmStandardDirectory, "_Standard.dm");

            foreach (string file in files) {
                string directoryPath = Path.GetDirectoryName(file);
                string fileName = Path.GetFileName(file);

                preprocessor.IncludeFile(directoryPath, fileName);
            }

            return preprocessor;
        }

        private static void CompareResult(DMASTNode n1, DMASTNode n2, string s) {
            Console.WriteLine("===================================================================================    " + s);
            Console.WriteLine( DMAST.PrintNodes(n1, max_depth: 2) );
            Console.WriteLine("-----------------------------------------------------------------------------------");
            Console.WriteLine( DMAST.PrintNodes(n2, max_depth: 2) );
            Console.WriteLine("===================================================================================");

        }
        private static bool Compile(List<Token> preprocessedTokens) {
            DMLexer dmLexer = new DMLexer(null, preprocessedTokens);
            DMParser dmParser = new DMParser(dmLexer);
            DMASTFile astFile = dmParser.File();
            Console.WriteLine("Parse 1");
            if (dmParser.Warnings.Count > 0) {
                foreach (CompilerWarning warning in dmParser.Warnings) {
                    Console.WriteLine(warning);
                }
            }

            if (dmParser.Errors.Count > 0) {
                foreach (CompilerError error in dmParser.Errors) {
                    Console.WriteLine(error);
                }
            }
            DMLexer dmLexer2 = new TokenWhitespaceFilter(new DMLexer(null, preprocessedTokens));
            OpenDreamShared.Compiler.DM.Testing.DMParser dmParser2 = new OpenDreamShared.Compiler.DM.Testing.DMParser(dmLexer2);
            Console.WriteLine("Parse 2");
            DMASTFile astFile2 = dmParser2.File();
            if (dmParser2.Warnings.Count > 0) {
                foreach (CompilerWarning warning in dmParser2.Warnings) {
                    Console.WriteLine(warning);
                }
            }
            if (dmParser2.Errors.Count > 0) {
                foreach (CompilerError error in dmParser2.Errors) {
                    Console.WriteLine(error);
                }
            }

            astFile.Compare(astFile2, new DMAST.CompareResult(CompareResult));

            DMASTSimplifier astSimplifier = new DMASTSimplifier();
            astSimplifier.SimplifyAST(astFile);

            DMVisitorObjectBuilder dmObjectBuilder = new DMVisitorObjectBuilder();
            dmObjectBuilder.BuildObjectTree(astFile);

            if (_errorCount > 0) {
                return false;
            }

            return true;
        }

        public static void Error(CompilerError error) {
            Console.WriteLine(error);
            _errorCount++;
        }

        public static void Warning(CompilerWarning warning) {
            Console.WriteLine(warning);
        }

        private static List<DreamMapJson> ConvertMaps(List<string> mapPaths) {
            List<DreamMapJson> maps = new();

            foreach (string mapPath in mapPaths) {
                DMPreprocessor preprocessor = new DMPreprocessor(false);
                preprocessor.IncludeFile(String.Empty, mapPath);

                DMLexer lexer = new DMLexer(mapPath, preprocessor.GetResult());
                DMMParser parser = new DMMParser(lexer);
                DreamMapJson map = parser.ParseMap();

                if (parser.Errors.Count > 0) {
                    foreach (CompilerError error in parser.Errors) {
                        Console.WriteLine(error);
                    }

                    continue;
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
            compiledDream.RootObject = DMObjectTree.CreateJsonRepresentation();
            if (DMObjectTree.GlobalInitProc != null) compiledDream.GlobalInitProc = DMObjectTree.GlobalInitProc.GetJsonRepresentation();

            string json = JsonSerializer.Serialize(compiledDream, new JsonSerializerOptions() {
                IgnoreNullValues = true
            });

            File.WriteAllText(outputFile, json);
            Console.WriteLine("Saved to " + outputFile);
        }
    }
}
