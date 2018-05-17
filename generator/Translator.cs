using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace Com.GitHub.ZachDeibert.CssComputers.Generator {
    class Translator {
        XmlDocument HtmlTemplate;
        XmlDocument CheckboxTemplate;
        string CssHeader;
        string ValidCss;
        string InvalidCss;

        void GenerateTitle(ComputerModel model, XmlDocument html) {
            XmlElement title = (XmlElement) html.GetElementsByTagName("title").Item(0);
            title.InnerText = string.Format(title.InnerText, model.Name);
        }

        public string GenerateHTML(ComputerModel model) {
            XmlDocument html = (XmlDocument) HtmlTemplate.CloneNode(true);
            GenerateTitle(model, html);
            XmlElement div = html.GetElementsByTagName("div").OfType<XmlElement>().First(e => e.GetAttribute("class") == "boxes");
            foreach (Pin pin in model.Pins.OrderBy(p => p.Offset)) {
                XmlElement template = (XmlElement) CheckboxTemplate.DocumentElement.CloneNode(true);
                XmlElement input = (XmlElement) template.GetElementsByTagName("input").Item(0);
                XmlElement label = (XmlElement) template.GetElementsByTagName("label").Item(0);
                if (pin.Type == PinType.Intermediate) {
                    input.SetAttribute("class", "intermediate-box");
                    template.RemoveChild(label);
                } else {
                    if (pin.FirstOfType) {
                        label.InnerText = string.Format(label.InnerText, pin.NameOfType);
                    } else {
                        template.RemoveChild(label);
                    }
                    if (pin.Type == PinType.Input) {
                        input.SetAttribute("class", "input-box");
                    } else {
                        input.SetAttribute("class", "output-box");
                    }
                }
                foreach (XmlNode node in template.ChildNodes) {
                    div.AppendChild(html.ImportNode(node, true));
                }
            }
            StringBuilder str = new StringBuilder();
            str.AppendLine("<!DOCTYPE html>");
            XmlWriterSettings settings = new XmlWriterSettings {
                OmitXmlDeclaration = true
            };
            using (XmlWriter writer = XmlWriter.Create(str, settings)) {
                html.WriteTo(writer);
            }
            return str.ToString();
        }

        void AddressPin(ComputerModel model, StringBuilder css, Pin pin, bool value) {
            css.AppendFormat("input:nth-of-type({0}):{1}", pin.Offset + 1, value ? "checked" : "not(:checked)");
        }

        public string GenerateCSS(ComputerModel model) {
            StringBuilder css = new StringBuilder();
            css.AppendLine(CssHeader);
            foreach (TruthTable table in model.TruthTables) {
                foreach (bool[] key in table.Table.Keys) {
                    bool value = table.Table[key];
                    int i = 0;
                    css.Append(".run:checked ~ .boxes ");
                    foreach (Pin pin in table.Inputs.OrderBy(p => p.Offset)) {
                        AddressPin(model, css, pin, key[i++]);
                        css.Append(" ~ ");
                    }
                    AddressPin(model, css, table.Output, value);
                    css.Append(", .run:checked ~ .boxes ");
                    i = 0;
                    foreach (Pin pin in table.Inputs.OrderBy(p => p.Offset)) {
                        AddressPin(model, css, pin, key[i++]);
                        css.Append(" ~ ");
                    }
                    AddressPin(model, css, table.Output, value);
                    css.Append(" + svg {");
                    css.Append(ValidCss);
                    css.AppendLine(" }");
                    i = 0;
                    css.Append(".run:checked ~ .boxes ");
                    foreach (Pin pin in table.Inputs.OrderBy(p => p.Offset)) {
                        AddressPin(model, css, pin, key[i++]);
                        css.Append(" ~ ");
                    }
                    AddressPin(model, css, table.Output, !value);
                    css.Append(", .run:checked ~ .boxes ");
                    i = 0;
                    foreach (Pin pin in table.Inputs.OrderBy(p => p.Offset)) {
                        AddressPin(model, css, pin, key[i++]);
                        css.Append(" ~ ");
                    }
                    AddressPin(model, css, table.Output, !value);
                    css.Append(" + svg {");
                    css.Append(InvalidCss);
                    css.AppendLine(" }");
                }
            }
            return css.ToString();
        }

        public Translator() {
            Assembly asm = typeof(Translator).Assembly;
            HtmlTemplate = new XmlDocument();
            using (Stream stream = asm.GetManifestResourceStream("generator.index.html")) {
                HtmlTemplate.Load(stream);
            }
            CheckboxTemplate = new XmlDocument();
            using (Stream stream = asm.GetManifestResourceStream("generator.checkbox.html")) {
                CheckboxTemplate.Load(stream);
            }
            using (Stream stream = asm.GetManifestResourceStream("generator.style.css")) {
                using (TextReader reader = new StreamReader(stream)) {
                    CssHeader = reader.ReadToEnd();
                }
            }
            using (Stream stream = asm.GetManifestResourceStream("generator.valid.css")) {
                using (TextReader reader = new StreamReader(stream)) {
                    ValidCss = reader.ReadToEnd();
                }
            }
            ValidCss = Regex.Replace(ValidCss, ".*{(.*)}.*", "$1", RegexOptions.Singleline);
            using (Stream stream = asm.GetManifestResourceStream("generator.invalid.css")) {
                using (TextReader reader = new StreamReader(stream)) {
                    InvalidCss = reader.ReadToEnd();
                }
            }
            InvalidCss = Regex.Replace(InvalidCss, ".*{(.*)}.*", "$1", RegexOptions.Singleline);
        }
    }
}
