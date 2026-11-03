// Uniform values belong to program-specific locations and survive useProgram().
// Never retain caller-owned arrays: frame packets reuse and mutate those buffers.
export function cacheUniforms(gl){
    const values=new WeakMap(),state={enabled:true,uploads:0,skipped:0};
    for(const name of ['uniform1f','uniform1i','uniform2f','uniform3f','uniform4f']){
        const original=gl[name].bind(gl);
        gl[name]=function(location,a,b,c,d){
            if(location===null)return;
            if(!state.enabled){values.delete(location);state.uploads++;return original(location,a,b,c,d);}
            const old=values.get(location);
            if(old?.kind===name&&old.a===a&&old.b===b&&old.c===c&&old.d===d){state.skipped++;return;}
            if(old?.kind===name){old.a=a;old.b=b;old.c=c;old.d=d;}
            else values.set(location,{kind:name,a,b,c,d});
            state.uploads++;return original(location,a,b,c,d);
        };
    }

    for(const name of ['uniform2fv','uniform3fv','uniform4fv','uniform3iv','uniformMatrix4fv']){
        const original=gl[name].bind(gl),matrix=name==='uniformMatrix4fv';
        gl[name]=function(location,a,b){
            if(location===null)return;const data=matrix?b:a;
            if(arguments.length>(matrix?3:2)){values.delete(location);state.uploads++;return original(...arguments);}
            if(!state.enabled||(matrix&&a)){values.delete(location);state.uploads++;return matrix?original(location,a,b):original(location,a);}
            const old=values.get(location);let same=old?.kind===name&&old.data.length===data.length;
            if(same)for(let i=0;i<data.length;i++)if(old.data[i]!==data[i]){same=false;break;}
            if(same){state.skipped++;return;}
            if(old?.kind===name&&old.data.length===data.length)old.data.set(data);
            else values.set(location,{kind:name,data:Float64Array.from(data)});
            state.uploads++;return matrix?original(location,a,b):original(location,a);
        };
    }
    return state;
}
