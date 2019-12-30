using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace LinqQueryIndex
{
    public static class IndexQueryExtensions
    {
        public static IndexQuery<T> AsIndexQueryable<T>(this IReadOnlyList<T> source, params Expression<Func<T, object>>[] indexers)
        {
            var result = new IndexQuery<T>(source);
            foreach (var indexer in indexers)
            {
                result.AddIndexer(indexer);
            }
            return result;
        }
    }
}
