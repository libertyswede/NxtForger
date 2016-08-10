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
            var height = nextBlockGeneratorReply.Height;
            var block = blockService.GetBlock(BlockLocator.ByHeight(height)).Result;
            var generatedBlock = blockService.GetBlock(BlockLocator.ByHeight(height + 1)).Result;
            
            var expectedGenerator = nextBlockGeneratorReply.Generators.First().AccountRs;
            var actualGenerator = generatedBlock.GeneratorRs;

            if (block.BlockId == nextBlockGeneratorReply.LastBlockId)
            {
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
                Console.WriteLine($"Block {nextBlockGeneratorReply.LastBlockId} at height {height} was rolled back, skipping.");
            }
            projectedGenerators.RemoveAt(0);
        }
    }
}
