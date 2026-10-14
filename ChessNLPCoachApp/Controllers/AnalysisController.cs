using ChessNLPCoachApp.Configuration;
using ChessNLPCoachApp.Interfaces;
using ChessNLPCoachApp.Models;
using ChessNLPCoachApp.Records;
using ChessNLPCoachApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace ChessNLPCoachApp.Controllers
{
    [Authorize]
    public class AnalysisController : Controller
    {
        private readonly IPgnService _pgnService;
        private readonly IMoveAnalysisService _moveAnalysisService;
        private readonly DailyAnalysisQuotaService _quotaService;
        private readonly SemaphoreSlim _analysisGate;
        private readonly ILogger<AnalysisController> _logger;
        public AnalysisController(IPgnService pgnService,
                                  IMoveAnalysisService moveAnalysisService,
                                  DailyAnalysisQuotaService quotaService,
                                  SemaphoreSlim analysisGate,
                                  ILogger<AnalysisController> logger)
        {
            _pgnService = pgnService;
            _moveAnalysisService = moveAnalysisService;
            _quotaService = quotaService;
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

            // Retrieve today's remaining analyses.
            ViewBag.RemainingAnalyses =
                await _quotaService.GetRemainingAsync(
                    userId,
                    HttpContext.RequestAborted);

            return View();

        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("upload-attempts")]
        [RequestSizeLimit(128 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = AnalysisLimits.MaxPgnFileBytes)]
        public async Task<IActionResult> Upload(IFormFile pgnFile)
        {
            string? userId =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            if (pgnFile == null || pgnFile.Length == 0)
            {
                TempData["UploadMessage"] =
                    "Please select a PGN file.";

                return RedirectToAction(nameof(Index));
            }

            if (pgnFile.Length > AnalysisLimits.MaxPgnFileBytes)
            {
                TempData["UploadMessage"] =
                    "PGN file cannot exceed 64 KB.";

                return RedirectToAction(nameof(Index));
            }

            if (!string.Equals(
                    Path.GetExtension(pgnFile.FileName),
                    ".pgn",
                    StringComparison.OrdinalIgnoreCase))
            {
                TempData["UploadMessage"] =
                    "Only .pgn files are accepted.";

                return RedirectToAction(nameof(Index));
            }

            int remainingDailyAllowance = await _quotaService.GetRemainingAsync(userId,
                                                                                HttpContext.RequestAborted);

            if (remainingDailyAllowance <= 0)
            {
                TempData["UploadMessage"] = "You have used all available Analyses today.";

                return RedirectToAction(nameof(Index));
            }

            // Allow only one simultaneous analysis within this application instance.
            bool acquired = await _analysisGate.WaitAsync(0);

            if (!acquired)
            {
                TempData["UploadMessage"] =
                    "Another analysis is running. Try again later.";

                return RedirectToAction(nameof(Index));
            }

            // Create a temporary file path.
            string tempFilePath = Path.Combine(
                Path.GetTempPath(),
                $"{Guid.NewGuid()}.pgn");

            try
            {
                // ==============================================
                // SAVE UPLOADED FILE TEMPORARILY
                // ==============================================

                await using (FileStream stream =
                    new FileStream(
                        tempFilePath,
                        FileMode.Create))
                {
                    await pgnFile.CopyToAsync(stream);
                }

                // ==============================================
                // PASS FILE TO PGN SERVICE
                // ==============================================

                List<PgnMoveContext> moves =
                    await _pgnService.GetMoveContextsAsync(
                        tempFilePath);

                if (moves.Count == 0 ||
                    moves.Count > AnalysisLimits.MaxPlyCount)
                {
                    TempData["UploadMessage"] =
                        "PGN must contain between 1 and 80 plies.";

                    return RedirectToAction(nameof(Index));
                }

                // =====================================
                // CONSUME DAILY ANALYSIS QUOTA
                // =====================================
                bool quotaConsumed =
                    await _quotaService.TryConsumeAsync(
                        userId,
                        HttpContext.RequestAborted);

                // This second check handles concurrent
                // requests that exhaust the final allowance.
                if (!quotaConsumed)
                {
                    TempData["UploadMessage"] =
                        "Your daily analysis limit has been reached.";

                    return RedirectToAction(nameof(Index));
                }

                // ==============================================
                // ANALYSE EVERY MOVE (STOCKFISH + LANGFLOW)
                // ==============================================

                List<MoveAnalysis> analyses =
                    await _moveAnalysisService.AnalyseGameAsync(
                        moves);

                // ==============================================
                // RETURN RESULT FOR TESTING
                // ==============================================

                GameAnalysisViewModel viewModel = new GameAnalysisViewModel
                                                  {
                                                      FileName = pgnFile.FileName,
                                                      MoveAnalyses = analyses
                                                  };

                return View("Results", viewModel);

            }
            catch (InvalidDataException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Invalid PGN uploaded.");

                TempData["UploadMessage"] =
                    "The PGN file could not be parsed.";
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Chess analysis failed.");

                TempData["UploadMessage"] =
                    "Unable to analyse the uploaded PGN.";
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

            return RedirectToAction(nameof(Index));
        }
    }
}
