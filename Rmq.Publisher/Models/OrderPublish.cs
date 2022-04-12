using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Rmq.Publisher.Models
{
    public class OrderPublish
    {
        public int OrderID { get; set; }
        public string OrderStatus { get; set; }

        public OrderPublish()
        {

        }

        public OrderPublish(int orderId, string orderStatus)
        {
            this.OrderID = orderId;
            this.OrderStatus = orderStatus;
        }
    }
}
