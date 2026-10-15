using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public readonly struct Money
{
    public decimal Amount { get; }

    public Money(decimal amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Сумма не может быть отрицательной.");
        Amount = amount;
    }

    public static Money Zero => new Money(0m);
    public static Money operator +(Money a, Money b) => new Money(a.Amount + b.Amount);

    public static Money operator -(Money a, Money b)
    {
        decimal result = a.Amount - b.Amount;
        if (result < 0)
            throw new InvalidOperationException("Нельзя получить отрицательную денежную сумму.");
        return new Money(result);
    }

    public static Money operator *(Money money, int multiplier)
    {
        if (multiplier < 0)
            throw new ArgumentOutOfRangeException(nameof(multiplier));
        return new Money(money.Amount * multiplier);
    }

    public static Money operator *(Money money, decimal multiplier)
    {
        if (multiplier < 0)
            throw new ArgumentOutOfRangeException(nameof(multiplier));
        return new Money(money.Amount * multiplier);
    }

    public override string ToString() =>
        Amount.ToString("N2", CultureInfo.GetCultureInfo("ru-RU")) + " ₽";
}

public abstract class Product
{
    public string Name { get; }

    protected Product(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Название товара не может быть пустым.", nameof(name));
        Name = name;
    }

    public abstract Money GetPrice();
    public abstract bool RequiresShipping { get; }
    public abstract decimal GetWeight();
}

public sealed class PhysicalProduct : Product
{
    private readonly Money _price;
    private readonly decimal _weight;

    public PhysicalProduct(string name, Money price, decimal weight) : base(name)
    {
        if (weight <= 0)
            throw new ArgumentOutOfRangeException(nameof(weight), "Вес должен быть больше нуля.");
        _price = price;
        _weight = weight;
    }

    public override Money GetPrice() => _price;
    public override bool RequiresShipping => true;
    public override decimal GetWeight() => _weight;
}

public sealed class DigitalProduct : Product
{
    private readonly Money _price;
    public decimal FileSizeMb { get; }

    public DigitalProduct(string name, Money price, decimal fileSizeMb) : base(name)
    {
        if (fileSizeMb <= 0)
            throw new ArgumentOutOfRangeException(nameof(fileSizeMb), "Размер файла должен быть больше нуля.");
        _price = price;
        FileSizeMb = fileSizeMb;
    }

    public override Money GetPrice() => _price;
    public override bool RequiresShipping => false;
    public override decimal GetWeight() => 0m;
}

public sealed class SubscriptionProduct : Product
{
    private readonly Money _monthlyPrice;
    public int Months { get; }

    public SubscriptionProduct(string name, Money monthlyPrice, int months) : base(name)
    {
        if (months < 1)
            throw new ArgumentOutOfRangeException(nameof(months), "Срок должен быть не меньше одного месяца.");
        _monthlyPrice = monthlyPrice;
        Months = months;
    }

    public override Money GetPrice()
    {
        Money total = _monthlyPrice * Months;
        return Months >= 12 ? total * 0.90m : total;
    }

    public override bool RequiresShipping => false;
    public override decimal GetWeight() => 0m;
}

public sealed class OrderItem
{
    public Product Product { get; }
    public int Quantity { get; }

    public OrderItem(Product product, int quantity)
    {
        Product = product ?? throw new ArgumentNullException(nameof(product));
        if (quantity < 1)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Количество должно быть не меньше 1.");
        Quantity = quantity;
    }

    public Money GetTotal() => Product.GetPrice() * Quantity;
}

public sealed class Order
{
    private readonly List<OrderItem> _items = new();
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();

    public void AddProduct(Product product) => AddProduct(product, 1);
    public void AddProduct(Product product, int quantity) => _items.Add(new OrderItem(product, quantity));

    public Money GetProductsTotal()
    {
        Money total = Money.Zero;
        foreach (OrderItem item in _items)
            total += item.GetTotal();
        return total;
    }

    public bool HasPhysicalProducts() => _items.Any(item => item.Product.RequiresShipping);

    public decimal GetTotalWeight()
    {
        decimal total = 0m;
        foreach (OrderItem item in _items)
            if (item.Product.RequiresShipping)
                total += item.Product.GetWeight() * item.Quantity;
        return total;
    }
}

public abstract class DiscountPolicy
{
    public abstract Money CalculateDiscount(Order order);
}

public sealed class NoDiscount : DiscountPolicy
{
    public override Money CalculateDiscount(Order order) => Money.Zero;
}

public sealed class PercentageDiscount : DiscountPolicy
{
    private readonly decimal _percentage;

    public PercentageDiscount(decimal percentage)
    {
        if (percentage < 0m || percentage > 100m)
            throw new ArgumentOutOfRangeException(nameof(percentage), "Скидка должна быть от 0 до 100 процентов.");
        _percentage = percentage;
    }

