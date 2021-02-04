using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using DanilovSoft.AsyncEx;

namespace TestApp
{
    class Program
    {
        [DebuggerDisplay("PropertyId = {PropertyId}, {PropertyName}")]
        class PropertyDto
        {
            public int PropertyId { get; set; }
            public string? PropertyName { get; set; }
        }

        [DebuggerDisplay("OrderId = {OrderId}, {OrderName}")]
        class OrderDto
        {
            public long OrderId { get; set; }
            public string? OrderName { get; set; }
        }

        [DebuggerDisplay("ProductId = {ProductId}, {ProductName}")]
        class ProductDto
        {
            public int ProductId { get; set; }
            public string? ProductName { get; set; }
        }

        /// <summary>
        /// Пользователь + Заказы.
        /// </summary>
        [DebuggerDisplay("Orders = {Orders.Count}")]
        record User1(int UserId, IList<OrderDto> Orders);

        /// <summary>
        /// Пользователь + Заказы + Товары.
        /// </summary>
        [DebuggerDisplay("Orders = {Orders.Count}")]
        record User2(int UserId, IList<Order1> Orders);

        /// <summary>
        /// Пользователь + Заказы + Товары + Свойства.
        /// </summary>
        [DebuggerDisplay("UserId = {UserId}, Orders = {Orders.Count}")]
        record User3(int UserId, IList<Order2> Orders);

        /// <summary>
        /// Пользователь + Заказы + Товары + Свойства + Значения_Свойств.
        /// </summary>
        [DebuggerDisplay("Orders = {Orders.Count}")]
        record User4(int UserId, IList<Order3> Orders);

        /// <summary>
        /// Заказ + Товары.
        /// </summary>
        [DebuggerDisplay("Products = {Products.Count}")]
        record Order1(OrderDto Dto, IList<ProductDto> Products);

        /// <summary>
        /// Заказ + Товары + Свойства.
        /// </summary>
        [DebuggerDisplay("Products = {Products.Count}")]
        record Order2(OrderDto Dto, IList<Product1> Products);

        /// <summary>
        /// Заказ + Товары + Свойства + Значения_Свойств.
        /// </summary>
        [DebuggerDisplay("Products = {Products.Count}")]
        record Order3(OrderDto Dto, IList<Product2> Products);

        /// <summary>
        /// Товар + Свойства.
        /// </summary>
        [DebuggerDisplay("Properties = {Properties.Count}")]
        record Product1(ProductDto Dto, IList<PropertyDto> Properties);

        /// <summary>
        /// Товар + Свойства + Значения_Свойств.
        /// </summary>
        [DebuggerDisplay("ProductName = {Dto.ProductName}, Properties = {Properties.Count}")]
        record Product2(ProductDto Dto, IList<Property1> Properties);

        /// <summary>
        /// Свойство + Значение.
        /// </summary>
        [DebuggerDisplay("{Dto.PropertyName}, Value = {Value}")]
        record Property1(PropertyDto Dto, string Value);

        static async Task Main()
        {
            int[] userIds = new[] { 1, 2, 3 };

            // Для каждого пользователя получим коллекцию заказов.
            var users = await ParallelTransform.Run(userIds, GetOrders,
                (UserId, Orders) => new User1(UserId, Orders));

            foreach (User1 user in users)
            {
                Debug.WriteLine(user.UserId);

                foreach (OrderDto order in user.Orders)
                {
                    Debug.WriteLine(order.OrderId);
                    Debug.WriteLine(order.OrderName);
                }
            }

            // Обогощаем заказы товарами.
            var users2 = await ParallelTransform.Transform(users, x =>
            {
                return x.Run(x.Item.Orders, o => GetOrderProducts(o.OrderId), (Order, Products) => new Order1(Order, Products));
            },
            (User, Orders) => new User2(User.UserId, Orders), 1);

            foreach (User2 user in users2)
            {
                Debug.WriteLine(user.UserId);

                foreach (Order1 order in user.Orders)
                {
                    Debug.WriteLine(order.Dto.OrderId);
                    Debug.WriteLine(order.Dto.OrderName);

                    foreach (ProductDto p in order.Products)
                    {
                        Debug.WriteLine(p.ProductId);
                        Debug.WriteLine(p.ProductName);
                    }
                }
            }

            // Обогощаем товары характеристиками.
            var users3 = await ParallelTransform.Transform(users2, x =>
            {
                return x.Sub(x.Item.Orders, xx =>
                {
                    return xx.Run(xx.Item.Products, p => GetProductProperties(p.ProductId), (Product, Properties) => new Product1(Product, Properties));
                },
                (Order, Products) => new Order2(Order.Dto, Products));
            },
            (User, Orders) => new User3(User.UserId, Orders), 1);


            foreach (User3 user in users3)
            {
                Debug.WriteLine(user.UserId);

                foreach (Order2 order in user.Orders)
                {
                    Debug.WriteLine(order.Dto.OrderId);
                    Debug.WriteLine(order.Dto.OrderName);

                    foreach (Product1 product in order.Products)
                    {
                        Debug.WriteLine(product.Dto.ProductId);
                        Debug.WriteLine(product.Dto.ProductName);

                        foreach (PropertyDto property in product.Properties)
                            Debug.WriteLine(property.PropertyName);
                    }
                }
            }

            // Обогощаем характеристики товаров значениями.
            var users4 = await ParallelTransform.Transform(users3, x =>
            {
                return x.Sub(x.Item.Orders, xx =>
                {
                    return x.Sub(xx.Item.Products, xxx =>
                    {
                        return xxx.Run(xxx.Item.Properties, pr => GetPropertyValue(pr.PropertyId), (Property, PValue) => new Property1(Property, PValue));
                    },
                    (Product, Properties) => new Product2(Product.Dto, Properties));
                },
                (Order, Products) => new Order3(Order.Dto, Products));
            },
            (User, Orders) => new User4(User.UserId, Orders), 1);


            foreach (User4 user in users4)
            {
                Debug.WriteLine(user.UserId);

                foreach (Order3 order in user.Orders)
                {
                    Debug.WriteLine(order.Dto.OrderId);
                    Debug.WriteLine(order.Dto.OrderName);

                    foreach (Product2 product in order.Products)
                    {
                        Debug.WriteLine(product.Dto.ProductId);
                        Debug.WriteLine(product.Dto.ProductName);

                        foreach (Property1 property in product.Properties)
                        {
                            Debug.WriteLine(property.Dto.PropertyName);
                            Debug.WriteLine(property.Value);
                        }
                    }
                }
            }
        }

        static async Task<IList<OrderDto>> GetOrders(int userId) => new List<OrderDto> 
        { 
            new() { OrderId = 765, OrderName = "Заказ на 1 товар" } 
        };
        static async Task<IList<ProductDto>> GetOrderProducts(long orderId) => new List<ProductDto>
        { 
            new() { ProductId = 876, ProductName = "Молоток" },
            new() { ProductId = 379, ProductName = "Пила" } 
        };
        static async Task<IList<PropertyDto>> GetProductProperties(int productId) => new List<PropertyDto>
        { 
            new() { PropertyId = 876, PropertyName = "Объём" },
            new() { PropertyId = 987, PropertyName = "Вес" } 
        };
        static async Task<string> GetPropertyValue(int propertyId) => "2 кг";
    }
}
