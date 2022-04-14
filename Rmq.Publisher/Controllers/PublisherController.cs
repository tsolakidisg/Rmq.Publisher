using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using Rmq.Publisher.Models;
using System.Collections.Concurrent;
using Newtonsoft.Json;
using System.Threading;
using Microsoft.Extensions.Configuration;

namespace Rmq.Publisher.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PublisherController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public PublisherController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpPost]
        public IActionResult Post([FromBody] OrderPublish order)
        {
            ConcurrentDictionary<string, OrderPublish> waitingRequests = new ConcurrentDictionary<string, OrderPublish>();

            //ConnectionFactory factory = new ConnectionFactory();
            //// "guest"/"guest" by default, limited to localhost connections
            //factory.HostName = "localhost";
            //factory.VirtualHost = "/";
            //factory.Port = 5672;
            //factory.UserName = "guest";
            //factory.Password = "guest";
            ConnectionFactory factory = new ConnectionFactory
            {
                HostName = _configuration["RabbitMqConnection:HostName"].ToString(),
                VirtualHost = _configuration["RabbitMqConnection:VirtualHost"].ToString(),
                Port = Convert.ToInt32(_configuration["RabbitMqConnection:Port"]),
                UserName = _configuration["RabbitMqConnection:Username"].ToString(),
                Password = _configuration["RabbitMqConnection:Password"].ToString()
            };

            IConnection conn = factory.CreateConnection();
            IModel channel = conn.CreateModel();

            string publishQueue = "publisherQueue";
            string consumeQueue = "consumerQueue";

            OrderConsume responseData = new OrderConsume();

            var consumer = new EventingBasicConsumer(channel);

            consumer.Received += (sender, e) =>
            {
                string messageData = Encoding.UTF8.GetString((byte[])e.BasicProperties.Headers["RequestId"]);
                OrderConsume response = JsonConvert.DeserializeObject<OrderConsume>(messageData);

                responseData = response;
            };

            channel.BasicConsume(consumeQueue, true, consumer);

            sendRequest(waitingRequests, channel, new OrderPublish(order.OrderID, order.OrderStatus), publishQueue);

            channel.Close();
            conn.Close();

            return StatusCode(StatusCodes.Status201Created);
        }

        private static void sendRequest(ConcurrentDictionary<string, OrderPublish> waitingRequest, IModel channel, OrderPublish request, string queueName)
        {
            string requestId = Guid.NewGuid().ToString();
            string requestData = JsonConvert.SerializeObject(request);

            waitingRequest[requestId] = request;

            var basicProperties = channel.CreateBasicProperties();
            basicProperties.Headers = new Dictionary<string, object>();
            basicProperties.Headers.Add("RequestId", Encoding.UTF8.GetBytes(requestId));

            channel.BasicPublish(
                "",
                queueName,
                basicProperties,
                Encoding.UTF8.GetBytes(requestData));
        }

    }
}
