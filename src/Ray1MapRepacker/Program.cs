using BinarySerializer;
using BinarySerializer.Ray1;
using BinarySerializer.Ray1.PC;
using Ray1MapRepacker;

// Define constants
const string SerializerLogFilePath = "SerializerLog.txt";
const string MapFileName = "LevelMap.map";
const string TileSetFileNamePrefix = "TileSet";

void ShowHelpScreen()
{
    Console.WriteLine("Rayman 1 PCX Tool\n" +
                      "\n" +
                      "Usage:\n" +
                      "  -e <level-path> <output-path> | Exports the tileset and map from the level to the output path.\n" +
                      "                                  A separate tileset PCX file is exported per available palette.\n" +
                      "  -i <level-path> <input-path>  | Imports the tileset and map to the level from the input path.\n" +
                      "                                  The files have to be named the same as when exporting. Only the\n" +
                      "                                  first tileset PCX will be used for the tiles. The rest will only\n" +
                      "                                  be used to import alternative palettes.");
}

// Parse args
if (args.Length != 3)
{
    ShowHelpScreen();
    return;
}

string mode = args[0];
string levFilePath = args[1];
string exportDir = args[2];

if (mode is not ("-e" or "-i") || !File.Exists(levFilePath))
{
    ShowHelpScreen();
    return;
}

// Create the context
SerializerSettings serializerSettings = new() { IgnoreCacheOnRead = true };
FileSerializerLogger serializerLogger = new(SerializerLogFilePath);
using Context context = new(String.Empty, settings: serializerSettings, serializerLogger: serializerLogger);

// TODO: Could support other versions, like Rayman Designer, by determining version from level file header
context.AddSettings(new Ray1Settings(Ray1EngineVersion.PC));

// Read the level
context.AddFile(new LinearFile(context, levFilePath));
LevelFile levFile = FileFactory.Read<LevelFile>(context, levFilePath);

// Export
if (mode == "-e")
{
    Console.WriteLine("Starting exporting process");
    
    Exporter exporter = new Exporter(context);
    exporter.ExportTileSet(levFile, exportDir, TileSetFileNamePrefix);
    exporter.ExportMap(levFile, exportDir, MapFileName);
    
    Console.WriteLine("Finished exporting process");
}
// Import
else if (mode == "-i")
{
    Console.WriteLine("Starting importing process");

    Importer importer = new Importer(context);
    importer.ImportTileSet(levFile, exportDir, TileSetFileNamePrefix);
    importer.ImportMap(levFile, exportDir, MapFileName);
    importer.UpdateMapTileRenderModes(levFile);
    importer.SaveFile(levFilePath, levFile);
    
    Console.WriteLine("Finished importing process");
}
else
{
    ShowHelpScreen();
}

ConsoleHelpers.WriteSuccess("Complete");