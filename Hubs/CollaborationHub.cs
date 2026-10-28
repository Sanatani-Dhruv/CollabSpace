using System.Text.Json;
using CollabSpace.Data;
using CollabSpace.Controllers;
using CollabSpace.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CollabSpace.Hubs;

public sealed class CollaborationHub : Hub
{
    private readonly ApplicationDbContext _db;

    public CollaborationHub(ApplicationDbContext db)
    {
        _db = db;
    }

    private static string GroupName(int roomId) => $"room:{roomId}";

    private int? JoinedRoomId => Context.Items["roomId"] is int id
        ? id
        : null;

    private bool HasRoomAccess(int roomId)
    {
        return Context.User?.HasClaim(
            RoomsController.RoomAccessClaim,
            roomId.ToString(
                System.Globalization.CultureInfo.InvariantCulture)) == true;
    }

    private void EnsureJoined(int roomId)
    {
        if (!HasRoomAccess(roomId))
            throw new HubException(
                "You are not authorized for this workspace.");

        if (JoinedRoomId != roomId)
            throw new HubException("Join this workspace first.");
    }

    private async Task EnsureActiveRoom(int roomId)
    {
        var active = await _db.Rooms.AsNoTracking().AnyAsync(r =>
            r.Id == roomId &&
            (!r.ExpiresAt.HasValue ||
             r.ExpiresAt.Value > DateTime.UtcNow));

        if (!active)
            throw new HubException(
                "Workspace not found or expired.");
    }

    private async Task EnsureActiveAccess(int roomId)
    {
        EnsureJoined(roomId);
        await EnsureActiveRoom(roomId);
    }

    private static CanvasNoteDto ToDto(CanvasNote note) =>
        new(
            note.Id,
            note.RoomId,
            note.Content,
            note.X,
            note.Y,
            note.Width,
            note.Height,
            note.Color,
            note.CreatedAt,
            note.UpdatedAt);

    private static void ValidateContent(string content)
    {
        if (content is null || content.Length > 500_000)
            throw new HubException(
                "Note content is too large or invalid.");
    }

    private static void ValidateGeometry(
        double x,
        double y,
        double width,
        double height)
    {
        if (!double.IsFinite(x) ||
            !double.IsFinite(y) ||
            !double.IsFinite(width) ||
            !double.IsFinite(height) ||
            Math.Abs(x) > 1_000_000 ||
            Math.Abs(y) > 1_000_000 ||
            width < 120 ||
            width > 1_000 ||
            height < 100 ||
            height > 1_000)
        {
            throw new HubException(
                "Canvas note position or dimensions are invalid.");
        }
    }

    private static void ValidateColor(string color)
    {
        if (color is null ||
            color.Length != 7 ||
            color[0] != '#' ||
            !color.AsSpan(1).ToArray()
                .All(Uri.IsHexDigit))
        {
            throw new HubException(
                "Canvas note color must be a hex color such as #fff3a3.");
        }
    }

    public async Task JoinRoom(int roomId)
    {
        if (roomId <= 0 || !HasRoomAccess(roomId))
            throw new HubException("Workspace access denied.");

        await EnsureActiveRoom(roomId);

        if (JoinedRoomId is int previousRoomId &&
            previousRoomId != roomId)
        {
            await Groups.RemoveFromGroupAsync(
                Context.ConnectionId,
                GroupName(previousRoomId));
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            GroupName(roomId));

        Context.Items["roomId"] = roomId;

        await Clients.Caller.SendAsync("RoomJoined", roomId);
    }

