### AsyncLock
#### Benefits
* Asynchronous
* No need to dispose
* Allocation-free most of the time
#### Usage example
```csharp
var locker = new AsyncLock();     
using (await locker.LockAsync())
{
    // ...
}
```

### AsyncLazy
#### Usage example
```csharp
var lazy = new AsyncLazy<int>(async () => await Task.FromResult(123), cacheFailure: false);
int value = await lazy.GetValueAsync();
```


### AsyncAutoResetEvent
#### Benefits
* Asynchronous
* No need to dispose
* Supports timeout
* Supports cancellation
#### Usage example
```csharp
var are = new AsyncAutoResetEvent();            
_ = Task.Delay(3000).ContinueWith(_ => are.Set());
bool gotSignal = await are.WaitAsync(5000, CancellationToken.None);
```

### Throttle
Skips redundant method calls
#### Benefits
* Guarantees synchronization between callback and Dispose call
#### Usage example
```csharp
using (var throttle = new Throttle<int>(callback: p => UiShowProgress(p), 100))
{
    for (int i = 0; i <= 100; i++)
    {
        throttle.Invoke(i);
        Thread.Sleep(10);
    }
}
// At this point, we know that "callback" is completed and will no longer be called by the Throttle.
UiShowProgress(100); // Just making sure that the user sees that the operation is 100% completed.

static void UiShowProgress(int progress)
{
    Console.WriteLine(progress); // Results: 8, 17, 25, 33, .., 100.
}
```
