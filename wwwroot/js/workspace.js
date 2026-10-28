"use strict";

const config = window.collabSpaceConfig;
const note = document.getElementById("noteContent");
const saveStatus = document.getElementById("saveStatus");
const tokenInput = document.querySelector(
    '#noteForm input[name="__RequestVerificationToken"]'
);

if (!config || !note || !saveStatus || !tokenInput) {
    throw new Error("CollabSpace workspace configuration is incomplete.");
}

const token = tokenInput.value;

const connection = new signalR.HubConnectionBuilder()
    .withUrl("/collaborationHub")
    .withAutomaticReconnect([0, 2000, 5000, 10000, 20000])
    .build();

let saveTimer = null;
let reconnectTimer = null;
let saveVersion = 0;
let lastSavedVersion = 0;
let retryDelay = 1000;
let saveInProgress = false;
let drawing = false;
let currentStroke = null;
let strokes = [];
let connectionReady = false;

const canvas = document.getElementById("drawingCanvas");
const ctx = canvas.getContext("2d");
const colorPicker = document.getElementById("penColor");

function hasPendingNoteChanges() {
    return saveVersion !== lastSavedVersion;
}

function scheduleSave(delay = 500) {
    clearTimeout(saveTimer);
    saveTimer = setTimeout(saveNote, delay);
}

function drawStroke(stroke) {
    if (!stroke || !Array.isArray(stroke.points) ||
        stroke.points.length === 0) {
        return;
    }

    ctx.strokeStyle = stroke.color || "#202020";
    ctx.fillStyle = stroke.color || "#202020";
    ctx.lineWidth = Number(stroke.width) || 3;
    ctx.lineCap = "round";
    ctx.lineJoin = "round";

    const points = stroke.points;

    if (points.length === 1) {
        ctx.beginPath();
        ctx.arc(
            points[0].x,
            points[0].y,
            ctx.lineWidth / 2,
            0,
            Math.PI * 2
        );
        ctx.fill();
        return;
    }

    ctx.beginPath();
    ctx.moveTo(points[0].x, points[0].y);

    for (let i = 1; i < points.length; i++) {
        ctx.lineTo(points[i].x, points[i].y);
    }

    ctx.stroke();
}

function redrawCanvas() {
    const rect = canvas.getBoundingClientRect();

    ctx.clearRect(0, 0, rect.width, rect.height);

    for (const stroke of strokes) {
        drawStroke(stroke);
    }
}

function resizeCanvas() {
    const rect = canvas.getBoundingClientRect();
    const dpr = window.devicePixelRatio || 1;

    canvas.width = Math.round(rect.width * dpr);
    canvas.height = Math.round(rect.height * dpr);

    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    redrawCanvas();
}

function pointFromEvent(event) {
    const rect = canvas.getBoundingClientRect();

    return {
        x: event.clientX - rect.left,
        y: event.clientY - rect.top
    };
}

function parseStrokes(savedStrokes) {
    return savedStrokes.map(data => JSON.parse(data));
}

connection.on("NoteUpdated", content => {
    // Do not discard local edits that have not been acknowledged.
    if (hasPendingNoteChanges()) {
        saveStatus.textContent =
            "Remote update received; your local edits are pending";
        return;
    }

    note.value = content;
    saveVersion++;
    lastSavedVersion = saveVersion;

    clearTimeout(saveTimer);
    saveStatus.textContent = "Updated by another participant";
});

connection.on("StrokeAdded", strokeData => {
    try {
        const stroke = JSON.parse(strokeData);
        strokes.push(stroke);
        drawStroke(stroke);
    } catch (error) {
        console.error("Could not render remote stroke:", error);
    }
});

connection.on("CanvasCleared", () => {
    strokes = [];
    redrawCanvas();
});

connection.onreconnecting(() => {
    connectionReady = false;
    saveStatus.textContent = hasPendingNoteChanges()
        ? "Disconnected — local note edits are pending"
        : "Reconnecting…";
});

connection.onreconnected(async () => {
    try {
        await joinAndLoad();

        if (hasPendingNoteChanges()) {
            saveStatus.textContent = "Reconnected — saving pending edits…";
            scheduleSave(0);
        } else {
            saveStatus.textContent = "Reconnected";
        }
    } catch (error) {
        console.error("Workspace recovery failed:", error);
        connectionReady = false;
        saveStatus.textContent = "Reconnected, but workspace recovery failed";
    }
});

