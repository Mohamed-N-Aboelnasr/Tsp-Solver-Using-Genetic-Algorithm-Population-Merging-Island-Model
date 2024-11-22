using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Master
{
    public class ConsumeQueue
    {
        public event EventHandler<MessageReceivedEventArgs<(string,double)>> EntropyReceived;
        //public event EventHandler<MessageReceivedEventArgs<(Configuration,HyperParameter,List<(List<int>,double)>)>> ConfigurationReceived;
        public event EventHandler<MessageReceivedEventArgs<string>> ReadyToStart;
        public event EventHandler<MessageReceivedEventArgs<string>> Merged;
        public event EventHandler<MessageReceivedEventArgs<List<(List<string>, double)>>> ReceivedEvac;
        public event EventHandler<MessageReceivedEventArgs<(string, List<(List<string>, double)>)>> Finished;
        
        public void consume_Last(IModel channel, string q)
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
                var message = JsonConvert.DeserializeObject<(string,List<(List<string>,double)>)>(body);
                OnFinished(message);

            };
            channel.BasicConsume(q, true, consumer);
        }

        public void consume_merge_confirmation(IModel channel, string q)
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
                OnMerged(message);
            };
            channel.BasicConsume(q, true, consumer);
        }

        public void consume_evac(IModel channel, string q)
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
                OnReceivedEvac(message);

            };
            channel.BasicConsume(q, true, consumer);
        }

        public void consume_entropy(IModel channel, string q)
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
                var message = JsonConvert.DeserializeObject<(string,double)>(body);
                
                OnEntropyReceived(message);
            };
            channel.BasicConsume(q, true, consumer);
        }
        
        public void consume_init_ready(IModel channel, string q)
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
                OnReadyToStart(message);
            };
            channel.BasicConsume(q, true, consumer);
        }
        
        /*public void consume_configuration(IModel channel, string q)
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
                var message = JsonConvert.DeserializeObject<(Configuration,HyperParameter,List<(List<int>,double)>)>(body);
                OnConfigurationReceived(message);
            };
            channel.BasicConsume(q, true, consumer);
        }
        */
        
        protected virtual void OnFinished((string,List<(List<string>, double)>) message)
        {
            Finished?.Invoke(this,  new MessageReceivedEventArgs<(string,List<(List<
            string>, double)>)>{Message = message});
        }

        protected virtual void OnMerged(string message)
        {
            Merged?.Invoke(this,  new MessageReceivedEventArgs<string>{Message = message});
        }

        protected virtual void OnReceivedEvac(List<(List<string>, double)> message)
        {
            ReceivedEvac?.Invoke(this,  new MessageReceivedEventArgs<List<(List<string>, double)>>{Message = message});
        }

        protected virtual void OnReadyToStart(string message)
        {
            ReadyToStart?.Invoke(this,  new MessageReceivedEventArgs<string>{Message = message});
        }
        
        protected virtual void OnEntropyReceived((string,double) message)
        {
            EntropyReceived?.Invoke(this, new MessageReceivedEventArgs<(string,double)> { Message = message });
        }
        
       /* protected virtual void OnConfigurationReceived((Configuration,HyperParameter,List<(List<int>,double)>) message)
        {
            ConfigurationReceived?.Invoke(this, new MessageReceivedEventArgs<(Configuration,HyperParameter,List<(List<int>,double)>)> { Message = message });
        }*/
    }

    public class MessageReceivedEventArgs<T>: EventArgs
    {
        
        public T Message;
    }
}