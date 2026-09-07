using CSharpFunctionalExtensions;
using Domain.Dto.Responces;

namespace Domain.Entitys.Users.Interfaces;

public interface IUserRepository
{
    string DatabaseName();
    Task<Result<UserEntity>> ByName(string login);
    Task<Result<UserEntity>> GetById(string id);
    Task<Result> Create(UserEntity entity);
    Task<Result> Update(UserEntity entity);
    Task<Result> Delete(string id);
    Task<PaginatedResponse<UserEntity>> List(int pageNumber, int pageSize);
    Task<List<UserEntity>> All();
}