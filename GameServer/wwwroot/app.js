const state = {
  room: null,
  playerId: localStorage.getItem("mln-player-id") || "",
  isHost: localStorage.getItem("mln-is-host") === "true",
  name: localStorage.getItem("mln-name") || "",
  message: "",
  eventSource: null,
  streamRoomCode: "",
  selectedEliminations: new Set()
};

const statusLabels = ["Phong cho", "Dang hoi", "Ket qua vong", "Dau tu", "Chung ket", "Ket thuc"];
const roleLabels = ["Thi sinh", "Nha dau tu", "Chung ket"];

const root = document.getElementById("root");
const roomBadge = document.getElementById("roomBadge");

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

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

function setRoom(room) {
  state.room = room;
  roomBadge.textContent = room ? `ROOM ${room.roomCode}` : "";
  roomBadge.classList.toggle("hidden", !room);
  startRoomStream();
  render();
}

function startRoomStream() {
  if (!state.room) {
    return;
  }

  if (state.eventSource && state.streamRoomCode === state.room.roomCode) {
    return;
  }

  if (state.eventSource) {
    state.eventSource.close();
  }

  const source = new EventSource(`/api/rooms/${state.room.roomCode}/events`);
  source.onmessage = (event) => {
    state.room = JSON.parse(event.data);
    state.message = "";
    render();
  };
  source.onerror = () => {
    state.message = "Dang thu ket noi lai voi phong.";
    render();
  };
  state.eventSource = source;
  state.streamRoomCode = state.room.roomCode;
}

function me() {
  return state.room?.players?.find((player) => player.id === state.playerId);
}

async function createRoom() {
  const name = document.getElementById("name").value.trim() || "Host";
  const room = await api("/api/rooms", {
    method: "POST",
    body: JSON.stringify({ hostName: name })
  });

  localStorage.setItem("mln-name", name);
  localStorage.setItem("mln-player-id", "host");
  localStorage.setItem("mln-is-host", "true");
  state.name = name;
  state.playerId = "host";
  state.isHost = true;
  state.message = `Da tao phong ${room.roomCode}`;
  setRoom(room);
}

async function joinRoom() {
  const name = document.getElementById("name").value.trim() || "Player";
  const code = document.getElementById("roomCode").value.trim().toUpperCase();
  try {
    const joined = await api(`/api/rooms/${code}/players`, {
      method: "POST",
      body: JSON.stringify({ name })
    });

    localStorage.setItem("mln-name", name);
    localStorage.setItem("mln-player-id", joined.playerId);
    localStorage.setItem("mln-is-host", "false");
    state.name = name;
    state.playerId = joined.playerId;
    state.isHost = false;
    state.message = "";
    setRoom(joined.room);
  } catch {
    state.message = "Khong vao duoc phong. Kiem tra ma phong hoac phong da bat dau.";
    render();
  }
}

async function action(path, body) {
  if (!state.room) {
    return;
  }

  try {
    const room = await api(`/api/rooms/${state.room.roomCode}/${path}`, {
      method: "POST",
      body: body ? JSON.stringify(body) : "{}"
    });
    setRoom(room);
  } catch (error) {
    state.message = error.message || "Khong gui duoc lenh.";
    render();
  }
}

function leaveRoom() {
  localStorage.removeItem("mln-player-id");
  localStorage.removeItem("mln-is-host");
  state.room = null;
  state.playerId = "";
  state.isHost = false;
  state.streamRoomCode = "";
  if (state.eventSource) {
    state.eventSource.close();
    state.eventSource = null;
  }
  roomBadge.classList.add("hidden");
  render();
}

function render() {
  if (!state.room) {
    root.innerHTML = renderHome();
    bindHome();
    return;
  }

  root.innerHTML = renderGame();
  bindGame();
}

function renderHome() {
  return `
    <section class="home">
      <div class="home-panel">
        <div class="card home-intro">
          <p class="eyebrow">Live classroom challenge</p>
          <h2>San dau tri thuc co diem so va dau tu.</h2>
          <p>Host dieu khien tung vong, nguoi choi tra loi tren thiet bi rieng, nguoi bi loai van tiep tuc tham gia bang vai tro nha dau tu.</p>
          <div class="home-stats">
            <div class="home-stat"><strong>10</strong><span>nguoi/phong</span></div>
            <div class="home-stat"><strong>30s</strong><span>moi cau hoi</span></div>
            <div class="home-stat"><strong>SQL</strong><span>luu trang thai</span></div>
          </div>
        </div>
        <div class="card home-form">
          <div class="field">
            <label for="name">Ten hien thi</label>
            <input id="name" value="${escapeHtml(state.name)}" placeholder="Vi du: Minh" />
          </div>
          <button id="createRoom" class="good">Tao phong moi</button>
          <div class="surface stack">
            <div class="field">
              <label for="roomCode">Ma phong</label>
              <div class="join-grid">
                <input id="roomCode" placeholder="VD: ABC12" />
                <button id="joinRoom">Vao phong</button>
              </div>
            </div>
          </div>
          ${state.message ? `<div class="message">${escapeHtml(state.message)}</div>` : ""}
        </div>
      </div>
    </section>
  `;
}

