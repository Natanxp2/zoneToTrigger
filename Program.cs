﻿using System.Formats.Asn1;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace zoneToTrigger;

class Program
{
    public static bool modify = false;
    public static string filepath = string.Empty;

    public static List<Region> checkpoints = [];
    public static List<Region> cancels = [];
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("File not provided");
            return;
        }
        modify = args.Contains("--modify") || args.Contains("-M");
        filepath = args[^1];

        if (!File.Exists(filepath) || !filepath.EndsWith(".json"))
        {
            Console.WriteLine($"File can't be loaded, exiting: {filepath}");
            return;
        }

        try
        {
            string json = File.ReadAllText(filepath);

            using var doc = JsonDocument.Parse(json);
            var segment = doc.RootElement
                .GetProperty("tracks")
                .GetProperty("main")
                .GetProperty("zones")
                .GetProperty("segments")[0];

            var checkpointsJson = segment.GetProperty("checkpoints").GetRawText();
            if (!string.IsNullOrEmpty(checkpointsJson))
            {
                var checkpointsArray = JsonDocument.Parse(checkpointsJson).RootElement.EnumerateArray();

                foreach (var checkpoint in checkpointsArray)
                {
                    var regionsJson = checkpoint.GetProperty("regions").GetRawText();
                    var regions = JsonSerializer.Deserialize<List<Region>>(regionsJson)!;
                    checkpoints.AddRange(regions);
                }

                checkpoints.RemoveAt(0);
            }

            // Extract and deserialize cancels
            var cancelsJson = segment.GetProperty("cancel").GetRawText();
            if (!string.IsNullOrEmpty(cancelsJson))
            {
                var cancelsArray = JsonDocument.Parse(cancelsJson).RootElement.EnumerateArray();

                foreach (var cancel in cancelsArray)
                {
                    var regionsJson = cancel.GetProperty("regions").GetRawText();
                    var regions = JsonSerializer.Deserialize<List<Region>>(regionsJson)!;
                    cancels.AddRange(regions);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            return;
        }

        int i = 0;
        foreach (var checkpoint in checkpoints)
        {
            checkpoint.targetname = $"limit_rockets_{i}_created_by_tools";
            checkpoint.GetValues();
            i++;
        }

        i = 0;
        foreach (var cancel in cancels)
        {
            cancel.targetname = $"infinite_rockets_{i}_created_by_tools";
            cancel.GetValues();
            i++;
        }

        WriteToFile();
    }

    public static void WriteToFile()
    {
        string cfgFilePath = filepath[..^5] + ".cfg";
        string logicOutput = "add:\n{\n";
        logicOutput += "\"classname\" \"logic_auto\"\n";
        logicOutput += "\"spawnflags\" \"0\"\n";
        logicOutput += "\"targetname\" \"ammo_triggers_created_by_tools\"\n";
        logicOutput += $"\"origin\" \"{checkpoints[0].origin[0]} {checkpoints[0].origin[1]} {checkpoints[0].origin[2]}\"\n";

        using StreamWriter writer = new(cfgFilePath);

        foreach (var checkpoint in checkpoints)
        {
            writer.Write(checkpoint.AddTrigger(limitAmmo: true));
            logicOutput += checkpoint.AddLogicOutput();
        }

        foreach (var cancel in cancels)
        {
            writer.Write(cancel.AddTrigger(limitAmmo: false));
            logicOutput += cancel.AddLogicOutput();
        }

        logicOutput += "}";

        writer.Write(logicOutput);
    }
}

public class Region
{
    [JsonPropertyName("points")]
    public List<List<double>> Points { get; set; }

    [JsonPropertyName("bottom")]
    public double Bottom { get; set; }

    [JsonPropertyName("height")]
    public double Height { get; set; }

    //Helpers
    public List<double>? origin;
    public string? mins;
    public string? maxs;
    public string targetname = string.Empty;
    public void GetValues()
    {
        if (Points.Count != 4)
        {
            Console.WriteLine("Region doesn't have exactly 4 points, exiting");
            Environment.Exit(1);
        }

        GetOrigin();
        GetMinsAndMaxs();
    }

    public void GetOrigin()
    {
        List<double> point1 = Points[0];
        List<double> point2 = Points[2];

        double midX = Math.Round((point1[0] + point2[0]) / 2, 3);
        double midY = Math.Round((point1[1] + point2[1]) / 2, 3);
        double midZ = Math.Round(Bottom + (Height / 2), 3);

        origin = [midX, midY, midZ];
    }
    public void GetMinsAndMaxs()
    {
        List<double> point1 = Points[0];
        List<double> point2 = Points[2];

        double halfSizeX = Math.Round(Math.Abs(point1[0] - point2[0]) / 2, 3);
        double halfSizeY = Math.Round(Math.Abs(point1[1] - point2[1]) / 2, 3);
        double halfSizeZ = Math.Round(Height / 2, 3);

        maxs = "maxs " + halfSizeX + " " + halfSizeY + " " + halfSizeZ;

        mins = "mins " + (halfSizeX * -1) + " " + (halfSizeY * -1) + " " + (halfSizeZ * -1);

    }

    public string AddTrigger(bool limitAmmo)
    {
        string s = "add:\n";
        s += "{\n";
        s += "\"classname\" \"trigger_multiple\"\n";
        s += $"\"origin\" \"{origin![0]} {origin[1]} {origin[2]}\"\n";
        s += $"\"spawnflags\" \"4097\"\n";
        s += $"\"StartDisabled\" \"0\"\n";
        s += $"\"targetname\" \"{targetname}\"\n";
        s += $"\"wait\" \"{1}\"\n";

        if (limitAmmo)
            s += $"\"OnTrigger\" \"!activator,SetRockets,4,0,-1\"\n";
        else
            s += $"\"OnStartTouch\" \"!activator,SetRockets,-1,0,-1\"\n";

        s += "}\n";

        return s;
    }

    public string AddLogicOutput()
    {
        string s = "";
        s += $"\"OnMapSpawn\" \"{targetname},AddOutput,solid 2,0.5,1\"\n";
        s += $"\"OnMapSpawn\" \"{targetname},AddOutput,{mins},1,1\"\n";
        s += $"\"OnMapSpawn\" \"{targetname},AddOutput,{maxs},1,1\"\n";
        return s;
    }

}