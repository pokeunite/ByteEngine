import {waterMath} from './shared-render.js';
// Native water material parameters, shared by every exported game.
export const waterUniforms=`
uniform bool waterSurface,waterVolume,waterReflections,hasWaterDepth;
uniform vec2 waterSize;uniform vec4 waterColumn,waterMotion,waterFlow,waterImpact,waterWake,waterAbsorption,waterScattering,waterStyle,waterWaves,waterDepthRect,waterDepthSettings;
uniform sampler2D waterDepthField;
`;
export const waterVertex=`
${waterMath}

float coastMask(vec2 q){if(!hasWaterDepth)return 1.;vec2 uv=(q-waterDepthRect.xy)/waterDepthRect.zw;if(any(lessThan(uv,vec2(0)))||any(greaterThan(uv,vec2(1))))return 1.;vec2 dim=vec2(textureSize(waterDepthField,0));vec2 rg=textureLod(waterDepthField,(uv*(dim-1.)+.5)/dim,0.).rg;float bed=mix(waterDepthSettings.x,waterDepthSettings.y,dot(rg,vec2(65280.,255.))/65535.);return smoothstep(0.,max(.01,waterDepthSettings.w),waterDepthSettings.z-bed);}
void gravityWave(inout vec3 disp,inout vec3 tx,inout vec3 tz,vec2 q,vec2 d,float amp,float length,float phase){byteGravityWave(disp,tx,tz,q,d,amp,length,phase,waterMotion.x,waterFlow.z);}
void displaceWater(inout vec3 p,inout vec3 N){if(!waterSurface)return;vec2 q=p.xz;if(waterStyle.w>.5){vec2 d=length(waterWaves.zw)>.001?normalize(waterWaves.zw):vec2(1,0);vec3 disp=vec3(0),tx=vec3(1,0,0),tz=vec3(0,0,1);float a=waterWaves.x*coastMask(q);gravityWave(disp,tx,tz,q,d,a,waterWaves.y,0.);gravityWave(disp,tx,tz,q,vec2(-d.y,d.x),a*.52,waterWaves.y*.492,1.7);gravityWave(disp,tx,tz,q,normalize(vec2(d.x*.8+d.y*.6,d.y*.8-d.x*.6)),a*.21,waterWaves.y*.1984,3.1);p+=disp;N=normalize(cross(tz,tx));}float edge=smoothstep(0.,.07,min(min(uv.x,1.-uv.x),min(uv.y,1.-uv.y)));float h=(sin(dot(q,vec2(2.6,1.1))-waterMotion.x*1.6)*.65+sin(dot(q,vec2(-1.4,3.6))+waterMotion.x*1.1)*.35)*waterMotion.z*edge;if(abs(N.y)>.5)p.y+=h;else p.z+=h;}
`;
export const waterFragment=`
${waterMath}

uniform sampler2D sceneColor,sceneDepth;uniform mat4 inverseVp,vp;uniform vec2 viewport;uniform int waterDetail,waterReflectionSteps;
float wn(vec2 p){return byteWaterNoise(p);}
vec3 unproject(vec2 uv,float depth){vec4 p=inverseVp*vec4(uv*2.-1.,depth,1.);return p.xyz/p.w;}
vec3 opticalWater(vec3 N){
 float t=waterMotion.x;bool channel=waterFlow.w>.5;vec2 q=channel?texcoord*waterSize:world.xz,adv=q-(channel?vec2(0,1):waterFlow.xy)*t*waterFlow.z;
 float p=dot(adv,vec2(3.1,1.7))+sin(q.y*1.4-t*.55)*.7,r=dot(adv,vec2(-2.3,4.7))+t*.8;
 vec2 slope=vec2(cos(p)*.14-cos(r)*.095,cos(p)*.075+cos(r)*.17);slope+=vec2(wn(adv*7.),wn(adv*7.+31.))*.055-.0275;
 vec3 ripple=vec3(-slope.x,0,-slope.y);if(channel){vec3 along=normalize(vec3(waterFlow.x,-dot(N.xz,waterFlow.xy)/max(.05,abs(N.y)),waterFlow.y));ripple=-normalize(cross(N,along))*slope.x-along*slope.y;}N=normalize(N+ripple*waterStyle.x);
 vec2 screen=gl_FragCoord.xy/viewport;float sz=texture(sceneDepth,screen).r;vec3 bed=unproject(screen,sz);float depth=clamp(world.y-bed.y,0.,24.);if(sz>.99999){depth=max(.04,waterColumn.x-waterColumn.y);bed=world-vec3(0,depth,0);}
 vec2 distorted=clamp(screen+N.xz*.009*min(depth,1.5)*smoothstep(0.,.15,depth),vec2(.001),vec2(.999));float dz=texture(sceneDepth,distorted).r;vec3 db=unproject(distorted,dz);if(dz<.99999&&db.y<world.y-.015){screen=distorted;bed=db;depth=clamp(world.y-bed.y,0.,24.);}
 vec3 refracted=texture(sceneColor,screen).rgb,V=normalize(eye-world),into=refract(-V,N,1./1.333),sigmaA=max(waterAbsorption.rgb,vec3(0)),sigmaS=max(waterScattering.rgb,vec3(0)),sigmaT=max(sigmaA+sigmaS,vec3(.000001));
 vec3 trans=byteWaterTransmittance(sigmaT,depth/max(.2,abs(into.y))),L=lightCount>0?normalize(-lightDirection[0]):vec3(0,1,0);float g=clamp(waterScattering.w,-.8,.8),mu=dot(-into,L),phase=byteWaterPhase(g,mu);vec3 incident=vec3(.055,.075,.095);if(lightCount>0)incident+=lightColor[0]*phase;
 vec3 under=refracted*trans*exp(-sigmaT*depth/max(.35,L.y))+incident*clamp(sigmaS/sigmaT*(1.-trans),vec3(0),vec3(1));
 vec2 b=bed.xz+vec2(sin(t*.43+bed.z)*.12,cos(t*.37+bed.x)*.12);float c1=sin(b.x*5.1+sin(b.y*3.7+t*.65)),c2=sin(b.y*5.8+sin(b.x*4.1-t*.53)),caustic=pow(max(0.,1.-abs(c1+c2)*1.7),9.)*(.65+.35*wn(b*.63));under+=refracted*trans*caustic*waterAbsorption.w*.35*exp(-depth*.6)*smoothstep(0.,.08,depth);
 vec3 ray=reflect(-V,N),reflected=mix(vec3(.035,.09,.12),vec3(.42,.58,.63),smoothstep(-.15,.85,ray.y));
 if(waterReflections&&waterDetail>0&&ray.y>0.){float previous=-1.;for(int i=0;i<20;i++){if(i>=waterReflectionSteps)break;vec3 samplePoint=world+N*.06+ray*(.1+float(i)*.26+float(i*i)*.018);vec4 clip=vp*vec4(samplePoint,1.);vec2 uv=clip.xy/clip.w*.5+.5;if(clip.w<=0.||any(lessThan(uv,vec2(.002)))||any(greaterThan(uv,vec2(.998))))break;float z=texture(sceneDepth,uv).r;vec3 hit=unproject(uv,z);float gap=length(eye-samplePoint)-length(eye-hit);if(z<.99999&&gap>0.&&gap<.38&&previous<=0.){float edge=smoothstep(0.,.09,min(min(uv.x,1.-uv.x),min(uv.y,1.-uv.y)));reflected=mix(reflected,texture(sceneColor,uv).rgb,edge*.8);break;}previous=gap;}}
 float fresnel=.02037+.97963*pow(1.-clamp(dot(N,V),0.,1.),5.);vec3 result=mix(under,reflected,fresnel);if(lightCount>0)result+=lightColor[0]*pow(max(0.,dot(ray,L)),mix(540.,60.,waterStyle.y))*1.2;
 float noise=wn(adv*3.2+wn(adv*.8+t*.12)),fine=wn(adv*10.+vec2(t*.15,-t*.11));vec2 ed=min(texcoord,vec2(1)-texcoord)*waterSize;float bank=1.-smoothstep(.015,.11,channel?ed.x:min(ed.x,ed.y));vec2 wp=world.xz-waterWake.xy,ip=world.xz-waterImpact.xy;float ring=(1.-smoothstep(.04,.22,abs(length(wp/vec2(max(.1,waterWake.z*.54),max(.1,waterWake.z*.3)))-1.)))*waterWake.w,impact=exp(-dot(ip,ip)/max(.01,waterImpact.z*waterImpact.z))*waterImpact.w;
 float streak=channel?smoothstep(.42,.73,wn(vec2(q.x*8.,q.y*2.1-t*waterFlow.z*2.1)))*clamp(abs(waterMotion.y)*.18,0.,.65):0.;float wash=.5+.5*sin(t*1.4+wn(q*.7)*9.),shore=(1.-smoothstep(.01,max(.04,waterStyle.z)*(.5+wash*.5),depth))*smoothstep(.48,.83,fine)*wash*waterStyle.w;
 float foam=clamp((shore+(1.-smoothstep(.025,.17,depth))*.2*bank+bank*.1*clamp(abs(waterMotion.y)*.3,0.,1.)+ring*.7+impact*(.45+noise)+streak)*(fine*.7+.3)*waterMotion.w,0.,.85);return mix(result,vec3(.72,.84,.81),foam);
}
`;

