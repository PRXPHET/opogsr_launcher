
using opogsr_launcher;
using opogsr_launcher.Hasher;
using opogsr_launcher.JsonContext;
using opogsr_launcher.Managers;
using opogsr_launcher.Other;
using opogsr_launcher.Other.Converters;
using opogsr_launcher.Other.Secrets;
using opogsr_uploader;
using System.Diagnostics;
using System.Text.Json;

Console.Title = "OP OGSR Uploader";

GlobalExceptionHandler.Initialize();

string dir = Environment.CurrentDirectory;
string index = Path.Combine(dir, "index.json");

if (!File.Exists(index))
{
    Console.WriteLine("Index file not found.");
    Close();
}

JsonSerializerOptions options = new()
{
    AllowTrailingCommas = true,
    TypeInfoResolver = SourceGenerationContext.Default
};

const string token = Secrets.GithubUploadToken;
const string repo = Secrets.GithubRepo;

GithubUploadManager manager = new(token, repo);

await manager.RepoTask();

List<IndexData> data = JsonSerializer.Deserialize(File.ReadAllText(index), SourceGenerationContext.Default.ListIndexData);

Stopwatch sw = Stopwatch.StartNew();

Console.WriteLine("Checking for hashes...");

data.AsParallel().WithDegreeOfParallelism(8).ForAll(d =>
{
    string fullFileName = Path.Combine(dir, d.directory ?? "", d.name);
    using var stream = File.OpenRead(fullFileName);

    if (stream.Length > StaticGlobals.Variables.LargeChunkSize)
    {
        Console.WriteLine("Calc hashes for chunked file " + d.name);
        long length = stream.Length;
        uint chunk_count = 1;
        while (length > StaticGlobals.Variables.LargeChunkSize)
        {
            chunk_count++;
            length -= StaticGlobals.Variables.LargeChunkSize;
        }
        List<string> hash_list = FileHasher.XxHashFromChunkedFile(stream, chunk_count).GetAwaiter().GetResult();
        if (d.hash_list is null || !Enumerable.SequenceEqual(hash_list, d.hash_list))
        {
            d.hash_list = hash_list;
            d.hash = null;
            Console.WriteLine("Hashes changed for chunked file " + d.name);
        }
    }
    else
    {
        Console.WriteLine("Calc hash for file " + d.name);
        string hash = FileHasher.XxHashFromFile(stream).GetAwaiter().GetResult();
        if (d.hash != hash)
        {
            d.hash = hash;
            d.hash_list = null;
            Console.WriteLine("Hash changed for file " + d.name);
        }
    }
});

bool changed = await ProcessDifference(data, manager.data);

if (!changed)
{
    sw.Stop();
    Console.WriteLine("No changed files detected.");
    Close();
}

Console.WriteLine("Updating index...");
File.WriteAllText(index, JsonSerializer.Serialize(data, SourceGenerationContext.Default.ListIndexData));
await manager.UpdateConfig(index);

sw.Stop();

Console.WriteLine("Successfully completed in: " + sw.Elapsed.ToString());

Close();

async Task<bool> ProcessDifference(List<IndexData> current, List<IndexData> remote)
{
    var comparer = new IndexDataComparer();

    var toUpload = current.Except(remote, comparer).ToList();

    var toDelete = remote.Except(current, comparer).ToList();

    if (toUpload.Count == 0 && toDelete.Count == 0)
        return false;

    Console.WriteLine("Release update started...");

    var parallel_options = new ParallelOptions() { MaxDegreeOfParallelism = 8 };

    await Parallel.ForEachAsync(toDelete, parallel_options, async (d, ct) =>
    {
        await manager.DeleteFile(d.name);
        Console.WriteLine("Old file successfully deleted: " + d.name);
    });

    await Parallel.ForEachAsync(toUpload, parallel_options, async (d, ct) =>
    {
        ulong prev_sent = 0;
        ulong cur_sent = 0;
        ulong size = 0;

        bool keep_track = false;

        async void LogSpeed()
        {
            while (keep_track)
            {
                await Task.Delay(1000, ct);

                if (size > 0 && cur_sent > 0)
                    Console.WriteLine($"[{d.name}]: {BytesToString.Convert(cur_sent)} of {BytesToString.Convert(size)}. Speed = {BytesToString.Convert(cur_sent - prev_sent)}/s.");

                prev_sent = cur_sent;
            }
        }

        var progress = new Progress<(ulong Sent, ulong Total)>();
        progress.ProgressChanged += (_, data) =>
        {
            cur_sent = data.Sent;
            size = data.Total;
        };

        keep_track = true;
        LogSpeed();
        await manager.UploadFile(d, progress);
        keep_track = false;

        Console.WriteLine("New file successfully uploaded: " + d.name);
    });

    Console.WriteLine("Release has been updated...");

    return true;
}

static void Close()
{
    if (!Debugger.IsAttached)
    {
        Console.WriteLine("Press Enter to close.");
        Console.ReadKey();
    }
    Environment.Exit(0);
}

public class IndexDataComparer : IEqualityComparer<IndexData>
{
    public bool Equals(IndexData? x, IndexData? y)
    {
        if (x == null || y == null) return false;

        if ((x.hash_list is null && y.hash_list is not null) || (y.hash_list is null && x.hash_list is not null))
            return false;

        if (x.hash_list is not null && y.hash_list is not null)
            return x.name == y.name && Enumerable.SequenceEqual(x.hash_list, y.hash_list);

        return x.name == y.name && x.hash == y.hash;
    }

    public int GetHashCode(IndexData obj)
    {
        return obj.name.GetHashCode();
    }
}