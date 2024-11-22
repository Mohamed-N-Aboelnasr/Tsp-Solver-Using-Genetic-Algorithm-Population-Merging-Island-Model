using System;
using System.Collections.Generic;
using System.Runtime.Intrinsics.X86;
using System.Text;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Worker
{
    public class ConsumerQueue
    {
        #region Events

        

        
        
        public event EventHandler<MessageReceivedEventArgs<string>> EvacMessageReceived;
        public event EventHandler<MessageReceivedEventArgs<(Configuration,HyperParameter,List<(List<string>,double)>)>> ConfigurationReceived;
        public event EventHandler<EventArgs> Initialized;
        public event EventHandler<MessageReceivedEventArgs<List<(List<string>, double)>>> ReceivedEvac;
        public event EventHandler<EventArgs> Finishing;
        #endregion


        #region RabbitMQ Consumer

        

        
        public void consume_Finishing(IModel channel, string q)
        {
            channel.QueueDeclare(q,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);
            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (sender, e) =>
            {
                var body = Encoding.UTF8.GetString(e.Body.ToArray());
                var message = JsonConvert.DeserializeObject<string>(body);
                Console.WriteLine("finishing");
                
                OnFinishing();
            };
            channel.BasicConsume(q, true, consumer);
        }
        
        public void consume_evac_Pop(IModel channel, string q)
        {
            channel.QueueDeclare(q,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);
            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (sender, e) =>
            {
                var body = Encoding.UTF8.GetString(e.Body.ToArray());
                var message = JsonConvert.DeserializeObject<List<(List<string>,double)>>(body);
                Console.WriteLine("receiving evac pop");
                OnReceivedEvac(message);
            };
            channel.BasicConsume(q, true, consumer);
        }

        public void consume_Start(IModel channel, string q)
        {
            channel.QueueDeclare(q,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);
            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (sender, e) =>
            {
                var body = Encoding.UTF8.GetString(e.Body.ToArray());
                var message = JsonConvert.DeserializeObject<string>(body);
                Console.WriteLine("start message");
                OnInitialized();
            };
            channel.BasicConsume(q, true, consumer);
        }
        
        public void consume_Evac(IModel channel, string q)
        {
            channel.QueueDeclare(q,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);
            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (sender, e) =>
            {
                var body = Encoding.UTF8.GetString(e.Body.ToArray());
                var message = JsonConvert.DeserializeObject<string>(body);
                Console.WriteLine("evac message");
                OnEvacMessageReceived();
            };
            channel.BasicConsume(q, true, consumer);
        }
        
        public void consume_configuration(IModel channel, string q)
        {
            channel.QueueDeclare(q,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);
            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (sender, e) =>
            {
                var body = Encoding.UTF8.GetString(e.Body.ToArray());
                var message = JsonConvert.DeserializeObject<(Configuration,HyperParameter,List<(List<string>,double)>)>(body);
                Console.WriteLine("config message");
                OnConfigurationReceived(message);
            };
            channel.BasicConsume(q, true, consumer);
        }
        
        #endregion


        #region Delegates

        

        
        
        public virtual void OnFinishing()
        {
            Finishing?.Invoke(this, null);
        }
        
        
        protected virtual void OnReceivedEvac(List<(List<string>, double)> message)
        {
            ReceivedEvac?.Invoke(this,  new MessageReceivedEventArgs<List<(List<string>, double)>>{Message = message});
        }
        
        public virtual void OnInitialized()
        {
            Initialized?.Invoke(this, null);
        }
        
        protected virtual void OnEvacMessageReceived()
        {
            EvacMessageReceived?.Invoke(this, null);
        }
        
        protected virtual void OnConfigurationReceived((Configuration,HyperParameter,List<(List<string>,double)>) message)
        {
            ConfigurationReceived?.Invoke(this, new MessageReceivedEventArgs<(Configuration,HyperParameter,List<(List<string>,double)>)> { Message = message });
        }
        
        #endregion
    }


    #region Generic Type Messaging Class

    

    
    public class MessageReceivedEventArgs<T> : EventArgs
    {
        
        public T Message;
    }
    #endregion
}