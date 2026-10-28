"use strict";

const config = window.collabSpaceConfig;
const note = document.getElementById("noteContent");
const saveStatus = document.getElementById("saveStatus");
const token = document.querySelector(
    '#noteForm input[name="__RequestVerificationToken"]'
).value;

const connection = new signalR.HubConnectionBuilder()
    .withUrl("/collaborationHub")
    .withAutomaticReconnect()
    .build();

let saveTimer;
let saveVersion = 0;
let drawing = false;
let currentStroke = null;
let strokes = [];
let connectionReady = false;

const canvas = document.getElementById("drawingCanvas");
const ctx = canvas.getContext("2d");
const colorPicker = document.getElementById("penColor");

function drawStroke(stroke) {
    if (!stroke || !Array.isArray(stroke.points) ||
        stroke.points.length === 0) return;

    ctx.strokeStyle = stroke.color || "#202020";
    ctx.fillStyle = stroke.color || "#202020";
    ctx.lineWidth = Number(stroke.width) || 3;
    ctx.lineCap = "round";
    ctx.lineJoin = "round";

    const points = stroke.points;

    if (points.length === 1) {
        ctx.beginPath();
        ctx.arc(points[0].x, points[0].y, ctx.lineWidth / 2, 0, Math.PI * 2);
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

connection.on("NoteUpdated", content => {
    note.value = content;
    saveVersion++;
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
    saveStatus.textContent = "Reconnecting…";
});

connection.onreconnected(async () => {
    try {
        await joinAndLoad();
        saveStatus.textContent = "Reconnected";
    } catch (error) {
        console.error(error);
        saveStatus.textContent = "Reconnect failed";
    }
});

connection.onclose(() => {
    connectionReady = false;
    saveStatus.textContent = "Disconnected — refresh to reconnect";
});

async function joinAndLoad() {
    await connection.invoke("JoinRoom", config.roomId);

    const savedStrokes = await connection.invoke(
        "GetStrokes", config.roomId
    );

    strokes = savedStrokes.map(data => JSON.parse(data));
    redrawCanvas();
    connectionReady = true;
}

async function connect() {
    try {
        await connection.start();
        await joinAndLoad();
        saveStatus.textContent = "Connected";
    } catch (error) {
        console.error(error);
        saveStatus.textContent = "Connection failed — refresh to retry";
    }
}

note.addEventListener("input", () => {
    saveVersion++;
    saveStatus.textContent = "Unsaved changes…";
    clearTimeout(saveTimer);
    saveTimer = setTimeout(saveNote, 500);
});

async function saveNote() {
    if (!connectionReady ||
        connection.state !== signalR.HubConnectionState.Connected) {
        saveStatus.textContent = "Waiting for connection…";
        return;
    }

    const version = saveVersion;
    saveStatus.textContent = "Saving…";

    try {
        await connection.invoke("UpdateNote", config.roomId, note.value);

        if (version === saveVersion) {
            saveStatus.textContent = "All changes saved";
        } else {
            saveStatus.textContent = "New changes pending…";
        }
    } catch (error) {
        console.error(error);
        saveStatus.textContent = "Save failed — retry your changes";
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

    const point = pointFromEvent(event);

    currentStroke = {
        color: colorPicker.value,
        width: 3,
        points: [point]
    };

    canvas.setPointerCapture(event.pointerId);
});

canvas.addEventListener("pointermove", event => {
    if (!drawing || !currentStroke) return;

    currentStroke.points.push(pointFromEvent(event));

    // Preview the current stroke locally.
    redrawCanvas();
    drawStroke(currentStroke);
});

async function stopDrawing() {
    if (!drawing || !currentStroke) return;

    drawing = false;

    const completedStroke = currentStroke;
    currentStroke = null;

    strokes.push(completedStroke);
    redrawCanvas();

    try {
        await connection.invoke(
            "AddStroke",
            config.roomId,
            JSON.stringify(completedStroke)
        );
    } catch (error) {
        console.error(error);
        saveStatus.textContent = "Stroke save failed";
    }
}

canvas.addEventListener("pointerup", stopDrawing);
canvas.addEventListener("pointercancel", stopDrawing);
canvas.addEventListener("lostpointercapture", () => {
    if (drawing) stopDrawing();
});

document.getElementById("clearCanvas").addEventListener("click", async () => {
    if (!connectionReady) return;

    try {
        await connection.invoke("ClearCanvas", config.roomId);
    } catch (error) {
        console.error(error);
        saveStatus.textContent = "Could not clear canvas";
    }
});

resizeCanvas();
window.addEventListener("resize", resizeCanvas);
connect();
