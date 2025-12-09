// 📄 File: Features/Catalog/Controllers/ProductController.cs

using Microsoft.AspNetCore.Mvc;
using xbytechat.api.Features.Catalog.DTOs;
using xbytechat.api.Features.Catalog.Services;
using xbytechat.api.Helpers;
using System;
using System.Linq;
using System.Threading.Tasks;
using xbytechat.api.Middleware.Attributes;

namespace xbytechat.api.Features.Catalog.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductController(IProductService productService)
        {
            _productService = productService;
        }

        // ✅ GET /api/product?businessId=...
        [HttpGet]
        [RequirePermission("ViewCatalog")]
        public async Task<IActionResult> GetAll([FromQuery] Guid businessId)
        {
            if (businessId == Guid.Empty)
                return BadRequest(ResponseResult.ErrorInfo("BusinessId is required."));

            var result = await _productService.GetProductsByBusinessIdAsync(businessId);
            return Ok(result);
        }

        // ✅ POST /api/product
        [HttpPost]
        [RequirePermission("ManageCatalog")]
        public async Task<IActionResult> Create([FromBody] CreateProductDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                var errorString = string.Join("; ", errors);
                return BadRequest(ResponseResult.ErrorInfo("Invalid product data.", errorString));
            }

            var result = await _productService.AddProductAsync(dto);
            if (!result.Success)
                return BadRequest(result);

            return StatusCode(201, result);
        }

        // ✅ DELETE /api/product/{id}?businessId=...
        [HttpDelete("{id}")]
        [RequirePermission("ManageCatalog")]
        public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid businessId)
        {
            if (businessId == Guid.Empty)
                return BadRequest(ResponseResult.ErrorInfo("BusinessId is required."));

            var result = await _productService.RemoveProductAsync(id, businessId);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        // ✅ PUT /api/product/{id}
        [HttpPut("{id}")]
        [RequirePermission("ManageCatalog")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductDto dto)
        {
            if (id != dto.Id)
                return BadRequest(ResponseResult.ErrorInfo("ID mismatch between route and body."));

            var result = await _productService.UpdateProductAsync(dto);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }
    }
}
