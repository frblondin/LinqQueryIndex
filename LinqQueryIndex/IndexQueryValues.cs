using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace LinqQueryIndex
{
    public abstract class IndexQueryValues
    {
        public IndexQueryValues(IList values)
        {
            Values = values ?? throw new ArgumentNullException(nameof(values));
        }

        public IList<Index> Indexes { get; } = new List<Index>();
        public IList Values { get; }
        public abstract Type ElementType { get; }

        internal Index<T> AddIndexer<T, TProperty>(Expression<Func<T, TProperty>> indexer, IEqualityComparer<TProperty> comparer = default)
        {
            var property = ExpressionReflector.GetProperty(indexer);
            var usedComparer = comparer ?? EqualityComparer<TProperty>.Default;
            var indexedValues = ((IEnumerable<T>)Values).ToLookup(indexer.Compile(), usedComparer);
            var result = new Index<T>(property, indexedValues, usedComparer);
            Indexes.Add(result);

            return result;
        }
    }

    internal class IndexQueryValues<T> : IndexQueryValues
    {
        public IndexQueryValues(IList values) : base(values)
        {
        }

        public override Type ElementType => typeof(T);
    }
}
