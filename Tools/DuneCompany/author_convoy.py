"""Create one combat course; do not overwrite existing levels or player saves."""
from author_motorsport import *
scene=base_scene('Convoy Breaker');ROOT.joinpath('Assets/ContractLevels').mkdir(exist_ok=True)
route=catmull([(0,0,-60),(0,0,-96),(30,0,-132),(35,0,-170),(-8,0,-207),(0,0,-252)])
terrain(scene,'convoy',route.tolist(),20,384,2)
# The sheltered bypass joins the road before extraction; broad curves permit towing.
bypass=catmull([(29,0,-120),(69,0,-145),(70,0,-178),(37,0,-215),(0,0,-252)])
height=scene['_height'];dense=resample(route,4);dense[:,1]=height(dense[:,0],dense[:,2])+.7
driver=node('Convoy driving route',components=[comp('RoutePath3D',waypointsJson=json.dumps(dense.tolist()),corridorWidth=20,gateRadius=9)])
scene['gameObjects'].append(driver)
scene['gameObjects'].append(node('Convoy salvage rules',(0,float(height(0,-60))+.7,-60),components=[comp('bytebard.dunecompany.DuneRecoveryContract3D',winchMission=True,title='Convoy Breaker',payment=2200,depressionDepth=0,deliveryHalfWidth=12),comp('bytebard.dunecompany.DuneConvoyContract3D',routeObject=driver['id'],extraction=[0,0,-252],deliveryRadius=12,convoySpeed=7,playerHealth=300,cargoHealth=220)]))
for o in scene['gameObjects']:
 for c in o.get('components',[]):
  if c['type'].endswith('DuneWorkshop3D'):c['properties']['automaticWeapons']=False
# Avoid scenery colliders crowding the road and especially the bypass.
rng=random.Random(822)
for i in range(140):
 x,z=rng.uniform(-95,115),rng.uniform(-292,30)
 if min(float(nearest(x,z,route)[0]),float(nearest(x,z,bypass)[0]))<23 or math.hypot(x,z)<30 or math.hypot(x,z+252)<25:continue
 prop(scene,'cliff-'+str(1+i%3),x,z,rng.random()*6.28,(1.1,1.3+i%3*.25,1.1),True)
wall=resample(route,10)
for i,p in enumerate(wall):
 if i<2 or i>len(wall)-4:continue
 direction=wall[min(i+1,len(wall)-1)]-wall[max(0,i-1)];direction[1]=0;direction/=np.linalg.norm(direction);side=np.array([-direction[2],0,direction[0]])
 for sign in [-1,1]:
  q=p+side*(24+rng.uniform(-1,1))*sign
  if float(nearest(q[0],q[2],bypass)[0])<22:continue
  prop(scene,'cliff-'+str(1+i%3),q[0],q[2],rng.uniform(-.5,.5),(1.4,2.4+(i%3)*.25,1.4),True)
for x,z in [(-22,-252),(24,-270)]:prop(scene,'container',x,z,.2,1,True)
prop(scene,'workshop-shed',-28,-267,0,1,True);prop(scene,'service-canopy',25,-245,0,1,False)
prop(scene,'salvage-sign',0,-265,0,1,False)
for x,z in [(-11,-90),(45,-163),(-23,-209)]:
 prop(scene,'chevron-sign',x,z,0,1,False)
for x,z in [(54,-155),(-20,-184)]:
 rock=prop(scene,'cliff-2',x,z,0,(1,1.4,1),True)
 scene['gameObjects'].append(node('Convoy rock winch anchor '+str(x),(x,float(height(x,z))+2,z),components=[comp('bytebard.dunecompany.DuneWinchAnchor3D')]))
for x,z in [(-18,-255),(18,-255)]:prop(scene,'route-flag',x,z,0,1,False)
for x,z in [(-5,-268),(5,-268)]:prop(scene,'convoy-barrier',x,z,0,1,True)
for key in list(scene):
 if key.startswith('_'):del scene[key]
path=ROOT/'Scenes/ConvoyBreaker.bytescene'
if path.exists():shutil.copy2(path,ROOT/'Docs/CombatConcepts/ConvoyBreaker-before-update.bytescene')
path.write_text(json.dumps(scene,indent=2),encoding='utf-8')
for p in OUT.glob('convoy*'): 
 if p.suffix=='.obj':meta(p,'Model3D')
meta(OUT/'salvage-sign.obj','Model3D');meta(OUT/'salvage-sign-lod.obj','Model3D')
print('Authored Convoy Breaker / road, bypass, shared-world convoy and extraction')
