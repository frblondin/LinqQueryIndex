using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace LinqIndexTest
{
    public class Order
    {
        public Order(string customerID, string orderNumber)
        {
            CustomerID = customerID ?? throw new ArgumentNullException(nameof(customerID));
            OrderNumber = orderNumber ?? throw new ArgumentNullException(nameof(orderNumber));
        }

        public string CustomerID { get; }

        public string OrderNumber { get; }
    }
}
