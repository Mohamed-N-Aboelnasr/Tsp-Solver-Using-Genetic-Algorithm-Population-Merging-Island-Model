using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Newtonsoft.Json;
using RabbitMQ.Client;
using System.Configuration;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using XPlot.Plotly;

namespace Master
{
    class Program
    {
        
        #region Global

        

        
        public static List<(string, double)> entropies = new List<(string, double)>();
        public static List<string> readyness = new List<string>();

        public static List<string> Islands =
            new List<string>(ConfigurationManager.AppSettings["Islands"]?.Split(new char[] {','}) ??
                             Array.Empty<string>());

        public static int n = Islands.Count;
        public static (string, string) least;

        
        
        
        
        #endregion
        #region Configuration and HyperParameters

        

        
        static City city0 = new City() {x = 0, y = 0,Name="0"};
        static City city1 = new City() {x = 3, y = 27,Name="1"};
        static City city2 = new City() {x = 14, y = 22,Name="2"};
        static City city3 = new City() {x = 1, y = 13,Name="3"};
        static City city4 = new City() {x = 20, y = 3,Name="4"};
        static City city5 = new City() {x = 20, y = 16,Name="5"};
        static City city6 = new City() {x = 28, y = 12,Name="6"};
        static City city7 = new City() {x = 30, y = 31,Name="7"};
        static City city8 = new City() {x = 11, y = 19,Name="8"};
        static City city9 = new City() {x = 7, y = 3,Name="9"};
        static City city10 = new City() {x = 10, y = 25,Name="10"};
        

        //public static List<City> cities = new List<City>(){ city0,city1, city2, city3, city4, city5, city6, city7, city8, city9, city10};
        public static List<City> cities = DAO.Read_csv(ConfigurationManager.AppSettings["data_path"]);
        
       

        
        
        

        
        private static Configuration configuration = new Configuration()                                                                    //functions configuration
        {
            SelectionMethod = Enum.Parse<SelectionMethod>(ConfigurationManager.AppSettings["SelectionMethod"] ?? string.Empty),
            CrossOverMethod = Enum.Parse<CrossOverMethod>(ConfigurationManager.AppSettings["CrossOverMethod"] ?? string.Empty),
            MutationMethod = Enum.Parse<MutationMethod>(ConfigurationManager.AppSettings["MutationMethod"] ?? string.Empty),
            EvalMethod = Enum.Parse<EvalMethod>(ConfigurationManager.AppSettings["EvalMethod"] ?? string.Empty),
            allCities = cities,
            DistancesLookup = Distances(cities,CalcDistance)
            
        };

        public static HyperParameter hyperParameters = new HyperParameter()
        {
            winningProbability = double.Parse(ConfigurationManager.AppSettings["winningProbability"] ?? string.Empty),
            PopulationSize = (int) (int.Parse(ConfigurationManager.AppSettings["PopulationSize"] ?? string.Empty)/Islands.Count),
            tournamentSampleSize = double.Parse(ConfigurationManager.AppSettings["tournamentSampleSize"] ?? string.Empty),
            ElitesRatio = double.Parse(ConfigurationManager.AppSettings["ElitesRatio"] ?? string.Empty),
            numberOfGenerations = int.Parse(ConfigurationManager.AppSettings["numberOfGenerationsBetweenMergers"] ?? string.Empty),
            BinsNo = int.Parse(ConfigurationManager.AppSettings["BinsNo"] ?? string.Empty),
            mutationProbability = double.Parse(ConfigurationManager.AppSettings["mutationProbability"] ?? string.Empty),
            isClosedLoop = bool.Parse(ConfigurationManager.AppSettings["isClosedLoop"] ?? string.Empty),
            StartingCity = ConfigurationManager.AppSettings["StartingCity"],
            EndingCity = ConfigurationManager.AppSettings["EndingCity"],
            MaxDegreeOfParallelism = int.Parse(ConfigurationManager.AppSettings["MaxDegreeOfParallelism"] ?? string.Empty),
            BoundedCapacity = int.Parse(ConfigurationManager.AppSettings["BoundedCapacity"] ?? string.Empty)
        };
        #endregion

        

        #region Internal Events

        

        
        public static event EventHandler<InternalMessageEventArgs<List<(string,double)>>> DoneReceiving;
        public static event EventHandler<InternalMessageEventArgs<string>> OneIsland;
        public static event EventHandler<InternalMessageEventArgs<List<string>>> AllReady;