connection.onclose(() => {
    connectionReady = false;

    saveStatus.textContent = hasPendingNoteChanges()
        ? "Disconnected — local note edits are pending"
        : "Disconnected — retrying connection…";

    scheduleReconnect();
});

async function joinAndLoad() {
    connectionReady = false;

    await connection.invoke("JoinRoom", config.roomId);

    const savedStrokes = await connection.invoke(
        "GetStrokes",
        config.roomId
    );

    // If drawing changed locally while loading, don't discard it.
    const localStrokes = strokes;
    const remoteStrokes = parseStrokes(savedStrokes);

    strokes = [...remoteStrokes, ...localStrokes];
    redrawCanvas();

    connectionReady = true;
}

function scheduleReconnect() {
    if (reconnectTimer !== null) {
        return;
    }

    reconnectTimer = setTimeout(async () => {
        reconnectTimer = null;
        await connect();
    }, 3000);
}

async function connect() {
    if (connection.state !== signalR.HubConnectionState.Disconnected) {
        return;
    }

    try {
        await connection.start();
        await joinAndLoad();

        if (hasPendingNoteChanges()) {
            saveStatus.textContent = "Connected — saving pending edits…";
            scheduleSave(0);
        } else {
            saveStatus.textContent = "Connected";
        }
    } catch (error) {
        console.error("Connection or room initialization failed:", error);

        connectionReady = false;
        saveStatus.textContent = hasPendingNoteChanges()
            ? "Connection failed — local note edits are pending"
            : "Connection failed — retrying…";

        scheduleReconnect();
    }
}

note.addEventListener("input", () => {
    saveVersion++;
    saveStatus.textContent = "Unsaved changes…";
    retryDelay = 1000;
    scheduleSave();
});

async function saveNote() {
    if (!hasPendingNoteChanges() || saveInProgress) {
        return;
    }

    if (!connectionReady ||
        connection.state !== signalR.HubConnectionState.Connected) {
        saveStatus.textContent = "Waiting for connection — edits retained";
        return;
    }

    const version = saveVersion;
    const content = note.value;

    saveInProgress = true;
    saveStatus.textContent = "Saving…";

    try {
        await connection.invoke("UpdateNote", config.roomId, content);

        lastSavedVersion = Math.max(lastSavedVersion, version);
        retryDelay = 1000;

        if (saveVersion !== version) {
            saveStatus.textContent = "Saving newer edits…";
            scheduleSave(0);
        } else {
            saveStatus.textContent = "All changes saved";
        }
    } catch (error) {
        console.error("Note save failed:", error);

        saveStatus.textContent = "Save failed — retrying automatically";

        if (connection.state === signalR.HubConnectionState.Connected) {
            scheduleSave(retryDelay);
            retryDelay = Math.min(retryDelay * 2, 30000);
        }
    } finally {
        saveInProgress = false;

        // An edit may have arrived while the previous save was running.
        if (hasPendingNoteChanges() &&
            connectionReady &&
            connection.state === signalR.HubConnectionState.Connected &&
            saveTimer === null) {
            scheduleSave(0);
        }
    }
}

document.getElementById("copyCode").addEventListener("click", async () => {
    const code = document.getElementById("accessCode").textContent.trim();

    try {
        await navigator.clipboard.writeText(code);
        document.getElementById("copyCode").textContent = "Copied!";
    } catch {
        window.prompt("Copy this access code:", code);
    }
});

canvas.addEventListener("pointerdown", event => {
    if (!connectionReady) {
        saveStatus.textContent = "Waiting for connection before drawing";
        return;
    }

    drawing = true;

    currentStroke = {
        color: colorPicker.value,
        width: 3,
        points: [pointFromEvent(event)]
    };

    canvas.setPointerCapture(event.pointerId);
});

canvas.addEventListener("pointermove", event => {
    if (!drawing || !currentStroke) {
        return;
    }

    currentStroke.points.push(pointFromEvent(event));

    redrawCanvas();
    drawStroke(currentStroke);
});

