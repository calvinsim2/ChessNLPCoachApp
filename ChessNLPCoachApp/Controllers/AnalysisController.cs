using ChessNLPCoachApp.Interfaces;
using ChessNLPCoachApp.Models;
using ChessNLPCoachApp.Records;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChessNLPCoachApp.Controllers
{
    [Authorize]
    public class AnalysisController : Controller
    {
        private readonly IPgnService _pgnService;
        private readonly IMoveAnalysisService _moveAnalysisService;
        public AnalysisController(IPgnService pgnService,
                                  IMoveAnalysisService moveAnalysisService)
        {
            _pgnService = pgnService;
            _moveAnalysisService = moveAnalysisService;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(IFormFile pgnFile)
        {
            if (pgnFile == null)
            {
                TempData["UploadMessage"] =
                    "No file was received by the server.";

                return RedirectToAction(nameof(Index));
            }

            if (pgnFile.Length == 0)
            {
                TempData["UploadMessage"] =
                    "The uploaded file is empty.";

                return RedirectToAction(nameof(Index));
            }

            // Create a temporary file path.
            string tempFilePath = Path.Combine(
                Path.GetTempPath(),
                $"{Guid.NewGuid()}.pgn");

            try
            {
                // ==============================================
                // 1. SAVE UPLOADED FILE TEMPORARILY
                // ==============================================

                await using (FileStream stream =
                    new FileStream(
                        tempFilePath,
                        FileMode.Create))
                {
                    await pgnFile.CopyToAsync(stream);
                }

                // ==============================================
                // 2. PASS FILE TO PGN SERVICE
                // ==============================================

                List<PgnMoveContext> moves =
                    await _pgnService.GetMoveContextsAsync(
                        tempFilePath);

                // ==============================================
                // 3. ANALYSE EVERY MOVE (STOCKFISH + LANGFLOW)
                // ==============================================

                List<MoveAnalysis> analyses =
                    await _moveAnalysisService.AnalyseGameAsync(
                        moves);

                // ==============================================
                // 4. RETURN RESULT FOR TESTING
                // ==============================================

                GameAnalysisViewModel viewModel = new GameAnalysisViewModel
                                                  {
                                                      FileName = pgnFile.FileName,
                                                      MoveAnalyses = analyses
                                                  };

                return View("Results", viewModel);

            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                // ==============================================
                // 5. CLEAN UP TEMPORARY FILE
                // ==============================================

                if (System.IO.File.Exists(tempFilePath))
                {
                    System.IO.File.Delete(tempFilePath);
                }
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
