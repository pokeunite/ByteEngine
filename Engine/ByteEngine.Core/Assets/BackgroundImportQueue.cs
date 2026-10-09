using System.Collections.Concurrent;
namespace ByteEngine.Core.Assets;
/// <summary>CPU-only serial importer worker. Owner-thread Pump publishes only the latest generation; disposal cancels all publication.</summary>
public sealed class BackgroundImportQueue : IDisposable
{
    private readonly SemaphoreSlim _worker = new(1);
    private readonly ConcurrentQueue<Action> _completed = new();
    private readonly Dictionary<Guid,long> _versions = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly int _ownerThread=Environment.CurrentManagedThreadId;
    private volatile bool _disposed;
    private volatile bool _disposeRequested;
    private int _resourcesDisposed;
    private int _pending;
    public int PendingCount=>Volatile.Read(ref _pending);
    public void Enqueue<T>(Guid asset,Func<T> import,Action<T> publish,Action<Exception>? failed=null)
    {
        CheckOwner();ObjectDisposedException.ThrowIf(_disposed,this);
        long version=_versions.GetValueOrDefault(asset)+1;_versions[asset]=version;
        Interlocked.Increment(ref _pending);
        _=Task.Run(async()=>
        {
            bool acquired=false;
            try
            {
                await _worker.WaitAsync(_shutdown.Token).ConfigureAwait(false);acquired=true;
                _shutdown.Token.ThrowIfCancellationRequested();
                var value=import();
                _shutdown.Token.ThrowIfCancellationRequested();
                _completed.Enqueue(()=>{if(!_disposed&&_versions.GetValueOrDefault(asset)==version)publish(value);});
            }
            catch(OperationCanceledException){}
            catch(Exception error){_completed.Enqueue(()=>{if(!_disposed&&_versions.GetValueOrDefault(asset)==version)failed?.Invoke(error);});}
            finally{if(acquired)_worker.Release();if(Interlocked.Decrement(ref _pending)==0&&_disposeRequested)DisposeResources();}
        });
    }
    public void Pump(int maximumResults=4)
    {
        CheckOwner();for(int i=0;i<Math.Max(0,maximumResults)&&_completed.TryDequeue(out var action);i++)action();
    }
    private void CheckOwner(){if(Environment.CurrentManagedThreadId!=_ownerThread)throw new InvalidOperationException("Import publication must run on the owning thread.");}
    private void DisposeResources(){if(Interlocked.Exchange(ref _resourcesDisposed,1)==0){_worker.Dispose();_shutdown.Dispose();}}
    public void Dispose(){CheckOwner();if(_disposed)return;_disposed=true;_shutdown.Cancel();_disposeRequested=true;while(_completed.TryDequeue(out _)){};if(PendingCount==0)DisposeResources();}
}
