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
CREATE TABLE [GameRooms] (
    [Id] uniqueidentifier NOT NULL,
    [RoomCode] nvarchar(12) NOT NULL,
    [HostName] nvarchar(120) NOT NULL,
    [Status] nvarchar(40) NOT NULL,
    [CurrentRound] int NOT NULL,
    [CurrentQuestionIndex] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    [QuestionEndsAt] datetime2 NULL,
    [WinnerPlayerId] uniqueidentifier NULL,
    CONSTRAINT [PK_GameRooms] PRIMARY KEY ([Id])
);

CREATE TABLE [Questions] (
    [Id] uniqueidentifier NOT NULL,
    [Content] nvarchar(1000) NOT NULL,
    [OptionA] nvarchar(500) NOT NULL,
    [OptionB] nvarchar(500) NOT NULL,
    [OptionC] nvarchar(500) NOT NULL,
    [OptionD] nvarchar(500) NOT NULL,
    [CorrectAnswer] nvarchar(1) NOT NULL,
    [Explanation] nvarchar(1000) NULL,
    [Difficulty] nvarchar(40) NOT NULL,
    [Topic] nvarchar(120) NULL,
    CONSTRAINT [PK_Questions] PRIMARY KEY ([Id])
);

CREATE TABLE [Users] (
    [Id] uniqueidentifier NOT NULL,
    [DisplayName] nvarchar(120) NOT NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
);

CREATE TABLE [Answers] (
    [Id] uniqueidentifier NOT NULL,
    [QuestionId] uniqueidentifier NOT NULL,
    [Label] nvarchar(1) NOT NULL,
    [Content] nvarchar(500) NOT NULL,
    [IsCorrect] bit NOT NULL,
    CONSTRAINT [PK_Answers] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Answers_Questions_QuestionId] FOREIGN KEY ([QuestionId]) REFERENCES [Questions] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [GameRounds] (
    [Id] uniqueidentifier NOT NULL,
    [RoomId] uniqueidentifier NOT NULL,
    [QuestionId] uniqueidentifier NOT NULL,
    [RoundNumber] int NOT NULL,
    [Status] nvarchar(40) NOT NULL,
    [StartedAt] datetime2 NOT NULL,
    [EndedAt] datetime2 NULL,
    CONSTRAINT [PK_GameRounds] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_GameRounds_GameRooms_RoomId] FOREIGN KEY ([RoomId]) REFERENCES [GameRooms] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_GameRounds_Questions_QuestionId] FOREIGN KEY ([QuestionId]) REFERENCES [Questions] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [Players] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(120) NOT NULL,
    [RoomId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NULL,
    [Score] int NOT NULL,
    [IsEliminated] bit NOT NULL,
    [Role] nvarchar(40) NOT NULL,
    [ConnectionId] nvarchar(160) NULL,
    CONSTRAINT [PK_Players] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Players_GameRooms_RoomId] FOREIGN KEY ([RoomId]) REFERENCES [GameRooms] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Players_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
);

CREATE TABLE [Investments] (
    [Id] uniqueidentifier NOT NULL,
    [RoomId] uniqueidentifier NOT NULL,
    [InvestorPlayerId] uniqueidentifier NOT NULL,
    [TargetPlayerId] uniqueidentifier NOT NULL,
    [Amount] int NOT NULL,
    [ProfitRate] decimal(5,2) NOT NULL,
    [Status] nvarchar(40) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Investments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Investments_GameRooms_RoomId] FOREIGN KEY ([RoomId]) REFERENCES [GameRooms] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Investments_Players_InvestorPlayerId] FOREIGN KEY ([InvestorPlayerId]) REFERENCES [Players] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Investments_Players_TargetPlayerId] FOREIGN KEY ([TargetPlayerId]) REFERENCES [Players] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [PlayerAnswers] (
    [Id] uniqueidentifier NOT NULL,
    [RoundId] uniqueidentifier NOT NULL,
    [PlayerId] uniqueidentifier NOT NULL,
    [Answer] nvarchar(1) NOT NULL,
    [IsCorrect] bit NOT NULL,
    [ResponseTime] time NOT NULL,
    [AnsweredAt] datetime2 NOT NULL,
    CONSTRAINT [PK_PlayerAnswers] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PlayerAnswers_GameRounds_RoundId] FOREIGN KEY ([RoundId]) REFERENCES [GameRounds] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_PlayerAnswers_Players_PlayerId] FOREIGN KEY ([PlayerId]) REFERENCES [Players] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [Scores] (
    [Id] uniqueidentifier NOT NULL,
    [PlayerId] uniqueidentifier NOT NULL,
    [RoundId] uniqueidentifier NULL,
    [Delta] int NOT NULL,
    [TotalAfter] int NOT NULL,
    [Reason] nvarchar(120) NOT NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_Scores] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Scores_GameRounds_RoundId] FOREIGN KEY ([RoundId]) REFERENCES [GameRounds] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_Scores_Players_PlayerId] FOREIGN KEY ([PlayerId]) REFERENCES [Players] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_Answers_QuestionId] ON [Answers] ([QuestionId]);

CREATE UNIQUE INDEX [IX_GameRooms_RoomCode] ON [GameRooms] ([RoomCode]);

CREATE INDEX [IX_GameRounds_QuestionId] ON [GameRounds] ([QuestionId]);

CREATE INDEX [IX_GameRounds_RoomId] ON [GameRounds] ([RoomId]);

CREATE INDEX [IX_Investments_InvestorPlayerId] ON [Investments] ([InvestorPlayerId]);

CREATE INDEX [IX_Investments_RoomId] ON [Investments] ([RoomId]);

CREATE INDEX [IX_Investments_TargetPlayerId] ON [Investments] ([TargetPlayerId]);

CREATE INDEX [IX_PlayerAnswers_PlayerId] ON [PlayerAnswers] ([PlayerId]);

CREATE UNIQUE INDEX [IX_PlayerAnswers_RoundId_PlayerId] ON [PlayerAnswers] ([RoundId], [PlayerId]);

CREATE INDEX [IX_Players_RoomId] ON [Players] ([RoomId]);

CREATE INDEX [IX_Players_UserId] ON [Players] ([UserId]);

CREATE INDEX [IX_Scores_PlayerId] ON [Scores] ([PlayerId]);

CREATE INDEX [IX_Scores_RoundId] ON [Scores] ([RoundId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260617162425_InitialCreate', N'9.0.6');

COMMIT;
GO

