using System;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using xbytechat.api.Features.Catalog.Models;
using xbytechat.api.Features.Catalog.Services;
using xbytechat.api.Features.Catalog.DTOs;
using xbytechat.api.Helpers;
using xbytechat.api;

namespace xbytechat.Tests.Features.Catalog.Services
{
    public class ProductServiceTests
    {
        // Setup helper method for in-memory DB context
        private AppDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new AppDbContext(options);
        }

        [Fact]
        public async Task AddProductAsync_Should_Add_Product_And_Return_Success()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new ProductService(context);

            var dto = new CreateProductDto
            {
                BusinessId = Guid.NewGuid(),
                Name = "Test Product",
                Description = "Test Desc",
                Price = 99.99m,
                Currency = "INR",
                ImageUrl = "img.jpg"
            };

            // Act
            var result = await service.AddProductAsync(dto);

            // Assert
            Assert.True(result.Success);
            Assert.Contains(context.Products, p => p.Name == "Test Product");
        }

        // More tests for update, delete, duplicate etc. can be added here
        [Fact]
        public async Task AddProductAsync_Should_Return_Error_If_Duplicate()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString(); // Unique DB per test run
            var dbContext = GetInMemoryDbContext();
            var service = new ProductService(dbContext);

            var businessId = Guid.NewGuid();
            var productName = "Test Product";

            // Add the first product
            var createDto = new CreateProductDto
            {
                BusinessId = businessId,
                Name = productName,
                Description = "First product",
                Price = 100,
                Currency = "INR",
                ImageUrl = ""
            };
            var firstResult = await service.AddProductAsync(createDto);
            Assert.True(firstResult.Success);

            // Act: Try to add a duplicate product (same name, same business)
            var duplicateDto = new CreateProductDto
            {
                BusinessId = businessId,
                Name = productName, // same name!
                Description = "Duplicate product",
                Price = 200,
                Currency = "INR",
                ImageUrl = ""
            };
            var duplicateResult = await service.AddProductAsync(duplicateDto);

            // Assert
            Assert.False(duplicateResult.Success);
            Assert.Contains("already exists", duplicateResult.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task AddProductAsync_Should_Fail_When_Product_Name_Already_Exists_For_Business()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new ProductService(context);

            var businessId = Guid.NewGuid();
            var dto = new CreateProductDto
            {
                BusinessId = businessId,
                Name = "Test Product",
                Description = "Test Desc",
                Price = 99.99m,
                Currency = "INR",
                ImageUrl = "img.jpg"
            };

            // Add first product
            var firstResult = await service.AddProductAsync(dto);
            Assert.True(firstResult.Success);

            // Try to add duplicate
            var secondResult = await service.AddProductAsync(dto);

            // Assert
            Assert.False(secondResult.Success);
            Assert.Contains("already exists", secondResult.Message ?? "", StringComparison.OrdinalIgnoreCase);
        }

   
        [Fact]
        public async Task RemoveProductAsync_Should_Delete_Product_And_Return_Success()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new ProductService(context);

            var businessId = Guid.NewGuid();

            // Add product to delete later
            var createDto = new CreateProductDto
            {
                BusinessId = businessId,
                Name = "Delete Me",
                Description = "To be deleted",
                Price = 10.0m,
                Currency = "INR",
                ImageUrl = "delete.jpg"
            };
            var addResult = await service.AddProductAsync(createDto);
            Assert.True(addResult.Success);

            var product = await context.Products.FirstOrDefaultAsync(p => p.BusinessId == businessId && p.Name == "Delete Me");
            Assert.NotNull(product);

            // Act
            var deleteResult = await service.RemoveProductAsync(product.Id, businessId);

            // Assert
            Assert.True(deleteResult.Success);
            var deleted = await context.Products.FindAsync(product.Id);
            Assert.Null(deleted);
        }

        [Fact]
        public async Task RemoveProductAsync_Should_Return_Error_If_Product_Not_Found()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new ProductService(context);

            var fakeProductId = Guid.NewGuid();
            var fakeBusinessId = Guid.NewGuid();

            // Act
            var result = await service.RemoveProductAsync(fakeProductId, fakeBusinessId);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
        }
        [Fact]
        public async Task UpdateProductAsync_Should_Update_Product_And_Return_Success()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new ProductService(context);

            // First, add a product to update
            var businessId = Guid.NewGuid();
            var product = new Product
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                Name = "Original",
                Description = "Old",
                Price = 10,
                Currency = "INR",
                ImageUrl = "old.jpg",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            context.Products.Add(product);
            await context.SaveChangesAsync();

            var updateDto = new UpdateProductDto
            {
                Id = product.Id,
                BusinessId = businessId,
                Name = "Updated",
                Description = "Updated Desc",
                Price = 20,
                Currency = "USD",
                ImageUrl = "new.jpg"
            };

            // Act
            var result = await service.UpdateProductAsync(updateDto);

            // Assert
            Assert.True(result.Success);
            var updated = await context.Products.FindAsync(product.Id);
            Assert.Equal("Updated", updated.Name);
            Assert.Equal("Updated Desc", updated.Description);
            Assert.Equal(20, updated.Price);
            Assert.Equal("USD", updated.Currency);
            Assert.Equal("new.jpg", updated.ImageUrl);
        }
        [Fact]
        public async Task UpdateProductAsync_Should_Return_Error_If_Product_Not_Found()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new ProductService(context);

            var updateDto = new UpdateProductDto
            {
                Id = Guid.NewGuid(), // Non-existent ID
                BusinessId = Guid.NewGuid(),
                Name = "Doesn't Matter",
                Description = "",
                Price = 0,
                Currency = "",
                ImageUrl = ""
            };

            // Act
            var result = await service.UpdateProductAsync(updateDto);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetProductsByBusinessIdAsync_Should_Return_Products_For_Business()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new ProductService(context);

            var businessId = Guid.NewGuid();

            // Seed with products for this business
            context.Products.AddRange(
                new Product
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    Name = "Product 1",
                    Description = "Desc 1",
                    Price = 10,
                    Currency = "INR",
                    ImageUrl = "1.jpg",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new Product
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    Name = "Product 2",
                    Description = "Desc 2",
                    Price = 20,
                    Currency = "INR",
                    ImageUrl = "2.jpg",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                // Product for another business (should not be returned)
                new Product
                {
                    Id = Guid.NewGuid(),
                    BusinessId = Guid.NewGuid(),
                    Name = "Other Business",
                    Description = "",
                    Price = 5,
                    Currency = "INR",
                    ImageUrl = "other.jpg",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                }
            );
            await context.SaveChangesAsync();

            // Act
            var result = await service.GetProductsByBusinessIdAsync(businessId);

            // Assert
            Assert.True(result.Success);
            var list = Assert.IsAssignableFrom<System.Collections.IEnumerable>(result.Data);
            var products = ((System.Collections.Generic.IEnumerable<object>)list).Cast<dynamic>().ToList();

            Assert.Equal(2, products.Count);
            Assert.All(products, p => Assert.Equal(businessId, (Guid)p.BusinessId));
        }
        [Fact]
        public async Task GetProductsByBusinessIdAsync_Should_Return_Empty_If_None()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new ProductService(context);

            var businessId = Guid.NewGuid(); // No products for this ID

            // Act
            var result = await service.GetProductsByBusinessIdAsync(businessId);

            // Assert
            Assert.True(result.Success);
            var list = Assert.IsAssignableFrom<System.Collections.IEnumerable>(result.Data);
            Assert.Empty(list);
        }

    }
}
