using System.Collections;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using NSubstitute;

namespace SentientArchitect.UnitTests.Helpers;

/// <summary>
/// Creates NSubstitute DbSet&lt;T&gt; fakes that support EF Core async LINQ operations
/// (ToListAsync, FirstOrDefaultAsync, etc.) backed by an in-memory list.
/// </summary>
internal static class AsyncDbSetHelper
{
    internal static DbSet<T> Create<T>(params T[] items) where T : class
        => Create(items.ToList());

    internal static DbSet<T> Create<T>(List<T> items) where T : class
    {
        var source  = new AsyncQueryableList<T>(items);
        var dbSet   = Substitute.For<DbSet<T>, IQueryable<T>, IAsyncEnumerable<T>>();

        ((IQueryable<T>)dbSet).Provider.Returns(source.Provider);
        ((IQueryable<T>)dbSet).Expression.Returns(source.Expression);
        ((IQueryable<T>)dbSet).ElementType.Returns(source.ElementType);
        ((IQueryable<T>)dbSet).GetEnumerator().Returns(source.GetEnumerator());
        ((IAsyncEnumerable<T>)dbSet)
            .GetAsyncEnumerator(Arg.Any<CancellationToken>())
            .Returns(_ => source.GetAsyncEnumerator());

        return dbSet;
    }

    // ── Internal async-capable queryable ─────────────────────────────────────

    private sealed class AsyncQueryableList<T> : IOrderedQueryable<T>, IAsyncEnumerable<T>
    {
        private readonly List<T> _list;
        private readonly IQueryable<T> _inner;

        public AsyncQueryableList(List<T> list)
        {
            _list  = list;
            _inner = list.AsQueryable();
            Provider = new AsyncListQueryProvider<T>(_inner.Provider);
        }

        public IQueryProvider Provider { get; }
        public Expression Expression   => _inner.Expression;
        public Type ElementType        => _inner.ElementType;

        public IEnumerator<T> GetEnumerator()           => _list.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator()         => _list.GetEnumerator();
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken _ = default)
            => new AsyncListEnumerator<T>(_list.GetEnumerator());
    }

    // ── Async-aware query provider ────────────────────────────────────────────

    private sealed class AsyncListQueryProvider<T> : IAsyncQueryProvider
    {
        private readonly IQueryProvider _inner;
        public AsyncListQueryProvider(IQueryProvider inner) => _inner = inner;

        public IQueryable CreateQuery(Expression expression)
            => WrapQuery<T>(expression);

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
            => WrapQuery<TElement>(expression);

        public object? Execute(Expression expression)            => _inner.Execute(expression);
        public TResult Execute<TResult>(Expression expression)  => _inner.Execute<TResult>(expression);

        /// <summary>
        /// EF Core calls this for FirstOrDefaultAsync, SingleOrDefaultAsync, etc.
        /// TResult is Task&lt;TValue&gt;; we execute synchronously and wrap with Task.FromResult.
        /// </summary>
        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken ct = default)
        {
            var valueType = typeof(TResult).GetGenericArguments()[0];
            var executeMethod = typeof(IQueryProvider)
                .GetMethod(nameof(IQueryProvider.Execute), 1, [typeof(Expression)])!
                .MakeGenericMethod(valueType);
            var value = executeMethod.Invoke(_inner, [expression]);
            var fromResult = typeof(Task)
                .GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(valueType);
            return (TResult)fromResult.Invoke(null, [value])!;
        }

        // Wrap a materialized expression into a new async-capable queryable.
        private IQueryable<TElement> WrapQuery<TElement>(Expression expression)
        {
            // Materialize using the inner LINQ-to-Objects provider so filtering, ordering, etc. apply.
            var innerQuery = _inner.CreateQuery<TElement>(expression);
            return new AsyncQueryableList<TElement>(innerQuery.ToList());
        }
    }

    // ── Async enumerator over a plain list enumerator ─────────────────────────

    private sealed class AsyncListEnumerator<T>(IEnumerator<T> inner) : IAsyncEnumerator<T>
    {
        public T Current => inner.Current;

        public ValueTask<bool> MoveNextAsync() => new(inner.MoveNext());

        public ValueTask DisposeAsync()
        {
            inner.Dispose();
            return default;
        }
    }
}
