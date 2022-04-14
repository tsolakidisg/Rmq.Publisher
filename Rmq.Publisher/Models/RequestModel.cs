using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Rmq.Publisher.Models
{
    public class RequestModel
    {
        public int OrderID { get; set; }
        public string OrderStatus { get; set; }

        public RequestModel()
        {

        }

        public RequestModel(int orderId, string orderStatus)
        {
            this.OrderID = orderId;
            this.OrderStatus = orderStatus;
        }
    }
}
