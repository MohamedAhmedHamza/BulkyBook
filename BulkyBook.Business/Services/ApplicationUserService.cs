using BulkyBook.Business.Services.IServices;
using BulkyBook.Models;
using BulkyBookWeb.DataAccess.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace BulkyBook.Business.Services
{
    public class ApplicationUserService : IApplicationUserService
    {
        public readonly ApplicationDbContext _context;
        public ApplicationUserService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<ApplicationUser>> GetAllUserAsync()
        {
            return await _context.applicationUsers.ToListAsync();
        }

        public async Task<ApplicationUser?> GetUserByIdAsync(string userId)
        {
            return await _context.applicationUsers.FirstOrDefaultAsync(u => u.Id == userId);
        }
    }
}