    public override Money CalculateDiscount(Order order) =>
        order.GetProductsTotal() * (_percentage / 100m);
}

public sealed class FixedDiscount : DiscountPolicy
{
    private readonly Money _amount;
    public FixedDiscount(Money amount) => _amount = amount;

    public override Money CalculateDiscount(Order order)
    {
        Money total = order.GetProductsTotal();
        return _amount.Amount > total.Amount ? total : _amount;
    }
}

public abstract class DeliveryMethod
{
    public abstract string Name { get; }
    public abstract bool IsSuitable(Order order);
    public abstract Money CalculateCost(Order order);
}

public sealed class CourierDelivery : DeliveryMethod
{
    public override string Name => "Курьерская доставка";
    public override bool IsSuitable(Order order) => order.HasPhysicalProducts();
    public override Money CalculateCost(Order order) =>
        new Money(300m) + new Money(80m) * order.GetTotalWeight();
}

public sealed class PickupDelivery : DeliveryMethod
{
    public override string Name => "Самовывоз";
    public override bool IsSuitable(Order order) => order.HasPhysicalProducts();
    public override Money CalculateCost(Order order) => Money.Zero;
}

public sealed class DigitalDelivery : DeliveryMethod
{
    public override string Name => "Цифровая доставка";
    public override bool IsSuitable(Order order) => !order.HasPhysicalProducts();
    public override Money CalculateCost(Order order) => Money.Zero;
}

public interface IPaymentMethod
{
    void Pay(Money amount);
}

public sealed class CardPayment : IPaymentMethod
{
    public void Pay(Money amount) =>
        Console.WriteLine($"Оплата банковской картой успешна: {amount}");
}

public sealed class CashPayment : IPaymentMethod
{
    private readonly Money _cash;
    public CashPayment(Money cash) => _cash = cash;

    public void Pay(Money amount)
    {
        if (_cash.Amount < amount.Amount)
            throw new InvalidOperationException("Недостаточно наличных для оплаты.");

        Money change = _cash - amount;
        Console.WriteLine($"Получено наличными: {_cash}");
        Console.WriteLine($"Сдача: {change}");
        Console.WriteLine("Оплата наличными успешна.");
    }
}

public sealed class CheckoutService
{
    public Money CalculateTotal(
        Order order,
        DiscountPolicy discountPolicy,
        DeliveryMethod deliveryMethod,
        out Money productsTotal,
        out Money discount,
        out Money deliveryCost)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(discountPolicy);
        ArgumentNullException.ThrowIfNull(deliveryMethod);

        if (order.Items.Count == 0)
            throw new InvalidOperationException("Нельзя оформить пустой заказ.");

        if (!deliveryMethod.IsSuitable(order))
            throw new InvalidOperationException(
                $"Способ доставки «{deliveryMethod.Name}» не подходит для этого заказа.");

        productsTotal = order.GetProductsTotal();
        discount = discountPolicy.CalculateDiscount(order);

        if (discount.Amount > productsTotal.Amount)
            throw new InvalidOperationException("Скидка не может превышать стоимость товаров.");

        deliveryCost = deliveryMethod.CalculateCost(order);
        return productsTotal - discount + deliveryCost;
    }
}

internal static class Program
{
    private static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        try
        {
            Order order = BuildOrderInteractively();

            Console.WriteLine("\n=== СОСТАВ ЗАКАЗА ===");
            foreach (OrderItem item in order.Items)
                Console.WriteLine($"{item.Product.Name} | Количество: {item.Quantity} | " +
                                  $"Цена за единицу: {item.Product.GetPrice()} | Сумма: {item.GetTotal()}");

            DiscountPolicy discountPolicy = ReadDiscountPolicy();
            DeliveryMethod deliveryMethod = ReadDeliveryMethod(order);

            var checkout = new CheckoutService();
            Money total = checkout.CalculateTotal(
                order, discountPolicy, deliveryMethod,
                out Money productsTotal, out Money discount, out Money deliveryCost);

            Console.WriteLine("\n=== РАСЧЁТ ЗАКАЗА ===");
            Console.WriteLine($"Стоимость товаров: {productsTotal}");
            Console.WriteLine($"Скидка:             {discount}");
            Console.WriteLine($"Доставка:           {deliveryCost}");
            Console.WriteLine($"ИТОГО:              {total}");

            Console.WriteLine("\n=== ОПЛАТА ===");
            IPaymentMethod payment = ReadPaymentMethod(total);
            payment.Pay(total);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            Console.WriteLine($"Некорректные данные: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"Ошибка операции: {ex.Message}");
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine($"Ошибка аргумента: {ex.Message}");
        }
    }

