using System;
using System.Linq.Expressions;
using System.Reflection;

namespace LinqQueryIndex
{
    public abstract class IndexExecutor
    {
        internal abstract object ExecuteBoxed();

        internal static IndexExecutor Create(Expression expression)
        {
            Type type = typeof(IndexExecutor<>).MakeGenericType(new Type[]
            {
                expression.Type
            });
            return (IndexExecutor)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new object[]
            {
                expression
            }, null);
        }

        protected IndexExecutor()
        {
        }
    }
}