        #endregion
        public static Stopwatch sw = new Stopwatch();
        
        static void Main(string[] args)
        {
            
            
            #region RabbitMQ Configuration
            
            var factory = new ConnectionFactory
            {
                Uri = new Uri("amqp://guest:guest@localhost:5672")
            };
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();
            #endregion
            
            var consumeQueue = new ConsumeQueue();
            

            

            
            consumeQueue.EntropyReceived += OnEntropyReceived;
            consumeQueue.ReadyToStart += OnReadyToStart;
            AllReady += (o, e) => OnAllReady(o, e, channel);
            DoneReceiving += (o, e) => OnDoneReceiving(o, e, channel);
            consumeQueue.ReceivedEvac += (o, e) => OnReceivedEvac(o, e, channel);
            consumeQueue.Merged += (o, e) => OnMerged(o, e, channel);
            OneIsland+=(o, e) => OnOneIsland(o, e, channel);
            consumeQueue.Finished += (o, e) => OnFinished(o, e, channel);

            
            //Sending Init Population (Start of the Algorithm)
            
            sw.Start();
            Islands.ForEach((x)=> ProducerQueue.puplish(channel,x+"_init",(configuration,hyperParameters,InitiateGenerationinds(hyperParameters,configuration))));
            
            consumeQueue.consume_init_ready(channel,"main_init");
            consumeQueue.consume_entropy(channel,"main_entropy");
            consumeQueue.consume_evac(channel, "main_evac");
            consumeQueue.consume_merge_confirmation(channel, "main_merge");
            consumeQueue.consume_Last(channel, "main_finish");

            

            

            Console.ReadLine();
            
        }
        


        #region On Received from Final Island

        

        
        public static void OnFinished(Object o, MessageReceivedEventArgs<(string, List<(List<string>, double)>)> e,
            IModel channel)
        {
            Console.WriteLine("----------------------------------------------------------------------------");
            Console.WriteLine("Final Solution from : "+e.Message.Item1);
            Console.WriteLine("best Fitness : "+e.Message.Item2.Max(x=>x.Item2));
            Console.WriteLine("shortest distance : "+1/e.Message.Item2.Max(x=>x.Item2));
            Console.WriteLine("----------------------------------------------------------------------------");
            var el=inds2gen(e.Message.Item2, configuration).population.OrderByDescending(x=>x.fitness).Take(1).ToList()[0];
            
            sw.Stop();

            #region Plot solution

            
            var scatter = new Scatter() {x =el.solution.Select(x=>x.x).ToList() , y = el.solution.Select(x=>x.y).ToList(),mode="lines+markers"};
            var chart = Chart.Plot(scatter);
            var t = "PS/I= " + hyperParameters.PopulationSize +
                    ",No_gen/I= "+hyperParameters.numberOfGenerations +
                    ",I= "+n+",ER= " + hyperParameters.ElitesRatio +
                    ",MR= " + hyperParameters.mutationProbability +
                    ",T= "+sw.Elapsed.TotalSeconds +
                    ",distance= " +
                   Math.Round(1/el.fitness,2);
            var chart_layout = new Layout.Layout{title=t};
            chart.WithLayout(chart_layout);
            chart.Show();
            #endregion
            


        }
        
                
            
        
        
        #endregion

        #region On One Island Remaing

        

        
        public static void OnOneIsland(Object o, InternalMessageEventArgs<string> e,IModel channel)
        {
            Islands.ForEach((i) => ProducerQueue.puplish(channel, i + "_finish", "finish"));
        }
        
        #endregion

        #region On Merger Confirmed

        

        
        public static void OnMerged(Object o, MessageReceivedEventArgs<string> e, IModel channel)
        {
            Console.WriteLine("continue");
            //Islands.ForEach(Console.WriteLine);
            if (Islands.Count == 1)
            {
                new X().OnOneIsland(Islands[0]);
            }
            else
            {
                new X().OnAllReady(Islands);
            }
        }
        
        #endregion

        #region On Received Evacuated Population

        

        
        public static void OnReceivedEvac(Object o, MessageReceivedEventArgs<List<(List<string>, double)>> e,
            IModel channel)
        {
            Islands.Remove(least.Item1);
            Console.WriteLine("sending evac to 2nd worst : "+least.Item2);
            
            ProducerQueue.puplish(channel,least.Item2+"_merge",e.Message);
        }
        #endregion

