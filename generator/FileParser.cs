using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Com.GitHub.ZachDeibert.CssComputers.Generator {
    class FileParser {
        Thread Thread;
        string Directory;
        string ChipName;
        volatile ComputerModel CurrentModel;
        object ModelLock;
        Semaphore InitialParseSemaphore;

        public ComputerModel Model {
            get {
                lock (ModelLock) {
                    return CurrentModel;
                }
            }
        }

        string MapPin(string prefix, Dictionary<string, string> mappings, string name) {
            if (mappings.ContainsKey(name)) {
                return mappings[name];
            } else {
                return string.Concat(prefix, name);
            }
        }

        List<ReadLine> ReadFile(string chip, string prefix, Dictionary<string, string> mappings) {
            string file = Path.Combine(Directory, string.Concat(chip, ".hdl"));
            List<ReadLine> lines = new List<ReadLine>();
            int includedChips = 0;
            foreach (ReadLine line in File.ReadAllLines(file).Select((s, i) => new ReadLine {
                    Tokens = s.Trim().Split(' '),
                    FileName = file,
                    LineNumber = i + 1
                }).Where(l => l.Tokens.Length > 0 && !l.Tokens[0].StartsWith("#")).ToList()) {
                switch (line.Tokens[0]) {
                    case "input":
                    case "output":
                        if (prefix != "") {
                            break;
                        }
                        goto case "pin";
                    case "pin":
                        if (line.Tokens.Length > 1) {
                            line.Tokens[1] = MapPin(prefix, mappings, line.Tokens[1]);
                        }
                        lines.Add(line);
                        break;
                    case "truth":
                        if (line.Tokens.Length > 2) {
                            line.Tokens[1] = string.Join(",", line.Tokens[1].Split(',').Select(s => MapPin(prefix, mappings, s)));
                            line.Tokens[2] = MapPin(prefix, mappings, line.Tokens[2]);
                        }
                        lines.Add(line);
                        break;
                    default:
                        Dictionary<string, string> newMappings = new Dictionary<string, string>();
                        foreach (string[] tokens in line.Tokens.Skip(1).Select(s => s.Split('='))) {
                            if (tokens.Length == 2) {
                                newMappings[tokens[0]] = MapPin(prefix, mappings, tokens[1]);
                            } else {
                                Console.Error.WriteLine("Invalid token after chip call ({0}:{1})", line.FileName, line.LineNumber);
                            }
                        }
                        string newPrefix = string.Format("{0}._inclusion_prefix_{1}", prefix, includedChips++);
                        lines.AddRange(ReadFile(line.Tokens[0], newPrefix, newMappings));
                        break;
                }
            }
            return lines;
        }

        ComputerModel GenerateModel() {
            ComputerModel model = new ComputerModel {
                Name = ChipName
            };
            foreach (ReadLine line in ReadFile(ChipName, "", new Dictionary<string, string>())) {
                switch (line.Tokens[0]) {
                    case "input":
                    case "output":
                    case "pin":
                        if (line.Tokens.Length != 3) {
                            Console.Error.WriteLine("Invalid {0} command: invalid number of arguments ({1}:{2})", line.Tokens[0], line.FileName, line.LineNumber);
                            return model;
                        }
                        PinType type;
                        switch (line.Tokens[0]) {
                            case "input":
                                type = PinType.Input;
                                break;
                            case "output":
                                type = PinType.Output;
                                break;
                            default:
                                type = PinType.Intermediate;
                                break;
                        }
                        int width;
                        if (!int.TryParse(line.Tokens[2], out width) || width < 1) {
                            Console.Error.WriteLine("Invalid {0} command: invalid width '{1}' ({2}:{3})", line.Tokens[0], line.Tokens[2], line.FileName, line.LineNumber);
                            return model;
                        }
                        if (width == 1) {
                            model.Pins.Add(new Pin {
                                Type = type,
                                Name = line.Tokens[1],
                                NameOfType = line.Tokens[1],
                                FirstOfType = true
                            });
                        } else {
                            model.Pins.AddRange(Enumerable.Range(1, width).Select(i => new Pin {
                                Type = type,
                                Name = string.Format("{0}{1}", line.Tokens[1], width - i),
                                NameOfType = line.Tokens[1],
                                FirstOfType = i == 1
                            }));
                        }
                        break;
                    case "truth":
                        if (line.Tokens.Length < 5) {
                            Console.Error.WriteLine("Invalid truth command: invalid number of arguments ({0}:{1})", line.FileName, line.LineNumber);
                            return model;
                        }
                        TruthTable table = new TruthTable {
                            Inputs = line.Tokens[1].Split(',').Select(s => model.Pins.FirstOrDefault(p => p.Name == s)).ToList(),
                            Output = model.Pins.FirstOrDefault(p => p.Name == line.Tokens[2] && p.Type != PinType.Input)
                        };
                        if (table.Inputs.Any(p => p == null) || table.Output == null) {
                            Console.Error.WriteLine("Unknown pin reference in truth table ({0}:{1})", line.FileName, line.LineNumber);
                            return model;
                        }
                        if (line.Tokens.Length != 3 + Math.Pow(2, table.Inputs.Count)) {
                            Console.Error.WriteLine("Invalid truth command: invalid number of arguments ({0}:{1})", line.FileName, line.LineNumber);
                            return model;
                        }
                        bool[] key = new bool[table.Inputs.Count];
                        Array.Fill(key, false);
                        for (int i = 3; i < line.Tokens.Length; ++i) {
                            bool value;
                            switch (line.Tokens[i]) {
                                case "0":
                                    value = false;
                                    break;
                                case "1":
                                    value = true;
                                    break;
                                default:
                                    Console.Error.WriteLine("Invalid truth command: invalid value ({0}:{1})", line.FileName, line.LineNumber);
                                    return model;
                            }
                            bool[] copy = new bool[key.Length];
                            Array.Copy(key, copy, key.Length);
                            table.Table[copy] = value;
                            for (int j = key.Length - 1; j >= 0; --j) {
                                if (key[j]) {
                                    key[j] = false;
                                } else {
                                    key[j] = true;
                                    break;
                                }
                            }
                        }
                        model.TruthTables.Add(table);
                        break;
                    default:
                        Console.Error.WriteLine("Invalid command '{0}' ({1}:{2})", line.Tokens[0], line.FileName, line.LineNumber);
                        return model;
                }
            }
            int offset = 0;
            foreach (Pin pin in model.Pins.Where(p => p.Type == PinType.Input)) {
                pin.Offset = offset++;
            }
            HashSet<Pin> unmapped = model.Pins.Where(p => p.Type != PinType.Input).ToHashSet();
            while (unmapped.Count > 0) {
                Pin[] toMap = unmapped.Where(p => model.TruthTables.Where(t => t.Output == p).All(t => t.Inputs.All(p2 => !unmapped.Contains(p2)))).ToArray();
                foreach (Pin pin in toMap) {
                    pin.Offset = offset++;
                    unmapped.Remove(pin);
                }
                if (toMap.Length == 0) {
                    Console.Error.WriteLine("Unable to map pins without recursion.");
                    return model;
                }
            }
            return model;
        }

        void Run() {
            FileSystemWatcher watcher = new FileSystemWatcher(Directory, string.Concat(ChipName, ".hdl"));
            ComputerModel model = GenerateModel();
            lock (ModelLock) {
                CurrentModel = model;
            }
            InitialParseSemaphore.Release();
            while (true) {
                watcher.WaitForChanged(WatcherChangeTypes.All);
                Console.WriteLine("Changes detected.  Reparsing file.");
                model = GenerateModel();
                lock (ModelLock) {
                    CurrentModel = model;
                }
            }
        }

        public void WaitForInitialParse() {
            if (InitialParseSemaphore != null) {
                InitialParseSemaphore.WaitOne();
                InitialParseSemaphore.Release();
            }
        }

        public FileParser(string file) {
            Thread = new Thread(Run);
            Directory = Path.GetDirectoryName(file);
            ChipName = Path.GetFileNameWithoutExtension(file);
            Thread.Start();
            ModelLock = new object();
            InitialParseSemaphore = new Semaphore(0, 1);
        }
    }
}