function bindHome() {
  document.getElementById("createRoom").addEventListener("click", createRoom);
  document.getElementById("joinRoom").addEventListener("click", joinRoom);
}

function renderGame() {
  const player = me();
  pruneSelectedEliminations();
  return `
    <section class="layout">
      <div class="stack">
        ${renderRoomStage(player)}
        ${renderMetrics(player)}
        ${renderMain(player)}
        ${state.message ? `<div class="message">${escapeHtml(state.message)}</div>` : ""}
      </div>
      <aside class="stack">
        ${state.isHost ? renderHostControls() : ""}
        ${renderLeaderboard(player)}
        <button id="leaveRoom" class="secondary">Thoat phong</button>
      </aside>
    </section>
  `;
}

function pruneSelectedEliminations() {
  if (!state.room) return;
  const selectableIds = new Set(
    state.room.players
      .filter((player) => !player.isEliminated && player.role !== 1 && state.room.status === 2)
      .map((player) => player.id)
  );
  state.selectedEliminations = new Set([...state.selectedEliminations].filter((id) => selectableIds.has(id)));
}

function renderRoomStage(player) {
  const activeCount = state.room.players.filter((item) => !item.isEliminated && item.role !== 1).length;
  return `
    <div class="card room-stage">
      <div>
        <span class="chip good">${statusLabels[state.room.status]}</span>
        <span class="chip">Room ${escapeHtml(state.room.roomCode)}</span>
      </div>
      <div>
        <span class="chip">${state.room.players.length}/10 nguoi</span>
        <span class="chip ${player ? roleClass(player.role) : "good"}">${player ? roleLabels[player.role] : "Host"}</span>
        <span class="chip">${activeCount} dang thi</span>
      </div>
    </div>
  `;
}

function renderMetrics(player) {
  return `
    <div class="metrics">
      <div class="card metric accent"><span>Trang thai</span><strong>${statusLabels[state.room.status]}</strong></div>
      <div class="card metric"><span>Vong hien tai</span><strong>${state.room.currentRound || "Lobby"}</strong></div>
      <div class="card metric"><span>Diem cua ban</span><strong>${player ? player.score : "Host"}</strong></div>
    </div>
  `;
}

function renderMain(player) {
  if (state.room.status === 0) return renderLobby();
  if (state.room.status === 1 || state.room.status === 2) return renderQuestion(player);
  if (state.room.status === 3) return renderInvest(player);
  if (state.room.status === 4) return renderFinal();
  return renderResult();
}

function renderLobby() {
  return `
    <div class="card">
      <div class="section-title">
        <h2>Phong cho</h2>
        <span class="chip good">San sang</span>
      </div>
      <p class="muted">${state.room.players.length}/10 nguoi da tham gia. Host co the bat dau khi lop da on dinh.</p>
      <div class="players">
        ${state.room.players.length ? state.room.players.map(renderPlayerCard).join("") : `<div class="empty">Chua co nguoi choi nao.</div>`}
      </div>
    </div>
  `;
}

function renderPlayerCard(player) {
  return `
    <div class="player">
      <div class="avatar">${escapeHtml(initials(player.name))}</div>
      <div class="player-body">
        <div class="player-name">${escapeHtml(player.name)}</div>
        <div class="player-meta">${roleLabels[player.role]} · ${player.score} diem${player.hasAnswered ? " · da tra loi" : ""}</div>
      </div>
    </div>
  `;
}

function renderQuestion(player) {
  const question = state.room.currentQuestion || {};
  const canAnswer = player && player.role !== 1 && !player.isEliminated && (state.room.status === 1 || state.room.status === 4) && !player.hasAnswered && !question.correctAnswer;
  const options = [
    ["A", question.optionA],
    ["B", question.optionB],
    ["C", question.optionC],
    ["D", question.optionD]
  ];

  return `
    <div class="card question-card">
      <div class="section-title">
        <h2>Cau hoi vong ${state.room.currentRound}</h2>
        <span class="chip warn">${escapeHtml(question.difficulty || "Dang cho")}</span>
      </div>
      <div class="question-copy">${escapeHtml(question.content)}</div>
      <div class="options">
        ${options.map(([key, text]) => `
          <button class="option answer" data-answer="${key}" ${canAnswer ? "" : "disabled"}>
            <span class="option-key">${key}</span>${escapeHtml(text)}
          </button>
        `).join("")}
      </div>
      ${player?.hasAnswered ? `<div class="answer-note">Da gui dap an. Cho host khoa vong de xem ket qua.</div>` : ""}
      ${question.correctAnswer ? `<div class="message"><strong>Dap an dung: ${question.correctAnswer}</strong><br>${escapeHtml(question.explanation)}</div>` : ""}
    </div>
  `;
}

