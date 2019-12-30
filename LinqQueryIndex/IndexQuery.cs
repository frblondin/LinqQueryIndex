using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace LinqQueryIndex
{
    public abstract class IndexQuery
    {
        public abstract IndexQueryValues QueryValues { get; }
        public abstract Expression Expression { get; }
        public abstract IEnumerable Enumerable { get; }
        internal abstract Expression QueryValuesExpression { get; }
    }

#pragma warning disable CA1710 // Identifiers should have correct suffix
    public sealed class IndexQuery<T> : IndexQuery, IOrderedQueryable<T>, IQueryable<T>, IEnumerable<T>, IEnumerable, IQueryable, IOrderedQueryable, IQueryProvider
#pragma warning restore CA1710 // Identifiers should have correct suffix
    {
        private IEnumerable<T> _enumerable;

        public IndexQuery(IReadOnlyList<T> values)
        {
            _enumerable = values ?? throw new ArgumentNullException(nameof(values));
            QueryValuesExpression = Expression = Expression.Constant(this);
            QueryValues = new IndexQueryValues<T>((IList)values);
        }

        private IndexQuery(IndexQueryValues queryValues, Expression queryValuesExpression, Expression expression)
        {
            Expression = expression ?? throw new ArgumentNullException(nameof(expression));
            QueryValuesExpression = queryValuesExpression ?? throw new ArgumentNullException(nameof(queryValuesExpression));
            QueryValues = queryValues ?? throw new ArgumentNullException(nameof(queryValues));
        }

        public override IndexQueryValues QueryValues { get; }
        internal override Expression QueryValuesExpression { get; }
        public override Expression Expression { get; }

        IQueryProvider IQueryable.Provider => this;

        Type IQueryable.ElementType => typeof(T);

        public override IEnumerable Enumerable => _enumerable;

        IQueryable IQueryProvider.CreateQuery(Expression expression) =>
            new IndexQuery<T>(QueryValues, QueryValuesExpression, expression);

        IQueryable<TElement> IQueryProvider.CreateQuery<TElement>(Expression expression) =>
            new IndexQuery<TElement>(QueryValues, QueryValuesExpression, expression);

        object IQueryProvider.Execute(Expression expression) => IndexExecutor.Create(expression).ExecuteBoxed();

        public TResult Execute<TResult>(Expression expression) => new IndexExecutor<TResult>(this, expression).Execute();

        public IEnumerator<T> GetEnumerator()
        {
            if (_enumerable == null)
            {
                _enumerable = IndexQueryCompiler.Compile<T>(QueryValues, QueryValuesExpression, Expression).Invoke();
            }
            return _enumerable.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public IndexQuery<T> AddIndexer<TProperty>(Expression<Func<T, TProperty>> indexer, IEqualityComparer<TProperty> comparer = default)
        {
            if (indexer is null)
            {
                throw new ArgumentNullException(nameof(indexer));
            }

            QueryValues.AddIndexer(indexer, comparer);
            return this;
        }
    }
}
