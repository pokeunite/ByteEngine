using ByteEngine.Core.Diagnostics;
namespace GoblinScrapper.Construction;
public sealed partial class VehicleBuilder3D
{
 private void TraceBuildState()
 {
  if(!ConstructionDiagnostics.Enabled||Assembly==null)return;
  if(_buildTraceGeneration!=ConstructionDiagnostics.Generation)
  {
   _buildTraceGeneration=ConstructionDiagnostics.Generation;
   ConstructionDiagnostics.Record("BUILD SNAPSHOT",$"plugin=0.3.0 project={ProjectRoot} catalog={_catalog?.Revision} modelDirectory={PartsDirectory} build={Assembly.ToJson()}");
   _previousBuildTrace="";
  }
  string state=$"mode={(Building?"build":"drive")} selectedPart={SelectedPart} selectedBlock={_selectedBlock} hoveredBlock={_hoveredBlock} candidateConnector={_candidateConnector} candidateBone={_candidateBone} twist={_twist} ownSocket={_ownSocket} movingBranch={_movingBlock} parts={Assembly.Parts.Count} undo={_assemblyUndo.Count} redo={_assemblyRedo.Count} placementIssue={_placementIssue} status={_message}";
  if(state!=_previousBuildTrace){ConstructionDiagnostics.Record("BUILDER STATE",state);_previousBuildTrace=state;}
 }
}
