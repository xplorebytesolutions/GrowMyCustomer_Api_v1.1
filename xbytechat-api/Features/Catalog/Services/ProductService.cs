using Microsoft.EntityFrameworkCore;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using xbytechat.api.Features.Catalog.DTOs;
using xbytechat.api.Features.Catalog.Models;
using xbytechat.api.Helpers;

namespace xbytechat.api.Features.Catalog.Services
{
    public class ProductService : IProductService
    {
        private readonly AppDbContext _context;

        public ProductService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ResponseResult> AddProductAsync(CreateProductDto dto)
        {
            try
            {
                var exists = await _context.Products
                    .AnyAsync(p => p.BusinessId == dto.BusinessId && p.Name == dto.Name);

                if (exists)
                {
                    Log.Warning("❌ Duplicate product add attempt: {ProductName} for BusinessId: {BusinessId}", dto.Name, dto.BusinessId);
                    return ResponseResult.ErrorInfo("Product with this name already exists.");
                }

                var newProduct = new Product
                {
                    Id = Guid.NewGuid(),
                    BusinessId = dto.BusinessId,
                    Name = dto.Name,
                    Description = dto.Description,
                    Price = dto.Price,
                    Currency = dto.Currency,
                    ImageUrl = dto.ImageUrl,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.Products.AddAsync(newProduct);
                await _context.SaveChangesAsync();

                Log.Information("✅ Product created: {ProductName} ({ProductId})", dto.Name, newProduct.Id);
                return ResponseResult.SuccessInfo("✅ Product added successfully.", newProduct.Id);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Failed to add product: {ProductName}", dto.Name);
                return ResponseResult.ErrorInfo("Failed to add product.", ex.Message);
            }
        }

        public async Task<ResponseResult> RemoveProductAsync(Guid id, Guid businessId)
        {
            try
            {
                var product = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == id && p.BusinessId == businessId);

                if (product == null)
                {
                    Log.Warning("❌ Attempted to delete non-existent product: {ProductId}", id);
                    return ResponseResult.ErrorInfo("Product not found.");
                }

                _context.Products.Remove(product);
                await _context.SaveChangesAsync();

                Log.Information("🗑️ Product deleted: {ProductId}", id);
                return ResponseResult.SuccessInfo("🗑️ Product deleted.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Failed to delete product: {ProductId}", id);
                return ResponseResult.ErrorInfo("Failed to delete product.", ex.Message);
            }
        }

        public async Task<ResponseResult> UpdateProductAsync(UpdateProductDto dto)
        {
            try
            {
                var product = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == dto.Id && p.BusinessId == dto.BusinessId);

                if (product == null)
                {
                    Log.Warning("❌ Attempted to update non-existent product: {ProductId}", dto.Id);
                    return ResponseResult.ErrorInfo("Product not found.");
                }

                product.Name = dto.Name;
                product.Description = dto.Description;
                product.Price = dto.Price;
                product.Currency = dto.Currency;
                product.ImageUrl = dto.ImageUrl;

                await _context.SaveChangesAsync();

                Log.Information("✅ Product updated: {ProductId}", dto.Id);
                return ResponseResult.SuccessInfo("✅ Product updated.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Failed to update product: {ProductId}", dto.Id);
                return ResponseResult.ErrorInfo("Failed to update product.", ex.Message);
            }
        }

        public async Task<ResponseResult> GetProductsByBusinessIdAsync(Guid businessId)
        {
            try
            {
                var products = await _context.Products
                    .Where(p => p.BusinessId == businessId && p.IsActive)
                    .Select(p => new ProductDto
                    {
                        Id = p.Id,
                        BusinessId = p.BusinessId,
                        Name = p.Name,
                        Description = p.Description,
                        Price = p.Price,
                        Currency = p.Currency,
                        ImageUrl = p.ImageUrl,
                        IsActive = p.IsActive
                    })
                    .ToListAsync();

                return ResponseResult.SuccessInfo("Products fetched.", products);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Failed to fetch products for BusinessId: {BusinessId}", businessId);
                return ResponseResult.ErrorInfo("Failed to fetch products.", ex.Message);
            }
        }
    }
}
