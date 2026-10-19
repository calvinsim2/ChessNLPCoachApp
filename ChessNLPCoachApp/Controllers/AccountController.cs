
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
        private readonly ILogger<AccountController> _logger;

        // Inject the login service into the controller.
        public AccountController(SignInManager<IdentityUser> signInManager, 
                                 ILogger<AccountController> logger)
        {
            _signInManager = signInManager;
            _logger = logger;
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

            Microsoft.AspNetCore.Identity.SignInResult signInResult;

            try
            {
                // Ask Identity to authenticate the user.
                signInResult = await _signInManager.PasswordSignInAsync(model.Username,
                                                                        model.Password,
                                                                        isPersistent: false,
                                                                        lockoutOnFailure: true);

                if (signInResult.Succeeded)
                {
                    return RedirectToAction("Index", "Analysis");
                }
                else
                {
                    _logger.LogError("Invalid credentials entered during sign in for username: {username}", model.Username);

                    TempData["ToastType"] = "error";
                    TempData["ToastMessage"] = "Invalid credentials or account does not exist";
                    return View(model);
                }
            }
            catch (Exception ex) 
            {
                _logger.LogError(ex, "Error occured during sign in for username: {username}", model.Username);

                TempData["ToastType"] = "error";
                TempData["ToastMessage"] = "Error occurred during sign-in";

                // Redisplay the login form, keeping the entered username.
                return View(model);
            }
            
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
