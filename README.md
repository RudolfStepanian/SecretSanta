# Secret Santa

A small Secret Santa site for a team. No e-mails: colleagues sign up inside a game with a login and a password, and the organizer manages the game through a secret link.

## How it works

1. The organizer creates a game (optional budget and note) and gets two links:
   - **organizer panel** (`/admin/{token}`): participants, exclusions, the draw;
   - **invite link** (`/join/{token}`): sent to colleagues.
2. The invite link opens a sign-in page with a sign-up tab. Each colleague signs up with a login, a password, their name (what their Secret Santa sees) and a wishlist. Later they sign in there with the login and password, from any device, and land on their own page (`/play/{invite}`).
3. The organizer runs the draw. Everyone opens their page and sees who they give to, along with that person's wishlist.

Rules:

- The draw builds one random cycle through all participants, so nobody draws themselves and there are no mutual pairs.
- Exclusions work both ways: the two people never give to each other.
- After the draw each participant turns over one of the face-down cards on their page; any card shows the recipient the draw assigned. The organizer panel shows who has opened their card.
- After the draw a giver can ask their recipient questions anonymously and the recipient can answer; messages are stored per recipient with only the side (Santa or recipient), never the sender's identity. Cancelling the draw deletes them.
- Nobody, the organizer included, can see the full list of pairs. Cancelling the draw resets them and reopens joining.
- After the draw, participants and exclusions are locked.
- Wishlists stay editable; the giver always sees the latest version.
- A login is 3 to 32 Latin letters, digits, dots, hyphens or underscores, unique within a game and case-insensitive. Participants who joined before logins existed sign in with their name until they choose a login on their page.
- Passwords are stored as salted PBKDF2 hashes (ASP.NET Core `PasswordHasher`). Five wrong passwords in a row lock the participant for 15 minutes.
- A device stays signed in to a game (one cookie per game) until the participant signs out at the bottom of their page.
- A forgotten password is reset from the organizer panel: the participant is signed out everywhere, and the organizer gets a one-time link (`/me/{token}`) to pass on, where the participant sets a new password.
- Personal links from before passwords (`/me/{token}`) still sign the device in; the page then asks for a login and a password, and once they are set the link stops working.
- UI languages: Armenian (default), Russian, English (`?lang=hy|ru|en`, remembered in a cookie).

## Notifications

Participants can turn on browser push notifications on their own page. A notification arrives when the organizer runs the draw, when a participant's Secret Santa asks them something, and when their recipient replies. The texts name nobody, and the organizer panel shows who has notifications on.

- Web Push is implemented with the platform's own crypto (`Services/WebPush.cs`: RFC 8291 encryption, RFC 8292 VAPID); the tests check the encryption against the RFC 8291 example. The VAPID key pair is created on the first start and kept in the database (`Settings`), so subscriptions survive restarts and deploys.
- Messages are sent in the background (`PushQueue`, `PushDelivery`); a subscription the push service reports as gone is deleted. Only the browsers' push services (Google, Mozilla, Microsoft, Apple) are accepted as endpoints.
- On an iPhone, web push works only for a site added to the Home Screen (iOS 16.4 or later); the page explains this, and the site ships a web app manifest and icons for it.
- Signing out on a device turns its notifications off.

## Stack

ASP.NET Core 10 (LTS) Razor Pages, EF Core with PostgreSQL, no JavaScript framework.

The pages use no emoji. A small grinch (an original drawing, `wwwroot/js/grinch.js`) wanders over them like a desktop pet: it walks on the cards and the bottom of the window, climbs the window sides, leaps between cards and can be picked up and thrown. It is purely decorative and stays hidden for visitors who prefer reduced motion.

All state lives in the database, including the data-protection keys, so the app itself is stateless. Migrations are applied on startup. The startup log has one line with the time from process start until the app is ready and, separately, how long building the EF model, the first database connection (that includes waking a suspended Neon compute) and `Database.Migrate()` took.

The free Render instance has 0.1 CPU, so startup is mostly CPU time, not the database. The Docker image is therefore published ReadyToRun (compiled ahead of time); measured with `--cpus=0.1 --memory=512m`, that took startup from about 13 s to about 7 s. The connection string turns GSS encryption off, because the runtime image has no Kerberos library.

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

On Render the app keeps itself awake: Render sets `RENDER_EXTERNAL_URL`, and when it is present the app requests its own public `/healthz` every 10 minutes (`KeepAwakeInterval`). The request goes through Render's proxy, so it counts as inbound traffic. The startup log says `Keep-awake: requesting ...`; failed requests are logged as warnings. Outside Render nothing happens.

That cannot wake an instance that is already asleep. `.github/workflows/keep-awake.yml` also pings `/healthz` from outside every 5 minutes as a backup, but GitHub may delay scheduled runs or not start them at all (on this repository it did not, for hours). Change `HEALTHZ_URL` there if the service moves.

Alternatively, point any external uptime pinger at `https://<your-service>.onrender.com/healthz` every 10 minutes, around the clock. With a Google account no extra sign-up is needed: create a project at [script.google.com](https://script.google.com), paste the code below, run `install` once and allow the requested access.

```js
const URL = 'https://<your-service>.onrender.com/healthz';

function ping() {
  UrlFetchApp.fetch(URL, { muteHttpExceptions: true, followRedirects: false });
}

// Run once: replaces any old triggers with one that calls ping() every 10 minutes.
function install() {
  ScriptApp.getProjectTriggers().forEach(t => ScriptApp.deleteTrigger(t));
  ScriptApp.newTrigger('ping').timeBased().everyMinutes(10).create();
}
```

Google e-mails you if the pings start failing (the site is down or does not answer within about a minute). `/healthz` does not touch the database, so the pings do not keep the database awake or use its quota.

This only works if this is the only free service in the Render workspace: Render gives 750 free instance hours per calendar month to the whole workspace, shared by all its free services, and one always-on instance uses up to 744 of them. When the hours run out, Render suspends all free services in the workspace until the start of the next month. If the workspace has other free services, ping only during the day (for example `*/10 9-21 * * *` in `Asia/Yerevan`) or move this service to a paid instance type.

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
