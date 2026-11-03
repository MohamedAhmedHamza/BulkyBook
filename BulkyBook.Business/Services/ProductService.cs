using BulkyBook.Business.Services.IServices;
using BulkyBook.Models;
using BulkyBookWeb.DataAccess.Data;
using System;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Text;

namespace BulkyBook.Business.Services
{
    public class ProductsService : IProductService
    {
        private readonly ApplicationDbContext _context;
        public ProductsService(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<IEnumerable<Product>> GetAllProductAsync(bool includeCategory=false)
        {
            if (includeCategory)
            {
                return await _context.Products.Include(p => p.Category).ToListAsync();
            }
            else
            {
                return await _context.Products.ToListAsync();
            }
        }

        public async Task<Product?> GetProductByIdAsync(int? id, bool includeCategory = false)
        {
            if (includeCategory)
            {
                return await _context.Products.Include(u=> u.Category).FirstOrDefaultAsync(u => u.Id == id);

            }
            else
            {
                return await _context.Products.FirstOrDefaultAsync(u => u.Id == id);

            }
        }
        
        public async Task CreateProductAsync(Product Products)
        {
            _context.Products.Add(Products);
            await _context.SaveChangesAsync();
        }
        public async Task UpdateProductAsync(Product Products)
        {
            _context.Products.Update(Products);
            await _context.SaveChangesAsync();
        }
        public async Task DeleteProductAsync(int id)
        {
            var Products = await _context.Products.FindAsync(id);
            if (Products == null)
            {
                throw new KeyNotFoundException($"Products with id {id} not found.");
            }
            _context.Products.Remove(Products);
            await _context.SaveChangesAsync();
        }
    }
}
