# Qvizz

A Kahoot-style quiz for friends. ASP.NET Core (.NET 10) + SignalR + EF Core / PostgreSQL.

- `/admin`: presenter screen (password protected), shown on the big screen.
- `/game`: player screen for phones.
- `/host`: host remote for your phone (same password as `/admin`): start, pause / resume, next, remove players, new game. Use it while the big screen shows `/admin`.

## Run

```bash
dotnet run
```

The app listens on `http://0.0.0.0:5080`, so phones on the same Wi-Fi can open `http://<your-PC-IP>:5080/game`.

## Configuration

| Setting | Where | Default |
|---|---|---|
| `Admin:Password` | appsettings.json / env `Admin__Password` | `qvizz-admin` |
| `ConnectionStrings:Qvizz` | user-secrets / env `ConnectionStrings__Qvizz` | empty = in memory only (lost on restart) |
| `Game:CountdownSeconds` / `QuestionSeconds` / `GroupingSeconds` | appsettings.json | 5 / 30 / 45 |
| `Game:PublicUrl` | appsettings.json / env `Game__PublicUrl` | empty = the address the presenter page is opened on, + `/game` |

Neon connection string for local runs (kept out of source control). Use the ".NET" format Neon shows
(`Host=...;Database=...;Username=...;Password=...;SSL Mode=Require`), not the `postgresql://` URL:

```bash
dotnet user-secrets set "ConnectionStrings:Qvizz" "Host=<host>.neon.tech;Database=<db>;Username=<user>;Password=<pw>;SSL Mode=Require"
```

## Database and game state

The database is the source of truth. Every change (joins, drawings, every answer change, scores,
the current phase and timer deadline) is saved to PostgreSQL within a fraction of a second, and on
startup the active game is loaded back, so a restart or redeploy resumes where it was. The engine keeps
a live copy in memory so phones get instant responses.

- Tables: `games` (one per session; `Status` = `Active` or `Archived`), `players`, `avatars`, `answers`
  (one row per player per question, including how often they changed it and how fast they answered).
- "New game" (on the podium, or the corner button) archives the current session and opens a new lobby.
- The schema is managed with EF Core migrations (`Data/Migrations`) and applied automatically on startup.
  After changing the entities: `dotnet tool run dotnet-ef migrations add <Name> --output-dir Data/Migrations`.

## Deploying to Railway

- Railway sets `PORT`; the app listens on it automatically.
- Set variables: `ConnectionStrings__Qvizz` (Neon, .NET format), `Admin__Password`, and
  `Game__PublicUrl` = `https://<your-app>.up.railway.app/game` so the lobby QR code points at the public address.
- Run a single instance (the live game lives in one process).
- The "keep phone screen on" feature needs HTTPS, which Railway provides.

## Quiz content

Edit `Quiz/quiz.json` and restart. Put images in `wwwroot/images/` and reference them as `/images/file.jpg`.
Question types: `MultipleChoice` (4 options + `correctIndex`), `TrueFalse` (`correctBool`),
`Grouping` (`pairs` of `left`/`right`, each with `text` and/or `image`), `ClosestNumber` (`correctNumber`, optional `unit`).
Any question can override its time with `timeLimitSeconds`.
Any question can have a picture: `"image": "/images/file.jpg"`. It shows large on the presenter screen only
(landscape pictures between the question and the answers, portrait ones beside them). Use JPG/PNG/WebP around 1600px wide.

## Look and feel

"Game Night × Trading Floor": a board-game table (cobalt board, white cards, plastic answer pieces,
players' drawn faces as pawns) with answers laid out like trading positions and scores as a ticker.

- The presenter screen is a fixed 1440×900 stage scaled to fit any laptop, TV or projector. Use the
  "Full screen" button in the bottom-right corner.
- The lobby shows faces for up to 30 players and switches to name tags above that.
- Fonts (Bricolage Grotesque, JetBrains Mono) and the SignalR client load from CDNs, so the
  presenter and phones need internet access.
- Approved design mockups: `.impeccable/mocks/screens/board.html`.
- After changing CSS/JS, bump the `?v=` number in `admin.html` / `game.html` so browsers reload them.

## Scoring

- Multiple choice: 100, true/false: 80, grouping: 20 per correct pair.
- Speed bonus for correct answers, in order of answering: 20, 18, 16, 15, 14 … 1, then 0. For grouping, anyone with at least one correct pair qualifies.
- Closest number: 100, 90, 80, 70, 65, 60 … 5 by distance, then 0. Equal distances share the better place.
- Streak: a question is "kept" with a correct answer; on matching with at least half the pairs right (2 of 4, 3 of 5);
  on closest number by being in the better half of all players (at least the closest). From the 3rd kept question in a
  row, each kept question earns +5 and the player gets a 🔥 on the standings. A miss or no answer resets the streak.
- Answers can be changed until the time runs out; the time of the last change counts for the speed bonus.
- Multiple choice / true-false end as soon as every connected player has answered. Grouping and closest-number
  always run the full time, and whatever is on the player's phone at that moment is scored.
