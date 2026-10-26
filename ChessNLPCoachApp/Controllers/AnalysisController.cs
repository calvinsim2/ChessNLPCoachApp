using ChessNLPCoachApp.Configuration;
using ChessNLPCoachApp.Helpers;
using ChessNLPCoachApp.Hubs;
using ChessNLPCoachApp.Interfaces;
using ChessNLPCoachApp.Models;
using ChessNLPCoachApp.Records;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace ChessNLPCoachApp.Controllers
{
    [Authorize]
    public class AnalysisController : Controller
    {
        private readonly IPgnService _pgnService;
        private readonly IMoveAnalysisService _moveAnalysisService;
        private readonly IDailyAnalysisQuotaService _quotaService;
        private readonly IHubContext<AnalysisProgressHub> _hubContext;
        private readonly IMemoryCache _cache;
        private readonly SemaphoreSlim _analysisGate;
        private readonly ILogger<AnalysisController> _logger;
        private sealed record CachedResult(string UserId, GameAnalysisViewModel ViewModel);
        public AnalysisController(IPgnService pgnService,
                                  IMoveAnalysisService moveAnalysisService,
                                  IDailyAnalysisQuotaService quotaService,
                                  IHubContext<AnalysisProgressHub> hubContext,
                                  IMemoryCache cache,
                                  SemaphoreSlim analysisGate,
                                  ILogger<AnalysisController> logger)
        {
            _pgnService = pgnService;
            _moveAnalysisService = moveAnalysisService;
            _quotaService = quotaService;
            _hubContext = hubContext;
            _cache = cache;
            _analysisGate = analysisGate;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            string? userId =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            try
            {
                // Retrieve today's remaining analyses.
                ViewBag.RemainingAnalyses = await _quotaService.GetRemainingAsync(userId,
                                                                                  HttpContext.RequestAborted);
            }
            catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
            {
                // The client disconnected, so there is no one to respond to.
                _logger.LogInformation("Client disconnected while loading daily analysis quota for user {UserId}.", userId);

                return new EmptyResult();
            }
            catch (Exception ex)
            {
                string errorId = HttpContext.TraceIdentifier;

                _logger.LogError(ex,
                                 "Failed to load daily analysis quota for user {UserId}. ErrorId: {ErrorId}",
                                 userId,
                                 errorId);

                // Still render the page; the quota is shown as unavailable.
                ViewBag.RemainingAnalyses = null;

                TempData["ToastType"] = "warn";
                TempData["ToastMessage"] = $"Your daily quota is temporarily unavailable. (Ref: {errorId})";
            }

            return View();

        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("upload-attempts")]
        [RequestSizeLimit(128 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = AnalysisLimits.MaxPgnFileBytes)]
        public async Task<IActionResult> Upload(IFormFile pgnFile, string? analysisId = null)
        {
            string? userId =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            if (pgnFile == null || pgnFile.Length == 0)
            {
                return UploadFailure("error", "Please select a PGN file.", StatusCodes.Status400BadRequest);
            }

            if (pgnFile.Length > AnalysisLimits.MaxPgnFileBytes)
            {
                return UploadFailure("error", "PGN file cannot exceed 64 KB.", StatusCodes.Status400BadRequest);
            }

            if (!string.Equals(Path.GetExtension(pgnFile.FileName),
                                                 ".pgn",
                                                 StringComparison.OrdinalIgnoreCase))
            {
                return UploadFailure("error", "Only .pgn files are accepted.", StatusCodes.Status400BadRequest);
            }

            Guid analysisRequestId;

            if (Request.IsAjaxRequest())
            {
                if (!Guid.TryParse(analysisId, out analysisRequestId)
                    || analysisRequestId == Guid.Empty)
                {
                    return UploadFailure("error", "Invalid analysis identifier.", StatusCodes.Status400BadRequest);
                }
            }
            else
            {
                // Normal form uploads do not require SignalR.
                analysisRequestId = Guid.NewGuid();
            }

            int remainingDailyAllowance;

            try
            {
                remainingDailyAllowance = await _quotaService.GetRemainingAsync(userId,
                                                                                HttpContext.RequestAborted);
            }
            catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
            {
                // The client disconnected, so there is no one to respond to.
                _logger.LogInformation("Client disconnected during quota check for user {UserId}.", userId);

                return new EmptyResult();
            }
            catch (Exception ex)
            {
                string errorId = HttpContext.TraceIdentifier;

                _logger.LogError(ex,
                                 "Failed to read daily analysis quota for user {UserId}. ErrorId: {ErrorId}",
                                 userId,
                                 errorId);

                return UploadFailure("error", 
                                     $"We couldn't check your daily quota right now. Please try again shortly. (Ref: {errorId})",
                                     StatusCodes.Status500InternalServerError);
            }

            if (remainingDailyAllowance <= 0)
            {
                return UploadFailure("warn", "You have used all available Analyses today.", StatusCodes.Status400BadRequest);
            }

            // Allow only one simultaneous analysis within this application instance.
            bool acquired = await _analysisGate.WaitAsync(0);

            if (!acquired)
            {
                return UploadFailure("warn", "Another analysis is running. Try again later.", StatusCodes.Status409Conflict);
            }

            // Create a temporary file path.
            string tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pgn");

            try
            {
                // ==============================================
                // SAVE UPLOADED FILE TEMPORARILY
                // ==============================================

                await using (FileStream stream = new FileStream(tempFilePath, FileMode.Create))
                {
                    await pgnFile.CopyToAsync(stream);
                }

                // ==============================================
                // PASS FILE TO PGN SERVICE
                // ==============================================

                List<PgnMoveContext> moves = await _pgnService.GetMoveContextsAsync(tempFilePath);

                if (moves.Count == 0 ||
                    moves.Count > AnalysisLimits.MaxPlyCount)
                {
                    return UploadFailure("error", "PGN must contain between 1 and 80 plies.", StatusCodes.Status400BadRequest);
                }

                // =====================================
                // CONSUME DAILY ANALYSIS QUOTA
                // =====================================
                bool quotaConsumed = await _quotaService.TryConsumeAsync(userId,
                                                                         HttpContext.RequestAborted);

                // This second check handles concurrent
                // requests that exhaust the final allowance.
                if (!quotaConsumed)
                {
                    return UploadFailure("warn", "Your daily analysis limit has been reached.", StatusCodes.Status400BadRequest);
                }

                // ==============================================
                // PREPARE PROGRESS CALLBACK
                // ==============================================

                Func<AnalysisProgress, Task>? progressCallback = Request.IsAjaxRequest() ?
                                                                 CreateProgressCallback(userId, analysisRequestId) : null;

                // Send the total number of plies before analysis.
                if (progressCallback != null)
                {
                    await progressCallback(
                        new AnalysisProgress(
                            CompletedPlys: 0,
                            TotalPlys: moves.Count,
                            San: string.Empty));
                }

                // ==============================================
                // ANALYSE EVERY MOVE (STOCKFISH + LLM)
                // ==============================================

                List<MoveAnalysis> analyses = await _moveAnalysisService.AnalyseGameAsync(moves, progressCallback);

                // ==============================================
                // RETURN RESULT FOR TESTING
                // ==============================================

                GameAnalysisViewModel viewModel = new GameAnalysisViewModel
                                                  {
                                                      FileName = pgnFile.FileName,
                                                      MoveAnalyses = analyses
                                                  };

                // AJAX uploads need a URL to navigate to.
                if (Request.IsAjaxRequest())
                {
                    string cacheKey = $"analysis-result:{analysisRequestId:N}";

                    _cache.Set( cacheKey, new CachedResult(userId, viewModel), TimeSpan.FromMinutes(20));

                    return Ok(new
                    {
                        redirectUrl = Url.Action(nameof(Results), 
                                                 "Analysis", 
                                                 new { analysisId = analysisRequestId })
                    });
                }

                return View("Results", viewModel);

            }
            catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "Client disconnected during PGN analysis for user {UserId}.",
                    userId);

                return new EmptyResult();
            }
            catch (InvalidDataException ex)
            {
                string errorId = HttpContext.TraceIdentifier;

                _logger.LogError(ex, "Invalid PGN uploaded.");

                return UploadFailure("error", $"The PGN file could not be parsed. (Ref: {errorId})", StatusCodes.Status400BadRequest);
            }
            catch (Exception ex)
            {
                string errorId = HttpContext.TraceIdentifier;

                _logger.LogError(ex,
                                 $"Chess PGN analysis failed. ErrorId: {errorId}",
                                 userId,
                                 errorId);

                return UploadFailure("error", $"Unable to analyse the uploaded PGN. (Ref: {errorId})", StatusCodes.Status500InternalServerError);
            }
            finally
            {
                try
                {
                    // Always remove the temporary file.
                    if (System.IO.File.Exists(tempFilePath))
                    {
                        System.IO.File.Delete(tempFilePath);
                    }
                }
                finally
                {
                    // Always release the analysis slot.
                    _analysisGate.Release();
                }
            }

        }

        [HttpGet]
        public IActionResult Results(Guid analysisId)
        {
            // Construct the same cache key used during upload.
            string cacheKey = $"analysis-result:{analysisId:N}";

            // Retrieve the completed analysis from memory.
            if (!_cache.TryGetValue<CachedResult>(cacheKey, out CachedResult? cachedResult))
            {
                return NotFound();
            }

            // Identify the currently authenticated user.
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Only the owner may access the cached result.
            if (cachedResult == null ||
                cachedResult.UserId != userId)
            {
                return NotFound();
            }

            // Reuse the existing Results.cshtml view.
            return View(cachedResult.ViewModel);
        }


        private IActionResult UploadFailure(string toastType, string toastMessage, int statusCode)
        {
            // AJAX requests receive JSON.
            if (Request.IsAjaxRequest())
            {
                return StatusCode(statusCode, new
                {
                    toastType,
                    toastMessage
                });
            }

            // Normal form submissions keep existing behaviour.
            TempData["ToastType"] = toastType;
            TempData["ToastMessage"] = toastMessage;

            return RedirectToAction(nameof(Index));
        }


        private Func<AnalysisProgress, Task> CreateProgressCallback(string userId, Guid analysisId)
        {
            string groupName = AnalysisProgressHub.GroupName(userId, analysisId);

            return async progress =>
            {
                try
                {
                    await _hubContext.Clients.Group(groupName)
                                             .SendAsync("AnalysisProgress", progress);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Unable to send analysis progress for {AnalysisId}.", analysisId);
                }
            };
        }


    }
}
