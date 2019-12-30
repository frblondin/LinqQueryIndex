using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace LinqQueryIndex
{
    public abstract class Index
    {
        public long Hit { get; protected set; }

        public abstract PropertyInfo Property { get; }

        internal abstract object Comparer { get; }

        internal bool Matches(PropertyInfo property, object comparer) =>
            Property == property && Comparer == comparer;
    }

    public class Index<T> : Index
    {
        public Index(PropertyInfo property, IEnumerable values, object comparer)
        {
            Property = property ?? throw new ArgumentNullException(nameof(property));
            Comparer = comparer;
            Values = values ?? throw new ArgumentNullException(nameof(values));
        }

        public override PropertyInfo Property { get; }

        internal override object Comparer { get; }

        public IEnumerable Values { get; }

        public IEnumerable<T> Where<TProperty>(TProperty value)
        {
            Hit++;
            var lookup = (ILookup<TProperty, T>)Values;
            return lookup[value];
        }

        public IEnumerable<IGrouping<TProperty, TSource>> GroupBy<TSource, TProperty>()
        {
            Hit++;
            return (ILookup<TProperty, TSource>)Values;
        }

        public T First<TProperty>(TProperty value) => Where(value).First();

        public T FirstOrDefault<TProperty>(TProperty value) => Where(value).FirstOrDefault();

        public IEnumerable<IGrouping<TProperty, TElement>> GroupBy<TSource, TProperty, TElement>(Func<TSource, TElement> elementSelector) =>
            GroupBy<TSource, TProperty>().Select(g => new Grouping<TProperty, TElement>(g.Key, g.Select(elementSelector)));

        private class Grouping<TKey, TElement> : IGrouping<TKey, TElement>
        {
            public Grouping(TKey key, IEnumerable<TElement> enumerable)
            {
                Key = key;
                Enumerable = enumerable ?? throw new ArgumentNullException(nameof(enumerable));
            }

            public TKey Key { get; }

            public IEnumerable<TElement> Enumerable { get; }

            public IEnumerator<TElement> GetEnumerator() => Enumerable.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => Enumerable.GetEnumerator();
        }
    }
}
