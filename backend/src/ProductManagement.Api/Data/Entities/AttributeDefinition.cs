namespace ProductManagement.Api.Data.Entities;

public class AttributeDefinition
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = "string";
    public ICollection<ProductAttributeValue> Values { get; set; } = new List<ProductAttributeValue>();
}
