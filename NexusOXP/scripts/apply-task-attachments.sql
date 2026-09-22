IF OBJECT_ID(N'[dbo].[TaskAttachments]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[TaskAttachments] (
        [Id] int NOT NULL IDENTITY,
        [TaskId] int NOT NULL,
        [UploadedById] nvarchar(450) NOT NULL,
        [FileName] nvarchar(260) NOT NULL,
        [StoredFileName] nvarchar(260) NOT NULL,
        [ContentType] nvarchar(120) NOT NULL,
        [FileSize] bigint NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_TaskAttachments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TaskAttachments_AspNetUsers_UploadedById] FOREIGN KEY ([UploadedById]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TaskAttachments_Tasks_TaskId] FOREIGN KEY ([TaskId]) REFERENCES [dbo].[Tasks] ([Id]) ON DELETE CASCADE
    );

    CREATE INDEX [IX_TaskAttachments_TaskId] ON [dbo].[TaskAttachments] ([TaskId]);
    CREATE INDEX [IX_TaskAttachments_UploadedById] ON [dbo].[TaskAttachments] ([UploadedById]);
END;

IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U') IS NOT NULL
    AND NOT EXISTS (
        SELECT 1
        FROM [dbo].[__EFMigrationsHistory]
        WHERE [MigrationId] = N'20260921120000_AddTaskAttachments'
    )
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260921120000_AddTaskAttachments', N'10.0.11');
END;
