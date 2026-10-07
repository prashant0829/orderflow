using MediatR;
using OrderFlow.Identity.Application.Abstractions;
using OrderFlow.Identity.Application.Auth.Dtos;
using OrderFlow.SharedKernel;

namespace OrderFlow.Identity.Application.Auth.Queries;

public record GetCurrentUserQuery(Guid UserId) : IRequest<UserDto>;

public class GetCurrentUserQueryHandler(IUserRepository users) : IRequestHandler<GetCurrentUserQuery, UserDto>
{
    public async Task<UserDto> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken) =>
        (await users.GetByIdAsync(request.UserId, cancellationToken) ?? throw new NotFoundException("User", request.UserId)).ToDto();
}
