using ByteEngine.Sandbox;

if (args.Contains("--goblin-physics-test"))
{
    ByteEngine.Sandbox.Goblin.GoblinPhysicsTests.Run();
    return;
}

if (args.Contains("--goblin-benchmark"))
{
    ByteEngine.Sandbox.Goblin.GoblinBenchmark.Run();
    return;
}

if (args.Contains("--goblin-scrapwar"))
{
    using var scrapwar = new ByteEngine.Sandbox.Goblin.GoblinScrapwarGame();
    scrapwar.Run();
    return;
}

using var game = new TpsCameraTestGame();

game.Run();
