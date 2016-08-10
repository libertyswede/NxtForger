using NxtLib.Forging;
using NxtLib.ServerInfo;
using NxtLib.Local;
using System.Collections.Generic;
using System.Threading;
using System;
using System.Linq;
using NxtLib.Blocks;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.Extensions.Configuration;

namespace NxtForger
{
    public class Program
    {
        private static string server;
        private static int sleepTime;
        private static ulong lastBlockId = Constants.GenesisBlockId;
        private static List<GetNextBlockGeneratorsReply> projectedGenerators = new List<GetNextBlockGeneratorsReply>();
        private static ServerInfoService serverInfoService;

        public static void Main(string[] args)
        {
            ReadConfig();

            serverInfoService = new ServerInfoService(server);
            var forgingService = new ForgingService(server);
            lastBlockId = Constants.GenesisBlockId;

            var blockchainStatus = serverInfoService.GetBlockchainStatus().Result;
            Console.WriteLine($"{DateTime.Now} Starting NxtForger check @ height {blockchainStatus.NumberOfBlocks - 1}");

            while (true)
            {
                blockchainStatus = serverInfoService.GetBlockchainStatus().Result;
                if (lastBlockId != blockchainStatus.LastBlockId)
                {
                    var nextBlockGenerators = forgingService.GetNextBlockGenerators(100).Result;
                    projectedGenerators.Add(nextBlockGenerators);

                    if (projectedGenerators.Count > 10)
                    {
                        CheckGeneratorResult();
                    }

                    lastBlockId = blockchainStatus.LastBlockId;
                }
                else
                {
                    Thread.Sleep(sleepTime);
                }
            }
        }

        private static void CheckGeneratorResult()
        {
            var blockService = new BlockService(server);

            var nextBlockGeneratorReply = projectedGenerators.First();
            var height = nextBlockGeneratorReply.Height;
            var block = blockService.GetBlock(BlockLocator.ByHeight(height)).Result;

            if (block.BlockId == nextBlockGeneratorReply.LastBlockId)
            {
                var generatedBlock = blockService.GetBlock(BlockLocator.ByHeight(height + 1)).Result;
                var expectedGenerator = nextBlockGeneratorReply.Generators.First().AccountRs;
                var actualGenerator = generatedBlock.GeneratorRs;

                if (expectedGenerator == actualGenerator)
                {
                    Console.WriteLine($"Expected generator generated block at height: {height + 1} id: {generatedBlock.BlockId}");
                }
                else
                {
                    var generator = nextBlockGeneratorReply.Generators.SingleOrDefault(g => g.AccountRs == expectedGenerator);
                    var index = generator != null ? nextBlockGeneratorReply.Generators.IndexOf(generator) : -1;
                    Console.WriteLine($"Unexpected generator generated block at height: {height + 1} expected: {expectedGenerator} but got: {actualGenerator} at index: {index}");
                }
            }
            else
            {
                Console.WriteLine($"Expected block {nextBlockGeneratorReply.LastBlockId} at height {height} was rolled back, skipping.");
            }
            projectedGenerators.RemoveAt(0);
        }

        private static void ReadConfig()
        {
            var configBuilder = new ConfigurationBuilder();
            configBuilder.SetBasePath(PlatformServices.Default.Application.ApplicationBasePath);
            configBuilder.AddJsonFile("config.json");
            configBuilder.AddJsonFile("config-Development.json", true);
            var config = configBuilder.Build();
            server = config.GetChildren().Single(c => c.Key == "ServerUri").Value;
            sleepTime = int.Parse(config.GetChildren().Single(c => c.Key == "SleepTime").Value);
        }
    }
}
