using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using RabbitMQ.Client;

namespace Master
{
    public class ProducerQueue
    {
        
        
        public static void puplish(IModel channel,string q,Object massage)
        {
            channel.QueueDeclare(q,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);
            
            var body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(massage));
            channel.BasicPublish("",q,null,body);
        }
        
    }
}