using System.Security.Claims;
using System.Security.Cryptography;
using CollabSpace.Data;
using CollabSpace.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CollabSpace.Controllers;

public class RoomsController : Controller
{
    public const string RoomAccessClaim = "collabspace:room_id";

    private readonly ApplicationDbContext _db;

    public RoomsController(ApplicationDbContext db)
    {
        _db = db;
    }

    private bool HasRoomAccess(int roomId)
    {
        return User.HasClaim(
            RoomAccessClaim,
            roomId.ToString(
                System.Globalization.CultureInfo.InvariantCulture));
    }

    private async Task GrantRoomAccessAsync(int roomId)
    {
        var claims = User.FindAll(RoomAccessClaim)
            .Where(c => int.TryParse(c.Value, out var existingId)
                        && existingId > 0)
            .GroupBy(c => c.Value)
            .Select(g => g.First())
            .ToList();

        var roomIdValue = roomId.ToString(
            System.Globalization.CultureInfo.InvariantCulture);

        if (!claims.Any(c => c.Value == roomIdValue))
        {
            claims.Add(new Claim(RoomAccessClaim, roomIdValue));
        }

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = false,
                AllowRefresh = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12)
            });
    }

    [HttpGet]
    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        string? name,
        CancellationToken cancellationToken)
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
            {
                continue;
            }

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

                await GrantRoomAccessAsync(room.Id);

                return RedirectToAction(
                    nameof(Workspace), new { id = room.Id });
            }
            catch (DbUpdateException)
            {
                _db.Entry(room).State = EntityState.Detached;

                if (!await _db.Rooms.AnyAsync(
                        r => r.AccessCode == code, cancellationToken))
                {
                    throw;
                }
            }
        }

        ModelState.AddModelError(
            "", "Could not create the workspace. Try again.");

        ViewData["RoomName"] = name;
        return View();
    }

    [HttpGet]
    public IActionResult Join() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Join(
        string? accessCode,
        CancellationToken cancellationToken)
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

        var room = await _db.Rooms
            .AsNoTracking()
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

        await GrantRoomAccessAsync(room.Id);

        return RedirectToAction(
            nameof(Workspace), new { id = room.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Workspace(
        int id,
        CancellationToken cancellationToken)
    {
        if (id <= 0 || !HasRoomAccess(id))
        {
            return NotFound("Workspace not found or access denied.");
        }

        var room = await _db.Rooms
            .AsNoTracking()
            .Include(r => r.Note)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (room is null ||
            (room.ExpiresAt.HasValue &&
             room.ExpiresAt.Value <= DateTime.UtcNow))
        {
            return NotFound("Workspace not found or expired.");
        }

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
        if (id <= 0 || !HasRoomAccess(id))
        {
            return NotFound(new {
                error = "Workspace not found or access denied."
            });
        }

        if (request.Content is null ||
            request.Content.Length > 500_000)
        {
            return BadRequest(new {
                error = "Note is too large or invalid."
            });
        }

        var room = await _db.Rooms
            .Include(r => r.Note)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (room is null ||
            (room.ExpiresAt.HasValue &&
             room.ExpiresAt.Value <= DateTime.UtcNow))
        {
            return NotFound(new {
                error = "Workspace not found or expired."
            });
        }

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
