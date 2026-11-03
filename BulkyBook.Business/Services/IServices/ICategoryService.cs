using BulkyBook.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace BulkyBook.Business.Services.IServices
{
    public interface ICategoryService
    {
       Task<Category?> GetCategoryByIdAsync(int? id);
       Task<IEnumerable<Category>> GetAllCategoryAsync();
        Task CreateCategoryAsync(Category category);
        Task UpdateCategoryAsync(Category category);
        Task DeleteCategoryAsync(int id);
        Task<bool> IsCategoryNameUniqueAsync(string name, int? id = null);

    }
}
