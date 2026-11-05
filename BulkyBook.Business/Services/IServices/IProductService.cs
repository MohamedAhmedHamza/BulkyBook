using BulkyBook.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace BulkyBook.Business.Services.IServices
{
    public interface IProductService
    {
        Task<Product?> GetProductByIdAsync(int? id, bool includeCategory = false);
        Task<IEnumerable<Product>> GetAllProductAsync(bool includeCategory=false);
        Task<(IEnumerable<Product> Items, int TotalCount)> SearchAsync(string? search, int? categoryId, string? sort, int page, int pageSize);
        Task CreateProductAsync(Product Product);
        Task UpdateProductAsync(Product Product);
        Task DeleteProductAsync(int id);
    }
}
