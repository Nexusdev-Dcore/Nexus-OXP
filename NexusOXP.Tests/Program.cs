using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using NexusOXP.Models;
using NexusOXP.Services;
using NexusOXP.ViewModels;

var tests = new List<(string Name, Action Test)>
{
    ("Project DTO preserves project state", ProjectDtoPreservesProjectState),
    ("Project planning validator rejects inverted dates", ProjectPlanningValidatorRejectsInvertedDates),
    ("Project planning validator accepts open dates", ProjectPlanningValidatorAcceptsOpenDates),
    ("Project planning validator detects past due dates", ProjectPlanningValidatorDetectsPastDueDates),
    ("Task DTO defaults match workflow defaults", TaskDtoDefaultsMatchWorkflowDefaults),
    ("Local file storage accepts safe attachment", LocalFileStorageAcceptsSafeAttachment),
    ("Local file storage rejects unsafe attachment", LocalFileStorageRejectsUnsafeAttachment)
};

var failures = 0;
foreach (var (name, test) in tests)
{
    try
    {
        test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failures++;
        Console.WriteLine($"FAIL {name}: {ex.Message}");
    }
}

if (failures > 0)
{
    Environment.ExitCode = 1;
}

static void ProjectDtoPreservesProjectState()
{
    var dto = new ProjectDto(
        10,
        "NEXUS Website",
        "Portfolio project",
        "owner-1",
        "Dorart Admin",
        DateTime.UtcNow,
        null,
        DateTime.UtcNow.AddDays(7),
        ProjectStatus.Active,
        ProjectPriority.High,
        4,
        "Development",
        12);

    Assert(dto.Status == ProjectStatus.Active, "Project status was not preserved.");
    Assert(dto.Priority == ProjectPriority.High, "Project priority was not preserved.");
    Assert(dto.TaskCount == 12, "Task count was not preserved.");
}

static void TaskDtoDefaultsMatchWorkflowDefaults()
{
    var createDto = new TaskCreateDto
    {
        Title = "Build API",
        ProjectId = 1
    };

    Assert(createDto.Status == ProjectTaskStatus.Todo, "New tasks should start in Todo.");
    Assert(createDto.Priority == ProjectPriority.Medium, "New tasks should default to Medium priority.");
}

static void ProjectPlanningValidatorRejectsInvertedDates()
{
    var startDate = new DateTime(2026, 9, 30);
    var deadline = new DateTime(2026, 9, 29);

    Assert(ProjectPlanningValidator.HasInvalidDateRange(startDate, deadline), "Deadline before start date was accepted.");
}

static void ProjectPlanningValidatorAcceptsOpenDates()
{
    Assert(!ProjectPlanningValidator.HasInvalidDateRange(null, DateTime.UtcNow), "Missing start date should be allowed.");
    Assert(!ProjectPlanningValidator.HasInvalidDateRange(DateTime.UtcNow, null), "Missing deadline should be allowed.");
}

static void ProjectPlanningValidatorDetectsPastDueDates()
{
    var today = new DateTime(2026, 9, 22);

    Assert(ProjectPlanningValidator.IsPastDueDate(today.AddDays(-1), today), "Past due date was not detected.");
    Assert(!ProjectPlanningValidator.IsPastDueDate(today, today), "Today should not be treated as past due.");
}

static void LocalFileStorageAcceptsSafeAttachment()
{
    using var workspace = new TempWorkspace();
    var storage = new LocalFileStorageService(workspace);
    var file = CreateFormFile("requirements.pdf", "application/pdf", "hello");

    var result = storage.SaveTaskAttachmentAsync(42, file).GetAwaiter().GetResult();

    Assert(result.FileName == "requirements.pdf", "Original file name was not preserved.");
    Assert(File.Exists(storage.GetTaskAttachmentPath(42, result.StoredFileName)), "Stored file was not created.");
}

static void LocalFileStorageRejectsUnsafeAttachment()
{
    using var workspace = new TempWorkspace();
    var storage = new LocalFileStorageService(workspace);
    var file = CreateFormFile("payload.exe", "application/octet-stream", "hello");

    try
    {
        storage.SaveTaskAttachmentAsync(42, file).GetAwaiter().GetResult();
    }
    catch (InvalidOperationException)
    {
        return;
    }

    throw new InvalidOperationException("Unsafe extension was accepted.");
}

static IFormFile CreateFormFile(string fileName, string contentType, string content)
{
    var bytes = System.Text.Encoding.UTF8.GetBytes(content);
    var stream = new MemoryStream(bytes);
    return new FormFile(stream, 0, bytes.Length, "file", fileName)
    {
        Headers = new HeaderDictionary(),
        ContentType = contentType
    };
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

internal sealed class TempWorkspace : IWebHostEnvironment, IDisposable
{
    public TempWorkspace()
    {
        ContentRootPath = Path.Combine(Path.GetTempPath(), "nexusoxp-tests", Guid.NewGuid().ToString("N"));
        WebRootPath = Path.Combine(ContentRootPath, "wwwroot");
        Directory.CreateDirectory(WebRootPath);
        ContentRootFileProvider = new PhysicalFileProvider(ContentRootPath);
        WebRootFileProvider = new PhysicalFileProvider(WebRootPath);
    }

    public string ApplicationName { get; set; } = "NexusOXP.Tests";

    public IFileProvider ContentRootFileProvider { get; set; }

    public string ContentRootPath { get; set; }

    public string EnvironmentName { get; set; } = "Development";

    public string WebRootPath { get; set; }

    public IFileProvider WebRootFileProvider { get; set; }

    public void Dispose()
    {
        ContentRootFileProvider = new NullFileProvider();
        WebRootFileProvider = new NullFileProvider();

        if (Directory.Exists(ContentRootPath))
        {
            Directory.Delete(ContentRootPath, recursive: true);
        }
    }
}
