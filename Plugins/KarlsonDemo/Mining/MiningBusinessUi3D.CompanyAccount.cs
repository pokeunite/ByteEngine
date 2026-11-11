using System.Numerics;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningBusinessUi3D
{
 bool accountDragging;
 void TickCompanyAccountInput(MiningOperation3D op)=>TickCompanySystemsInput(op);
 void DrawCompanyAccount(BusinessPainter p,MiningOperation3D op)=>DrawCompanyAccountCard(p,op,28,148,504);
}