function renderInvest(player) {
  const finalists = state.room.players.filter((item) => item.role === 2);
  const canInvest = player?.role === 1;
  const previousQuestion = state.room.currentQuestion;
  const previousResult = previousQuestion?.correctAnswer
    ? `<div class="message">Vong chung ket truoc da qua. Dap an dung: <strong>${escapeHtml(previousQuestion.correctAnswer)}</strong>. Nha dau tu nhan lai von + 30%, nguoi chung ket nhan 200 diem + 70% tong dau tu.</div>`
    : "";
  return `
    <div class="card stack">
      <div class="section-title">
        <h2>Dau tu theo vong</h2>
        <span class="chip warn">Toi da 100%</span>
      </div>
      ${previousResult}
      <p class="muted">Moi luot dau tu chi tinh cho cau hoi chung ket sap toi. Nha dau tu co the dat toi da toan bo diem hien co, nhung khong duoc vuot qua so diem dang co.</p>
      <div class="finalists">
        ${finalists.map(renderPlayerCard).join("")}
      </div>
      <select id="targetPlayer">
        ${finalists.map((item) => `<option value="${item.id}">${escapeHtml(item.name)} - ${item.score} diem</option>`).join("")}
      </select>
      ${canInvest ? `
        <div class="row">
          <input id="investAmount" type="number" min="0" max="${player.score}" value="${Math.min(100, player.score)}" />
          <button id="investButton" class="good">Dau tu</button>
        </div>
        <p class="muted">Toi da: ${player.score} diem</p>
      ` : ""}
      ${renderInvestments()}
    </div>
  `;
}

function renderFinal() {
  const finalists = state.room.players.filter((player) => player.role === 2);
  if (state.room.currentQuestion) {
    return `
      <div class="stack">
        <div class="card room-stage">
          <span class="chip good">Bo cau hoi chung ket</span>
          <span class="chip">Nguoi choi cuoi cung: ${escapeHtml(finalists[0]?.name || "Chua co")}</span>
        </div>
        ${renderQuestion(me())}
      </div>
    `;
  }

  return `
    <div class="card">
      <div class="section-title">
        <h2>Chung ket</h2>
        <span class="chip good">Bo cau hoi moi</span>
      </div>
      <p class="muted">Chung ket solo bat dau khi chi con 1 nguoi choi. Host bam cau hoi chung ket de dung bo cau hoi rieng.</p>
      <div class="finalists">
        ${finalists.map((player) => `
          <div class="player">
              <div class="avatar">${escapeHtml(initials(player.name))}</div>
            <div class="player-body">
              <div class="player-name">${escapeHtml(player.name)}</div>
              <div class="player-meta">${player.score} diem</div>
              ${state.isHost ? `
                <div class="row">
                  <button class="finish" data-winner="${player.id}">Ket thuc va cong bo</button>
                  ${finalists.length > 1 ? `<button class="finish-loser danger" data-loser="${player.id}">Chon thua</button>` : ""}
                </div>
              ` : ""}
            </div>
          </div>
        `).join("")}
      </div>
    </div>
  `;
}

function renderResult() {
  const winner = state.room.players.find((player) => player.id === state.room.winnerPlayerId);
  return `
    <div class="card stack">
      <p class="eyebrow">Ket qua cuoi</p>
      <h2 class="question-title">${escapeHtml(winner?.name || "Chung ket that bai")}</h2>
      ${winner ? `<div class="message">Nguoi chien thang: <strong>${escapeHtml(winner.name)}</strong></div>` : `<div class="message">Nguoi choi chung ket tra loi sai. Tro choi ket thuc va cac khoan dau tu duoc tinh la thua.</div>`}
      ${renderInvestments()}
    </div>
  `;
}

function renderInvestments() {
  const names = Object.fromEntries(state.room.players.map((player) => [player.id, player.name]));
  if (!state.room.investments.length) {
    return `<div class="empty">Chua co luot dau tu.</div>`;
  }

  return `
    <div class="stack">
      ${state.room.investments.map((item) => `
        <div class="investment-item">
          <strong>${escapeHtml(names[item.investorPlayerId])}</strong> dau tu <strong>${item.amount}</strong> diem cho <strong>${escapeHtml(names[item.targetPlayerId])}</strong>
          <span class="chip ${item.status === "Won" ? "good" : item.status === "Lost" ? "danger" : "warn"}">${escapeHtml(item.status)}</span>
        </div>
      `).join("")}
    </div>
  `;
}

