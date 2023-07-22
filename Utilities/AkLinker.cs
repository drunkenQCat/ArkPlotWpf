using System;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using HtmlAgilityPack;

namespace ArkPlotWpf.Model;

internal class AkLinker
{
    //这个是用来从prts读关卡数据的
    public string ActiveName { get; }
    public string? ActiveCode { get; }
    private readonly List<string> stageList = new ();
    private readonly List<string> begList = new ();
    private readonly List<string> endList = new ();
    private readonly List<string> stList = new ();
            
    public AkLinker (string huodong)
    {
        // 为什么要用huodong呢？因为要输入的活动名必须是中文的 
        this.ActiveName = huodong;
        const string actListUrl = "https://wiki.biligame.com/arknights/%E7%94%A8%E6%88%B7:418054283/activity_table";
        var html = GetHtml(actListUrl);
        // 加载网页
        HtmlDocument activePage = new HtmlDocument();
        activePage.LoadHtml(html);
            
        // 这个网页是b站wiki的一个activity table，可以直接用中文名搜到活动代码
        var selfNode = activePage.DocumentNode.SelectSingleNode($"//td[text()='{this.ActiveName}']");
        // 找到带活动名的节点，第二个兄弟节点就是activeCode
        var activeNode = selfNode.ParentNode.Descendants("td").ToList()[0];
        var activeCode =activeNode?.InnerHtml;
        this.ActiveCode = activeCode;
        Console.WriteLine($"这个活动的代号是{activeCode}");
        GetStages();
    }

    public List<KeyValuePair<string,string>> LinkStages(Hashtable plots)
    {
        /*没啥好说的，抄python版，三步走
        *想想办法修改相应的键值
        */
        var fileList = plots.Keys;
        List<string> fileNames = new List<string>();
        foreach(var k in fileList)
        {
            fileNames.Add(k.ToString()!);
        }
        foreach(var oldKey in fileNames)
        {
            if (oldKey.Contains("beg")) SubKey(oldKey, plots, this.begList);
            if (oldKey.Contains("end")) SubKey(oldKey, plots, this.endList);
            if (oldKey.Contains("st")) SubKey(oldKey, plots, this.stList);
        }
        var sortedStages = SortStages(plots);
        return sortedStages;
    }
    private static string GetHtml(string url)
    {
        var _ = new HttpClient();
        var text = _.GetStringAsync(url).Result;
        return text;
    }
    private void GetStages()
    {
        // prts因为剧情模拟器的问题，暂时摆烂了。所以现在只能用情报处理室的网页来获取
        var activePage = new HtmlWeb();
        var htmlDoc = activePage.Load("https://prts.wiki/w/%E6%83%85%E6%8A%A5%E5%A4%84%E7%90%86%E5%AE%A4");
        var activeNode = htmlDoc.DocumentNode.SelectSingleNode($"//table[@class=\"wikitable\"]//big[text()='{this.ActiveName}']");
        // 关卡编号、行动前后、关卡名都要分别获取
        var codeNodes = activeNode.SelectNodes(@"./ancestor::tr/following-sibling::tr/td/div/table/tbody/tr/td/span[2]/span/text()");
        var preAndAftNodes = activeNode.SelectNodes(@"./ancestor::tr/following-sibling::tr/td//td[3]/span[1]/b/text()");
        var stageNameNodes = activeNode.SelectNodes(@"./ancestor::tr/following-sibling::tr/td/div/table/tbody/tr/td/span[2]/text()");
        Console.WriteLine("情报处理室已加载");

        // 这个小函数单纯用来降重的，不降重字典里边就自动替掉了
        void Sort(HtmlNode n, string s)
        {
            switch (n.InnerHtml)
            {
                case "行动前":
                    begList.Add(s);
                    break;
                case "行动后":
                    endList.Add(s);
                    break;
                case "幕间":
                    stList.Add(s);
                    break;
            }

            stageList.Add(s);
        }

        for (var i = 0; i < codeNodes.Count; i++)
        {
            var fullName = codeNodes[i].InnerText + " " +  stageNameNodes[i].InnerText + " " + preAndAftNodes[i].InnerHtml;

            Sort(preAndAftNodes[i], fullName);
        }

    }
        
    private string GetKey(string oldKey, List<string> newList)
    {
        var oldNum = GetDigit(oldKey);
        foreach(var n in newList)
        {
            if (GetDigit(n) == oldNum)
            {
                return n;
            }
        }
        return "";
    }

    private int GetDigit(string name)
    {
        // 这一坨用来把文件名里的数字提取出来
        Regex digitMatch =new Regex(@"\d+",RegexOptions.Compiled) ;
        var oldMatches = digitMatch.Matches(name);
        // 把匹配到的数字写进数组
        string[] oldNumbers = new string[oldMatches.Count];
        for (int i = 0; i < oldMatches.Count; i++)
        {
            oldNumbers[i] = oldMatches[i].Value;
        }
        // 把数组里最后一个成员转换成整型
        int oldNum = int.Parse(oldNumbers[oldNumbers.Length - 1]);
        return oldNum;
    }

    private void SubKey(string oldKey, Hashtable plots, List<string> newList)
    {
        var newKey = GetKey(oldKey, newList);
        plots[newKey] = plots[oldKey];
        plots.Remove(oldKey);

    }

    private List<KeyValuePair<string,string>> SortStages(Hashtable plots)
    {
        List<KeyValuePair<string,string>> dict = new List<KeyValuePair<string,string>>();
        foreach (var name in this.stageList)
        {
            var text = plots[name]!.ToString();
            var pair = new KeyValuePair<string,string>(name, text!);
            dict.Add(pair);
        }
        return dict;
    }
}