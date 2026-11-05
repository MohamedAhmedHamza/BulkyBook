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

        public async Task<(IEnumerable<Product> Items, int TotalCount)> SearchAsync(string? search, int? categoryId, string? sort, int page, int pageSize)
        {
            var query = _context.Products.AsNoTracking().Include(p => p.Category).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(p => p.Title.Contains(term) || p.Author.Contains(term) || p.ISBN.Contains(term));
            }
            if (categoryId.HasValue)
            {
                query = query.Where(p => p.CategoryId == categoryId.Value);
            }

            var total = await query.CountAsync();

            query = sort switch
            {
                "price_asc" => query.OrderBy(p => p.Price),
                "price_desc" => query.OrderByDescending(p => p.Price),
                "title" => query.OrderBy(p => p.Title),
                _ => query.OrderByDescending(p => p.Id)
            };

            if (page < 1) page = 1;
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return (items, total);
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
