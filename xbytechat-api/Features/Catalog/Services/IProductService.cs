using System;
using System.Threading.Tasks;
using xbytechat.api.Features.Catalog.DTOs;
using xbytechat.api.Helpers;

namespace xbytechat.api.Features.Catalog.Services
{
    public interface IProductService
    {
        Task<ResponseResult> AddProductAsync(CreateProductDto dto);
        Task<ResponseResult> RemoveProductAsync(Guid id, Guid businessId);
        Task<ResponseResult> UpdateProductAsync(UpdateProductDto dto);
        Task<ResponseResult> GetProductsByBusinessIdAsync(Guid businessId);
    }
}
