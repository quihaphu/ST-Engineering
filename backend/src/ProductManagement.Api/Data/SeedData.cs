using Microsoft.EntityFrameworkCore;
using ProductManagement.Api.Data.Entities;

namespace ProductManagement.Api.Data;

public static class SeedData
{
    public static readonly Guid CategoryMen = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid CategoryWomen = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid CategoryKids = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid CategoryAccessories = Guid.Parse("44444444-4444-4444-4444-444444444444");

    public static readonly Guid AttrMaterial = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid AttrBrand = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public static readonly Guid AttrCare = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    public static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>().HasData(
            new Category { Id = CategoryMen, Name = "Men" },
            new Category { Id = CategoryWomen, Name = "Women" },
            new Category { Id = CategoryKids, Name = "Kids" },
            new Category { Id = CategoryAccessories, Name = "Accessories" });

        modelBuilder.Entity<AttributeDefinition>().HasData(
            new AttributeDefinition { Id = AttrMaterial, Name = "Material", DataType = "string" },
            new AttributeDefinition { Id = AttrBrand, Name = "Brand", DataType = "string" },
            new AttributeDefinition { Id = AttrCare, Name = "CareInstructions", DataType = "string" });
    }
}
