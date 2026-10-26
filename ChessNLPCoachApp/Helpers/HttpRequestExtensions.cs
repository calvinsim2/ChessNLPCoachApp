namespace ChessNLPCoachApp.Helpers
{
    /// <summary>
    /// Request helpers shared by controllers, filters and middleware.
    /// </summary>
    public static class HttpRequestExtensions
    {
        /// <summary>
        /// True when the request was sent by JavaScript (fetch/XHR) and expects a
        /// JSON <c>{ toastType, toastMessage }</c> body instead of a redirect.
        /// </summary>
        public static bool IsAjaxRequest(this HttpRequest request)
        {
            return string.Equals(request.Headers.XRequestedWith.ToString(),
                                 "XMLHttpRequest",
                                 StringComparison.OrdinalIgnoreCase);
        }
    }
}