        #region On Received and Processing Entropies
        public static void OnDoneReceiving(Object o, InternalMessageEventArgs<List<(string,double)>> e,IModel channel)
        {
            var leasts = e.Message.ToList().OrderBy((x) => x.Item2).Take(2).ToList();
            least = (leasts[0].Item1, leasts[1].Item1);
            entropies.ToList().ForEach(x=>Console.WriteLine(x.Item1+" : "+x.Item2));
                
            Console.WriteLine("The worst is "+least.Item1);
                
            Interlocked.Exchange<List<(string, double)>>(ref entropies, new List<(string, double)>());
            Console.WriteLine("sending Evac order to "+least.Item1);
            ProducerQueue.puplish(channel,least.Item1+"_Evac","Evac");
        }
        #endregion
        
        #region On Receiving Entropies

        

        
        public static void OnEntropyReceived(Object o,MessageReceivedEventArgs<(string,double)> e)
        {
            entropies.Add(e.Message);
            
            if (entropies.Count == Islands.Count)
            {
                
                new X().OnDoneReceiving(entropies);
                
            }
            
            
        }
        #endregion
        
        #region On All Islands Ready

        

        
        public static void OnAllReady(Object o, InternalMessageEventArgs<List<string>> e, IModel channel)
        {
            Console.WriteLine("Starting");
            e.Message.ForEach((i) => ProducerQueue.puplish(channel, i + "_start", "start"));
            
        }
        
        #endregion
        
        #region On Ready to Start

        
        public static void OnReadyToStart(Object o,MessageReceivedEventArgs<string> e)
        {
            readyness.Add(e.Message);
            Console.WriteLine(e.Message+" is Ready");
            if (Islands.Count == 1)
            {
                new X().OnOneIsland(Islands[0]);
            }
            else if (readyness.Count == Islands.Count)
            {
                new X().OnAllReady(readyness);
                
                Interlocked.Exchange<List<string>>(ref readyness, new List<string>());
            }

            

        }
        
        #endregion
        
        #region Initialize 1st Population

        

        
        public static List<(List<string>,double)> InitiateGenerationinds(HyperParameter hp,Configuration c)
        {
            //generate a number (generationSize) of chromosomes as intial generation
            var allCities = new List<string>();
            
            allCities.AddRange(c.allCities.Select(x=>x.Name).ToList());
            
            
            
            
            if (hp.isClosedLoop==false)
            {
                allCities.Remove(allCities.Find(x=>x==hp.StartingCity));
                allCities.Remove(allCities.Find(x=>x==hp.EndingCity));
            }
            

            var generation = new Generation();
            var rnd = hp.rnd;
            var populationSize = hp.PopulationSize;
            var pop = new ConcurrentBag<(List<string>,double)>();
            
            BufferBlock<List<string>> bufferBlock = new BufferBlock<List<string>>(new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = hp.MaxDegreeOfParallelism,
                BoundedCapacity = hp.BoundedCapacity
            });
            ActionBlock<List<string>> actionBlock = new ActionBlock<List<string>>(x =>
            {

                pop.Add( (x.OrderBy((y)=>rnd.Next()).ToList(),0));
             
            }, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = hp.MaxDegreeOfParallelism,
                BoundedCapacity = hp.BoundedCapacity
            });
            bufferBlock.LinkTo(actionBlock, new DataflowLinkOptions() { PropagateCompletion = true });

            for (int i = 0; i < populationSize; i++)
            {
                bufferBlock.SendAsync(allCities).Wait();
            }
            
            bufferBlock.Complete();
            Task.WaitAll(actionBlock.Completion);


