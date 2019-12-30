using System;
using System.Linq.Expressions;

namespace LinqQueryIndex
{
    public class IndexExecutor<T> : IndexExecutor
    {
        public IndexExecutor(IndexQuery indexQuery, Expression expression)
        {
            Expression = expression;
            IndexQuery = indexQuery;
        }

        public Expression Expression { get; }
        public IndexQuery IndexQuery { get; }

        private Func<T> _func;

        internal override object ExecuteBoxed()
        {
            return Execute();
        }
        internal T Execute()
        {
            if (_func == null)
            {
                var enumerableRewriter = new IndexExpressionRewriter(IndexQuery.QueryValues, IndexQuery.QueryValuesExpression);
                var body = enumerableRewriter.Visit(Expression);
                var expression = Expression.Lambda<Func<T>>(body, null);
                _func = expression.Compile();
            }
            return _func();
        }
    }
}
