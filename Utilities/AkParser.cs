using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ArkPlotWpf.Model
{
    class AkParser
    {
                
        private Regex nameReg = new Regex("(?<=(\\[name=\")|(\\[multiline\\(name=\")).*(?=\")",RegexOptions.Compiled);
        private Regex nameLineReg = new Regex("\\[name=\".*\"\\]", RegexOptions.Compiled);    // 角色名的相关正则
        private Regex lineLineReg = new Regex("(?<=\\[)[A-Za-z]*(?=\\])", RegexOptions.Compiled);// [Charater]、[Dialog]等无参标签就变成线
        private Regex chineseLineReg = new Regex("^[^\\[].*$", RegexOptions.Compiled);// 以“[”起头的，就一律当做引用。另，不一定是汉字
        private Regex tagLineReg = new Regex("(?<=(\\[(?!name))).*(?=\\()", RegexOptions.Compiled);   // 标签不是name的另外处理
        private List<Regex> regList = new List<Regex>();
        private List<Func<string, string>> methodList = new List<Func<string, string>>();
        private JObject tagList = new JObject();
        

        public string? markDown { get; set;}
        public string jsonFile;

        public AkParser(string plot, string jsonPath)
        {
            jsonFile = jsonPath;
            AkParserInit();
            ConvertToMarkdown(plot);
        }
        private void Readjson()
        {
            // tag都存在这个地方

            using (System.IO.StreamReader file = System.IO.File.OpenText(jsonFile))
            {
                using (JsonTextReader reader = new JsonTextReader(file))
                {
                    this.tagList = (JObject)JToken.ReadFrom(reader);
                }
            }
        }

        private void AkParserInit()
        {
            Readjson();
            regList.Add(nameReg);
            regList.Add(tagLineReg);
            regList.Add(lineLineReg);
            regList.Add(chineseLineReg);
            methodList.Add(Namlize);
            methodList.Add(Taglize);
            methodList.Add(Linize);
            methodList.Add(Chinize);
        }

        private string Namlize(string line)
        {
            var name = nameReg.Match(line).Value;
            var nameLine = nameLineReg.Replace(line, $"**{name}**`讲道：`");
            return nameLine + Environment.NewLine;
        }

        private string Taglize(string line)
        {
            var tag = tagLineReg.Match(line).Value;
            tag = tag.ToLower();
            var tagNew = (string)tagList[tag]!;
            line = line.Replace("_", " ");
            if (tagNew == "") return tagNew;
            try
            {
                var tagReg = (string)tagList[tag+"_reg"]!;
                var newLine = tagNew + Regex.Match(line, tagReg).Value;
                return newLine + Environment.NewLine;
            }
            catch (System.Exception)
            {
                Console.WriteLine($"出错的句子\n{line}");
                throw ;
            }
        }

        private string Linize(string line)
        {
            return "\r\n\r\n---";
        }

        private string Chinize(string line)
        {
            return $"> {line}\r\n";
        }

        private string MatchType(string line)
        {
            for(int i = 0; i < regList.Count; i++)
            {
                var matched = regList[i].Match(line);
                if (matched.Value != "")
                {
                    var result = methodList[i](line);
                    return result;
                }
            }
            return line;
        }

        private string[] PlotSplitter(string plot)
        {
            return plot.Split("\n");
        }

        private string RipDollar(string text)
        {
            text = Regex.Replace(text, @"\$", "");
            return text;
        }
        private string ConvertToMarkdown(string plot)
        {
            var plotlines = PlotSplitter(plot);
            // 做一些降重的努力
            int count = 1;
            // 每一章的第一个有效句一定是分隔线
            var preLine = "\r\n\r\n---";
            foreach (var line in plotlines)
            {
                var newLine = MatchType(line);
                if (newLine == "") continue;
                if (newLine == preLine)
                {
                    count++;
                    continue;
                }
                else
                {
                    var currentLine = newLine;
                    if(preLine == "\r\n\r\n---")
                        newLine = preLine;
                    else if(count == 1)
                        newLine =preLine;
                        
                    else if(count > 1)
                        newLine = preLine + "×" + count;
                    count = 1;
                    preLine = currentLine;
                }
                markDown = markDown + newLine + "\r\n";
            }
            if (markDown!=null)
            {
                markDown = RipDollar(markDown);
                return markDown;
            }
            else
            {
                Console.WriteLine("什么都没写上去");
                System.Environment.Exit(1);
                return "";
            }
        }
    }
}