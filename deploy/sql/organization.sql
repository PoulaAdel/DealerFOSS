IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260724210646_InitialOrganization'
)
BEGIN
    IF SCHEMA_ID(N'org') IS NULL EXEC(N'CREATE SCHEMA [org];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260724210646_InitialOrganization'
)
BEGIN
    CREATE TABLE [org].[DealerOrganizations] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Slug] nvarchar(100) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(120) NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [ModifiedBy] nvarchar(120) NULL,
        [ConcurrencyStamp] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_DealerOrganizations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260724210646_InitialOrganization'
)
BEGIN
    CREATE TABLE [org].[LegalEntities] (
        [Id] uniqueidentifier NOT NULL,
        [OrganizationId] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [RegisteredName] nvarchar(200) NULL,
        [TaxId] nvarchar(50) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(120) NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [ModifiedBy] nvarchar(120) NULL,
        [ConcurrencyStamp] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_LegalEntities] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LegalEntities_DealerOrganizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [org].[DealerOrganizations] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260724210646_InitialOrganization'
)
BEGIN
    CREATE TABLE [org].[Rooftops] (
        [Id] uniqueidentifier NOT NULL,
        [LegalEntityId] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Code] nvarchar(30) NOT NULL,
        [TimeZone] nvarchar(60) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(120) NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [ModifiedBy] nvarchar(120) NULL,
        [ConcurrencyStamp] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_Rooftops] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Rooftops_LegalEntities_LegalEntityId] FOREIGN KEY ([LegalEntityId]) REFERENCES [org].[LegalEntities] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260724210646_InitialOrganization'
)
BEGIN
    CREATE TABLE [org].[Departments] (
        [Id] uniqueidentifier NOT NULL,
        [RooftopId] uniqueidentifier NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [Kind] nvarchar(30) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [CreatedBy] nvarchar(120) NOT NULL,
        [ModifiedAt] datetimeoffset NULL,
        [ModifiedBy] nvarchar(120) NULL,
        [ConcurrencyStamp] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_Departments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Departments_Rooftops_RooftopId] FOREIGN KEY ([RooftopId]) REFERENCES [org].[Rooftops] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260724210646_InitialOrganization'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DealerOrganizations_Slug] ON [org].[DealerOrganizations] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260724210646_InitialOrganization'
)
BEGIN
    CREATE INDEX [IX_Departments_RooftopId] ON [org].[Departments] ([RooftopId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260724210646_InitialOrganization'
)
BEGIN
    CREATE INDEX [IX_LegalEntities_OrganizationId] ON [org].[LegalEntities] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260724210646_InitialOrganization'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Rooftops_Code] ON [org].[Rooftops] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260724210646_InitialOrganization'
)
BEGIN
    CREATE INDEX [IX_Rooftops_LegalEntityId] ON [org].[Rooftops] ([LegalEntityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260724210646_InitialOrganization'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260724210646_InitialOrganization', N'10.0.10');
END;

COMMIT;
GO

