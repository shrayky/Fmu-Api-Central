using Domain.Attributes;
using Domain.Configuration.Constants;
using Domain.Entitys.Instance.Interfaces;
using Shared.FilesFolders;

namespace Application.Instance.Services;

[AutoRegisterService]
public class CheckerTemplateLocator : ICheckerTemplateLocator
{
    public string Folder => Path.Combine(
        Folders.CommonApplicationDataFolder(ApplicationInformation.Manufacture, ApplicationInformation.Name),
        "instance-template");
}