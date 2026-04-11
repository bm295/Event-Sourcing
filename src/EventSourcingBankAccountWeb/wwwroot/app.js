const state = {
  demo: null,
  selectedComponentId: "application-service",
  selectedEventId: null
};

const componentDiagram = document.getElementById("component-diagram");
const componentDetail = document.getElementById("component-detail");
const flowSteps = document.getElementById("flow-steps");
const eventTableBody = document.getElementById("event-table-body");
const eventDetail = document.getElementById("event-detail");
const historyList = document.getElementById("history-list");
const message = document.getElementById("message");
const balance = document.getElementById("balance");
const accountStatus = document.getElementById("account-status");
const lastUpdated = document.getElementById("last-updated");

async function fetchJson(url, options) {
  const response = await fetch(url, {
    headers: { "Content-Type": "application/json" },
    ...options
  });

  const payload = await response.json();
  if (!response.ok) {
    throw payload;
  }

  return payload;
}

async function loadState() {
  const demo = await fetchJson("/api/demo/state");
  state.demo = demo;
  if (!state.selectedEventId && demo.events.length > 0) {
    state.selectedEventId = demo.events[demo.events.length - 1].eventId;
  }
  render();
}

async function executeCommand(commandType) {
  const body = {
    commandType,
    amount: Number(document.getElementById("amount").value),
    ownerName: document.getElementById("owner-name").value
  };

  try {
    const result = await fetchJson("/api/demo/commands", {
      method: "POST",
      body: JSON.stringify(body)
    });

    state.demo = result.state;
    state.selectedEventId = result.newEventIds.at(-1) ?? state.selectedEventId;
    message.textContent = "";
    render();
  } catch (error) {
    state.demo = error.state;
    message.textContent = error.error ?? "Request failed.";
    render();
  }
}

async function replay() {
  state.demo = await fetchJson("/api/demo/replay", { method: "POST" });
  message.textContent = "";
  render();
}

async function resetDemo() {
  state.demo = await fetchJson("/api/demo/reset", { method: "POST" });
  state.selectedEventId = null;
  message.textContent = "";
  render();
}

function render() {
  if (!state.demo) {
    return;
  }

  balance.textContent = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" }).format(state.demo.account.balance);
  accountStatus.textContent = state.demo.account.isOpen ? "Open" : "Closed";
  lastUpdated.textContent = `Last updated ${new Date(state.demo.lastUpdatedUtc).toLocaleTimeString()}`;

  renderComponents();
  renderInspector();
  renderFlow();
  renderEvents();
  renderSelectedEvent();
  renderHistory();
}

function renderComponents() {
  componentDiagram.innerHTML = "";
  state.demo.components.forEach((component) => {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "component";
    if (state.demo.activeComponentIds.includes(component.id)) {
      button.classList.add("active");
    }
    if (state.selectedComponentId === component.id) {
      button.classList.add("selected");
    }
    button.innerHTML = `<strong>${component.displayName}</strong><p>${component.description}</p>`;
    button.addEventListener("click", () => {
      state.selectedComponentId = component.id;
      renderInspector();
      renderComponents();
    });
    componentDiagram.appendChild(button);
  });
}

function renderInspector() {
  const component = state.demo.components.find((item) => item.id === state.selectedComponentId) ?? state.demo.components[0];
  if (!component) {
    componentDetail.innerHTML = "<p>No component selected.</p>";
    return;
  }

  componentDetail.innerHTML = `
    <div>
      <p class="eyebrow">Selected Component</p>
      <h3>${component.displayName}</h3>
      <p>${component.description}</p>
    </div>
    <div>
      <strong>Emits</strong>
      <div class="pill-row">${component.emits.map((item) => `<span class="pill">${item}</span>`).join("")}</div>
    </div>
    <div>
      <strong>Consumes</strong>
      <div class="pill-row">${component.consumes.map((item) => `<span class="pill">${item}</span>`).join("")}</div>
    </div>
  `;
}

function renderFlow() {
  flowSteps.innerHTML = "";
  if (state.demo.latestFlow.length === 0) {
    flowSteps.innerHTML = "<li>No flow executed yet.</li>";
    return;
  }

  state.demo.latestFlow.forEach((step) => {
    const item = document.createElement("li");
    item.innerHTML = `<strong>${step.title}</strong><div>${step.detail}</div>`;
    flowSteps.appendChild(item);
  });
}

function renderEvents() {
  eventTableBody.innerHTML = "";
  state.demo.events.forEach((eventRecord) => {
    const row = document.createElement("tr");
    if (eventRecord.eventId === state.selectedEventId) {
      row.classList.add("selected");
    }
    row.innerHTML = `
      <td>${eventRecord.sequenceNumber}</td>
      <td>${eventRecord.eventType}</td>
      <td><span class="status-pill">${eventRecord.status}</span></td>
      <td>${new Date(eventRecord.createdAtUtc).toLocaleString()}</td>
      <td>${eventRecord.eventId}</td>
    `;
    row.addEventListener("click", () => {
      state.selectedEventId = eventRecord.eventId;
      renderEvents();
      renderSelectedEvent();
    });
    eventTableBody.appendChild(row);
  });
}

function renderSelectedEvent() {
  const eventRecord = state.demo.events.find((item) => item.eventId === state.selectedEventId) ?? state.demo.events.at(-1);
  if (!eventRecord) {
    eventDetail.innerHTML = "<p>No event selected.</p>";
    return;
  }

  eventDetail.innerHTML = `
    <div>
      <p class="eyebrow">Event Record</p>
      <h3>${eventRecord.eventType}</h3>
      <p><strong>Event Id:</strong> ${eventRecord.eventId}</p>
      <p><strong>Stream:</strong> ${eventRecord.streamId}</p>
      <p><strong>Sequence:</strong> ${eventRecord.sequenceNumber}</p>
      <p><strong>Created:</strong> ${new Date(eventRecord.createdAtUtc).toLocaleString()}</p>
    </div>
    <div>
      <strong>Status History</strong>
      <div class="status-row">${eventRecord.statusHistory.map((item) => `<span class="status-pill">${item}</span>`).join("")}</div>
    </div>
    <div>
      <strong>Payload</strong>
      <pre>${escapeHtml(eventRecord.payload)}</pre>
    </div>
  `;
}

function renderHistory() {
  historyList.innerHTML = "";
  if (state.demo.account.history.length === 0) {
    historyList.innerHTML = "<li>No read-model entries yet.</li>";
    return;
  }

  state.demo.account.history.forEach((entry) => {
    const item = document.createElement("li");
    item.innerHTML = `<strong>#${entry.sequenceNumber} ${entry.eventType}</strong><div>${entry.description}</div>`;
    historyList.appendChild(item);
  });
}

function escapeHtml(value) {
  return value
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;");
}

document.querySelectorAll("[data-command]").forEach((button) => {
  button.addEventListener("click", () => executeCommand(button.dataset.command));
});

document.getElementById("replay-button").addEventListener("click", replay);
document.getElementById("reset-button").addEventListener("click", resetDemo);

loadState();
