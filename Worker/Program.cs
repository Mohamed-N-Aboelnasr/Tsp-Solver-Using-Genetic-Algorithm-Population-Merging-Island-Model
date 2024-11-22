using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using RabbitMQ.Client;

namespace Worker
{
    class Program
    {
        #region Global

        

        
        static Configuration c = new Configuration();
        static HyperParameter hp = new HyperParameter();
        private static Generation g = new Generation();
        public static string il;
        
        #endregion
        
        static void Main(string[] args)
        {
            Console.WriteLine("Enter Island Name : ");
            
            il = Console.ReadLine();
            Console.WriteLine(il);
            
            #region RabbitMQ configuration

            

            
            var factory = new ConnectionFactory
            {
                Uri = new Uri("amqp://guest:guest@localhost:5672")
            };
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();
            #endregion
            
            
            
            var consumerQueue = new ConsumerQueue();
        
        
            //Events subscribers
            
            
            consumerQueue.ConfigurationReceived +=(o,e)=> OnConfigurationReceived(o,e,channel);
            consumerQueue.Initialized +=(o, e)=> OnInitialized(o,e,channel);
            consumerQueue.EvacMessageReceived += (o, e) => OnEvacMessageReceived(o,e,channel);
            consumerQueue.ReceivedEvac += (o, e) => OnReceivedEvac(o, e, channel);
            consumerQueue.Finishing += (o, e) => OnFinishing(o, e, channel);
                
            
            //RabbitMQ Consumers
            
            consumerQueue.consume_configuration(channel,il+"_init");
            consumerQueue.consume_Start(channel,il+"_start");
            consumerQueue.consume_Evac(channel,il+"_Evac");
            consumerQueue.consume_evac_Pop(channel,il+"_merge");
            consumerQueue.consume_Finishing(channel, il + "_finish");


            
            Console.ReadLine();
        }

        

        #region On Receiving Configurations
        
        public static void OnConfigurationReceived(Object o,MessageReceivedEventArgs<(Configuration,HyperParameter,List<(List<string>,double)>)> e,IModel channel)
        {
            Console.WriteLine("config received");
            
            c = e.Message.Item1;
            hp = e.Message.Item2;
            g = inds2gen(e.Message.Item3, c);
            ProducerQueue.puplish(channel,"main_init",il);
            
        }
        #endregion

        #region On Receiving Start Order
        
        public static void OnInitialized(Object o, EventArgs e,IModel channel)
        {
            Console.WriteLine("Running");

            g = Algorithm.Genetic(g, hp,c);

            var fitnesses = g.population.Select(x => x.fitness).ToList();
            
            ProducerQueue.puplish(channel,"main_entropy",(il,GetEntropy(fitnesses,hp.BinsNo)));
        }
        #endregion 
        
        #region On Receiving Evacutaion Order
        public static void OnEvacMessageReceived(Object o, EventArgs e,IModel channel)
        {
            Console.WriteLine("Evacuating");
            ProducerQueue.puplish(channel,"main_evac",gen2inds(g));
        }
        #endregion

        #region On Receiving Evacuted Population

        
        public static void OnReceivedEvac(Object o, MessageReceivedEventArgs<List<(List<string>, double)>> e,
            IModel channel)
        {
            var pop = gen2inds(g);
            pop.AddRange(e.Message);
            var newPop = pop.OrderByDescending(x => x.Item2).Take(pop.Count * 2 / 3).ToList();
            g = inds2gen(newPop,c);
            hp.PopulationSize = g.population.Count;
            Console.WriteLine("merged population to size : "+hp.PopulationSize);
            
            ProducerQueue.puplish(channel,"main_merge",il);
        }
        
        #endregion
        
        #region On Receiving Finishing Order
        
        public static void OnFinishing(Object o, EventArgs e,IModel channel)
        {
            Console.WriteLine("Finishing");
            
            g=Algorithm.Genetic(g,hp,c);
            ProducerQueue.puplish(channel,"main_finish",(il,gen2inds(g)));
        }
        
        #endregion



        #region Conversion between Generation and Indices

        

        
        public static List<(List<string>,double)> gen2inds(Generation g)
        {
            var inds = new List<(List<string>,double)>();
            foreach (var chromosome in g.population)
            {
                var l1 = new List<string>();
                foreach (var city in chromosome.solution)
                {
                    l1.Add(city.Name);
                }
                inds.Add((l1,chromosome.fitness));
            }

            return inds;
        }
        
        public static Generation inds2gen(List<(List<string>,double)> inds,Configuration c)
        {
            var g = new Generation();
            var cities = c.AllCities;
            foreach (var l in inds)
            {
                var c1 = new List<City>();
                foreach (var i in l.Item1)
                {
                    c1.Add(cities.Find(x=>x.Name==i));


                }

                g.population.Add(new Chromosome() {solution = c1,fitness = l.Item2});

            }

            return g;
        }
        #endregion

        #region Get Entropy

        

        public static double GetEntropy(List<double> f,int bins)
        {
            return Entropy(Bucketize(f,bins));
        }
        public static List<int> Bucketize(List<double> source, int totalBuckets)
        {
            var min = source.Min();
            var max = source.Max();
            var buckets = new int[totalBuckets];

            var bucketSize = (max - min) / totalBuckets;
            foreach (var value in source)
            {
                int bucketIndex = 0;
                if (bucketSize > 0.0)
                {
                    bucketIndex = (int)((value - min) / bucketSize);
                    if (bucketIndex == totalBuckets)
                    {
                        bucketIndex--;
                    }
                }
                buckets[bucketIndex]++;
            }

            return buckets.ToList();
        }

        public static double Entropy(List<int> bins)
        {
            var n = bins.Sum();
            var plnp = bins.Select(x =>(x!=0? ((double)x /(double) n) * Math.Log(((double)x /(double) n)):0)).Sum();
            return -plnp;
        }

        
        #endregion
        
    }
}