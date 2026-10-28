# CollabSpace — Project Handoff

Last updated: 2026-10-28
Project status: MVP features implemented; reliability, access control, and final testing next.

## 1. Project goal

Build CollabSpace, a real-time collaborative notes and drawing web application for a college project.

Users can:
- Create a shared workspace.
- Generate a unique access code.
- Join a workspace using its access code.
- Edit shared notes.
- Draw on a shared canvas.
- See other participants' changes without refreshing.
- Reopen a workspace and recover its saved content.

Prioritize a working, demonstrable MVP over production-scale complexity.

## 2. Technology stack

- OS: Alpine Linux 3.23.x, x86_64
- Shell: zsh
- .NET SDK: 10.0.110
- ASP.NET Core runtime: 10.0.13
- Framework: ASP.NET Core MVC
- Language: C#
- Real-time communication: ASP.NET Core SignalR
- Database: MariaDB 11.4.12
- ORM: Entity Framework Core 9.0.0
- MySQL provider: Pomelo.EntityFrameworkCore.MySql 9.0.0
- Frontend: Razor Views, HTML, CSS, Bootstrap, JavaScript Canvas
- Project directory: ~/projects/CollabSpace
- Project file: CollabSpace.csproj
- UserSecretsId: CollabSpace-Local-Development

## 3. Working preferences

- The terminal is already in ~/projects/CollabSpace. Do not add `cd ~/projects/CollabSpace` to routine commands.
- Work in small, verifiable steps.
- Usually provide 2–3 complete files per response.
- Inspect existing files before overwriting them.
- Include exact file paths and copy-paste zsh commands.
- Run `dotnet build` after meaningful changes.
- Diagnose errors from actual terminal output.
- Do not recreate the existing MVC project.
- Keep `UseAppHost=false`.
- For clipboard-only commands/code, use the preferred pattern:
  ```
  {
      echo 'Commands or code'
  } | xclip -i
  ```
  The user prefers X11 PRIMARY selection. Do not use Python scripts merely to copy content.
- For commands that actually modify files or run builds, provide executable terminal commands.
- Never put real database passwords in this file or source control.
- Do not print or share actual secret values.

## 4. Project configuration

The existing MVC project targets `net10.0`.

Important project settings:
- Nullable: enable
- ImplicitUsings: enable
- UseAppHost: false
- UserSecretsId: CollabSpace-Local-Development

Installed packages:
- Microsoft.EntityFrameworkCore 9.0.0
- Microsoft.EntityFrameworkCore.Design 9.0.0
- Pomelo.EntityFrameworkCore.MySql 9.0.0

EF Core CLI:
- dotnet-ef 9.0.0
- Already installed and available through PATH.
- No changes to ~/.zshrc are needed.

Keep EF Core and Pomelo package versions aligned at 9.0.0. Do not upgrade packages without a reason.

## 5. Database configuration

Database:
- Name: collabspace_db
- Engine: MariaDB 11.4.12
- Development account: root, local development only
- Host: 127.0.0.1
- Port: 3306

Connection string:
- Stored in .NET User Secrets under ConnectionStrings:DefaultConnection.
- UserSecretsId: CollabSpace-Local-Development.
- Do not hardcode credentials or expose secret values.

MariaDB networking issue resolved:
- An active skip-networking directive previously prevented TCP connections.
- MariaDB configuration was changed to allow TCP connections on 127.0.0.1.
- The service was restarted and TCP connectivity verified.
- Configuration file: /etc/my.cnf.d/mariadb-server.cnf
- Backup: /etc/my.cnf.d/mariadb-server.cnf.bak
- Keep MariaDB bound to 127.0.0.1; do not expose it externally.

## 6. Database schema — applied

Migration:
- InitialCreate
- Migration files are in `Migrations/`.
- Migration has been applied successfully.

Verified tables:
- Rooms
- RoomNotes
- DrawingStrokes
- __EFMigrationsHistory

### Room model

File: `Models/Room.cs`

Properties:
- Id
- Name
- AccessCode
- CreatedAt
- ExpiresAt
- Note
- DrawingStrokes

### RoomNote model

File: `Models/RoomNote.cs`

Properties:
- Id
- RoomId
- Content
- UpdatedAt
- Room

RoomNote has a one-to-one relationship with Room.

### DrawingStroke model

File: `Models/DrawingStroke.cs`

Properties:
- Id
- RoomId
- StrokeData
- CreatedAt
- Room

DrawingStroke has a many-to-one relationship with Room. StrokeData contains JSON-serialized stroke information.

### ApplicationDbContext

File: `Data/ApplicationDbContext.cs`

DbSets:
- Rooms
- RoomNotes
- DrawingStrokes

