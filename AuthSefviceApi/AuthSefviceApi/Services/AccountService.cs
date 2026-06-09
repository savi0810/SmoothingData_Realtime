using AuthSefviceApi.Data.DTOs;
using AuthSefviceApi.Data.Models;
using AuthSefviceApi.Data.Repositorys;

namespace AuthSefviceApi.Services
{
    public class AccountService
    {
        private readonly AccountRepository _accountRepository;
        private readonly JwtService _jwtService;

        public AccountService(AccountRepository accountRepository, JwtService jwtService)
        {
            _accountRepository = accountRepository;
            _jwtService = jwtService;
        }

        public async Task<List<Account>> GetAllAccountsAsync()
        {
            return await _accountRepository.GetAllAccountsAsync();
        }

        public async Task RegisterAsync(RegisterDto dto)
        {
            if (await _accountRepository.GetAccountByUsernameAsync(dto.Username) is not null)
                throw new Exception("Пользователь с таким именем уже существует.");

            if (await _accountRepository.GetAccountByEmailAsync(dto.Email) is not null)
                throw new Exception("Пользователь с таким адресом электронной почты уже зарегистрирован.");

            var account = new Account
            {
                Username = dto.Username,
                Email = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
            };

            await _accountRepository.AddAccountAsync(account);
        }

        public async Task<string> LoginAsync(string username, string password)
        {
            var account = await _accountRepository.GetAccountByUsernameAsync(username);

            if (account is null || !BCrypt.Net.BCrypt.Verify(password, account.PasswordHash))
                throw new Exception("Неверное имя пользователя или пароль.");

            return _jwtService.GenerateToken(account);
        }
    }
}
