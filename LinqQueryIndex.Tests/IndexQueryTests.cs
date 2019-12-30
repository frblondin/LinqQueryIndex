using LinqIndexTest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LinqQueryIndex;
using System.Diagnostics;
using NUnit.Framework;
using System.Collections.Immutable;

namespace LinqIndex.Tests
{
    public class IndexQueryTests
    {
        IImmutableList<Order> _orders;

        [Test]
        public void WhereTest()
        {
            var withoutIndex = WithoutIndex();
            var withIndex = UseIndex();
            Console.WriteLine($"Without index: {withoutIndex}\nWith index: {withIndex}");
        }

        private TimeSpan UseIndex()
        {
            var indexed = _orders.AsIndexQueryable()
                .AddIndexer(o => o.CustomerID);
            var stopWatch = Stopwatch.StartNew();
            var result1 = (from o in indexed
                           where o.CustomerID == "GAU1"
                           select o.OrderNumber).ToList();
            return stopWatch.Elapsed;
        }

        private TimeSpan WithoutIndex()
        {
            var stopWatch = Stopwatch.StartNew();
            var result1 = (from o in _orders
                           where o.CustomerID == "GAU1"
                           select o.OrderNumber).ToList();
            return stopWatch.Elapsed;
        }

        [Test]
        public void WhereCacheTest()
        {
            // Arrange
            var indexed = _orders.AsIndexQueryable()
                .AddIndexer(o => o.CustomerID, StringComparer.OrdinalIgnoreCase);
            var query = indexed.Compile((string id) => from o in indexed
                                                       where o.CustomerID == id
                                                       select o.OrderNumber);

            // Act
            Assert.AreEqual(_orders[0].OrderNumber, query(_orders[0].CustomerID).FirstOrDefault());
            Assert.AreEqual(null, query("unexisting").FirstOrDefault());

            // Assert
            Assert.AreEqual(2, indexed.QueryValues.Indexes[0].Hit);
        }

        [Test]
        public void FirstTest()
        {
            // Arrange
            var indexed = _orders.AsIndexQueryable()
                .AddIndexer(o => o.CustomerID);

            // Act
            indexed.First(o => o.CustomerID == _orders[0].CustomerID);

            // Assert
            Assert.AreEqual(1, indexed.QueryValues.Indexes[0].Hit);
        }

        [Test]
        public void FirstOrDefaultTest()
        {
            // Arrange
            var indexed = _orders.AsIndexQueryable()
                .AddIndexer(o => o.CustomerID);

            // Act
            indexed.FirstOrDefault(o => o.CustomerID == _orders[0].CustomerID);

            // Assert
            Assert.AreEqual(1, indexed.QueryValues.Indexes[0].Hit);
        }

        [Test]
        public void Grouping1Test()
        {
            // Arrange
            var indexed = _orders.AsIndexQueryable()
                .AddIndexer(o => o.CustomerID);

            // Act
            indexed.GroupBy(o => o.CustomerID).ToList();

            // Assert
            Assert.AreEqual(1, indexed.QueryValues.Indexes[0].Hit);
        }

        [Test]
        public void Grouping2Test()
        {
            // Arrange
            var indexed = _orders.AsIndexQueryable()
                .AddIndexer(o => o.CustomerID);

            // Act
            indexed.GroupBy(o => o.CustomerID, o => o.OrderNumber).ToList();

            // Assert
            Assert.AreEqual(1, indexed.QueryValues.Indexes[0].Hit);
        }

        [Test]
        public void Grouping2SameComparerTest()
        {
            // Arrange
            var indexed = _orders.AsIndexQueryable()
                .AddIndexer(o => o.CustomerID, StringComparer.OrdinalIgnoreCase);

            // Act
            indexed.GroupBy(o => o.CustomerID, o => o.OrderNumber, StringComparer.OrdinalIgnoreCase).ToList();

            // Assert
            Assert.AreEqual(1, indexed.QueryValues.Indexes[0].Hit);
        }

        [Test]
        public void Grouping2AnotherComparerTest()
        {
            // Arrange
            var indexed = _orders.AsIndexQueryable()
                .AddIndexer(o => o.CustomerID, StringComparer.OrdinalIgnoreCase);

            // Act
            indexed.GroupBy(o => o.CustomerID, o => o.OrderNumber, StringComparer.Ordinal).ToList();

            // Assert
            Assert.AreEqual(0, indexed.QueryValues.Indexes[0].Hit);
        }

        [OneTimeSetUp]
        public void CreateSample()
        {
            var result = ImmutableList.CreateBuilder<Order>();
            for (int i = 0; i < 10000; i++)
            {
                result.Add(new Order("GOT" + i, "Order" + i));
                result.Add(new Order("GOT" + i, "Order" + i * 2));
                result.Add(new Order("GOT" + i, "Order" + i * 3));
                result.Add(new Order("GOT" + i, "Order" + i * 4));
                result.Add(new Order("DEA" + i, "Order" + i));
                result.Add(new Order("DEA" + i, "Order" + i * 2));
                result.Add(new Order("DEA" + i, "Order" + i * 3));
                result.Add(new Order("DEA" + i, "Order" + i * 4));
                result.Add(new Order("GAU" + i, "Order" + i));
                result.Add(new Order("GAU" + i, "Order" + i * 2));
                result.Add(new Order("GAU" + i, "Order" + i * 3));
                result.Add(new Order("GAU" + i, "Order" + i * 4));
                result.Add(new Order("ZEE" + i, "Order" + i));
                result.Add(new Order("ZEE" + i, "Order" + i * 2));
                result.Add(new Order("ZEE" + i, "Order" + i * 3));
                result.Add(new Order("ZEE" + i, "Order" + i * 4));
                result.Add(new Order("GOT" + i, "Order" + i));
                result.Add(new Order("GOT" + i, "Order" + i * 2));
                result.Add(new Order("GOT" + i, "Order" + i * 3));
                result.Add(new Order("GOT" + i, "Order" + i * 4));
                result.Add(new Order("VAL" + i, "Order" + i));
                result.Add(new Order("VAL" + i, "Order" + i * 2));
                result.Add(new Order("VAL" + i, "Order" + i * 3));
                result.Add(new Order("VAL" + i, "Order" + i * 4));
            }
            _orders = result.ToImmutable();
        }
    }
}
