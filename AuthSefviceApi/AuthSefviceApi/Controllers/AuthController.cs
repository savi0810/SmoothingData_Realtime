using AuthSefviceApi.Data.DTOs;
using AuthSefviceApi.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AuthSefviceApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AccountService accountService;

        public AuthController(AccountService accountService)
        {
            this.accountService = accountService;
        }

        [HttpGet("getall")]
        public async Task<IActionResult> GetAllAccounts()
        {
            try
            {
                var accounts = await accountService.GetAllAccountsAsync();
                return Ok(accounts);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }       

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
        {
            try 
            {
                await accountService.RegisterAsync(registerDto);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            try
            {
                var token = await accountService.LoginAsync(loginDto.Username, loginDto.Password);
                return Ok(token);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
