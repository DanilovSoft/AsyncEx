using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Threading.Tasks.Sources;
using DanilovSoft.AsyncEx;

namespace TestApp
{
    class User
    {
        public int UserId { get; set; }
        public Item[] Items { get; set; }
    }

    class Item
    {
        public int ItemId { get; set; }
    }

    class Program
    {
        static async Task Main()
        {
            var users = new User[] 
            { 
                new User 
                { 
                    UserId = 123,
                    Items = new Item[] 
                    { 
                        new Item 
                        {
                            ItemId = 1
                        } 
                    }
                },
                new User
                {
                    UserId = 456,
                    Items = new Item[]
                    {
                        new Item
                        {
                            ItemId = 2
                        }
                    }
                },
            };

            var result = await ParallelTransform.Transform(users, x => 
            {
                return x.Run(x.Item.Items, item => GetItemName(item.ItemId), (item, itemName) => (Item: item, ItemName: itemName));
            }, 
            (user, items) => (User: user, Items: items));



            var result2 = await ParallelTransform.Transform(users, async x =>
            {
                await foreach (var item in x.Item.Items)
                {
                    var itemName = await GetItemName(x.Item.UserId, item.ItemId);
                    return (x.Item, ItemName: itemName);
                }
            },
            (user, items) => (User: user, Items: items));
        }

        private static async Task<string> GetItemName(int userId, int itemId)
        {
            return v.ToString();
        }
    }
}