function renderHostControls() {
  const finalists = state.room.players.filter((player) => player.role === 2);
  const selectedCount = state.selectedEliminations.size;
  const hasFinalQuestion = state.room.status === 4 && Boolean(state.room.currentQuestion);
  const finalAnswerRevealed = hasFinalQuestion && Boolean(state.room.currentQuestion.correctAnswer);
  return `
    <div class="card stack">
      <div class="section-title"><h2>Host controls</h2></div>
      <div class="host-actions">
        <button id="startQuestion" ${state.room.status === 1 || state.room.status === 4 || state.room.status === 5 ? "disabled" : ""}>Bat dau cau hoi</button>
        <button id="startFinalQuestion" class="good" ${state.room.status !== 4 || !finalists.length || (hasFinalQuestion && !finalAnswerRevealed) ? "disabled" : ""}>Cau hoi chung ket</button>
        <button id="lockRound" class="warn" ${!((state.room.status === 1) || (hasFinalQuestion && !finalAnswerRevealed)) ? "disabled" : ""}>Khoa dap an</button>
        <button id="eliminate" class="danger" ${state.room.status !== 2 || selectedCount === 0 ? "disabled" : ""}>Loai ${selectedCount || ""} nguoi da chon</button>
        <button id="startFinal" class="good" ${state.room.status !== 3 || finalists.length !== 1 ? "disabled" : ""}>Mo chung ket solo</button>
      </div>
      ${state.room.status === 2 ? `<p class="muted">Tick nguoi can loai trong bang xep hang. He thong khong tu chon theo diem.</p>` : ""}
      ${state.room.status === 3 ? `<p class="muted">Chi mo chung ket khi con dung 1 nguoi choi cuoi cung.</p>` : ""}
    </div>
  `;
}

function renderLeaderboard(currentPlayer) {
  return `
    <div class="card stack">
      <div class="section-title">
        <h2>Bang xep hang</h2>
        <span class="chip">${state.room.players.length} nguoi</span>
      </div>
      ${state.room.players.map((player, index) => `
        <div class="player rank ${player.id === currentPlayer?.id ? "me" : ""}">
          ${renderEliminationPicker(player, index)}
          <div class="player-body">
            <div class="player-name">${escapeHtml(player.name)}</div>
            <div class="player-meta">${roleLabels[player.role]}${player.hasAnswered ? " · da tra loi" : ""}</div>
          </div>
          <div class="score">${player.score}</div>
        </div>
      `).join("")}
    </div>
  `;
}

function renderEliminationPicker(player, index) {
  const canPick = state.isHost && state.room.status === 2 && !player.isEliminated && player.role !== 1;
  if (!canPick) {
    return `<div class="avatar">${index + 1}</div>`;
  }

  return `
    <label class="pick-eliminate" title="Chon de loai">
      <input class="eliminate-choice" type="checkbox" value="${player.id}" ${state.selectedEliminations.has(player.id) ? "checked" : ""} />
      <span>${index + 1}</span>
    </label>
  `;
}

function bindGame() {
  document.getElementById("leaveRoom").addEventListener("click", leaveRoom);
  document.getElementById("startQuestion")?.addEventListener("click", () => action("start-question"));
  document.getElementById("startFinalQuestion")?.addEventListener("click", () => action("start-final-question"));
  document.getElementById("lockRound")?.addEventListener("click", () => action("lock-round"));
  document.getElementById("eliminate")?.addEventListener("click", () => {
    action("eliminate", { playerIds: [...state.selectedEliminations] });
    state.selectedEliminations.clear();
  });
  document.getElementById("startFinal")?.addEventListener("click", () => action("start-final"));
  document.querySelectorAll(".eliminate-choice").forEach((checkbox) => {
    checkbox.addEventListener("change", () => {
      if (checkbox.checked) {
        state.selectedEliminations.add(checkbox.value);
      } else {
        state.selectedEliminations.delete(checkbox.value);
      }
      render();
    });
  });
  document.querySelectorAll(".answer").forEach((button) => {
    button.addEventListener("click", () => action("answers", { playerId: state.playerId, answer: button.dataset.answer }));
  });
  document.getElementById("investButton")?.addEventListener("click", () => {
    action("investments", {
      investorPlayerId: state.playerId,
      targetPlayerId: document.getElementById("targetPlayer").value,
      amount: Number(document.getElementById("investAmount").value)
    });
  });
  document.querySelectorAll(".finish").forEach((button) => {
    button.addEventListener("click", () => action("finish", { winnerPlayerId: button.dataset.winner }));
  });
  document.querySelectorAll(".finish-loser").forEach((button) => {
    button.addEventListener("click", () => {
      const finalists = state.room.players.filter((player) => player.role === 2);
      const winner = finalists.find((player) => player.id !== button.dataset.loser);
      if (winner) {
        action("finish", { winnerPlayerId: winner.id });
      }
    });
  });
}

render();
