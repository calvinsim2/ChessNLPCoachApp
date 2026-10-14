
using ChessNLPCoachApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ChessNLPCoachApp.Controllers
{
    public class AccountController : Controller
    {
        // Identity service for login and logout.
        private readonly SignInManager<IdentityUser> _signInManager;

        // Inject the login service into the controller.
        public AccountController(
            SignInManager<IdentityUser> signInManager)
        {
            _signInManager = signInManager;
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login()
        {
            // Skip login when already authenticated.
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Analysis");
            }

            // Display Views/Account/Login.cshtml.
            return View();
        }

        [AllowAnonymous]
        [HttpPost]

        // Reject forms without a valid anti-forgery token.
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            LoginViewModel model)
        {
            // Check the submitted model validation.
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Ask Identity to authenticate the user.
            var result =
                await _signInManager.PasswordSignInAsync(
                    model.Username,
                    model.Password,
                    isPersistent: false,
                    lockoutOnFailure: true);

            // Redirect to the upload page on success.
            if (result.Succeeded)
            {
                return RedirectToAction("Index", "Analysis");
            }

            // Display a generic message on failure.
            ModelState.AddModelError(
                string.Empty,
                "Invalid credentials or account temporarily unavailable.");

            // Redisplay the login form.
            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            // Remove the user's authentication session.
            await _signInManager.SignOutAsync();

            // Return to the login screen.
            return RedirectToAction(nameof(Login));
        }
    }
}
