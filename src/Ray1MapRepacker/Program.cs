using BinarySerializer;
using Ray1MapRepacker;

// Define constants
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
string mapFileDir = args[2];

if (mode is not ("-e" or "-i") || !File.Exists(levFilePath))
{
    ShowHelpScreen();
    return;
}

// Create the context
using Context context = ContextHelper.CreateDefaultContext();

// Export
if (mode == "-e")
{
    Exporter exporter = new(context);
    exporter.ExportLevel(levFilePath, mapFileDir, TileSetFileNamePrefix, MapFileName);
}
// Import
else if (mode == "-i")
{
    Importer importer = new(context);
    importer.ImportLevel(levFilePath, mapFileDir, TileSetFileNamePrefix, MapFileName);
}
else
{
    ShowHelpScreen();
    return;
}

ConsoleHelpers.WriteSuccess("Complete");