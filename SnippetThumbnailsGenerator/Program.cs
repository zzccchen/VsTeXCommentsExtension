﻿using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media;
using System.Xml.Linq;
using Microsoft.VisualStudio.Text;
using VsTeXCommentsExtension.Integration;
using VsTeXCommentsExtension.Integration.Data;
using VsTeXCommentsExtension.View;

namespace SnippetThumbnailsGenerator
{
    internal class Program
    {
        private const string OutputPath = "Output";
        private static HtmlRenderer renderer;

        [STAThread]
        private static void Main()
        {
            new System.Windows.Application(); //needed for correct resource loading

            var rendererCache = new HtmlRendererCache();
            if (Directory.Exists(rendererCache.CacheDirectory))
            {
                Directory.Delete(rendererCache.CacheDirectory, true);
            }

            if (Directory.Exists(OutputPath))
            {
                Directory.Delete(OutputPath, true);
            }
            Directory.CreateDirectory(OutputPath);

            var form = new Form();
            form.Load +=
                (s, e) =>
                {
                    renderer = new HtmlRenderer();
#pragma warning disable VSTHRD110
                    Task.Run(new Action(GenerateSnippets));
#pragma warning restore VSTHRD110
                };
            Application.Run(form); //we need message pump for web browser
        }

        private static void GenerateSnippets()
        {
            var font = new System.Drawing.Font("Consolas", 12);
            var cfg = XElement.Load(@"..\..\..\Snippets.xml");
            var exportElement = new XElement("Snippets");
            int index = 0;
            foreach (var snippet in cfg.Elements("Snippet"))
            {
                var code = snippet.Element("Code").Value;
                Console.WriteLine($"Rendering {code}");

                var teXCommentText = $"$${code}$$";
                var fullSpan = new Span(0, teXCommentText.Length);
                var teXCommentBlockSpan = new TeXCommentBlockSpan(fullSpan, fullSpan, 0, 0, 0, "\r\n", 100, null, null, "CSharp");
                var result = renderer.Render(
                      new HtmlRenderer.Input(
                          new TeXCommentTag(teXCommentText, teXCommentBlockSpan),
                          1.3,
                          Colors.Black,
                          Colors.White,
                          font,
                          null,
                          null));

                SaveSnippet(result.CachePath, snippet.Element("Group").Value, code, $"{++index}.png", exportElement);
            }

            exportElement.Save(Path.Combine(OutputPath, "Snippets.xml"));

            Console.WriteLine("Done");
        }

        private static void SaveSnippet(string renderedImagePath, string group, string code, string outputPath, XElement exportElement)
        {
            var absoluteOutputPath = Path.Combine(Environment.CurrentDirectory, OutputPath, outputPath);
            var outputDirectory = Path.GetDirectoryName(absoluteOutputPath);
            if (!Directory.Exists(outputDirectory)) Directory.CreateDirectory(outputDirectory);
            File.Copy(renderedImagePath, absoluteOutputPath);


            bool isMultiline = code.Contains('\n');
            if (isMultiline)
            {
                var lines = code.Split('\n');
                for (int i = 1; i < lines.Length; i++)
                {
                    lines[i] = "//" + lines[i];
                }
                code = lines.Aggregate((a, b) => a + "\r\n" + b);
            }

            var snippetElement = new XElement("Snippet");
            snippetElement.Add(new XElement("Group") { Value = group });
            snippetElement.Add(new XElement("Code") { Value = code });
            snippetElement.Add(new XElement("Icon") { Value = "Snippets/" + outputPath });
            exportElement.Add(snippetElement);
        }
    }
}