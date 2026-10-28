# CollabSpace — Project Handoff

Last updated: 2026-10-28
Project status: Initial setup; database configuration in progress.

## 1. Project goal

Build CollabSpace, a real-time collaborative notes and drawing web application.

Users can:
- Create a shared workspace.
- Generate a unique access code.
- Join a workspace using its access code.
- Edit shared notes.
- Draw on a shared canvas.
- See other participants' changes without refreshing.
- Reopen a workspace and recover its saved content.

This is a college project. The priority is a working, demonstrable MVP,
not a production-scale Google Docs clone.

## 2. Technology stack

- OS: Alpine Linux 3.23.x, x86_64
- Shell: zsh
- .NET SDK: 10.0.110
- ASP.NET Core runtime: 10.0.13
- Framework: ASP.NET Core MVC
- Language: C#
- Real-time communication: ASP.NET Core SignalR
- Database: MariaDB 11.4.x (MySQL-compatible)
- ORM: Entity Framework Core
- MySQL provider: Pomelo.EntityFrameworkCore.MySql
- Frontend: Razor Views, HTML, CSS, Bootstrap, JavaScript Canvas
- Project directory: ~/projects/CollabSpace

## 3. Project setup status

Completed:
- Installed .NET 10 SDK using Alpine's package manager.
- Created MVC project at ~/projects/CollabSpace.
- Confirmed that the .NET SDK and ASP.NET Core runtime work.
- Added UseAppHost=false to CollabSpace.csproj because restore
  initially failed while looking for an Alpine-specific app host package.
- Confirmed that restore, build, and run work with that setting.

Important:
- Keep <UseAppHost>false</UseAppHost> in the project's PropertyGroup.
- Use `dotnet build` and `dotnet run` normally after the setting is saved.
- Do not remove or overwrite the existing MVC project.

## 4. Database decisions

Intended database:
- Database name: collabspace_db
- Database engine: MariaDB
- Development database account: root, as explicitly requested by the user.
- Database access: local development only.

The user prefers using root rather than creating a separate application account.

Do not assume the database or account configuration succeeded until verified.
Do not drop collabspace_db when removing an application user.

## 5. Current blocker

The command:

    dotnet user-secrets init

failed with:

    Could not find the global property 'UserSecretsId' in MSBuild project
    '/home/someone123/projects/CollabSpace/CollabSpace.csproj'.
    Ensure this property is set in the project or use the '--id'
    command line option.

Likely next fix:
- Inspect CollabSpace.csproj.
- Add a UserSecretsId property to an existing PropertyGroup, or run
  `dotnet user-secrets init --id <unique-project-id>` if supported by
  the installed SDK.
- Verify the csproj still builds.
- Then save ConnectionStrings:DefaultConnection using dotnet user-secrets.
- Do not put real database passwords into this handoff document.

Example property:

    <UserSecretsId>CollabSpace-Local-Development</UserSecretsId>

Verify that the property is present before initializing user secrets.

## 6. Planned architecture

Controllers:
- HomeController
- RoomsController

SignalR:
- Hubs/CollaborationHub.cs

Models:
- Room.cs
- RoomNote.cs
- DrawingStroke.cs

Data:
- ApplicationDbContext.cs

Services (add when useful):
- RoomService.cs
- CollaborationService.cs

Views:
- Views/Home/Index.cshtml
- Views/Rooms/Create.cshtml
- Views/Rooms/Join.cshtml
- Views/Rooms/Workspace.cshtml

JavaScript:
- wwwroot/js/collaboration.js
- wwwroot/js/drawing.js

## 7. Initial database design

Rooms:
- Id
- Name
- AccessCode (unique)
- CreatedAt
- ExpiresAt (nullable)

RoomNotes:
- Id
- RoomId (foreign key)
- Content
- UpdatedAt

DrawingStrokes:
- Id
- RoomId (foreign key)
- StrokeData (JSON or LONGTEXT)
- CreatedAt

Each room initially has one shared note document and multiple drawing strokes.
A separate sticky-note entity can be added later if needed.

## 8. Planned implementation stages

Stage 1: Verify .NET and MariaDB; configure EF Core and connection string.
Stage 2: Create Room model and ApplicationDbContext.
Stage 3: Create EF Core migration and database tables.
Stage 4: Implement room creation and access-code joining.
Stage 5: Build the workspace Razor View.
Stage 6: Add SignalR room groups and live note synchronization.
Stage 7: Add HTML Canvas drawing and broadcast strokes.
Stage 8: Save/load notes and drawing history.
Stage 9: Show participant presence and handle reconnects.
Stage 10: Improve styling, test, and prepare the college demonstration.

## 9. Collaboration behavior

- Work in small, verifiable steps.
- Usually provide 2–3 complete files per response.
- Include exact file paths and copy-paste shell commands.
- Bash/zsh scripts are welcome when safe and useful.
- Do not overwrite files without inspecting existing contents.
- Keep code compatible with .NET 10 and the installed Alpine environment.
- Run `dotnet build` after meaningful changes.
- Diagnose errors from actual terminal output rather than guessing.
- Explain which directory a command should run in.
- Keep this file updated when major milestones or architecture decisions change.

## 10. Immediate next actions

1. Fix the UserSecretsId issue in CollabSpace.csproj.
2. Verify MariaDB is running and collabspace_db exists.
3. Configure the connection string without committing credentials.
4. Install/verify compatible EF Core and Pomelo package versions.
5. Implement Room.cs and ApplicationDbContext.cs.
6. Build and verify before moving on to room controllers.

## 11. How to resume in a new conversation

Provide LATEST.md and say:

"Continue building CollabSpace from LATEST.md. Inspect the current
stage and provide the next small implementation step with complete files
and copy-paste commands. Do not repeat completed work."
