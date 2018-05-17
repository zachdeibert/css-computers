using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Com.GitHub.ZachDeibert.CssComputers.Generator {
    class Options {
        public bool ShowHelp;
        public bool ShowVersion;
        public string ParseFailureReason;
        public string Input;
        public string Output;
        public int Port = 8000;

        public static void PrintHelp(TextWriter stream) {
            stream.WriteLine("Usage: {0} [-h|--help] [-v|--version] [-p|--port <port num>] [-o|--out <output file>] <input file>", Process.GetCurrentProcess().MainModule.FileName);
            stream.WriteLine();
            stream.WriteLine("Options:");
            stream.WriteLine("    -h, --help     Show this help message");
            stream.WriteLine("    -v, --version  Show the version information");
            stream.WriteLine("    -p, --port     Specify the port to listen on");
            stream.WriteLine("    -o, --out      Specify the output file and do not start a server");
        }

        public static void PrintVersion(TextWriter stream) {
            stream.WriteLine("CSS Computer Generator");
            stream.WriteLine("Version {0}", typeof(Options).Assembly.GetName().Version);
            stream.WriteLine();
            stream.WriteLine("Written by Zach Deibert <zachdeibert@gmail.com>");
            stream.WriteLine("https://github.com/zachdeibert/css-computers");
        }

        public Options(string[] args) {
            IEnumerator<string> argEnum = ((IEnumerable<string>) args).GetEnumerator();
            while (argEnum.MoveNext()) {
                switch (argEnum.Current) {
                    case "-h":
                    case "--help":
                        ShowHelp = true;
                        break;
                    case "-v":
                    case "--version":
                        ShowVersion = true;
                        break;
                    case "-p":
                    case "--port":
                        if (argEnum.MoveNext()) {
                            if (!int.TryParse(argEnum.Current, out Port)) {
                                ParseFailureReason = "Invalid port number";
                            } else if (Port <= 0 || Port >= 65536) {
                                ParseFailureReason = "Port out of range";
                            }
                        } else {
                            ParseFailureReason = string.Concat("Expected parameter after '", argEnum.Current, "'");
                        }
                        break;
                    case "-o":
                    case "--out":
                        if (argEnum.MoveNext()) {
                            Output = argEnum.Current;
                        } else {
                            ParseFailureReason = string.Concat("Expected parameter after '", argEnum.Current, "'");
                        }
                        break;
                    default:
                        if (Input == null) {
                            if (File.Exists(argEnum.Current)) {
                                Input = argEnum.Current;
                            } else {
                                ParseFailureReason = string.Concat("File '", argEnum.Current, "' does not exist");
                            }
                        } else {
                            ParseFailureReason = "Cannot specify multipe input files";
                        }
                        break;
                }
            }
            if (Input == null) {
                ParseFailureReason = "No input file specified";
            }
        }
    }
}