Configuration:
- Unique index on Room.AccessCode.
- Unique index on RoomNote.RoomId.
- Foreign keys to Room.
- Cascade deletion for dependent notes and strokes.
- LONGTEXT columns for note content and stroke data.

No schema changes have been required for the features implemented so far.

## 7. Program.cs

File: `Program.cs`

Existing configuration includes:
- MVC controllers and views.
- ApplicationDbContext registered using Pomelo's UseMySql.
- MariaDB server version configured as 11.4.0 for compatibility.
- Existing error handling, HTTPS redirection, routing, authorization, and static asset mapping.
- SignalR service registration with `builder.Services.AddSignalR()`.
- Hub endpoint:
  `app.MapHub<CollabSpace.Hubs.CollaborationHub>("/collaborationHub");`

The application targets .NET 10 while using EF Core/Pomelo 9.0.0. Preserve this setup unless an actual error requires a change.

## 8. Current application files

Core files:
- `CollabSpace.csproj`
- `Program.cs`
- `Controllers/HomeController.cs`
- `Controllers/RoomsController.cs`
- `Models/Room.cs`
- `Models/RoomNote.cs`
- `Models/DrawingStroke.cs`
- `Data/ApplicationDbContext.cs`
- `Migrations/20261028103813_InitialCreate.cs`
- `Migrations/20261028103813_InitialCreate.Designer.cs`
- `Migrations/ApplicationDbContextModelSnapshot.cs`

Views:
- `Views/Home/Index.cshtml`
- `Views/Home/Privacy.cshtml`
- `Views/Rooms/Create.cshtml`
- `Views/Rooms/Join.cshtml`
- `Views/Rooms/Workspace.cshtml`
- `Views/Shared/Error.cshtml`
- `Views/Shared/_Layout.cshtml`
- `Views/Shared/_Layout.cshtml.css`
- `Views/Shared/_ValidationScriptsPartial.cshtml`
- `Views/_ViewImports.cshtml`
- `Views/_ViewStart.cshtml`

SignalR and JavaScript:
- `Hubs/CollaborationHub.cs`
- `wwwroot/js/workspace.js`
- `wwwroot/js/site.js`

Other files such as `Program.cs.bak` may exist. Inspect before modifying.

## 9. Implemented features

### Home page
File: `Views/Home/Index.cshtml`

The default welcome page has been replaced by a CollabSpace landing page with:
- Create Workspace link.
- Join Workspace link.
- Shared Notes feature description.
- Collaborative Canvas feature description.
- Easy Access feature description.

### Room creation
File: `Controllers/RoomsController.cs`

- GET and POST Create actions.
- Trims and validates room names.
- Room names must contain 1–100 characters.
- Generates access codes using `RandomNumberGenerator.GetBytes(8)`.
- Encodes 8 random bytes as a 16-character hexadecimal code.
- Checks access-code uniqueness.
- Saves the room to MariaDB.
- Retries code collisions up to five times.
- Redirects to Workspace after successful creation.

### Room joining
- GET and POST Join actions.
- Trims and uppercases submitted access codes.
- Validates input.
- Finds matching rooms.
- Handles invalid or expired rooms.
- Redirects valid room codes to Workspace.

### Workspace UI
File: `Views/Rooms/Workspace.cshtml`

Includes:
- Workspace name.
- Room access code and Copy code button.
- Shared notes textarea.
- Save status.
- Drawing canvas.
- Pen color selector.
- Clear button.
- Anti-forgery token for the note form.
- `window.collabSpaceConfig` containing room ID and SaveNote URL.
- SignalR browser client script loaded before `workspace.js`.

The current SignalR CDN reference in the workspace view is:
`https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/10.0.0/signalr.min.js`

It was found in the view using grep. The user reported that the implementation worked. Do not change the CDN reference without a specific reason or evidence of a loading problem.

### Note persistence and synchronization
- Notes are stored in `RoomNotes`.
- The workspace loads the saved note content when opened.
- The hub's UpdateNote method saves changes to MariaDB.
- The hub broadcasts NoteUpdated to other participants in the same room.
- The sender does not receive its own NoteUpdated event.
- The browser updates the textarea for incoming changes without triggering an input event.
- JavaScript debounces outgoing note updates.
- The hub rejects content larger than 500,000 characters.

### Drawing persistence and synchronization
File: `Hubs/CollaborationHub.cs`

Hub methods:
- `JoinRoom(int roomId)`
- `UpdateNote(int roomId, string content)`
- `GetStrokes(int roomId)`
- `AddStroke(int roomId, string strokeData)`
- `ClearCanvas(int roomId)`

