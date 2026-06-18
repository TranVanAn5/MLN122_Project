namespace GameServer.Business.Models;

public enum RoomStatus
{
    Lobby,
    Question,
    RoundResults,
    Investment,
    Final,
    Finished
}

public enum PlayerRole
{
    Contestant,
    Investor,
    Finalist
}

public sealed class GameRoom
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string RoomCode { get; init; } = "";
    public string HostName { get; init; } = "Host";
    public RoomStatus Status { get; set; } = RoomStatus.Lobby;
    public int CurrentRound { get; set; }
    public int CurrentQuestionIndex { get; set; } = -1;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? QuestionEndsAt { get; set; }
    public List<Player> Players { get; init; } = [];
    public List<Question> Questions { get; init; } = QuestionBank.Create();
    public List<PlayerAnswer> PlayerAnswers { get; init; } = [];
    public List<Investment> Investments { get; init; } = [];
    public string? WinnerPlayerId { get; set; }
}

public sealed class Player
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = "";
    public int Score { get; set; } = 1000;
    public bool IsEliminated { get; set; }
    public PlayerRole Role { get; set; } = PlayerRole.Contestant;
    public string? ConnectionId { get; set; }
}

public sealed class Question
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Content { get; init; } = "";
    public string OptionA { get; init; } = "";
    public string OptionB { get; init; } = "";
    public string OptionC { get; init; } = "";
    public string OptionD { get; init; } = "";
    public string CorrectAnswer { get; init; } = "A";
    public string Explanation { get; init; } = "";
    public string Difficulty { get; init; } = "Easy";
}

public sealed class PlayerAnswer
{
    public string PlayerId { get; init; } = "";
    public string QuestionId { get; init; } = "";
    public string Answer { get; init; } = "";
    public bool IsCorrect { get; init; }
    public TimeSpan ResponseTime { get; init; }
}

public sealed class Investment
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string InvestorPlayerId { get; init; } = "";
    public string TargetPlayerId { get; init; } = "";
    public int Amount { get; init; }
    public decimal ProfitRate { get; init; } = 0.3m;
    public string Status { get; set; } = "Pending";
}

public sealed class RoomSnapshot
{
    public string RoomCode { get; init; } = "";
    public RoomStatus Status { get; init; }
    public int CurrentRound { get; init; }
    public DateTime? QuestionEndsAt { get; init; }
    public QuestionView? CurrentQuestion { get; init; }
    public IReadOnlyList<PlayerView> Players { get; init; } = [];
    public IReadOnlyList<Investment> Investments { get; init; } = [];
    public string? WinnerPlayerId { get; init; }
}

public sealed class QuestionView
{
    public string Id { get; init; } = "";
    public string Content { get; init; } = "";
    public string OptionA { get; init; } = "";
    public string OptionB { get; init; } = "";
    public string OptionC { get; init; } = "";
    public string OptionD { get; init; } = "";
    public string Difficulty { get; init; } = "";
    public string? CorrectAnswer { get; init; }
    public string? Explanation { get; init; }
}

public sealed class PlayerView
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public int Score { get; init; }
    public bool IsEliminated { get; init; }
    public PlayerRole Role { get; init; }
    public bool HasAnswered { get; init; }
}