async function stopDrawing() {
    if (!drawing || !currentStroke) {
        return;
    }

    drawing = false;

    const completedStroke = currentStroke;
    currentStroke = null;

    strokes.push(completedStroke);
    redrawCanvas();

    if (!connectionReady ||
        connection.state !== signalR.HubConnectionState.Connected) {
        saveStatus.textContent =
            "Stroke is local only — connection unavailable";
        return;
    }

    try {
        await connection.invoke(
            "AddStroke",
            config.roomId,
            JSON.stringify(completedStroke)
        );
    } catch (error) {
        console.error("Stroke save failed:", error);
        saveStatus.textContent =
            "Stroke may not be saved — redraw after reconnecting";
    }
}

canvas.addEventListener("pointerup", stopDrawing);
canvas.addEventListener("pointercancel", stopDrawing);

canvas.addEventListener("lostpointercapture", () => {
    if (drawing) {
        stopDrawing();
    }
});

document.getElementById("clearCanvas").addEventListener("click", async () => {
    if (!connectionReady) {
        saveStatus.textContent = "Connect before clearing the canvas";
        return;
    }

    try {
        await connection.invoke("ClearCanvas", config.roomId);
    } catch (error) {
        console.error("Canvas clear failed:", error);
        saveStatus.textContent = "Could not clear canvas";
    }
});

resizeCanvas();
window.addEventListener("resize", resizeCanvas);
connect();
// ==========================================
// Sticky-note canvas — Chunk 1
// State and rendering foundation
// ==========================================

const stickyBoard = document.getElementById("stickyNotesCanvas");
const addCanvasNoteButton = document.getElementById("addCanvasNote");
const canvasNoteStatus = document.getElementById("canvasNoteStatus");

const canvasNotes = new Map();
const canvasNoteSaveTimers = new Map();
const canvasNoteSaveVersions = new Map();

let canvasNotesReady = false;

function setCanvasNoteStatus(message) {
	if (canvasNoteStatus) {
		canvasNoteStatus.textContent = message;
	}
}

function normalizeCanvasNote(note) {
	if (!note || !Number.isInteger(Number(note.id))) {
		throw new Error("Received an invalid canvas note.");
	}

	return {
		id: Number(note.id),
		roomId: Number(note.roomId),
		content: String(note.content ?? ""),
		x: Number(note.x),
		y: Number(note.y),
		width: Number(note.width),
		height: Number(note.height),
		color: String(note.color ?? "#fff3a3"),
		createdAt: note.createdAt,
		updatedAt: note.updatedAt
	};


}

function renderCanvasNote(noteData) {
	if (!stickyBoard) {
		return;
	}

	const note = normalizeCanvasNote(noteData);
	let element = stickyBoard.querySelector(
		`[data-canvas-note-id="${note.id}"]`
	);

	if (!element) {
		element = document.createElement("article");
		element.className = "sticky-note";
		element.dataset.canvasNoteId = String(note.id);

		const header = document.createElement("div");
		header.className = "sticky-note-header";

		const title = document.createElement("span");
		title.textContent = "Note";

		const tools = document.createElement("div");
		tools.className = "sticky-note-tools";

		const color = document.createElement("input");
		color.type = "color";
		color.className = "sticky-note-color";
		color.value = /^#[0-9a-fA-F]{6}$/.test(note.color)
			? note.color
			: "#fff3a3";
		color.title = "Change note color";
		color.setAttribute("aria-label", "Change note color");

		const remove = document.createElement("button");
		remove.type = "button";
		remove.className = "btn btn-sm btn-outline-danger";
		remove.textContent = "×";
		remove.title = "Delete note";
		remove.setAttribute("aria-label", "Delete note");

		const content = document.createElement("textarea");
		content.className = "sticky-note-content";
		content.maxLength = 500000;
		content.placeholder = "Write an idea…";
		content.setAttribute("aria-label", "Sticky note content");

		const resize = document.createElement("div");
		resize.className = "sticky-note-resize";
		resize.setAttribute("aria-label", "Resize note");

		tools.append(color, remove);
		header.append(title, tools);
		element.append(header, content, resize);
		stickyBoard.append(element);
	}

	element.style.left = `${note.x}px`;
	element.style.top = `${note.y}px`;
	element.style.width = `${note.width}px`;
	element.style.height = `${note.height}px`;
	element.style.backgroundColor = note.color;

	const content = element.querySelector(".sticky-note-content");
	const color = element.querySelector(".sticky-note-color");

	// Avoid overwriting text while the user is editing this note.
	if (document.activeElement !== content) {
		content.value = note.content;
	}

	if (color && color.value !== note.color) {
		color.value = /^#[0-9a-fA-F]{6}$/.test(note.color)
			? note.color
			: "#fff3a3";
	}

	canvasNotes.set(note.id, note);


}

