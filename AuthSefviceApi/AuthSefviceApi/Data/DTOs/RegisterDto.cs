using System.ComponentModel.DataAnnotations;
namespace AuthSefviceApi.Data.DTOs
{
    public class RegisterDto
    {
        [Required(ErrorMessage = "Имя пользователя обязательно для заполнения.")]
        [MinLength(3, ErrorMessage = "Имя пользователя должно содержать не менее 3 символов.")]
        [MaxLength(50, ErrorMessage = "Имя пользователя не должно превышать 50 символов.")]
        public string Username { get; set; } = string.Empty;
        [Required(ErrorMessage = "Электронная почта обязательна для заполнения.")]
        [EmailAddress(ErrorMessage = "Неверный формат электронной почты.")]
        public string Email { get; set; } = string.Empty;
        [Required(ErrorMessage = "Пароль обязателен для заполнения.")]
        [MinLength(6, ErrorMessage = "Пароль должен содержать не менее 6 символов.")]
        public string Password { get; set; } = string.Empty;
    }
}