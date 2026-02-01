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
                      "     <tile-set-path>            | In addition, a tileset path to an existing PCX file can be attached\n" +
                      "                                  to avoid exporting the tileset, but forcing the map to use the existing one.\n" +
                      "  -i <level-path> <input-path>  | Imports the tileset and map to the level from the input path.\n" +
                      "                                  The files have to be named the same as when exporting. Only the\n" +
                      "                                  first tileset PCX will be used for the tiles. The rest will only\n" +
                      "                                  be used to import alternative palettes.");
}

// Parse args
if (args.Length is < 3 or > 4)
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

// Read the level

// Export
if (mode == "-e")
{
    if (args.Length > 3)
    {
        string tileSetPathPCX = args[3];
        new Exporter(context).ExportLevelForceUsingTileSet(levFilePath, mapFileDir, MapFileName, tileSetPathPCX);
    }
    else
    {
        new Exporter(context).ExportLevel(levFilePath, mapFileDir, MapFileName, TileSetFileNamePrefix);
    }
}
// Import
else if (mode == "-i")
{
    new Importer(context).ImportLevel(levFilePath, mapFileDir, MapFileName, TileSetFileNamePrefix);
}
else
{
    ShowHelpScreen();
}

ConsoleHelpers.WriteSuccess("Complete");