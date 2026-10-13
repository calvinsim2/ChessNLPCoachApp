using ChessNLPCoachApp.Interfaces;
using ChessNLPCoachApp.Records;
using System.Diagnostics;

namespace ChessNLPCoachApp.Services
{
    // =============================================================
    // STOCKFISH CLIENT
    // =============================================================

    public sealed class StockfishClient : IStockfishClient, IAsyncDisposable
    {
        private readonly string _stockfishPath;

        private Process? _process;

        private StreamWriter? _input;

        private StreamReader? _output;

        public StockfishClient(string stockfishPath)
        {
            _stockfishPath = stockfishPath;
        }


        // ---------------------------------------------------------
        // START STOCKFISH
        // ---------------------------------------------------------

        public async Task StartAsync()
        {
            if (_process != null)
            {
                return;
            }

            if (!File.Exists(_stockfishPath))
            {
                throw new FileNotFoundException(
                    "Stockfish executable could not be found.",
                    _stockfishPath);
            }

            var startInfo =
                new ProcessStartInfo
                {
                    FileName = _stockfishPath,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

            _process =
                new Process
                {
                    StartInfo = startInfo
                };

            _process.Start();

            _input =
                _process.StandardInput;

            _output =
                _process.StandardOutput;

            await SendCommandAsync("uci");

            await ReadUntilAsync(
                line => line == "uciok");

            await SendCommandAsync(
                "setoption name Threads value 2");

            await SendCommandAsync(
                "setoption name Hash value 128");

            await SendCommandAsync(
                "setoption name UCI_ShowWDL value true");

            await WaitUntilReadyAsync();
        }


        // ---------------------------------------------------------
        // ANALYSE POSITION
        // ---------------------------------------------------------

        public async Task<List<EngineLine>> AnalyseAsync(
            string fen,
            int depth,
            int multiPv,
            string? searchMove = null)
        {
            await SendCommandAsync(
                $"setoption name MultiPV value {multiPv}"
            );

            await WaitUntilReadyAsync();

            await SendCommandAsync(
                $"position fen {fen}"
            );

            var command =
                $"go depth {depth}";

            /*
             * searchmoves tells Stockfish:
             *
             * "Analyse the position, but force this particular
             * root move."
             *
             * This lets us evaluate the player's move from the
             * SAME original position.
             */
            if (!string.IsNullOrWhiteSpace(searchMove))
            {
                command += $" searchmoves {searchMove}";
            }

            await SendCommandAsync(command);

            var latestLines =
                new Dictionary<int, EngineLine>();

            while (true)
            {
                var line =
                    await _output!.ReadLineAsync();

                if (line == null)
                {
                    break;
                }

                if (line.StartsWith("info "))
                {
                    var parsed =
                        ParseInfoLine(line);

                    if (parsed != null)
                    {
                        latestLines[parsed.MultiPv] =
                            parsed;
                    }
                }

                if (line.StartsWith("bestmove "))
                {
                    break;
                }
            }

            return latestLines
                .Values
                .OrderBy(x => x.MultiPv)
                .ToList();
        }


        // ---------------------------------------------------------
        // PARSE STOCKFISH INFO
        // ---------------------------------------------------------

        private static EngineLine? ParseInfoLine(string line)
        {
            string[] tokens =
                line.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries);

            int depthIndex =
                Array.IndexOf(tokens, "depth");

            int scoreIndex =
                Array.IndexOf(tokens, "score");

            int pvIndex =
                Array.IndexOf(tokens, "pv");

            if (depthIndex < 0 ||
                scoreIndex < 0 ||
                pvIndex < 0)
            {
                return null;
            }

            if (scoreIndex + 2 >= tokens.Length)
            {
                return null;
            }

            int multiPv = 1;

            int multiPvIndex =
                Array.IndexOf(
                    tokens,
                    "multipv");

            if (multiPvIndex >= 0 &&
                multiPvIndex + 1 < tokens.Length)
            {
                int.TryParse(
                    tokens[multiPvIndex + 1],
                    out multiPv);
            }

            int.TryParse(
                tokens[depthIndex + 1],
                out int depth);

            string scoreType =
                tokens[scoreIndex + 1];

            int.TryParse(
                tokens[scoreIndex + 2],
                out int scoreValue);

            int? wdlWin = null;

            int? wdlDraw = null;

            int? wdlLoss = null;

            int wdlIndex =
                Array.IndexOf(
                    tokens,
                    "wdl");

            if (wdlIndex >= 0 &&
                wdlIndex + 3 < tokens.Length)
            {
                if (int.TryParse(
                        tokens[wdlIndex + 1],
                        out int parsedWin) &&
                    int.TryParse(
                        tokens[wdlIndex + 2],
                        out int parsedDraw) &&
                    int.TryParse(
                        tokens[wdlIndex + 3],
                        out int parsedLoss))
                {
                    wdlWin =
                        parsedWin;

                    wdlDraw =
                        parsedDraw;

                    wdlLoss =
                        parsedLoss;
                }
            }

            string pv =
                string.Join(
                    " ",
                    tokens.Skip(pvIndex + 1));

            return new EngineLine(
                MultiPv: multiPv,
                Depth: depth,
                ScoreType: scoreType,
                ScoreValue: scoreValue,
                PrincipalVariation: pv,
                WdlWin: wdlWin,
                WdlDraw: wdlDraw,
                WdlLoss: wdlLoss);
        }


        // ---------------------------------------------------------
        // UCI HELPERS
        // ---------------------------------------------------------

        private async Task SendCommandAsync(
            string command)
        {
            await _input!.WriteLineAsync(command);

            await _input.FlushAsync();
        }


        private async Task WaitUntilReadyAsync()
        {
            await SendCommandAsync("isready");

            await ReadUntilAsync(
                line => line == "readyok"
            );
        }


        private async Task ReadUntilAsync(
            Func<string, bool> condition)
        {
            while (true)
            {
                var line =
                    await _output!.ReadLineAsync();

                if (line == null)
                {
                    throw new Exception(
                        "Stockfish terminated unexpectedly."
                    );
                }

                if (condition(line))
                {
                    return;
                }
            }
        }


        // ---------------------------------------------------------
        // CLEAN UP
        // ---------------------------------------------------------

        public async ValueTask DisposeAsync()
        {
            if (_process == null)
            {
                return;
            }

            try
            {
                await SendCommandAsync("quit");
            }
            catch
            {
                // Ignore shutdown problems.
            }

            if (!_process.HasExited)
            {
                _process.Kill();
            }

            _process.Dispose();
        }
    }
}
