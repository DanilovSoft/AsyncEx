using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using DanilovSoft.AsyncEx;

namespace TestApp
{
    class Program
    {
        static async Task Main()
        {
            int[] itemIds = new[] { 1, 2, 3 };

            // Для каждого Id получим коллекцию дочерних записей.
            var items = await ParallelTransform.Run(itemIds, id => GetChilds(id),
                (Id, Childs) => (Id, Childs),
                maxDegreeOfParallelism: 10);

            foreach (var item in items)
            {
                Console.WriteLine(item.Id);
                foreach (long childId in item.Childs)
                {
                    Console.WriteLine(childId);
                }
            }

            // Обогощаем дочерние записи "именем".
            var withName = await ParallelTransform.Run(items, x =>
            {
                return x.Run(x.Item.Childs, childId => GetNameByChildId(childId), (Id, Name) => (Id, Name));
            },
                (item, Childs) => (item.Id, Childs),
                maxDegreeOfParallelism: 10);

            foreach (var item in withName)
            {
                Console.WriteLine(item.Id);
                foreach (var child in item.Childs)
                {
                    Console.WriteLine(child.Id);
                    Console.WriteLine(child.Name);
                }
            }
        }

        static async Task<long[]> GetChilds(int id) => new long[] { 5, 6, 7 };

        static async Task<string> GetNameByChildId(long childId) => $"child_{childId}";
    }
}
