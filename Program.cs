using System.Collections.Generic;
using System.Threading;
using System;
using System.Linq;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.Extensions.Configuration;
using NxtLib.Forging;
using NxtLib.ServerInfo;
using NxtLib.Local;
using NxtLib.Blocks;

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
                var generatorsOrdered = nextBlockGeneratorReply.Generators.OrderBy(g => g.HitTime).ToList();
                var expectedAccountRs = generatorsOrdered.First().AccountRs;
                var actualAccountRs = generatedBlock.GeneratorRs;

                if (expectedAccountRs == actualAccountRs)
                {
                    Console.WriteLine($"Expected at height: {height + 1} id: {generatedBlock.BlockId}");
                }
                else
                {
                    var expectedGenerator = generatorsOrdered.Single(g => g.AccountRs == expectedAccountRs);
                    var actualGenerator = generatorsOrdered.SingleOrDefault(g => g.AccountRs == actualAccountRs);
                    var index = actualGenerator != null ? generatorsOrdered.IndexOf(actualGenerator) : -1;
                    Console.WriteLine($"Unexpected generator at height: {height + 1} for block id: {generatedBlock.BlockId}");
                    Console.WriteLine($"Expected: {expectedAccountRs} Deadline: {expectedGenerator.Deadline} but got: {actualAccountRs} Deadline: {actualGenerator?.Deadline} at index: {index}");
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
