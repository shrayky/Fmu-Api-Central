using Application.AlertTemplates;
using CSharpFunctionalExtensions;
using Domain.Dto.Responces;
using Domain.Entitys.AlertTemplates;
using Domain.Entitys.AlertTemplates.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.Tests;

public class AlertTemplateManagerTests
{
    [Fact]
    public async Task EnsureDefaults_добавляет_отсутствующий_шаблон_сертификатов()
    {
        var repository = new MemoryTemplates();
        repository.Items.Add(new AlertTemplateEntity { Id = "check-online-nodes", Name = "есть" });
        var sut = new AlertTemplateManager(NullLogger<AlertTemplateManager>.Instance, repository);

        var result = await sut.EnsureDefaults();

        Assert.True(result.IsSuccess);
        Assert.Contains(repository.Items, template => template.Id == "check-organization-certificates");
        Assert.Contains(repository.Items, template => template.Id == "check-cryptopro-license");
        Assert.Equal(1, repository.Items.Count(template => template.Id == "check-online-nodes"));
    }

    private sealed class MemoryTemplates : IAlertTemplateRepository
    {
        public List<AlertTemplateEntity> Items { get; } = [];

        public Task<Result> Create(AlertTemplateEntity entity)
        {
            Items.Add(entity);
            return Task.FromResult(Result.Success());
        }

        public Task<Result> Update(AlertTemplateEntity entity) => throw new NotImplementedException();

        public Task<Result> Delete(string id) => throw new NotImplementedException();

        public Task<Result<AlertTemplateEntity>> GetById(string id) => throw new NotImplementedException();

        public Task<PaginatedResponse<AlertTemplateEntity>> List(int pageNumber, int pageSize)
            => throw new NotImplementedException();

        public Task<List<AlertTemplateEntity>> All() => Task.FromResult(Items.ToList());

        public Task<List<AlertTemplateEntity>> AllEnabled() => throw new NotImplementedException();
    }
}
