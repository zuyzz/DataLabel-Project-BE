using Microsoft.AspNetCore.Http;

namespace DataLabelProject.Business.Services.FileUpload;

public record FileItem(Guid FileId, string ContentType, string StorageUri, string Metadata, string Hash);

public interface IFileUploadStrategy
{
    /// Returns true if this strategy can handle the provided upload file.
    bool CanHandle(IFormFile file);

    /// Process the uploaded file and upload individual items into storage; return list of uploaded items.
    /// Storage folder structure: [datasetId] datasetName/[random-uuid]/
    Task<IEnumerable<FileItem>> ProcessAsync(IFormFile file, string storageDir);
}

