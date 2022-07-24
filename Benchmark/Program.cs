using System.Reflection.Metadata;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

using DanilovSoft.AsyncEx;

public class Program
{
    //private readonly IEnumerable<Task> _tasks;

    public Program()
    {
        
    }

    [Benchmark]
    public void WhenAllOrAnyException()
    {
        GetTasks().WhenAllOrAnyException().GetAwaiter().GetResult();
    }

    //[Benchmark]
    //public void WhenAllOrAnyException2()
    //{
    //    GetTasks().WhenAllOrAnyException2().GetAwaiter().GetResult();
    //}

    static void Main()
    {
        var summary = BenchmarkRunner.Run(typeof(Program).Assembly);
    }

    private static IEnumerable<Task> GetTasks()
    {
        const int limit = 500;
        var tasks = new Task[limit];
        for (var i = 0; i < limit; i++)
        {
            tasks[i] = Task.Delay(1 + i);
        }
        return tasks;
    }
}