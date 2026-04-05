# HoaFoodAudio

HoaFoodAudio is a food-stall audio guide system with:

- .NET MAUI mobile app
- FastAPI backend
- PostgreSQL database
- Owner dashboard
- Superadmin dashboard

This README focuses on two things:

- run the project on a local machine
- move the whole project to another machine and run it again

For a step-by-step Vietnamese Docker guide, see:

- `HUONG_DAN_DOCKER.md`

## Project Structure

- `App/`: .NET MAUI mobile app
- `Backend/`: FastAPI backend, SQL scripts, Docker files
- `Web/`: login, owner dashboard, admin, superadmin pages
- `Releases/`: APK and QR release artifacts
- `PRD.html`: PRD presentation
- `PRD.docx`: PRD document

## Main Runtime Flow

1. User opens the mobile app.
2. App loads language, cache, and current location.
3. App calls backend APIs such as:
   - `GET /sync/version`
   - `POST /nearby`
   - `GET /stalls/{id}`
   - `GET /qr/resolve`
   - `GET /audio/stalls/{id}`
   - `POST /logs/location`
   - `POST /logs/listening`
4. Backend reads PostgreSQL data and returns stall content.
5. App shows popup, plays audio, or falls back to TTS.
6. Web dashboards read analytics and heatmap data from backend.

## Requirements

### For backend

- Python 3.11+ or newer
- PostgreSQL

### For app build

- .NET 9 SDK
- .NET MAUI workload
- Android SDK if you want to build APK

## Database

Important SQL files:

- `Backend/schema.sql`: create schema for a fresh database
- `Backend/migration.sql`: apply safe updates for an existing database
- `Backend/cleanup_categories.sql`: cleanup duplicated or broken categories

Default database URL in code:

```text
postgresql://admin:password123@localhost:5432/food_street_db
```

This value comes from:

- `Backend/main.py`

You can override it with `DATABASE_URL`.

## Run Backend Locally

### 1. Create database

Example with PostgreSQL:

```powershell
createdb -U admin -h localhost food_street_db
psql -U admin -h localhost -d food_street_db -f D:\C#\Backend\schema.sql
psql -U admin -h localhost -d food_street_db -f D:\C#\Backend\migration.sql
```

### 2. Create virtual environment and install packages

```powershell
cd D:\C#\Backend
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
```

### 3. Start backend

```powershell
$env:DATABASE_URL="postgresql://admin:password123@localhost:5432/food_street_db"
$env:APP_SECRET="streetfeast-secret-key"
$env:SEED_DEFAULT_ADMIN="true"
python -m uvicorn main:app --host 0.0.0.0 --port 8000
```

Backend will be available at:

- `http://localhost:8000`

Useful pages:

- `http://localhost:8000/login`
- `http://localhost:8000/owner`
- `http://localhost:8000/superadmin`

## Default Web Login

If the database is fresh and `SEED_DEFAULT_ADMIN=true`, backend creates:

- username: `admin`
- password: `admin123`

## Run Web

You do not need a separate frontend server.

The backend serves the web pages directly from:

- `Web/`

So once backend is running, open the URLs above in the browser.

## Run Mobile App

The app base URL is defined in:

- `App/ApiSettings.cs`

Current default base URL:

```text
https://hoafoodaudio.live
```

If you want the mobile app to use a local backend or another machine:

1. Change `DefaultLanBaseUrl` in `App/ApiSettings.cs`
2. Rebuild the app

Example:

```csharp
private const string DefaultLanBaseUrl = "http://192.168.1.10:8000";
```

Build the app:

```powershell
dotnet build D:\C#\App\FoodStreetAudioGuide.csproj
```

Release APK currently kept in repo:

- `Releases/HoaFoodAudio-v1.2.apk`

## Move The Project To Another Machine

There are two common ways.

### Option 1: Move source code only

Use this if the target machine can create a fresh database.

Steps:

1. Clone or copy the repo
2. Install Python, PostgreSQL, .NET SDK, MAUI workload
3. Run:
   - `Backend/schema.sql`
   - `Backend/migration.sql`
4. Start backend
5. If needed, change app base URL and rebuild APK

### Option 2: Move source code and real data

Use this if you want the target machine to have the same users, stalls, and analytics data.

Recommended:

1. Export database from the source machine using `pg_dump`
2. Copy the dump file to the target machine
3. Restore the dump into PostgreSQL
4. Start backend with the same `APP_SECRET`

Why `APP_SECRET` matters:

- QR signatures depend on it
- if you change it, old QR codes may stop working

## Restore Real Database On Another Machine

Example with SQL dump:

```powershell
psql -U admin -h localhost -d food_street_db -f D:\path\to\food_street_db_export.sql
```

Example with PostgreSQL custom dump:

```powershell
pg_restore -U admin -h localhost -d food_street_db --clean --if-exists D:\path\to\food_street_db_export.dump
```

## Run On Ubuntu Or VPS

Typical backend start command:

```bash
cd ~/CSharp/Backend
python3 -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
export DATABASE_URL="postgresql://admin:password123@localhost:5432/food_street_db"
export APP_SECRET="streetfeast-secret-key"
python -m uvicorn main:app --host 127.0.0.1 --port 8000
```

If using systemd, the service can point to the same backend entrypoint:

```bash
python -m uvicorn main:app --host 127.0.0.1 --port 8000
```

## Recommended Submission Package

If you want to submit this project to a teacher or move it safely:

- source code repo
- `PRD.html`
- `PRD.docx`
- `Releases/HoaFoodAudio-v1.2.apk`
- database dump file kept outside Git if it contains real data

## Notes

- Real database dumps should not be pushed to Git unless you explicitly want that.
- The repo now ignores export files such as:
  - `Backend/food_street_db_export.sql`
  - `Backend/*.dump`
- The duplicate old PRD and old release artifacts were removed to keep the repo cleaner.