    private static Order BuildOrderInteractively()
    {
        var order = new Order();

        Console.WriteLine("=== ВВОД ЗАКАЗА ===");
        Console.Write("Сколько товаров вы хотите добавить? ");
        int count = ReadInt(1, 100);

        for (int i = 1; i <= count; i++)
        {
            Console.WriteLine($"\n--- Товар #{i} ---");
            Product product = ReadProduct();
            Console.Write("Количество: ");
            int qty = ReadInt(1, 10_000);
            order.AddProduct(product, qty);
        }

        return order;
    }

    private static Product ReadProduct()
    {
        Console.WriteLine("Тип товара:");
        Console.WriteLine("  1 — Физический товар");
        Console.WriteLine("  2 — Цифровой товар");
        Console.WriteLine("  3 — Подписка");
        Console.Write("Ваш выбор: ");
        int type = ReadInt(1, 3);

        Console.Write("Название: ");
        string name = ReadNonEmptyString();

        Console.Write("Цена (₽): ");
        decimal price = ReadDecimal(0m, 1_000_000_000m);

        switch (type)
        {
            case 1:
                Console.Write("Вес (кг): ");
                decimal weight = ReadDecimal(0.001m, 100_000m);
                return new PhysicalProduct(name, new Money(price), weight);

            case 2:
                Console.Write("Размер файла (МБ): ");
                decimal size = ReadDecimal(0.001m, 1_000_000m);
                return new DigitalProduct(name, new Money(price), size);

            case 3:
                Console.Write("Срок подписки (мес.): ");
                int months = ReadInt(1, 1200);
                return new SubscriptionProduct(name, new Money(price), months);

            default:
                throw new InvalidOperationException("Неизвестный тип товара.");
        }
    }

    private static DiscountPolicy ReadDiscountPolicy()
    {
        Console.WriteLine("\n=== СКИДКА ===");
        Console.WriteLine("  1 — Без скидки");
        Console.WriteLine("  2 — Процентная скидка");
        Console.WriteLine("  3 — Фиксированная скидка");
        Console.Write("Ваш выбор: ");
        int type = ReadInt(1, 3);

        switch (type)
        {
            case 1:
                return new NoDiscount();

            case 2:
                Console.Write("Процент скидки (0–100): ");
                decimal percent = ReadDecimal(0m, 100m);
                return new PercentageDiscount(percent);

            case 3:
                Console.Write("Сумма скидки (₽): ");
                decimal amount = ReadDecimal(0m, 1_000_000_000m);
                return new FixedDiscount(new Money(amount));

            default:
                throw new InvalidOperationException("Неизвестный тип скидки.");
        }
    }

    private static DeliveryMethod ReadDeliveryMethod(Order order)
    {
        Console.WriteLine("\n=== ДОСТАВКА ===");
        Console.WriteLine("  1 — Курьерская доставка");
        Console.WriteLine("  2 — Самовывоз");
        Console.WriteLine("  3 — Цифровая доставка");
        Console.Write("Ваш выбор: ");
        int type = ReadInt(1, 3);

        DeliveryMethod method = type switch
        {
            1 => new CourierDelivery(),
            2 => new PickupDelivery(),
            3 => new DigitalDelivery(),
            _ => throw new InvalidOperationException("Неизвестный способ доставки.")
        };

        if (!method.IsSuitable(order))
            throw new InvalidOperationException(
                $"Способ доставки «{method.Name}» не подходит для этого заказа.");

        return method;
    }

    private static IPaymentMethod ReadPaymentMethod(Money total)
    {
        Console.WriteLine("  1 — Банковская карта");
        Console.WriteLine("  2 — Наличные");
        Console.Write("Ваш выбор: ");
        int type = ReadInt(1, 2);

        if (type == 1)
            return new CardPayment();

        Console.Write($"Введите сумму наличных (не меньше {total}): ");
        decimal cash = ReadDecimal(total.Amount, 1_000_000_000m);
        return new CashPayment(new Money(cash));
    }

    private static string ReadNonEmptyString()
    {
        while (true)
        {
            string? value = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
            Console.Write("Значение не может быть пустым. Повторите ввод: ");
        }
    }

    private static int ReadInt(int min, int max)
    {
        while (true)
        {
            string? input = Console.ReadLine();
            if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                && value >= min && value <= max)
                return value;

            Console.Write($"Введите целое число от {min} до {max}: ");
        }
    }

    private static decimal ReadDecimal(decimal min, decimal max)
    {
        while (true)
        {
            string? input = Console.ReadLine();
            if (decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value)
                && value >= min && value <= max)
                return value;

            if (decimal.TryParse(input, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
                && value >= min && value <= max)
                return value;

            Console.Write($"Введите число от {min} до {max}: ");
        }
    }
}