using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Interfaces.Repositories;
using GMSoft.Domain.Entities;
using MediatR;

namespace GMSoft.Application.Features.Customers.Delete;

public class DeleteCustomerCommandHandler : IRequestHandler<DeleteCustomerCommand>
{
    private readonly ICustomerRepository _customers;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteCustomerCommandHandler(ICustomerRepository customers, IUnitOfWork unitOfWork)
    {
        _customers  = customers;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await _customers.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.Id);

        // Un cliente con historia no se elimina. Ademas de romper la trazabilidad de
        // sus visitas, borrarlo esconderia los envases que todavia tiene en su poder.
        if (await _customers.HasHistoryAsync(request.Id, cancellationToken))
            throw new ConflictException(
                "Este cliente ya tiene visitas, envases o pagos y no se puede eliminar. " +
                "Desactivalo: sale del recorrido y su cuenta y sus envases quedan a la vista.");

        _customers.Delete(customer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