// ==========================================
// Sticky-note canvas — Chunk 2
// Dragging, resizing, interactions, and events
// ==========================================

function removeCanvasNoteDOM(noteId) {
    const numericId = Number(noteId);
    const element = stickyBoard?.querySelector(
        `[data-canvas-note-id="${numericId}"]`
    );
    if (element) {
        element.remove();
    }
    canvasNotes.delete(numericId);
    canvasNoteSaveTimers.delete(numericId);
    canvasNoteSaveVersions.delete(numericId);
}

function getCanvasNoteData(noteId) {
    const numericId = Number(noteId);
    const element = stickyBoard?.querySelector(
        `[data-canvas-note-id="${numericId}"]`
    );
    if (!element) {
        return null;
    }

    const cached = canvasNotes.get(numericId) || {
        id: numericId,
        roomId: config.roomId,
        content: "",
        x: 100,
        y: 100,
        width: 320,
        height: 220,
        color: "#fff3a3"
    };

    return {
        ...cached,
        content: element.querySelector(".sticky-note-content")?.value ?? cached.content,
        color: element.querySelector(".sticky-note-color")?.value ?? cached.color,
        x: parseFloat(element.style.left) || cached.x,
        y: parseFloat(element.style.top) || cached.y,
        width: parseFloat(element.style.width) || cached.width,
        height: parseFloat(element.style.height) || cached.height
    };
}

function scheduleCanvasNoteSave(noteId, delay = 500) {
    const numericId = Number(noteId);
    const existingTimer = canvasNoteSaveTimers.get(numericId);
    if (existingTimer) {
        clearTimeout(existingTimer);
    }

    const currentVersion = (canvasNoteSaveVersions.get(numericId) || 0) + 1;
    canvasNoteSaveVersions.set(numericId, currentVersion);

    const timer = setTimeout(async () => {
        await saveCanvasNote(numericId, currentVersion);
    }, delay);

    canvasNoteSaveTimers.set(numericId, timer);
}

async function saveCanvasNote(noteId, version) {
    const numericId = Number(noteId);
    if (!connectionReady || connection.state !== signalR.HubConnectionState.Connected) {
        setCanvasNoteStatus("Waiting for connection to save sticky note…");
        return;
    }

    const note = getCanvasNoteData(numericId);
    if (!note) {
        return;
    }

    try {
        setCanvasNoteStatus("Saving sticky note…");
        await connection.invoke(
            "UpdateCanvasNote",
            config.roomId,
            note.id,
            note.content,
            note.x,
            note.y,
            note.width,
            note.height,
            note.color
        );

        if (canvasNoteSaveVersions.get(numericId) === version) {
            setCanvasNoteStatus("All sticky notes saved");
        }
    } catch (error) {
        console.error("Failed to save sticky note:", error);
        setCanvasNoteStatus("Failed to save sticky note");
    }
}

