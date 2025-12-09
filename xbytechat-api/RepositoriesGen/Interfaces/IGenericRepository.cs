using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace xbytechat.api.Repositories.Interfaces
{
    public interface IGenericRepository<T> where T : class
    {
        // 🔍 Basic Reads
        Task<IEnumerable<T>> GetAllAsync();
        Task<T?> FindByIdAsync(Guid id);
        Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);
        Task<IEnumerable<T>> FindAllAsync(Expression<Func<T, bool>> predicate);

        // 🔐 Checks
        Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate);

        // ✍️ Commands
        Task AddAsync(T entity);
        void Update(T entity);
        void Delete(T entity);
        Task SaveAsync();

        Task<List<T>> WhereAsync(Expression<Func<T, bool>> predicate);

        // ✅ New: Supports advanced LINQ operations like Include()
        IQueryable<T> AsQueryable();
    }
}
