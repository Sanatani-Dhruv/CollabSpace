using System.Security.Cryptography;
using CollabSpace.Data;
using CollabSpace.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CollabSpace.Controllers;

public class RoomsController : Controller
{
    private readonly ApplicationDbContext _db;

    public RoomsController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        string? name, CancellationToken cancellationToken)
    {
        name = name?.Trim();

        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
        {
            ModelState.AddModelError(
                nameof(name),
                "Enter a workspace name of 1–100 characters.");
        }

        if (!ModelState.IsValid)
        {
            ViewData["RoomName"] = name;
            return View();
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var code = Convert.ToHexString(
                RandomNumberGenerator.GetBytes(8));

            if (await _db.Rooms.AnyAsync(
                    r => r.AccessCode == code, cancellationToken))
                continue;

            var room = new Room
            {
                Name = name!,
                AccessCode = code,
                CreatedAt = DateTime.UtcNow
            };

            _db.Rooms.Add(room);

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                return RedirectToAction(nameof(Workspace),
                    new { id = room.Id });
            }
            catch (DbUpdateException)
            {
                _db.Entry(room).State = EntityState.Detached;

                if (!await _db.Rooms.AnyAsync(
                        r => r.AccessCode == code, cancellationToken))
                    throw;
            }
        }

        ModelState.AddModelError("", "Could not create the workspace. Try again.");
        ViewData["RoomName"] = name;
        return View();
    }

    [HttpGet]
    public IActionResult Join() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Join(
        string? accessCode, CancellationToken cancellationToken)
    {
        accessCode = accessCode?.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(accessCode) ||
            accessCode.Length > 32)
        {
            ModelState.AddModelError(
                nameof(accessCode), "Enter a valid access code.");
            ViewData["AccessCode"] = accessCode;
            return View();
        }

        var room = await _db.Rooms.AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.AccessCode == accessCode, cancellationToken);

        if (room is null ||
            (room.ExpiresAt.HasValue &&
             room.ExpiresAt.Value <= DateTime.UtcNow))
        {
            ModelState.AddModelError(
                "", "No active workspace was found with that code.");
            ViewData["AccessCode"] = accessCode;
            return View();
        }

        return RedirectToAction(nameof(Workspace), new { id = room.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Workspace(
        int id, CancellationToken cancellationToken)
    {
        var room = await _db.Rooms
            .AsNoTracking()
            .Include(r => r.Note)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (room is null ||
            (room.ExpiresAt.HasValue &&
             room.ExpiresAt.Value <= DateTime.UtcNow))
            return NotFound("Workspace not found or expired.");

        ViewBag.RoomId = room.Id;
        ViewBag.RoomName = room.Name;
        ViewBag.AccessCode = room.AccessCode;
        ViewBag.NoteContent = room.Note?.Content ?? "";

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveNote(
        int id,
        [FromBody] SaveNoteRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Content is null || request.Content.Length > 500_000)
            return BadRequest(new { error = "Note is too large or invalid." });

        var room = await _db.Rooms
            .Include(r => r.Note)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (room is null ||
            (room.ExpiresAt.HasValue &&
             room.ExpiresAt.Value <= DateTime.UtcNow))
            return NotFound(new { error = "Workspace not found or expired." });

        if (room.Note is null)
        {
            room.Note = new RoomNote
            {
                RoomId = room.Id,
                Content = request.Content,
                UpdatedAt = DateTime.UtcNow
            };
        }
        else
        {
            room.Note.Content = request.Content;
            room.Note.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            saved = true,
            updatedAt = room.Note.UpdatedAt
        });
    }
}

public sealed class SaveNoteRequest
{
    public string Content { get; set; } = "";
}
