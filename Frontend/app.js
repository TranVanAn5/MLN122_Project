const { useEffect, useMemo, useRef, useState } = React;
const h = React.createElement;

const statusLabels = ["Phòng chờ", "Đang hỏi", "Kết quả vòng", "Đầu tư", "Chung kết", "Kết thúc"];
const roleLabels = ["Thí sinh", "Nhà đầu tư", "Chung kết"];

function initials(name) {
  return String(name || "?").trim().slice(0, 2).toUpperCase();
}

function roleClass(role) {
  if (role === 1) return "warn";
  if (role === 2) return "good";
  return "";
}

async function api(path, options = {}) {
  const response = await fetch(path, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      ...(options.headers || {})
    }
  });

  if (!response.ok) {
    throw new Error(await response.text());
  }

  return response.json();
}

function App() {
  const [room, setRoom] = useState(null);
  const [playerId, setPlayerId] = useState(localStorage.getItem("mln-player-id") || "");
  const [isHost, setIsHost] = useState(localStorage.getItem("mln-is-host") === "true");
  const [name, setName] = useState(localStorage.getItem("mln-name") || "");
  const [roomCode, setRoomCode] = useState("");
  const [message, setMessage] = useState("");
  const [selectedEliminations, setSelectedEliminations] = useState(() => new Set());
  const streamRoomCode = useRef("");
  const eventSource = useRef(null);

  const player = useMemo(
    () => room?.players?.find((item) => item.id === playerId),
    [room, playerId]
  );

  useEffect(() => {
    if (!room) {
      if (eventSource.current) {
        eventSource.current.close();
        eventSource.current = null;
      }
      streamRoomCode.current = "";
      return;
    }

    if (eventSource.current && streamRoomCode.current === room.roomCode) {
      return;
    }

    if (eventSource.current) {
      eventSource.current.close();
    }

    const source = new EventSource(`/api/rooms/${room.roomCode}/events`);
    source.onmessage = (event) => {
      setRoom(JSON.parse(event.data));
      setMessage("");
    };
    source.onerror = () => setMessage("Đang thử kết nối lại với phòng.");
    eventSource.current = source;
    streamRoomCode.current = room.roomCode;

    return () => source.close();
  }, [room?.roomCode]);

  useEffect(() => {
    if (!room) {
      return;
    }

    const selectableIds = new Set(
      room.players
        .filter((item) => !item.isEliminated && item.role !== 1 && room.status === 2)
        .map((item) => item.id)
    );

    setSelectedEliminations((current) => {
      const next = new Set([...current].filter((id) => selectableIds.has(id)));
      return next.size === current.size ? current : next;
    });
  }, [room]);

  async function createRoom() {
    const displayName = name.trim() || "Chủ phòng";
    const createdRoom = await api("/api/rooms", {
      method: "POST",
      body: JSON.stringify({ hostName: displayName })
    });

    localStorage.setItem("mln-name", displayName);
    localStorage.setItem("mln-player-id", "host");
    localStorage.setItem("mln-is-host", "true");
    setName(displayName);
    setPlayerId("host");
    setIsHost(true);
    setMessage(`Đã tạo phòng ${createdRoom.roomCode}`);
    setRoom(createdRoom);
  }

  async function joinRoom() {
    const displayName = name.trim() || "Người chơi";
    const code = roomCode.trim().toUpperCase();
    try {
      const joined = await api(`/api/rooms/${code}/players`, {
        method: "POST",
        body: JSON.stringify({ name: displayName })
      });

      localStorage.setItem("mln-name", displayName);
      localStorage.setItem("mln-player-id", joined.playerId);
      localStorage.setItem("mln-is-host", "false");
      setName(displayName);
      setPlayerId(joined.playerId);
      setIsHost(false);
      setMessage("");
      setRoom(joined.room);
    } catch {
      setMessage("Không vào được phòng. Kiểm tra mã phòng hoặc phòng đã bắt đầu.");
    }
  }

  async function action(path, body) {
    if (!room) {
      return;
    }

    try {
      const updatedRoom = await api(`/api/rooms/${room.roomCode}/${path}`, {
        method: "POST",
        body: body ? JSON.stringify(body) : "{}"
      });
      setRoom(updatedRoom);
    } catch (error) {
      setMessage(error.message || "Không gửi được lệnh.");
    }
  }

  function leaveRoom() {
    localStorage.removeItem("mln-player-id");
    localStorage.removeItem("mln-is-host");
    setRoom(null);
    setPlayerId("");
    setIsHost(false);
    setSelectedEliminations(new Set());
    setMessage("");
  }

  function toggleElimination(id) {
    setSelectedEliminations((current) => {
      const next = new Set(current);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }

  return h("main", { className: "app" },
    h(Header, { room }),
    !room
      ? h(Home, { name, setName, roomCode, setRoomCode, message, createRoom, joinRoom })
      : h(Game, {
          room,
          player,
          playerId,
          isHost,
          message,
          selectedEliminations,
          action,
          leaveRoom,
          toggleElimination,
          clearEliminations: () => setSelectedEliminations(new Set())
        })
  );
}

function Header({ room }) {
  return h("header", { className: "topbar stage-enter" },
    h("div", { className: "brand" },
      h("div", { className: "brand-mark" }, "MLN"),
      h("div", null,
        h("h1", null, "Trò chơi MLN trực tiếp"),
        h("p", null, "Quiz đấu trường, đầu tư điểm và chung kết trực tiếp.")
      )
    ),
    h("div", { className: `room-badge ${room ? "" : "hidden"}` }, room ? `PHÒNG ${room.roomCode}` : "")
  );
}

function Home({ name, setName, roomCode, setRoomCode, message, createRoom, joinRoom }) {
  return h("section", { className: "home stage-enter" },
    h("div", { className: "home-panel" },
      h("div", { className: "home-intro stage-card" },
        h("div", { className: "home-copy" },
          h("p", { className: "eyebrow" }, "Thử thách lớp học trực tiếp"),
          h("h2", null, "Sàn đấu tri thức có điểm số và đầu tư."),
          h("p", null, "Chủ phòng điều khiển từng vòng, người chơi trả lời trên thiết bị riêng, người bị loại vẫn tiếp tục tham gia bằng vai trò nhà đầu tư."),
          h("div", { className: "home-stats" },
            h(Stat, { value: "10", label: "người/phòng" }),
            h(Stat, { value: "30s", label: "mỗi câu hỏi" }),
            h(Stat, { value: "SQL", label: "lưu trạng thái" })
          )
        ),
        h(ArenaVisual)
      ),
      h("div", { className: "card home-form" },
        h("div", { className: "form-heading" },
          h("span", { className: "chip good" }, "Nhanh"),
          h("h2", null, "Vào sàn đấu")
        ),
        h("div", { className: "field" },
          h("label", { htmlFor: "name" }, "Tên hiển thị"),
          h("input", {
            id: "name",
            value: name,
            placeholder: "Ví dụ: Minh",
            onChange: (event) => setName(event.target.value)
          })
        ),
        h("button", { className: "good pulse-action", onClick: createRoom }, "Tạo phòng mới"),
        h("div", { className: "surface stack" },
          h("div", { className: "field" },
            h("label", { htmlFor: "roomCode" }, "Mã phòng"),
            h("div", { className: "join-grid" },
              h("input", {
                id: "roomCode",
                value: roomCode,
                placeholder: "VD: ABC12",
                onChange: (event) => setRoomCode(event.target.value.toUpperCase()),
                onKeyDown: (event) => event.key === "Enter" && joinRoom()
              }),
              h("button", { onClick: joinRoom }, "Vào phòng")
            )
          )
        ),
        message ? h("div", { className: "message toast-in" }, message) : null
      )
    )
  );
}

function Stat({ value, label }) {
  return h("div", { className: "home-stat lift-in" }, h("strong", null, value), h("span", null, label));
}

function ArenaVisual() {
  return h("div", { className: "arena-visual", "aria-hidden": "true" },
    h("div", { className: "arena-ring ring-one" }),
    h("div", { className: "arena-ring ring-two" }),
    h("div", { className: "arena-core" },
      h("span", null, "A"),
      h("span", null, "B"),
      h("span", null, "C"),
      h("span", null, "D")
    ),
    h("div", { className: "arena-card card-one" }, "Chủ phòng"),
    h("div", { className: "arena-card card-two" }, "Câu hỏi"),
    h("div", { className: "arena-card card-three" }, "Đầu tư")
  );
}

function Game(props) {
  const { room, player, isHost, message, leaveRoom } = props;

  return h("section", { className: "layout stage-enter" },
    h("div", { className: "stack" },
      h(RoomStage, { room, player }),
      h(Metrics, { room, player }),
      h(MainPanel, props),
      message ? h("div", { className: "message toast-in" }, message) : null
    ),
    h("aside", { className: "stack sidebar-enter" },
      isHost ? h(HostControls, props) : null,
      h(Leaderboard, props),
      h("button", { className: "secondary", onClick: leaveRoom }, "Thoát phòng")
    )
  );
}

function RoomStage({ room, player }) {
  const activeCount = room.players.filter((item) => !item.isEliminated && item.role !== 1).length;
  return h("div", { className: "card room-stage live-strip" },
    h("div", { className: "room-stage-main" },
      h("div", null,
        h("span", { className: "chip good" }, statusLabels[room.status]),
        h("span", { className: "chip" }, `Phòng ${room.roomCode}`)
      ),
      h(StatusFlow, { current: room.status })
    ),
    h("div", { className: "room-stage-meta" },
      h("span", { className: "chip" }, `${room.players.length}/10 người`),
      h("span", { className: `chip ${player ? roleClass(player.role) : "good"}` }, player ? roleLabels[player.role] : "Chủ phòng"),
      h("span", { className: "chip" }, `${activeCount} đang thi`)
    )
  );
}

function StatusFlow({ current }) {
  return h("div", { className: "status-flow" },
    statusLabels.map((label, index) =>
      h("span", {
        key: label,
        className: index <= current ? "active" : ""
      }, label)
    )
  );
}

function Metrics({ room, player }) {
  return h("div", { className: "metrics" },
    h(Metric, { accent: true, label: "Trạng thái", value: statusLabels[room.status] }),
    h(Metric, { label: "Vòng hiện tại", value: room.currentRound || "Phòng chờ" }),
    h(Metric, { label: "Điểm của bạn", value: player ? player.score : "Chủ phòng" })
  );
}

function Metric({ accent, label, value }) {
  return h("div", { className: `card metric metric-pop ${accent ? "accent" : ""}` },
    h("div", { className: "metric-dot" }),
    h("span", null, label),
    h("strong", null, value)
  );
}

function MainPanel(props) {
  const { room } = props;
  if (room.status === 0) return h(Lobby, props);
  if (room.status === 1 || room.status === 2) return h(Question, props);
  if (room.status === 3) return h(Invest, props);
  if (room.status === 4) return h(Final, props);
  return h(Result, props);
}

function Lobby({ room }) {
  return h("div", { className: "card panel-swap" },
    h("div", { className: "section-title" },
      h("div", null, h("p", { className: "eyebrow" }, "Phòng chờ"), h("h2", null, "Phòng chờ")),
      h("span", { className: "chip good" }, "Sẵn sàng")
    ),
    h("p", { className: "muted" }, `${room.players.length}/10 người đã tham gia. Chủ phòng có thể bắt đầu khi lớp đã ổn định.`),
    h("div", { className: "players" },
      room.players.length
        ? room.players.map((item) => h(PlayerCard, { key: item.id, player: item }))
        : h("div", { className: "empty" }, "Chưa có người chơi nào.")
    )
  );
}

function PlayerCard({ player }) {
  return h("div", { className: "player lift-in" },
    h("div", { className: "avatar" }, initials(player.name)),
    h("div", { className: "player-body" },
      h("div", { className: "player-name" }, player.name),
      h("div", { className: "player-meta" }, `${roleLabels[player.role]} - ${player.score} điểm${player.hasAnswered ? " - đã trả lời" : ""}`)
    )
  );
}

function Question({ room, player, playerId, action }) {
  const question = room.currentQuestion || {};
  const canAnswer = player && player.role !== 1 && !player.isEliminated && (room.status === 1 || room.status === 4) && !player.hasAnswered && !question.correctAnswer;
  const options = [
    ["A", question.optionA],
    ["B", question.optionB],
    ["C", question.optionC],
    ["D", question.optionD]
  ];

  return h("div", { className: "card question-card panel-swap" },
    h("div", { className: "section-title" },
      h("div", null, h("p", { className: "eyebrow" }, "Đang thi đấu"), h("h2", null, `Câu hỏi vòng ${room.currentRound}`)),
      h("span", { className: "chip warn" }, question.difficulty || "Đang chờ")
    ),
    h("div", { className: "question-copy" }, question.content),
    h("div", { className: "options" },
      options.map(([key, text]) =>
        h("button", {
          key,
          className: "option answer option-reveal",
          disabled: !canAnswer,
          onClick: () => action("answers", { playerId, answer: key })
        }, h("span", { className: "option-key" }, key), text)
      )
    ),
    player?.hasAnswered ? h("div", { className: "answer-note toast-in" }, "Đã gửi đáp án. Chờ host khóa vòng để xem kết quả.") : null,
    question.correctAnswer ? h("div", { className: "message toast-in" },
      h("strong", null, `Đáp án đúng: ${question.correctAnswer}`),
      h("br"),
      question.explanation
    ) : null
  );
}

function Invest({ room, player, playerId, action }) {
  const [targetPlayerId, setTargetPlayerId] = useState("");
  const [amount, setAmount] = useState(Math.min(100, player?.score || 0));
  const finalists = room.players.filter((item) => item.role === 2);
  const canInvest = player?.role === 1;
  const previousQuestion = room.currentQuestion;

  useEffect(() => {
    setTargetPlayerId(finalists[0]?.id || "");
  }, [room.roomCode, finalists.map((item) => item.id).join("|")]);

  return h("div", { className: "card stack panel-swap" },
    h("div", { className: "section-title" }, h("h2", null, "Đầu tư theo vòng"), h("span", { className: "chip warn" }, "Tối đa 100%")),
    previousQuestion?.correctAnswer ? h("div", { className: "message toast-in" }, `Vòng chung kết trước đã qua. Đáp án đúng: ${previousQuestion.correctAnswer}. Nhà đầu tư nhận lại vốn + 30%.`) : null,
    h("p", { className: "muted" }, "Mỗi lượt đầu tư chỉ tính cho câu hỏi chung kết sắp tới. Nhà đầu tư có thể đặt tối đa toàn bộ điểm hiện có."),
    h("div", { className: "finalists" }, finalists.map((item) => h(PlayerCard, { key: item.id, player: item }))),
    h("select", { value: targetPlayerId, onChange: (event) => setTargetPlayerId(event.target.value) },
      finalists.map((item) => h("option", { key: item.id, value: item.id }, `${item.name} - ${item.score} điểm`))
    ),
    canInvest ? h("div", { className: "row" },
      h("input", {
        type: "number",
        min: "0",
        max: player.score,
        value: amount,
        onChange: (event) => setAmount(Number(event.target.value))
      }),
      h("button", {
        className: "good pulse-action",
        onClick: () => action("investments", { investorPlayerId: playerId, targetPlayerId, amount })
      }, "Đầu tư")
    ) : null,
    canInvest ? h("p", { className: "muted" }, `Tối đa: ${player.score} điểm`) : null,
    h(Investments, { room })
  );
}

function Final(props) {
  const { room, isHost, action } = props;
  const finalists = room.players.filter((item) => item.role === 2);

  if (room.currentQuestion) {
    return h("div", { className: "stack panel-swap" },
      h("div", { className: "card room-stage" },
        h("span", { className: "chip good" }, "Bộ câu hỏi chung kết"),
        h("span", { className: "chip" }, `Người chơi cuối cùng: ${finalists[0]?.name || "Chưa có"}`)
      ),
      h(Question, props)
    );
  }

  return h("div", { className: "card panel-swap" },
    h("div", { className: "section-title" }, h("h2", null, "Chung kết"), h("span", { className: "chip good" }, "Bộ câu hỏi mới")),
    h("p", { className: "muted" }, "Chung kết solo bắt đầu khi chỉ còn 1 người chơi. Chủ phòng bấm câu hỏi chung kết để dùng bộ câu hỏi riêng."),
    h("div", { className: "finalists" },
      finalists.map((item) => h("div", { key: item.id, className: "player lift-in" },
        h("div", { className: "avatar" }, initials(item.name)),
        h("div", { className: "player-body" },
          h("div", { className: "player-name" }, item.name),
          h("div", { className: "player-meta" }, `${item.score} điểm`),
          isHost ? h("div", { className: "row" },
            h("button", { onClick: () => action("finish", { winnerPlayerId: item.id }) }, "Kết thúc và công bố"),
            finalists.length > 1 ? h("button", {
              className: "danger",
              onClick: () => {
                const winner = finalists.find((player) => player.id !== item.id);
                if (winner) action("finish", { winnerPlayerId: winner.id });
              }
            }, "Chọn thua") : null
          ) : null
        )
      ))
    )
  );
}

function Result({ room }) {
  const winner = room.players.find((item) => item.id === room.winnerPlayerId);
  return h("div", { className: "card stack panel-swap result-panel" },
    h("p", { className: "eyebrow" }, "Kết quả cuối"),
    h("h2", { className: "question-title" }, winner?.name || "Chung kết thất bại"),
    winner
      ? h("div", { className: "message toast-in" }, h("strong", null, `Người chiến thắng: ${winner.name}`))
      : h("div", { className: "message toast-in" }, "Người chơi chung kết trả lời sai. Trò chơi kết thúc và các khoản đầu tư được tính là thua."),
    h(Investments, { room })
  );
}

function Investments({ room }) {
  const names = Object.fromEntries(room.players.map((item) => [item.id, item.name]));
  if (!room.investments.length) {
    return h("div", { className: "empty" }, "Chưa có lượt đầu tư.");
  }

  return h("div", { className: "stack" },
    room.investments.map((item) => h("div", { key: item.id, className: "investment-item lift-in" },
      h("strong", null, names[item.investorPlayerId]),
      ` đầu tư ${item.amount} điểm cho `,
      h("strong", null, names[item.targetPlayerId]),
      h("span", { className: `chip ${item.status === "Won" ? "good" : item.status === "Lost" ? "danger" : "warn"}` }, item.status)
    ))
  );
}

function HostControls({ room, selectedEliminations, action, clearEliminations }) {
  const finalists = room.players.filter((item) => item.role === 2);
  const hasFinalQuestion = room.status === 4 && Boolean(room.currentQuestion);
  const finalAnswerRevealed = hasFinalQuestion && Boolean(room.currentQuestion.correctAnswer);
  const selectedCount = selectedEliminations.size;

  return h("div", { className: "card stack host-panel" },
    h("div", { className: "section-title" }, h("div", null, h("p", { className: "eyebrow" }, "Điều phối"), h("h2", null, "Điều khiển chủ phòng"))),
    h("div", { className: "host-actions" },
      h("button", { disabled: room.status === 1 || room.status === 4 || room.status === 5, onClick: () => action("start-question") }, "Bắt đầu câu hỏi"),
      h("button", { className: "good", disabled: room.status !== 4 || !finalists.length || (hasFinalQuestion && !finalAnswerRevealed), onClick: () => action("start-final-question") }, "Câu hỏi chung kết"),
      h("button", { className: "warn", disabled: !((room.status === 1) || (hasFinalQuestion && !finalAnswerRevealed)), onClick: () => action("lock-round") }, "Khóa đáp án"),
      h("button", {
        className: "danger",
        disabled: room.status !== 2 || selectedCount === 0,
        onClick: () => {
          action("eliminate", { playerIds: [...selectedEliminations] });
          clearEliminations();
        }
      }, `Loại ${selectedCount || ""} người đã chọn`),
      h("button", { className: "good", disabled: room.status !== 3 || finalists.length !== 1, onClick: () => action("start-final") }, "Mở chung kết solo")
    ),
    room.status === 2 ? h("p", { className: "muted" }, "Tick người cần loại trong bảng xếp hạng. Hệ thống không tự chọn theo điểm.") : null,
    room.status === 3 ? h("p", { className: "muted" }, "Chỉ mở chung kết khi còn đúng 1 người chơi cuối cùng.") : null
  );
}

function Leaderboard({ room, player, isHost, selectedEliminations, toggleElimination }) {
  return h("div", { className: "card stack leaderboard-panel" },
    h("div", { className: "section-title" },
      h("div", null, h("p", { className: "eyebrow" }, "Trực tiếp"), h("h2", null, "Bảng xếp hạng")),
      h("span", { className: "chip" }, `${room.players.length} người`)
    ),
    room.players.map((item, index) =>
      h("div", { key: item.id, className: `player rank lift-in ${item.id === player?.id ? "me" : ""}` },
        h(EliminationPicker, { room, player: item, index, isHost, selectedEliminations, toggleElimination }),
        h("div", { className: "player-body" },
          h("div", { className: "player-name" }, item.name),
          h("div", { className: "player-meta" }, `${roleLabels[item.role]}${item.hasAnswered ? " - đã trả lời" : ""}`)
        ),
        h("div", { className: "score" }, item.score)
      )
    )
  );
}

function EliminationPicker({ room, player, index, isHost, selectedEliminations, toggleElimination }) {
  const canPick = isHost && room.status === 2 && !player.isEliminated && player.role !== 1;
  if (!canPick) {
    return h("div", { className: "avatar" }, index + 1);
  }

  return h("label", { className: "pick-eliminate", title: "Chọn để loại" },
    h("input", {
      type: "checkbox",
      checked: selectedEliminations.has(player.id),
      onChange: () => toggleElimination(player.id)
    }),
    h("span", null, index + 1)
  );
}

ReactDOM.createRoot(document.getElementById("root")).render(h(App));
