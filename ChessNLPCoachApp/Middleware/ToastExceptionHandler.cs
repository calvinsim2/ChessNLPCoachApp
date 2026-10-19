using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace ChessNLPCoachApp.Middleware
{
    /// <summary>
    /// Global safety net for exceptions that no controller caught.
    /// </summary>
    /// <remarks>
    /// Instead of showing an error page, it logs the exception, stores a generic
    /// toastr message (with a reference ID) in TempData, and redirects the user back
    /// to the page they came from. _Layout.cshtml then displays the toast.
    /// Registered as a singleton, so it must only depend on singleton services.
    /// </remarks>
    public sealed class ToastExceptionHandler : IExceptionHandler
    {
        private const string FallbackPath = "/Account/Login";

        private readonly ITempDataDictionaryFactory _tempDataFactory;
        private readonly ILogger<ToastExceptionHandler> _logger;

        public ToastExceptionHandler(ITempDataDictionaryFactory tempDataFactory,
                                     ILogger<ToastExceptionHandler> logger)
        {
            _tempDataFactory = tempDataFactory;
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(HttpContext context,
                                                    Exception exception,
                                                    CancellationToken cancellationToken)
        {
            // The client disconnected, so there is no one to respond to.
            if (exception is OperationCanceledException &&
                context.RequestAborted.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "Client disconnected during {Method} {Path}.",
                    context.Request.Method,
                    context.Request.Path);

                return true;
            }

            string errorId = context.TraceIdentifier;

            _logger.LogError(
                exception,
                "Unhandled exception on {Method} {Path}. ErrorId: {ErrorId}",
                context.Request.Method,
                context.Request.Path,
                errorId);

            string toastMessage =
                $"Something went wrong. Please try again shortly. (Ref: {errorId})";

            ITempDataDictionary tempData = _tempDataFactory.GetTempData(context);

            // An unread toast means the page we redirected to (or a page another
            // action redirected to) also failed before it could render. Stop here
            // rather than risk bouncing the user between failing pages.
            string? returnUrl = tempData.ContainsKey("ToastMessage")
                ? null
                : GetSafeReturnUrl(context);

            if (returnUrl == null)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "text/plain; charset=utf-8";

                await context.Response.WriteAsync(toastMessage, cancellationToken);

                return true;
            }

            tempData["ToastType"] = "error";
            tempData["ToastMessage"] = toastMessage;

            // MVC's TempData filter doesn't run here, so persist the cookie manually.
            tempData.Save();

            context.Response.Redirect(returnUrl);

            return true;
        }

        // ==============================================
        // CHOOSE WHERE TO SEND THE USER
        // ==============================================

        private static string? GetSafeReturnUrl(HttpContext context)
        {
            HttpRequest request = context.Request;

            // Prefer the page the user came from, but only if it's on this site.
            if (Uri.TryCreate(request.Headers.Referer.ToString(), UriKind.Absolute, out Uri? referer) &&
                string.Equals(referer.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase))
            {
                if (!IsFailingPage(request, referer.AbsolutePath))
                {
                    return referer.PathAndQuery;
                }
            }

            if (!IsFailingPage(request, FallbackPath))
            {
                return FallbackPath;
            }

            return null;
        }

        // Redirecting a failed GET back to itself would loop. A failed POST can safely
        // return to the same path, since the browser will GET it instead.
        private static bool IsFailingPage(HttpRequest request, string path)
        {
            return HttpMethods.IsGet(request.Method) &&
                   string.Equals(request.Path.Value, path, StringComparison.OrdinalIgnoreCase);
        }
    }
}
