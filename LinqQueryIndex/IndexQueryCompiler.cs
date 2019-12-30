using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace LinqQueryIndex
{
    public static class IndexQueryCompiler
    {
        public static Func<IEnumerable<TResult>> Compile<TIndex, TResult>(this TIndex index, Expression<Func<IEnumerable<TResult>>> query)
            where TIndex : IndexQuery
        {
            if (index is null)
            {
                throw new ArgumentNullException(nameof(index));
            }

            if (query is null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            return index.CompileInternal<TIndex, TResult, Func<IEnumerable<TResult>>>(query);
        }

        public static Func<TArg0, IEnumerable<TResult>> Compile<TIndex, TArg0, TResult>(this TIndex index, Expression<Func<TArg0, IEnumerable<TResult>>> query)
            where TIndex : IndexQuery
        {
            if (index is null)
            {
                throw new ArgumentNullException(nameof(index));
            }

            if (query is null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            return index.CompileInternal<TIndex, TResult, Func<TArg0, IEnumerable<TResult>>>(query);
        }

        private static TFunc CompileInternal<TIndex, TResult, TFunc>(this TIndex index, Expression<TFunc> query)
            where TIndex : IndexQuery
        {
            var body = Rewrite<TResult>(index.QueryValues, index.QueryValuesExpression, query.Body);
            var invokeMethod = body.Type.GetMethod("Invoke");
            var lambda = Expression.Lambda<TFunc>(
                Expression.Call(body, invokeMethod),
                query.Parameters);
            return lambda.Compile();
        }

        internal static Func<IEnumerable<T>> Compile<T>(IndexQueryValues queryValues, Expression queryValuesExpression, Expression expression)
        {
            return Rewrite<T>(queryValues, queryValuesExpression, expression).Compile();
        }

        internal static Expression<Func<IEnumerable<T>>> Rewrite<T>(IndexQueryValues queryValues, Expression queryValuesExpression, Expression expression)
        {
            var enumerableRewriter = new IndexExpressionRewriter(queryValues, queryValuesExpression);
            var body = enumerableRewriter.Visit(expression);
            return Expression.Lambda<Func<IEnumerable<T>>>(body, null);
        }
    }
}
