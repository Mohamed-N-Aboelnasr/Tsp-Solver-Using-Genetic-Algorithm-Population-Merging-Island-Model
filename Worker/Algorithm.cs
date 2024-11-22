using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Worker
{
    public static class Algorithm
    {
       
         #region Genetic
        
        
        
        public static Generation Genetic(Generation generation, HyperParameter hyperParameter, Configuration configuration)
        {
            var makeGeneration = new MakeGeneration();
            var (MakeGenerationFunc, Evaluate) = ChooseFunctions(makeGeneration, configuration, hyperParameter);
            generation = Evaluate(generation);                                                                              //evaluate 1st generation
            var nGenrations = hyperParameter.numberOfGenerations;
            Console.WriteLine("generation "+0);
            Console.WriteLine("Fitness of best Solution: "+ generation.elites[0].fitness);
            Console.WriteLine("Total Distance: "+ 1/generation.elites[0].fitness);
            
           for(var i=0;i<nGenrations;i++)
            {
                
                var newGeneration = MakeGenerationFunc(generation);                                                             //makeGeneration includes selection + crossover + mutation
                var newFullGeneration = AddElite(generation, newGeneration);                                    //adding elite of old generation to new generation
                
                generation = Evaluate(newFullGeneration);                                                                   //evaluate new generation
                
                Console.WriteLine("generation "+(i+1).ToString());
                Console.WriteLine("Fitness of best Solution: " + generation.elites[0].fitness);
                Console.WriteLine("Total Distance: "+ 1/generation.elites[0].fitness);
            }

            return generation; 
            
        }
        
        #endregion
        
        #region Compositing

        //composing selection and crossbreeding and mutation

        
        public static (Func<Generation, Generation> makeNewGeneration, Func<Generation, Generation> evaluateGeneration) ChooseFunctions(MakeGeneration makeGeneration, Configuration c,HyperParameter hyperParameter)
        {   //choosing functions configuration and making pipeline composite function of selection,crossover and mutation (producing new generation) along with evaluation function (not in pipeline since it's used in different places) 
            
            var mgfselecting = makeGeneration.selectList.FirstOrDefault(x => x.method == c.SelectionMethod).select;
            Func<Generation, CrossOverCandidates> select = (x) => Selection(x, hyperParameter, mgfselecting);                                        //configuring selection with the chosen function
            
            var mgfco = makeGeneration.crossoverList.FirstOrDefault(x => x.method == c.CrossOverMethod).crossover;
            Func<CrossOverCandidates,Generation> crossOver = (x) => CrossOver(x, mgfco,hyperParameter,c);                                                             //configuring CrossOver with the chosen function
            
            var mgfMutate = makeGeneration.mutatingList.FirstOrDefault(x => x.method == c.MutationMethod).mutate;
            Func<Generation,Generation> mutate = (x) => Mutation(x, hyperParameter, mgfMutate);                                                     //configuring Mutation with the chosen function
            
            var mgfEval = makeGeneration.evalList.FirstOrDefault(x => x.method == c.EvalMethod).evaluate;
            Func<Generation,Generation> evaluate = (x) => EvaluateGeneration(x, mgfEval,c,hyperParameter);                                                          //configuring Evaluation with the chosen function

            return (select.Compose(crossOver).Compose(mutate),evaluate);                                                                   //returning composite function and evaluation function
            
            

        }
        //
        public static Func<T1, T3> Compose<T1, T2, T3>(this Func<T1, T2> f, Func<T2, T3> g)
        {
            return (x) => g(f(x));
        }
        #endregion

        #region Add Elites

        public static Generation AddElite(Generation oldGeneration, Generation newGeneration)
        {
            //adding elites of the previous population to the new population
            var newPopulation = newGeneration.population.Select(x=>x).ToList();
            newPopulation.AddRange(oldGeneration.elites);
            var newGeneration2 = new Generation() {population = newPopulation};
            return newGeneration2;

        }

        #endregion
        
        #region Mutation

        public static Generation Mutation(Generation g, HyperParameter hp,Func<Chromosome,Random,Chromosome> mutate)
        {   //apply mutation to generation with a certain propability (mutationProbability)
            var mutationProbabilty = hp.mutationProbability;
            
            var pop = new ConcurrentBag<Chromosome>();
            
            
            BufferBlock<(int,Chromosome)> bufferBlock = new BufferBlock<(int,Chromosome)>(new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = hp.MaxDegreeOfParallelism,
                BoundedCapacity = hp.BoundedCapacity
            });
            ActionBlock<(int,Chromosome)> actionBlock = new ActionBlock<(int,Chromosome)>(x =>
            {
                //if number smaller than mutation probability mutate chromosome and add,else add unmutated chromosome
                
                var rnd = new Random(Seed:x.Item1);
                
                pop.Add((rnd.NextDouble() < mutationProbabilty ? mutate(x.Item2, rnd) : x.Item2));
                

            }, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = hp.MaxDegreeOfParallelism,
                BoundedCapacity = hp.BoundedCapacity
            });
            var seedingRandom = new Random();
            bufferBlock.LinkTo(actionBlock, new DataflowLinkOptions() { PropagateCompletion = true });
            
            g.population.ForEach((x)=>bufferBlock.SendAsync((seedingRandom.Next(),x)).Wait());
            
            bufferBlock.Complete();
            Task.WaitAll(actionBlock.Completion);

            var newGeneration = new Generation() {population = pop.ToList()};
            
            return newGeneration;
        }
        
        public static Chromosome GeneSwaping(Chromosome c,Random rnd)
        {   //apply mutation to chromosome by swaping 2 randomly choosen genes
            var n = c.solution.Count;
            var i = rnd.Next(0,n);
            var j = rnd.Next(0,n);
            
            while (i == j) { j = rnd.Next(0, n); }

            var newSolution = new List<City>();
            newSolution.AddRange( c.solution);
            (newSolution[i], newSolution[j]) = (newSolution[j], newSolution[i]);                //swaping
            var c2 = new Chromosome() {solution = newSolution};
            
            return c2;
        }
        
        public static Chromosome GeneInversion(Chromosome c, Random rnd)
        {
            var n = c.solution.Count;
            var i = rnd.Next(0,n);
            var j = rnd.Next(0,n);



            while (i == j)
            {
                j = rnd.Next(0, n); 
                
            }
            if (i > j)
            {
                (i, j) = (j, i);
            }

            var s1 = c.solution.Take(i).ToList();
            var s2 = c.solution.Skip(i).Take(j - i).Reverse().ToList();
            var s3 = c.solution.Skip(j).ToList();
            var sol = s1.Concat(s2).Concat(s3).ToList();
            var c2 = new Chromosome() {solution = sol};
            return c2;

        }

        #endregion

        #region Crossover

        public static Generation CrossOver(CrossOverCandidates crossOverCandidates,Func<(Chromosome, Chromosome),Random,Configuration,(Chromosome, Chromosome)> crossoverMethod,HyperParameter hp,Configuration configuration)
        {   //apply crossover for all candidates pair to generate new generation

            var pop= new ConcurrentBag<Chromosome>();
            var rr = new Random();
            
            BufferBlock<((Chromosome,Chromosome),int)> bufferBlock = new BufferBlock<((Chromosome,Chromosome),int)>(new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = hp.MaxDegreeOfParallelism,
                BoundedCapacity = hp.BoundedCapacity
            });
            ActionBlock<((Chromosome,Chromosome),int)> actionBlock = new ActionBlock<((Chromosome,Chromosome),int)>(x =>
            {

                var offspring = crossoverMethod(x.Item1,new Random(Seed:x.Item2),configuration);
                pop.Add(offspring.Item1);
                pop.Add(offspring.Item2);
                
            }, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = hp.MaxDegreeOfParallelism,
                BoundedCapacity = hp.BoundedCapacity
            });
            bufferBlock.LinkTo(actionBlock, new DataflowLinkOptions() { PropagateCompletion = true });
            var r = new Random();
            for (int i = 0; i < crossOverCandidates.Parent1.Count; i++)
            {
                
                bufferBlock.SendAsync(((crossOverCandidates.Parent1[i],crossOverCandidates.Parent2[i]),r.Next())).Wait();
            }
            
            
            
            bufferBlock.Complete();
            Task.WaitAll(actionBlock.Completion);
            
            Generation generation = new Generation(){population = pop.ToList()};
            
            return generation;
        }
        
        public static (Chromosome,Chromosome) CrossOver2Points((Chromosome, Chromosome) cc,Random rnd,Configuration configuration)
        {   //crossover 2 chromosomes to get 2 offsprings
            
            var c1 = cc.Item1;
            var c2 = cc.Item2;
            var n = c1.solution.Count;
            //randomly choose i (starting point of slice) and j (ending point of slice)
            var i = rnd.Next(0, n/2);                                                                   
            var j = i + rnd.Next(1,n/2);
            
            var slice1 = c1.solution.Skip(i).Take(j-i).ToList();
            var slice2 = c2.solution.Skip(i).Take(j-i).ToList();
            //complementary of the slices
            var rest1 = c1.solution.Where((x) => !slice2.Contains(x)).ToList();
            var rest2 = c2.solution.Where((x) => !slice1.Contains(x)).ToList();
            
            //inserting slices in the correct position

            var newSolution1 = rest1.Take(i).ToList();
            newSolution1.AddRange(slice2);
            newSolution1.AddRange(rest1.Skip(i).ToList());
            
            var newSolution2 = rest2.Take(i).ToList();
            newSolution2.AddRange(slice1);
            newSolution2.AddRange(rest2.Skip(i).ToList());  
            

            return (new Chromosome(){solution = newSolution1}, new Chromosome(){solution = newSolution2});

        }
        public static (Chromosome, Chromosome) CrossOver1Points((Chromosome, Chromosome) cc, Random rnd,Configuration configuration)
        {
            var c1 = cc.Item1;
            var c2 = cc.Item2;
            var n = c1.solution.Count;
            
            //randomly choose splitting point
            var i = rnd.Next(1, n);                                                                   

            var slice1 = c1.solution.Take(i).ToList();
            var slice2 = c2.solution.Take(i).ToList();
            //complementary of the slices
            var s1 = c1.solution.Except(slice2).ToList();
            var s2 = c2.solution.Except(slice1).ToList();
            
            
            var newSolution1 = slice1.Concat(s2).ToList();
            
            var newSolution2 = s1.Concat(slice2).ToList();

            return (new Chromosome(){solution = newSolution1}, new Chromosome(){solution = newSolution2});
        }

         public static (Chromosome, Chromosome) CrossOverGX5((Chromosome, Chromosome) Parents, Random rnd,Configuration configuration)
        {
            return (GX5(Parents, rnd,configuration), GX5(Parents, rnd,configuration));
        }
        

        public static Chromosome GX5((Chromosome, Chromosome) Parents, Random rnd,Configuration configuration)
        {
            var child = new List<City>();
            var n = Parents.Item1.solution.Count;
            var i = rnd.Next(0, n);
            var city = Parents.Item1.solution[i];
            child.Add(city);
            
            while (child.Count < n)
            {


                city = child.Last();
                i = Parents.Item1.solution.Select(((city11, i1) => (city11, i1))).First(x => x.city11.Name == city.Name).i1;
                var j = Parents.Item2.solution.Select(((city11, i1) => (city11, i1))).First(x => x.city11.Name == city.Name)
                    .i1;
                var a = (i == 0 ? n - 1 : i-1);
                var b = (i == n-1 ? 0 : i+1);
                var c = (j == 0 ? n - 1 : j-1);
                var d = (j == n-1 ? 0 : j+1);
                
                var l = new List<City>()
                {
                    Parents.Item1.solution[a], Parents.Item1.solution[b], Parents.Item2.solution[c],
                    Parents.Item2.solution[d]
                };
                var remain = l.Except(child).ToList();

                if (remain.Count > 0)
                {
                    child.Add(get_closest(city, remain,configuration));
                }
                else
                {
                    var remain2 = Parents.Item1.solution.Except(child).Take(20).OrderBy(x => rnd.Next()).ToList();
                    child.Add(get_closest(city,remain2,configuration));
                }
            }

            return new Chromosome() {solution = child};

        }

        public static City get_closest(City c, List<City> l,Configuration configuration)
        {
            var closest = l.Select(x => (x, configuration.DistancesLookup[x.Name][c.Name])).OrderBy(x => x.Item2).Take(1).ToList()[0].x;
            return closest;
        }
        
        public static (Chromosome, Chromosome) OX1((Chromosome, Chromosome) cc, Random rnd,Configuration configuration)
        {
            var c1 = cc.Item1;
            var c2 = cc.Item2;
            var n = c1.solution.Count;

            var i1 = rnd.Next(0, n); //has to be smaller than i2
            var i2 = rnd.Next(0, n);
            if (i1 > i2)
            {
                (i1, i2) = (i2, i1);
            }

            var a1 = c1.solution.Take(i1).ToList();
            var a2 = c1.solution.Skip(i1).Take(i2 - i1).ToList();
            var a3 = c1.solution.Skip(i2).ToList();
            
            var b1 = c2.solution.Take(i1).ToList();
            var b2 = c2.solution.Skip(i1).Take(i2 - i1).ToList();
            var b3 = c2.solution.Skip(i2).ToList();

            var s13 = b3.Concat(b1).Concat(b2).Except(a2).Take(b3.Count).ToList();
            var s11 = b3.Concat(b1).Concat(b2).Except(a2).Skip(b3.Count).ToList();

            var s23 = a3.Concat(a1).Concat(a2).Except(b2).Take(a3.Count).ToList();
            var s21 = a3.Concat(a1).Concat(a2).Except(b2).Skip(a3.Count).ToList();

            var newSolution1 = s11.Concat(a2).Concat(s13).ToList();
            
            var newSolution2 = s21.Concat(b2).Concat(s23).ToList();
            
            return (new Chromosome(){solution = newSolution1}, new Chromosome(){solution = newSolution2});
        }
        #endregion

        #region Selection

        public static CrossOverCandidates Selection(Generation g, HyperParameter hp,Func<List<Chromosome>,HyperParameter,Random,(Chromosome,Chromosome)> selectionMethod)
        {   //uses specified selection method to generate a number of pairs of chromosomes (crossOverCandidates) that will undergo Crossover
            
            var nCrossover = hp.PopulationSize*(1 - hp.ElitesRatio) / 2;
            var p1 = new ConcurrentBag<Chromosome>();
            var p2 = new ConcurrentBag<Chromosome>();
            var lc = g.population.OrderByDescending(x => x.fitness).ToList();
            var rnd = hp.rnd;
            var r = new Random();
            double totalFitness = lc.Sum(item => item.fitness);
            List<double>relFitness= lc.Select(item => item.fitness/totalFitness).ToList();
            List<double> propapility = new List<double>();
            propapility.Add(relFitness[0]);
            for (int j = 1; j < relFitness.Count; j++)
            {
                propapility.Add(relFitness[j]+ propapility.Last());

            }
            
            BufferBlock<(int,List<Chromosome>)> bufferBlock1 = new BufferBlock<(int,List<Chromosome>)>(new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = hp.MaxDegreeOfParallelism,
                BoundedCapacity = hp.BoundedCapacity
            });
            
            ActionBlock<(int,List<Chromosome>)> actionBlock1 = new ActionBlock<(int,List<Chromosome>)>(x =>
            {
                
               var (chromosome1, chromosome2) = selectionMethod(x.Item2,hp, new Random(Seed:x.Item1));
                 
                p1.Add(chromosome1);
                p2.Add(chromosome2);
             
            }, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = hp.MaxDegreeOfParallelism,
                BoundedCapacity = hp.BoundedCapacity
            });
            
            bufferBlock1.LinkTo(actionBlock1, new DataflowLinkOptions() { PropagateCompletion = true });

            var rr = new Random();
            Enumerable.Range(0, (int) nCrossover).ToList().ForEach(i=>bufferBlock1.SendAsync((rr.Next(),lc)).Wait());
           
            bufferBlock1.Complete();

            Task.WaitAll(actionBlock1.Completion);

            CrossOverCandidates crossOverCandidates = new CrossOverCandidates() {Parent1 = p1.ToList(),Parent2 = p2.ToList()};
            return crossOverCandidates;
        }

        
        public static (Chromosome,Chromosome) RouletteSelection(List<Chromosome> lc,HyperParameter hp,Random rnd)
        {
            
            var lcc = lc.Select(x=>x).ToList();
            var totalFitness = lcc.Sum(x => x.fitness);
            var relFitness = lcc.Select(x => x.fitness / totalFitness).ToList();
            var props = new List<double>() {0};
            
            Enumerable.Range(0, lcc.Count).ToList()
                .ForEach(i => props.Add(relFitness[i]+props.Last()));
            var r1 = rnd.NextDouble();
            var winner1 = GetWinnerIndex(props, r1);
            var r2 = rnd.NextDouble();
            var winner2 = GetWinnerIndex(props, r2);

            var c1 = lc[winner1];
            var c2 = lc[winner2];

            return (c1, c2);
        }
        
        public static (Chromosome, Chromosome) TournamentSelection(List<Chromosome> lc,HyperParameter hp, Random rnd)
        {
            var c1 = TournamentSelect1Chromosome(lc,hp, rnd);
            var c2 = TournamentSelect1Chromosome(lc,hp, rnd);

            return (c1, c2);
        }

        public static Chromosome TournamentSelect1Chromosome(List<Chromosome> Population,HyperParameter hp, Random rnd)
        {   //choose random sample of size k then assign weights based on fitness and choose winner randomly based on these wieghts then return winner chromosome

            
            var p = hp.winningProbability;
            var k = (int)(hp.tournamentSampleSize*hp.PopulationSize);
            var sample =Population.OrderBy((x) => rnd.Next()).Take(k).ToList();
            sample = sample.OrderByDescending((x) => x.fitness).ToList();                                     //sorted sample
            List<double> rankingList = new List<double>(){0};

            Enumerable.Range(0, k - 1).ToList()
                .ForEach(i => rankingList.Add(rankingList.Last() + p * Math.Pow(1 - p, i)));
            rankingList.Add(1);

            var r = rnd.NextDouble();
            var winner = sample[GetWinnerIndex(rankingList,r)];
            return winner;
        }
        
        public static int GetWinnerIndex(List<double> l,double r)
        {
            
            //l.ForEach();
            for(int i =1;i<l.Count;i++)
            {
                if (r < l[i]) { return i-1; }

            }

            return l.Count;
        }
        
        #endregion
        
        #region Evaluation

        public static Generation EvaluateGeneration(Generation g, Func<Chromosome,Configuration,HyperParameter,Chromosome> EvalChromosome,Configuration c,HyperParameter hp )
        {
            //uses the passed evaluation function and run on the entire generation and return a generation of evaluated chromosomes also choose the generation elites
            
            
            var pop = new ConcurrentBag<Chromosome>();

            
            BufferBlock<Chromosome> bufferBlock = new BufferBlock<Chromosome>(new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = hp.MaxDegreeOfParallelism,
                BoundedCapacity = hp.BoundedCapacity
            });
            
            ActionBlock<Chromosome> actionBlock = new ActionBlock<Chromosome>(x =>
            {

                var chromosome = EvalChromosome(x, c, hp);
                pop.Add(chromosome);
             
            }, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = hp.MaxDegreeOfParallelism,
                BoundedCapacity = hp.BoundedCapacity
            });
            bufferBlock.LinkTo(actionBlock, new DataflowLinkOptions() { PropagateCompletion = true });
            
            
            g.population.ForEach((x)=>bufferBlock.SendAsync(x).Wait());
            
            bufferBlock.Complete();
            Task.WaitAll(actionBlock.Completion);

            var updatedGeneration = new Generation() {population = pop.ToList()};
            
            updatedGeneration.elites = updatedGeneration.population.OrderByDescending((x) => x.fitness).Take((int)(hp.PopulationSize*hp.ElitesRatio)).ToList();
            
            return updatedGeneration;
        }
        
        
        public static Chromosome EvaluateChromosome(Chromosome chromosome, Configuration configuration,HyperParameter hp)
        {
            //evaluate each chromosome by calculating the total distance from the lookup dictionary then return a new evaluated chromosome (with fitness attribute filled)
            
            double totalDistance = 0;
            List<City> L1 = new List<City>();
           
            
            //if the path is closed loop we end the sequence with the starting city, if not we end it with the ending city (only added while evaluating)
            if (hp.isClosedLoop)
            {
                L1.AddRange(chromosome.solution);
            }
            else
            {
                L1.Add(configuration.AllCities.Find(x=>x.Name==hp.StartingCity));
                L1.AddRange(chromosome.solution);
                L1.Add(configuration.AllCities.Find(x=>x.Name==hp.EndingCity));
            }
            

            var cityA = L1[0];
            foreach (var cityB in L1)
            {
                totalDistance += configuration.DistancesLookup[cityA.Name][cityB.Name];
                cityA = cityB;
            }

            if (hp.isClosedLoop)
            {
                totalDistance += configuration.DistancesLookup[L1.Last().Name][L1[0].Name];
            }

            List<City> newList = new List<City>();
            newList.AddRange(chromosome.solution);

            Chromosome newChromosome = new Chromosome() {solution = newList ,fitness = 1/totalDistance};
            return newChromosome;
        }
        public static Dictionary<string, Dictionary<string, double>> Distances(List<City> allCities,Func<City,City,double> CalcDistance)
        {
            //create a lookup dictionary for distances between cities (to get the distance between 2 cities we call dictionary[city1.name][city2.name])
            
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
        
        #region Initialization

        public static Generation fadyinit(List<City> cities,int numberOfChromosomes)
        {
            Random rnd = new Random();
            List<Chromosome> chromosomes = new List<Chromosome>();
            for (int i = 0; i < numberOfChromosomes; i++)
            {
                List<City> tmp = new List<City>(cities.OrderBy(x => rnd.Next()).ToList());
                
                chromosomes.Add(new Chromosome(){solution = tmp});
            }
            Generation P = new Generation(){population = chromosomes};
            return P;
        }
        
        public static Generation InitiateGeneration(HyperParameter hp,Configuration c)
        {
            //generate a number (generationSize) of chromosomes as intial generation
            var allCities = c.AllCities.Select(x=>x).ToList();
            
            if (!hp.isClosedLoop)
            {
                allCities.Remove(allCities.Find(x => x.Name == hp.StartingCity));
                allCities.Remove(allCities.Find(x => x.Name == hp.EndingCity));

            }
           

            var generation = new Generation();
            var rnd = hp.rnd;
            var populationSize = hp.PopulationSize;
            var pop = new ConcurrentBag<Chromosome>();
            
            BufferBlock<(int,List<City>)> bufferBlock = new BufferBlock<(int,List<City>)>(new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = hp.MaxDegreeOfParallelism,
                BoundedCapacity = hp.BoundedCapacity
            });
            ActionBlock<(int,List<City>)> actionBlock = new ActionBlock<(int,List<City>)>(x =>
            {
                var rnd = new Random(Seed:x.Item1);
                var l = new List<City>(x.Item2.OrderBy((y) => rnd.Next()).ToList());
                pop.Add(new Chromosome() {solution =l});
             
            }, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = hp.MaxDegreeOfParallelism,
                BoundedCapacity = hp.BoundedCapacity
            });
            bufferBlock.LinkTo(actionBlock, new DataflowLinkOptions() { PropagateCompletion = true });

            var seedingRandom = new Random();
            for (int i = 0; i < populationSize; i++)
            {
                bufferBlock.SendAsync((seedingRandom.Next(),allCities)).Wait();
            }
            
            bufferBlock.Complete();
            Task.WaitAll(actionBlock.Completion);
            
            generation.population.AddRange(pop);

            return generation;
        }
        #endregion

        #region ChoosingFunctions

        public class MakeGeneration
        {
            public List<(EvalMethod method, Func<Chromosome, Configuration, HyperParameter, Chromosome> evaluate)> evalList;

            public List<(SelectionMethod method, Func<List<Chromosome>, HyperParameter,Random,(Chromosome,Chromosome)> select)>
                selectList;

            public
                List<(CrossOverMethod method,
                    Func<(Chromosome, Chromosome),Random,Configuration , (Chromosome, Chromosome)> crossover)> crossoverList;

            public List<(MutationMethod method, Func<Chromosome, Random, Chromosome> mutate)> mutatingList;

            public MakeGeneration()
            {
                evalList =
                    new List<(EvalMethod method, Func<Chromosome, Configuration, HyperParameter, Chromosome> evaluate)>()
                    {
                        (EvalMethod.Distance, EvaluateChromosome)
                    };
                selectList =
                    new List<(SelectionMethod method, Func<List<Chromosome>, HyperParameter,Random,(Chromosome,Chromosome)> select)>()
                    {
                        (SelectionMethod.Tournament, TournamentSelection),
                        (SelectionMethod.Roulette, RouletteSelection)
                    };
                crossoverList =
                    new List<(CrossOverMethod method,
                        Func<(Chromosome, Chromosome),Random,Configuration , (Chromosome, Chromosome)> crossover)>()
                    {
                        (CrossOverMethod.CrossOver2Points,CrossOver2Points),
                        (CrossOverMethod.CrossOver1Points,CrossOver1Points),
                        (CrossOverMethod.GX5,CrossOverGX5),
                        (CrossOverMethod.OX1,OX1)
                    };
                mutatingList =
                    new List<(MutationMethod method, Func<Chromosome, Random, Chromosome> mutate)>()
                    {
                        (MutationMethod.Swapping, GeneSwaping),
                        (MutationMethod.Inversion, GeneInversion)
                    };

            }
        }

        #endregion
    }
    
    #region Main Classes

    

    
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
    
    //
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
        public List<Chromosome> Parent1;
        public List<Chromosome> Parent2;

    }
    
    #endregion

    #region Parameters and Configrations
    
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

    public class Configuration
    {
        public EvalMethod EvalMethod;
        public SelectionMethod SelectionMethod;
        public CrossOverMethod CrossOverMethod;
        public MutationMethod MutationMethod;
        public List<City> AllCities;
        public Dictionary<string, Dictionary<string, double>> DistancesLookup;
        

    }
    
    //ThreadSafe Randomizer class
    public class RandomGen3
    {
        private static Random _global =
            new Random();
        [ThreadStatic]
        private static Random _local;

        public int Next(int mini,int maxi)
        {
            if (_local == null)
            {
                int seed;
                lock (_global)
                {
                    seed = _global.Next();
                }
                _local = new Random(seed);
            }

            return _local.Next(mini,maxi);
        }
        
        public int Next()
        {
            if (_local == null)
            {
                int seed;
                lock (_global)
                {
                    seed = _global.Next();
                }
                _local = new Random(seed);
            }

            return _local.Next();
        }
        
        public double NextDouble()
        {
            if (_local == null)
            {
                int seed;
                lock (_global)
                {
                    seed = _global.Next();
                }
                _local = new Random(seed);
            }

            return _local.NextDouble();
        }
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
        GX5,
        OX1
    }

    public enum MutationMethod
    {
        Swapping,
        Inversion
    }
    
    #endregion
    }