// Wire up interactions on sticky note elements
if (stickyBoard) {
    stickyBoard.addEventListener("pointerdown", event => {
        const noteEl = event.target.closest(".sticky-note");
        if (!noteEl) return;

        const noteId = Number(noteEl.dataset.canvasNoteId);

        // Handle delete button
        if (event.target.closest(".btn-outline-danger")) {
            event.stopPropagation();
            deleteCanvasNoteServer(noteId);
            return;
        }

        // Handle color picker change
        if (event.target.classList.contains("sticky-note-color")) {
            return; // let native color picker work
        }

        // Handle textarea input
        if (event.target.classList.contains("sticky-note-content")) {
            noteEl.style.zIndex = String(Date.now());
            return;
        }

        // Handle resizing
        const isResizeHandle = event.target.classList.contains("sticky-note-resize");
        const startX = event.clientX;
        const startY = event.clientY;
        const startLeft = parseFloat(noteEl.style.left) || 100;
        const startTop = parseFloat(noteEl.style.top) || 100;
        const startWidth = parseFloat(noteEl.style.width) || 320;
        const startHeight = parseFloat(noteEl.style.height) || 220;

        noteEl.style.zIndex = String(Date.now());

        function onPointerMove(e) {
            const dx = e.clientX - startX;
            const dy = e.clientY - startY;

            if (isResizeHandle) {
                const newWidth = Math.max(200, startWidth + dx);
                const newHeight = Math.max(150, startHeight + dy);
                noteEl.style.width = `${newWidth}px`;
                noteEl.style.height = `${newHeight}px`;
            } else {
                const newLeft = Math.max(0, startLeft + dx);
                const newTop = Math.max(0, startTop + dy);
                noteEl.style.left = `${newLeft}px`;
                noteEl.style.top = `${newTop}px`;
            }
        }

        function onPointerUp() {
            window.removeEventListener("pointermove", onPointerMove);
            window.removeEventListener("pointerup", onPointerUp);
            scheduleCanvasNoteSave(noteId, 0);
        }

        window.addEventListener("pointermove", onPointerMove);
        window.addEventListener("pointerup", onPointerUp);
    });

    stickyBoard.addEventListener("input", event => {
        if (event.target.classList.contains("sticky-note-content")) {
            const noteEl = event.target.closest(".sticky-note");
            if (noteEl) {
                const noteId = Number(noteEl.dataset.canvasNoteId);
                scheduleCanvasNoteSave(noteId, 400);
            }
        }
    });

    stickyBoard.addEventListener("change", event => {
        if (event.target.classList.contains("sticky-note-color")) {
            const noteEl = event.target.closest(".sticky-note");
            if (noteEl) {
                const noteId = Number(noteEl.dataset.canvasNoteId);
                noteEl.style.backgroundColor = event.target.value;
                scheduleCanvasNoteSave(noteId, 0);
            }
        }
    });
}

async function deleteCanvasNoteServer(noteId) {
    const numericId = Number(noteId);
    removeCanvasNoteDOM(numericId);
    try {
        setCanvasNoteStatus("Deleting note…");
        await connection.invoke("DeleteCanvasNote", config.roomId, numericId);
        setCanvasNoteStatus("Note deleted");
    } catch (error) {
        console.error("Failed to delete note on server:", error);
        setCanvasNoteStatus("Could not delete note");
    }
}

if (addCanvasNoteButton) {
    addCanvasNoteButton.addEventListener("click", async () => {
        if (!connectionReady || connection.state !== signalR.HubConnectionState.Connected) {
            setCanvasNoteStatus("Connect before adding sticky notes");
            return;
        }

        try {
            setCanvasNoteStatus("Adding sticky note…");
            const newNote = await connection.invoke(
                "CreateCanvasNote",
                config.roomId,
                ""
            );
            renderCanvasNote(newNote);
            setCanvasNoteStatus("Sticky note added");
        } catch (error) {
            console.error("Failed to create sticky note:", error);
            setCanvasNoteStatus("Could not create sticky note");
        }
    });
}

// Register Hub listeners for canvas notes
connection.on("CanvasNoteCreated", note => {
    renderCanvasNote(note);
});

connection.on("CanvasNoteUpdated", note => {
    const numericId = Number(note.id);
    const element = stickyBoard?.querySelector(`[data-canvas-note-id="${numericId}"]`);
    // If the user is actively typing in this note, do not overwrite their content immediately
    if (element && document.activeElement === element.querySelector(".sticky-note-content")) {
        return;
    }
    renderCanvasNote(note);
});

connection.on("CanvasNoteDeleted", noteId => {
    removeCanvasNoteDOM(noteId);
});

// Load existing canvas notes during joinAndLoad / reconnection
async function loadCanvasNotes() {
    try {
        const notes = await connection.invoke("GetCanvasNotes", config.roomId);
        if (Array.isArray(notes)) {
            for (const note of notes) {
                renderCanvasNote(note);
            }
        }
        canvasNotesReady = true;
    } catch (error) {
        console.error("Failed to load canvas notes:", error);
        setCanvasNoteStatus("Failed to load sticky notes");
    }
}

// Helper for escaping text in notes
function escapeHtml(text) {
    const map = {
        '&': '&amp;',
        '<': '&lt;',
        '>': '&gt;',
        '"': '&quot;',
        "'": '&#039;'
    };
    return String(text).replace(/[&<>"']/g, m => map[m]);
}

// Ensure loadCanvasNotes is called upon connection/reconnection
const originalJoinAndLoad = joinAndLoad;
window.joinAndLoad = async function() {
    await originalJoinAndLoad();
    await loadCanvasNotes();
};
