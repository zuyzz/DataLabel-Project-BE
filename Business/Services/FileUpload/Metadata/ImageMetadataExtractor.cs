using System.Text.Json;
using MetadataExtractor;
using MetadataExtractor.Formats.Jpeg;
using MetadataExtractor.Formats.Png;
using MetadataExtractor.Formats.Gif;
using MetadataExtractor.Formats.WebP;
using MetadataDirectory = MetadataExtractor.Directory;

namespace DataLabelProject.Business.Services.FileUpload.Metadata;

/// <summary>
/// Extracts width/height metadata from image files using the MetadataExtractor library.
/// Reads only image header/metadata without decoding pixel data into memory.
/// Supports: PNG, JPEG, GIF, WebP
/// </summary>
public class ImageMetadataExtractor : IMetadataExtractor
{
    public bool CanHandle(string contentType) =>
        MediaTypeConstants.Image.SupportedContentTypes.Contains(
            contentType?.ToLowerInvariant() ?? string.Empty);

    public async Task<string?> ExtractAsync(Stream stream)
    {
        try
        {
            long? fileSize = null;

            if (stream.CanSeek)
            {
                fileSize = stream.Length;
                stream.Position = 0;
            }

            var directories = await Task.Run(() => ImageMetadataReader.ReadMetadata(stream));
            var (width, height) = ExtractDimensions(directories);

            if (width > 0 && height > 0)
                return JsonSerializer.Serialize(new { width, height, fileSize });
        }
        catch
        {
            // Silently fail - metadata extraction is optional
        }

        return null;
    }

    /// <summary>
    /// Extracts image dimensions from metadata directories.
    /// Uses type-specific directory methods for accurate extraction.
    /// </summary>
    private static (int width, int height) ExtractDimensions(IEnumerable<MetadataDirectory> directories)
    {
        foreach (var directory in directories)
        {
            var (width, height) = directory switch
            {
                JpegDirectory jpeg => TryGetDimensions(jpeg, JpegDirectory.TagImageWidth, JpegDirectory.TagImageHeight),
                PngDirectory png => TryGetDimensions(png, PngDirectory.TagImageWidth, PngDirectory.TagImageHeight),
                GifHeaderDirectory gif => TryGetDimensions(gif, GifHeaderDirectory.TagImageWidth, GifHeaderDirectory.TagImageHeight),
                WebPDirectory webp => TryGetDimensions(webp, WebPDirectory.TagImageWidth, WebPDirectory.TagImageHeight),
                _ => (0, 0)
            };

            if (width > 0 && height > 0)
                return (width, height);
        }

        return (0, 0);
    }

    /// <summary>
    /// Helper to safely extract width/height from a directory using tag IDs.
    /// </summary>
    private static (int width, int height) TryGetDimensions(MetadataDirectory directory, int widthTag, int heightTag)
    {
        int width = directory.TryGetInt32(widthTag, out var w) ? w : 0;
        int height = directory.TryGetInt32(heightTag, out var h) ? h : 0;
        return (width, height);
    }
}
