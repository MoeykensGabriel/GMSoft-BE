using FluentValidation;

namespace GMSoft.Application.Features.Sessions.Close;

public class CloseSessionCommandValidator : AbstractValidator<CloseSessionCommand>
{
    public CloseSessionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.KilometersAtClose)
            .GreaterThanOrEqualTo(0).WithMessage("El kilometraje no puede ser negativo.");

        RuleFor(x => x.Returns).NotNull();

        RuleForEach(x => x.Returns).ChildRules(linea =>
        {
            linea.RuleFor(l => l.ProductId).NotEmpty();
            linea.RuleFor(l => l.State).IsInEnum();
            linea.RuleFor(l => l.Quantity)
                .GreaterThan(0).WithMessage("Devolver cero unidades no es una devolucion.");
        });

        // Repetir producto y estado duplica la descarga y tapa un faltante real.
        RuleFor(x => x.Returns)
            .Must(r => r.Select(l => (l.ProductId, l.State)).Distinct().Count() == r.Count)
            .WithMessage("Hay lineas repetidas en la descarga.");

        // Cero es valido: dejar constancia de que llego cero es distinto de no haber rendido.
        RuleFor(x => x.Settlement!.AmountReceived)
            .GreaterThanOrEqualTo(0).WithMessage("El efectivo entregado no puede ser negativo.")
            .LessThanOrEqualTo(Correction.CorrectReceptionCommandValidator.MaxAmount)
            .When(x => x.Settlement is not null);
        RuleFor(x => x.Settlement!.Notes).MaximumLength(500).When(x => x.Settlement is not null);
    }
}
