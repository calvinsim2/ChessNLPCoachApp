# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

ASP.NET Core MVC (.NET 10) app that analyses an uploaded PGN game move-by-move with Stockfish, derives deterministic chess facts in C#, and asks an LLM to turn them into beginner-friendly explanations. Single project: `ChessNLPCoachApp/ChessNLPCoachApp.csproj` (solution file `ChessNLPCoachApp.slnx`). There is no test project.

## Commands

Run from the repository root:

```powershell
dotnet restore ChessNLPCoachApp.slnx
dotnet build ChessNLPCoachApp.slnx
dotnet run --project ChessNLPCoachApp/ChessNLPCoachApp.csproj --environment Development

# EF Core migrations (SQL Server)
dotnet ef migrations add <Name> --project ChessNLPCoachApp/ChessNLPCoachApp.csproj
dotnet ef database update --project ChessNLPCoachApp/ChessNLPCoachApp.csproj --startup-project ChessNLPCoachApp/ChessNLPCoachApp.csproj

# Docker (installs Linux Stockfish 19, listens on 8080)
docker build -t chessnlp:local .
docker run --rm -p 8080:8080 --env-file .env.docker chessnlp:local
```

Client-side libraries are managed by LibMan (`ChessNLPCoachApp/libman.json`, e.g. toastr) and are committed under `wwwroot/lib`.

## Configuration

- `ConnectionStrings:DefaultConnection` — required at startup (User Secrets locally). `Program.cs` forces a ≥30s connect timeout and enables EF retry-on-failure because Azure SQL may be paused/cold-starting.
- `Stockfish:Path` / `Stockfish:Depth` — Windows path in `appsettings*.json`; the Dockerfile overrides with `Stockfish__Path=/usr/local/bin/stockfish`.
- `Foundry:Endpoint` / `Foundry:Deployment` / `Foundry:ApiKey` — hosted GPT-4.1-mini (via the `OpenAI` SDK) used by `FoundryChessCoachClient`.
- `LANGFLOW_BASE_URL` / `LANGFLOW_FLOW_ID` / `LANGFLOW_API_KEY` — read as **process environment variables** (not `IConfiguration`). A `.env` next to the built assembly is loaded via DotNetEnv if present.
- `SeedUser:Username` / `SeedUser:Password` — in Development only, `Program.cs` creates this Identity user at startup if missing (password min length 10).

## Architecture

Request flow for an analysis (`AnalysisController.Upload`):

1. Guards in order: `[Authorize]`, antiforgery, `upload-attempts` rate-limit policy (per user, hourly), file size/extension, `DailyAnalysisQuotaService.GetRemainingAsync`, then a singleton `SemaphoreSlim(1,1)` that allows **one analysis per app instance** (non-blocking `WaitAsync(0)`).
2. The upload is saved to a temp file and parsed by `PgnService` (Gera.Chess library) into `PgnMoveContext` records (SAN, UCI, FEN before/after). Parse failures throw `InvalidDataException`.
3. Ply count is checked, then quota is consumed atomically with `TryConsumeAsync` (conditional `ExecuteUpdateAsync`, with a duplicate-key fallback for the first insert of the day).
4. `MoveAnalysisService.AnalyseGameAsync` runs sequentially per move:
   - `StockfishClient` (long-lived UCI process over stdin/stdout, scoped per request) returns MultiPV 3 top lines; if the played move isn't among them, a second `searchmoves`-limited search evaluates it from the same position so scores are comparable.
   - `Helpers/MoveClassifier` scores quality primarily by **WDL expected-point loss** (thresholds are constants there), keeping centipawn loss as secondary.
   - `Helpers/MoveFactsBuilder` + `ChessBoardAnalysisHelper` derive deterministic board/tactical facts.
   - `Helpers/CoachingPromptBuilder` assembles the prompt; the LLM client returns the explanation.
5. Results render in `Views/Analysis/Results.cshtml` via `GameAnalysisViewModel`.

LLM provider selection is **hard-coded** in `MoveAnalysisService.AnalyseMoveAsync`: Foundry is called by default; Langflow is injected but only used by swapping the commented line. Both clients are always registered in DI. The Foundry system prompt (in `FoundryChessCoachClient`) treats the C#-computed facts as authoritative and forbids the model from inventing board details — keep new facts flowing through `MoveFacts`/`CoachingPromptBuilder` rather than asking the LLM to infer them.

Limits live in `Configuration/AnalysisLimits.cs` (64 KiB PGN, 80 plies, 3 analyses/UTC day, 10 uploads/hour). Some user-facing messages in the controller repeat these numbers as literals.

Persistence: `ApplicationDbContext` is an Identity DbContext plus `DailyAnalysisUsages`. Default route is `Account/Login`.

### Error handling / user messages

User-facing messages are shown with **toastr**, not inline: set `TempData["ToastType"]` (`success` | `info` | `warn` | `error`) and `TempData["ToastMessage"]`, then redirect; `_Layout.cshtml` calls `uiCommon.notify[type]` (`wwwroot/js/uiCommon.js`). Controllers catch expected failures themselves, include `HttpContext.TraceIdentifier` as a "Ref" in the message, and treat `OperationCanceledException` on `RequestAborted` as a client disconnect (return `EmptyResult`). Anything uncaught goes to `Middleware/ToastExceptionHandler`, which writes the toast to TempData, saves it manually, and redirects to a same-site referer (or `/Account/Login`) with loop protection. It's a singleton — it may only depend on singleton services.

## Deployment

Pushing to `master` triggers `.github/workflows/deploy-azure.yml`: builds the Docker image, pushes to ACR `chessnlpacr`, and deploys to App Service `chessNLPcoach` (OIDC Azure login). Migrations are not applied by the pipeline.
