# Secret Santa

A small Secret Santa site for a team. No accounts, no e-mails: everything works through secret links.

## How it works

1. The organizer creates a game (optional budget and note) and gets two links:
   - **organizer panel** (`/admin/{token}`): participants, exclusions, the draw;
   - **invite link** (`/join/{token}`): sent to colleagues.
2. Each colleague joins with a name and a wishlist and receives a **personal link** (`/me/{token}`).
3. The organizer runs the draw. Everyone opens their personal link and sees who they give to, along with that person's wishlist.

Rules:

- The draw builds one random cycle through all participants, so nobody draws themselves and there are no mutual pairs.
- Exclusions work both ways: the two people never give to each other.
- After the draw each participant turns over one of the face-down cards on their page; any card shows the recipient the draw assigned. The organizer panel shows who has opened their card.
- After the draw a giver can ask their recipient questions anonymously and the recipient can answer; messages are stored per recipient with only the side (Santa or recipient), never the sender's identity. Cancelling the draw deletes them.
- Nobody, the organizer included, can see the full list of pairs. Cancelling the draw resets them and reopens joining.
- After the draw, participants and exclusions are locked.
- Wishlists stay editable; the giver always sees the latest version.
- A lost personal link can be reissued from the organizer panel; the old one stops working.
- The device that joined remembers the participant, so opening the invite link again leads back to the personal page.
- UI languages: Armenian (default), Russian, English (`?lang=hy|ru|en`, remembered in a cookie).

## Stack

ASP.NET Core 10 (LTS) Razor Pages, EF Core with PostgreSQL, no JavaScript framework.

All state lives in the database, including the data-protection keys, so the app itself is stateless. Migrations are applied on startup. The startup log has one line with the time from process start until the app is ready and, separately, how long `Database.Migrate()` took (that includes the first connection to the database, so it shows a slow database wake-up).

`GET /healthz` (and `HEAD`) answers `200 ok` without touching the database and with `Cache-Control: no-store`.

The only setting is the database: `DATABASE_URL` (`postgres://user:password@host:port/db?sslmode=require`) or `ConnectionStrings:Default` (Npgsql format).

## Run locally

```bash
docker run -d --name secretsanta-postgres -p 5433:5432 -e POSTGRES_USER=secretsanta -e POSTGRES_PASSWORD=secretsanta -e POSTGRES_DB=secretsanta postgres:16-alpine
```

```bash
dotnet run --project src/SecretSanta
```

Open http://localhost:5106. `appsettings.Development.json` points at that container.

## Deploy to Render

1. Create a PostgreSQL database (for example on Neon, region Frankfurt, next to the Render service) and copy its connection URL.
2. Push this repository to GitHub, then in Render choose **New → Blueprint** and select the repository; `render.yaml` describes the service.
3. Paste the connection URL into `DATABASE_URL` when Render asks for it.

`render.yaml` sets `autoDeployTrigger: checksPass`: a push to `main` is deployed only after the CI checks on that commit have passed. The health check is `/healthz`.

### Keeping the free instance awake

A free Render web service spins down after 15 minutes without incoming requests, and the next request waits about a minute while it starts again. That is fine for the organizer, but annoying when the whole team opens their links on game day.

While the game is running, point an external uptime pinger at `https://<your-service>.onrender.com/healthz` every 10 minutes during the day, for example with [cron-job.org](https://cron-job.org) and a schedule like `*/10 9-21 * * *` in the `Asia/Yerevan` time zone. `/healthz` does not touch the database, so the pings do not keep the database awake or use its quota.

Don't ping around the clock: Render gives 750 free instance hours per calendar month to the whole workspace, shared by all free services in it, and a single always-on instance already uses up to 744 of them. Turn the pinger off after the game.

## Tests

```bash
dotnet test
```

Tests use xUnit v3 on Microsoft Testing Platform (selected in `global.json`).

To check that the EF model and the migrations agree:

```bash
dotnet tool restore
dotnet ef migrations has-pending-model-changes --project src/SecretSanta
```

## CI

`.github/workflows/ci.yml` runs on every push and pull request: restore, build with warnings as errors, tests, the EF model check, `docker build`, and a check that the image has the `Asia/Yerevan` time zone (without it dates silently fall back to UTC; the app also logs a warning at startup).

## Docker

```bash
docker build -t secretsanta .
docker run -d --name secretsanta -p 8080:8080 -e DATABASE_URL=... secretsanta
```

Put the container behind HTTPS (reverse proxy); `X-Forwarded-*` headers are honoured, so the generated links use the public host and scheme.
