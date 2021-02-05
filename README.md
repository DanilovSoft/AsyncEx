```csharp
// Мутация дерева для обогащения свойств товаров значениями.
var users2 = await ParallelTransform.Transform(users, x =>
{
    return x.Sub(x.Item.Orders, xx =>
    {
        return x.Sub(xx.Item.Products, xxx =>
        {
            return xxx.Run(xxx.Item.Properties, pr => GetPropertyValue(pr.PropertyId), (Property, Value) => (Property, Value));
        },
        (Product, Properties) => (Product.Dto, Properties));
    },
    (Order, Products) => (Order.Dto, Products));
},
(User, Orders) => (User.UserId, Orders), maxDegreeOfParallelism: 10);
```
