using System.Linq;
using SList = System.Collections.Generic.List<string>;
using SListGroup = System.Collections.Generic.List<System.Collections.Generic.List<string>>;
// the character name and character portrait pair
using CharPortrait = System.Collections.Generic.Dictionary<string, string>;

namespace ArkPlotWpf.Utilities;

public class MdReconstructor
{
    private SList _lines;
    public readonly SListGroup LineGroups = new();
    public readonly List<PortraitGrp> PortraitGrps = new();
    public string Result
    {
        get
        {
            var lines = LineGroups.Select(grp => string.Join("\r\n\r\n", grp));
            return string.Join("\r\n\r\n---\r\n\r\n", lines);
        }
    }

    public MdReconstructor(string md)
    {
        _lines = new SList(md.Split("\r\n"));
        WashLines();
        GroupLinesBySegment();
        ProcessPortraits();
    }

    private void ProcessPortraits()
    {
        foreach (var group in PortraitGrps)
        {
            CleanPortraitLines(group);
            CharPortrait portraitLinks = StaticNames(group.PortraitMarks);
            MakePortraitChart(group, portraitLinks);
        }
    }

    private void MakePortraitChart(PortraitGrp group, CharPortrait portraitLinks)
    {
        var chartItems = string.Join("|", portraitLinks.Values);
        var chartHead = $"|{chartItems}|";
        var chartSeg = string.Concat(Enumerable.Repeat(" --- |", portraitLinks.Count));
        chartSeg = $"|{chartSeg}";
        var chartBody = $"{chartHead}\r\n{chartSeg}\r\n\r\n";
        group.SList.Insert(0, chartBody);
        LineGroups[group.Index] = group.SList;
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
        return string.Join(" ", splitPortrait);
    }
    private void WashLines()
    {
        var linesWithoutEmptyLine =
          from line in _lines
          where !string.IsNullOrEmpty(line)
          select line;
        _lines = linesWithoutEmptyLine.ToList();
    }

    private void GroupLinesBySegment()
    {
        SList temp = new();
        var isPortaritGrp = false;
        int grpIndex = 0;
        foreach (var item in _lines)
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
            char k = GetTheP(item);
            if (k == 'p') isPortaritGrp = true;
            temp.Add(item);
        }

        void PrepareForNextGroup()
        {
            temp.Clear();
            grpIndex++;
            isPortaritGrp = false;
        }
    }

    private static char GetTheP(string item)
    {
        char k;
        try
        {
            k = item[12];
        }
        catch (System.Exception)
        {

            k = ' ';
        }

        return k;
    }

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
            if (GetTheP(line) != 'p') continue;
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
