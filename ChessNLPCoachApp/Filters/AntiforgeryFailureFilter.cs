using ChessNLPCoachApp.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Core.Infrastructure;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace ChessNLPCoachApp.Filters
{
    /// <summary>
    /// Replaces the empty 400 returned by <c>[ValidateAntiForgeryToken]</c> with a toast.
    /// </summary>
    /// <remarks>
    /// AJAX requests receive JSON <c>{ toastType, toastMessage }</c>; normal form posts
    /// get the toast through TempData and are redirected to the Analysis page.
    /// </remarks>
    public sealed class AntiforgeryFailureFilter : IAlwaysRunResultFilter
    {
        private const string Message = "Your form has expired. Please refresh the page and try again.";

        private readonly ITempDataDictionaryFactory _tempDataFactory;

        public AntiforgeryFailureFilter(ITempDataDictionaryFactory tempDataFactory)
        {
            _tempDataFactory = tempDataFactory;
        }

        public void OnResultExecuting(ResultExecutingContext context)
        {
            if (context.Result is not IAntiforgeryValidationFailedResult)
            {
                return;
            }

            if (context.HttpContext.Request.IsAjaxRequest())
            {
                context.Result = new ObjectResult(new { toastType = "warn", toastMessage = Message })
                {
                    StatusCode = StatusCodes.Status400BadRequest
                };

                return;
            }

            ITempDataDictionary tempData = _tempDataFactory.GetTempData(context.HttpContext);

            tempData["ToastType"] = "warn";
            tempData["ToastMessage"] = Message;

            context.Result = new RedirectToActionResult("Index", "Analysis", null);
        }

        public void OnResultExecuted(ResultExecutedContext context)
        {
        }
    }
}
