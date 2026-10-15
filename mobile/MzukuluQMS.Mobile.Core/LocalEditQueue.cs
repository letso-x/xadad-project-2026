namespace MzukuluQMS.Mobile.Core;

// Serialize captured edits without dropping a later edit when an earlier write fails.
// Callers observe individual failures; DrainAsync waits for all scheduled writes.
public sealed class LocalEditQueue
{
 private readonly object gate=new();
 private Task tail=Task.CompletedTask;
 public Task Enqueue(Func<Task> write)
 {
  lock(gate){var result=RunAsync(tail,write);tail=ObserveAsync(result);return result;}
 }
 public Task DrainAsync(){lock(gate)return tail;}
 private static async Task RunAsync(Task previous,Func<Task> write){await previous;await write();}
 private static async Task ObserveAsync(Task write){try{await write;}catch{/* The enqueue caller handles this failure. Later writes must still run. */}}
}
