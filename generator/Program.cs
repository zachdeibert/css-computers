using System;
using System.IO;
using System.Net;
using System.Text;

namespace Com.GitHub.ZachDeibert.CssComputers.Generator {
    class Program {
        static void Main(string[] args) {
            Options opts = new Options(args);
            if (opts.ParseFailureReason != null) {
                Console.Error.WriteLine(opts.ParseFailureReason);
                Console.Error.WriteLine();
                Options.PrintHelp(Console.Error);
                Environment.Exit(1);
            } else if (opts.ShowHelp) {
                Options.PrintHelp(Console.Out);
            } else if (opts.ShowVersion) {
                Options.PrintVersion(Console.Out);
            } else {
                FileParser parser = new FileParser(opts.Input);
                Translator translator = new Translator();
                if (opts.Output == null) {
                    using (HttpListener listener = new HttpListener()) {
                        listener.Prefixes.Add(string.Format("http://+:{0}/", opts.Port));
                        listener.Start();
                        parser.WaitForInitialParse();
                        Console.WriteLine("Listening on http://localhost:{0}/...", opts.Port);
                        while (true) {
                            HttpListenerContext ctx = listener.GetContext();
                            ComputerModel model = parser.Model;
                            string content;
                            if (ctx.Request.Url.PathAndQuery == "/style.css") {
                                content = translator.GenerateCSS(model);
                                ctx.Response.ContentType = "text/css";
                            } else {
                                content = translator.GenerateHTML(model);
                                ctx.Response.ContentType = "text/html";
                            }
                            byte[] buffer = Encoding.UTF8.GetBytes(content);
                            ctx.Response.ContentLength64 = buffer.Length;
                            ctx.Response.OutputStream.Write(buffer, 0, buffer.Length);
                            ctx.Response.OutputStream.Close();
                        }
                    }
                } else {
                    parser.WaitForInitialParse();
                    ComputerModel model = parser.Model;
                    parser.Stop();
                    string stylesheet = Path.Combine(Path.GetDirectoryName(opts.Output), string.Concat(Path.GetFileNameWithoutExtension(opts.Output), ".css"));
                    File.WriteAllText(opts.Output, translator.GenerateHTML(model, Path.GetFileName(stylesheet)));
                    File.WriteAllText(stylesheet, translator.GenerateCSS(model));
                    Console.WriteLine("Document written to {0}", opts.Output);
                }
            }
        }
    }
}
