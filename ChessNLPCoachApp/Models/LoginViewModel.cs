using System.ComponentModel.DataAnnotations;

namespace ChessNLPCoachApp.Models
{
    public class LoginViewModel
    {
        // Username cannot be empty.
        [Required]
        public string Username { get; set; } = string.Empty;

        // Password cannot be empty.
        [Required]

        // Inform MVC that this field contains a password.
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}
