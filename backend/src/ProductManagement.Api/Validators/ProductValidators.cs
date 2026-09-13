using FluentValidation;
using ProductManagement.Api.Dtos;

namespace ProductManagement.Api.Validators;

public class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description is not null);
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Variants).NotEmpty().WithMessage("At least one variant is required.");
        RuleForEach(x => x.Variants).SetValidator(new ProductVariantDtoValidator());
        RuleFor(x => x.Variants)
            .Must(HaveUniqueSkus)
            .WithMessage("Variant SKUs must be unique within the request.");
        When(x => x.Attributes is not null, () =>
        {
            RuleForEach(x => x.Attributes!).SetValidator(new ProductAttributeValueDtoValidator());
            RuleFor(x => x.Attributes)
                .Must(HaveUniqueAttributeDefinitions)
                .WithMessage("Attribute definitions must be unique within the request.");
        });
    }

    private static bool HaveUniqueSkus(IReadOnlyList<ProductVariantDto> variants)
    {
        var skus = variants.Select(v => v.Sku.Trim().ToUpperInvariant()).ToList();
        return skus.Count == skus.Distinct().Count();
    }

    private static bool HaveUniqueAttributeDefinitions(IReadOnlyList<ProductAttributeValueDto>? attributes)
    {
        if (attributes is null || attributes.Count == 0)
        {
            return true;
        }

        var ids = attributes.Select(a => a.AttributeDefinitionId).ToList();
        return ids.Count == ids.Distinct().Count();
    }
}

public class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description is not null);
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.RowVersion).NotEmpty();
        RuleFor(x => x.Variants).NotEmpty().WithMessage("At least one variant is required.");
        RuleForEach(x => x.Variants).SetValidator(new ProductVariantDtoValidator());
        RuleFor(x => x.Variants)
            .Must(HaveUniqueSkus)
            .WithMessage("Variant SKUs must be unique within the request.");
        When(x => x.Attributes is not null, () =>
        {
            RuleForEach(x => x.Attributes!).SetValidator(new ProductAttributeValueDtoValidator());
            RuleFor(x => x.Attributes)
                .Must(HaveUniqueAttributeDefinitions)
                .WithMessage("Attribute definitions must be unique within the request.");
        });
    }

    private static bool HaveUniqueSkus(IReadOnlyList<ProductVariantDto> variants)
    {
        var skus = variants.Select(v => v.Sku.Trim().ToUpperInvariant()).ToList();
        return skus.Count == skus.Distinct().Count();
    }

    private static bool HaveUniqueAttributeDefinitions(IReadOnlyList<ProductAttributeValueDto>? attributes)
    {
        if (attributes is null || attributes.Count == 0)
        {
            return true;
        }

        var ids = attributes.Select(a => a.AttributeDefinitionId).ToList();
        return ids.Count == ids.Distinct().Count();
    }
}

public class ProductVariantDtoValidator : AbstractValidator<ProductVariantDto>
{
    public ProductVariantDtoValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Size).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Color).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.StockQuantity).GreaterThanOrEqualTo(0);
    }
}

public class ProductAttributeValueDtoValidator : AbstractValidator<ProductAttributeValueDto>
{
    public ProductAttributeValueDtoValidator()
    {
        RuleFor(x => x.AttributeDefinitionId).NotEmpty();
        RuleFor(x => x.Value).NotEmpty().MaximumLength(500);
    }
}
