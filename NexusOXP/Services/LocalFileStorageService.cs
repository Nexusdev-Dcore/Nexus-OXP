using Microsoft.AspNetCore.StaticFiles;

namespace NexusOXP.Services
{
    public class LocalFileStorageService : IFileStorageService
    {
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf",
            ".png",
            ".jpg",
            ".jpeg",
            ".gif",
            ".webp",
            ".txt",
            ".md",
            ".csv",
            ".xlsx",
            ".docx"
        };

        private const long MaxFileSize = 10 * 1024 * 1024;

        private readonly IWebHostEnvironment _environment;
        private readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

        public LocalFileStorageService(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public async Task<StoredFileResult> SaveTaskAttachmentAsync(int taskId, IFormFile file)
        {
            if (file.Length <= 0)
            {
                throw new InvalidOperationException("Choose a non-empty file.");
            }

            if (file.Length > MaxFileSize)
            {
                throw new InvalidOperationException("Files must be 10 MB or smaller.");
            }

            var fileName = Path.GetFileName(file.FileName);
            var extension = Path.GetExtension(fileName);

            if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            {
                throw new InvalidOperationException("This file type is not allowed.");
            }

            var storedFileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
            var directory = GetTaskAttachmentDirectory(taskId);
            Directory.CreateDirectory(directory);

            var fullPath = Path.Combine(directory, storedFileName);
            await using (var stream = File.Create(fullPath))
            {
                await file.CopyToAsync(stream);
            }

            var contentType = !string.IsNullOrWhiteSpace(file.ContentType)
                ? file.ContentType
                : _contentTypeProvider.TryGetContentType(fileName, out var detectedType)
                    ? detectedType
                    : "application/octet-stream";

            return new StoredFileResult(fileName, storedFileName, contentType, file.Length);
        }

        public Task DeleteTaskAttachmentAsync(int taskId, string storedFileName)
        {
            var path = GetTaskAttachmentPath(taskId, storedFileName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return Task.CompletedTask;
        }

        public string GetTaskAttachmentPath(int taskId, string storedFileName)
        {
            return Path.Combine(GetTaskAttachmentDirectory(taskId), Path.GetFileName(storedFileName));
        }

        private string GetTaskAttachmentDirectory(int taskId)
        {
            return Path.Combine(_environment.ContentRootPath, "App_Data", "uploads", "tasks", taskId.ToString());
        }
    }
}
