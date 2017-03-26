# Examples for AccessFlow

This Visual Studio repository comprises projects demonstrating the use of [AccessFlow](https://github.com/c7hm4r/AccessFlow).

For an introduction, please read the [AccessFlow README](https://github.com/c7hm4r/AccessFlow/blob/master/README.md) before.

## `SimpleComponent`

That project demostrates how access contexts are executed simultaneosly compared to synchronous execution.

The access scopes of `SimpleComponent` have a type of `long`. The logic `FlagBasedAccessScopeLogic` is implemented with bitwise operations.

## `AsyncBoxes`

This project contains an implementation of a minimalistic access contextual which representing a container for a single value, which can be read and set.

`IAsyncBox<T>` is the interface for that access contextual. It inherits from the two interfaces `IAsyncGetter<T>` and `IAsyncSetter<T>` to separate the reading and writing part with the goal of incresing reuse.

Both interfaces contain a method executed asynchronously.

```
public interface IAsyncGetter<out T>
{
    IReactive<T> Get();
}

public interface IAsyncSetter<in T>
{
    Task Set(IFuture<T> value);
}
```

`IReactive<T>` represents an operation producing a result. The result of an operation may be available earlier than the operation completes (e.g. because of cleanup or updating of indexes for later read accesses). That’s why the return type is not `Task<T>` as `Task<T>` finishes exactly when its `Result` is available. `IReactive<T>` combines a `Task` (property `ProcessTask`) representing the end the operation and an `IFuture<T>` (property `Result`) representing the asynchronous result. Note that it is necessary to await the `ProcessTask` to handle any exception occurred during the execution of `Get()`.

The counterpart `Set()` consumes an `IFuture<T>` enabling the execution of `Set` to begin before the parameter `value` is known. In practice that time can be used for e.g. loading indexes or opening (file) streams.

The method structure may seem uncomfortable at first glance, however there are some means provided by AccessFlow to simplify the usage.

- Any object can be converted to an `IFuture<T>` by invoking the extension method `ToFuture` on it:

  ```
  IFuture<int> a = 2.ToFuture();
  ```

- The method `ITaskCollector.Adding<T>(IReactive<T> reactive)` adds `reactive.ProcessTask` to the `ITaskCollector` and returns `IFuture<T> reactive.Result`. That way, the `IReactive` result of a method may be handled in a single line:

  ```
  // Executes an action with a new TaskCollector (tc) and
  // at the end waits for all Task-s in tc to complete
  await TaskCollector.With(async tc => {
    // Creates a new IAsyncBox<int> containing an initial value of 2
    IAsyncBox<int> box = AsyncBox.Create(initial: 2);
    // Adds box.Get().ProcessTask to tc and awaits box.Get().Result
    Console.WriteLine(await tc.Adding(box.Get()));
  });
  ```
  
## `StreamProcessing`

That project is a not yet optimal experiment to create an asynchronous stream or pipe with AccessFlow without using additional threading techniques. The stream is based on an linked list of `node`s each containing an element. The access scope (`CSScope`) is defined by intervals of RWScopes (read write scope). That way operations can perform within an interval of the stream without blocking other operations in distant intervals.

To enable O(1) appending and reading, is is necessary to keep the node which was appended/read last. This is done using `IAppender` and `IAsyncEnumerator` respectively. These objects encapsulate the state for continuous appending/reading but are not access contextuals themselves, that means a new access context is created for each read/append operation. Also the caller of the append operations has to manage the release of the access scopes to enable subsequent read accesses to run.
