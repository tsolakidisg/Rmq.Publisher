﻿using Microsoft.AspNetCore.Http;
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

        [HttpGet]
        public IActionResult Get()
        {
            // Create a connection factory, using the RabbitMQ configuration settings from appsettings.json
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

            // Queue name to retrieve from
            string consumeQueue = "consumerQueue";

            // Initialize the message string and the number of retries
            string message = string.Empty;
            int retries;
            // Max 3 retries to read from the queue
            for (retries = 1; retries <= 3; retries++)
            {
                try
                {
                    // Get the message from the queue without acknowledging it yet
                    BasicGetResult result = channel.BasicGet(consumeQueue, false);
                    if (result != null)
                    {
                        // Decode the received message, deserialize it into a JSON object and acknowledge its processing
                        message = Encoding.UTF8.GetString(result.Body.Span);
                        ResponseModel response = JsonConvert.DeserializeObject<ResponseModel>(message);
                        channel.BasicAck(result.DeliveryTag, false);
                        retries = 10; // Set retries to 10 (>3) to exit the loop and use it later as a flag to depict the message receival
                    }
                }
                catch (Exception e)
                {
                    throw e;
                }

            }

            if (retries >= 10)
            {
                // The message was retrieved successfully
                return Ok(message);
            }
            else
            {
                // The message was not found
                return NotFound("Order not found (Max retries: 3 reached).");
            }
        }

        [HttpPost]
        public IActionResult Post([FromBody] RequestModel order)
        {
            // Create a Dictionary to cover the request format => Header: RequestId(string), Payload: Object(RequestModel) 
            ConcurrentDictionary<string, RequestModel> pendingRequests = new ConcurrentDictionary<string, RequestModel>();

            // Create a connection factory, using the RabbitMQ configuration settings from appsettings.json
            ConnectionFactory factory = new ConnectionFactory
            {
                HostName = _configuration["RabbitMqConnection:HostName"].ToString(),
                VirtualHost = _configuration["RabbitMqConnection:VirtualHost"].ToString(),
                Port = Convert.ToInt32(_configuration["RabbitMqConnection:Port"]),
                UserName = _configuration["RabbitMqConnection:Username"].ToString(),
                Password = _configuration["RabbitMqConnection:Password"].ToString()
            };

            IConnection connection = factory.CreateConnection();
            IModel channel = connection.CreateModel();

            // Queue name for publish
            string publishQueue = "publisherQueue";

            SendRequest(pendingRequests, channel, new RequestModel(order.OrderID, order.OrderStatus), publishQueue);

            channel.Close();
            connection.Close();

            return StatusCode(StatusCodes.Status201Created);
        }

        private static void SendRequest(ConcurrentDictionary<string, RequestModel> pendingRequest, IModel channel, RequestModel request, string queueName)
        {
            // Generate a guid to use as header in the RequestId filed
            string requestId = Guid.NewGuid().ToString();
            // Serialize the request as a JSON object
            string requestData = JsonConvert.SerializeObject(request);

            pendingRequest[requestId] = request;

            var basicProperties = channel.CreateBasicProperties();
            // Create the Header field RequestId in order to use it in the request
            basicProperties.Headers = new Dictionary<string, object>
            {
                { "RequestId", Encoding.UTF8.GetBytes(requestId) }
            };

            channel.BasicPublish(
                string.Empty, // Use the default exchange
                queueName, 
                basicProperties,
                Encoding.UTF8.GetBytes(requestData) // Encode the request message as Bytes
                );
        }

    }
}
