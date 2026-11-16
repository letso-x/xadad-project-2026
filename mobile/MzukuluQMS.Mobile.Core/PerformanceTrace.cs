using System.Diagnostics;
namespace MzukuluQMS.Mobile.Core;

public readonly record struct PerformanceSample(string Operation,double ElapsedMilliseconds);

// The app installs a sink only in Debug or an explicitly opted-in profiling build.
// In ordinary Release builds scopes are allocation-free no-ops; never record input.
public static class PerformanceTrace
{
 public static Action<PerformanceSample>? Sink {get;set;}
 public static Scope Measure(string operation)=>new(operation,Sink);
 public readonly struct Scope:IDisposable
 {
  private readonly string operation;private readonly Action<PerformanceSample>? sink;private readonly long started;
  internal Scope(string operation,Action<PerformanceSample>? sink){this.operation=operation;this.sink=sink;started=sink is null?0:Stopwatch.GetTimestamp();}
  public void Dispose()
  {
   if(sink is null)return;
   try{sink(new(operation,Stopwatch.GetElapsedTime(started).TotalMilliseconds));}
   catch{/* Optional diagnostics must not alter durability or business outcomes. */}
  }
 }
}
