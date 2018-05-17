using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Com.GitHub.ZachDeibert.CssComputers.Generator {
    class FileParser {
        Thread Thread;
        string FileName;
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

        ComputerModel GenerateModel() {
            ComputerModel model = new ComputerModel {
                Name = Path.GetFileNameWithoutExtension(FileName)
            };
            int lineNum = 0;
            foreach (string[] line in File.ReadAllLines(FileName).Select(s => s.Trim().Split(' '))) {
                ++lineNum;
                if (line.Length > 0 && !line[0].StartsWith("#")) {
                    switch (line[0]) {
                        case "input":
                        case "output":
                        case "pin":
                            if (line.Length != 3) {
                                Console.Error.WriteLine("Invalid {0} command on line {1}: invalid number of arguments", line[0], lineNum);
                                return model;
                            }
                            PinType type;
                            switch (line[0]) {
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
                            if (!int.TryParse(line[2], out width) || width < 1) {
                                Console.Error.WriteLine("Invalid {0} command on line {1}: invalid width '{2}'", line[0], lineNum, line[2]);
                                return model;
                            }
                            if (width == 1) {
                                model.Pins.Add(new Pin {
                                    Type = type,
                                    Name = line[1],
                                    NameOfType = line[1],
                                    FirstOfType = true
                                });
                            } else {
                                model.Pins.AddRange(Enumerable.Range(0, width).Select(i => new Pin {
                                    Type = type,
                                    Name = string.Format("{0}{1}", line[1], i),
                                    NameOfType = line[1],
                                    FirstOfType = i == 0
                                }));
                            }
                            break;
                        case "truth":
                            if (line.Length < 5) {
                                Console.Error.WriteLine("Invalid truth command on line {0}: invalid number of arguments", lineNum);
                                return model;
                            }
                            TruthTable table = new TruthTable {
                                Inputs = line[1].Split(',').Select(s => model.Pins.FirstOrDefault(p => p.Name == s)).ToList(),
                                Output = model.Pins.FirstOrDefault(p => p.Name == line[2] && p.Type != PinType.Input)
                            };
                            if (table.Inputs.Any(p => p == null) || table.Output == null) {
                                Console.Error.WriteLine("Unknown pin reference in truth table on line {0}", lineNum);
                                return model;
                            }
                            if (line.Length != 3 + Math.Pow(2, table.Inputs.Count)) {
                                Console.Error.WriteLine("Invalid truth command on line {0}: invalid number of arguments", lineNum);
                                return model;
                            }
                            bool[] key = new bool[table.Inputs.Count];
                            Array.Fill(key, false);
                            for (int i = 3; i < line.Length; ++i) {
                                bool value;
                                switch (line[i]) {
                                    case "0":
                                        value = false;
                                        break;
                                    case "1":
                                        value = true;
                                        break;
                                    default:
                                        Console.Error.WriteLine("Invalid truth command on line {0}: invalid value", lineNum);
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
                            Console.Error.WriteLine("Invalid command '{0}' on line {1}", line[0], lineNum);
                            return model;
                    }
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
            FileSystemWatcher watcher = new FileSystemWatcher(Path.GetDirectoryName(FileName), Path.GetFileName(FileName));
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
            FileName = file;
            Thread.Start();
            ModelLock = new object();
            InitialParseSemaphore = new Semaphore(0, 1);
        }
    }
}
