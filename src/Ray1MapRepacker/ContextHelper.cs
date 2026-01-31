using BinarySerializer;
using BinarySerializer.Ray1;
using BinarySerializer.Ray1.PC;

namespace Ray1MapRepacker;

public static class ContextHelper
{
    const string SerializerLogFilePath = "SerializerLog.txt";
    
    public static Context CreateDefaultContext()
    {
        // Create the context
        SerializerSettings serializerSettings = new() { IgnoreCacheOnRead = true };
        FileSerializerLogger serializerLogger = new(SerializerLogFilePath);
        Context context = new(String.Empty, settings: serializerSettings, serializerLogger: serializerLogger);

        // TODO: Could support other versions, like Rayman Designer, by determining version from level file header
        context.AddSettings(new Ray1Settings(Ray1EngineVersion.PC));
        
        return context;
    }

    public static LevelFile ReadLevelFile(Context context, string levFilePath)
    {
        // Read the level
        if (!context.FileExists(levFilePath))
        {
            context.AddFile(new LinearFile(context, levFilePath));
        }
        return FileFactory.Read<LevelFile>(context, levFilePath);
    }
}