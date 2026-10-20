namespace ByteEngine.Core.VisualLogic;
public sealed partial class CreatorSceneServices
{
    public sealed class SceneOperation
    {
        internal Task<object?> Task=null!;
        internal Action<object?> Apply=null!;
        internal bool PendingPulse;
        public bool Completed {get;internal set;}
        public bool FinishedThisUpdate {get;internal set;}
        public string Error {get;internal set;}="";
        public bool Succeeded=>Completed&&Error.Length==0;
    }
    private readonly Dictionary<string,SceneOperation> _operations=new(StringComparer.Ordinal);
    public SceneOperation? GetOperation(string key)=>_operations.GetValueOrDefault(key);
    public void StartOperation(string key,Func<object?> work,Action<object?> apply)
    {
        if(_operations.TryGetValue(key,out var current)&&!current.Completed)throw new InvalidOperationException("This slot already has an operation in progress.");
        if(_operations.Count>=256&&!_operations.ContainsKey(key))throw new InvalidOperationException("Remove unused slot results before starting more than 256 named operations.");
        Task<object?> task;
        if(OperatingSystem.IsBrowser()){try{task=System.Threading.Tasks.Task.FromResult(work());}catch(Exception error){task=System.Threading.Tasks.Task.FromException<object?>(error);}}
        else task=System.Threading.Tasks.Task.Run(work);
        _operations[key]=new(){Task=task,Apply=apply};
    }
    public void RecordOperationSuccess(string key)=>_operations[key]=new(){Completed=true,PendingPulse=true,Task=System.Threading.Tasks.Task.FromResult<object?>(null),Apply=_=>{}};
    public void RemoveOperation(string key){if(_operations.TryGetValue(key,out var operation)&&operation.Completed)_operations.Remove(key);}
    private void PollOperations()
    {
        foreach(var operation in _operations.Values.ToArray())
        {
            operation.FinishedThisUpdate=operation.PendingPulse;operation.PendingPulse=false;
            if(operation.Completed||!operation.Task.IsCompleted)continue;
            try{operation.Apply(operation.Task.GetAwaiter().GetResult());}
            catch(Exception error){operation.Error=error.Message;}
            operation.Completed=true;operation.FinishedThisUpdate=true;
        }
    }
}
