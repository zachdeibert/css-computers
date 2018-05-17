using System;
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
                using (HttpListener listener = new HttpListener()) {
                    listener.Prefixes.Add(string.Format("http://+:{0}/", opts.Port));
                    listener.Start();
                    FileParser parser = new FileParser(opts.Input);
                    Translator translator = new Translator();
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
            }
        }
    }
}
