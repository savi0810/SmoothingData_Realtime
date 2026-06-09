using AuthSefviceApi.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthSefviceApi.Data.Repositorys
{
    public class AccountRepository
    {
        private readonly AppDbContext _context;

        public AccountRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAccountAsync(Account account)
        {
            await _context.Accounts.AddAsync(account);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Account>> GetAllAccountsAsync()
        {
            return await _context.Accounts.ToListAsync();
        }

        public async Task<Account?> GetAccountByUsernameAsync(string username)
        {
            return await _context.Accounts.FirstOrDefaultAsync(a => a.Username == username);
        }

        public async Task<Account?> GetAccountByEmailAsync(string email)
        {
            return await _context.Accounts.FirstOrDefaultAsync(a => a.Email == email);
        }
    }
}
