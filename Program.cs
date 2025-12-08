using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace zoneToTrigger;

class Program
{
    public static string filepath = string.Empty;
    public static string triggersType = string.Empty;
    public static Dictionary<string, StringBuilder> triggers = [];

    public static List<Region> checkpoints = [];
    public static List<Region> cancels = [];
    static void Main(string[] args)
    {
        if (args.Length < 2)
        {
            string helpMessage = @"Provide the name of a configuration file followed by a path to the .json file

Example 1: ./zoneToTrigger rj test.json
Example 2: zoneToTrigger.exe df.ini ./zones/test.json
            
.ini file must be present in the executable directory to be loaded, providing the extension is not required";
            Console.WriteLine(helpMessage);
            return;
        }

        triggersType = Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "config"))
                        .FirstOrDefault(f => Path.GetFileName(f).StartsWith(args[0], StringComparison.OrdinalIgnoreCase));

        if (!File.Exists(triggersType))
        {
            Console.WriteLine($"Configuration file {args[0]} doesn't exist");
            return;
        }

        filepath = args[^1];
        triggers = ParseIni(triggersType);

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
            else
            {
                Console.WriteLine("No checkpoints detected!");
                Environment.Exit(1);
            }

            // Extract and deserialize cancels
            string cancelsJson = string.Empty;
            try
            {
                cancelsJson = segment.GetProperty("cancel").GetRawText();
            }
            catch { }

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
        string guid = Guid.NewGuid().ToString("N")[..10];
        foreach (var checkpoint in checkpoints)
        {

            checkpoint.targetname = $"checkpoint_{i + 1}_{guid}";
            checkpoint.zoneToTrigger = $"created_by_zoneToTrigger";
            checkpoint.GetValues();
            i++;
        }

        i = 0;
        foreach (var cancel in cancels)
        {
            cancel.targetname = $"cancel_{i + 1}_{guid}";
            cancel.zoneToTrigger = $"created_by_zoneToTrigger";
            cancel.GetValues();
            i++;
        }

        WriteToFile(guid);
    }

    public static void WriteToFile(string guid)
    {
        string cfgFilePath = filepath[..^5] + ".cfg";
        string logicOutput = "add:\n{\n";
        logicOutput += "\"classname\" \"logic_auto\"\n";
        logicOutput += "\"spawnflags\" \"0\"\n";
        logicOutput += $"\"targetname\" \"{guid}\"\n";
        logicOutput += "\"zoneToTrigger\" \"created_by_zoneToTrigger\"\n";
        logicOutput += $"\"origin\" \"{checkpoints[0].origin[0]} {checkpoints[0].origin[1]} {checkpoints[0].origin[2]}\"\n";

        using StreamWriter writer = new(cfgFilePath);

        if (checkpoints.Count > 0)
        {
            if (!triggers.ContainsKey("checkpoint"))
            {
                Console.Error.WriteLine($"WARNING: Checkpoint zones are not defined.");
            }
            else
            {
                foreach (var checkpoint in checkpoints)
                {
                    writer.Write(checkpoint.AddTrigger("checkpoint"));
                    logicOutput += checkpoint.AddLogicOutput();
                }
            }
        }

        if (cancels.Count > 0)
        {
            if (!triggers.ContainsKey("cancel"))
            {
                Console.Error.WriteLine($"WARNING: Cancel zones are not defined.");
            }
            else
            {
                foreach (var cancel in cancels)
                {
                    writer.Write(cancel.AddTrigger("cancel"));
                    logicOutput += cancel.AddLogicOutput();
                }
            }
        }

        logicOutput += "}";

        writer.Write(logicOutput);
    }

    public static Dictionary<string, StringBuilder> ParseIni(string filepath)
    {
        string[] lines = File.ReadAllLines(filepath);
        var sections = new Dictionary<string, StringBuilder>();
        string current = null;

        foreach (var line in lines)
        {
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                current = line.Trim('[', ']').ToLower();
                sections[current] = new StringBuilder();
            }
            else if (current != null)
            {
                sections[current].AppendLine(line);
            }
        }
        return sections;
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
    public List<double> origin = [];
    public string mins = string.Empty;
    public string maxs = string.Empty;
    public string targetname = string.Empty;
    public string zoneToTrigger = string.Empty;
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

    public string AddTrigger(string section)
    {
        if (!Program.triggers.TryGetValue(section, out var value))
        {
            Console.Error.WriteLine($"WARNING: trigger '{section}' is not defined.");
            return "";
        }
        string trigger = value.ToString();

        string s = $"\"origin\" \"{origin![0]} {origin[1]} {origin[2]}\"\n";
        s += $"\"targetname\" \"{targetname}\"\n";
        s += $"\"zoneToTrigger\" \"{zoneToTrigger}\"\n";

        int index = trigger.LastIndexOf('}');
        if (index != -1)
        {
            trigger = trigger.Insert(index, s);
        }

        return trigger;

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