using BulkyBook.Business.Services.IServices;
using BulkyBook.Models;
using BulkyBookWeb.DataAccess.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace BulkyBook.Business.Services
{
    public class ShoppingCartService : IShoppingCartService
    {
        public readonly ApplicationDbContext _context;
        public ShoppingCartService(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<ShoppingCart?> GetCartByIdAsync(int cartId)
        {
            return await _context.ShoppingCarts.Include(u=> u.Product).FirstOrDefaultAsync(u=> u.Id == cartId);
        }
        public async Task<IEnumerable<ShoppingCart>> GetUserCartItemsAsync(string userId)
        {
            return await _context.ShoppingCarts.Include(u => u.Product).Where(u => u.ApplicationUserId == userId).ToListAsync();
        }
        public async Task<int> GetCartCountAsync(string userId)
        {
            return await _context.ShoppingCarts.Where(u => u.ApplicationUserId == userId).SumAsync(u => u.Count);
        }
        public async Task ClearAsCartAsync(string userId)
        {
            var cartItems = _context.ShoppingCarts
                .Include(u => u.Product)
                .Where(u => u.ApplicationUserId == userId);
            if (cartItems.Any())
            {
                _context.ShoppingCarts.RemoveRange(cartItems);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<ShoppingCart> AddToCartAsync(ShoppingCart cart)
        {
            var existingCartItem = await _context.ShoppingCarts
                .FirstOrDefaultAsync(u => u.ApplicationUserId == cart.ApplicationUserId && u.ProductId == cart.ProductId);
            if (existingCartItem != null)
            {   
                existingCartItem.Count += cart.Count;
                await _context.SaveChangesAsync();
                return existingCartItem;
            }
            else
            {
                _context.ShoppingCarts.Add(cart);
                await _context.SaveChangesAsync();
                return cart;
            }
        }
        
        public async Task UpdateCartAsync(ShoppingCart cart)
        {
            if(cart.Count <= 0)
            {
                _context.ShoppingCarts.Remove(cart);
            }
            else
            {
                _context.ShoppingCarts.Update(cart);
            }
            await _context.SaveChangesAsync();
        }
    }
}
