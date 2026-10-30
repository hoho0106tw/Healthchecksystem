using System.IO.Compression;
using System.Reflection.Metadata;
using System.Text;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: PdbExtract <pdb-path> [output-dir]");
    return 2;
}

var pdbPath = Path.GetFullPath(args[0]);
var outputDir = args.Length > 1 ? Path.GetFullPath(args[1]) : null;
var embeddedSourceKind = new Guid("0E8A571B-6926-466E-B4AD-8AB04611F5FE");

using var stream = File.OpenRead(pdbPath);
using var provider = MetadataReaderProvider.FromPortablePdbStream(stream);
var reader = provider.GetMetadataReader();

foreach (var documentHandle in reader.Documents)
{
    var document = reader.GetDocument(documentHandle);
    var name = reader.GetString(document.Name);
    byte[]? sourceBytes = null;

    foreach (var debugInfoHandle in reader.GetCustomDebugInformation(documentHandle))
    {
        var debugInfo = reader.GetCustomDebugInformation(debugInfoHandle);
        if (reader.GetGuid(debugInfo.Kind) != embeddedSourceKind)
        {
            continue;
        }

        var blob = reader.GetBlobBytes(debugInfo.Value);
        if (blob.Length < 4)
        {
            continue;
        }

        var uncompressedSize = BitConverter.ToInt32(blob, 0);
        if (uncompressedSize == 0)
        {
            sourceBytes = blob.Skip(4).ToArray();
            break;
        }

        using var compressed = new MemoryStream(blob, 4, blob.Length - 4);
        using var deflate = new DeflateStream(compressed, CompressionMode.Decompress);
        using var output = new MemoryStream(uncompressedSize);
        deflate.CopyTo(output);
        sourceBytes = output.ToArray();
        break;
    }

    Console.WriteLine($"{name}\tEmbedded={(sourceBytes is not null ? "yes" : "no")}\tSize={sourceBytes?.Length ?? 0}");

    if (sourceBytes is null || outputDir is null)
    {
        continue;
    }

    var safeName = name
        .Replace('\\', Path.DirectorySeparatorChar)
        .Replace('/', Path.DirectorySeparatorChar)
        .TrimStart(Path.DirectorySeparatorChar);
    var outputPath = Path.Combine(outputDir, safeName);
    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    File.WriteAllText(outputPath, Encoding.UTF8.GetString(sourceBytes), new UTF8Encoding(false));
}

return 0;
