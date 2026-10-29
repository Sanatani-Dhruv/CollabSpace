# CollabSpace

**CollabSpace** is a high-performance, real-time collaborative workspace and digital canvas application built using **ASP.NET Core 10**, **SignalR**, **Entity Framework Core 9**, and **MariaDB**. Designed specifically for college study groups and agile development teams, CollabSpace enables instant brainstorming and multi-user ideation sessions via secure, isolated rooms without requiring page refreshes.

---

## Key Features

* **Interactive Sticky Notes System:** Create, edit, drag, resize, recolor, and delete interactive canvas sticky notes with real-time multi-user synchronization.
* **Real-Time WebSockets:** Powered by ASP.NET Core SignalR hub channels, ensuring sub-millisecond client state broadcasting.
* **Persistent Database Storage:** Room sessions, user workspaces, and canvas state data are automatically persisted and managed through MariaDB and EF Core.
* **Secure Room Access:** Cryptographically secure room access generation with server-side room validation and session authorization.
* **Responsive Workspace UI:** Clean, modern interface styled with Bootstrap 5 and customized vanilla JavaScript canvas/workspace handlers.

---

## Technology Stack

* **Backend:** C#, .NET 10, ASP.NET Core MVC
* **Real-Time Communication:** ASP.NET Core SignalR WebSockets
* **Database & ORM:** MariaDB 11.4, Entity Framework Core 9 (Pomelo Provider)
* **Frontend:** Razor Views, HTML5, CSS3, Bootstrap 5, Vanilla ES6 JavaScript

---

## Project Structure

```text
CollabSpace/
├── Controllers/
│   ├── HomeController.cs
│   └── RoomsController.cs
├── Data/
│   └── ApplicationDbContext.cs
├── Hubs/
│   └── CollaborationHub.cs
├── Migrations/
│   └── (EF Core Migration Files)
├── Models/
│   ├── CanvasNote.cs
│   ├── DrawingStroke.cs
│   ├── Room.cs
│   └── RoomNote.cs
├── Views/
│   ├── Home/
│   ├── Rooms/
│   │   └── Workspace.cshtml
│   └── Shared/
├── wwwroot/
│   ├── css/
│   └── js/
│       └── workspace.js
├── CollabSpace.csproj
└── Program.cs

```

---

## Getting Started

### Prerequisites

Ensure you have the following installed on your local machine or development environment:

* [.NET 10 SDK](https://dotnet.microsoft.com/)
* [MariaDB 11.x](https://mariadb.org/)

### Configuration

1. Clone or navigate to the project directory:
```bash
cd ~/projects/CollabSpace

```


2. Configure your database connection string using .NET User Secrets:
```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=127.0.0.1;Port=3306;Database=collabspace_db;Uid=root;Pwd=YOUR_PASSWORD;"

```


3. Apply Entity Framework Core migrations to set up the database schema:
```bash
dotnet ef database update

```


4. Build and run the project:
```bash
dotnet build
dotnet run

```


5. Open your browser and navigate to `https://localhost:5001` or the specified local development URL.

---

## License

Developed as an academic project demonstration MVP.
