using Microsoft.AspNetCore.Http;

namespace NexusOXP.Services
{
    public interface IFileStorageService
    {
        Task<StoredFileResult> SaveTaskAttachmentAsync(int taskId, IFormFile file);

        Task DeleteTaskAttachmentAsync(int taskId, string storedFileName);

        string GetTaskAttachmentPath(int taskId, string storedFileName);
    }

    public record StoredFileResult(
        string FileName,
        string StoredFileName,
        string ContentType,
        long FileSize);
}
