using GMSoft.Application.Features.Reports.Common;
using MediatR;

namespace GMSoft.Application.Features.Reports.ContainersOut;

/// <summary>Cuantos envases hay en la calle, por producto. Sin paginar: es una linea por producto.</summary>
public record GetContainersOutReportQuery : IRequest<IReadOnlyList<ContainersOutLineDto>>;
