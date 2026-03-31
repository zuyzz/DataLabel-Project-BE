using SharpCompress.Archives;
using DataLabelProject.Business.Services.FileUpload.Metadata;
using DataLabelProject.Shared.Utils;

namespace DataLabelProject.Business.Services.FileUpload;

/// <summary>
/// Processes archive uploads with production-grade optimizations.
/// - Streams archive to temp file (not RAM)
/// - Extracts image metadata using MetadataExtractor library
/// - Enforces resource limits
/// - Uses bounded parallelism
/// </summary>
public class ArchiveUploadStrategy : IFileUploadStrategy
{
    private readonly Storage.IFileStorage _storage;
    private readonly MetadataExtractorFactory _metadataExtractorFactory;
    
    // Resource limits
    private const long MaxArchiveSize = 500 * 1024 * 1024; // 500 MB
    private const long MaxEntrySize = 100 * 1024 * 1024;   // 100 MB per file
    private const int MaxFileCount = 1000;

    public ArchiveUploadStrategy(Storage.IFileStorage storage, MetadataExtractorFactory metadataExtractorFactory)
    {
        _storage = storage;
        _metadataExtractorFactory = metadataExtractorFactory;
    }

    public bool CanHandle(IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName ?? string.Empty).ToLowerInvariant();
        Console.WriteLine($"Archive check: {file.FileName} ext={ext}");
        return MediaTypeConstants.Archive.SupportedExtensions.Contains(ext);
    }

    public async Task<IEnumerable<FileItem>> ProcessAsync(
        IFormFile file,
        string storageDir)
    {
        // Validate archive size upfront
        if (file.Length > MaxArchiveSize)
            throw new InvalidOperationException($"Archive size {file.Length} exceeds maximum {MaxArchiveSize}");

        string? tempArchivePath = null;
        try
        {
            // Stream archive to temporary disk file (not RAM)
            tempArchivePath = Path.Combine(Path.GetTempPath(), $"archive_{Guid.NewGuid()}.tmp");
            using (var fileStream = file.OpenReadStream())
            using (var tempFileStream = File.Create(tempArchivePath))
            {
                await fileStream.CopyToAsync(tempFileStream);
            }

            // Process archive from temp file with bounded parallelism
            var uploaded = new List<FileItem>();
            
            // Read archive entries from temp file
            using (var fileStream = File.OpenRead(tempArchivePath))
            using (var archive = ArchiveFactory.Open(fileStream))
            {
                var entries = archive.Entries
                    .Where(e => !e.IsDirectory && IsValidExtension(Path.GetExtension(e.Key)))
                    .ToList();

                if (!entries.Any())
                    throw new InvalidOperationException($"Archive contains no allowed files");

                // Enforce file count limit
                if (entries.Count > MaxFileCount)
                    throw new InvalidOperationException($"Archive contains {entries.Count} files, exceeds maximum {MaxFileCount}");

                // CRITICAL FIX: Read archive entries sequentially, upload in parallel
                // SharpCompress RAR streams are NOT thread-safe, especially MultiVolumeReadOnlyStream
                // Multiple parallel reads corrupt the read pointer → ArgumentOutOfRangeException
                var uploadTasks = new List<Task<FileItem>>();
                var semaphore = new SemaphoreSlim(4, 4);

                foreach (var entry in entries)
                {
                    // Validate entry size using entry.Size (not entryStream.Length, which RAR doesn't support)
                    if (entry.Size > MaxEntrySize)
                        throw new InvalidOperationException($"File {entry.Key} exceeds maximum size {MaxEntrySize}");

                    // Read entry sequentially into MemoryStream (now thread-safe)
                    var ms = new MemoryStream();
                    using (var entryStream = entry.OpenEntryStream())
                    {
                        await entryStream.CopyToAsync(ms);
                    }
                    ms.Position = 0;

                    // Queue parallel upload task
                    uploadTasks.Add(Task.Run(async () =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            return await ProcessBufferedEntryAsync(ms, entry.Key, storageDir);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }));
                }

                uploaded.AddRange(await Task.WhenAll(uploadTasks));
            }

            return uploaded;
        }
        finally
        {
            // Cleanup temp file
            if (tempArchivePath != null && File.Exists(tempArchivePath))
            {
                try { File.Delete(tempArchivePath); } 
                catch { /* ignore cleanup errors */ }
            }
        }
    }

    private async Task<FileItem> ProcessBufferedEntryAsync(
        MemoryStream stream,
        string entryName,
        string storageKey)
    {
        stream.Position = 0;

        var hash = Hashing.ComputeHash(stream);
        stream.Position = 0;

        var ext = Path.GetExtension(entryName).ToLowerInvariant();
        var contentType = MediaTypeConstants.GetContentTypeByExtension(ext)
            ?? "application/octet-stream";

        var fileId = Guid.NewGuid();
        storageKey = $"{storageKey}/{fileId}";

        stream.Position = 0;
        var metadata = await _metadataExtractorFactory.ExtractMetadataAsync(stream, contentType);
        var uri = await _storage.CreateFileAsync(stream, storageKey, contentType);

        return new FileItem(fileId, contentType, uri, metadata ?? "{}", hash);
    }

    private static bool IsValidExtension(string ext)
    {
        ext = ext.ToLowerInvariant();
        return MediaTypeConstants.Image.SupportedExtensions.Contains(ext)
        // || MediaTypeConstants.Audio.SupportedExtensions.Contains(ext)
        // || MediaTypeConstants.Video.SupportedExtensions.Contains(ext)
        ;
    }
}

