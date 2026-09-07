using CSharpFunctionalExtensions;
using Domain.Dto.Responces;
using Domain.Entitys.Users.Dto;

namespace Domain.Entitys.Users.Interfaces;

public interface IUsersManagerService
{
    Task<Result> Create(UserView data);
    Task<Result> Update(UserView data);
    Task<Result> Delete(string id);
    Task<PaginatedResponse<UserView>> List(int pageNumber, int pageSize);
    Task<Result> ChangePassword(string id, string password);
}
