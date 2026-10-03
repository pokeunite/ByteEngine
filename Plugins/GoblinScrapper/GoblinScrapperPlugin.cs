using ByteEngine.Core.Plugins;
using GoblinScrapper.Construction;
namespace GoblinScrapper;
public sealed class GoblinScrapperPlugin : IByteEnginePlugin
{
    public void Register(ByteEnginePluginContext context)
    {
        context.RegisterComponent(new VehicleBuilder3DCodec(), new("Goblin Contraption Builder", "Gameplay", "Build modular goblin machines from blocks and functional joints.", "goblin vehicle construction"));
        GoblinEvents.Register(context);
        context.RegisterSerializedAlias("VehicleBuilder3D", "bytebard.goblinscrapper.VehicleBuilder3D");
    }
}
