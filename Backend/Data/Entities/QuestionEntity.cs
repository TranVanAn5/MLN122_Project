namespace GameServer.Data.Entities;

public sealed class QuestionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Content { get; set; } = "";
    public string OptionA { get; set; } = "";
    public string OptionB { get; set; } = "";
    public string OptionC { get; set; } = "";
    public string OptionD { get; set; } = "";
    public string CorrectAnswer { get; set; } = "A";
    public string? Explanation { get; set; }
    public string Difficulty { get; set; } = "Easy";
    public string? Topic { get; set; }

    public List<AnswerEntity> Answers { get; set; } = [];
    public List<GameRoundEntity> Rounds { get; set; } = [];
}
