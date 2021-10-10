using System.Threading.Tasks;

namespace DanilovSoft.AsyncEx
{
    public interface IAggregator<TOutput>
    {
        Task InvokeAsync();
        TOutput InvokeResult { get; }
    }
}
