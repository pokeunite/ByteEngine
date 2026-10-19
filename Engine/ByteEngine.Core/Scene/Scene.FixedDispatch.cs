using ByteEngine.Core.Physics;

namespace ByteEngine.Core.Scene;

public sealed partial class Scene
{
    private readonly List<(GameObject Object,int Index)> _fixedDispatch = new();
    private readonly List<(GameObject Object,Rigidbody3D Body)> _poseDispatch = new();
    private int _fixedDispatchVersion=-1;

    private void RefreshFixedDispatch()
    {
        if(_fixedDispatchVersion==_componentRegistryVersion)return;
        _fixedDispatch.Clear();_poseDispatch.Clear();
        for(int i=0;i<_frameObjects.Count;i++)
        {
            var item=_frameObjects[i];
            if(item.NeedsFixedUpdate)_fixedDispatch.Add((item,i));
            if(item.GetComponent<Rigidbody3D>() is {} body)_poseDispatch.Add((item,body));
        }
        _fixedDispatchVersion=_componentRegistryVersion;
    }

    private void DispatchFixedUpdate()
    {
        int cursor=-1,position=0,version=_fixedDispatchVersion;
        while(true)
        {
            // A callback can add a hook to a later object. Refresh and resume
            // after the last visited scene index, matching full traversal semantics.
            if(version!=_componentRegistryVersion)
            {
                RefreshFixedDispatch();version=_fixedDispatchVersion;position=0;
                while(position<_fixedDispatch.Count&&_fixedDispatch[position].Index<=cursor)position++;
            }
            if(position>=_fixedDispatch.Count)return;
            var (item,index)=_fixedDispatch[position++];cursor=index;
            if(!_pendingDestroy.Contains(item.Id))item.FixedUpdateInternal();
        }
    }
}
