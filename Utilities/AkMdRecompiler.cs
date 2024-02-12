using System.Linq;
using System.Threading.Tasks;
// the character name and character portrait pair
using CharPortrait = System.Collections.Generic.Dictionary<string, string>;
using SList = System.Collections.Generic.List<string>;
using SListGroup = System.Collections.Generic.List<System.Collections.Generic.List<string>>;

namespace ArkPlotWpf.Utilities;

public class MdReconstructor
{
    private SList lineList;
    public readonly SListGroup LineGroups = new();
    public readonly List<PortraitGrp> PortraitGrps = new();
    public string Result
    {
        get
        {
            var lines = LineGroups.Select(grp => string.Join("\r\n\r\n", grp));
            return "\r\n" + string.Join("\r\n\r\n---\r\n\r\n", lines) + "\r\n";
        }
    }

    public MdReconstructor(string md)
    {
        lineList = new SList(md.Split("\r\n"));
        WashLines();
        GroupLinesBySegment();
        ProcessPortraits();
    }
    public MdReconstructor(IEnumerable<string> lines)
    {
        lineList = lines.ToList();
        GroupLinesBySegment();
        ProcessPortraits();
    }
    public void GetResultToBuilder(StringBuilder builder)
    {
        builder.AppendLine();
        foreach (var group in LineGroups)
        {
            builder.Append("\r\n\r\n---\r\n\r\n");
            builder.AppendJoin("\r\n\r\n", group);
        }
        builder.AppendLine();
    }

    private void ProcessPortraits()
    {
        var tasks = new List<Task>();
        foreach (var group in PortraitGrps)
        {
            Task ProcessSingleGroup()
            {
                CleanPortraitLines(group);
                CharPortrait portraitLinks = StaticNames(group.PortraitMarks);
                MakePortraitChart(group, portraitLinks);
                LineGroups[group.Index] = group.SList;
                return Task.CompletedTask;
            }

            tasks.Add(ProcessSingleGroup());
        }
        Task.WhenAll(tasks);
    }

    private void MakePortraitChart(PortraitGrp group, CharPortrait portraitLinks)
    {
        if (portraitLinks.Count == 0) return;
        var chartItems = string.Join("|", portraitLinks.Values);
        var chartHead = $"|{chartItems}|";
        var chartSeg = string.Concat(Enumerable.Repeat(" --- |", portraitLinks.Count));
        chartSeg = $"|{chartSeg}";
        var chartBody = $"{chartHead}\r\n{chartSeg}\r\n\r\n";
        group.SList.Insert(0, chartBody);
    }


    private void CleanPortraitLines(PortraitGrp group)
    {
        var portraitMark = group.PortraitMarks;
        foreach (var mark in portraitMark.Reverse())
        {
            var portraitIndex = mark.Index;
            /*
            *  ~~<img class="portrait"...~~
            *  ~~`立绘`name~~
            *  **Name**`说道：`....
            */
            // such kind of condition is normal condition, 
            // remove two lines
            group.SList.RemoveRange(portraitIndex, 2);
        }
        RemoveLiHui(group);

    }

    private static void RemoveLiHui(PortraitGrp group)
    {
        // `立绘`grani#1 
        //  ^
        //  the first char is `. after clean if still has such line, clean them.
        for (var i = group.SList.Count - 1; i >= 0; i--)
        {
            if (group.SList[i].Contains("立绘")) group.SList.RemoveAt(i);
        }
    }

    private CharPortrait StaticNames(IndexedCharacter[] characters)
    {
        var charaDict = new CharPortrait();
        foreach (var character in characters.Reverse())
        {
            if (character.Name == "s") continue;
            charaDict[character.Name] = AddTitleToPortrat(character.Name, character.Portrait);
        }
        return charaDict;
    }

