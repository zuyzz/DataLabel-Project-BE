using DataLabelProject.Business.Services.FileUpload.Metadata;
using DataLabelProject.Shared.Utils;

namespace DataLabelProject.Business.Services.FileUpload;

public class ImageUploadStrategy : IFileUploadStrategy
{
    private readonly Storage.IFileStorage _storage;
    private readonly MetadataExtractorFactory _metadataExtractorFactory;

    public ImageUploadStrategy(Storage.IFileStorage storage, MetadataExtractorFactory metadataExtractorFactory)
    {
        _storage = storage;
        _metadataExtractorFactory = metadataExtractorFactory;
    }

    public bool CanHandle(IFormFile file)
    {
        var contentType = file.ContentType?.ToLowerInvariant() ?? string.Empty;
        var isImage = MediaTypeConstants.Image.SupportedContentTypes.Contains(contentType);
        return isImage && !IsArchive(file.FileName);
    }

    public async Task<IEnumerable<FileItem>> ProcessAsync(
        IFormFile file,
        string storageDir)
    {
        using var stream = file.OpenReadStream();

        var hash = Hashing.ComputeHash(stream);
        stream.Position = 0;

        var contentType = file.ContentType ?? "application/octet-stream";

        var fileId = Guid.NewGuid();
        var storageKey = $"{storageDir}/{fileId}";

        stream.Position = 0;
        var metadata = await _metadataExtractorFactory.ExtractMetadataAsync(stream, contentType);
        var uri = await _storage.CreateFileAsync(stream, storageKey, contentType);

        var item = new FileItem(fileId, contentType, uri, metadata ?? "{}", hash);

        return new[] { item };
    }

    private static bool IsArchive(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false;
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return MediaTypeConstants.Archive.SupportedExtensions.Contains(ext);
    }
}