    // Legacy shared-note support. Retained until the canvas UI is migrated.
    public async Task UpdateNote(int roomId, string content)
    {
        await EnsureActiveAccess(roomId);
        ValidateContent(content);

        var room = await _db.Rooms
            .Include(r => r.Note)
            .FirstOrDefaultAsync(r => r.Id == roomId);

        if (room is null)
            throw new HubException(
                "Workspace not found or expired.");

        if (room.Note is null)
        {
            room.Note = new RoomNote
            {
                RoomId = room.Id,
                Content = content,
                UpdatedAt = DateTime.UtcNow
            };
        }
        else
        {
            room.Note.Content = content;
            room.Note.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        await Clients.OthersInGroup(GroupName(roomId))
            .SendAsync("NoteUpdated", content);
    }

    // Return every saved canvas note in stable order.
    public async Task<List<CanvasNoteDto>> GetCanvasNotes(int roomId)
    {
        await EnsureActiveAccess(roomId);

        var notes = await _db.CanvasNotes
            .AsNoTracking()
            .Where(n => n.RoomId == roomId)
            .OrderBy(n => n.Id)
            .ToListAsync();

        return notes.Select(ToDto).ToList();
    }

    // Create a new note at the default canvas position.
    public async Task<CanvasNoteDto> CreateCanvasNote(
        int roomId,
        string content)
    {
        await EnsureActiveAccess(roomId);
        ValidateContent(content);

        var now = DateTime.UtcNow;

        var note = new CanvasNote
        {
            RoomId = roomId,
            Content = content,
            X = 100,
            Y = 100,
            Width = 320,
            Height = 220,
            Color = "#fff3a3",
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.CanvasNotes.Add(note);
        await _db.SaveChangesAsync();

        var result = ToDto(note);

        await Clients.OthersInGroup(GroupName(roomId))
            .SendAsync("CanvasNoteCreated", result);

        return result;
    }

    // Update content, position, size, and color for one note.
    public async Task<CanvasNoteDto> UpdateCanvasNote(
        int roomId,
        int noteId,
        string content,
        double x,
        double y,
        double width,
        double height,
        string color)
    {
        await EnsureActiveAccess(roomId);
        ValidateContent(content);
        ValidateGeometry(x, y, width, height);
        ValidateColor(color);

        var note = await _db.CanvasNotes.FirstOrDefaultAsync(n =>
            n.Id == noteId && n.RoomId == roomId);

        if (note is null)
            throw new HubException(
                "Canvas note not found in this workspace.");

        note.Content = content;
        note.X = x;
        note.Y = y;
        note.Width = width;
        note.Height = height;
        note.Color = color;
        note.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        var result = ToDto(note);

        await Clients.OthersInGroup(GroupName(roomId))
            .SendAsync("CanvasNoteUpdated", result);

        return result;
    }

    // Delete only the requested note belonging to this room.
    public async Task DeleteCanvasNote(int roomId, int noteId)
    {
        await EnsureActiveAccess(roomId);

        var note = await _db.CanvasNotes.FirstOrDefaultAsync(n =>
            n.Id == noteId && n.RoomId == roomId);

        if (note is null)
            throw new HubException(
                "Canvas note not found in this workspace.");

        _db.CanvasNotes.Remove(note);
        await _db.SaveChangesAsync();

        await Clients.OthersInGroup(GroupName(roomId))
            .SendAsync("CanvasNoteDeleted", noteId);
    }

    public async Task<List<string>> GetStrokes(int roomId)
    {
        await EnsureActiveAccess(roomId);

        return await _db.DrawingStrokes
            .AsNoTracking()
            .Where(s => s.RoomId == roomId)
            .OrderBy(s => s.Id)
            .Select(s => s.StrokeData)
            .ToListAsync();
    }

    public async Task AddStroke(int roomId, string strokeData)
    {
        await EnsureActiveAccess(roomId);

        if (string.IsNullOrWhiteSpace(strokeData) ||
            strokeData.Length > 100_000)
        {
            throw new HubException(
                "Stroke data is invalid or too large.");
        }

        try
        {
            using var document = JsonDocument.Parse(strokeData);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new JsonException();
        }
        catch (JsonException)
        {
            throw new HubException(
                "Stroke data must be a JSON object.");
        }

        var stroke = new DrawingStroke
        {
            RoomId = roomId,
            StrokeData = strokeData,
            CreatedAt = DateTime.UtcNow
        };

        _db.DrawingStrokes.Add(stroke);
        await _db.SaveChangesAsync();

        await Clients.OthersInGroup(GroupName(roomId))
            .SendAsync("StrokeAdded", strokeData);
    }

    public async Task ClearCanvas(int roomId)
    {
        await EnsureActiveAccess(roomId);

        var strokes = await _db.DrawingStrokes
            .Where(s => s.RoomId == roomId)
            .ToListAsync();

        _db.DrawingStrokes.RemoveRange(strokes);
        await _db.SaveChangesAsync();

        await Clients.Group(GroupName(roomId))
            .SendAsync("CanvasCleared");
    }
}

public sealed record CanvasNoteDto(
    int Id,
    int RoomId,
    string Content,
    double X,
    double Y,
    double Width,
    double Height,
    string Color,
    DateTime CreatedAt,
    DateTime UpdatedAt);