    private string AddTitleToPortrat(string title, string portrait)
    {
        var splitPortrait = portrait.Split(' ').ToList();
        splitPortrait.Insert(1, $"title=\"{title}\"");
        var concated = string.Join(" ", splitPortrait);
        return $"<div class=\"crop\">{concated}</div>";
    }
    private void WashLines()
    {
        var linesWithoutEmptyLine =
          from line in lineList
          where !string.IsNullOrEmpty(line)
          select line;
        lineList = linesWithoutEmptyLine.ToList();
    }

    private void GroupLinesBySegment()
    {
        SList temp = new();
        var isPortaritGrp = false;
        int grpIndex = 0;
        foreach (var item in lineList)
        {
            if (item.StartsWith('-') && temp.Count >= 16)
            {
                temp.RemoveAll(line => line.StartsWith('-'));
                LineGroups.Add(new SList(temp));
                if (isPortaritGrp) AddPortrait(grpIndex, temp);
                PrepareForNextGroup();
            }
            // <img class="portrait"
            //             ^         it's 13th alphabet
            if (IsPortrait(item)) isPortaritGrp = true;
            temp.Add(item);
        }

        void PrepareForNextGroup()
        {
            temp.Clear();
            grpIndex++;
            isPortaritGrp = false;
        }
    }

    // private void GroupLinesBySegmentA()
    // {
    //     StringBuilder temp = new();
    //     var isPortaritGrp = false;
    //     int grpIndex = 0;
    //     // Regex pattern to match lines starting with '-' or containing '<img class="portrait"'
    //     Regex pattern = new Regex(@"^-\s*|<img class=""portrait""");
    //     foreach (var item in _lines)
    //     {
    //         // Check if the line matches the pattern
    //         Match match = pattern.Match(item);
    //         if (match.Success && temp.Length >= 16)
    //         {
    //             // Remove the lines starting with '-'
    //             temp.Replace("-\n", "");
    //             // Split the StringBuilder into an array of strings and add it to the LineGroups
    //             LineGroups.Add(temp.ToString().Split('\n').ToList());
    //             if (isPortaritGrp) AddPortrait(grpIndex, temp.ToString());
    //             PrepareForNextGroup();
    //         }
    //         // Check if the match contains the '<img class="portrait"' tag
    //         if (match.Groups[1].Success) isPortaritGrp = true;
    //         // Append the line to the StringBuilder
    //         temp.AppendLine(item);
    //     }
    //
    //     void PrepareForNextGroup()
    //     {
    //         temp.Clear();
    //         grpIndex++;
    //         isPortaritGrp = false;
    //     }
    // }


    private static bool IsPortrait(string item) => item.Length > 12 && item[12] == 'p';

    private void AddPortrait(int grpIndex, SList temp)
    {
        SList deepCopy = new(temp);
        var characters = GetCharacters(temp);
        PortraitGrps.Add(new PortraitGrp(grpIndex, deepCopy, characters));
    }

    private IndexedCharacter[] GetCharacters(SList paragraph)
    {
        List<IndexedCharacter> characters = new List<IndexedCharacter>();
        for (var i = 0; i < paragraph.Count - 1; i++)
        {
            var line = paragraph[i];
            if (!IsPortrait(line)) continue;
            if (i + 2 >= paragraph.Count)
            {
                characters.Add(MarkStrangeCharacter(i));
                continue;
            }

            string[] splitedStrong = paragraph[i + 2].Remove(0, 2).Split("**");
            var name = splitedStrong.FirstOrDefault();
            if (splitedStrong.Length != 2 || name == null || name.Length < 1)
            {
                /*
                *  ~~<img class="portrait"...~~
                *  ~~`立绘`name~~
                *  <audio ...
                *
                */
                // such kind of condition is named as "strange"("s")
                characters.Add(MarkStrangeCharacter(i));
                continue;
            }
            characters.Add(new IndexedCharacter(i, name, line));
        }
        return characters.ToArray();
    }
    private IndexedCharacter MarkStrangeCharacter(int index)
    {
        return new IndexedCharacter(index, "s", "");
    }


    public record PortraitGrp(int Index, SList SList, IndexedCharacter[] PortraitMarks);
    public record IndexedCharacter(int Index, string Name, string Portrait);
}