Behavior:
- Joins SignalR group `room:{roomId}` after validating that the room exists and is not expired.
- Tracks the joined room ID in `Context.Items`.
- Rejects update requests for a room that the connection has not joined.
- Loads persisted stroke JSON ordered by database ID.
- Saves completed stroke JSON to `DrawingStrokes`.
- Broadcasts new strokes to other room participants.
- Deletes saved strokes when clearing the canvas.
- Broadcasts CanvasCleared to all room participants.
- Validates that stroke data is a JSON object.
- Rejects stroke payloads larger than 100,000 characters.

File: `wwwroot/js/workspace.js`

Behavior:
- Connects to `/collaborationHub`.
- Joins the room and loads saved strokes.
- Reconnects using SignalR automatic reconnect.
- Reloads stroke history after reconnecting.
- Receives and renders NoteUpdated, StrokeAdded, and CanvasCleared events.
- Saves note changes through SignalR.
- Draws with pointer events.
- Sends completed strokes containing color, width, and points.
- Loads saved strokes on workspace initialization.
- Supports clearing drawings across participants.
- Uses a canvas resize function and redraws stored strokes.

The user confirmed that the earlier implementation and tests worked. The latest persistent drawing implementation was then provided, but the exact result of its `dotnet build` and browser testing has not yet been reported. Verify before assuming every new drawing feature passed.

## 10. Important security and reliability limitations

These are the next priorities.

### Access control
- The room access code currently functions as a room identifier during the join flow.
- Workspace URLs use numeric room IDs.
- The SignalR hub checks that a room exists and that a connection joined it, but this is not authentication.
- Someone who knows or guesses a numeric room ID may be able to invoke JoinRoom.
- The browser currently has the room ID in its JavaScript configuration.
- The room access code must not be treated as a complete authorization system.
- Improve workspace access by validating the access code server-side and establishing a suitable session or other authorization mechanism.
- Ensure all hub operations are protected by the chosen authorization design.

### Concurrent note editing
- Multiple users can update a note.
- Simultaneous edits can overwrite each other because the current implementation is last-write-wins.
- There is no conflict resolution or version tracking.
- Improve this after the basic access-control pass, while keeping the solution suitable for a college MVP.

### Reconnection
- The JavaScript uses SignalR automatic reconnect.
- The hub stores the joined room ID in connection-scoped Context.Items.
- The client invokes JoinRoom after reconnecting.
- Test reconnect behavior and error cases.
- Ensure pending note changes are not silently lost after disconnection.

### Drawing limitations
- Strokes are broadcast after a completed stroke, not continuously during drawing.
- Stroke coordinates use CSS pixel values.
- Resizing the canvas may affect drawing alignment.
- The canvas has no undo feature.
- No participant presence indicators have been implemented.
- Clearing a canvas deletes all saved strokes for that room.

### Other limitations
- No user accounts or participant identity system.
- No production-grade rate limiting.
- No collaborative cursor indicators.
- No robust concurrent-edit conflict handling.
- No automated tests have been reported.

## 11. Current verification commands

Run these from the existing project directory; do not add an unnecessary `cd`.

Build:
```
dotnet build
```

Run:
```
dotnet run
```

Inspect SignalR script references:
```
grep -n -E 'signalr|workspace.js' Views/Rooms/Workspace.cshtml
```

List controller and view files:
```
find Controllers Views -maxdepth 3 -type f -print | sort
```

Check packages:
```
dotnet list package
```

## 12. Recommended next implementation stage

Prioritize access control and reliability rather than adding more visual features.

Suggested order:

1. Inspect the actual current `Program.cs`, `RoomsController.cs`, `Hubs/CollaborationHub.cs`, and `Views/Rooms/Workspace.cshtml`.
2. Design an MVP access-control mechanism that validates the room's access code server-side and avoids relying on the numeric room ID alone.
3. Ensure the workspace and every hub method enforce the selected access-control mechanism.
4. Verify room creation, joining, and direct workspace URL access.
5. Test two-browser-tab note and drawing synchronization.
6. Test persistence after reload and reconnection after a dropped connection.
7. Improve simultaneous note edits with a lightweight version or conflict check.
8. Add participant presence if time permits.
9. Prepare a concise college demonstration script.

Do not introduce migrations unless a real schema change is needed.

## 13. Resume instructions

In a new conversation, provide this file and say:

"Continue building CollabSpace from LATEST.md. Inspect the actual current files before editing. Start with access control and reliability. Give the next small implementation step with complete files and copy-paste zsh commands. My terminal is already in the project directory. For clipboard-only commands, use `{ echo '...' } | xclip -i`; don't use Python scripts just for copying. Do not repeat completed work."
