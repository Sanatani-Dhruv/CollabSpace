using System.Text.Json;
using CollabSpace.Data;
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

    private void EnsureJoined(int roomId)
    {
        if (JoinedRoomId != roomId)
            throw new HubException("Join this workspace first.");
    }

    private async Task<bool> IsActiveRoom(int roomId)
    {
        return await _db.Rooms.AsNoTracking().AnyAsync(r =>
            r.Id == roomId &&
            (!r.ExpiresAt.HasValue ||
             r.ExpiresAt.Value > DateTime.UtcNow));
    }

    public async Task JoinRoom(int roomId)
    {
        if (roomId <= 0 || !await IsActiveRoom(roomId))
            throw new HubException("Workspace not found or expired.");

        await Groups.AddToGroupAsync(
            Context.ConnectionId, GroupName(roomId));

        Context.Items["roomId"] = roomId;

        await Clients.Caller.SendAsync("RoomJoined", roomId);
    }

    public async Task UpdateNote(int roomId, string content)
    {
        EnsureJoined(roomId);

        if (content is null || content.Length > 500_000)
            throw new HubException("Note is too large or invalid.");

        var room = await _db.Rooms
            .Include(r => r.Note)
            .FirstOrDefaultAsync(r => r.Id == roomId);

        if (room is null ||
            (room.ExpiresAt.HasValue &&
             room.ExpiresAt.Value <= DateTime.UtcNow))
            throw new HubException("Workspace not found or expired.");

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

    public async Task<List<string>> GetStrokes(int roomId)
    {
        EnsureJoined(roomId);

        if (!await IsActiveRoom(roomId))
            throw new HubException("Workspace not found or expired.");

        return await _db.DrawingStrokes
            .AsNoTracking()
            .Where(s => s.RoomId == roomId)
            .OrderBy(s => s.Id)
            .Select(s => s.StrokeData)
            .ToListAsync();
    }

    public async Task AddStroke(int roomId, string strokeData)
    {
        EnsureJoined(roomId);

        if (string.IsNullOrWhiteSpace(strokeData) ||
            strokeData.Length > 100_000)
            throw new HubException("Stroke data is invalid or too large.");

        try
        {
            using var document = JsonDocument.Parse(strokeData);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new JsonException();
        }
        catch (JsonException)
        {
            throw new HubException("Stroke data must be a JSON object.");
        }

        if (!await IsActiveRoom(roomId))
            throw new HubException("Workspace not found or expired.");

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
        EnsureJoined(roomId);

        if (!await IsActiveRoom(roomId))
            throw new HubException("Workspace not found or expired.");

        var strokes = await _db.DrawingStrokes
            .Where(s => s.RoomId == roomId)
            .ToListAsync();

        _db.DrawingStrokes.RemoveRange(strokes);
        await _db.SaveChangesAsync();

        await Clients.Group(GroupName(roomId))
            .SendAsync("CanvasCleared");
    }
}
