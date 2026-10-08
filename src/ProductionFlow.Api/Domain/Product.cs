namespace ProductionFlow.Api.Domain;

public sealed class Product
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Code { get; private set; }
    public string Name { get; private set; }

    public Product(string code, string name)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException(
                "Kod produktu jest wymagany.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Nazwa produktu jest wymagana.", nameof(name));
        }

        Code = code.Trim();
        Name = name.Trim();
    }
}