            return pop.ToList();
        }
        #endregion
        
        #region Making Lookup Distances Dictionary

        

        
        public static Dictionary<string, Dictionary<string, double>> Distances(List<City> allCities,Func<City,City,double> CalcDistance)
        {
            //create a lookup dictionary for distances between cities
            
            var dic = new Dictionary<string, Dictionary<string, double>>();
            allCities.ForEach((city1)=>dic[city1.Name]=new Dictionary<string, double>());
            foreach (var kvp in dic)
            {
                var dic2 = new Dictionary<string, double>();
                allCities.ForEach((city2)=> dic2.Add(city2.Name,CalcDistance(city2,allCities.Find((x)=>x.Name==kvp.Key))));
                dic[kvp.Key] = dic2;

            }

            return dic;
        }

        public static double CalcDistance(City city1, City city2)
        {
            var xdiff = city1.x - city2.x;
            var ydiff = city1.y - city2.y;
            
            return Math.Pow(Math.Pow(xdiff, 2) + Math.Pow(ydiff, 2), 0.5);
        }
        
        #endregion
        
        class X
        {
            public virtual void OnDoneReceiving(List<(string,double)> message)
            {
                DoneReceiving?.Invoke(this, new InternalMessageEventArgs<List<(string,double)>> { Message = message });
            }

            public virtual void OnOneIsland(string message)
            {
                OneIsland?.Invoke(this,new InternalMessageEventArgs<string>(){Message = message});
            }

            public virtual void OnAllReady(List<string> message)
            {
                AllReady?.Invoke(this, new InternalMessageEventArgs<List<string>>(){Message = message});
            }
            
            
        }
        public static Generation inds2gen(List<(List<string>,double)> inds,Configuration c)
        {
            var g = new Generation();
            var cities = c.allCities;
            foreach (var l in inds)
            {
                var c1 = new List<City>();
                foreach (var i in l.Item1)
                {
                    //Console.WriteLine(i);
                    c1.Add(cities.Find(x=>x.Name==i));

                    //Console.WriteLine(cities[i].Name);

                }

                g.population.Add(new Chromosome() {solution = c1,fitness = l.Item2});

            }

            return g;
        }
    }

    #region Classes

    

    
    class InternalMessageEventArgs<T>
    {
        public T Message;
    }
    
    public class City
    {
        public string Name;
        public double x { get; set; }
    
        public double y { get; set; }
        

        public override string ToString()
        {
            return Name.ToString();
        }
    }
    
    public class Chromosome
    {
        public List<City> solution;
        public double fitness;
    }

    public class Generation
    {
        public List<Chromosome> population = new List<Chromosome>();
        public List<Chromosome> elites = new List<Chromosome>();


    }

    public class CrossOverCandidates
    {
        public ConcurrentBag<(Chromosome, Chromosome)> candidates;

    }
    
    
    public class HyperParameter
    {
        public double winningProbability;
        public double mutationProbability;
        public int PopulationSize;
        public double ElitesRatio;
        public double tournamentSampleSize;
        public int numberOfGenerations;
        public int MaxDegreeOfParallelism;           //max degree for dataflow blocks
        public int BoundedCapacity;                 //bounded capacity for dataflow blocks
        public bool isClosedLoop=false;             //if you want to start and end with the same city
        public string StartingCity;
        public string EndingCity;
        public int BinsNo;
        public RandomGen3 rnd = new RandomGen3();   //randomizer to be used throughout the program
    }
    public class RandomGen3
    {
        private static RNGCryptoServiceProvider _global =
            new RNGCryptoServiceProvider();
        [ThreadStatic]
        private static Random _local;

        public int Next(int mini,int maxi)
        {
            Random inst = _local;
            if (inst == null)
            {
                byte[] buffer = new byte[4];
                _global.GetBytes(buffer);
                var seed = BitConverter.ToInt32(buffer, 0);
                _local = inst = new Random(
                    seed);
            }
            return inst.Next(mini,maxi);
        }
        
        public int Next()
        {
            Random inst = _local;
            if (inst == null)
            {
                byte[] buffer = new byte[4];
                _global.GetBytes(buffer);
                var seed = BitConverter.ToInt32(buffer, 0);
                _local = inst = new Random(
                    seed);
            }
            return inst.Next();
        }
        
        public double NextDouble()
        {
            Random inst = _local;
            if (inst == null)
            {
                byte[] buffer = new byte[4];
                _global.GetBytes(buffer);
                var seed = BitConverter.ToInt32(buffer, 0);
                _local = inst = new Random(
                    seed);
            }
            return inst.NextDouble();
        }
    }
    
    public class Configuration
    {
        public EvalMethod EvalMethod;
        public SelectionMethod SelectionMethod;
        public CrossOverMethod CrossOverMethod;
        public MutationMethod MutationMethod;
        public List<City> allCities;
        public Dictionary<string, Dictionary<string, double>> DistancesLookup;
        

    }
    public enum EvalMethod
    {
        Distance
    }

    public enum SelectionMethod
    {
        Tournament,
        Roulette
        
    }

    public enum CrossOverMethod
    {
        CrossOver2Points,
        CrossOver1Points,
        GX5
    }

    public enum MutationMethod
    {
        Swapping,
        Inversion
    }
    #endregion
}