namespace GameServer.Data.Entities;

public sealed class AnswerEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid QuestionId { get; set; }
    public string Label { get; set; } = "";
    public string Content { get; set; } = "";
    public bool IsCorrect { get; set; }

    public QuestionEntity Question { get; set; } = null!;
}