public static class QuestionBank
{
    public static List<Question> Create() =>
    [
        new() { Content = "Theo kinh tế chính trị Marxist, giá trị thặng dư chủ yếu được tạo ra từ đâu?", OptionA = "Máy móc tự sinh lợi", OptionB = "Lao động không được trả công đầy đủ", OptionC = "Thuế nhà nước", OptionD = "Quảng cáo", CorrectAnswer = "B", Explanation = "Giá trị thặng dư phát sinh từ phần lao động vượt quá giá trị sức lao động.", Difficulty = "Dễ" },
        new() { Content = "Tiền công danh nghĩa là gì?", OptionA = "Số tiền người lao động nhận được", OptionB = "Lượng hàng hóa mua được", OptionC = "Lợi nhuận doanh nghiệp", OptionD = "Chi phí thuê đất", CorrectAnswer = "A", Explanation = "Tiền công danh nghĩa là khoản tiền ghi nhận bằng tiền tệ.", Difficulty = "Dễ" },
        new() { Content = "Lợi nhuận trong doanh nghiệp thường được hiểu là gì?", OptionA = "Doanh thu trừ chi phí", OptionB = "Tổng tiền lương", OptionC = "Giá bán của một sản phẩm", OptionD = "Thuế VAT", CorrectAnswer = "A", Explanation = "Lợi nhuận là phần chênh lệch còn lại sau khi trừ chi phí.", Difficulty = "Dễ" },
        new() { Content = "Cổ tức là khoản thu nhập gắn với loại vốn nào?", OptionA = "Vốn cổ phần", OptionB = "Vốn vay tiêu dùng", OptionC = "Tiền thuê nhà", OptionD = "Quỹ bảo hiểm", CorrectAnswer = "A", Explanation = "Cổ tức được chia cho cổ đông dựa trên phần vốn cổ phần.", Difficulty = "Trung bình" },
        new() { Content = "Trong kinh tế nền tảng, dữ liệu người dùng có thể trở thành gì?", OptionA = "Một nguồn giá trị kinh tế", OptionB = "Một loại tiền pháp định", OptionC = "Một khoản thuế", OptionD = "Một hợp đồng lao động", CorrectAnswer = "A", Explanation = "Nền tảng số có thể khai thác dữ liệu để tối ưu dịch vụ, quảng cáo và mô hình doanh thu.", Difficulty = "Trung bình" },
        new() { Content = "AI có thể làm thay đổi quan hệ lao động theo cách nào?", OptionA = "Tự động hóa một phần công việc", OptionB = "Xóa bỏ mọi nhu cầu kỹ năng", OptionC = "Loại bỏ hoàn toàn thị trường", OptionD = "Không tạo ảnh hưởng nào", CorrectAnswer = "A", Explanation = "AI thường tự động hóa nhiệm vụ cụ thể và làm thay đổi yêu cầu kỹ năng.", Difficulty = "Trung bình" },
        new() { Content = "Tiền thuê đất trong phân phối thu nhập thường gắn với yếu tố nào?", OptionA = "Quyền sở hữu hoặc kiểm soát đất đai", OptionB = "Số giờ làm thêm", OptionC = "Chi phí quảng cáo", OptionD = "Tiền công tối thiểu", CorrectAnswer = "A", Explanation = "Tiền thuê phản ánh thu nhập từ quyền sử dụng tài sản khan hiếm như đất.", Difficulty = "Khó" },
        new() { Content = "Một nền tảng giao đồ ăn tăng phí với tài xế nhưng giảm hiển thị đơn. Vấn đề nào nổi bật nhất?", OptionA = "Bất cân xứng quyền lực giữa nền tảng và lao động", OptionB = "Cổ tức bắt buộc", OptionC = "Tiền thuê đất nông nghiệp", OptionD = "Lạm phát do vàng", CorrectAnswer = "A", Explanation = "Nền tảng kiểm soát thuật toán phân phối việc làm, tạo lệ thuộc cho người lao động.", Difficulty = "Tình huống" },
        new() { Content = "Nếu năng suất tăng nhưng tiền công thực tế không tăng tương ứng, điều gì có thể tăng?", OptionA = "Phần giá trị thặng dư tương đối", OptionB = "Số ngày trong tuần", OptionC = "Thuế nhập khẩu", OptionD = "Chi phí đi lại bắt buộc", CorrectAnswer = "A", Explanation = "Năng suất cao hơn có thể làm tăng phần giá trị do lao động tạo ra vượt quá tiền công.", Difficulty = "Khó" },
        new() { Content = "Trong MVP game này, người bị loại chuyển sang vai trò nào?", OptionA = "Nhà đầu tư", OptionB = "Host", OptionC = "Trọng tài", OptionD = "Người xem ẩn danh", CorrectAnswer = "A", Explanation = "Người bị loại vẫn tham gia bằng cách đầu tư điểm cho finalist.", Difficulty = "Dễ" }
    ];
}

public static class FinalQuestionBank
{
    public static List<Question> Create() =>
    [
        new() { Content = "Trong chung kết, nếu một nền tảng dùng AI để tăng năng suất nhưng giảm nhu cầu lao động trực tiếp, mâu thuẫn nào trở nên rõ nhất?", OptionA = "Mâu thuẫn giữa lực lượng sản xuất mới và quan hệ phân phối cũ", OptionB = "Mâu thuẫn giữa địa tô và thời tiết", OptionC = "Mâu thuẫn giữa thuế VAT và quảng cáo", OptionD = "Mâu thuẫn giữa tiền mặt và thẻ ngân hàng", CorrectAnswer = "A", Explanation = "AI làm biến đổi lực lượng sản xuất, nhưng quyền sở hữu và phân phối lợi ích có thể vẫn tập trung.", Difficulty = "Chung kết" },
        new() { Content = "Nếu người lao động tạo ra giá trị mới lớn hơn tiền công nhận được, phần chênh lệch đó thường được gọi là gì?", OptionA = "Cổ tức", OptionB = "Giá trị thặng dư", OptionC = "Tiền thuê", OptionD = "Khấu hao", CorrectAnswer = "B", Explanation = "Giá trị thặng dư là phần giá trị vượt quá giá trị sức lao động.", Difficulty = "Chung kết" },
        new() { Content = "Trong nền kinh tế nền tảng, yếu tố nào giúp doanh nghiệp kiểm soát mạnh việc phân phối đơn hàng?", OptionA = "Thuật toán", OptionB = "Số seri tiền giấy", OptionC = "Lịch âm", OptionD = "Mã bưu chính", CorrectAnswer = "A", Explanation = "Thuật toán quyết định hiển thị, phân phối và đánh giá công việc trên nền tảng.", Difficulty = "Chung kết" },
        new() { Content = "Một nhà đầu tư nhận cổ tức vì họ sở hữu điều gì?", OptionA = "Sức lao động", OptionB = "Cổ phần", OptionC = "Hóa đơn bán lẻ", OptionD = "Ca làm việc", CorrectAnswer = "B", Explanation = "Cổ tức gắn với quyền sở hữu cổ phần trong doanh nghiệp.", Difficulty = "Chung kết" },
        new() { Content = "Khi tiền công danh nghĩa tăng nhưng giá cả tăng nhanh hơn, điều gì có thể giảm?", OptionA = "Tiền công thực tế", OptionB = "Số lượng doanh nghiệp", OptionC = "Tên sản phẩm", OptionD = "Mã phòng chơi", CorrectAnswer = "A", Explanation = "Tiền công thực tế đo sức mua, nên có thể giảm nếu lạm phát cao hơn mức tăng tiền lương.", Difficulty = "Chung kết" }
    ];
}
