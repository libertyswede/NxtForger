using NxtLib.Forging;
using NxtLib.ServerInfo;
using NxtLib.Local;
using System.Collections.Generic;
using System.Threading;
using System;
using System.Linq;
using NxtLib.Blocks;

namespace NxtForger
{
    public class Program
    {
        private const string server = "http://node1.ardorcrypto.com:7876/nxt";
        private static ulong lastBlockId = Constants.GenesisBlockId;
        private static List<GetNextBlockGeneratorsReply> projectedGenerators = new List<GetNextBlockGeneratorsReply>();
        private static ServerInfoService serverInfoService;

        public static void Main(string[] args)
        {
            serverInfoService = new ServerInfoService(server);
            var forgingService = new ForgingService(server);

            var blockchainStatus = serverInfoService.GetBlockchainStatus().Result;
            lastBlockId = blockchainStatus.LastBlockId;

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
                    Thread.Sleep(10000);
                }
            }
        }

        private static void CheckGeneratorResult()
        {
            var blockService = new BlockService(server);

            var nextBlockGeneratorReply = projectedGenerators.First();
            var height = nextBlockGeneratorReply.Height + 1;
            var block = blockService.GetBlock(BlockLocator.ByHeight(height)).Result;
            
            var expectedGenerator = nextBlockGeneratorReply.Generators.First().AccountId;
            var actualGenerator = block.Generator;

            if (expectedGenerator == actualGenerator)
            {
                Console.WriteLine($"Expected generator generated block at height {height}");
            }
            else
            {
                var generator = nextBlockGeneratorReply.Generators.SingleOrDefault(g => g.AccountId == expectedGenerator);
                var index = generator != null ? nextBlockGeneratorReply.Generators.IndexOf(generator) : -1;
                Console.Write($"Unexpected generator generated block at height {height}, expected: {expectedGenerator} but got {actualGenerator} ");
                Console.WriteLine($"at index {index}");
            }
            projectedGenerators.RemoveAt(0);
        }
    }
}
